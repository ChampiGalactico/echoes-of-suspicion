using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>
    /// Door that listens to a single Puzzle (via IPuzzleNode).
    /// Can be a leaf or a parent — the door doesn't care.
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
