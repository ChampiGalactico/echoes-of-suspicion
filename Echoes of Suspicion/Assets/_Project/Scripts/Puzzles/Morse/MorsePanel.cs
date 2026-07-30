using Mirror;
using TMPro;
using UnityEngine;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Panel interactuable de un símbolo Morse. Hereda de RatInteractable y
    /// usa el flujo de interacción existente (NetworkRatInteractor →
    /// CmdTryInteract → ServerInteract). No decide localmente si es correcto:
    /// solo reenvía la elección al coordinador, que es autoritativo.
    ///
    /// Solo es interactuable por el Runner y solo mientras hay un puzzle
    /// activo (no resuelto).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MorsePanel : RatInteractable
    {
        public enum PanelVisualState
        {
            Idle,
            CurrentSuccess,
            Failure,
            Solved
        }

        [Header("Símbolo")]

        [Tooltip("Uno de: E T A N S M D U G R.")]
        [SerializeField]
        private string symbolId = "E";

        [Header("Coordinador")]

        [SerializeField]
        private MorsePuzzleCoordinator coordinator;

        [Header("Feedback (MaterialPropertyBlock)")]

        [SerializeField]
        private Renderer targetRenderer;

        [SerializeField]
        private Color idleColor = new(0.20f, 0.22f, 0.26f, 1f);

        [SerializeField]
        private Color successColor = new(0.20f, 0.80f, 0.35f, 1f);

        [SerializeField]
        private Color failureColor = new(0.85f, 0.20f, 0.20f, 1f);

        [Tooltip("Estado de éxito estable del puzzle resuelto. Verde (no azul).")]
        [SerializeField]
        private Color solvedColor = new(0.16f, 0.62f, 0.30f, 1f);

        [Header("Etiqueta de letra")]

        [Tooltip("TMP 3D que muestra la letra del símbolo (E, T, A, ...). " +
                 "Solo la letra, nunca el código Morse. Opcional.")]
        [SerializeField]
        private TMP_Text symbolLabel;

        [Header("Debug")]

        [SerializeField]
        private bool verboseLogging = false;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public string SymbolId => symbolId;

        public MorsePuzzleCoordinator Coordinator
        {
            get => coordinator;
            set => coordinator = value;
        }

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            ApplyVisualState(PanelVisualState.Idle);
            RefreshSymbolLabel();
        }

        private void RefreshSymbolLabel()
        {
            if (symbolLabel != null)
            {
                symbolLabel.text = symbolId; // solo la letra, nunca el Morse
            }
        }

        // ─── RatInteractable overrides ───

        public override bool CanPreviewInteraction(GameObject interactor)
        {
            return interactor != null &&
                   coordinator != null &&
                   coordinator.IsPuzzleActiveForPreview;
        }

        public override bool IsInteractableBy(GameObject interactor)
        {
            if (interactor == null || coordinator == null)
            {
                return false;
            }

            if (!coordinator.IsPuzzleActiveForPreview)
            {
                return false;
            }

            CharacterStatsProvider stats =
                interactor.GetComponent<CharacterStatsProvider>();

            return stats != null && stats.Role == PlayerRole.Runner;
        }

        [Server]
        public override bool CanServerInteract(NetworkIdentity interactor)
        {
            if (interactor == null || coordinator == null)
            {
                return false;
            }

            CharacterStatsProvider stats =
                interactor.GetComponent<CharacterStatsProvider>();

            if (stats == null || stats.Role != PlayerRole.Runner)
            {
                return false;
            }

            return coordinator.CanAcceptPanelInteraction();
        }

        [Server]
        public override void ServerInteract(NetworkIdentity interactor)
        {
            if (coordinator == null)
            {
                return;
            }

            if (verboseLogging)
            {
                Debug.Log(
                    $"[MorsePanel] Símbolo '{symbolId}' enviado al coordinador.",
                    this);
            }

            coordinator.ServerSubmitSymbol(symbolId, this, interactor);
        }

        // ─── Feedback (llamado por el coordinador vía RPC del coordinador) ───

        /// <summary>
        /// Cambia el estado visual del panel. Se ejecuta en clientes; usa
        /// MaterialPropertyBlock para no tocar materiales compartidos.
        /// </summary>
        public void ApplyVisualState(PanelVisualState state)
        {
            if (targetRenderer == null)
            {
                return;
            }

            Color color = state switch
            {
                PanelVisualState.CurrentSuccess => successColor,
                PanelVisualState.Failure => failureColor,
                PanelVisualState.Solved => solvedColor,
                _ => idleColor,
            };

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(symbolId))
            {
                symbolId = symbolId.Trim().ToUpperInvariant();
            }

            if (symbolLabel != null)
            {
                symbolLabel.text = symbolId;
            }
        }
#endif
    }
}
