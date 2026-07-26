using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>
    /// Palanca, botón, válvula, interruptor de módulo. El valor es un bool.
    /// Cubre: módulos del motor, botones de relojes, conexiones eléctricas,
    /// trampas de presión (usado como "¿está pisada?" en navegación ciega).
    /// </summary>
    public class ToggleActor : PuzzleActorBase
    {
        [SerializeField] private NoiseLevel _noiseOnToggle = NoiseLevel.Medium;

        [SyncVar(hook = nameof(OnStateChanged))]
        private bool _isOn;

        [Header("Events")]
        public UnityEvent<bool> OnToggled;

        public bool IsOn => _isOn;
        public override object GetValue() => _isOn;

        /// <summary>Llamado desde el input del jugador (raycast + tecla E).</summary>
        public void Interact() => CmdToggle();

        [Command(requiresAuthority = false)]
        private void CmdToggle()
        {
            if (!CanInteract) return;
            _isOn = !_isOn;
            MakeNoise(_noiseOnToggle);
            RaiseValueChanged();
        }

        /// <summary>Para que el puzzle lo reinicie sin pasar por interacción del jugador.</summary>
        [Server]
        public void ForceSet(bool value)
        {
            _isOn = value;
            RaiseValueChanged();
        }

        private void OnStateChanged(bool oldVal, bool newVal) => OnToggled?.Invoke(newVal);
    }
}
