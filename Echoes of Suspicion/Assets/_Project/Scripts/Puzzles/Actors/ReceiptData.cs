using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Companion component for receipt items in the Bills puzzle.
    /// Holds the DocumentData (for Guide terminal rendering) and a
    /// unique receiptId used by BillsPuzzleCoordinator to validate
    /// which bill was sent.
    ///
    /// Attach alongside NetworkPickupItem + PickableItem on receipt prefabs.
    /// The NetworkPickupItem handles all pickup/drop/inventory logic;
    /// this component is purely data — same pattern as PickableItem.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPickupItem))]
    public sealed class ReceiptData : MonoBehaviour
    {
        [Header("Receipt Identity")]

        [SerializeField, Tooltip("Unique id matching BillsPuzzleCoordinator entries (e.g. 'water', 'electric').")]
        private string _receiptId;

        [SerializeField, Tooltip("Display name shown in prompts and Guide terminal (e.g. 'Recibo de Agua').")]
        private string _receiptDisplayName = "Recibo";

        [Header("Document")]

        [SerializeField, Tooltip("DocumentData asset with the receipt content. " +
                                 "Rendered by ReadableUI (Runner preview) and GuideTerminalView (Guide payment screen).")]
        private DocumentData _documentData;

        [Header("Payment Info")]

        [SerializeField, Tooltip("Amount shown on the Guide's payment screen.")]
        private float _amount;

        [SerializeField, Tooltip("Payment code shown on the Guide's terminal.")]
        private string _paymentCode = "N/A";

        // ── Public accessors ──────────────────────────────────

        public string ReceiptId => _receiptId;
        public string ReceiptDisplayName => _receiptDisplayName;
        public DocumentData Document => _documentData;
        public float Amount => _amount;
        public string PaymentCode => _paymentCode;
    }
}
