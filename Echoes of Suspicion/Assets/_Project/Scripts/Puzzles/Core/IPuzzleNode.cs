using System;

namespace EOS.Puzzles
{
    /// <summary>
    /// Contract implemented by Puzzle. Allows PuzzleDoor and other
    /// listeners to react to any puzzle being solved without knowing
    /// whether it's a leaf or a parent.
    /// </summary>
    public interface IPuzzleNode
    {
        string NodeId { get; }
        bool IsSolved { get; }

        /// <summary>Se dispara una sola vez, cuando este nodo pasa a resuelto.</summary>
        event Action<IPuzzleNode> OnSolved;
    }
}
