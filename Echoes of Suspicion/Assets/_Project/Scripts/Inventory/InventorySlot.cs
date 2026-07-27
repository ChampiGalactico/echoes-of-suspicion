using Mirror;

/// <summary>
/// One slot in the player's inventory.
/// Stored inside a SyncList so Mirror replicates changes automatically.
///
/// All items use the hide/reveal pattern: the world object is hidden on
/// pickup and revealed on drop. The item's netId is stored in itemNetId
/// so the server can find the original object.
///
/// Puzzle items additionally have a PickableItem component that holds
/// PuzzleItemData for the puzzle system.
/// </summary>
public struct InventorySlot
{
    /// <summary>Runtime index into the shared ItemData registry.</summary>
    public int itemId;

    /// <summary>Stack count (1 for non-stackable items, 0 = empty).</summary>
    public int count;

    /// <summary>Per-instance durability (e.g. flashlight battery level). -1 = unused.</summary>
    public float durability;

    /// <summary>
    /// NetId of the hidden world object this slot represents.
    /// Used to find and reveal the item on drop/throw.
    /// </summary>
    public uint itemNetId;

    /// <summary>
    /// True if this item has a PickableItem component (puzzle item).
    /// Used by SlotActorInteractable to allow placement in puzzle slots.
    /// </summary>
    public bool isPuzzle;

    public bool IsEmpty => count <= 0 || itemId < 0;

    /// <summary>Whether this slot holds a puzzle item with PuzzleItemData.</summary>
    public bool IsPuzzleItem => isPuzzle;

    public static InventorySlot Empty => new InventorySlot
    {
        itemId = -1,
        count = 0,
        durability = -1f,
        itemNetId = 0,
        isPuzzle = false
    };
}

/// <summary>
/// SyncList wrapper so Mirror can serialize InventorySlot over the network.
/// </summary>
public class SyncListInventorySlot : SyncList<InventorySlot> { }
