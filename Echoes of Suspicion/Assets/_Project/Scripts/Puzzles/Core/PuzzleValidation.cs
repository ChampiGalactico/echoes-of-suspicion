using System.Collections.Generic;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Las funciones de comparación puras. No conocen actores, no conocen
    /// Mirror, no conocen puzzles — solo reciben valores y responden true/false.
    /// Esto es a propósito: si mañana quieres testear "¿la suma de estos 3
    /// números da 18500?" sin levantar Unity, puedes.
    /// </summary>
    public static class PuzzleValidation
    {
        /// <summary>Compara un valor cualquiera contra un string esperado.</summary>
        public static bool Matches(object actual, string expected)
        {
            if (actual == null) return string.IsNullOrEmpty(expected);

            // Un item colocado en un Slot se compara por su ItemId, no por
            // su referencia de objeto — así "el mismo tipo de destornillador"
            // cuenta como igual sin importar cuál instancia física es.
            if (actual is PuzzleItemData item) return item.ItemId == expected;

            return actual.ToString() == expected;
        }

        public static bool SumEquals(IEnumerable<object> values, float target, float tolerance)
        {
            float sum = 0f;
            foreach (var v in values)
            {
                if (v is PuzzleItemData item) sum += item.NumericValue;
                else if (v is float f) sum += f;
                else if (v is int i) sum += i;
            }
            return Mathf.Abs(sum - target) <= tolerance;
        }

        public static bool SequenceMatches(IReadOnlyList<object> actual, IReadOnlyList<string> expected)
        {
            if (actual.Count != expected.Count) return false;
            for (int i = 0; i < actual.Count; i++)
            {
                if (!Matches(actual[i], expected[i])) return false;
            }
            return true;
        }

        public static bool InRange(float value, float min, float max) => value >= min && value <= max;

        public static bool InTimeWindow(float actionTime, float start, float end) =>
            actionTime >= start && actionTime <= end;
    }
}
