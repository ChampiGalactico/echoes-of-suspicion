using System.Collections;
using UnityEngine;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Emisor posicional que reproduce un patrón Morse ("." / "-") mediante
    /// un AudioSource 3D. Solo el emisor del paso actual debe estar activo;
    /// el coordinador se encarga de encender uno y apagar el resto.
    ///
    /// Si no se asigna un AudioClip, genera en runtime un tono sinusoidal
    /// sencillo, para que el prototipo funcione sin assets externos.
    ///
    /// Este componente es puramente de presentación (audio). No decide
    /// lógica de puzzle ni toca la red.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MorseEmitter : MonoBehaviour
    {
        [Header("Tiempos")]

        [SerializeField, Min(0.02f)]
        private float dotDuration = 0.22f;

        [SerializeField, Min(1f)]
        private float dashMultiplier = 3f;

        [Tooltip("Silencio entre pulsos del mismo símbolo.")]
        [SerializeField, Min(0f)]
        private float intraSymbolGap = 0.18f;

        [Tooltip("Silencio antes de repetir el patrón completo.")]
        [SerializeField, Min(0f)]
        private float repeatDelay = 1.1f;

        [Header("Tono")]

        [Tooltip("Clip opcional. Si es null se genera un tono sinusoidal.")]
        [SerializeField]
        private AudioClip toneClip;

        [SerializeField, Min(20f)]
        private float frequency = 620f;

        [SerializeField, Range(0f, 1f)]
        private float volume = 0.7f;

        [Header("Espacialización 3D")]

        [SerializeField, Min(0.1f)]
        private float minDistance = 1.5f;

        [SerializeField, Min(0.2f)]
        private float maxDistance = 14f;

        [Header("Debug")]

        [SerializeField]
        private bool verboseLogging = false;

        private AudioSource audioSource;
        private AudioClip generatedClip;
        private Coroutine playbackRoutine;
        private string currentPattern = string.Empty;

        public bool IsPlaying => playbackRoutine != null;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            ConfigureAudioSource();
        }

        private void OnDisable()
        {
            StopEmitting();
        }

        private void ConfigureAudioSource()
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f; // 3D posicional
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = minDistance;
            audioSource.maxDistance = maxDistance;
            audioSource.volume = volume;
        }

        /// <summary>
        /// Empieza a reproducir el patrón indicado en bucle hasta que se
        /// llame a <see cref="StopEmitting"/>.
        /// </summary>
        public void PlayPattern(string pattern)
        {
            StopEmitting();

            currentPattern = pattern ?? string.Empty;

            if (currentPattern.Length == 0)
            {
                return;
            }

            gameObject.SetActive(true);
            playbackRoutine = StartCoroutine(PlaybackLoop());

            if (verboseLogging)
            {
                Debug.Log(
                    $"[MorseEmitter] '{name}' reproduciendo '{currentPattern}'.",
                    this);
            }
        }

        /// <summary>Detiene la reproducción y silencia el AudioSource.</summary>
        public void StopEmitting()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            if (audioSource != null)
            {
                audioSource.Stop();
            }

            currentPattern = string.Empty;
        }

        private IEnumerator PlaybackLoop()
        {
            EnsureClip();

            while (true)
            {
                for (int index = 0; index < currentPattern.Length; index++)
                {
                    char pulse = currentPattern[index];
                    float pulseDuration =
                        pulse == '-'
                            ? dotDuration * dashMultiplier
                            : dotDuration;

                    PlayPulse(pulseDuration);
                    yield return new WaitForSeconds(pulseDuration);

                    audioSource.Stop();

                    if (index < currentPattern.Length - 1)
                    {
                        yield return new WaitForSeconds(intraSymbolGap);
                    }
                }

                yield return new WaitForSeconds(repeatDelay);
            }
        }

        private void PlayPulse(float duration)
        {
            if (audioSource.clip == null)
            {
                return;
            }

            audioSource.Stop();
            audioSource.time = 0f;
            audioSource.Play();
        }

        private void EnsureClip()
        {
            if (toneClip != null)
            {
                audioSource.clip = toneClip;
                return;
            }

            if (generatedClip == null)
            {
                generatedClip = GenerateSineClip();
            }

            audioSource.clip = generatedClip;
        }

        /// <summary>
        /// Genera un clip sinusoidal de un segundo como fallback. El pulso
        /// real se recorta con la duración de reproducción de cada punto/raya.
        /// </summary>
        private AudioClip GenerateSineClip()
        {
            const int sampleRate = 44100;
            const float length = 1f;
            int sampleCount = Mathf.CeilToInt(sampleRate * length);

            float[] samples = new float[sampleCount];
            float fadeSamples = sampleRate * 0.01f; // 10 ms fade in/out

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f;

                if (i < fadeSamples)
                {
                    envelope = i / fadeSamples;
                }
                else if (i > sampleCount - fadeSamples)
                {
                    envelope = (sampleCount - i) / fadeSamples;
                }

                samples[i] =
                    Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            AudioClip clip = AudioClip.Create(
                "MorseToneFallback", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
