using Mirror;
using UnityEngine;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Trigger de escena que arranca el puzzle Morse cuando el Runner entra.
    /// La validación es autoritativa en servidor. Ignora al Guide y a los
    /// objetos que no son jugadores, y no reinicia un puzzle ya resuelto.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MorsePuzzleStartTrigger : NetworkBehaviour
    {
        [Header("Coordinador")]

        [SerializeField]
        private MorsePuzzleCoordinator coordinator;

        [Header("Pruebas")]

        [Tooltip("Arranca automáticamente en Start (solo para pruebas).")]
        [SerializeField]
        private bool autoStartForTesting = false;

        [Header("Debug")]

        [SerializeField]
        private bool verboseLogging = false;

        private bool hasTriggered;

        public override void OnStartServer()
        {
            base.OnStartServer();

            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }

            if (autoStartForTesting && coordinator != null)
            {
                coordinator.ServerStartPuzzle();
                hasTriggered = true;
            }
        }

        [ServerCallback]
        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered || coordinator == null)
            {
                return;
            }

            if (coordinator.IsSolved)
            {
                return;
            }

            NetworkIdentity identity =
                other.GetComponentInParent<NetworkIdentity>();

            if (identity == null)
            {
                return; // no es un jugador de red
            }

            CharacterStatsProvider stats =
                identity.GetComponent<CharacterStatsProvider>();

            if (stats == null || stats.Role != PlayerRole.Runner)
            {
                return; // ignora Guide y no-jugadores
            }

            hasTriggered = true;
            coordinator.ServerStartPuzzle();

            if (verboseLogging)
            {
                Debug.Log(
                    "[MorsePuzzleStartTrigger] Runner entró; puzzle iniciado.",
                    this);
            }
        }

        /// <summary>Permite rearmar el trigger desde servidor si se reinicia.</summary>
        [Server]
        public void ServerRearm()
        {
            hasTriggered = false;
        }
    }
}
