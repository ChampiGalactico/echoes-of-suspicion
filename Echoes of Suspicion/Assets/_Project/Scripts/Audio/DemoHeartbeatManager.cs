using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Progressive heartbeat that escalates with each puzzle solved.
    ///
    /// Independent of RunnerCreatureAwareness heartbeat (creature proximity).
    /// This heartbeat represents narrative tension — the player feels
    /// something is wrong without knowing why. Both systems can coexist;
    /// use a slightly different clip or pitch to distinguish them.
    ///
    /// SETUP:
    /// 1. Place on a persistent GameObject in the biome scene.
    /// 2. Assign the demo puzzles in order.
    /// 3. Assign the heartbeat AudioClip.
    /// 4. Configure intervals per stage.
    ///
    /// The heartbeat only plays on the Runner's client (TargetRpc).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class DemoHeartbeatManager : NetworkBehaviour
    {
        [Header("Audio")]

        [SerializeField, Tooltip("Heartbeat clip (single 'tun'). " +
                                 "Use a different clip or pitch than HeartbeatAudioFeedback to distinguish.")]
        private AudioClip heartbeatClip;

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.75f;

        [SerializeField, Tooltip("Pitch multiplier. Increase slightly (1.1-1.2) to sound different from creature heartbeat.")]
        private float pitch = 1.1f;

        [Header("Progression")]

        [SerializeField, Tooltip("Heartbeat interval after each puzzle. " +
                                 "Index 0 = after puzzle 1, index 1 = after puzzle 2, etc. " +
                                 "Lower = faster = more tense.")]
        private float[] stageIntervals = { 3.0f, 1.5f, 0.6f };

        [SerializeField, Tooltip("Interval during the final event (door opening, jumpscare incoming).")]
        private float panicInterval = 0.3f;

        [Header("Puzzles")]

        [SerializeField, Tooltip("Demo puzzle nodes in order. Each must implement IPuzzleNode " +
                                 "(Puzzle, MorsePuzzleCoordinator, etc.).")]
        private MonoBehaviour[] demoPuzzleNodes;

        // ── State ────────────────────────────────────────────

        [SyncVar]
        private int _currentStage = -1; // -1 = silent, 0 = after puzzle 1, etc.

        [SyncVar]
        private bool _panicMode;

        private float _lastBeatTime;
        private AudioSource _audioSource;
        private bool _isRunnerLocal;

        // ── Lifecycle ────────────────────────────────────────

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (demoPuzzleNodes == null) return;

            for (int i = 0; i < demoPuzzleNodes.Length; i++)
            {
                if (demoPuzzleNodes[i] == null) continue;

                IPuzzleNode node = demoPuzzleNodes[i] as IPuzzleNode;
                if (node == null)
                {
                    Debug.LogWarning(
                        $"[DemoHeartbeat] Element {i} ({demoPuzzleNodes[i].name}) " +
                        $"does not implement IPuzzleNode — skipping.", demoPuzzleNodes[i]);
                    continue;
                }

                int stage = i;
                node.OnSolved += _ => OnPuzzleSolved(stage);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.spatialBlend = 0f; // 2D — internal sensation.
            _audioSource.playOnAwake = false;
            _audioSource.pitch = pitch;
        }

        private void Update()
        {
            // Client-side heartbeat playback.
            if (!IsActiveOnRunner()) return;

            float interval = GetCurrentInterval();
            if (interval <= 0f) return;

            if (Time.time - _lastBeatTime < interval) return;

            _lastBeatTime = Time.time;
            PlayBeat();
        }

        // ── Server: puzzle progression ───────────────────────

        [Server]
        private void OnPuzzleSolved(int stageIndex)
        {
            if (stageIndex <= _currentStage) return;
            _currentStage = stageIndex;

            Debug.Log($"[DemoHeartbeat] Stage advanced to {_currentStage}. " +
                      $"Interval: {GetStageInterval(_currentStage):F1}s");
        }

        /// <summary>
        /// Called by DemoFinalEventManager to activate panic heartbeat.
        /// </summary>
        [Server]
        public void ActivatePanicMode()
        {
            _panicMode = true;
            Debug.Log($"[DemoHeartbeat] PANIC MODE — interval: {panicInterval:F1}s");
        }

        /// <summary>
        /// Called to stop all heartbeat (e.g. after fade to black).
        /// </summary>
        [Server]
        public void StopHeartbeat()
        {
            _currentStage = -1;
            _panicMode = false;
        }

        // ── Interval calculation ─────────────────────────────

        private float GetCurrentInterval()
        {
            if (_panicMode) return panicInterval;
            if (_currentStage < 0) return -1f; // Silent.
            return GetStageInterval(_currentStage);
        }

        private float GetStageInterval(int stage)
        {
            if (stageIntervals == null || stageIntervals.Length == 0)
                return 2f;

            int idx = Mathf.Clamp(stage, 0, stageIntervals.Length - 1);
            return stageIntervals[idx];
        }

        // ── Audio playback (client) ──────────────────────────

        private void PlayBeat()
        {
            if (heartbeatClip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(heartbeatClip, volume);
        }

        /// <summary>
        /// Returns true if this client is the Runner and should hear the heartbeat.
        /// Caches the lookup to avoid per-frame FindPlayer calls.
        /// </summary>
        private bool IsActiveOnRunner()
        {
            if (_currentStage < 0 && !_panicMode) return false;

            var local = NetworkClient.localPlayer;
            if (local == null) return false;

            var stats = local.GetComponent<CharacterStatsProvider>();
            return stats != null && stats.Role == PlayerRole.Runner;
        }
    }
}
