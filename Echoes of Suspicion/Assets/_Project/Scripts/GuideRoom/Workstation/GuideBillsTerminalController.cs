using Mirror;
using UnityEngine;

namespace EOS.GuideRoom
{
    /// <summary>
    /// Conecta el escáner con el botón PAGAR de la MainScreen
    /// durante el puzzle de bills.
    ///
    /// Flujo:
    /// 1. BillsPuzzleCoordinator anuncia un bill → este controller
    ///    muestra el anuncio en la terminal e inicia el countdown local.
    /// 2. El Guía recoge el recibo del fax y lo inserta en el scanner.
    /// 3. FolderScannerDock muestra el documento en la terminal.
    /// 4. FolderScannerDock.OnReceiptScanned dispara → este controller
    ///    activa el panel PAGAR en la terminal.
    /// 5. Guía presiona Enter / botón PAGAR → OnPayPressed fires →
    ///    este controller enruta a CmdConfirmBillPayment.
    /// 6. Cuando el recibo es expulsado, OnReceiptEjected dispara →
    ///    desactiva PAGAR.
    ///
    /// SETUP:
    /// 1. Colocar en el mismo GO que GuideTerminalView o cerca.
    /// 2. Asignar terminalView y folderScanner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuideBillsTerminalController : MonoBehaviour
    {
        [Header("References")]

        [SerializeField, Tooltip("La terminal principal del Guía.")]
        private GuideTerminalView terminalView;

        [SerializeField, Tooltip("El escáner de documentos del Guía.")]
        private FolderScannerDock folderScanner;

        [Header("Debug")]

        [SerializeField]
        private bool verbose = true;

        // ── State ────────────────────────────────────────────

        private string _scannedItemId;
        private bool _payActive;

        // Timer state (client-side countdown).
        private float _timerRemaining;
        private float _timerTotal;
        private bool _timerActive;

        // ── Lifecycle ────────────────────────────────────────

        private void OnEnable()
        {
            if (folderScanner != null)
            {
                folderScanner.OnReceiptScanned += HandleReceiptScanned;
                folderScanner.OnReceiptEjected += HandleReceiptEjected;
            }

            if (terminalView != null)
                terminalView.OnPayPressed += HandlePayPressed;

            // Listen for bill announcements (includes time limit).
            EOS.Puzzles.BillsPuzzleCoordinator.OnGuideNextBillAnnounced += HandleNextBillAnnounced;

            // Listen for payment results to give feedback.
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientBillPaid += HandleBillPaid;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientPaymentFailed += HandlePaymentFailed;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientTimeout += HandleTimeout;
        }

        private void OnDisable()
        {
            if (folderScanner != null)
            {
                folderScanner.OnReceiptScanned -= HandleReceiptScanned;
                folderScanner.OnReceiptEjected -= HandleReceiptEjected;
            }

            if (terminalView != null)
                terminalView.OnPayPressed -= HandlePayPressed;

            EOS.Puzzles.BillsPuzzleCoordinator.OnGuideNextBillAnnounced -= HandleNextBillAnnounced;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientBillPaid -= HandleBillPaid;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientPaymentFailed -= HandlePaymentFailed;
            EOS.Puzzles.BillsPuzzleCoordinator.OnClientTimeout -= HandleTimeout;
        }

        private void Update()
        {
            if (!_timerActive) return;

            _timerRemaining -= Time.deltaTime;

            if (_timerRemaining < 0f)
                _timerRemaining = 0f;

            if (terminalView != null)
                terminalView.UpdateTimer(_timerRemaining, _timerTotal);
        }

        // ── Bill announcement ───────────────────────────────

        private void HandleNextBillAnnounced(
            int billIndex, int totalBills,
            string billName, string instructions, float timeLimit)
        {
            if (verbose)
                Debug.Log($"[GuideBills] Bill announced: {billName} ({billIndex + 1}/{totalBills}), " +
                          $"time: {timeLimit:F0}s");

            // Start local countdown.
            _timerTotal = timeLimit;
            _timerRemaining = timeLimit;
            _timerActive = timeLimit > 0f;

            // Show announcement on terminal.
            if (terminalView != null)
            {
                terminalView.ShowBillAnnouncement(billIndex, totalBills, billName, instructions);

                if (_timerActive)
                    terminalView.UpdateTimer(_timerRemaining, _timerTotal);
            }
        }

        // ── Scanner events ───────────────────────────────────

        private void HandleReceiptScanned(string itemId)
        {
            _scannedItemId = itemId;
            _payActive = true;

            if (verbose)
                Debug.Log($"[GuideBills] Receipt scanned: {itemId}. PAGAR activated.");

            // Switch terminal to bills mode so PAGAR is visible.
            if (terminalView != null)
                terminalView.ActivatePayButton(itemId);
        }

        private void HandleReceiptEjected()
        {
            _scannedItemId = null;
            _payActive = false;

            if (verbose)
                Debug.Log("[GuideBills] Receipt ejected. PAGAR deactivated.");

            if (terminalView != null)
                terminalView.DeactivatePayButton();
        }

        // ── Pay action ───────────────────────────────────────

        private void HandlePayPressed(string itemId)
        {
            if (!_payActive || string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[GuideBills] Pay pressed but no receipt scanned.");
                return;
            }

            if (verbose)
                Debug.Log($"[GuideBills] Confirming payment for: {itemId}");

            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null)
            {
                Debug.LogWarning("[GuideBills] No local player found.");
                return;
            }

            var health = localPlayer.GetComponent<PlayerHealth>();
            if (health != null)
                health.CmdConfirmBillPayment(itemId);
        }

        // ── Payment result feedback ──────────────────────────

        private void HandleBillPaid(int billIndex, string billName)
        {
            if (verbose)
                Debug.Log($"[GuideBills] Bill paid: {billName}");

            StopTimer();

            if (terminalView != null)
                terminalView.ShowPaymentSuccess(billName);

            _payActive = false;
            _scannedItemId = null;
        }

        private void HandlePaymentFailed(string expectedBillName)
        {
            if (verbose)
                Debug.Log($"[GuideBills] Payment failed. Expected: {expectedBillName}");

            StopTimer();

            if (terminalView != null)
                terminalView.ShowPaymentFailed(expectedBillName);

            _payActive = false;
            _scannedItemId = null;
        }

        private void HandleTimeout()
        {
            if (verbose)
                Debug.Log("[GuideBills] Timeout!");

            StopTimer();

            if (terminalView != null)
                terminalView.ShowTimeout();

            _payActive = false;
            _scannedItemId = null;
        }

        // ── Timer helpers ────────────────────────────────────

        private void StopTimer()
        {
            _timerActive = false;
            _timerRemaining = 0f;

            if (terminalView != null)
                terminalView.HideTimer();
        }
    }
}
