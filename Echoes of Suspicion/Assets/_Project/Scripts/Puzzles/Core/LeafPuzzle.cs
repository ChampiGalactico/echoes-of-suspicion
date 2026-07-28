using Mirror;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace EOS.Puzzles
{
    /// <summary>
    /// Un puzzle "hoja": lee los valores de un grupo de actores y los
    /// compara contra un PuzzleAnswer usando PuzzleValidation. Esta clase
    /// no sabe si sus actores son Toggles, Slots o Diales — solo llama
    /// GetValue() sobre cada uno.
    ///
    /// Nota sobre _actorRefs: Unity no puede serializar una lista de
    /// interfaces en el Inspector, así que se arrastran los componentes
    /// como MonoBehaviour y se convierten a IPuzzleActor en OnStartServer.
    /// Si arrastras algo que no implementa IPuzzleActor, se ignora en
    /// silencio (Where(a => a != null)) — vale la pena loguear un warning
    /// en producción para detectar el error de configuración pronto.
    /// </summary>
    public class LeafPuzzle : NetworkBehaviour, IPuzzleNode
    {
        [Header("Events")]
        public UnityEvent OnPuzzleSolved;
        public UnityEvent OnPuzzleFailed;
        public UnityEvent OnPuzzleReset;

        [Header("Identidad")]
        [SerializeField] private string _nodeId;

        [Header("Actores involucrados (deben implementar IPuzzleActor)")]
        [SerializeField] private MonoBehaviour[] _actorRefs;

        [Header("Respuesta correcta")]
        [SerializeField] private PuzzleAnswer _answer;

        [Header("Comportamiento al fallar")]
        [SerializeField] private bool _allowRetry = true;
        [SerializeField] private float _resetDelay = 5f;
        [SerializeField] private float _guideHealthPenalty = 5f;

        private List<IPuzzleActor> _actors;
        private float _activatedAtTime;

        [SyncVar]
        private bool _isSolved;

        [SyncVar]
        private bool _isActive = true;

        public string NodeId => _nodeId;
        public bool IsSolved => _isSolved;
        public event Action<IPuzzleNode> OnSolved;


        public override void OnStartServer()
        {
            base.OnStartServer();

            _actors = _actorRefs
                .Select(r => r as IPuzzleActor)
                .Where(a => a != null)
                .ToList();

            foreach (var actor in _actors)
                actor.OnValueChanged += HandleActorChanged;

            _activatedAtTime = Time.time;
        }

        /// <summary>
        /// Reacciona a CUALQUIER cambio de cualquiera de sus actores.
        /// Para ContinuousGuard, esto ES la validación (no espera un
        /// "confirmar"). Para los demás tipos, aquí no pasa nada — esos
        /// esperan a que algo llame AttemptSolve() explícitamente
        /// (ej: el jugador pulsa "confirmar" en el teclado).
        /// </summary>
        [Server]
        private void HandleActorChanged()
        {
            if (_isSolved || !_isActive) return;

            if (_answer.Type == ValidationType.ContinuousGuard && !Validate())
            {
                HandleFailure();
            }
        }

        /// <summary>Llamar cuando el jugador confirma su intento (botón, teclado, etc).</summary>
        [Server]
        public void AttemptSolve()
        {
            if (_isSolved || !_isActive) return;

            if (Validate()) HandleSuccess();
            else HandleFailure();
        }

        [Server]
        private bool Validate()
        {
            var values = _actors.Select(a => a.GetValue()).ToList();

            switch (_answer.Type)
            {
                case ValidationType.Matches:
                    return values.Count > 0 &&
                           PuzzleValidation.Matches(values[0], _answer.ExpectedValues[0]);

                case ValidationType.SumEquals:
                    return PuzzleValidation.SumEquals(values, _answer.TargetSum, _answer.SumTolerance);

                case ValidationType.SequenceMatches:
                    return PuzzleValidation.SequenceMatches(values, _answer.ExpectedValues);

                case ValidationType.InRange:
                    return values.Count > 0 && values[0] is float f &&
                           PuzzleValidation.InRange(f, _answer.RangeMin, _answer.RangeMax);

                case ValidationType.TimeWindow:
                    float elapsed = Time.time - _activatedAtTime;
                    return PuzzleValidation.InTimeWindow(elapsed, _answer.WindowStart, _answer.WindowEnd);

                case ValidationType.ContinuousGuard:
                    // Válido mientras NINGÚN actor esté en estado "true"
                    // (ej: ninguna trampa pisada). Apenas uno lo esté, falla.
                    return values.All(v => !(v is bool b && b));

                default:
                    return false;
            }
        }

        [Server]
        private void HandleSuccess()
        {
            _isSolved = true;
            _isActive = false;
            OnSolved?.Invoke(this);
            RpcOnSolved();
        }

        [Server]
        private void HandleFailure()
        {
            PuzzleEvents.RaiseNoiseGenerated(transform.position, NoiseLevel.High);

            if (_guideHealthPenalty > 0f)
                PuzzleEvents.RaiseGuideHealthPenalty(_guideHealthPenalty);

            RpcOnFailed();

            if (_allowRetry)
                Invoke(nameof(ServerReset), _resetDelay);
        }

        [Server]
        private void ServerReset()
        {
            if (_isSolved) return;
            _isActive = true;
            _activatedAtTime = Time.time;
            RpcOnReset();
        }

        /// <summary>Usado por CompositePuzzle para bloquear/desbloquear en la regla EN ORDEN.</summary>
        [Server]
        public void SetActive(bool active) => _isActive = active;

        [ClientRpc] private void RpcOnSolved()
        {
            OnPuzzleSolved?.Invoke();
        }
        [ClientRpc] private void RpcOnFailed()
        {
            OnPuzzleFailed?.Invoke();
        }
        [ClientRpc] private void RpcOnReset()
        {
            OnPuzzleReset?.Invoke();    
        }
    }
}
