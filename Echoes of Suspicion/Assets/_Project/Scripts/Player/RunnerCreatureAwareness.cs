using Mirror;
using UnityEngine;

public enum RunnerThreatLevel { None, Alert, Search, Chase }

/// <summary>
/// Detecta criaturas cercanas al Corredor combinando la Perspicacia de
/// ambos jugadores (Corredor + Guía). Solo tiene efecto real cuando este
/// Player tiene rol Runner.
///
/// El radio compartido (lo que vería el Guía en su mapa) y el latido usan
/// polling normal (CheckNearbyCreatures cada checkInterval) — no necesitan
/// precisión de frame.
///
/// La música de amenaza (Alert/Search/Chase) usa EVENTOS en vez de polling:
/// se suscribe a CreatureController.OnAnyCreatureStateChanged, que se
/// dispara exactamente cuando una criatura cambia de estado. Esto evita
/// perderse transiciones rápidas (ej. Alert → Chase → Attack en menos de
/// un checkInterval), que el polling anterior se saltaba.
///
/// El "latido" es una habilidad exclusiva de Perspicacia por encima de 1:
/// no es un radio que se achica, es una habilidad que directamente no existe
/// con Perspicacia baja o neutra. Cuando existe, dispara un pulso puntual
/// cada cierto intervalo mientras haya una criatura dentro del radio — con
/// Perspicacia baja el intervalo es largo (pulsos espaciados), con Perspicacia
/// alta es corto (casi constante, se siente como racha). Es determinístico,
/// no depende de probabilidad.
///
/// Vive en el servidor. Los eventos se replican solo al dueño del Player
/// mediante TargetRpc — son sensaciones internas suyas, no algo que otros
/// jugadores deban percibir.
/// </summary>
[RequireComponent(typeof(CharacterStatsProvider))]
[RequireComponent(typeof(AudioSource))]
public sealed class RunnerCreatureAwareness : NetworkBehaviour
{
    [Header("Shared Radius (mapa del Guía)")]
    [SerializeField, Tooltip("Radio base antes de multiplicadores.")]
    private float baseSharedRadius = 10f;

    [SerializeField, Tooltip("Tope máximo del radio compartido, sin importar qué tan altos sean los multiplicadores combinados.")]
    private float maxSharedRadio = 18f;

    [Header("Heartbeat Pulse Interval")]
    [SerializeField, Tooltip("Tiempo entre latidos con la Perspicacia más baja considerada 'con habilidad' (justo encima de 1).")]
    private float slowestHeartbeatInterval = 3f;

    [SerializeField, Tooltip("Tiempo entre latidos con la Perspicacia máxima esperada (ver maxExpectedPerception).")]
    private float fastestHeartbeatInterval = 0.5f;

    [SerializeField, Tooltip("Multiplicador de Perspicacia considerado el máximo posible, para escalar el intervalo.")]
    private float maxExpectedPerception = 2f;

    [Header("Check Settings")]
    [SerializeField, Tooltip("Cada cuántos segundos se revisa el radio compartido y el latido (optimización).")]
    private float checkInterval = 0.3f;

    [Header("Threat Audio (tensión)")]
    [SerializeField, Tooltip("Disparo puntual (una vez) cuando una criatura te detecta y entra en Alert.")]
    private AudioClip alertDetectSomething;

    [SerializeField, Tooltip("Loop de tensión mientras una criatura te busca (Search) tras perderte.")]
    private AudioClip searchTensionLoop;

    [SerializeField, Tooltip("Loop de tensión mientras una criatura te está persiguiendo activamente (Chase).")]
    private AudioClip chaseTensionLoop;

    [SerializeField, Range(0f, 1f)]
    private float tensionVolume = 0.5f;

    [SerializeField, Min(0f), Tooltip("Duración del fade out al dejar de sonar el loop de Search.")]
    private float searchFadeOutDuration = 1.5f;

    [Header("Audio")]
    [SerializeField, Tooltip("AudioSource dedicado para la música de amenaza. Debe ser distinto del que usan los pasos, o Stop() de uno corta al otro.")]
    private AudioSource audioSource;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    /// <summary>Se dispara SOLO en la máquina del dueño del Player, cada vez que "siente" un pulso de latido.</summary>
    public event System.Action OnHeartbeatPulse;

