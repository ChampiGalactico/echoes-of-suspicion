using System.Collections;
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
    /// Accepts any MonoBehaviour that implements IPuzzleNode (Puzzle,
    /// MorsePuzzleCoordinator, etc.) via the demoPuzzleNodes array.
    ///
    /// SETUP:
    /// 1. Place on a persistent GameObject in the biome scene.
    /// 2. Assign demoPuzzleNodes in order — drag any component that
    ///    implements IPuzzleNode (Puzzle, MorsePuzzleCoordinator, etc.).
    /// 3. Assign the finalEventManager.
    /// 4. Optionally assign the heartbeatManager.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class DemoProgressionManager : NetworkBehaviour
    {
        [Header("Puzzles (in order)")]

        [SerializeField, Tooltip("All demo puzzle nodes in order. " +
                                 "Each must implement IPuzzleNode (Puzzle, MorsePuzzleCoordinator, etc.).")]
        private MonoBehaviour[] demoPuzzleNodes;

        [Header("References")]

        [SerializeField]
        private DemoFinalEventManager finalEventManager;

        [SerializeField]
        private DemoHeartbeatManager heartbeatManager;

        [SerializeField, Tooltip("Fax del Guía. Se activa al resolver el puzzle indicado en faxActivationPuzzleIndex.")]
        private EOS.GuideRoom.GuideFaxReceiver guideFaxReceiver;

        [SerializeField, Tooltip("Coordinador del puzzle de bills. Se inicia automáticamente junto con el fax.")]
        private BillsPuzzleCoordinator billsPuzzleCoordinator;

        [SerializeField, Tooltip("Coordinador del puzzle Morse. Se inicia al resolver el puzzle indicado en morseActivationPuzzleIndex.")]
        private Morse.MorsePuzzleCoordinator morsePuzzleCoordinator;

        [SerializeField, Tooltip("Índice del puzzle cuya resolución inicia el puzzle Morse (0-based). Default: 0 (después de CarRepair).")]
        private int morseActivationPuzzleIndex = 0;

        [SerializeField, Tooltip("Índice del puzzle cuya resolución activa el fax y el puzzle de bills (0-based). Default: 1 (puzzle 2).")]
        private int faxActivationPuzzleIndex = 1;

        [SerializeField, Tooltip("Delay en segundos entre resolver un puzzle y activar el siguiente sistema.")]
        private float puzzleTransitionDelay = 3f;

        [Header("Debug")]

        [SerializeField, Tooltip("Log progression events to console.")]
        private bool verbose = true;

        // ── State ────────────────────────────────────────────

        [SyncVar]
        private int _puzzlesSolved;

        /// <summary>
        /// Total tracked puzzles (may differ from demoPuzzleNodes.Length if
        /// billsPuzzleCoordinator is subscribed directly).
        /// </summary>
        private int _trackedPuzzleCount;

        public int PuzzlesSolved => _puzzlesSolved;
        public int TotalPuzzles => _trackedPuzzleCount > 0 ? _trackedPuzzleCount : (demoPuzzleNodes != null ? demoPuzzleNodes.Length : 0);
        public bool IsComplete => _puzzlesSolved >= TotalPuzzles;

        // ── Lifecycle ────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            // ── Diagnostic: show exactly what's assigned and what's missing ──
            Debug.Log("[DemoProgression] === SETUP DIAGNOSTIC ===");
            Debug.Log($"[DemoProgression] demoPuzzleNodes: {(demoPuzzleNodes != null ? demoPuzzleNodes.Length.ToString() : "NULL")} elements");
            Debug.Log($"[DemoProgression] finalEventManager: {(finalEventManager != null ? "OK" : "MISSING")}");
            Debug.Log($"[DemoProgression] heartbeatManager: {(heartbeatManager != null ? "OK" : "MISSING")}");
            Debug.Log($"[DemoProgression] morsePuzzleCoordinator: {(morsePuzzleCoordinator != null ? "OK" : "MISSING")} (activates after puzzle {morseActivationPuzzleIndex})");
            Debug.Log($"[DemoProgression] guideFaxReceiver: {(guideFaxReceiver != null ? "OK" : "MISSING")}");
            Debug.Log($"[DemoProgression] billsPuzzleCoordinator: {(billsPuzzleCoordinator != null ? "OK" : "MISSING")} (activates after puzzle {faxActivationPuzzleIndex})");

            if (demoPuzzleNodes == null || demoPuzzleNodes.Length == 0)
            {
                Debug.LogError("[DemoProgression] No puzzle nodes assigned! Nothing will be tracked.");
                return;
            }

            int validCount = 0;
            for (int i = 0; i < demoPuzzleNodes.Length; i++)
            {
                if (demoPuzzleNodes[i] == null)
                {
                    Debug.LogWarning($"[DemoProgression] Element {i}: NULL (empty slot)");
                    continue;
                }

                IPuzzleNode node = demoPuzzleNodes[i] as IPuzzleNode;
                if (node == null)
                {
                    Debug.LogWarning(
                        $"[DemoProgression] Element {i} ({demoPuzzleNodes[i].name}) " +
                        $"does not implement IPuzzleNode — skipping.", demoPuzzleNodes[i]);
                    continue;
                }

                Debug.Log($"[DemoProgression] Element {i}: {demoPuzzleNodes[i].name} (OK — {demoPuzzleNodes[i].GetType().Name})");
                int index = i;
                node.OnSolved += _ => OnPuzzleSolved(index);
                validCount++;
            }

            // ── Direct subscription to billsPuzzleCoordinator (safety net) ──
            // If billsPuzzleCoordinator is assigned but NOT in demoPuzzleNodes,
            // its completion would never be counted. Subscribe directly.
            if (billsPuzzleCoordinator != null)
            {
                bool billsInArray = false;
                for (int i = 0; i < demoPuzzleNodes.Length; i++)
                {
                    if (demoPuzzleNodes[i] != null &&
                        demoPuzzleNodes[i] == (MonoBehaviour)billsPuzzleCoordinator)
                    {
                        billsInArray = true;
                        break;
                    }
                }

                if (!billsInArray)
                {
                    billsPuzzleCoordinator.OnAllBillsPaid += OnBillsCompletedDirect;
                    validCount++;
                    Debug.LogWarning(
                        "[DemoProgression] billsPuzzleCoordinator is NOT in demoPuzzleNodes! " +
                        "Subscribed directly via OnAllBillsPaid as extra puzzle.");
                }
            }

            _trackedPuzzleCount = validCount;

            Debug.Log($"[DemoProgression] Tracking {_trackedPuzzleCount} total puzzle nodes. " +
                      $"Final event triggers after all {_trackedPuzzleCount} are solved.");
            Debug.Log("[DemoProgression] === END DIAGNOSTIC ===");
        }

        // ── Puzzle completion ────────────────────────────────

        [Server]
        private void OnPuzzleSolved(int puzzleIndex)
        {
            _puzzlesSolved++;

            if (verbose)
                Debug.Log($"[DemoProgression] Puzzle {puzzleIndex} solved. " +
                          $"Progress: {_puzzlesSolved}/{_trackedPuzzleCount}");

            RpcNotifyPuzzleSolved(puzzleIndex, _puzzlesSolved, _trackedPuzzleCount);

            // Start Morse puzzle when the designated puzzle is solved.
            if (puzzleIndex == morseActivationPuzzleIndex && morsePuzzleCoordinator != null)
                StartCoroutine(ActivateMorseAfterDelay());

            // Activate Guide fax receiver and start bills puzzle when the designated puzzle is solved.
            if (puzzleIndex == faxActivationPuzzleIndex)
                StartCoroutine(ActivateBillsAfterDelay());

            CheckAllPuzzlesComplete();
        }

        /// <summary>
        /// Direct callback for when billsPuzzleCoordinator completes
        /// but is not tracked through demoPuzzleNodes.
        /// </summary>
        [Server]
        private void OnBillsCompletedDirect()
        {
            _puzzlesSolved++;

            if (verbose)
                Debug.Log($"[DemoProgression] Bills puzzle completed (direct). " +
                          $"Progress: {_puzzlesSolved}/{_trackedPuzzleCount}");

            RpcNotifyPuzzleSolved(-1, _puzzlesSolved, _trackedPuzzleCount);
            CheckAllPuzzlesComplete();
        }

        [Server]
        private void CheckAllPuzzlesComplete()
        {
            if (_puzzlesSolved >= _trackedPuzzleCount)
            {
                if (verbose)
                    Debug.Log("[DemoProgression] All puzzles solved — triggering final event!");

                if (finalEventManager != null)
                    finalEventManager.TriggerFinalEvent();
                else
                    Debug.LogError("[DemoProgression] finalEventManager is NULL! Cannot trigger final event.");
            }
        }

        // ── Delayed activation ───────────────────────────────

        [Server]
        private IEnumerator ActivateMorseAfterDelay()
        {
            if (verbose)
                Debug.Log($"[DemoProgression] Waiting {puzzleTransitionDelay}s before starting Morse...");

            yield return new WaitForSeconds(puzzleTransitionDelay);

            if (morsePuzzleCoordinator != null)
            {
                morsePuzzleCoordinator.ServerStartPuzzle();

                if (verbose)
                    Debug.Log("[DemoProgression] Morse puzzle started.");
            }
        }

        [Server]
        private IEnumerator ActivateBillsAfterDelay()
        {
            if (verbose)
                Debug.Log($"[DemoProgression] Waiting {puzzleTransitionDelay}s before activating bills...");

            yield return new WaitForSeconds(puzzleTransitionDelay);

            if (guideFaxReceiver != null)
                guideFaxReceiver.Activate();

            if (billsPuzzleCoordinator != null)
                billsPuzzleCoordinator.StartBillsPuzzle();
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
