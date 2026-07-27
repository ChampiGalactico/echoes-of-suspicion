using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Networked inventory with a fixed number of slots plus a separate battery counter.
/// Attach to the player prefab alongside NetworkRatInteractor.
///
/// All items use the hide/reveal pattern: the original world object is hidden
/// on pickup and revealed on drop/throw. No objects are destroyed or respawned.
///
/// Batteries do NOT consume inventory slots — they are tracked as a simple synced int.
/// </summary>
[DisallowMultipleComponent]
public class NetworkInventory : NetworkBehaviour
{
    public const int SlotCount = 5;

    [Header("Default Loadout")]
    [Tooltip("Items placed in the inventory on spawn. Index = slot. Null entries are skipped.")]
    [SerializeField]
    private ItemData[] defaultItems = new ItemData[SlotCount];

    [Header("Throw Settings")]
    [SerializeField, Min(1f)]
    [Tooltip("Base throw speed before strength multiplier. Adjust to taste.")]
    private float baseThrowSpeed = 12f;

    [SerializeField, Range(0f, 0.4f)]
    [Tooltip("Extra upward bias added to the throw direction for a nice arc. " +
             "0 = pure aim direction, 0.3 = noticeable lob.")]
    private float upwardBias = 0.15f;

    // ── Synced state ──────────────────────────────────────────

    private readonly SyncListInventorySlot slots = new SyncListInventorySlot();

    [SyncVar]
    private int activeSlotIndex;

    [SyncVar]
    private int batteryCount;

    // ── Public read-only accessors ────────────────────────────

    public int ActiveSlotIndex => activeSlotIndex;
    public int BatteryCount => batteryCount;

    public InventorySlot GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
        {
            return InventorySlot.Empty;
        }

