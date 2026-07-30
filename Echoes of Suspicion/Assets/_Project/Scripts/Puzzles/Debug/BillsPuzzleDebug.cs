#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Debug helper for testing the Bills puzzle solo (without a Guide player).
    /// Attach to the same GameObject as BillsPuzzleCoordinator.
    ///
    /// Use the Inspector buttons or context menu to simulate Guide actions.
    /// Only works on the server (Host mode).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BillsPuzzleCoordinator))]
    public sealed class BillsPuzzleDebug : MonoBehaviour
    {
        [Header("Auto-Pay")]
        [SerializeField, Tooltip("Automatically confirm payment when a receipt arrives.")]
        private bool autoConfirmCorrect;

        [Header("Manual Payment")]
        [SerializeField, Tooltip("ItemId to send with ConfirmPayment. " +
                                 "Leave empty to use the last received itemId.")]
        private string manualItemId;

        [Header("Status (read-only)")]
        [SerializeField] private bool puzzleStarted;
        [SerializeField] private int currentBill;
        [SerializeField] private string lastReceivedItemId;
        [SerializeField] private float timeRemaining;

        private BillsPuzzleCoordinator _coordinator;
        private bool _subscribedToFax;

        private void Awake()
        {
            _coordinator = GetComponent<BillsPuzzleCoordinator>();
        }

        private void Update()
        {
            if (_coordinator == null) return;

            puzzleStarted = _coordinator.IsStarted;
            currentBill = _coordinator.CurrentBillIndex;
            timeRemaining = _coordinator.TimeRemaining;

            // Subscribe to fax events lazily (after Mirror starts).
            if (!_subscribedToFax)
                TrySubscribeToFax();
        }

        private void OnDisable()
        {
            UnsubscribeFromFax();
        }

        private void TrySubscribeToFax()
        {
            if (!NetworkServer.active) return;

            var fax = FindFirstObjectByType<FaxMachine>();
            if (fax == null) return;

            fax.OnReceiptSent += OnReceiptSent;
            _subscribedToFax = true;
            Debug.Log("[BillsDebug] Subscribed to FaxMachine events.");
        }

        private void UnsubscribeFromFax()
        {
            if (!_subscribedToFax) return;

            var fax = FindFirstObjectByType<FaxMachine>();
            if (fax != null)
                fax.OnReceiptSent -= OnReceiptSent;

            _subscribedToFax = false;
        }

        private void OnReceiptSent(string itemId, PuzzleItemData data, DocumentData doc)
        {
            lastReceivedItemId = itemId;
            Debug.Log($"[BillsDebug] Receipt received: {itemId} ({(data != null ? data.DisplayName : "?")})");

            if (autoConfirmCorrect)
            {
                Debug.Log($"[BillsDebug] Auto-confirming payment for: {itemId}");
                _coordinator.ConfirmPayment(itemId);
            }
        }

        // ── Inspector Buttons (via Context Menu) ─────────────

        [ContextMenu("1. Start Bills Puzzle")]
        private void DebugStartPuzzle()
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[BillsDebug] Must be running as Host.");
                return;
            }

            _coordinator.StartBillsPuzzle();
            Debug.Log("[BillsDebug] Puzzle started.");
        }

        [ContextMenu("2. Confirm Payment (last received)")]
        private void DebugConfirmLast()
        {
            if (!NetworkServer.active) return;

            if (string.IsNullOrEmpty(lastReceivedItemId))
            {
                Debug.LogWarning("[BillsDebug] No receipt received yet. Send one via fax first.");
                return;
            }

            Debug.Log($"[BillsDebug] Confirming payment: {lastReceivedItemId}");
            _coordinator.ConfirmPayment(lastReceivedItemId);
        }

        [ContextMenu("3. Confirm Payment (manual ItemId)")]
        private void DebugConfirmManual()
        {
            if (!NetworkServer.active) return;

            if (string.IsNullOrEmpty(manualItemId))
            {
                Debug.LogWarning("[BillsDebug] Set 'Manual Item Id' in the inspector first.");
                return;
            }

            Debug.Log($"[BillsDebug] Confirming manual payment: {manualItemId}");
            _coordinator.ConfirmPayment(manualItemId);
        }

        [ContextMenu("4. Confirm Payment (wrong — test reset)")]
        private void DebugConfirmWrong()
        {
            if (!NetworkServer.active) return;

            Debug.Log("[BillsDebug] Sending wrong payment to test reset.");
            _coordinator.ConfirmPayment("DEBUG_WRONG_ID");
        }

        [ContextMenu("4. Log Current State")]
        private void DebugLogState()
        {
            var bill = _coordinator.GetCurrentBill();
            Debug.Log($"[BillsDebug] Started={_coordinator.IsStarted} " +
                      $"Bill={_coordinator.CurrentBillIndex}/{_coordinator.TotalBills} " +
                      $"Complete={_coordinator.IsComplete} " +
                      $"Timer={_coordinator.TimeRemaining:F1}s " +
                      $"CurrentTag={bill?.acceptedTag ?? "none"} " +
                      $"LastReceived={lastReceivedItemId ?? "none"}");
        }
    }
}
#endif
