using Mirror;
using UnityEngine;

/// <summary>
/// Spawns a visual model in the player's hand based on the active inventory slot.
/// All players see each other's held items via SyncVar.
///
/// Attach to the player prefab. Requires RatHoldSocketProvider for the hold socket.
/// </summary>
[DisallowMultipleComponent]
public class NetworkHeldItemVisual : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private NetworkInventory inventory;

    [SerializeField]
    private RatHoldSocketProvider socketProvider;

    // ── Synced state ──────────────────────────────────────────

    /// <summary>
    /// The item registry ID of what's currently shown in hand.
    /// -1 = nothing. Synced so all clients spawn the correct visual.
    /// </summary>
    [SyncVar(hook = nameof(OnHeldItemIdChanged))]
    private int heldItemId = -1;

    // ── Local visual instance ─────────────────────────────────

    private GameObject currentVisualInstance;
    private int localVisualItemId = -1;

    // ── Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<NetworkInventory>();
        if (socketProvider == null)
            socketProvider = GetComponent<RatHoldSocketProvider>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        inventory.OnActiveSlotChanged += ServerOnSlotChanged;
        inventory.OnInventoryChanged += ServerRefreshHeldItem;

        // Set initial visual.
        ServerRefreshHeldItem();
    }

    public override void OnStopServer()
    {
        if (inventory != null)
        {
            inventory.OnActiveSlotChanged -= ServerOnSlotChanged;
            inventory.OnInventoryChanged -= ServerRefreshHeldItem;
        }

        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Spawn visual for the current synced state.
        RefreshVisual(heldItemId);
    }

    public override void OnStopClient()
    {
        DestroyVisual();
        base.OnStopClient();
    }

    // ── Server: track what the active slot holds ──────────────

    private void ServerOnSlotChanged(int newIndex)
    {
        ServerRefreshHeldItem();
    }

    [Server]
    private void ServerRefreshHeldItem()
    {
        InventorySlot slot = inventory.ActiveSlot;
        heldItemId = slot.IsEmpty ? -1 : slot.itemId;
    }

    // ── Client: spawn/destroy the visual model ────────────────

    private void OnHeldItemIdChanged(int oldId, int newId)
    {
        RefreshVisual(newId);
    }

    private void RefreshVisual(int itemId)
    {
        // Same item already shown?
        if (itemId == localVisualItemId && currentVisualInstance != null)
            return;

        DestroyVisual();

        if (itemId < 0 || ItemRegistry.Instance == null)
        {
            localVisualItemId = -1;
            return;
        }

        ItemData data = ItemRegistry.Instance.GetData(itemId);
        if (data == null)
        {
            localVisualItemId = -1;
            return;
        }

        GameObject prefab = data.heldModelPrefab != null
            ? data.heldModelPrefab
            : data.worldPrefab;

        if (prefab == null)
        {
            localVisualItemId = -1;
            return;
        }

        Transform holdSocket = null;
        if (socketProvider != null)
            socketProvider.TryGetHoldSocket(out holdSocket);

        if (holdSocket == null)
        {
            localVisualItemId = -1;
            return;
        }

        currentVisualInstance = Instantiate(prefab, holdSocket);

        // Strip non-visual components first — this clone is visual only.
        StripNonVisual(currentVisualInstance);

        // Apply transform after stripping so nothing overrides it.
        currentVisualInstance.transform.localPosition = Vector3.zero;
        currentVisualInstance.transform.localRotation = Quaternion.identity;
        currentVisualInstance.transform.localScale = prefab.transform.localScale * data.heldScale;

        localVisualItemId = itemId;
    }

    private void DestroyVisual()
    {
        if (currentVisualInstance != null)
        {
            Destroy(currentVisualInstance);
            currentVisualInstance = null;
        }

        localVisualItemId = -1;
    }

    /// <summary>
    /// Disable all non-visual components on the held model clone.
    /// We disable instead of destroying to avoid RequireComponent conflicts
    /// (e.g. NetworkPickupItem requires Rigidbody).
    /// </summary>
    private static void StripNonVisual(GameObject obj)
    {
        // Disable all MonoBehaviours (NetworkPickupItem, PickableItem, etc.)
        foreach (MonoBehaviour mb in obj.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(mb);

        // Disable all NetworkIdentities.
        foreach (Mirror.NetworkIdentity ni in obj.GetComponentsInChildren<Mirror.NetworkIdentity>(true))
            Destroy(ni);

        // Now safe to destroy Rigidbodies (no RequireComponent left).
        foreach (Rigidbody rb in obj.GetComponentsInChildren<Rigidbody>(true))
            Destroy(rb);

        // Disable colliders so the visual doesn't block raycasts.
        foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }

    /// <summary>
    /// Called externally (by throw/drop system) to immediately clear the visual
    /// before the inventory sync arrives, so there's no visual pop.
    /// </summary>
    public void ClearVisualImmediate()
    {
        DestroyVisual();
    }
}
