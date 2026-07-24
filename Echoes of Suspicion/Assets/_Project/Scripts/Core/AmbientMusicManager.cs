using System.Collections;
using UnityEngine;

/// <summary>
/// Música de ambientación continua: arranca en el menú principal y suena
/// durante toda la partida, sin cortarse entre escenas. No es un objeto de
/// red — cada cliente tiene el suyo local, sonando su propia ambientación.
///
/// Se pausa (con fade out) cuando el Corredor local percibe amenaza de la
/// criatura (Alert/Search/Chase), y se reanuda (con fade in) cuando la
/// criatura vuelve a Patrol. Esto SOLO ocurre en la pantalla del Corredor —
/// el Guía nunca la pausa, sigue sonando siempre para él.
/// </summary>
public sealed class AmbientMusicManager : MonoBehaviour
{
    public static AmbientMusicManager Instance { get; private set; }

    [Header("Ambient Track")]
    [SerializeField]
    private AudioClip ambientTrack;

    [SerializeField, Range(0f, 1f)]
    private float ambientVolume = 0.5f;

    [Header("Fade Timings")]
    [SerializeField, Min(0f)]
    private float duckFadeDuration = 1.5f;

    [SerializeField, Min(0f)]
    private float resumeFadeDuration = 1.5f;

    [SerializeField, Tooltip("Arrastra aquí el AudioSource que va a reproducir la ambientación.")]
    private AudioSource audioSource;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
        {
            Debug.LogError("[AmbientMusicManager] No hay AudioSource asignado en el Inspector.");
            return;
        }

        audioSource.clip = ambientTrack;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        audioSource.volume = ambientVolume;
        audioSource.Play();
    }

    /// <summary>
    /// Pausa la ambientación con fade out. Llamar cuando el Corredor local
    /// empieza a percibir amenaza (Alert/Search/Chase).
    /// </summary>
    public void Duck()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeTo(0f, duckFadeDuration));
    }

    /// <summary>
    /// Reanuda la ambientación con fade in. Llamar cuando la criatura vuelve
    /// a Patrol (ya no hay amenaza sobre el Corredor local).
    /// </summary>
    public void Resume()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeTo(ambientVolume, resumeFadeDuration));
    }

    private IEnumerator FadeTo(float targetVolume, float duration)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
            yield return null;
        }

        audioSource.volume = targetVolume;
        fadeCoroutine = null;
    }
}