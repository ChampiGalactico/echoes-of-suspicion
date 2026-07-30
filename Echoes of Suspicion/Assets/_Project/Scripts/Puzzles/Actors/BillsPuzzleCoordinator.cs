using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Orchestrates the "Carlos's Bills" puzzle (Puzzle 3).
    ///
    /// Manages the ordered queue of receipts the Runner must find and fax.
    /// Communicates with the Guide's payment system and the parent Puzzle
    /// hierarchy for completion tracking.
    ///
    /// Flow:
    /// 1. Guide sees the next pending bill on their terminal.
    /// 2. Guide tells Runner which receipt to find.
    /// 3. Runner picks up receipt, inserts into FaxMachine.
    /// 4. FaxMachine.OnReceiptSent fires → this coordinator validates.
    /// 5. If correct receipt: notify Guide to pay, advance puzzle child.
    /// 6. If wrong receipt: fail + noise + reset.
    /// 7. When Guide confirms payment: mark child as solved.
    /// 8. Repeat until all bills are paid → parent Puzzle completes.
    ///
    /// SETUP:
    /// - Place this on the same GameObject as the root Puzzle (CompletionRule: InOrder).
    /// - Assign billEntries in order matching the Puzzle children.
    /// - Assign the FaxMachine reference.
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

        [Header("Time Pressure")]
        [SerializeField, Tooltip("Time limit for the first bill (seconds). 0 = no limit.")]
        private float initialTimeLimit = 60f;

        [SerializeField, Tooltip("Seconds removed from the time limit per bill completed.")]
        private float timeShrinkPerBill = 8f;

        [SerializeField, Tooltip("Minimum time limit (won't shrink below this).")]
        private float minimumTimeLimit = 20f;

        // ── Synced state ────────────────────────────────────

        [SyncVar]
        private int _currentBillIndex;

        [SyncVar]
        private float _currentTimeLimit;

        [SyncVar]
        private float _timeRemaining;

        [SyncVar]
        private bool _waitingForPayment;

        [SyncVar]
        private bool _isComplete;

        // ── Server-only ─────────────────────────────────────

        private bool _timerActive;

        // ── Events (server-side, for Guide integration) ──────

        /// <summary>
        /// Fired when a receipt arrives at the Guide's fax.
        /// Parameters: receiptId, receiptDisplayName, amount, paymentCode.
        /// The Guide's FaxReceiverDock subscribes to this.
        /// </summary>
        public event System.Action<string, string, float, string> OnReceiptArrivedForGuide;

        /// <summary>
        /// Fired when the coordinator needs the Guide to see
        /// which bill is next. Parameters: billIndex, receiptDisplayName, locationHint.
        /// </summary>
        public event System.Action<int, string, string> OnNextBillAnnounced;

        /// <summary>
        /// Fired when a bill is successfully paid.
        /// Parameter: billIndex.
        /// </summary>
        public event System.Action<int> OnBillPaid;

        /// <summary>
        /// Fired when all bills are paid and the puzzle is complete.
        /// </summary>
        public event System.Action OnAllBillsPaid;

        // ── Public accessors ────────────────────────────────

        public int CurrentBillIndex => _currentBillIndex;
        public int TotalBills => billEntries != null ? billEntries.Length : 0;
        public float TimeRemaining => _timeRemaining;
        public float CurrentTimeLimit => _currentTimeLimit;
        public bool WaitingForPayment => _waitingForPayment;
        public bool IsComplete => _isComplete;

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
            _currentTimeLimit = initialTimeLimit;
            _timeRemaining = _currentTimeLimit;
            _waitingForPayment = false;
            _isComplete = false;
            _timerActive = initialTimeLimit > 0f;

            if (faxMachine != null)
                faxMachine.OnReceiptSent += HandleReceiptSent;

            // Announce the first bill.
            AnnounceCurrentBill();
        }

        public override void OnStopServer()
        {
            if (faxMachine != null)
                faxMachine.OnReceiptSent -= HandleReceiptSent;

            base.OnStopServer();
        }

        private void Update()
        {
            if (!isServer || _isComplete) return;

            // Timer countdown (only when not waiting for Guide payment).
            if (_timerActive && !_waitingForPayment && _currentTimeLimit > 0f)
            {
                _timeRemaining -= Time.deltaTime;

                if (_timeRemaining <= 0f)
                {
                    HandleTimeout();
                }
            }
        }

        // ── Receipt handling (server) ────────────────────────

        [Server]
        private void HandleReceiptSent(string receiptId, ReceiptData receiptData)
        {
            if (_isComplete || _waitingForPayment) return;

            BillEntry current = GetCurrentBill();
            if (current == null) return;

            if (receiptId != current.receiptId)
            {
                // Wrong receipt sent — fail.
                HandleWrongReceipt();
                return;
            }

            // Correct receipt — stop timer, notify Guide to pay.
            _waitingForPayment = true;

            if (receiptData != null)
            {
                OnReceiptArrivedForGuide?.Invoke(
                    receiptId,
                    receiptData.ReceiptDisplayName,
                    receiptData.Amount,
                    receiptData.PaymentCode);
            }

            // Also notify via TargetRpc to the Guide player.
            var guidePlayer = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);
            if (guidePlayer != null)
            {
                TargetNotifyReceiptArrived(
                    guidePlayer.connectionToClient,
                    current.receiptDisplayName,
                    receiptData != null ? receiptData.Amount : 0f,
                    receiptData != null ? receiptData.PaymentCode : "N/A");
            }
        }

        /// <summary>
        /// Called by the Guide's BillPaymentButton when they press PAGAR.
        /// Validates and advances the puzzle.
        /// </summary>
        [Server]
        public void ConfirmPayment()
        {
            if (!_waitingForPayment || _isComplete) return;

            BillEntry current = GetCurrentBill();
            if (current == null) return;

            // Mark child puzzle as solved.
            if (current.puzzleChild != null)
            {
                current.puzzleChild.SubmitValue(
                    current.receiptId, 0f, null);
            }

            _waitingForPayment = false;

            OnBillPaid?.Invoke(_currentBillIndex);

            // Notify all clients.
            RpcBillPaidFeedback(_currentBillIndex, current.receiptDisplayName);

            // Advance to next bill.
            _currentBillIndex++;

            if (_currentBillIndex >= billEntries.Length)
            {
                // All bills paid!
                _isComplete = true;
                OnAllBillsPaid?.Invoke();
                return;
            }

            // Shrink time limit for next bill.
            _currentTimeLimit = Mathf.Max(
                minimumTimeLimit,
                _currentTimeLimit - timeShrinkPerBill);
            _timeRemaining = _currentTimeLimit;

            AnnounceCurrentBill();
        }

        // ── Failure handling ─────────────────────────────────

        [Server]
        private void HandleWrongReceipt()
        {
            PuzzleEvents.RaiseNoiseGenerated(transform.position, NoiseLevel.High);
            RpcWrongReceiptFeedback();

            // Reset timer for current bill.
            _timeRemaining = _currentTimeLimit;
        }

        [Server]
        private void HandleTimeout()
        {
            PuzzleEvents.RaiseNoiseGenerated(transform.position, NoiseLevel.Medium);
            RpcTimeoutFeedback();

            // Reset timer.
            _timeRemaining = _currentTimeLimit;
        }

        // ── Announcements ────────────────────────────────────

        [Server]
        private void AnnounceCurrentBill()
        {
            BillEntry current = GetCurrentBill();
            if (current == null) return;

            OnNextBillAnnounced?.Invoke(
                _currentBillIndex,
                current.receiptDisplayName,
                current.locationHint);

            // Tell the Guide which bill is next.
            var guidePlayer = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);
            if (guidePlayer != null)
            {
                TargetAnnounceNextBill(
                    guidePlayer.connectionToClient,
                    _currentBillIndex,
                    billEntries.Length,
                    current.receiptDisplayName,
                    current.locationHint);
            }
        }

        // ── Client RPCs ──────────────────────────────────────

        [ClientRpc]
        private void RpcBillPaidFeedback(int billIndex, string billName)
        {
            Debug.Log($"[BillsPuzzle] Bill paid: {billName} ({billIndex + 1}/{billEntries.Length})");
        }

        [ClientRpc]
        private void RpcWrongReceiptFeedback()
        {
            Debug.Log("[BillsPuzzle] Wrong receipt sent!");
        }

        [ClientRpc]
        private void RpcTimeoutFeedback()
        {
            Debug.Log("[BillsPuzzle] Time's up for current bill!");
        }

        // ── Target RPCs (Guide only) ─────────────────────────

        [TargetRpc]
        private void TargetAnnounceNextBill(
            NetworkConnectionToClient target,
            int billIndex, int totalBills,
            string billName, string locationHint)
        {
            // Guide's UI listens to this.
            Debug.Log($"[BillsPuzzle → Guide] Next bill ({billIndex + 1}/{totalBills}): " +
                      $"{billName} — {locationHint}");
        }

        [TargetRpc]
        private void TargetNotifyReceiptArrived(
            NetworkConnectionToClient target,
            string billName, float amount, string paymentCode)
        {
            Debug.Log($"[BillsPuzzle → Guide] Receipt arrived: {billName}, ${amount}, code: {paymentCode}");
        }
    }

    /// <summary>
    /// One entry in the bills queue. Maps a receipt to its child puzzle
    /// and provides display info for the Guide.
    /// </summary>
    [System.Serializable]
    public sealed class BillEntry
    {
        [Tooltip("Must match ReceiptData.ReceiptId on the receipt prefab.")]
        public string receiptId;

        [Tooltip("Display name (e.g. 'Recibo de Agua').")]
        public string receiptDisplayName;

        [Tooltip("Hint for the Guide to tell the Runner where to look.")]
        public string locationHint;

        [Tooltip("Child Puzzle that gets solved when this bill is paid.")]
        public Puzzle puzzleChild;
    }
}
