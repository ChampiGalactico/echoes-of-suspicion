using Mirror;
using UnityEngine;

/// <summary>
/// Genera ruido de pasos según el estado de movimiento del jugador local,
/// y reproduce el audio correspondiente (pasos o respiración) en loop
/// continuo por estado: el clip suena mientras dure el estado, se repite
/// solo si es más corto que el tiempo que llevas en él, y se corta de
/// inmediato al cambiar de estado (no espera a que termine).
///
/// No modifica el sistema de percepción de las criaturas (CreatureNoisePerception
/// sigue igual). En cambio, se autofiltra por proximidad ANTES de publicar:
/// - Agachado: nunca publica NoiseEvent (silencio para las criaturas).
/// - Caminando: solo publica si hay una criatura dentro de un radio chico propio
///   de los pasos (detección solo de cerca).
/// - Corriendo: publica siempre, sin filtro extra — el hearingRadius normal de
///   cada criatura ya cubre "se oye en todo su rango auditivo".
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(PlayerCrouchController))]
[RequireComponent(typeof(PlayerSprintController))]
[RequireComponent(typeof(AudioSource))]
public sealed class PlayerFootstepNoiseSource : NetworkBehaviour
{
    [Header("Intervalo entre pasos (NoiseEvent)")]
    [SerializeField, Min(0.05f)]
    private float walkStepInterval = 0.5f;
    [SerializeField, Min(0.05f)]
    private float sprintStepInterval = 0.3f;

    [Header("Intensidad emitida (pasa el umbral normal de percepción)")]
    [SerializeField, Range(0f, 1f)]
    private float walkIntensity = 0.35f;
    [SerializeField, Range(0f, 1f)]
    private float sprintIntensity = 0.8f;

    [Header("Filtro de proximidad SOLO para caminar")]
    [SerializeField, Tooltip("Radio propio de los pasos al caminar. Si ninguna criatura está así de cerca, ni se publica el evento.")]
    private float walkOnlyDetectionRadius = 4f;

    [Header("Audio - Footsteps")]
    [SerializeField, Tooltip("Clip con ambos pasos (izquierda + derecha) al caminar. Se reproduce en loop mientras camina.")]
    private AudioClip walkStepCycle;
    [SerializeField, Tooltip("Clip con ambos pasos (izquierda + derecha) al correr. Se reproduce en loop mientras corre.")]
    private AudioClip sprintStepCycle;

    [Header("Audio - Crouch (respiración en vez de pasos)")]
    [SerializeField]
    private AudioClip crouchBreathingLoop;

    [Header("Audio Source")]
    [SerializeField, Range(0f, 1f)] private float stepVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float breathingVolume = 0.2f;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = false;
    

    private AudioSource audioSource;
    private PlayerCrouchController crouchController;
    private PlayerSprintController sprintController;
    private CharacterController characterController;
    private float lastStepTime;

    private void Awake()
    {
        crouchController = GetComponent<PlayerCrouchController>();
        sprintController = GetComponent<PlayerSprintController>();
        characterController = GetComponent<CharacterController>();
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        bool isCrouching = crouchController.IsCrouching;
        bool isMoving = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).sqrMagnitude > 0.01f;
        bool isSprinting = isMoving && sprintController.IsSprinting;
        bool isWalking = isMoving && !isSprinting && !isCrouching;

        HandleMovementAudio(isCrouching, isWalking, isSprinting);

        // El NoiseEvent (para las criaturas) sigue funcionando por intervalo,
        // independiente del audio.
        if (!isMoving || isCrouching)
        {
            return;
        }

        float interval = isSprinting ? sprintStepInterval : walkStepInterval;
        if (Time.time - lastStepTime < interval)
        {
            return;
        }

        lastStepTime = Time.time;

        float intensity = isSprinting ? sprintIntensity : walkIntensity;
        CmdReportFootstep(transform.position, intensity, isSprinting);
    }

    /// <summary>
    /// Reproduce el clip correspondiente al estado actual en loop continuo.
    /// Solo reinicia el audio cuando CAMBIA de estado — mientras se mantiene
    /// en el mismo estado, el clip sigue sonando (y hace loop solo si es más
    /// corto que el tiempo que llevas en ese estado). Al cambiar de estado,
    /// se corta de inmediato sin esperar a que termine.
    /// </summary>
    private void HandleMovementAudio(bool isCrouching, bool isWalking, bool isSprinting)
    {
        AudioClip targetClip =
            isCrouching ? crouchBreathingLoop :
            isSprinting ? sprintStepCycle :
            isWalking ? walkStepCycle :
            null; // quieto: silencio

        float targetVolume = isCrouching ? breathingVolume : stepVolume;


        // Ya está sonando el clip correcto para este estado: dejarlo seguir.
        if (audioSource.clip == targetClip && audioSource.isPlaying)
        {
            return;
        }
        // El estado cambió (o no había nada sonando): cortar y arrancar el nuevo.
        audioSource.Stop();

        if (targetClip != null)
        {
            audioSource.clip = targetClip;
            audioSource.loop = true;
            audioSource.volume = targetVolume;
            audioSource.Play();
        }
    }

    [Command]
    private void CmdReportFootstep(Vector3 position, float intensity, bool isSprinting)
    {
        // Caminando: solo se publica si hay una criatura MUY cerca.
        // Corriendo: siempre se publica, el hearingRadius normal de la
        // criatura decide si lo escucha (sistema sin cambios).
        if (!isSprinting && !HasCreatureWithinRadius(position, walkOnlyDetectionRadius))
        {
            return;
        }

        NoiseEvent noiseEvent = new NoiseEvent(position, intensity, NoiseSource.Footsteps, netId);
        NoiseEventBus.Publish(noiseEvent);

        RpcNotifyFootstepNoise(position, intensity);

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerFootstepNoiseSource] Paso publicado ({(isSprinting ? "sprint" : "walk")}), " +
                      $"intensidad {intensity:F2} en {position}");
        }
    }

    [ClientRpc(includeOwner = false)]
    private void RpcNotifyFootstepNoise(Vector3 position, float intensity)
    {
        if (isServer)
        {
            return;
        }

        NoiseEvent noiseEvent = new NoiseEvent(position, intensity, NoiseSource.Footsteps, netId);
        NoiseEventBus.Publish(noiseEvent);
    }

    private static bool HasCreatureWithinRadius(Vector3 position, float radius)
    {
        var creatures = FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
        foreach (var creature in creatures)
        {
            if (Vector3.Distance(position, creature.transform.position) <= radius)
            {
                return true;
            }
        }
        return false;
    }
}