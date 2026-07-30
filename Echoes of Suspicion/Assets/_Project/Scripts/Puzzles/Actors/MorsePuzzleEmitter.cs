using System.Collections;
using Mirror;
using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Positional audio emitter that plays a Morse code pattern in a loop.
    ///
    /// Place on a wall/object in the lava hallway. When the Runner gets
    /// close enough they hear the pattern and relay it to the Guide over
    /// voice chat. The Guide decodes it with their Morse reference table.
    ///
    /// Each emitter is linked to a wall section (RatInteractable) that
    /// the Runner activates once the Guide tells them the correct one.
    ///
    /// SETUP:
    /// 1. Attach to a GameObject with a collider (trigger zone optional).
    /// 2. Assign the morseClip (a short pure tone, ~200ms).
    /// 3. Set morsePattern using dots and dashes: ".- " = A, "..." = S, etc.
    ///    Spaces between letters, " / " between words (optional).
    /// 4. Assign linkedPuzzle — the child Puzzle this emitter represents.
    /// 5. The emitter starts looping when activated by MorsePuzzleController.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class MorsePuzzleEmitter : NetworkBehaviour
    {
        [Header("Morse Pattern")]

        [SerializeField, Tooltip("Morse pattern. Use '.' for dot, '-' for dash, ' ' between letters, ' / ' between words.")]
        private string morsePattern = ".- ";

        [SerializeField, Tooltip("Label for this emitter (e.g. the decoded letter). " +
                                 "Used internally for debugging. The Guide decodes from the sound.")]
        private string decodedLabel = "A";

        [Header("Timing")]

        [SerializeField, Min(0.05f)]
        private float dotDuration = 0.15f;

        [SerializeField, Min(0.1f)]
        private float dashDuration = 0.45f;

        [SerializeField, Min(0.05f)]
        private float symbolPause = 0.1f;

        [SerializeField, Min(0.1f)]
        private float letterPause = 0.3f;

        [SerializeField, Min(0.5f)]
        private float wordPause = 0.7f;

        [SerializeField, Min(0.5f), Tooltip("Pause before replaying the full pattern.")]
        private float loopPause = 2.0f;

        [Header("Audio")]

        [SerializeField, Tooltip("Short tone clip for dots and dashes. " +
                                 "A single sine wave beep works best.")]
        private AudioClip morseClip;

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.8f;

        [SerializeField, Tooltip("Pitch for the tone. Vary between emitters " +
                                 "for easier differentiation.")]
        private float tonePitch = 1.0f;

        [Header("Visual Feedback")]

        [SerializeField, Tooltip("Optional light that pulses with each symbol.")]
        private Light pulseLight;

        [SerializeField]
        private Color lightColor = new Color(1f, 0.4f, 0.1f);

        [SerializeField]
        private float lightIntensity = 2f;

        [Header("Puzzle")]

        [SerializeField, Tooltip("The child Puzzle that gets solved when the " +
                                 "Runner activates the correct wall section for this emitter.")]
        private Puzzle linkedPuzzle;

        // ── State ────────────────────────────────────────────

        [SyncVar(hook = nameof(OnActiveChanged))]
        private bool _isActive;

        private AudioSource _audioSource;
        private Coroutine _loopRoutine;

        // ── Public API ───────────────────────────────────────

        public string DecodedLabel => decodedLabel;
        public string MorsePattern => morsePattern;
        public Puzzle LinkedPuzzle => linkedPuzzle;
        public bool IsActive => _isActive;

        /// <summary>
        /// Server-side: start emitting the Morse pattern.
        /// Called by MorsePuzzleController when it's this emitter's turn.
        /// </summary>
        [Server]
        public void Activate()
        {
            _isActive = true;
        }

        /// <summary>
        /// Server-side: stop emitting.
        /// </summary>
        [Server]
        public void Deactivate()
        {
            _isActive = false;
        }

        // ── Lifecycle ────────────────────────────────────────

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.spatialBlend = 1f; // Fully 3D.
            _audioSource.playOnAwake = false;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.maxDistance = 12f;
            _audioSource.minDistance = 1f;
            _audioSource.pitch = tonePitch;

            if (pulseLight != null)
            {
                pulseLight.color = lightColor;
                pulseLight.intensity = 0f;
            }
        }

        private void OnActiveChanged(bool oldVal, bool newVal)
        {
            if (newVal)
                StartLoop();
            else
                StopLoop();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (_isActive)
                StartLoop();
        }

        private void OnDisable()
        {
            StopLoop();
        }

        // ── Loop ─────────────────────────────────────────────

        private void StartLoop()
        {
            StopLoop();
            _loopRoutine = StartCoroutine(MorseLoop());
        }

        private void StopLoop()
        {
            if (_loopRoutine != null)
            {
                StopCoroutine(_loopRoutine);
                _loopRoutine = null;
            }

            if (pulseLight != null)
                pulseLight.intensity = 0f;
        }

        private IEnumerator MorseLoop()
        {
            while (true)
            {
                yield return StartCoroutine(PlayPattern());
                yield return new WaitForSeconds(loopPause);
            }
        }

        private IEnumerator PlayPattern()
        {
            for (int i = 0; i < morsePattern.Length; i++)
            {
                char c = morsePattern[i];

                switch (c)
                {
                    case '.':
                        PlayTone(dotDuration);
                        yield return new WaitForSeconds(dotDuration + symbolPause);
                        break;

                    case '-':
                        PlayTone(dashDuration);
                        yield return new WaitForSeconds(dashDuration + symbolPause);
                        break;

                    case '/':
                        yield return new WaitForSeconds(wordPause);
                        break;

                    case ' ':
                        // Check if it's " / " word separator (handled by '/').
                        // Otherwise it's a letter pause.
                        if (i + 1 < morsePattern.Length && morsePattern[i + 1] != '/')
                            yield return new WaitForSeconds(letterPause);
                        break;
                }
            }
        }

        private void PlayTone(float duration)
        {
            if (morseClip == null || _audioSource == null) return;

            _audioSource.PlayOneShot(morseClip, volume);

            if (pulseLight != null)
                StartCoroutine(PulseRoutine(duration));
        }

        private IEnumerator PulseRoutine(float duration)
        {
            if (pulseLight == null) yield break;

            pulseLight.intensity = lightIntensity;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                pulseLight.intensity = Mathf.Lerp(lightIntensity, 0f, elapsed / duration);
                yield return null;
            }

            pulseLight.intensity = 0f;
        }
    }
}
