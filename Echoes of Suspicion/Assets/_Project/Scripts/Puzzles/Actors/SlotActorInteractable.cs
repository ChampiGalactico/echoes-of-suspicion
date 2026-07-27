using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Companion component for SlotActor that makes it interactable
    /// through the unified raycast system (NetworkRatInteractor).
    ///
    /// When the player presses E while looking at this slot AND their
    /// active inventory slot holds a puzzle item, the item is placed.
    ///
    /// Attach to the same GameObject as SlotActor.
    /// </summary>
    [RequireComponent(typeof(SlotActor))]
    public class SlotActorInteractable : RatInteractable
    {
        private SlotActor slotActor;

        private void Awake()
        {
            slotActor = GetComponent<SlotActor>();
        }

        public override bool CanPreviewInteraction(GameObject interactor)
        {
            if (slotActor == null || slotActor.HasItem || !slotActor.CanInteract)
            {
                return false;
            }

            // Only show prompt if the player has a puzzle item in their active slot.
            NetworkInventory inventory =
                interactor.GetComponent<NetworkInventory>();

            if (inventory == null)
            {
                return false;
            }

            return inventory.ActiveSlot.IsPuzzleItem;
        }

        [Server]
        public override bool CanServerInteract(NetworkIdentity interactor)
        {
            if (slotActor == null || slotActor.HasItem || !slotActor.CanInteract)
            {
                return false;
            }

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

            InventorySlot activeSlot = inventory.ActiveSlot;
            if (!activeSlot.IsPuzzleItem)
            {
                return false;
            }

            // Resolve the PickableItem and check if the slot accepts it.
            PickableItem pickable = ResolvePickableItem(activeSlot.itemNetId);
            return pickable != null;
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

            InventorySlot activeSlot = inventory.ActiveSlot;
            PickableItem pickable = ResolvePickableItem(activeSlot.itemNetId);

            if (pickable == null)
            {
                return;
            }

            // Try to place in the puzzle slot.
            if (!slotActor.TryPlace(pickable))
            {
                return;
            }

            // Tell the NetworkPickupItem to move to the snap point (stays hidden).
            NetworkPickupItem pickupItem =
                pickable.GetComponent<NetworkPickupItem>();

            if (pickupItem != null)
            {
                Vector3 snapPos = slotActor.SnapPosition;
                pickupItem.PlaceInSlot(snapPos);
            }

            // Remove from inventory.
            inventory.ServerRemoveItem(inventory.ActiveSlotIndex);
        }

        private static PickableItem ResolvePickableItem(uint netId)
        {
            if (netId == 0)
            {
                return null;
            }

            var spawnedTable = NetworkServer.active
                ? NetworkServer.spawned
                : NetworkClient.spawned;

            if (!spawnedTable.TryGetValue(netId, out NetworkIdentity identity))
            {
                return null;
            }

            return identity.GetComponent<PickableItem>();
        }
    }
}
