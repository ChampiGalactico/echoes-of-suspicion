using System.Collections.Generic;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Alfabeto Morse completo: A-Z + Ñ (27 letras). Cada letra tiene su
    /// patrón estándar de puntos y rayas. Es puramente estático y
    /// determinista: el servidor elige qué palabra usar y solo sincroniza
    /// identificadores pequeños (letras), nunca assets.
    ///
    /// Patrón: '.' = punto (dot), '-' = raya (dash).
    /// </summary>
    public static class MorseAlphabet
    {
        /// <summary>Todas las letras válidas, en orden alfabético español.</summary>
        public static readonly string[] Symbols =
        {
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J",
            "K", "L", "M", "N", "Ñ", "O", "P", "Q", "R", "S",
            "T", "U", "V", "W", "X", "Y", "Z"
        };

        private static readonly Dictionary<string, string> Patterns =
            new()
            {
                { "A", ".-" },
                { "B", "-..." },
                { "C", "-.-." },
                { "D", "-.." },
                { "E", "." },
                { "F", "..-." },
                { "G", "--." },
                { "H", "...." },
                { "I", ".." },
                { "J", ".---" },
                { "K", "-.-" },
                { "L", ".-.." },
                { "M", "--" },
                { "N", "-." },
                { "Ñ", "--.--" },
                { "O", "---" },
                { "P", ".--." },
                { "Q", "--.-" },
                { "R", ".-." },
                { "S", "..." },
                { "T", "-" },
                { "U", "..-" },
                { "V", "...-" },
                { "W", ".--" },
                { "X", "-..-" },
                { "Y", "-.--" },
                { "Z", "--.." },
            };

        /// <summary>Devuelve el patrón ("." / "-") de un símbolo, o "" si no existe.</summary>
        public static string GetPattern(string symbolId)
        {
            if (string.IsNullOrEmpty(symbolId))
                return string.Empty;

            return Patterns.TryGetValue(symbolId.ToUpperInvariant(), out string pattern)
                ? pattern
                : string.Empty;
        }

        /// <summary>True si el identificador es una letra válida del alfabeto.</summary>
        public static bool IsValidSymbol(string symbolId)
        {
            return !string.IsNullOrEmpty(symbolId) &&
                   Patterns.ContainsKey(symbolId.ToUpperInvariant());
        }
    }
}