        return slots[index];
    }

    public InventorySlot ActiveSlot =>
        GetSlot(activeSlotIndex);

    public ItemData GetItemData(int slotIndex)
    {
        InventorySlot slot = GetSlot(slotIndex);
        return slot.IsEmpty ? null : ItemRegistry.Instance.GetData(slot.itemId);
    }

    public ItemData ActiveItemData =>
        GetItemData(activeSlotIndex);

    // ── Events (client-side, for UI) ──────────────────────────

    /// <summary>Fired on every client when any slot changes.</summary>
    public event Action OnInventoryChanged;

    /// <summary>Fired on every client when the active slot index changes.</summary>
    public event Action<int> OnActiveSlotChanged;

    /// <summary>Fired on every client when the battery count changes.</summary>
    public event Action<int> OnBatteryCountChanged;

    // ── Lifecycle ─────────────────────────────────────────────

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Initialize empty slots.
        for (int i = 0; i < SlotCount; i++)
        {
            slots.Add(InventorySlot.Empty);
        }

        // Place default items.
        for (int i = 0; i < SlotCount && i < defaultItems.Length; i++)
        {
            if (defaultItems[i] == null)
            {
                continue;
            }

            int id = ItemRegistry.Instance.GetId(defaultItems[i]);
            if (id < 0)
            {
                Debug.LogWarning(
                    $"[Inventory] Default item '{defaultItems[i].itemName}' " +
                    "is not registered in the ItemRegistry.");
                continue;
            }

            slots[i] = new InventorySlot
            {
                itemId = id,
                count = 1,
                durability = -1f,
                itemNetId = 0,
                isPuzzle = false
            };
        }

        activeSlotIndex = 0;
        batteryCount = 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        slots.Callback += OnSlotsChanged;
    }

    private void OnSlotsChanged(
        SyncListInventorySlot.Operation op,
        int index,
        InventorySlot oldItem,
        InventorySlot newItem)
    {
        OnInventoryChanged?.Invoke();
    }

    // ── Commands (client → server) ────────────────────────────

    [Command]
    public void CmdSetActiveSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
        {
            return;
        }

        activeSlotIndex = index;

        // Notify server-side listeners.
        OnActiveSlotChanged?.Invoke(index);

        RpcNotifyActiveSlotChanged(index);
    }

    [ClientRpc]
    private void RpcNotifyActiveSlotChanged(int index)
    {
        // Avoid double-firing on host (server + client in one process).
        if (!isServer)
        {
            OnActiveSlotChanged?.Invoke(index);
        }
    }

    /// <summary>
    /// Try to add an item to the first available slot.
    /// Returns the slot index used, or -1 if inventory is full.
    /// Server-only.
    /// </summary>
    [Server]
    public int ServerAddItem(
        ItemData data,
        int count = 1,
        uint itemNetId = 0,
        bool isPuzzle = false)
    {
        int id = ItemRegistry.Instance.GetId(data);
        if (id < 0)
        {
            return -1;
        }

        // If stackable, look for an existing stack with room.
        // Stacking only works for items without a unique netId
        // (each hidden world object occupies its own slot).
        if (data.isStackable && itemNetId == 0)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot.itemId == id &&
                    slot.itemNetId == 0 &&
                    slot.count < data.maxStack)
                {
                    int available = data.maxStack - slot.count;
                    int toAdd = Mathf.Min(count, available);

                    slot.count += toAdd;
                    slots[i] = slot;
                    return i;
                }
            }
        }

        // Find first empty slot.
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i] = new InventorySlot
                {
                    itemId = id,
                    count = Mathf.Min(count, data.maxStack),
                    durability = -1f,
                    itemNetId = itemNetId,
                    isPuzzle = isPuzzle
                };

                return i;
            }
        }

        return -1; // Full.
    }

    /// <summary>Remove the item at slotIndex. Server-only.</summary>
    [Server]
    public void ServerRemoveItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            return;
        }

        slots[slotIndex] = InventorySlot.Empty;
    }

    /// <summary>Update durability of a slot. Server-only.</summary>
    [Server]
    public void ServerSetDurability(int slotIndex, float value)
    {
        if (slotIndex < 0 || slotIndex >= slots.Count)
        {
            return;
        }

        InventorySlot slot = slots[slotIndex];
        slot.durability = value;
        slots[slotIndex] = slot;
    }

    // ── Battery counter ───────────────────────────────────────

    [Server]
    public void ServerAddBatteries(int amount)
    {
        batteryCount += amount;
        RpcBatteryCountChanged(batteryCount);
    }

    /// <summary>
    /// Try to consume one battery. Returns true if successful.
    /// Server-only.
    /// </summary>
    [Server]
    public bool ServerConsumeBattery()
    {
        if (batteryCount <= 0)
        {
            return false;
        }

        batteryCount--;
        RpcBatteryCountChanged(batteryCount);
        return true;
    }

    [ClientRpc]
    private void RpcBatteryCountChanged(int newCount)
    {
        OnBatteryCountChanged?.Invoke(newCount);
    }

    // ── Drop active item ────────────────────────────────────────

    [Command]
    public void CmdDropActiveItem()
    {
        ServerDropItem(activeSlotIndex);
    }

    /// <summary>
    /// Drop the item at the given slot index.
    /// Finds the hidden world object by netId and reveals it at the drop position.
    /// Server-only.
    /// </summary>
    [Server]
    public void ServerDropItem(int slotIndex)
    {
        InventorySlot slot = GetSlot(slotIndex);
        if (slot.IsEmpty)
        {
            return;
        }

        Vector3 dropPos = transform.position + transform.forward * 1.2f;

        ServerRevealItem(slot, dropPos);
        ServerRemoveItem(slotIndex);
    }

    /// <summary>
    /// Find the hidden world object and reveal it at the given position.
    /// </summary>
    [Server]
    private bool ServerRevealItem(InventorySlot slot, Vector3 position)
    {
        if (slot.itemNetId == 0)
        {
            return false;
        }

        if (!NetworkServer.spawned.TryGetValue(
                slot.itemNetId, out NetworkIdentity identity))
        {
            return false;
        }

        NetworkPickupItem pickupItem =
            identity.GetComponent<NetworkPickupItem>();

        if (pickupItem == null)
        {
            return false;
        }

        pickupItem.Drop(position);
        return true;
    }

    /// <summary>
    /// Returns the item netId from the active slot, or 0 if none.
    /// </summary>
    public uint GetActiveItemNetId()
    {
        return ActiveSlot.itemNetId;
    }

    // ── Throw active item (parabolic) ─────────────────────────

    /// <summary>
    /// Throw the active item with physics.
    /// The client sends the camera forward direction; the server validates,
    /// reveals the hidden object, and applies force.
    /// </summary>
    [Command]
    public void CmdThrowActiveItem(Vector3 throwDirection)
    {
        InventorySlot slot = GetSlot(activeSlotIndex);
        if (slot.IsEmpty)
        {
            return;
        }

        // Validate direction.
        if (!IsFiniteVector(throwDirection) || throwDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        throwDirection = throwDirection.normalized;

        // Get strength multiplier.
        CharacterStatsProvider stats = GetComponent<CharacterStatsProvider>();
        float strengthMul = stats != null ? stats.StrengthMultiplier : 1f;

        ServerThrowItem(slot, throwDirection, strengthMul);
        ServerRemoveItem(activeSlotIndex);
    }

    [Server]
    private void ServerThrowItem(
        InventorySlot slot, Vector3 direction, float strengthMul)
    {
        if (slot.itemNetId == 0)
        {
            return;
        }

        if (!NetworkServer.spawned.TryGetValue(
                slot.itemNetId, out NetworkIdentity identity))
        {
            return;
        }

        NetworkPickupItem pickupItem =
            identity.GetComponent<NetworkPickupItem>();

        if (pickupItem == null)
        {
            return;
        }

        // Reveal at the player's position.
        Vector3 spawnPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.3f;
        pickupItem.Drop(spawnPos);

        ApplyThrowPhysics(pickupItem.gameObject, direction, strengthMul);
    }

    [Server]
    private void ApplyThrowPhysics(
        GameObject obj, Vector3 direction, float strengthMul)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            return;
        }

        // Ensure physics is active.
        rb.isKinematic = false;
        rb.useGravity = true;

        // Add upward bias for a parabolic arc.
        Vector3 biasedDirection = (direction + Vector3.up * upwardBias).normalized;

        float finalSpeed = baseThrowSpeed * strengthMul;

        // VelocityChange ignores mass — consistent feel across items.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(biasedDirection * finalSpeed, ForceMode.VelocityChange);

        // Add a bit of spin for visual flair.
        rb.AddTorque(
            UnityEngine.Random.insideUnitSphere * 5f,
            ForceMode.VelocityChange);
    }

    private static bool IsFiniteVector(Vector3 v)
    {
        return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
