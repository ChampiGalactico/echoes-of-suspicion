using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>
    /// Puerta que escucha a UN IPuzzleNode — puede ser un LeafPuzzle simple
    /// o un CompositePuzzle entero (ej: "todos los puzzles del bioma").
    /// La puerta no sabe ni le importa cuál de los dos es.
    /// </summary>
    public class PuzzleDoor : NetworkBehaviour
    {
        [SerializeField, Tooltip("Debe implementar IPuzzleNode")]
        private MonoBehaviour _nodeRef;

        [SyncVar(hook = nameof(OnUnlockedChanged))]
        private bool _isUnlocked;

        public UnityEvent OnDoorOpened;
        public bool IsUnlocked => _isUnlocked;

        private IPuzzleNode _node;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _node = _nodeRef as IPuzzleNode;
            if (_node != null) _node.OnSolved += _ => Unlock();
        }

        [Server]
        public void Unlock()
        {
            if (_isUnlocked) return;
            _isUnlocked = true;
        }

        private void OnUnlockedChanged(bool oldVal, bool newVal)
        {
            if (newVal) OnDoorOpened?.Invoke();
        }
    }
}
