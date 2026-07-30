using System.Collections.Generic;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Alfabeto Morse del MVP. Diez símbolos fijos, cada uno con su patrón
    /// de puntos y rayas. Es puramente estático y determinista: el servidor
    /// elige qué símbolos usar y solo sincroniza sus identificadores (una
    /// letra por símbolo), nunca el asset ni el patrón completo.
    ///
    /// Patrón: '.' = punto (dot), '-' = raya (dash).
    /// </summary>
    public static class MorseAlphabet
    {
        /// <summary>Los diez identificadores válidos del MVP, en orden fijo.</summary>
        public static readonly string[] Symbols =
        {
            "E", "T", "A", "N", "S", "M", "D", "U", "G", "R"
        };

        private static readonly Dictionary<string, string> Patterns =
            new()
            {
                { "E", "." },
                { "T", "-" },
                { "A", ".-" },
                { "N", "-." },
                { "S", "..." },
                { "M", "--" },
                { "D", "-.." },
                { "U", "..-" },
                { "G", "--." },
                { "R", ".-." },
            };

        /// <summary>Devuelve el patrón ("." / "-") de un símbolo, o "" si no existe.</summary>
        public static string GetPattern(string symbolId)
        {
            if (string.IsNullOrEmpty(symbolId))
            {
                return string.Empty;
            }

            return Patterns.TryGetValue(symbolId, out string pattern)
                ? pattern
                : string.Empty;
        }

        /// <summary>True si el identificador es uno de los diez símbolos válidos.</summary>
        public static bool IsValidSymbol(string symbolId)
        {
            return !string.IsNullOrEmpty(symbolId) &&
                   Patterns.ContainsKey(symbolId);
        }
    }
}
