using Mirror;
using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;
using System.Linq;

namespace EOS.Puzzles
{
    /// <summary>The 4 ways to combine child puzzle states into a parent result.</summary>
    public enum CompletionRule
    {
        All,     // all children must be solved
        InOrder, // all must solve in the order they appear in the children array
        Any,     // any single child solving is enough
        NOfM,    // at least N children must solve
    }

    /// <summary>
    /// Universal puzzle node. Everything is a Puzzle.
    ///
    /// Leaf: has a PuzzleAnswer, receives input via PuzzleInteractable.
    /// Parent: has children, uses CompletionRule to combine their results.
    /// Both: has children AND a PuzzleAnswer for value-level validation.
    ///
    /// Old files to delete after migrating:
    ///   LeafPuzzle.cs, CompositePuzzle.cs, PuzzleActorBase.cs,
    ///   SlotActor.cs, SlotActorInteractable.cs, ToolUseActor.cs,
    ///   ToolUseInteractable.cs, RepairOrderTracker.cs, CarRepairFeedback.cs,
    ///   IPuzzleActor.cs
    /// </summary>
    public class Puzzle : NetworkBehaviour, IPuzzleNode
    {
        [Header("* Identity")]
        [SerializeField] private string _nodeId;

        [Header("* Answer (optional for parent-only puzzles)")]
        [SerializeField] private PuzzleAnswer _answer;

        [Header("Hierarchy (optional)")]
        [Tooltip("Leave empty for root puzzles.")]
        [SerializeField] private Puzzle _parent;
        [Tooltip("Leave empty for leaf puzzles.")]
        [SerializeField] private Puzzle[] _children;

        [Header("Completion Rule (parent puzzles)")]
        [SerializeField] private CompletionRule _completionRule = CompletionRule.All;
        [SerializeField, Tooltip("Only for NOfM")]
        private int _requiredCount = 1;

        [Header("* Behavior")]
        [Tooltip("Negative = damage to runner, positive = heal.")]
        [SerializeField] private float _healthImpact = -10f;
        [SerializeField] private float _guideHealthPenalty = 5f;
        [SerializeField] private float _useDelay = 0f;
        [SerializeField] private bool _allowRetry = true;
        [SerializeField] private float _resetDelay = 5f;

        [Header("Feedback — Success (optional)")]
        [SerializeField] private ParticleSystem _successVFX;
        [SerializeField] private AudioClip _successSound;

        [Header("Feedback — Failure (optional)")]
        [SerializeField] private ParticleSystem _failVFX;
        [SerializeField] private AudioClip _failSound;

        [Header("Feedback — Light (optional)")]
        [SerializeField] private Light _feedbackLight;
        [SerializeField] private float _feedbackLightDuration = 1.5f;

        [Header("Events (optional)")]
        public UnityEvent OnPuzzleSolved;
        public UnityEvent OnPuzzleFailed;
        public UnityEvent OnPuzzleReset;

        // ─── Synced state ───

        [SyncVar] private bool _isSolved;
        [SyncVar] private bool _isActive = true;
        [SyncVar] private string _submittedId;
        [SyncVar] private float _submittedNumeric;

        // ─── Server-only state ───

        private int _nextExpectedIndex;
        private float _activatedAtTime;
        private AudioSource _audioSource;
        private bool _busy; // prevents overlapping submissions

        // ─── IPuzzleNode ───

        public string NodeId => _nodeId;
        public bool IsSolved => _isSolved;
        public event Action<IPuzzleNode> OnSolved;

        // ─── Public read-only accessors ───

        public bool IsActive => _isActive;
        public string SubmittedId => _submittedId;
        public float SubmittedNumeric => _submittedNumeric;

        /// <summary>Server-only event fired during reset, before the client RPC.</summary>
        public event Action OnServerReset;

        // =====================================================================
        //  LIFECYCLE
        // =====================================================================

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;

            if (_feedbackLight != null) _feedbackLight.enabled = false;
            if (_successVFX != null) _successVFX.gameObject.SetActive(false);
            if (_failVFX != null) _failVFX.gameObject.SetActive(false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _activatedAtTime = Time.time;
        }

        // =====================================================================
        //  INPUT — called by PuzzleInteractable
        // =====================================================================

        /// <summary>
        /// Submit a value for validation. Called by PuzzleInteractable.
        /// </summary>
        /// <param name="id">String identifier (ItemId, code, toggle state).</param>
        /// <param name="numeric">Numeric value (NumericValue, dial position).</param>
        /// <param name="interactor">The player who submitted.</param>
        [Server]
        public void SubmitValue(string id, float numeric, NetworkIdentity interactor)
        {
            if (_isSolved || !_isActive || _busy) return;

            _submittedId = id;
            _submittedNumeric = numeric;

            if (_useDelay > 0f)
                StartCoroutine(DelayedValidation(interactor));
            else
                RunLeafValidation();
        }

