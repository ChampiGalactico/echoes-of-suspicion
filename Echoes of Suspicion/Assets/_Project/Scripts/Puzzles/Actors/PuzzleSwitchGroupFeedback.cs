using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Controla el feedback visual y sonoro compartido de un grupo
    /// de interruptores pertenecientes al mismo puzzle.
    ///
    /// Cuando cualquiera de los puzzles observados falla:
    /// - todos los interruptores se muestran en rojo;
    /// - se reproduce un sonido de error.
    ///
    /// Cuando el sistema reinicia los puzzles:
    /// - todos regresan a su color apagado.
    ///
    /// El color verde individual continúa siendo controlado por
    /// PuzzleInteractable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PuzzleSwitchGroupFeedback : MonoBehaviour
    {
        [Header("Puzzles observados")]

        [SerializeField]
        [Tooltip(
            "Puzzles individuales cuyos eventos de fallo y reset " +
            "controlarán este feedback."
        )]
        private Puzzle[] watchedPuzzles;

        [Header("Renderers de los interruptores")]

        [SerializeField]
        private Renderer[] switchRenderers;

        [Header("Colores")]

        [SerializeField]
        private Color idleColor =
            new(0.12f, 0.12f, 0.12f, 1f);

        [SerializeField]
        private Color failureColor =
            new(1f, 0.05f, 0.05f, 1f);

        [Header("Audio de error")]

        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private AudioClip failureSound;

        [SerializeField, Range(0f, 1f)]
        private float failureVolume = 1f;

        private MaterialPropertyBlock propertyBlock;
        private bool failureActive;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");

        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        private void Awake()
        {
            propertyBlock =
                new MaterialPropertyBlock();

            if (audioSource == null)
            {
                audioSource =
                    GetComponent<AudioSource>();
            }
        }

        private void OnEnable()
        {
            SubscribeToPuzzleEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromPuzzleEvents();
        }

        private void SubscribeToPuzzleEvents()
        {
            if (watchedPuzzles == null)
            {
                return;
            }

            foreach (Puzzle puzzle in watchedPuzzles)
            {
                if (puzzle == null)
                {
                    continue;
                }

                puzzle.OnPuzzleFailed.AddListener(
                    HandlePuzzleFailed
                );

                puzzle.OnPuzzleReset.AddListener(
                    HandlePuzzleReset
                );
            }
        }

        private void UnsubscribeFromPuzzleEvents()
        {
            if (watchedPuzzles == null)
            {
                return;
            }

            foreach (Puzzle puzzle in watchedPuzzles)
            {
                if (puzzle == null)
                {
                    continue;
                }

                puzzle.OnPuzzleFailed.RemoveListener(
                    HandlePuzzleFailed
                );

                puzzle.OnPuzzleReset.RemoveListener(
                    HandlePuzzleReset
                );
            }
        }

        private void HandlePuzzleFailed()
        {
            /*
             * Evita reproducir el sonido varias veces si más de un
             * evento de fallo llega durante el mismo reinicio.
             */
            if (failureActive)
            {
                return;
            }

            failureActive = true;

            ApplyColorToAll(failureColor);

            if (
                audioSource != null &&
                failureSound != null
            )
            {
                audioSource.PlayOneShot(
                    failureSound,
                    failureVolume
                );
            }
        }

        private void HandlePuzzleReset()
        {
            if (!failureActive)
            {
                return;
            }

            failureActive = false;

            ApplyColorToAll(idleColor);
        }

        private void ApplyColorToAll(
            Color targetColor
        )
        {
            if (
                switchRenderers == null ||
                propertyBlock == null
            )
            {
                return;
            }

            foreach (Renderer switchRenderer in switchRenderers)
            {
                if (switchRenderer == null)
                {
                    continue;
                }

                switchRenderer.GetPropertyBlock(
                    propertyBlock
                );

                /*
                 * URP Lit normalmente utiliza _BaseColor.
                 * _Color queda como compatibilidad para otros shaders.
                 */
                propertyBlock.SetColor(
                    BaseColorId,
                    targetColor
                );

                propertyBlock.SetColor(
                    ColorId,
                    targetColor
                );

                switchRenderer.SetPropertyBlock(
                    propertyBlock
                );
            }
        }

        private void OnValidate()
        {
            if (audioSource == null)
            {
                audioSource =
                    GetComponent<AudioSource>();
            }

            failureVolume =
                Mathf.Clamp01(failureVolume);
        }
    }
}