    private CharacterStatsProvider statsProvider;
    private float lastCheckTime;
    private float lastHeartbeatTime;
    private RunnerThreatLevel currentThreatLevel = RunnerThreatLevel.None;
    private Coroutine fadeOutCoroutine;

    private void Awake()
    {
        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        statsProvider = GetComponent<CharacterStatsProvider>();
        CreatureController.OnAnyCreatureStateChanged += HandleCreatureStateChanged;
    }

    public override void OnStopServer()
    {
        CreatureController.OnAnyCreatureStateChanged -= HandleCreatureStateChanged;
        base.OnStopServer();
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (statsProvider.Role != PlayerRole.Runner)
        {
            return;
        }

        if (Time.time - lastCheckTime < checkInterval)
        {
            return;
        }

        lastCheckTime = Time.time;
        CheckNearbyCreatures();
    }

    /// <summary>
    /// Reacciona a cualquier cambio de estado de cualquier criatura del bioma.
    /// Filtra por si el target es este jugador, y traduce el estado de la
    /// criatura al nivel de amenaza correspondiente.
    /// </summary>
    private void HandleCreatureStateChanged(CreatureController creature, CreatureStateType stateType, uint? targetNetId)
    {
        Debug.Log($"[RunnerCreatureAwareness] Evento recibido: stateType={stateType}, targetNetId={targetNetId}, mi netId={netId}, coincide={targetNetId == netId}");
        if (targetNetId != netId)
        {
            return;
        }

        RunnerThreatLevel newLevel = stateType switch
        {
            CreatureStateType.Alert => RunnerThreatLevel.Alert,
            CreatureStateType.Search => RunnerThreatLevel.Search,
            CreatureStateType.Chase => RunnerThreatLevel.Chase,
            CreatureStateType.Attacking => RunnerThreatLevel.Chase, // seguimos en tensión alta durante el ataque
            _ => RunnerThreatLevel.None
        };

        if (newLevel == currentThreatLevel)
        {
            return;
        }

        RunnerThreatLevel previousLevel = currentThreatLevel;
        currentThreatLevel = newLevel;

        if (showDebugLogs)
        {
            Debug.Log($"[RunnerCreatureAwareness] Threat level: {previousLevel} → {newLevel}");
        }

        Debug.Log($"[RunnerCreatureAwareness] A punto de enviar TargetRpc. connectionToClient null? {connectionToClient == null}");

        TargetUpdateThreatAudio(connectionToClient, newLevel, previousLevel);

        bool becameThreatened = previousLevel == RunnerThreatLevel.None && newLevel != RunnerThreatLevel.None;
        bool becameSafe = newLevel == RunnerThreatLevel.None;

        if (EOSNetworkManager.AreProtagonistsReunited)
        {
            if (becameThreatened)
            {
                RpcDuckAmbientForAll();
            }
            if (becameSafe)
            {
                RpcResumeAmbientForAll();
            }
        }
        else
        {
            if (becameThreatened)
            {
                TargetDuckAmbient(connectionToClient);
            }
            if (becameSafe)
            {
                TargetResumeAmbient(connectionToClient);
            }
        }
    }

    /// <summary>
    /// Revisa el radio compartido (para el futuro mapa del Guía) y el latido.
    /// La detección de Alert/Search/Chase YA NO vive aquí — se maneja por
    /// eventos en HandleCreatureStateChanged.
    /// </summary>
    private void CheckNearbyCreatures()
    {
        float myPerception = statsProvider.PerceptionMultiplier;

        var guideProvider = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);
        float guidePerception = guideProvider != null ? guideProvider.PerceptionMultiplier : 1f;

        float sharedRadius = Mathf.Min(baseSharedRadius * myPerception * guidePerception, maxSharedRadio);

        bool hasHeartbeatAbility = myPerception > 1f;

        var creatures = FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
        bool creatureNearby = false;

        foreach (var creature in creatures)
        {
            float distance = Vector3.Distance(transform.position, creature.transform.position);

            if (distance <= sharedRadius)
            {
                creatureNearby = true;
            }

            // TODO: cuando exista el mapa del Guía, publicar aquí qué criaturas
            // caen dentro de sharedRadius para que ese sistema las muestre.
        }

