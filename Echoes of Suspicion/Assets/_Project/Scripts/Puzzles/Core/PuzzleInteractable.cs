using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// How the player physically interacts with a Puzzle.
    /// This is the optional companion that bridges player input to the Puzzle brain.
    ///
    /// Interaction modes:
    ///   ToolUse   — tool stays in hand, animation/sound plays, then validates.
    ///   SlotPlace — item is removed from inventory and placed at the snap point.
    ///   Toggle    — press E to flip a boolean state.
    ///   Keypad    — (future) enter a code via UI.
    ///   Dial      — (future) rotate to a numeric value.
    ///
    /// Attach to the same GameObject as a Puzzle component.
    /// Requires a Collider (not trigger) on the Interactable layer for spherecast.
    /// </summary>
    [RequireComponent(typeof(Puzzle))]
    public class PuzzleInteractable : RatInteractable
    {
        public enum InteractionMode
        {
            ToolUse,
            SlotPlace,
            Toggle,
            Keypad,
            Dial,
        }

        [Header("* Mode")]
        [SerializeField] private InteractionMode _mode = InteractionMode.ToolUse;

        [Header("Item Filtering (optional, empty = accept any puzzle item)")]
        [SerializeField] private string[] _acceptedTags;

        [Header("SlotPlace Settings (optional)")]
        [SerializeField] private Transform _snapPoint;

        [Header("Audio (optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _useSound;

        // ─── State ───

        private Puzzle _puzzle;

        [SyncVar]
        private uint _placedItemNetId; // SlotPlace only

        [SyncVar]
        private bool _toggleState; // Toggle only

        public bool HasPlacedItem => _placedItemNetId != 0;

        // =====================================================================
        //  LIFECYCLE
        // =====================================================================

        private void Awake()
        {
            _puzzle = GetComponent<Puzzle>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Listen for server-side reset to clear placed items.
            if (_puzzle != null)
                _puzzle.OnServerReset += HandleServerReset;
        }

        private void OnDestroy()
        {
            if (_puzzle != null)
                _puzzle.OnServerReset -= HandleServerReset;
        }

        // =====================================================================
        //  CAN INTERACT — client preview + server gate
        // =====================================================================

        public override bool CanPreviewInteraction(GameObject interactor)
        {
            if (_puzzle == null || _puzzle.IsSolved) return false;
            if (!_puzzle.IsActive) return false;

            switch (_mode)
            {
                case InteractionMode.ToolUse:
                case InteractionMode.SlotPlace:
                    return HasValidItem(interactor) &&
                           (_mode != InteractionMode.SlotPlace || !HasPlacedItem);

                case InteractionMode.Toggle:
                case InteractionMode.Keypad:
                case InteractionMode.Dial:
                    return true;

                default:
                    return false;
            }
        }

        [Server]
        public override bool CanServerInteract(NetworkIdentity interactor)
        {
            if (_puzzle == null || _puzzle.IsSolved) return false;
            if (!_puzzle.IsActive) return false;
            if (interactor == null) return false;

            switch (_mode)
            {
                case InteractionMode.ToolUse:
                    return HasValidItemServer(interactor);

                case InteractionMode.SlotPlace:
                    return !HasPlacedItem && HasValidItemServer(interactor);

                case InteractionMode.Toggle:
                case InteractionMode.Keypad:
                case InteractionMode.Dial:
                    return true;

                default:
                    return false;
            }
        }

        // =====================================================================
        //  INTERACT — dispatches to mode handler
        // =====================================================================

        [Server]
        public override void ServerInteract(NetworkIdentity interactor)
        {
            if (!CanServerInteract(interactor)) return;

            switch (_mode)
            {
                case InteractionMode.ToolUse:
                    HandleToolUse(interactor);
                    break;

                case InteractionMode.SlotPlace:
                    HandleSlotPlace(interactor);
                    break;

                case InteractionMode.Toggle:
                    HandleToggle(interactor);
                    break;

                // Keypad and Dial are placeholders for now.
                case InteractionMode.Keypad:
                case InteractionMode.Dial:
                    break;
            }
        }

        // =====================================================================
        //  MODE HANDLERS
        // =====================================================================

        /// <summary>Tool stays in hand. Sound plays. Puzzle validates after useDelay.</summary>
        [Server]
        private void HandleToolUse(NetworkIdentity interactor)
        {
            PuzzleItemData itemData = GetHeldItemData(interactor);
            if (itemData == null) return;

            if (_useSound != null)
                RpcPlayUseSound();

            _puzzle.SubmitValue(itemData.ItemId, itemData.NumericValue, interactor);
        }

        /// <summary>Item removed from inventory and snapped to point.</summary>
        [Server]
        private void HandleSlotPlace(NetworkIdentity interactor)
        {
            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return;

            InventorySlot activeSlot = inventory.ActiveSlot;
            PickableItem pickable = ResolvePickableItem(activeSlot.itemNetId);
            if (pickable == null || pickable.ItemData == null) return;

            PuzzleItemData itemData = pickable.ItemData;

            // Store placed item reference.
            _placedItemNetId = pickable.GetComponent<NetworkIdentity>().netId;

            // Move item to snap point.
            NetworkPickupItem pickupItem = pickable.GetComponent<NetworkPickupItem>();
            if (pickupItem != null && _snapPoint != null)
                pickupItem.PlaceInSlot(_snapPoint.position);

            // Remove from player inventory.
            inventory.ServerRemoveItem(inventory.ActiveSlotIndex);

            // Make noise.
            PuzzleEvents.RaiseNoiseGenerated(transform.position, NoiseLevel.Low);

            // Submit to puzzle.
            _puzzle.SubmitValue(itemData.ItemId, itemData.NumericValue, interactor);
        }

        /// <summary>Flip boolean state on each press.</summary>
        [Server]
        private void HandleToggle(NetworkIdentity interactor)
        {
            _toggleState = !_toggleState;
            _puzzle.SubmitValue(
                _toggleState.ToString(),
                _toggleState ? 1f : 0f,
                interactor);
        }

        // =====================================================================
        //  RESET
        // =====================================================================

        [Server]
        private void HandleServerReset()
        {
            if (_mode == InteractionMode.SlotPlace)
                _placedItemNetId = 0;

            if (_mode == InteractionMode.Toggle)
                _toggleState = false;
        }

        // =====================================================================
        //  ITEM HELPERS
        // =====================================================================

        /// <summary>Client-side check: does the player hold a valid puzzle item?</summary>
        private bool HasValidItem(GameObject interactor)
        {
            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return false;

            InventorySlot slot = inventory.ActiveSlot;
            if (!slot.IsPuzzleItem) return false;

            // Tag filtering (client-side best-effort).
            if (_acceptedTags != null && _acceptedTags.Length > 0)
            {
                PickableItem pickable = ResolvePickableItem(slot.itemNetId);
                if (pickable == null || pickable.ItemData == null) return false;
                if (!IsTagAccepted(pickable.ItemData.ItemTag)) return false;
            }

            return true;
        }

        /// <summary>Server-side check: does the player hold a valid puzzle item?</summary>
        [Server]
        private bool HasValidItemServer(NetworkIdentity interactor)
        {
            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return false;

            InventorySlot slot = inventory.ActiveSlot;
            if (!slot.IsPuzzleItem) return false;

            PickableItem pickable = ResolvePickableItem(slot.itemNetId);
            if (pickable == null || pickable.ItemData == null) return false;

            if (_acceptedTags != null && _acceptedTags.Length > 0)
            {
                if (!IsTagAccepted(pickable.ItemData.ItemTag)) return false;
            }

            return true;
        }

        /// <summary>Get the PuzzleItemData of the player's held item (server).</summary>
        [Server]
        private PuzzleItemData GetHeldItemData(NetworkIdentity interactor)
        {
            var inventory = interactor.GetComponent<NetworkInventory>();
            if (inventory == null) return null;

            PickableItem pickable = ResolvePickableItem(inventory.ActiveSlot.itemNetId);
            return pickable != null ? pickable.ItemData : null;
        }

        private bool IsTagAccepted(string tag)
        {
            if (_acceptedTags == null || _acceptedTags.Length == 0) return true;
            foreach (var t in _acceptedTags)
            {
                if (t == tag) return true;
            }
            return false;
        }

        private static PickableItem ResolvePickableItem(uint netId)
        {
            if (netId == 0) return null;

            var table = NetworkServer.active
                ? NetworkServer.spawned
                : NetworkClient.spawned;

            if (!table.TryGetValue(netId, out NetworkIdentity identity))
                return null;

            return identity.GetComponent<PickableItem>();
        }

        // =====================================================================
        //  AUDIO RPC
        // =====================================================================

        [ClientRpc]
        private void RpcPlayUseSound()
        {
            if (_audioSource != null && _useSound != null)
                _audioSource.PlayOneShot(_useSound);
        }
    }
}
