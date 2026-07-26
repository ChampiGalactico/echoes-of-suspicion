using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Objeto que el Corredor recoge del mundo y lleva a un SlotActor.
    /// No es un "actor" en sí mismo (no expone un valor que un puzzle lea
    /// directamente) — es el objeto que, una vez colocado en un Slot, SE
    /// CONVIERTE en el valor de ese Slot a través de ItemData.
    /// </summary>
    public class PickableItem : NetworkBehaviour
    {
        [SerializeField] private PuzzleItemData _itemData;

        [SyncVar(hook = nameof(OnPickedUpChanged))]
        private bool _isPickedUp;

        public PuzzleItemData ItemData => _itemData;
        public bool IsPickedUp => _isPickedUp;

        private Vector3 _originPosition;

        private void Start() => _originPosition = transform.position;

        public void Interact(GameObject interactor) => CmdPickUp();

        [Command(requiresAuthority = false)]
        private void CmdPickUp()
        {
            if (_isPickedUp) return;
            _isPickedUp = true;
            PuzzleEvents.RaiseNoiseGenerated(transform.position, NoiseLevel.Low);
        }

        [Server]
        public void Drop(Vector3 position)
        {
            _isPickedUp = false;
            transform.position = position;
        }

        [Server]
        public void ReturnToOrigin() => Drop(_originPosition);

        private void OnPickedUpChanged(bool oldVal, bool newVal)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = !newVal;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = !newVal;
        }
    }
}
