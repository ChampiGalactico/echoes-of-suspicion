using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Controls the Morse code puzzle in the lava hallway (Puzzle 2).
    ///
    /// Manages a sequence of MorsePuzzleEmitters: activates them one at
    /// a time. The Runner listens to the sound, relays the pattern to
    /// the Guide over voice, and the Guide decodes it from their Morse
    /// reference table and tells the Runner which wall section to activate.
    ///
    /// SETUP:
    /// 1. Place on a root Puzzle (CompletionRule: InOrder).
    /// 2. Each child Puzzle is a "round" — assign the corresponding
    ///    MorsePuzzleEmitter in the rounds array.
    /// 3. Each emitter's linkedPuzzle should point to its child Puzzle.
    /// 4. Wall sections (interactables) should SubmitValue to the child
    ///    Puzzle they represent. Correct submission → child solves →
    ///    this controller advances to the next round.
    ///
    /// Scene hierarchy:
    /// ```
    /// MorsePuzzle_Root (Puzzle InOrder + MorsePuzzleController)
    /// ├── Round1_Child (Puzzle)
    /// │   ├── Emitter_1 (MorsePuzzleEmitter, linkedPuzzle = Round1_Child)
    /// │   └── WallSection_A (interactable → SubmitValue to Round1_Child)
    /// ├── Round2_Child (Puzzle)
    /// │   ├── Emitter_2 (MorsePuzzleEmitter, linkedPuzzle = Round2_Child)
    /// │   └── WallSection_B (interactable → SubmitValue to Round2_Child)
    /// └── Round3_Child (Puzzle)
    ///     ├── Emitter_3 (MorsePuzzleEmitter, linkedPuzzle = Round3_Child)
    ///     └── WallSection_C (interactable → SubmitValue to Round3_Child)
    /// ```
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class MorsePuzzleController : NetworkBehaviour
    {
        [Header("Rounds (in order)")]

        [SerializeField, Tooltip("Emitters for each round. Activated sequentially.")]
        private MorsePuzzleEmitter[] rounds;

        [Header("Failure")]

        [SerializeField, Tooltip("Noise level when Runner activates the wrong section.")]
        private NoiseLevel wrongAnswerNoise = NoiseLevel.Medium;

        [Header("Activation")]

        [SerializeField, Tooltip("Trigger zone that starts the puzzle when Runner enters. " +
                                 "Leave null to start immediately on server start.")]
        private Collider activationTrigger;

        [SerializeField, Tooltip("Delay before the first emitter activates.")]
        private float activationDelay = 1.5f;

        // ── State ────────────────────────────────────────────

        [SyncVar]
        private int _currentRound;

        [SyncVar]
        private bool _started;

        [SyncVar]
        private bool _complete;

        public int CurrentRound => _currentRound;
        public bool IsStarted => _started;
        public bool IsComplete => _complete;

        // ── Lifecycle ────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (rounds == null || rounds.Length == 0)
            {
                Debug.LogWarning("[MorsePuzzle] No rounds assigned!");
                return;
            }

            // Subscribe to each round's linked puzzle OnPuzzleSolved.
            for (int i = 0; i < rounds.Length; i++)
            {
                if (rounds[i] == null || rounds[i].LinkedPuzzle == null) continue;

                int roundIndex = i;
                rounds[i].LinkedPuzzle.OnPuzzleSolved.AddListener(
                    () => OnRoundSolved(roundIndex));
                rounds[i].LinkedPuzzle.OnPuzzleFailed.AddListener(
                    () => OnRoundFailed(roundIndex));
            }

            // Auto-start if no trigger zone.
            if (activationTrigger == null)
                Invoke(nameof(StartPuzzle), activationDelay);
        }

        // ── Trigger zone (optional) ─────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!isServer || _started) return;

            // Only start when a player enters.
            var identity = other.GetComponentInParent<NetworkIdentity>();
            if (identity == null) return;

            var stats = identity.GetComponent<CharacterStatsProvider>();
            if (stats == null || stats.Role != PlayerRole.Runner) return;

            StartPuzzle();
        }

        // ── Puzzle flow ──────────────────────────────────────

        [Server]
        private void StartPuzzle()
        {
            if (_started) return;
            _started = true;
            _currentRound = 0;

            Debug.Log("[MorsePuzzle] Puzzle started. Activating round 0.");
            ActivateRound(0);
        }

        [Server]
        private void ActivateRound(int index)
        {
            // Deactivate all emitters first.
            foreach (var emitter in rounds)
            {
                if (emitter != null)
                    emitter.Deactivate();
            }

            if (index < 0 || index >= rounds.Length) return;

            rounds[index].Activate();

            Debug.Log($"[MorsePuzzle] Round {index} active. " +
                      $"Pattern: {rounds[index].MorsePattern} " +
                      $"({rounds[index].DecodedLabel})");
        }

        [Server]
        private void OnRoundSolved(int roundIndex)
        {
            if (roundIndex != _currentRound) return;

            Debug.Log($"[MorsePuzzle] Round {roundIndex} solved!");

            // Deactivate current emitter.
            if (rounds[roundIndex] != null)
                rounds[roundIndex].Deactivate();

            _currentRound++;

            if (_currentRound >= rounds.Length)
            {
                _complete = true;
                Debug.Log("[MorsePuzzle] All rounds complete!");
                return;
            }

            // Short delay before next round.
            Invoke(nameof(ActivateNextRound), 1.5f);
        }

        [Server]
        private void OnRoundFailed(int roundIndex)
        {
            if (roundIndex != _currentRound) return;

            Debug.Log($"[MorsePuzzle] Round {roundIndex} failed — wrong section!");

            PuzzleEvents.RaiseNoiseGenerated(transform.position, wrongAnswerNoise);
        }

        [Server]
        private void ActivateNextRound()
        {
            ActivateRound(_currentRound);
        }
    }
}
