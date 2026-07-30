using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Orchestrates the "Carlos's Bills" puzzle (Puzzle 3).
    ///
    /// Flow:
    /// 1. Guide calls StartBillsPuzzle() from their terminal.
    /// 2. Coordinator sets fax accepted tag for the current round.
    /// 3. Runner picks up receipts, inserts into FaxMachine.
    ///    - Fax checks tag: match → sends, mismatch → rejects (red light).
    /// 4. Fax.OnReceiptSent fires → coordinator notifies Guide.
    /// 5. Guide reviews receipt on their terminal:
    ///    - PAGAR → ConfirmPayment(selectedItemId).
    ///      Correct → advance. Wrong → penalty + puzzle resets.
    ///    - RECHAZAR → RejectReceipt(itemId). No penalty.
    /// 6. Repeat until all bills paid → puzzle completes.
    ///
    /// Tag-based filtering per round:
    /// - Agua:         tag "ReceiptWater"     — 1 receipt in world.
    /// - Electricidad: tag "ReceiptElectric"  — 2 receipts, both faxable.
    /// - Renta:        tag "ReceiptRent"      — 3 receipts, all faxable.
    /// - Matrícula:    tag "ReceiptTuition"   — 1 receipt, tight timer.
    ///
    /// When Guide pays the wrong receipt, both players take damage and
    /// the puzzle resets to bill 0. The Guide must re-pay all bills
    /// using receipts already accumulated on their side.
    ///
    /// SETUP:
    /// - Place on same GameObject as root Puzzle (CompletionRule: InOrder).
    /// - Assign billEntries in order matching Puzzle children.
    /// - Each BillEntry.acceptedTag = the PuzzleItemData.ItemTag for
    ///   that round (e.g. "ReceiptElectric").
    /// - Each child Puzzle's PuzzleAnswer holds the correct ItemId.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class BillsPuzzleCoordinator : NetworkBehaviour
    {
        [Header("Bill Entries (in order)")]
        [SerializeField, Tooltip("Each entry matches a child Puzzle in the root. Order matters.")]
        private BillEntry[] billEntries;

        [Header("References")]
        [SerializeField, Tooltip("The fax machine in the Runner's area.")]
        private FaxMachine faxMachine;

        [SerializeField, Tooltip("Root puzzle (CompletionRule: InOrder) with one child per bill.")]
        private Puzzle rootPuzzle;

        [Header("Time Pressure (defaults)")]
        [SerializeField, Tooltip("Default time limit when a BillEntry doesn't override. 0 = no timer.")]
        private float defaultTimeLimit = 60f;

        [Header("Objective Texts (fallback)")]
        [SerializeField, Tooltip("Default Runner objective if BillEntry doesn't specify one.")]
        private string defaultRunnerObjective = "Entrégale el recibo al guía";

        [SerializeField, Tooltip("Default Guide objective if BillEntry doesn't specify one.")]
        private string defaultGuideObjective = "Pídele al corredor que te envíe el recibo";

        // ── Synced state ────────────────────────────────────

        [SyncVar]
        private int _currentBillIndex;

        [SyncVar]
        private float _currentTimeLimit;

        [SyncVar]
        private float _timeRemaining;

        [SyncVar]
        private bool _isComplete;

        [SyncVar]
        private bool _started;

        // ── Server-only ─────────────────────────────────────

        private bool _timerActive;

        // ── Events (server-side) ─────────────────────────────

        /// <summary>
        /// Fired when a receipt arrives via fax for the Guide.
        /// Parameters: itemId, paymentCode.
        /// </summary>
        public event System.Action<string, string> OnReceiptArrivedForGuide;

        /// <summary>Fired when the next bill is announced. Parameters: billIndex, displayName.</summary>
        public event System.Action<int, string> OnNextBillAnnounced;

        /// <summary>Fired when a bill is correctly paid. Parameter: billIndex.</summary>
        public event System.Action<int> OnBillPaid;

        /// <summary>Fired when all bills are paid.</summary>
        public event System.Action OnAllBillsPaid;

        /// <summary>
        /// Fired when the Guide pays the wrong receipt.
        /// Listeners should apply damage to both players.
        /// </summary>
        public event System.Action OnPaymentFailed;

        // ── Public accessors ────────────────────────────────

        public int CurrentBillIndex => _currentBillIndex;
        public int TotalBills => billEntries != null ? billEntries.Length : 0;
        public float TimeRemaining => _timeRemaining;
        public float CurrentTimeLimit => _currentTimeLimit;
        public bool IsComplete => _isComplete;
        public bool IsStarted => _started;

        public BillEntry GetCurrentBill()
        {
            if (billEntries == null || _currentBillIndex >= billEntries.Length)
                return null;
            return billEntries[_currentBillIndex];
        }

        // ── Lifecycle ───────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            _currentBillIndex = 0;
            _isComplete = false;
            _started = false;

            if (faxMachine != null)
                faxMachine.OnReceiptSent += HandleReceiptSent;
        }

        public override void OnStopServer()
        {
            if (faxMachine != null)
                faxMachine.OnReceiptSent -= HandleReceiptSent;

            base.OnStopServer();
        }

        /// <summary>
        /// Called by the Guide's terminal to start the bills puzzle.
        /// Sets fax tag, objectives, and timer for the first bill.
        /// </summary>
        [Server]
        public void StartBillsPuzzle()
        {
            if (_started || _isComplete) return;

            _started = true;

            ApplyCurrentRound();
        }

        private void Update()
        {
            if (!isServer || !_started || _isComplete) return;

            if (_timerActive && _currentTimeLimit > 0f)
            {
                _timeRemaining -= Time.deltaTime;

                if (_timeRemaining <= 0f)
                    HandleTimeout();
            }
        }

        // ── Receipt handling (server) ────────────────────────

        [Server]
        private void HandleReceiptSent(string itemId, PuzzleItemData puzzleData, DocumentData receiptDocument)
        {
            if (!_started || _isComplete) return;

            BillEntry current = GetCurrentBill();
            if (current == null) return;

            string displayName = puzzleData != null
                ? puzzleData.DisplayName
                : current.receiptDisplayName;

            string paymentCode = ExtractPaymentCode(receiptDocument);

            OnReceiptArrivedForGuide?.Invoke(itemId, paymentCode);

            // Notify Guide that a receipt arrived.
            var guidePlayer = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);
            if (guidePlayer != null)
            {
                TargetNotifyReceiptArrived(
                    guidePlayer.connectionToClient,
                    itemId,
                    displayName,
                    paymentCode);
            }
        }

        // ── Payment (server) ─────────────────────────────────

        /// <summary>
        /// Called by the Guide's payment button (PAGAR).
        /// The Guide sends the ItemId of the receipt currently on
        /// their monitor. The child Puzzle validates it against its
        /// PuzzleAnswer. If correct → advance. If wrong → penalty + reset.
        /// </summary>
        [Server]
        public void ConfirmPayment(string selectedItemId)
        {
            if (!_started || _isComplete) return;

            BillEntry current = GetCurrentBill();
            if (current == null || current.puzzleChild == null) return;

            // Let the puzzle validate and handle success/failure internally.
            current.puzzleChild.SubmitValue(selectedItemId, 0f, null);

            if (current.puzzleChild.IsSolved)
            {
                // Correct — puzzle child handled success feedback.
                OnBillPaid?.Invoke(_currentBillIndex);
                RpcBillPaidFeedback(_currentBillIndex, current.receiptDisplayName);

                _currentBillIndex++;

                if (_currentBillIndex >= billEntries.Length)
                {
                    _isComplete = true;
                    _timerActive = false;
                    OnAllBillsPaid?.Invoke();

                    ObjectiveManager.SetRunnerObjective("Espera instrucciones del guía");
                    ObjectiveManager.SetGuideObjective("Todos los recibos pagados");
                    return;
                }

                ApplyCurrentRound();
            }
            else
            {
                // Wrong receipt — SubmitValue already triggered the
                // puzzle's HandleFailure (noise, health penalty, sound).
                // Coordinator just resets and notifies.
                RpcPaymentFailedFeedback(current.receiptDisplayName);

                ServerResetPuzzle();
            }
        }

        // ── Reset (wrong payment) ────────────────────────────

        [Server]
        private void ServerResetPuzzle()
        {
            _currentBillIndex = 0;

            // Reset root puzzle — clears _nextExpectedIndex and all children.
            if (rootPuzzle != null)
                rootPuzzle.ResetAllChildren();

            ApplyCurrentRound();

            var guidePlayer = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);
            if (guidePlayer != null)
            {
                TargetNotifyPuzzleReset(
                    guidePlayer.connectionToClient,
                    billEntries.Length);
            }
        }

        // ── Round setup ──────────────────────────────────────

        /// <summary>
        /// Applies all per-round settings: fax tag, objectives, timer,
        /// and announces the bill to the Guide.
        /// </summary>
        [Server]
        private void ApplyCurrentRound()
        {
            BillEntry current = GetCurrentBill();
            if (current == null) return;

            // Set fax filter for this round.
            if (faxMachine != null)
                faxMachine.SetAcceptedTag(current.acceptedTag);

            ApplyTimeLimitForCurrentBill();
            ApplyBillObjectives();
            AnnounceCurrentBill();
        }

        // ── Objectives ───────────────────────────────────────

        [Server]
        private void ApplyBillObjectives()
        {
            BillEntry current = GetCurrentBill();
            if (current == null) return;

            string runnerText = !string.IsNullOrEmpty(current.runnerObjective)
                ? current.runnerObjective
                : defaultRunnerObjective;

            string guideText = !string.IsNullOrEmpty(current.guideObjective)
                ? current.guideObjective
                : defaultGuideObjective;

            ObjectiveManager.SetObjectives(runnerText, guideText);
        }

        // ── Timer ────────────────────────────────────────────

        [Server]
        private void ApplyTimeLimitForCurrentBill()
        {
            BillEntry current = GetCurrentBill();
            if (current == null) return;

            float limit = current.timeLimitOverride > 0f
                ? current.timeLimitOverride
                : defaultTimeLimit;

            _currentTimeLimit = limit;
            _timeRemaining = limit;
            _timerActive = limit > 0f;
        }

        // ── Failure handling ─────────────────────────────────

        // TODO: Modificar Puzzle.cs para soportar countdown propio por child.
        //       Por ahora el coordinador maneja el timer y delega el fallo al child.

        [Server]
        private void HandleTimeout()
        {
            _timerActive = false;

            BillEntry current = GetCurrentBill();
            if (current != null && current.puzzleChild != null)
                current.puzzleChild.ForceFailure();

            RpcTimeoutFeedback();

            ServerResetPuzzle();
        }

        // ── Helpers ──────────────────────────────────────────

        /// <summary>
        /// Extracts payment code from a DocumentData's Caption section.
        /// The DocumentData comes from the actual receipt sent via fax.
        /// </summary>
        private string ExtractPaymentCode(DocumentData receiptDoc)
        {
            if (receiptDoc == null || receiptDoc.Sections == null)
                return "N/A";

            foreach (var section in receiptDoc.Sections)
            {
                if (section.Type == SectionType.Caption && !string.IsNullOrEmpty(section.Text))
                    return section.Text;
            }

            return "N/A";
        }

        // ── Announcements ────────────────────────────────────

        [Server]
        private void AnnounceCurrentBill()
        {
            BillEntry current = GetCurrentBill();
            if (current == null) return;

            OnNextBillAnnounced?.Invoke(_currentBillIndex, current.receiptDisplayName);

            var guidePlayer = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);
            if (guidePlayer != null)
            {
                TargetAnnounceNextBill(
                    guidePlayer.connectionToClient,
                    _currentBillIndex,
                    billEntries.Length,
                    current.receiptDisplayName,
                    current.guideInstructions);
            }
        }

        // ── Client RPCs ──────────────────────────────────────

        [ClientRpc]
        private void RpcBillPaidFeedback(int billIndex, string billName)
        {
            Debug.Log($"[BillsPuzzle] Bill paid: {billName} ({billIndex + 1}/{billEntries.Length})");
        }

        [ClientRpc]
        private void RpcTimeoutFeedback()
        {
            Debug.Log("[BillsPuzzle] Time's up for current bill!");
        }

        [ClientRpc]
        private void RpcPaymentFailedFeedback(string expectedBillName)
        {
            Debug.Log($"[BillsPuzzle] Wrong payment! Expected {expectedBillName}. Puzzle resets.");
        }

        // ── Target RPCs (Guide only) ─────────────────────────

        [TargetRpc]
        private void TargetAnnounceNextBill(
            NetworkConnectionToClient target,
            int billIndex, int totalBills,
            string billName, string instructions)
        {
            Debug.Log($"[BillsPuzzle → Guide] Next bill ({billIndex + 1}/{totalBills}): " +
                      $"{billName}" +
                      (string.IsNullOrEmpty(instructions) ? "" : $" | {instructions}"));
        }

        [TargetRpc]
        private void TargetNotifyReceiptArrived(
            NetworkConnectionToClient target,
            string itemId, string billName, string paymentCode)
        {
            Debug.Log($"[BillsPuzzle → Guide] Receipt arrived: {billName} (id: {itemId}), code: {paymentCode}");
        }

        [TargetRpc]
        private void TargetNotifyPuzzleReset(
            NetworkConnectionToClient target,
            int totalBills)
        {
            Debug.Log($"[BillsPuzzle → Guide] Puzzle reset! Must re-pay all {totalBills} bills in order.");
        }

        [TargetRpc]
        private void TargetWrongPaymentAttempt(
            NetworkConnectionToClient target,
            string expectedBillName)
        {
            Debug.Log($"[BillsPuzzle → Guide] Wrong payment! Expected {expectedBillName}.");
        }
    }

    /// <summary>
    /// One entry in the bills queue.
    ///
    /// Tag-based filtering:
    /// - acceptedTag = the PuzzleItemData.ItemTag for this round.
    ///   The fax only sends items with this exact tag.
    /// - The correct answer lives in the child Puzzle's PuzzleAnswer.
    ///
    /// Round examples:
    /// - Agua:    acceptedTag "ReceiptWater",    1 receipt  → straightforward.
    /// - Luz:     acceptedTag "ReceiptElectric", 2 receipts → Guide picks correct.
    /// - Renta:   acceptedTag "ReceiptRent",     3 receipts → Guide picks correct.
    /// - Matrícula: acceptedTag "ReceiptTuition", 1 receipt  → tight timer.
    /// </summary>
    [System.Serializable]
    public sealed class BillEntry
    {
        [Header("Receipt Matching")]

        [Tooltip("Exact tag the fax accepts this round (e.g. 'ReceiptElectric'). " +
                 "Must match PuzzleItemData.ItemTag on the receipt prefabs.")]
        public string acceptedTag;

        [Header("Display")]

        [Tooltip("Display name shown on Guide's terminal (e.g. 'Recibo de Agua').")]
        public string receiptDisplayName;

        [Header("Puzzle")]

        [Tooltip("Child Puzzle that gets solved when this bill is paid.")]
        public Puzzle puzzleChild;

        [Header("Time")]

        [Tooltip("Per-round time limit (seconds). 0 = use default.")]
        public float timeLimitOverride;

        [Header("Objectives")]

        [Tooltip("Runner objective this round (e.g. 'Busca el recibo de agua').")]
        public string runnerObjective;

        [Tooltip("Guide objective this round (e.g. 'Pídele al corredor el recibo de agua').")]
        public string guideObjective;

        [TextArea(1, 3)]
        [Tooltip("Extra instructions for the Guide this round " +
                 "(e.g. 'Hay dos recibos de luz. Revisa cuál es el vigente.').")]
        public string guideInstructions;

    }
}