        [Server]
        private IEnumerator DelayedValidation(NetworkIdentity interactor)
        {
            _busy = true;

            if (interactor != null)
                TargetFreezePlayer(interactor.connectionToClient);

            yield return new WaitForSeconds(_useDelay);

            RunLeafValidation();

            if (interactor != null)
                TargetUnfreezePlayer(interactor.connectionToClient);

            _busy = false;
        }

        [Server]
        private void RunLeafValidation()
        {
            if (_answer == null) return;
            bool hasChildren = _children != null && _children.Length > 0;
            if (hasChildren) return; // parents don't validate via SubmitValue

            if (ValidateLeaf())
                HandleSuccess();
            else
                HandleFailure();
        }

        // =====================================================================
        //  CHILD REPORTING — parent receives child notifications
        // =====================================================================

        /// <summary>
        /// Called by a child when it passes its own local validation.
        /// Returns false if the parent rejects (e.g. wrong order).
        /// </summary>
        [Server]
        public bool AcceptChildSolved(Puzzle child)
        {
            if (_isSolved) return true;

            // InOrder enforcement
            if (_completionRule == CompletionRule.InOrder)
            {
                int idx = System.Array.IndexOf(_children, child);
                if (idx != _nextExpectedIndex)
                    return false; // wrong order — child will revert and fail
                _nextExpectedIndex++;
            }

            // Check if enough children are solved
            if (!EvaluateChildren())
                return true; // accepted, but parent not done yet

            // All children satisfied — optional parent-level value validation
            if (_answer != null)
            {
                if (ValidateChildValues())
                    HandleSuccess();
                else
                    HandleFailure();
            }
            else
            {
                HandleSuccess();
            }

            return true;
        }

        // =====================================================================
        //  VALIDATION
        // =====================================================================

        /// <summary>Validate this puzzle's own submitted value against its answer.</summary>
        [Server]
        private bool ValidateLeaf()
        {
            if (_answer == null) return false;

            switch (_answer.Type)
            {
                case ValidationType.Matches:
                    return _answer.ExpectedValues.Length > 0 &&
                           PuzzleValidation.Matches(
                               _submittedId, _answer.ExpectedValues[0]);

                case ValidationType.SumEquals:
                    return PuzzleValidation.SumEquals(
                        new object[] { _submittedNumeric },
                        _answer.TargetSum, _answer.SumTolerance);

                case ValidationType.SequenceMatches:
                    // Single leaf can only match first expected value.
                    return _answer.ExpectedValues.Length == 1 &&
                           PuzzleValidation.Matches(
                               _submittedId, _answer.ExpectedValues[0]);

                case ValidationType.InRange:
                    return PuzzleValidation.InRange(
                        _submittedNumeric, _answer.RangeMin, _answer.RangeMax);

                case ValidationType.TimeWindow:
                    float elapsed = Time.time - _activatedAtTime;
                    return PuzzleValidation.InTimeWindow(
                        elapsed, _answer.WindowStart, _answer.WindowEnd);

                case ValidationType.ContinuousGuard:
                    // Guard is broken when value is "true".
                    return _submittedId != "true" && _submittedId != "True";

                default:
                    return false;
            }
        }

        /// <summary>Validate children's submitted values against this puzzle's answer.</summary>
        [Server]
        private bool ValidateChildValues()
        {
            if (_answer == null || _children == null) return false;

            switch (_answer.Type)
            {
                case ValidationType.Matches:
                    return _children.Length > 0 &&
                           PuzzleValidation.Matches(
                               _children[0].SubmittedId,
                               _answer.ExpectedValues[0]);

                case ValidationType.SumEquals:
                    var nums = _children
                        .Select(c => (object)c.SubmittedNumeric).ToList();
                    return PuzzleValidation.SumEquals(
                        nums, _answer.TargetSum, _answer.SumTolerance);

                case ValidationType.SequenceMatches:
                    var ids = _children
                        .Select(c => (object)c.SubmittedId).ToList();
                    return PuzzleValidation.SequenceMatches(
                        ids, _answer.ExpectedValues);

                case ValidationType.InRange:
                    return _children.Length > 0 &&
                           PuzzleValidation.InRange(
                               _children[0].SubmittedNumeric,
                               _answer.RangeMin, _answer.RangeMax);

                default:
                    return false;
            }
        }

        /// <summary>Check if enough children are solved per the CompletionRule.</summary>
        [Server]
        private bool EvaluateChildren()
        {
            if (_children == null || _children.Length == 0) return false;

            int solved = _children.Count(c => c != null && c.IsSolved);

            return _completionRule switch
            {
                CompletionRule.All     => solved == _children.Length,
                CompletionRule.InOrder => solved == _children.Length,
                CompletionRule.Any     => solved >= 1,
                CompletionRule.NOfM    => solved >= _requiredCount,
                _                      => false,
            };
        }

        // =====================================================================
        //  SUCCESS / FAILURE / RESET
        // =====================================================================

