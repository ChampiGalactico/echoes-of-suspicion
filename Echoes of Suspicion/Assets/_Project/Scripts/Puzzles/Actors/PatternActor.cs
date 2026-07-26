using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>Qué caracteres acepta un PatternActor.</summary>
    public enum PatternCharset { Numeric, Alphabetic, Alphanumeric }

    /// <summary>
    /// Teclado / panel de códigos. Un solo actor cubre "numérico",
    /// "alfabético" y "alfanumérico" — la única diferencia entre esos tres
    /// es qué caracteres acepta CmdAppendChar, así que es un campo de
    /// configuración (_charset), no tres clases distintas.
    ///
    /// El valor es un string que se va armando con cada tecla presionada.
    /// </summary>
    public class PatternActor : PuzzleActorBase
    {
        [SerializeField] private PatternCharset _charset = PatternCharset.Numeric;
        [SerializeField] private int _maxLength = 6;

        [SyncVar(hook = nameof(OnPatternChanged))]
        private string _currentPattern = "";

        [Header("Events")]
        public UnityEvent<string> OnPatternUpdated;

        public string CurrentPattern => _currentPattern;
        public override object GetValue() => _currentPattern;

        [Command(requiresAuthority = false)]
        public void CmdAppendChar(string ch)
        {
            if (!CanInteract || _currentPattern.Length >= _maxLength) return;
            if (!IsValidChar(ch)) return;

            _currentPattern += ch;
            MakeNoise(NoiseLevel.Low);
            RaiseValueChanged();
        }

        [Command(requiresAuthority = false)]
        public void CmdBackspace()
        {
            if (!CanInteract || _currentPattern.Length == 0) return;
            _currentPattern = _currentPattern.Substring(0, _currentPattern.Length - 1);
            RaiseValueChanged();
        }

        [Command(requiresAuthority = false)]
        public void CmdClear()
        {
            _currentPattern = "";
            RaiseValueChanged();
        }

        private bool IsValidChar(string ch)
        {
            if (string.IsNullOrEmpty(ch) || ch.Length != 1) return false;
            char c = ch[0];

            return _charset switch
            {
                PatternCharset.Numeric => char.IsDigit(c),
                PatternCharset.Alphabetic => char.IsLetter(c),
                PatternCharset.Alphanumeric => char.IsLetterOrDigit(c),
                _ => false,
            };
        }

        private void OnPatternChanged(string oldVal, string newVal) => OnPatternUpdated?.Invoke(newVal);
    }
}
