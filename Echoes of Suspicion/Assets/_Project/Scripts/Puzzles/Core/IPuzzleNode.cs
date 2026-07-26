using System;

namespace EOS.Puzzles
{
    /// <summary>
    /// Contrato compartido entre un puzzle simple (LeafPuzzle) y un puzzle
    /// compuesto (CompositePuzzle). Gracias a esto, un CompositePuzzle puede
    /// tener como hijo tanto a un LeafPuzzle como a OTRO CompositePuzzle,
    /// sin ningún caso especial — así es como se anidan puzzles dentro de
    /// puzzles, y puzzles dentro de biomas, con la misma pieza.
    /// </summary>
    public interface IPuzzleNode
    {
        string NodeId { get; }
        bool IsSolved { get; }

        /// <summary>Se dispara una sola vez, cuando este nodo pasa a resuelto.</summary>
        event Action<IPuzzleNode> OnSolved;
    }
}
