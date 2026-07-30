using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Tracks overall demo progression across all puzzles and
    /// triggers the final event when the last puzzle is solved.
    ///
    /// Also acts as the bridge between individual puzzle systems
    /// (Morse, Bills) and the DemoHeartbeatManager / DemoFinalEventManager.
    ///
    /// SETUP:
    /// 1. Place on a persistent GameObject in the biome scene.
    /// 2. Assign demoPuzzles in order (same list as DemoHeartbeatManager).
    /// 3. Assign the finalEventManager.
    /// 4. Optionally assign the heartbeatManager (it also self-subscribes,
    ///    but this allows manual override).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class DemoProgressionManager : NetworkBehaviour
    {
        [Header("Puzzles (in order)")]

        [SerializeField, Tooltip("All demo puzzles in order. Last one triggers the final event.")]
        private Puzzle[] demoPuzzles;

        [Header("References")]

        [SerializeField]
        private DemoFinalEventManager finalEventManager;

        [SerializeField]
        private DemoHeartbeatManager heartbeatManager;

        [Header("Debug")]

        [SerializeField, Tooltip("Log progression events to console.")]
        private bool verbose = true;

        // ── State ────────────────────────────────────────────

        [SyncVar]
        private int _puzzlesSolved;

        public int PuzzlesSolved => _puzzlesSolved;
        public int TotalPuzzles => demoPuzzles != null ? demoPuzzles.Length : 0;
        public bool IsComplete => _puzzlesSolved >= TotalPuzzles;

        // ── Lifecycle ────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (demoPuzzles == null || demoPuzzles.Length == 0)
            {
                Debug.LogWarning("[DemoProgression] No puzzles assigned!");
                return;
            }

            for (int i = 0; i < demoPuzzles.Length; i++)
            {
                if (demoPuzzles[i] == null) continue;

                int index = i;
                demoPuzzles[i].OnPuzzleSolved.AddListener(() => OnPuzzleSolved(index));
            }

            if (verbose)
                Debug.Log($"[DemoProgression] Tracking {demoPuzzles.Length} puzzles.");
        }

        // ── Puzzle completion ────────────────────────────────

        [Server]
        private void OnPuzzleSolved(int puzzleIndex)
        {
            _puzzlesSolved++;

            if (verbose)
                Debug.Log($"[DemoProgression] Puzzle {puzzleIndex} solved. " +
                          $"Progress: {_puzzlesSolved}/{demoPuzzles.Length}");

            RpcNotifyPuzzleSolved(puzzleIndex, _puzzlesSolved, demoPuzzles.Length);

            // Check if this was the last puzzle.
            if (_puzzlesSolved >= demoPuzzles.Length)
            {
                if (verbose)
                    Debug.Log("[DemoProgression] All puzzles solved — triggering final event!");

                if (finalEventManager != null)
                    finalEventManager.TriggerFinalEvent();
            }
        }

        // ── Client feedback ──────────────────────────────────

        [ClientRpc]
        private void RpcNotifyPuzzleSolved(int puzzleIndex, int solved, int total)
        {
            if (verbose)
                Debug.Log($"[DemoProgression] Puzzle complete! ({solved}/{total})");
        }
    }
}
