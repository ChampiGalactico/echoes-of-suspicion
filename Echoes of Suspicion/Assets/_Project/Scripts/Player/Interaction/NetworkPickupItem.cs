using Mirror;
using UnityEngine;

/// <summary>
/// Unified pickup component for ALL inventory items.
///
/// When a player interacts, the object is hidden (not destroyed) and its
/// netId is stored in the inventory slot. On drop or throw, the same object
/// is revealed at the target position.
///
/// For puzzle items, add a PickableItem companion component alongside this one.
/// NetworkPickupItem handles all interaction and visibility; PickableItem
/// only holds PuzzleItemData for the puzzle system.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class NetworkPickupItem : RatInteractable
{
    [Header("Item")]
    [SerializeField]
    private ItemData itemData;

    // ── Synced state ──────────────────────────────────────────

    [SyncVar(hook = nameof(OnPickedUpChanged))]
    private bool isPickedUp;

    [SyncVar]
    private float currentDurability = -1f;

    // ── Local cache ───────────────────────────────────────────

    private Vector3 originPosition;
    private Quaternion originRotation;
    private Rigidbody itemRigidbody;

    // ── Public accessors ──────────────────────────────────────

    public ItemData ItemData => itemData;
    public bool IsPickedUp => isPickedUp;
    public float CurrentDurability => currentDurability;

    // ── Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        itemRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        originPosition = transform.position;
        originRotation = transform.rotation;
    }

    // ── RatInteractable overrides ─────────────────────────────

    public override bool CanPreviewInteraction(GameObject interactor)
    {
        return !isPickedUp && base.CanPreviewInteraction(interactor);
    }

    [Server]
    public override bool CanServerInteract(NetworkIdentity interactor)
    {
        if (isPickedUp || interactor == null)
        {
            return false;
        }

        NetworkInventory inventory =
            interactor.GetComponent<NetworkInventory>();

        if (inventory == null)
        {
            return false;
        }

        // Check if there's room.
        for (int i = 0; i < NetworkInventory.SlotCount; i++)
        {
            if (inventory.GetSlot(i).IsEmpty)
            {
                return true;
            }
        }

        return false;
    }

    [Server]
    public override void ServerInteract(NetworkIdentity interactor)
    {
        if (!CanServerInteract(interactor))
        {
            return;
        }

        NetworkInventory inventory =
            interactor.GetComponent<NetworkInventory>();

        // Check if this is a puzzle item.
        bool isPuzzle = GetComponent<EOS.Puzzles.PickableItem>() != null;

        int slotUsed = inventory.ServerAddItem(
            itemData,
            count: 1,
            itemNetId: netIdentity.netId,
            isPuzzle: isPuzzle);

        if (slotUsed < 0)
        {
            return;
        }

        // Restore durability if this item had one.
        if (currentDurability >= 0f)
        {
            inventory.ServerSetDurability(slotUsed, currentDurability);
        }

        // Hide the world object.
        isPickedUp = true;
        ServerSetPhysicsEnabled(false);

        if (isPuzzle)
        {
            EOS.Puzzles.PuzzleEvents.RaiseNoiseGenerated(
                transform.position,
                EOS.Puzzles.NoiseLevel.Low);
        }
    }

    // ── Server: drop / throw / puzzle placement ──────────────

    /// <summary>
    /// Reveal the item at the given position (used by drop and throw).
    /// </summary>
    [Server]
    public void Drop(Vector3 position)
    {
        transform.position = position;
        isPickedUp = false;
        ServerSetPhysicsEnabled(true);
    }

    /// <summary>
    /// Return the item to its original spawn position.
    /// </summary>
    [Server]
    public void ReturnToOrigin()
    {
        transform.position = originPosition;
        transform.rotation = originRotation;
        isPickedUp = false;
        ServerSetPhysicsEnabled(true);
    }

    /// <summary>
    /// Move the item to a snap point but keep it hidden.
    /// Used by SlotActor when the item is placed in a puzzle slot.
    /// </summary>
    [Server]
    public void PlaceInSlot(Vector3 snapPosition)
    {
        transform.position = snapPosition;
        // Stays picked up (hidden) — the SlotActor manages visual representation.
    }

    // ── Durability ────────────────────────────────────────────

    [Server]
    public void ServerSetDurability(float durability)
    {
        currentDurability = durability;
    }

    // ── Physics helpers ───────────────────────────────────────

    [Server]
    private void ServerSetPhysicsEnabled(bool enabled)
    {
        if (itemRigidbody != null)
        {
            if (!enabled)
            {
                // Zero out velocity BEFORE going kinematic.
                itemRigidbody.linearVelocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
            }

            itemRigidbody.isKinematic = !enabled;
            itemRigidbody.useGravity = enabled;
        }
    }

    // ── Visual sync (all clients) ─────────────────────────────

    private void OnPickedUpChanged(bool oldValue, bool newValue)
    {
        SetVisibility(!newValue);
    }

    private void SetVisibility(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
        }

        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            c.enabled = visible;
        }
    }
}
