using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// How the player physically interacts with a Puzzle.
    /// This is the optional companion that bridges player input
    /// to the Puzzle brain.
    ///
    /// Interaction modes:
    /// ToolUse   — tool stays in hand, animation/sound plays, then validates.
    /// SlotPlace — item is removed from inventory and placed at the snap point.
    /// Toggle    — press E to flip a boolean state.
    /// Keypad    — future: enter a code via UI.
    /// Dial      — future: rotate to a numeric value.
    ///
    /// Attach to the same GameObject as a Puzzle component.
    /// Requires a Collider, not trigger, on the Interactable layer.
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
        [SerializeField]
        private InteractionMode _mode =
            InteractionMode.ToolUse;

        [Header(
            "Item Filtering (optional, empty = accept any puzzle item)"
        )]
        [SerializeField]
        private string[] _acceptedTags;

        [Header("SlotPlace Settings (optional)")]
        [SerializeField]
        private Transform _snapPoint;

        [Header("Audio (optional)")]
        [SerializeField]
        private AudioSource _audioSource;

        [SerializeField]
        private AudioClip _useSound;

        [Header("Toggle Visual Feedback (optional)")]
        [SerializeField]
        [Tooltip(
            "Renderer que cambiará de color al activar el interruptor."
        )]
        private Renderer _visualRenderer;

        [SerializeField]
        [Tooltip("Color del interruptor cuando está apagado.")]
        private Color _toggleOffColor =
            new(0.12f, 0.12f, 0.12f, 1f);

        [SerializeField]
        [Tooltip("Color del interruptor cuando está activado.")]
        private Color _toggleOnColor =
            new(0f, 1f, 0.333f, 1f);

        // ─── State ───

        private Puzzle _puzzle;
        private MaterialPropertyBlock _materialPropertyBlock;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");

        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        [SyncVar]
        private uint _placedItemNetId;

        [SyncVar(hook = nameof(HandleToggleStateChanged))]
        private bool _toggleState;

        public bool HasPlacedItem =>
            _placedItemNetId != 0;

        // =====================================================================
        // LIFECYCLE
        // =====================================================================

        private void Awake()
        {
            _puzzle = GetComponent<Puzzle>();

            if (_audioSource == null)
            {
                _audioSource =
                    GetComponent<AudioSource>();
            }

            if (_visualRenderer == null)
            {
                _visualRenderer =
                    GetComponentInChildren<Renderer>();
            }

            _materialPropertyBlock =
                new MaterialPropertyBlock();

            ApplyToggleVisual(_toggleState);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (_puzzle != null)
            {
                _puzzle.OnServerReset +=
                    HandleServerReset;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            ApplyToggleVisual(_toggleState);
        }

        private void OnDestroy()
        {
            if (_puzzle != null)
            {
                _puzzle.OnServerReset -=
                    HandleServerReset;
            }
        }

        // =====================================================================
        // CAN INTERACT
        // =====================================================================

        public override bool CanPreviewInteraction(
            GameObject interactor
        )
        {
            if (
                _puzzle == null ||
                _puzzle.IsSolved ||
                !_puzzle.IsActive
            )
            {
                return false;
            }

            switch (_mode)
            {
                case InteractionMode.ToolUse:
                case InteractionMode.SlotPlace:
                    return
                        HasValidItem(interactor) &&
                        (
                            _mode != InteractionMode.SlotPlace ||
                            !HasPlacedItem
                        );

                case InteractionMode.Toggle:
                case InteractionMode.Keypad:
                case InteractionMode.Dial:
                    return true;

                default:
                    return false;
            }
        }

        [Server]
        public override bool CanServerInteract(
            NetworkIdentity interactor
        )
        {
            if (
                _puzzle == null ||
                _puzzle.IsSolved ||
                !_puzzle.IsActive ||
                interactor == null
            )
            {
                return false;
            }

            switch (_mode)
            {
                case InteractionMode.ToolUse:
                    return HasValidItemServer(interactor);

                case InteractionMode.SlotPlace:
                    return
                        !HasPlacedItem &&
                        HasValidItemServer(interactor);

                case InteractionMode.Toggle:
                case InteractionMode.Keypad:
                case InteractionMode.Dial:
                    return true;

                default:
                    return false;
            }
        }

        // =====================================================================
        // INTERACT
        // =====================================================================

        [Server]
        public override void ServerInteract(
            NetworkIdentity interactor
        )
        {
            if (!CanServerInteract(interactor))
            {
                return;
            }

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

                case InteractionMode.Keypad:
                case InteractionMode.Dial:
                    break;
            }
        }

        // =====================================================================
        // MODE HANDLERS
        // =====================================================================

        [Server]
        private void HandleToolUse(
            NetworkIdentity interactor
        )
        {
            PuzzleItemData itemData =
                GetHeldItemData(interactor);

            if (itemData == null)
            {
                return;
            }

            PlayUseSoundForClients();

            _puzzle.SubmitValue(
                itemData.ItemId,
                itemData.NumericValue,
                interactor
            );
        }

        [Server]
        private void HandleSlotPlace(
            NetworkIdentity interactor
        )
        {
            NetworkInventory inventory =
                interactor.GetComponent<NetworkInventory>();

            if (inventory == null)
            {
                return;
            }

            InventorySlot activeSlot =
                inventory.ActiveSlot;

            PickableItem pickable =
                ResolvePickableItem(
                    activeSlot.itemNetId
                );

            if (
                pickable == null ||
                pickable.ItemData == null
            )
            {
                return;
            }

            PuzzleItemData itemData =
                pickable.ItemData;

            NetworkIdentity itemIdentity =
                pickable.GetComponent<NetworkIdentity>();

            if (itemIdentity == null)
            {
                return;
            }

            _placedItemNetId =
                itemIdentity.netId;

            NetworkPickupItem pickupItem =
                pickable.GetComponent<NetworkPickupItem>();

            if (
                pickupItem != null &&
                _snapPoint != null
            )
            {
                pickupItem.PlaceInSlot(
                    _snapPoint.position
                );
            }

            inventory.ServerRemoveItem(
                inventory.ActiveSlotIndex
            );

            PuzzleEvents.RaiseNoiseGenerated(
                transform.position,
                NoiseLevel.Low
            );

            PlayUseSoundForClients();

            _puzzle.SubmitValue(
                itemData.ItemId,
                itemData.NumericValue,
                interactor
            );
        }

        [Server]
        private void HandleToggle(
            NetworkIdentity interactor
        )
        {
            _toggleState =
                !_toggleState;

            /*
             * Actualización inmediata para el host.
             * Los clientes remotos recibirán el cambio mediante
             * el hook del SyncVar.
             */
            ApplyToggleVisual(_toggleState);

            PlayUseSoundForClients();

            _puzzle.SubmitValue(
                _toggleState.ToString(),
                _toggleState ? 1f : 0f,
                interactor
            );
        }

        // =====================================================================
        // TOGGLE VISUALS
        // =====================================================================

        private void HandleToggleStateChanged(
            bool oldValue,
            bool newValue
        )
        {
            ApplyToggleVisual(newValue);
        }

        private void ApplyToggleVisual(
            bool isActive
        )
        {
            if (
                _mode != InteractionMode.Toggle ||
                _visualRenderer == null
            )
            {
                return;
            }

            if (_materialPropertyBlock == null)
            {
                _materialPropertyBlock =
                    new MaterialPropertyBlock();
            }

            Color targetColor =
                isActive
                    ? _toggleOnColor
                    : _toggleOffColor;

            _visualRenderer.GetPropertyBlock(
                _materialPropertyBlock
            );

            /*
             * URP Lit utiliza _BaseColor.
             * Otros shaders pueden utilizar _Color.
             */
            _materialPropertyBlock.SetColor(
                BaseColorId,
                targetColor
            );

            _materialPropertyBlock.SetColor(
                ColorId,
                targetColor
            );

            _visualRenderer.SetPropertyBlock(
                _materialPropertyBlock
            );
        }

        // =====================================================================
        // RESET
        // =====================================================================

        [Server]
        private void HandleServerReset()
        {
            if (_mode == InteractionMode.SlotPlace)
            {
                _placedItemNetId = 0;
            }

            if (_mode == InteractionMode.Toggle)
            {
                _toggleState = false;

                /*
                 * Actualización inmediata para el host.
                 * Los demás clientes reciben el hook del SyncVar.
                 */
                ApplyToggleVisual(false);
            }
        }

        // =====================================================================
        // ITEM HELPERS
        // =====================================================================

        private bool HasValidItem(
            GameObject interactor
        )
        {
            NetworkInventory inventory =
                interactor.GetComponent<NetworkInventory>();

            if (inventory == null)
            {
                return false;
            }

            InventorySlot slot =
                inventory.ActiveSlot;

            if (!slot.IsPuzzleItem)
            {
                return false;
            }

            if (
                _acceptedTags != null &&
                _acceptedTags.Length > 0
            )
            {
                PickableItem pickable =
                    ResolvePickableItem(
                        slot.itemNetId
                    );

                if (
                    pickable == null ||
                    pickable.ItemData == null
                )
                {
                    return false;
                }

                if (
                    !IsTagAccepted(
                        pickable.ItemData.ItemTag
                    )
                )
                {
                    return false;
                }
            }

            return true;
        }

        [Server]
        private bool HasValidItemServer(
            NetworkIdentity interactor
        )
        {
            NetworkInventory inventory =
                interactor.GetComponent<NetworkInventory>();

            if (inventory == null)
            {
                return false;
            }

            InventorySlot slot =
                inventory.ActiveSlot;

            if (!slot.IsPuzzleItem)
            {
                return false;
            }

            PickableItem pickable =
                ResolvePickableItem(
                    slot.itemNetId
                );

            if (
                pickable == null ||
                pickable.ItemData == null
            )
            {
                return false;
            }

            if (
                _acceptedTags != null &&
                _acceptedTags.Length > 0 &&
                !IsTagAccepted(
                    pickable.ItemData.ItemTag
                )
            )
            {
                return false;
            }

            return true;
        }

        [Server]
        private PuzzleItemData GetHeldItemData(
            NetworkIdentity interactor
        )
        {
            NetworkInventory inventory =
                interactor.GetComponent<NetworkInventory>();

            if (inventory == null)
            {
                return null;
            }

            PickableItem pickable =
                ResolvePickableItem(
                    inventory.ActiveSlot.itemNetId
                );

            return pickable != null
                ? pickable.ItemData
                : null;
        }

        private bool IsTagAccepted(
            string tag
        )
        {
            if (
                _acceptedTags == null ||
                _acceptedTags.Length == 0
            )
            {
                return true;
            }

            foreach (string acceptedTag in _acceptedTags)
            {
                if (acceptedTag == tag)
                {
                    return true;
                }
            }

            return false;
        }

        private static PickableItem ResolvePickableItem(
            uint netId
        )
        {
            if (netId == 0)
            {
                return null;
            }

            var spawnedTable =
                NetworkServer.active
                    ? NetworkServer.spawned
                    : NetworkClient.spawned;

            if (
                !spawnedTable.TryGetValue(
                    netId,
                    out NetworkIdentity identity
                )
            )
            {
                return null;
            }

            return identity.GetComponent<PickableItem>();
        }

        // =====================================================================
        // AUDIO RPC
        // =====================================================================

        [Server]
        private void PlayUseSoundForClients()
        {
            if (
                _audioSource == null ||
                _useSound == null
            )
            {
                return;
            }

            RpcPlayUseSound();
        }

        [ClientRpc]
        private void RpcPlayUseSound()
        {
            if (
                _audioSource == null ||
                _useSound == null
            )
            {
                return;
            }

            _audioSource.PlayOneShot(_useSound);
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (_audioSource == null)
            {
                _audioSource =
                    GetComponent<AudioSource>();
            }

            if (_visualRenderer == null)
            {
                _visualRenderer =
                    GetComponentInChildren<Renderer>();
            }

            if (!Application.isPlaying)
            {
                ApplyToggleVisual(_toggleState);
            }
        }
    }
}