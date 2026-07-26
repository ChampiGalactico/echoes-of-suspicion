using Mirror;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EOS.Puzzles
{
    /// <summary>Las 4 formas de combinar el estado de varios hijos en un solo resultado.</summary>
    public enum CompletionRule
    {
        All,     // todos los hijos deben estar resueltos, sin importar el orden
        InOrder, // deben resolverse en el orden en que aparecen en _childRefs
        Any,     // basta con que uno se resuelva
        NOfM,    // basta con que _requiredCount de ellos se resuelvan
    }

    /// <summary>
    /// Un puzzle "compuesto": no valida actores directamente, valida el
    /// IsSolved de sus hijos. Implementa IPuzzleNode igual que LeafPuzzle,
    /// así que puede ser hijo de OTRO CompositePuzzle sin caso especial —
    /// esto es lo que permite anidar (un bioma contiene puzzles, uno de
    /// esos puzzles contiene sub-etapas, etc).
    /// </summary>
    public class CompositePuzzle : NetworkBehaviour, IPuzzleNode
    {
        [Header("Identidad")]
        [SerializeField] private string _nodeId;

        [Header("Hijos (deben implementar IPuzzleNode: LeafPuzzle o CompositePuzzle)")]
        [SerializeField] private MonoBehaviour[] _childRefs;

        [Header("Regla de combinación")]
        [SerializeField] private CompletionRule _rule = CompletionRule.All;
        [SerializeField, Tooltip("Solo usado por NOfM")] private int _requiredCount = 1;

        private List<IPuzzleNode> _children;
        private int _nextExpectedIndex; // solo usado por InOrder

        [SyncVar]
        private bool _isSolved;

        public string NodeId => _nodeId;
        public bool IsSolved => _isSolved;
        public event Action<IPuzzleNode> OnSolved;

        public override void OnStartServer()
        {
            base.OnStartServer();

            _children = _childRefs
                .Select(r => r as IPuzzleNode)
                .Where(n => n != null)
                .ToList();

            foreach (var child in _children)
                child.OnSolved += HandleChildSolved;

            if (_rule == CompletionRule.InOrder)
                ApplyOrderLocks();
        }

        /// <summary>
        /// El truco de EN ORDEN: en vez de validar el orden después de que
        /// pasó, se bloquea la interacción de los hijos que todavía no les
        /// toca. Solo LeafPuzzle expone SetActive() — un hijo que sea a su
        /// vez un CompositePuzzle maneja su propio bloqueo internamente.
        /// </summary>
        [Server]
        private void ApplyOrderLocks()
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i] is LeafPuzzle leaf)
                    leaf.SetActive(i == _nextExpectedIndex);
            }
        }

        [Server]
        private void HandleChildSolved(IPuzzleNode solvedChild)
        {
            if (_isSolved) return;

            if (_rule == CompletionRule.InOrder)
            {
                _nextExpectedIndex++;
                ApplyOrderLocks();
            }

            if (Evaluate())
            {
                _isSolved = true;
                OnSolved?.Invoke(this);
                RpcOnSolved();
            }
        }

        [Server]
        private bool Evaluate()
        {
            int solvedCount = _children.Count(c => c.IsSolved);

            return _rule switch
            {
                CompletionRule.All => solvedCount == _children.Count,
                CompletionRule.InOrder => solvedCount == _children.Count,
                CompletionRule.Any => solvedCount >= 1,
                CompletionRule.NOfM => solvedCount >= _requiredCount,
                _ => false,
            };
        }

        [ClientRpc] private void RpcOnSolved() { }
    }
}
