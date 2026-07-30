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
        }

        private void Awake()
        {
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
            SetText(footerText, "SISTEMA DE CONSULTA  //  ARCHIVO ABIERTO");
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

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
