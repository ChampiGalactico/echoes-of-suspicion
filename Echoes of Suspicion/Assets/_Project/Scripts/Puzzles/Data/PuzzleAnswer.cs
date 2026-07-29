using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Los 6 patrones de validación que cubren los 15 tipos de puzzle.
    /// Elegir uno de estos + los actores correctos es, para la mayoría de
    /// los casos, todo lo que hace falta para crear un puzzle nuevo.
    /// </summary>
    public enum ValidationType
    {
        Matches,          // un solo valor debe coincidir exactamente
        SumEquals,        // la suma de varios valores numéricos = objetivo
        SequenceMatches,  // un array de valores debe coincidir en orden
        InRange,          // un valor debe caer entre un mínimo y un máximo
        TimeWindow,       // una acción debe ocurrir dentro de una ventana de tiempo
        ContinuousGuard,  // falla apenas algún actor entra en estado "malo" (no espera confirmación)
    }

    /// <summary>
    /// The correct answer for a Puzzle, as data instead of code.
    /// Creating a new puzzle is almost always just creating one of these
    /// assets and filling in the fields for the chosen ValidationType
    /// — no C# changes needed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPuzzleAnswer", menuName = "EOS/Puzzles/PuzzleAnswer")]
    public class PuzzleAnswer : ScriptableObject
    {
        public ValidationType Type;

        [Header("Matches (usa solo el índice 0) / SequenceMatches (uno por actor, en orden)")]
        public string[] ExpectedValues;

        [Header("SumEquals")]
        public float TargetSum;
        public float SumTolerance = 0f;

        [Header("InRange")]
        public float RangeMin;
        public float RangeMax;

        [Header("TimeWindow (segundos desde que el puzzle se activó)")]
        public float WindowStart;
        public float WindowEnd;
    }
}
