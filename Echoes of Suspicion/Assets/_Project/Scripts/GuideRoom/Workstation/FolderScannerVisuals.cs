using System.Collections;
using TMPro;
using UnityEngine;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Feedback visual y sonoro de la bandeja-escáner.
    ///
    /// No contiene lógica de inventario ni de red.
    /// FolderScannerDock le ordena mostrar, escanear o expulsar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FolderScannerVisuals : MonoBehaviour
    {
        [Header("Carpeta visible")]

        [SerializeField]
        private GameObject folderVisualRoot;

        [SerializeField]
        private Renderer[] folderRenderers;

        [SerializeField]
        private TMP_Text folderLabel;

        [Header("Escáner")]

        [SerializeField]
        private Transform scannerBeam;

        [SerializeField]
        private Vector3 beamStartLocalPosition =
            new(0f, 0.415f, -0.42f);

        [SerializeField]
        private Vector3 beamEndLocalPosition =
            new(0f, 0.415f, 0.33f);

        [SerializeField]
        private Renderer statusLightRenderer;

        [Header("Colores")]

        [SerializeField]
        private Color idleColor =
            new(0.05f, 0.22f, 0.08f, 1f);

        [SerializeField]
        private Color scanningColor =
            new(0.15f, 1f, 0.35f, 1f);

        [SerializeField]
        private Color readyColor =
            new(0.25f, 1f, 0.45f, 1f);

        [SerializeField]
        private Color errorColor =
            new(1f, 0.08f, 0.05f, 1f);

        [Header("Audio")]

        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private AudioClip insertSound;

        [SerializeField]
        private AudioClip scanCompleteSound;

        [SerializeField]
        private AudioClip ejectSound;

        [SerializeField]
        private AudioClip rejectSound;

        private Coroutine scanRoutine;
        private Coroutine rejectRoutine;

        private MaterialPropertyBlock propertyBlock;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");

        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            propertyBlock =
                new MaterialPropertyBlock();

            if (audioSource == null)
            {
                audioSource =
                    GetComponent<AudioSource>();
            }

            SetIdle();
        }

        public void SetIdle()
        {
            StopVisualCoroutines();

            if (folderVisualRoot != null)
            {
                folderVisualRoot.SetActive(false);
            }

            if (scannerBeam != null)
            {
                scannerBeam.gameObject.SetActive(false);
                scannerBeam.localPosition =
                    beamStartLocalPosition;
            }

            SetStatusColor(idleColor);
        }

        public void ShowFolder(
            GuideFolderData folderData
        )
        {
            if (folderVisualRoot != null)
            {
                folderVisualRoot.SetActive(true);
            }

            Color folderColor =
                folderData != null
                    ? folderData.FolderColor
                    : new Color(
                        0.25f,
                        0.36f,
                        0.22f,
                        1f
                    );

            ApplyFolderColor(folderColor);

            if (folderLabel != null)
            {
                folderLabel.text =
                    folderData != null
                        ? folderData.DisplayName
                        : "ARCHIVO";
            }
        }

        public void BeginScan(
            GuideFolderData folderData,
            float duration
        )
        {
            ShowFolder(folderData);

            if (scanRoutine != null)
            {
                StopCoroutine(scanRoutine);
            }

            scanRoutine =
                StartCoroutine(
                    ScanRoutine(
                        Mathf.Max(0.1f, duration)
                    )
                );

            PlayOneShot(insertSound);
        }

        public void FinishScan()
        {
            if (scanRoutine != null)
            {
                StopCoroutine(scanRoutine);
                scanRoutine = null;
            }

            if (scannerBeam != null)
            {
                scannerBeam.gameObject.SetActive(false);
                scannerBeam.localPosition =
                    beamStartLocalPosition;
            }

            SetStatusColor(readyColor);
            PlayOneShot(scanCompleteSound);
        }

        public void EjectFolder()
        {
            StopVisualCoroutines();

            if (folderVisualRoot != null)
            {
                folderVisualRoot.SetActive(false);
            }

            if (scannerBeam != null)
            {
                scannerBeam.gameObject.SetActive(false);
            }

            SetStatusColor(idleColor);
            PlayOneShot(ejectSound);
        }

        public void Reject()
        {
            if (rejectRoutine != null)
            {
                StopCoroutine(rejectRoutine);
            }

            rejectRoutine =
                StartCoroutine(RejectRoutine());

            PlayOneShot(rejectSound);
        }

        private IEnumerator ScanRoutine(
            float duration
        )
        {
            SetStatusColor(scanningColor);

            if (scannerBeam != null)
            {
                scannerBeam.gameObject.SetActive(true);
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float normalized =
                    Mathf.Clamp01(
                        elapsed / duration
                    );

                if (scannerBeam != null)
                {
                    scannerBeam.localPosition =
                        Vector3.Lerp(
                            beamStartLocalPosition,
                            beamEndLocalPosition,
                            normalized
                        );
                }

                yield return null;
            }

            if (scannerBeam != null)
            {
                scannerBeam.gameObject.SetActive(false);
                scannerBeam.localPosition =
                    beamStartLocalPosition;
            }

            scanRoutine = null;
        }

        private IEnumerator RejectRoutine()
        {
            SetStatusColor(errorColor);

            yield return new WaitForSecondsRealtime(
                0.55f
            );

            SetStatusColor(
                folderVisualRoot != null &&
                folderVisualRoot.activeSelf
                    ? readyColor
                    : idleColor
            );

            rejectRoutine = null;
        }

        private void ApplyFolderColor(
            Color targetColor
        )
        {
            if (folderRenderers == null)
            {
                return;
            }

            foreach (
                Renderer folderRenderer
                in folderRenderers
            )
            {
                if (folderRenderer == null)
                {
                    continue;
                }

                ApplyRendererColor(
                    folderRenderer,
                    targetColor,
                    useEmission: false
                );
            }
        }

        private void SetStatusColor(
            Color targetColor
        )
        {
            if (statusLightRenderer == null)
            {
                return;
            }

            ApplyRendererColor(
                statusLightRenderer,
                targetColor,
                useEmission: true
            );
        }

        private void ApplyRendererColor(
            Renderer targetRenderer,
            Color targetColor,
            bool useEmission
        )
        {
            if (propertyBlock == null)
            {
                propertyBlock =
                    new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(
                propertyBlock
            );

            propertyBlock.SetColor(
                BaseColorId,
                targetColor
            );

            propertyBlock.SetColor(
                ColorId,
                targetColor
            );

            if (useEmission)
            {
                propertyBlock.SetColor(
                    EmissionColorId,
                    targetColor * 2.5f
                );
            }

            targetRenderer.SetPropertyBlock(
                propertyBlock
            );
        }

        private void PlayOneShot(
            AudioClip clip
        )
        {
            if (
                audioSource == null ||
                clip == null
            )
            {
                return;
            }

            audioSource.PlayOneShot(clip);
        }

        private void StopVisualCoroutines()
        {
            if (scanRoutine != null)
            {
                StopCoroutine(scanRoutine);
                scanRoutine = null;
            }

            if (rejectRoutine != null)
            {
                StopCoroutine(rejectRoutine);
                rejectRoutine = null;
            }
        }
    }
}
