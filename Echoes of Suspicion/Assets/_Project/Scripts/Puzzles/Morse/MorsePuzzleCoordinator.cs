using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Coordinador autoritativo del puzzle Morse. Es la única fuente de
    /// verdad: genera la secuencia en el servidor, mantiene el índice actual,
    /// valida cada interacción y controla éxito/fallo/reinicio, ruido y daño.
    ///
    /// Implementa IPuzzleNode para poder engancharse a PuzzleDoor igual que
    /// cualquier Puzzle del proyecto.
    ///
    /// SINCRONIZACIÓN: solo se replican datos pequeños y seguros —
    /// la secuencia como string de letras (p. ej. "SGE"), el índice actual,
    /// y flags de estado. Nunca se sincronizan ScriptableObjects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MorsePuzzleCoordinator : NetworkBehaviour, IPuzzleNode
    {
        [Header("Identidad")]

        [SerializeField]
        private string nodeId = "morse.corridor";

        [Header("Definición (asset local, NO se sincroniza)")]

        [SerializeField]
        private MorsePuzzleDefinition definition;

        [Header("Emisor")]

        [SerializeField, Tooltip("Emisor de audio Morse (uno solo, reproduce todos los patrones).")]
        private MorseEmitter emitter;

        [Header("Teclado")]

        [SerializeField, Tooltip("Teclado Morse (contiene los paneles y el display).")]
        private MorseKeyboard keyboard;

        [Header("Eventos de feedback")]

        public UnityEvent OnPuzzleStarted;
        public UnityEvent OnStepAdvanced;
        public UnityEvent OnStepFailed;
        public UnityEvent OnPuzzleSolvedEvent;
        public UnityEvent OnPuzzleResetEvent;

        [Header("Debug")]

        [SerializeField]
        private bool verboseLogging = false;

        // ─── Estado sincronizado (datos pequeños y seguros) ───

        // Sin hook: la presentación de estos dos valores se reconstruye vía
        // RPC (clientes activos) y OnStartClient (late join). No necesitan
        // reaccionar por sí solos, así evitamos hooks vacíos.
        [SyncVar]
        private string syncedSequence = string.Empty;

        [SyncVar]
        private int syncedCurrentIndex = -1;

        [SyncVar(hook = nameof(OnStartedChanged))]
        private bool syncedStarted = false;

        [SyncVar(hook = nameof(OnSolvedChanged))]
        private bool syncedSolved = false;

        // ─── Estado solo-servidor ───

        private readonly List<string> serverSequence = new();
        private bool serverBusy;
        private Coroutine serverAdvanceRoutine;

        // ─── IPuzzleNode ───

        public string NodeId => nodeId;
        public bool IsSolved => syncedSolved;
        public event Action<IPuzzleNode> OnSolved;

        // ─── Accessors de presentación ───

        /// <summary>
        /// Un panel puede previsualizarse/activarse solo si el puzzle empezó,
        /// no está resuelto. La comprobación de rol la hace el propio panel.
        /// </summary>
        public bool IsPuzzleActiveForPreview =>
            syncedStarted && !syncedSolved;

        public int CurrentIndex => syncedCurrentIndex;

        // =====================================================================
        //  LIFECYCLE
        // =====================================================================

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Late join: reconstruir presentación desde el estado sincronizado.
            RebuildPresentationFromState();
        }

        // =====================================================================
        //  ARRANQUE (servidor)
        // =====================================================================

        /// <summary>
        /// Inicia el puzzle en el servidor. Idempotente: si ya empezó y no se
        /// ha resuelto, no hace nada. Llamado por el trigger de escena.
        /// </summary>
        [Server]
        public void ServerStartPuzzle()
        {
            if (syncedStarted && !syncedSolved)
            {
                return; // ya activo, evita reinicios accidentales
            }

            if (definition == null)
            {
                Debug.LogError(
                    "[MorsePuzzleCoordinator] Falta MorsePuzzleDefinition.",
                    this);
                return;
            }

            GenerateSequence();

            syncedSolved = false;
            serverBusy = false;
            syncedSequence = string.Join(string.Empty, serverSequence);
            syncedCurrentIndex = 0;
            syncedStarted = true;

            if (verboseLogging)
            {
                Debug.Log(
                    $"[MorsePuzzleCoordinator] Secuencia generada: " +
                    $"{syncedSequence}", this);
            }

            // Pass the pattern directly — avoids SyncVar timing race.
            string firstSymbol = serverSequence[0];
            string firstPattern = MorseAlphabet.GetPattern(firstSymbol);
            RpcPlayEmitterDirect(firstPattern);
            RpcUpdateWordDisplay(0); // Show all underscores.
        }

        [Server]
        private void GenerateSequence()
        {
            serverSequence.Clear();

            if (definition.HasTargetWord)
            {
                // Modo palabra: cada letra de la palabra es un paso.
                string word = definition.TargetWord;
                foreach (char c in word)
                {
                    string letter = c.ToString();
                    if (MorseAlphabet.IsValidSymbol(letter))
                        serverSequence.Add(letter.ToUpperInvariant());
                }

                if (serverSequence.Count == 0)
                {
                    Debug.LogError(
                        "[MorsePuzzleCoordinator] targetWord no contiene letras " +
                        "válidas del alfabeto Morse.", this);
                }
            }
            else
            {
                // Modo aleatorio: elige símbolos distintos del pool.
                List<string> pool = new();
                foreach (string candidate in definition.AllowedSymbols)
                {
                    if (MorseAlphabet.IsValidSymbol(candidate) &&
                        !pool.Contains(candidate))
                    {
                        pool.Add(candidate);
                    }
                }

                int length = Mathf.Min(definition.SequenceLength, pool.Count);

                for (int i = 0; i < length; i++)
                {
                    int pick = UnityEngine.Random.Range(0, pool.Count);
                    serverSequence.Add(pool[pick]);
                    pool.RemoveAt(pick);
                }
            }
        }

        // =====================================================================
        //  VALIDACIÓN (servidor)
        // =====================================================================

        /// <summary>True si el coordinador puede aceptar una interacción ahora.</summary>
        [Server]
        public bool CanAcceptPanelInteraction()
        {
            return syncedStarted && !syncedSolved && !serverBusy;
        }

        /// <summary>
        /// Recibe el símbolo elegido por el Runner desde un MorsePanel.
        /// Autoritativo: decide acierto/fallo, avanza o repite.
        /// </summary>
        [Server]
        public void ServerSubmitSymbol(
            string symbolId, MorsePanel panel, NetworkIdentity interactor)
        {
            if (!CanAcceptPanelInteraction())
            {
                return;
            }

            if (syncedCurrentIndex < 0 ||
                syncedCurrentIndex >= serverSequence.Count)
            {
                return;
            }

            serverBusy = true;

            string expected = serverSequence[syncedCurrentIndex];
            bool correct = string.Equals(
                symbolId, expected, StringComparison.OrdinalIgnoreCase);

            if (correct)
            {
                HandleCorrect(panel);
            }
            else
            {
                HandleIncorrect(panel);
            }
        }

        [Server]
        private void HandleCorrect(MorsePanel panel)
        {
            RpcStopEmitter();

            if (panel != null)
            {
                RpcPanelFeedback(
                    panel.netIdentity,
                    (int)MorsePanel.PanelVisualState.CurrentSuccess);
            }

            RpcOnStepAdvanced();

            // Reveal the letter just solved on the display.
            int nextIndex = syncedCurrentIndex + 1;
            RpcUpdateWordDisplay(nextIndex);

            if (nextIndex >= serverSequence.Count)
            {
                HandleSolved();
                return;
            }

            if (serverAdvanceRoutine != null)
            {
                StopCoroutine(serverAdvanceRoutine);
            }

            serverAdvanceRoutine =
                StartCoroutine(ServerAdvanceAfterDelay(nextIndex));
        }

        [Server]
        private IEnumerator ServerAdvanceAfterDelay(int nextIndex)
        {
            yield return new WaitForSeconds(definition.AdvanceDelay);

            syncedCurrentIndex = nextIndex;
            serverBusy = false;

            RpcPlayEmitter(nextIndex);
            serverAdvanceRoutine = null;
        }

        [Server]
        private void HandleIncorrect(MorsePanel panel)
        {
            // El índice NO avanza. Aplica daño, ruido y feedback, y repite
            // el patrón del paso actual (o reinicia toda la secuencia si la
            // definición lo pide).
            ApplyRunnerDamage();
            RaiseFailureNoise(panel);
            RaiseGuidePenalty();

            if (panel != null)
            {
                RpcPanelFeedback(
                    panel.netIdentity,
                    (int)MorsePanel.PanelVisualState.Failure);
            }

            RpcOnStepFailed();

            if (serverAdvanceRoutine != null)
            {
                StopCoroutine(serverAdvanceRoutine);
            }

            serverAdvanceRoutine =
                StartCoroutine(ServerRetryAfterDelay());
        }

        [Server]
        private IEnumerator ServerRetryAfterDelay()
        {
            yield return new WaitForSeconds(definition.RetryDelay);

            if (definition.ResetWholeSequenceOnFailure)
            {
                syncedCurrentIndex = 0;
                RpcUpdateWordDisplay(0); // Clear all revealed letters.
            }

            serverBusy = false;
            RpcPlayEmitter(syncedCurrentIndex);
            serverAdvanceRoutine = null;
        }

        [Server]
        private void HandleSolved()
        {
            syncedSolved = true;
            serverBusy = false;

            RpcStopEmitter();
            RpcUpdateWordDisplay(serverSequence.Count); // Reveal full word.
            RpcOnSolved();

            OnSolved?.Invoke(this);

            if (verboseLogging)
            {
                Debug.Log("[MorsePuzzleCoordinator] Puzzle resuelto.", this);
            }
        }

        /// <summary>Reinicio explícito desde servidor (p. ej. depuración).</summary>
        [Server]
        public void ServerResetPuzzle()
        {
            if (serverAdvanceRoutine != null)
            {
                StopCoroutine(serverAdvanceRoutine);
                serverAdvanceRoutine = null;
            }

            serverBusy = false;
            syncedSolved = false;
            syncedStarted = false;
            syncedCurrentIndex = -1;
            syncedSequence = string.Empty;
            serverSequence.Clear();

            RpcStopEmitter();
            RpcUpdateWordDisplay(0);
            RpcOnReset();
        }

        // =====================================================================
        //  DAÑO / RUIDO — reutiliza los sistemas existentes
        // =====================================================================

        [Server]
        private void ApplyRunnerDamage()
        {
            if (definition.DamageOnFailure <= 0f)
            {
                return;
            }

            CharacterStatsProvider runner =
                PlayerUtils.FindPlayerByRole(PlayerRole.Runner);

            if (runner == null)
            {
                return;
            }

            PlayerHealth health = runner.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(definition.DamageOnFailure);
            }
        }

        [Server]
        private void RaiseFailureNoise(MorsePanel panel)
        {
            Vector3 origin = panel != null
                ? panel.transform.position
                : transform.position;

            // 1) Convención de puzzles del proyecto.
            PuzzleEvents.RaiseNoiseGenerated(
                origin, definition.FailureNoiseLevel);

            // 2) Bus que las criaturas escuchan realmente (NoiseEventBus).
            uint runnerNetId = ResolveRunnerNetId();
            NoiseEventBus.Publish(new NoiseEvent(
                origin,
                definition.FailureNoiseIntensity,
                NoiseSource.ObjectImpact,
                runnerNetId));
        }

        [Server]
        private void RaiseGuidePenalty()
        {
            if (definition.GuideHealthPenaltyOnFailure > 0f)
            {
                PuzzleEvents.RaiseGuideHealthPenalty(
                    definition.GuideHealthPenaltyOnFailure);
            }
        }

        [Server]
        private uint ResolveRunnerNetId()
        {
            CharacterStatsProvider runner =
                PlayerUtils.FindPlayerByRole(PlayerRole.Runner);

            return runner != null ? runner.netId : 0u;
        }

        // =====================================================================
        //  CLIENT RPCs — presentación (emisores y feedback)
        // =====================================================================

        [ClientRpc]
        private void RpcPlayEmitter(int index)
        {
            PlayEmitterPattern(index);
        }

        /// <summary>
        /// Plays a morse pattern directly on all clients, bypassing SyncVar lookup.
        /// Used for the initial puzzle start to avoid SyncVar timing race.
        /// </summary>
        [ClientRpc]
        private void RpcPlayEmitterDirect(string pattern)
        {
            if (emitter == null)
            {
                Debug.LogWarning("[MorsePuzzleCoordinator] RpcPlayEmitterDirect: emitter is null!");
                return;
            }

            if (string.IsNullOrEmpty(pattern))
            {
                Debug.LogWarning("[MorsePuzzleCoordinator] RpcPlayEmitterDirect: pattern is empty!");
                return;
            }

            Debug.Log($"[MorsePuzzleCoordinator] RpcPlayEmitterDirect: playing pattern '{pattern}'");
            emitter.PlayPattern(pattern);
        }

        [ClientRpc]
        private void RpcStopEmitter()
        {
            if (emitter != null)
                emitter.StopEmitting();
        }

        [ClientRpc]
        private void RpcPanelFeedback(NetworkIdentity panelIdentity, int state)
        {
            if (panelIdentity == null)
            {
                return;
            }

            MorsePanel panel = panelIdentity.GetComponent<MorsePanel>();
            if (panel != null)
            {
                panel.ApplyVisualState((MorsePanel.PanelVisualState)state);
            }
        }

        [ClientRpc]
        private void RpcUpdateWordDisplay(int revealedCount)
        {
            UpdateWordDisplay(revealedCount);
        }

        [ClientRpc]
        private void RpcOnStepAdvanced() => OnStepAdvanced?.Invoke();

        [ClientRpc]
        private void RpcOnStepFailed() => OnStepFailed?.Invoke();

        [ClientRpc]
        private void RpcOnSolved()
        {
            MarkAllPanelsSolved();
            OnPuzzleSolvedEvent?.Invoke();
        }

        [ClientRpc]
        private void RpcOnReset()
        {
            ResetAllPanelVisuals();
            OnPuzzleResetEvent?.Invoke();
        }

        // =====================================================================
        //  SYNCVAR HOOKS — reconstrucción de presentación en clientes
        // =====================================================================

        private void OnStartedChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                OnPuzzleStarted?.Invoke();
            }
        }

        private void OnSolvedChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                MarkAllPanelsSolved();
            }
        }

        // =====================================================================
        //  HELPERS DE PRESENTACIÓN
        // =====================================================================

        private void RebuildPresentationFromState()
        {
            if (!syncedStarted)
            {
                return;
            }

            if (syncedSolved)
            {
                StopEmitter();
                MarkAllPanelsSolved();
                UpdateWordDisplay(syncedSequence.Length);
                return;
            }

            PlayEmitterPattern(syncedCurrentIndex);
            UpdateWordDisplay(syncedCurrentIndex);
        }

        private void PlayEmitterPattern(int index)
        {
            if (emitter == null)
            {
                Debug.LogWarning($"[MorsePuzzleCoordinator] PlayEmitterPattern({index}): emitter is null!");
                return;
            }

            string symbol = GetSymbolAt(index);
            if (string.IsNullOrEmpty(symbol))
            {
                Debug.LogWarning(
                    $"[MorsePuzzleCoordinator] PlayEmitterPattern({index}): " +
                    $"symbol is empty (syncedSequence='{syncedSequence}')");
                return;
            }

            string pattern = MorseAlphabet.GetPattern(symbol);
            Debug.Log($"[MorsePuzzleCoordinator] PlayEmitterPattern({index}): symbol='{symbol}', pattern='{pattern}'");
            emitter.PlayPattern(pattern);
        }

        private void StopEmitter()
        {
            if (emitter != null)
                emitter.StopEmitting();
        }

        /// <summary>
        /// Construye el texto del display: letras reveladas + guiones bajos.
        /// Ej: secuencia "SGE", currentIndex 1 → "S _ _"
        /// </summary>
        private string BuildWordDisplayText(int revealedCount)
        {
            if (string.IsNullOrEmpty(syncedSequence)) return "";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < syncedSequence.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(i < revealedCount ? syncedSequence[i] : '_');
            }
            return sb.ToString();
        }

        private void UpdateWordDisplay(int revealedCount)
        {
            if (keyboard != null)
                keyboard.UpdateWordDisplay(BuildWordDisplayText(revealedCount));
        }

        private string GetSymbolAt(int index)
        {
            if (string.IsNullOrEmpty(syncedSequence) ||
                index < 0 || index >= syncedSequence.Length)
            {
                return string.Empty;
            }

            return syncedSequence[index].ToString();
        }

        private MorsePanel[] GetPanels()
        {
            return keyboard != null ? keyboard.GetAllPanels() : null;
        }

        private void MarkAllPanelsSolved()
        {
            MorsePanel[] p = GetPanels();
            if (p == null) return;

            foreach (MorsePanel panel in p)
            {
                if (panel != null)
                    panel.ApplyVisualState(MorsePanel.PanelVisualState.Solved);
            }
        }

        private void ResetAllPanelVisuals()
        {
            MorsePanel[] p = GetPanels();
            if (p == null) return;

            foreach (MorsePanel panel in p)
            {
                if (panel != null)
                    panel.ApplyVisualState(MorsePanel.PanelVisualState.Idle);
            }
        }

        // =====================================================================
        //  WIRING (usado por el builder de Editor)
        // =====================================================================

        public void EditorConfigure(
            MorsePuzzleDefinition def,
            MorseEmitter singleEmitter,
            MorseKeyboard morseKeyboard)
        {
            definition = def;
            emitter = singleEmitter;
            keyboard = morseKeyboard;
        }
    }
}
