using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>
    /// Espacio que "recibe cierto objeto": banco de trabajo, mostrador,
    /// conexión de cables. El valor expuesto es el PuzzleItemData del item
    /// colocado (o null si está vacío) — así SumEquals puede leer su precio
    /// y Matches puede leer su ItemId, sin que el Slot sepa cuál de las dos
    /// validaciones se está usando.
    /// </summary>
    public class SlotActor : PuzzleActorBase
    {
        [SerializeField] private Transform _snapPoint;
        [SerializeField] private string[] _acceptedTags; // vacío = acepta cualquier item

        [SyncVar(hook = nameof(OnItemChanged))]
        private uint _placedItemNetId;

        [Header("Events")]
        public UnityEvent<PickableItem> OnItemPlaced;
        public UnityEvent OnItemRemoved;

        public bool HasItem => _placedItemNetId != 0;

        public PickableItem PlacedItem
        {
            get
            {
                if (_placedItemNetId == 0) return null;
                var table = NetworkServer.active ? NetworkServer.spawned : NetworkClient.spawned;
                return table.TryGetValue(_placedItemNetId, out var identity)
                    ? identity.GetComponent<PickableItem>()
                    : null;
            }
        }

        public override object GetValue() => PlacedItem != null ? PlacedItem.ItemData : null;

        [Server]
        public bool TryPlace(PickableItem item)
        {
            if (HasItem || item == null) return false;

            if (_acceptedTags != null && _acceptedTags.Length > 0)
            {
                bool accepted = false;
                foreach (var tag in _acceptedTags)
                {
                    if (item.ItemData != null && item.ItemData.ItemTag == tag) { accepted = true; break; }
                }
                if (!accepted) return false;
            }

            _placedItemNetId = item.GetComponent<NetworkIdentity>().netId;

            var pos = _snapPoint != null ? _snapPoint.position : transform.position;
            item.transform.position = pos;

            MakeNoise(NoiseLevel.Low);
            RaiseValueChanged();
            return true;
        }

        /// <summary>Vacía el slot y devuelve el item que estaba (para que el jugador lo recupere).</summary>
        [Server]
        public PickableItem Clear()
        {
            var item = PlacedItem;
            _placedItemNetId = 0;
            RaiseValueChanged();
            return item;
        }

        private void OnItemChanged(uint oldVal, uint newVal)
        {
            if (newVal != 0) OnItemPlaced?.Invoke(PlacedItem);
            else OnItemRemoved?.Invoke();
        }
    }
}
