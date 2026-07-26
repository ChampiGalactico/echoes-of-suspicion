using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>
    /// Objeto que solo muestra información: foto, indicador, señal, recibo.
    /// A diferencia de los demás actores, el jugador NO cambia su valor al
    /// interactuar — solo lo revela. El valor lo fija el propio LeafPuzzle
    /// (ej: indicadores generados al azar al iniciar el puzzle).
    ///
    /// Por eso su GetValue() sirve para dos cosas distintas según el puzzle:
    /// (a) alimentar una validación (ej: el string que un DisplayValue debe
    /// coincidir), o (b) simplemente darle información al Guía por voz, sin
    /// participar directamente en Validate().
    /// </summary>
    public class ExaminableActor : PuzzleActorBase
    {
        [SyncVar]
        private string _displayValue = "";

        [Header("Events")]
        public UnityEvent<string> OnExamined;

        public override object GetValue() => _displayValue;

        [Command(requiresAuthority = false)]
        public void CmdExamine()
        {
            RpcShow(_displayValue);
        }

        [ClientRpc]
        private void RpcShow(string value) => OnExamined?.Invoke(value);

        /// <summary>El puzzle (no el jugador) fija qué información muestra este examinable.</summary>
        [Server]
        public void SetDisplayValue(string value)
        {
            _displayValue = value;
            RaiseValueChanged();
        }
    }
}