        [Server]
        private void HandleSuccess()
        {
            _isSolved = true;
            _isActive = false;

            // Ask parent before committing
            if (_parent != null && !_parent.AcceptChildSolved(this))
            {
                // Parent rejected (wrong order, etc.)
                _isSolved = false;
                _isActive = true;
                HandleFailure();
                return;
            }

            // Committed
            OnSolved?.Invoke(this);
            RpcOnSuccess();
        }

        [Server]
        private void HandleFailure()
        {
            PuzzleEvents.RaiseNoiseGenerated(transform.position, NoiseLevel.High);

            if (_guideHealthPenalty > 0f)
                PuzzleEvents.RaiseGuideHealthPenalty(_guideHealthPenalty);

            ApplyHealthImpact();
            RpcOnFailure();

            if (_allowRetry)
                Invoke(nameof(ServerReset), _resetDelay);
        }

        /// <summary>
        /// Force a failure without validation. Used when the parent
        /// rejects this puzzle (e.g. wrong order) — the value was locally
        /// correct but globally wrong.
        /// </summary>
        [Server]
        public void ForceFailure()
        {
            if (_isSolved) return;
            HandleFailure();
        }

        [Server]
        private void ServerReset()
        {
            if (_isSolved) return;

            // If parent uses InOrder, reset the entire puzzle tree.
            if (_parent != null && _parent._completionRule == CompletionRule.InOrder)
            {
                _parent.ResetAllChildren();
                return;
            }

            // Otherwise just reset self.
            ForceReset();
        }

        /// <summary>
        /// Hard reset this puzzle regardless of current state.
        /// Used by parent to reset all children after any failure.
        /// </summary>
        [Server]
        public void ForceReset()
        {
            CancelInvoke(nameof(ServerReset));

            _isSolved = false;
            _isActive = true;
            _submittedId = null;
            _submittedNumeric = 0f;
            _activatedAtTime = Time.time;
            _busy = false;

            OnServerReset?.Invoke();
            RpcOnReset();
        }

        /// <summary>
        /// Reset all children and parent tracking state.
        /// Called when any child fails to restart the entire puzzle.
        /// </summary>
        [Server]
        public void ResetAllChildren()
        {
            if (_children == null) return;

            _nextExpectedIndex = 0;

            foreach (var child in _children)
            {
                if (child != null)
                    child.ForceReset();
            }
        }

        [Server]
        private void ApplyHealthImpact()
        {
            if (Mathf.Approximately(_healthImpact, 0f)) return;

            var runner = PlayerUtils.FindPlayerByRole(PlayerRole.Runner);
            if (runner == null) return;

            var health = runner.GetComponent<PlayerHealth>();
            if (health == null) return;

            if (_healthImpact < 0f)
                health.TakeDamage(Mathf.Abs(_healthImpact));
            // Uncomment when Heal() exists:
            // else
            //     health.Heal(_healthImpact);
        }

        // =====================================================================
        //  CLIENT RPCs — feedback
        // =====================================================================

        [ClientRpc]
        private void RpcOnSuccess()
        {
            PlayVFX(_successVFX);
            PlaySound(_successSound);
            OnPuzzleSolved?.Invoke();
        }

        [ClientRpc]
        private void RpcOnFailure()
        {
            PlayVFX(_failVFX);
            PlaySound(_failSound);

            if (_feedbackLight != null)
            {
                _feedbackLight.enabled = true;
                Invoke(nameof(TurnOffFeedbackLight), _feedbackLightDuration);
            }

            OnPuzzleFailed?.Invoke();
        }

        [ClientRpc]
        private void RpcOnReset()
        {
            TurnOffFeedbackLight();
            StopVFX(_failVFX);
            OnPuzzleReset?.Invoke();
        }

        // =====================================================================
        //  FREEZE / UNFREEZE — for useDelay
        // =====================================================================

        [TargetRpc]
        private void TargetFreezePlayer(NetworkConnectionToClient target)
        {
            SetPlayerFrozen(true);
        }

        [TargetRpc]
        private void TargetUnfreezePlayer(NetworkConnectionToClient target)
        {
            SetPlayerFrozen(false);
        }

        private void SetPlayerFrozen(bool frozen)
        {
            var local = NetworkClient.localPlayer;
            if (local == null) return;

            var mov = local.GetComponent<NetworkPlayerMovement>();
            var view = local.GetComponent<NetworkFirstPersonView>();
            var inter = local.GetComponent<NetworkRatInteractor>();

            if (mov != null) mov.enabled = !frozen;
            if (view != null) view.enabled = !frozen;
            if (inter != null) inter.enabled = !frozen;
        }

        // =====================================================================
        //  VFX / AUDIO HELPERS
        // =====================================================================

        private void PlayVFX(ParticleSystem vfx)
        {
            if (vfx == null) return;
            vfx.gameObject.SetActive(true);
            vfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            vfx.Play(true);
        }

        private void StopVFX(ParticleSystem vfx)
        {
            if (vfx == null) return;
            vfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            vfx.gameObject.SetActive(false);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }

        private void TurnOffFeedbackLight()
        {
            if (_feedbackLight != null)
                _feedbackLight.enabled = false;
        }
    }
}
