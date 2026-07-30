using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Companion component that marks a NetworkPickupItem as a puzzle item.
    ///
    /// Holds PuzzleItemData used by SlotActor for tag filtering and value
    /// evaluation. All interaction, visibility, and inventory logic is
    /// handled by NetworkPickupItem — this component is purely data.
    ///
    /// Attach alongside NetworkPickupItem on puzzle item prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPickupItem))]
    public class PickableItem : MonoBehaviour
    {
        [Header("Puzzle Data")]
        [SerializeField]
        private PuzzleItemData _puzzleData;

        [Header("Document (optional)")]
        [SerializeField, Tooltip("Optional DocumentData for items that carry readable content " +
                                 "(e.g. receipts). Used by FaxMachine to send document info to the Guide.")]
        private DocumentData _documentData;

        /// <summary>Primary accessor for the puzzle data.</summary>
        public PuzzleItemData PuzzleData => _puzzleData;

        /// <summary>
        /// Backward-compatible property used by SlotActor for tag
        /// filtering and value evaluation.
        /// </summary>
        public PuzzleItemData ItemData => _puzzleData;

        /// <summary>
        /// Optional document content carried by this item.
        /// Null for non-document items.
        /// </summary>
        public DocumentData DocumentData => _documentData;
    }
}
