using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>
    /// Perilla, válvula, dial de radio. El valor es un float continuo entre
    /// un mínimo y un máximo, en vez de un estado discreto como el Toggle.
    /// Cubre: sintonizar una frecuencia, ajustar un nivel de agua/presión.
    /// </summary>
    public class DialActor : PuzzleActorBase
    {
        [SerializeField] private float _min = 0f;
        [SerializeField] private float _max = 100f;
        [SerializeField] private float _step = 1f;

        [SyncVar(hook = nameof(OnDialSet))]
        private float _currentValue;

        [Header("Events")]
        public UnityEvent<float> OnDialChanged;

        public float CurrentValue => _currentValue;
        public override object GetValue() => _currentValue;

        /// <summary>delta positivo o negativo, ej: +1 al girar a la derecha, -1 a la izquierda.</summary>
        [Command(requiresAuthority = false)]
        public void CmdAdjust(float delta)
        {
            if (!CanInteract) return;
            _currentValue = Mathf.Clamp(_currentValue + delta * _step, _min, _max);
            RaiseValueChanged();
        }

        private void OnDialSet(float oldVal, float newVal) => OnDialChanged?.Invoke(newVal);
    }
}
