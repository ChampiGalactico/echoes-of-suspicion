using TMPro;
using UnityEngine;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Vista reutilizable de la terminal principal del Guía.
    /// No contiene lógica de puzzles ni inventario.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuideTerminalView : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text stationIdText;

        [Header("Waiting")]
        [SerializeField] private GameObject waitingPanel;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text subStatusText;

        [Header("Document")]
        [SerializeField] private GameObject documentPanel;
        [SerializeField] private TMP_Text folderNameText;
        [SerializeField] private TMP_Text documentTitleText;
        [SerializeField] private TMP_Text documentBodyText;
        [SerializeField] private TMP_Text pageText;

        [Header("Footer")]
        [SerializeField] private TMP_Text footerText;
        [SerializeField] private TMP_Text versionText;

        [Header("Legibilidad")]
        [Tooltip("Tamaño de fuente del título del documento.")]
        [SerializeField] private float titleFontSize = 34f;

        [Tooltip("Tamaño de fuente del cuerpo del documento.")]
        [SerializeField] private float bodyFontSize = 22f;

        [Tooltip("Tamaño mínimo permitido si se usa auto-size.")]
        [SerializeField] private float minBodyFontSize = 18f;

        [Tooltip("Tamaño máximo permitido si se usa auto-size.")]
        [SerializeField] private float maxBodyFontSize = 24f;

        [Tooltip("Si true, el cuerpo usa auto-size dentro de [min,max]. Si " +
                 "false, usa bodyFontSize fijo (recomendado con documentos " +
                 "compactos).")]
        [SerializeField] private bool useBodyAutoSize = false;

        [Tooltip("Interlineado del cuerpo (TMP lineSpacing).")]
        [SerializeField] private float bodyLineSpacing = 6f;

        [Tooltip("Márgenes del cuerpo: x=izq, y=arriba, z=der, w=abajo.")]
        [SerializeField] private Vector4 bodyMargins = new(8f, 4f, 8f, 4f);

        [Tooltip("Modo de overflow del cuerpo. Truncate evita que el texto " +
                 "salga del área verde.")]
        [SerializeField] private TextOverflowModes bodyOverflow =
            TextOverflowModes.Truncate;

        public void Configure(
            TMP_Text header,
            TMP_Text stationId,
            GameObject waiting,
            TMP_Text status,
            TMP_Text subStatus,
            GameObject document,
            TMP_Text folderName,
            TMP_Text documentTitle,
            TMP_Text documentBody,
            TMP_Text page,
            TMP_Text footer,
            TMP_Text version)
        {
            headerText = header;
            stationIdText = stationId;
            waitingPanel = waiting;
            statusText = status;
            subStatusText = subStatus;
            documentPanel = document;
            folderNameText = folderName;
            documentTitleText = documentTitle;
            documentBodyText = documentBody;
            pageText = page;
            footerText = footer;
            versionText = version;

            ApplyReadabilitySettings();
        }

        private void Awake()
        {
            ApplyReadabilitySettings();
            ShowWaiting();
        }

        public void ShowWaiting()
        {
            SetPanels(waiting: true);
            SetText(headerText, "TERMINAL DE ARCHIVOS");
            SetText(statusText, "INSERTE UNA CARPETA");
            SetText(subStatusText, "- SISTEMA EN ESPERA -");
            SetText(footerText, "SISTEMA DE CONSULTA  //  EN ESPERA");
        }

        public void ShowLoading(string folderName)
        {
            SetPanels(waiting: true);
            SetText(
                statusText,
                string.IsNullOrWhiteSpace(folderName)
                    ? "LEYENDO ARCHIVO..."
                    : $"LEYENDO: {folderName.ToUpperInvariant()}");
            SetText(subStatusText, "- PROCESANDO DATOS -");
            SetText(footerText, "SISTEMA DE CONSULTA  //  CARGANDO");
        }

        public void ShowDocument(
            string folderName,
            string documentTitle,
            string body,
            int pageIndex,
            int pageCount)
        {
            SetPanels(waiting: false);

            SetText(
                folderNameText,
                string.IsNullOrWhiteSpace(folderName)
                    ? "CARPETA SIN IDENTIFICAR"
                    : folderName.ToUpperInvariant());

            SetText(
                documentTitleText,
                string.IsNullOrWhiteSpace(documentTitle)
                    ? "DOCUMENTO"
                    : documentTitle.ToUpperInvariant());

            SetText(documentBodyText, body ?? string.Empty);

            int safePageCount = Mathf.Max(1, pageCount);
            int safePageIndex = Mathf.Clamp(pageIndex, 0, safePageCount - 1);

            SetText(pageText, $"{safePageIndex + 1:00} / {safePageCount:00}");

            bool hasNextPage =
                safePageIndex <
                safePageCount - 1;

            SetText(
                footerText,
                hasNextPage
                    ? "SISTEMA DE CONSULTA  //  SIGUIENTE DOCUMENTO"
                    : "SISTEMA DE CONSULTA  //  RETIRAR CARPETA"
            );
        }

        public void ShowError(string message)
        {
            SetPanels(waiting: true);
            SetText(statusText, "ERROR DE LECTURA");
            SetText(
                subStatusText,
                string.IsNullOrWhiteSpace(message)
                    ? "- ARCHIVO NO RECONOCIDO -"
                    : $"- {message.ToUpperInvariant()} -");
            SetText(footerText, "SISTEMA DE CONSULTA  //  ERROR");
        }

        private void SetPanels(bool waiting)
        {
            if (waitingPanel != null)
            {
                waitingPanel.SetActive(waiting);
            }

            if (documentPanel != null)
            {
                documentPanel.SetActive(!waiting);
            }
        }

        /// <summary>
        /// Aplica la configuración de legibilidad serializada a los textos de
        /// título y cuerpo. Pública para que el builder pueda invocarla y
        /// persistir los ajustes al ejecutar Create or Refresh, sin reconstruir
        /// la terminal completa. Segura ante referencias nulas.
        /// </summary>
        public void ApplyReadabilitySettings()
        {
            if (documentTitleText != null)
            {
                documentTitleText.enableAutoSizing = false;
                documentTitleText.fontSize = titleFontSize;
            }

            if (documentBodyText == null)
            {
                return;
            }

            documentBodyText.enableWordWrapping = true;
            documentBodyText.richText = true;
            documentBodyText.lineSpacing = bodyLineSpacing;
            documentBodyText.margin = bodyMargins;
            documentBodyText.overflowMode = bodyOverflow;

            if (useBodyAutoSize)
            {
                documentBodyText.enableAutoSizing = true;
                documentBodyText.fontSizeMin = Mathf.Max(1f, minBodyFontSize);
                documentBodyText.fontSizeMax =
                    Mathf.Max(minBodyFontSize, maxBodyFontSize);
            }
            else
            {
                documentBodyText.enableAutoSizing = false;
                documentBodyText.fontSize = bodyFontSize;
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
