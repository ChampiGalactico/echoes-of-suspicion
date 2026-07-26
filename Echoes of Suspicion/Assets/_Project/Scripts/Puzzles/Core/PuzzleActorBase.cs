using Mirror;
using UnityEngine;
using System;

namespace EOS.Puzzles
{
    /// <summary>
    /// Base común de todos los actores. Aquí SOLO va lo que todos comparten:
    /// identidad, estado de "puede interactuar", sincronización por red del
    /// evento de cambio, y el atajo para generar ruido.
    ///
    /// A propósito NO hay aquí un método genérico "Interact()" — cada actor
    /// concreto define su propia forma de recibir input (un toggle se
    /// presiona, un teclado recibe teclas, un dial se gira, un slot recibe
    /// un item). Esa es la parte que varía; esta clase es la que no varía.
    /// </summary>
    public abstract class PuzzleActorBase : NetworkBehaviour, IPuzzleActor
    {
        [Header("Identidad")]
        [SerializeField] private string _actorId;

        [SyncVar]
        private bool _canInteract = true;

        public string ActorId => _actorId;
        public bool CanInteract => _canInteract;

        public event Action OnValueChanged;

        /// <summary>Cada actor concreto sabe cómo exponer su propio valor.</summary>
        public abstract object GetValue();

        [Server]
        public void SetCanInteract(bool value) => _canInteract = value;

        /// <summary>
        /// Los actores concretos llaman esto después de modificar su SyncVar
        /// interno. Dispara el RPC que notifica a todos los clientes, y eso
        /// a su vez dispara OnValueChanged localmente (incluido en el server).
        /// </summary>
        [Server]
        protected void RaiseValueChanged()
        {
            OnValueChanged?.Invoke(); // el server también quiere enterarse
            RpcNotifyValueChanged();
        }

        [ClientRpc]
        private void RpcNotifyValueChanged()
        {
            if (isServer) return; // el server ya se notificó a sí mismo arriba
            OnValueChanged?.Invoke();
        }

        [Server]
        protected void MakeNoise(NoiseLevel level)
        {
            PuzzleEvents.RaiseNoiseGenerated(transform.position, level);
        }
    }
}
