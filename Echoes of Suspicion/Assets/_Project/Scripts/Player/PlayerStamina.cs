using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Stamina del jugador: se gasta mientras corre y se regenera
/// mientras camina o permanece quieto.
///
/// Al agotarse completamente entra en estado "exhausted".
/// No puede volver a correr apenas recupere un poco de stamina,
/// sino cuando alcance el porcentaje indicado por
/// recoveryThresholdFraction.
///
/// El máximo y la regeneración escalan con el StaminaMultiplier
/// proporcionado por CharacterStatsProvider.
/// </summary>
[RequireComponent(typeof(CharacterStatsProvider))]
public sealed class PlayerStamina : NetworkBehaviour
{
    [Header("Base (antes de multiplicador)")]

    [SerializeField, Min(0f)]
    private float baseMaxStamina = 6f;

    [SerializeField, Min(0f)]
    [Tooltip(
        "Cantidad de stamina gastada por segundo mientras corre."
    )]
    private float drainPerSecond = 1f;

    [SerializeField, Min(0f)]
    [Tooltip(
        "Cantidad base de stamina regenerada por segundo."
    )]
    private float baseRegenPerSecond = 0.5f;

    [Header("Exhaustion")]

    [SerializeField, Range(0f, 1f)]
    [Tooltip(
        "Fracción del máximo que debe recuperar antes de poder " +
        "volver a correr después de agotarse."
    )]
    private float recoveryThresholdFraction = 0.5f;

    [Header("Audio")]

    [SerializeField]
    [Tooltip("Jadeo pesado al quedarse sin aire.")]
    private AudioClip exhaustedGaspClip;

    [SerializeField, Range(0f, 1f)]
    private float gaspVolume = 0.7f;

    [SerializeField]
    [Tooltip(
        "AudioSource dedicado, separado del AudioSource de pasos."
    )]
    private AudioSource audioSource;

    /*
     * Ambos valores deben sincronizarse.
     *
     * Sin maxStamina sincronizada, el cliente podría conocer la
     * stamina actual pero no tendría el máximo correcto para calcular
     * el porcentaje de la barra del HUD.
     */
    [SyncVar(hook = nameof(HandleMaxStaminaSynced))]
    private float maxStamina;

    [SyncVar(hook = nameof(HandleStaminaSynced))]
    private float currentStamina;

    private CharacterStatsProvider statsProvider;
    private PlayerSprintController sprintController;

    private float regenPerSecond;
    private bool isExhausted;

    /// <summary>
    /// Controlado por PlayerSprintController.
    /// True mientras el servidor considera que el jugador está corriendo.
    /// </summary>
    public bool IsSprinting
    {
        get;
        set;
    }

    public float CurrentStamina => currentStamina;

    public float MaxStamina => maxStamina;

    public bool HasStamina => currentStamina > 0f;

    /// <summary>
    /// Indica si el jugador puede comenzar o continuar corriendo.
    ///
    /// Si agotó completamente su stamina, permanece bloqueado hasta
    /// alcanzar el umbral de recuperación.
    /// </summary>
    public bool CanSprint =>
        currentStamina > 0f &&
        !isExhausted;

    /// <summary>
    /// Notifica al HUD cuando cambia la stamina actual o máxima.
    ///
    /// Primer parámetro: stamina actual.
    /// Segundo parámetro: stamina máxima.
    /// </summary>
    public event Action<float, float> OnStaminaChanged;

    private void Awake()
    {
        statsProvider =
            GetComponent<CharacterStatsProvider>();

        sprintController =
            GetComponent<PlayerSprintController>();

        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        if (isServer)
        {
            RecalculateFromStats();
        }
    }

    /// <summary>
    /// Llamado por EOSNetworkManager después de asignar el personaje,
    /// para calcular la stamina usando el multiplicador correspondiente.
    /// </summary>
    [Server]
    public void RecalculateFromStats()
    {
        if (statsProvider == null)
        {
            Debug.LogError(
                "PlayerStamina: no se encontró CharacterStatsProvider.",
                this
            );

            return;
        }

        float staminaMultiplier =
            statsProvider.StaminaMultiplier;

        maxStamina =
            baseMaxStamina *
            staminaMultiplier;

        regenPerSecond =
            baseRegenPerSecond *
            staminaMultiplier;

        currentStamina = maxStamina;
        isExhausted = false;

        NotifyStaminaChanged();
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        /*
         * Se comprueba cada frame para detener el sprint
         * inmediatamente cuando la stamina se agota.
         */
        if (IsSprinting && !CanSprint)
        {
            sprintController?.ForceStopSprinting();
        }

        if (IsSprinting && currentStamina > 0f)
        {
            DrainStamina();
        }
        else if (
            !IsSprinting &&
            currentStamina < maxStamina
        )
        {
            RegenerateStamina();
        }

        UpdateExhaustionState();
    }

    [Server]
    private void DrainStamina()
    {
        currentStamina = Mathf.Max(
            0f,
            currentStamina -
            drainPerSecond *
            Time.deltaTime
        );

        if (currentStamina > 0f)
        {
            return;
        }

        if (!isExhausted)
        {
            TargetPlayGaspSound(connectionToClient);
        }

        isExhausted = true;
    }

    [Server]
    private void RegenerateStamina()
    {
        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina +
            regenPerSecond *
            Time.deltaTime
        );
    }

    [Server]
    private void UpdateExhaustionState()
    {
        if (!isExhausted)
        {
            return;
        }

        float recoveryThreshold =
            maxStamina *
            recoveryThresholdFraction;

        if (currentStamina >= recoveryThreshold)
        {
            isExhausted = false;
        }
    }

    /// <summary>
    /// Hook ejecutado cuando el cliente recibe un nuevo máximo.
    /// </summary>
    private void HandleMaxStaminaSynced(
        float oldValue,
        float newValue
    )
    {
        NotifyStaminaChanged();
    }

    /// <summary>
    /// Hook ejecutado cuando el cliente recibe una nueva stamina actual.
    /// </summary>
    private void HandleStaminaSynced(
        float oldValue,
        float newValue
    )
    {
        NotifyStaminaChanged();
    }

    private void NotifyStaminaChanged()
    {
        OnStaminaChanged?.Invoke(
            currentStamina,
            maxStamina
        );
    }

    [TargetRpc]
    private void TargetPlayGaspSound(
        NetworkConnectionToClient target
    )
    {
        if (
            exhaustedGaspClip == null ||
            audioSource == null
        )
        {
            return;
        }

        audioSource.PlayOneShot(
            exhaustedGaspClip,
            gaspVolume
        );
    }
}