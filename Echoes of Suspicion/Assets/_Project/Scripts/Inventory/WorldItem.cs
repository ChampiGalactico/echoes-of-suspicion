using Mirror;
using UnityEngine;

/// <summary>
/// OBSOLETE — Use NetworkPickupItem instead.
/// This class is kept only to avoid breaking existing prefab references.
/// Migrate any remaining prefabs to NetworkPickupItem, then delete this file.
/// </summary>
[System.Obsolete("Use NetworkPickupItem instead.")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class WorldItem : RatInteractable
{
    [Header("Item")]
    [SerializeField]
    private ItemData itemData;

    [Header("Physics")]
    [SerializeField]
    private Rigidbody itemRigidbody;

    [SyncVar]
    private float currentDurability = -1f;

    public ItemData ItemData => itemData;
    public float CurrentDurability => currentDurability;

    private void Awake()
    {
        if (itemRigidbody == null)
        {
            itemRigidbody = GetComponent<Rigidbody>();
        }
    }

    public override bool CanPreviewInteraction(GameObject interactor)
    {
        return base.CanPreviewInteraction(interactor);
    }

    [Server]
    public override bool CanServerInteract(NetworkIdentity interactor)
    {
        if (interactor == null)
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

        int slotUsed = inventory.ServerAddItem(itemData);

        if (slotUsed < 0)
        {
            return;
        }

        // Restore durability if this item had one (e.g. a dropped flashlight).
        if (currentDurability >= 0f)
        {
            inventory.ServerSetDurability(slotUsed, currentDurability);
        }

        NetworkServer.Destroy(gameObject);
    }

    /// <summary>
    /// Set the durability of this world item (called when dropping from inventory).
    /// </summary>
    [Server]
    public void ServerSetDurability(float durability)
    {
        currentDurability = durability;
    }
}
