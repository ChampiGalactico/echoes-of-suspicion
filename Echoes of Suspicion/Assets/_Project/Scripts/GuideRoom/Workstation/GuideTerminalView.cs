using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Vista reutilizable de la terminal principal del Guía.
    ///
    /// Soporta paginación automática de texto largo usando TMP Page overflow.
    /// Navegación: flechas del teclado (izq/der) o botones en pantalla.
    ///
    /// Modos de visualización:
    /// - Waiting: esperando carpeta o acción.
    /// - Document: carpeta escaneada con título + body paginado.
    /// - Bills: recibos y pagos del puzzle de bills.
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

        [Header("Pagination Buttons (optional)")]
        [SerializeField, Tooltip("Botón '<' para página anterior. Auto-creado si null.")]
        private GameObject prevPageButton;

        [SerializeField, Tooltip("Botón '>' para página siguiente. Auto-creado si null.")]
        private GameObject nextPageButton;

        [Header("Bills Mode")]
        [SerializeField, Tooltip("Panel de acción de pago (botón PAGAR). Auto-creado si null.")]
        private GameObject payActionPanel;

        [SerializeField, Tooltip("Texto del countdown del bill actual. Se oculta cuando no hay timer.")]
        private TMP_Text timerText;

        [Header("Legibilidad")]

        [Tooltip("Tamaño de fuente del título del documento.")]
        [SerializeField] private float titleFontSize = 34f;

        [Tooltip("Tamaño de fuente del cuerpo del documento.")]
        [SerializeField] private float bodyFontSize = 22f;

        [Tooltip("Tamaño mínimo permitido si se usa auto-size.")]
        [SerializeField] private float minBodyFontSize = 18f;

        [Tooltip("Tamaño máximo permitido si se usa auto-size.")]
        [SerializeField] private float maxBodyFontSize = 24f;

        [Tooltip("Si true, el cuerpo usa auto-size dentro de [min,max].")]
        [SerializeField] private bool useBodyAutoSize = false;

        [Tooltip("Interlineado del cuerpo (TMP lineSpacing).")]
        [SerializeField] private float bodyLineSpacing = 6f;

        [Tooltip("Márgenes del cuerpo: x=izq, y=arriba, z=der, w=abajo.")]
        [SerializeField] private Vector4 bodyMargins = new(8f, 4f, 8f, 4f);

        // ── Pagination state ────────────────────────────────

        private int _bodyPageIndex;   // 0-based
        private int _bodyPageCount = 1;
        private int _docIndex;
        private int _docCount = 1;

        // ── Mode ────────────────────────────────────────────

        private enum TerminalMode { Waiting, Document, Bills }
        private TerminalMode _currentMode = TerminalMode.Waiting;

        // ── Bills state ─────────────────────────────────────

        private bool _receiptReady;
        private string _lastReceiptItemId;

        /// <summary>
        /// Se invoca cuando el Guide presiona PAGAR (Enter o botón).
        /// Parámetro: itemId del último recibo recibido.
        /// </summary>
        public event System.Action<string> OnPayPressed;

        // ── Public accessors ────────────────────────────────

        public int BodyPageIndex => _bodyPageIndex;
        public int BodyPageCount => _bodyPageCount;
        public bool IsInBillsMode => _currentMode == TerminalMode.Bills;

        // ── Configuration ───────────────────────────────────

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

        private void Update()
        {
            if (_currentMode == TerminalMode.Waiting) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Body page navigation.
            if (keyboard.leftArrowKey.wasPressedThisFrame ||
                keyboard.aKey.wasPressedThisFrame)
            {
                PrevBodyPage();
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame ||
                keyboard.dKey.wasPressedThisFrame)
            {
                NextBodyPage();
            }

            // Bills mode: Enter = PAGAR.
            if (_currentMode == TerminalMode.Bills &&
                _receiptReady &&
                (keyboard.enterKey.wasPressedThisFrame ||
                 keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                HandlePayAction();
            }
        }

        // ── Waiting mode ────────────────────────────────────

        public void ShowWaiting()
        {
            _currentMode = TerminalMode.Waiting;
            SetPanels(waiting: true);
            SetPayActionVisible(false);
            HideTimer();
            SetText(headerText, "TERMINAL DE ARCHIVOS");
            SetText(statusText, "INSERTE UNA CARPETA");
            SetText(subStatusText, "- SISTEMA EN ESPERA -");
            SetText(footerText, "SISTEMA DE CONSULTA  //  EN ESPERA");
        }

        public void ShowLoading(string folderName)
        {
            SetPanels(waiting: true);
            SetPayActionVisible(false);
            SetText(
                statusText,
                string.IsNullOrWhiteSpace(folderName)
                    ? "LEYENDO ARCHIVO..."
                    : $"LEYENDO: {folderName.ToUpperInvariant()}");
            SetText(subStatusText, "- PROCESANDO DATOS -");
            SetText(footerText, "SISTEMA DE CONSULTA  //  CARGANDO");
        }

        // ── Document mode (folders) ─────────────────────────

        public void ShowDocument(
            string folderName,
            string documentTitle,
            string body,
            int pageIndex,
            int pageCount)
        {
            _currentMode = TerminalMode.Document;
            _docIndex = pageIndex;
            _docCount = pageCount;
            _receiptReady = false;

            SetPanels(waiting: false);
            SetPayActionVisible(false);

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

            SetBodyText(body ?? string.Empty);
            UpdatePageIndicator();
            UpdateFooter();
        }

        // ── Bills mode ──────────────────────────────────────

        /// <summary>
        /// Muestra qué recibo necesita el Guide para este round.
        /// Llamado cuando se anuncia un nuevo bill.
        /// </summary>
        public void ShowBillAnnouncement(
            int billIndex, int totalBills,
            string billName, string instructions)
        {
            _currentMode = TerminalMode.Bills;
            _receiptReady = false;
            _lastReceiptItemId = null;

            SetPanels(waiting: false);
            SetPayActionVisible(false);

            SetText(headerText, "SISTEMA DE PAGOS");

            SetText(folderNameText,
                $"RECIBO {billIndex + 1} DE {totalBills}");

            SetText(documentTitleText,
                $"SE NECESITA: {billName.ToUpperInvariant()}");

            string bodyContent = "Esperando recibo del corredor...";
            if (!string.IsNullOrEmpty(instructions))
                bodyContent += $"\n\n<color=#8DA88F>{instructions}</color>";

            SetBodyText(bodyContent);
            UpdatePageIndicator();

            SetText(footerText, "SISTEMA DE PAGOS  //  ESPERANDO RECIBO");
        }

        /// <summary>
        /// Muestra el recibo recibido vía fax con el botón PAGAR.
        /// </summary>
        public void ShowReceivedReceipt(
            string itemId, string displayName, string paymentCode)
        {
            _receiptReady = true;
            _lastReceiptItemId = itemId;

            SetPanels(waiting: false);
            SetPayActionVisible(true);

            SetText(documentTitleText,
                $"RECIBO: {displayName.ToUpperInvariant()}");

            string bodyContent =
                $"ID: {itemId}\n" +
                $"Código de pago: <color=#38E850>{paymentCode}</color>\n\n" +
                "<size=130%><color=#E8D838><b>>>> Presione ENTER para PAGAR <<<</b></color></size>\n" +
                "<color=#8DA88F>o haga clic en el botón PAGAR.</color>";

            SetBodyText(bodyContent);
            UpdatePageIndicator();

            SetText(footerText, "SISTEMA DE PAGOS  //  CONFIRMAR PAGO [ENTER]");
        }

        /// <summary>
        /// Feedback de pago exitoso antes de pasar al siguiente bill.
        /// </summary>
        public void ShowPaymentSuccess(string billName)
        {
            _receiptReady = false;
            SetPayActionVisible(false);

            SetText(documentTitleText, "PAGO CONFIRMADO");

            SetBodyText(
                $"<color=#38E850>[OK] {billName} pagado correctamente.</color>\n\n" +
                "Cargando siguiente recibo...");
            UpdatePageIndicator();

            SetText(footerText, "SISTEMA DE PAGOS  //  PAGO EXITOSO");
        }

        /// <summary>
        /// Feedback de pago fallido + reset.
        /// </summary>
        public void ShowPaymentFailed(string expectedBillName)
        {
            _receiptReady = false;
            SetPayActionVisible(false);

            SetText(documentTitleText, "ERROR DE PAGO");

            SetBodyText(
                $"<color=#E84038>✗ Recibo incorrecto.</color>\n\n" +
                $"Se esperaba: {expectedBillName}\n\n" +
                "El sistema se reiniciará. Todos los pagos\n" +
                "deben realizarse nuevamente en orden.");
            UpdatePageIndicator();

            SetText(footerText, "SISTEMA DE PAGOS  //  ERROR — REINICIANDO");
        }

        /// <summary>
        /// Puzzle de recibos completado.
        /// </summary>
        public void ShowBillsComplete()
        {
            _receiptReady = false;
            SetPayActionVisible(false);
            HideTimer();

            SetText(documentTitleText, "PAGOS COMPLETADOS");

            SetBodyText(
                "<color=#38E850>Todos los recibos han sido pagados.</color>\n\n" +
                "El sistema se cerrará automáticamente.");
            UpdatePageIndicator();

            SetText(footerText, "SISTEMA DE PAGOS  //  COMPLETADO");
        }

        /// <summary>
        /// Timeout del bill actual.
        /// </summary>
        public void ShowTimeout()
        {
            _receiptReady = false;
            SetPayActionVisible(false);

            SetText(documentTitleText, "TIEMPO AGOTADO");
            SetBodyText(
                "<color=#E84038>El tiempo para este pago se agotó.</color>\n\n" +
                "El sistema se reiniciará.");
            UpdatePageIndicator();

            SetText(footerText, "SISTEMA DE PAGOS  //  TIEMPO AGOTADO");
        }

        // ── Error ───────────────────────────────────────────

        public void ShowError(string message)
        {
            SetPanels(waiting: true);
            SetPayActionVisible(false);
            SetText(statusText, "ERROR DE LECTURA");
            SetText(
                subStatusText,
                string.IsNullOrWhiteSpace(message)
                    ? "- ARCHIVO NO RECONOCIDO -"
                    : $"- {message.ToUpperInvariant()} -");
            SetText(footerText, "SISTEMA DE CONSULTA  //  ERROR");
        }

        // ── Pagination ──────────────────────────────────────

        public void NextBodyPage()
        {
            if (_bodyPageIndex >= _bodyPageCount - 1) return;
            _bodyPageIndex++;
            ApplyBodyPage();
            UpdatePageIndicator();
        }

        public void PrevBodyPage()
        {
            if (_bodyPageIndex <= 0) return;
            _bodyPageIndex--;
            ApplyBodyPage();
            UpdatePageIndicator();
        }

        // ── Pay action ──────────────────────────────────────

        private void HandlePayAction()
        {
            if (!_receiptReady || string.IsNullOrEmpty(_lastReceiptItemId))
                return;

            OnPayPressed?.Invoke(_lastReceiptItemId);
        }

        /// <summary>
        /// Hook para el botón PAGAR en la UI (onClick).
        /// </summary>
        public void OnPayButtonClicked()
        {
            HandlePayAction();
        }

        /// <summary>
        /// Activa el botón PAGAR. Llamado por GuideBillsTerminalController
        /// cuando un recibo es escaneado en el scanner.
        /// </summary>
        public void ActivatePayButton(string itemId)
        {
            _currentMode = TerminalMode.Bills;
            _receiptReady = true;
            _lastReceiptItemId = itemId;
            SetPayActionVisible(true);

            SetText(footerText,
                "<color=#E8D838><b>>>> ENTER → PAGAR <<<</b></color>  //  ← → CAMBIAR PÁGINA");
            StartPayBlink();
        }

        /// <summary>
        /// Desactiva el botón PAGAR. Llamado cuando el recibo es expulsado.
        /// </summary>
        public void DeactivatePayButton()
        {
            _receiptReady = false;
            _lastReceiptItemId = null;
            SetPayActionVisible(false);
            StopPayBlink();

            if (_currentMode == TerminalMode.Bills)
                _currentMode = TerminalMode.Document;
        }

        // ── Timer ────────────────────────────────────────────

        /// <summary>
        /// Updates the countdown display. Called every frame by the controller.
        /// </summary>
        public void UpdateTimer(float remaining, float total)
        {
            if (timerText == null) return;

            timerText.gameObject.SetActive(true);

            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);

            // Color: green → yellow → red based on fraction remaining.
            string color;
            float fraction = total > 0f ? remaining / total : 0f;
            if (fraction > 0.5f)
                color = "#38E850"; // green
            else if (fraction > 0.2f)
                color = "#E8D838"; // yellow
            else
                color = "#E84038"; // red

            timerText.text = $"<color={color}>TIEMPO RESTANTE: {minutes:00}:{seconds:00}</color>";
        }

        /// <summary>
        /// Hides the countdown display.
        /// </summary>
        public void HideTimer()
        {
            if (timerText != null)
                timerText.gameObject.SetActive(false);
        }

        // ── Internal ────────────────────────────────────────

        private void SetBodyText(string content)
        {
            if (documentBodyText == null) return;

            _bodyPageIndex = 0;

            // Use Page overflow for automatic pagination.
            documentBodyText.overflowMode = TextOverflowModes.Page;
            documentBodyText.text = content;
            documentBodyText.ForceMeshUpdate();

            _bodyPageCount = Mathf.Max(1, documentBodyText.textInfo.pageCount);
            ApplyBodyPage();
        }

        private void ApplyBodyPage()
        {
            if (documentBodyText == null) return;

            // TMP pageToDisplay is 1-indexed.
            documentBodyText.pageToDisplay = _bodyPageIndex + 1;

            // Show/hide nav buttons.
            if (prevPageButton != null)
                prevPageButton.SetActive(_bodyPageIndex > 0);

            if (nextPageButton != null)
                nextPageButton.SetActive(_bodyPageIndex < _bodyPageCount - 1);
        }

        private void UpdatePageIndicator()
        {
            if (pageText == null) return;

            if (_currentMode == TerminalMode.Bills)
            {
                // Bills mode: only show body pages if >1.
                if (_bodyPageCount > 1)
                    pageText.text = $"PÁG {_bodyPageIndex + 1:00} / {_bodyPageCount:00}";
                else
                    pageText.text = "";
            }
            else
            {
                // Document mode: show doc index + body page.
                int safeDocCount = Mathf.Max(1, _docCount);
                int safeDocIndex = Mathf.Clamp(_docIndex, 0, safeDocCount - 1);

                if (_bodyPageCount > 1)
                    pageText.text =
                        $"{safeDocIndex + 1:00}/{safeDocCount:00} · PÁG {_bodyPageIndex + 1}/{_bodyPageCount}";
                else
                    pageText.text = $"{safeDocIndex + 1:00} / {safeDocCount:00}";
            }
        }

        private void UpdateFooter()
        {
            if (_currentMode != TerminalMode.Document) return;

            int safeDocCount = Mathf.Max(1, _docCount);
            bool hasNextDoc = _docIndex < safeDocCount - 1;

            string nav = "";
            if (_bodyPageCount > 1)
                nav = "  //  ← → CAMBIAR PÁGINA";

            SetText(
                footerText,
                hasNextDoc
                    ? $"SISTEMA DE CONSULTA  //  SIGUIENTE DOCUMENTO{nav}"
                    : $"SISTEMA DE CONSULTA  //  RETIRAR CARPETA{nav}"
            );
        }

        private void SetPanels(bool waiting)
        {
            if (waitingPanel != null)
                waitingPanel.SetActive(waiting);

            if (documentPanel != null)
                documentPanel.SetActive(!waiting);
        }

        private void SetPayActionVisible(bool visible)
        {
            if (payActionPanel != null)
                payActionPanel.SetActive(visible);
        }

        /// <summary>
        /// Aplica la configuración de legibilidad serializada.
        /// </summary>
        public void ApplyReadabilitySettings()
        {
            if (documentTitleText != null)
            {
                documentTitleText.enableAutoSizing = false;
                documentTitleText.fontSize = titleFontSize;
            }

            if (documentBodyText == null) return;

            documentBodyText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            documentBodyText.richText = true;
            documentBodyText.lineSpacing = bodyLineSpacing;
            documentBodyText.margin = bodyMargins;

            // Use Page overflow for pagination support.
            documentBodyText.overflowMode = TextOverflowModes.Page;

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
                target.text = value;
        }

        // ── Pay button blink ────────────────────────────────

        private Coroutine _payBlinkRoutine;

        private void StartPayBlink()
        {
            StopPayBlink();
            _payBlinkRoutine = StartCoroutine(PayBlinkLoop());
        }

        private void StopPayBlink()
        {
            if (_payBlinkRoutine != null)
            {
                StopCoroutine(_payBlinkRoutine);
                _payBlinkRoutine = null;
            }
        }

        private System.Collections.IEnumerator PayBlinkLoop()
        {
            const string on  = "<color=#E8D838><b>>>> ENTER → PAGAR <<<</b></color>  //  ← → CAMBIAR PÁGINA";
            const string off = "<color=#8DA88F><b>    ENTER → PAGAR    </b></color>  //  ← → CAMBIAR PÁGINA";

            while (true)
            {
                SetText(footerText, on);
                yield return new WaitForSeconds(0.6f);
                SetText(footerText, off);
                yield return new WaitForSeconds(0.4f);
            }
        }
    }
}