        if (!hasHeartbeatAbility || !creatureNearby)
        {
            return;
        }

        float t = Mathf.InverseLerp(1f, maxExpectedPerception, myPerception);
        float pulseInterval = Mathf.Lerp(slowestHeartbeatInterval, fastestHeartbeatInterval, t);

        if (Time.time - lastHeartbeatTime < pulseInterval)
        {
            return;
        }

        lastHeartbeatTime = Time.time;

        if (showDebugLogs)
        {
            Debug.Log($"[RunnerCreatureAwareness] 💓 Pulso de latido (intervalo {pulseInterval:F1}s)");
        }

        TargetHeartbeatPulse(connectionToClient);
    }

    [TargetRpc]
    private void TargetHeartbeatPulse(NetworkConnectionToClient target)
    {
        OnHeartbeatPulse?.Invoke();
    }

    [TargetRpc]
    private void TargetUpdateThreatAudio(NetworkConnectionToClient target, RunnerThreatLevel level, RunnerThreatLevel previousLevel)
    {
        if (fadeOutCoroutine != null)
        {
            StopCoroutine(fadeOutCoroutine);
            fadeOutCoroutine = null;
        }


        if (level == RunnerThreatLevel.Alert)
        {
            // El ping solo suena la primera vez que ENTRA a Alert, no cada frame que se mantiene.
            if (previousLevel != RunnerThreatLevel.Alert && alertDetectSomething != null)
            {
                audioSource.PlayOneShot(alertDetectSomething, tensionVolume);
            }

            // Después del ping, Alert usa el mismo loop de tensión que Search.
            if (audioSource.clip != searchTensionLoop || !audioSource.isPlaying)
            {
                audioSource.Stop();
                audioSource.volume = tensionVolume;
                audioSource.clip = searchTensionLoop;
                audioSource.loop = true;
                audioSource.Play();
            }
            return;
        }

        AudioClip targetClip = level switch
        {
            RunnerThreatLevel.Chase => chaseTensionLoop,
            RunnerThreatLevel.Search => searchTensionLoop,
            _ => null
        };

        if (audioSource.clip == targetClip && audioSource.isPlaying)
        {
            return;
        }

        // Volver a la calma (None): SIEMPRE con fade out, sin importar de qué
        // estado veníamos (Alert, Search o Chase comparten el mismo loop).
        if (targetClip == null)
        {
            if (audioSource.isPlaying)
            {
                fadeOutCoroutine = StartCoroutine(FadeOutAndStop(searchFadeOutDuration));
            }
            return;
        }

        // Cambiar hacia Chase o Search desde algo distinto: corte directo,
        // salvo que vengamos de Search/Alert hacia algo que no sea Chase
        // (ya cubierto arriba, level nunca es null aquí).
        audioSource.Stop();
        audioSource.volume = tensionVolume;
        audioSource.clip = targetClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    private System.Collections.IEnumerator FadeOutAndStop(float duration)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        audioSource.Stop();
        audioSource.volume = tensionVolume;
        fadeOutCoroutine = null;
    }

    private System.Collections.IEnumerator PlayAfterFade(AudioClip clip, float delay)
    {
        yield return new WaitForSeconds(delay);

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.volume = tensionVolume;
        audioSource.Play();
    }

    [TargetRpc]
    private void TargetDuckAmbient(NetworkConnectionToClient target)
    {
        if (AmbientMusicManager.Instance != null)
        {
            AmbientMusicManager.Instance.Duck();
        }
    }

    [TargetRpc]
    private void TargetResumeAmbient(NetworkConnectionToClient target)
    {
        if (AmbientMusicManager.Instance != null)
        {
            AmbientMusicManager.Instance.Resume();
        }
    }

    [ClientRpc]
    private void RpcDuckAmbientForAll()
    {
        if (AmbientMusicManager.Instance != null)
        {
            AmbientMusicManager.Instance.Duck();
        }
    }

    [ClientRpc]
    private void RpcResumeAmbientForAll()
    {
        if (AmbientMusicManager.Instance != null)
        {
            AmbientMusicManager.Instance.Resume();
        }
    }
}