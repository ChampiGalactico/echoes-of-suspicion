using Mirror;
using UnityEngine;

/// <summary>
/// Stamina del jugador: se gasta mientras corre, se regenera caminando o quieto.
///
/// Al agotarse del todo entra en estado "exhausted": no puede volver a correr
/// apenas tenga stamina > 0 (eso oscilaría encendido/apagado en el límite),
/// sino que debe recuperar hasta un umbral (recoveryThresholdFraction) antes
/// de poder volver a activarse.
///
/// Revisa CanSprint en cada Update (no solo cuando el cliente manda un nuevo
/// Command) para forzar el corte de sprint apenas se agota, sin depender de
/// que el cliente vuelva a mandar su intención.
///
/// El máximo y la velocidad de regeneración escalan con StaminaMultiplier
/// del personaje — todos parten de una base común, más grande y más rápida
/// para quien tenga mejor stamina.
/// </summary>
[RequireComponent(typeof(CharacterStatsProvider))]
public sealed class PlayerStamina : NetworkBehaviour
{
    [Header("Base (antes de multiplicador)")]
    [SerializeField, Min(0f)]
    private float baseMaxStamina = 6f;

    [SerializeField, Min(0f), Tooltip("Segundos de stamina que se gastan por segundo mientras corre.")]
    private float drainPerSecond = 1f;

    [SerializeField, Min(0f), Tooltip("Segundos de stamina que se regeneran por segundo caminando o quieto.")]
    private float baseRegenPerSecond = 0.5f;

    [Header("Exhaustion")]
    [SerializeField, Range(0f, 1f), Tooltip("Fracción del máximo que debe recuperar antes de poder volver a correr, una vez se agota del todo.")]
    private float recoveryThresholdFraction = 0.5f;


    [SyncVar]
    private float currentStamina;

    private CharacterStatsProvider statsProvider;
    private PlayerSprintController sprintController;
    private float maxStamina;
    private float regenPerSecond;
    private bool isExhausted;

    /// <summary>Controlado por PlayerSprintController: true mientras el jugador está corriendo activamente.</summary>
    public bool IsSprinting { get; set; }

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public bool HasStamina => currentStamina > 0f;

    /// <summary>
    /// True si puede correr ahora mismo. Distinto de HasStamina: si se agotó del
    /// todo, se queda bloqueado hasta recuperar el umbral de recuperación, no
    /// apenas tenga stamina > 0 (evita oscilar encendido/apagado en el límite).
    /// </summary>
    public bool CanSprint => currentStamina > 0f && !isExhausted;

    [Header("Audio")]
    [SerializeField, Tooltip("Jadeo pesado al quedarse sin aire.")]
    private AudioClip exhaustedGaspClip;

    [SerializeField, Range(0f, 1f)]
    private float gaspVolume = 0.7f;

    [SerializeField, Tooltip("AudioSource dedicado, distinto del de pasos, para que no se corten entre sí.")]
    private AudioSource audioSource;

    private void Awake()
    {
        statsProvider = GetComponent<CharacterStatsProvider>();
        sprintController = GetComponent<PlayerSprintController>();

        if (audioSource != null)
        {
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
    }

    private void Start()
    {
        if (isServer)
        {
            RecalculateFromStats();
        }
    }

    /// <summary>
    /// Llamado por EOSNetworkManager justo después de asignar el personaje,
    /// para que la Stamina use el multiplicador real.
    /// </summary>
    public void RecalculateFromStats()
    {
        float staminaMultiplier = statsProvider.StaminaMultiplier;
        maxStamina = baseMaxStamina * staminaMultiplier;
        regenPerSecond = baseRegenPerSecond * staminaMultiplier;
        currentStamina = maxStamina;
        isExhausted = false;
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        // Revisa CADA FRAME si sigue teniendo permiso para correr, sin
        // esperar a que el cliente mande un nuevo Command. Esto es lo que
        // faltaba: antes solo se evaluaba CanSprint una vez, al momento de
        // recibir la intención, no mientras seguía corriendo.
        if (IsSprinting && !CanSprint)
        {
            sprintController?.ForceStopSprinting();
        }

        if (IsSprinting && currentStamina > 0f)
        {
            currentStamina = Mathf.Max(0f, currentStamina - drainPerSecond * Time.deltaTime);

            if (currentStamina <= 0f)
            {
                if (!isExhausted)
                {
                    TargetPlayGaspSound(connectionToClient);
                }
                isExhausted = true;
            }
        }
        else if (!IsSprinting && currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + regenPerSecond * Time.deltaTime);
        }

        if (isExhausted && currentStamina >= maxStamina * recoveryThresholdFraction)
        {
            isExhausted = false;
        }
    }

    [TargetRpc]
    private void TargetPlayGaspSound(NetworkConnectionToClient target)
    {
        if (exhaustedGaspClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(exhaustedGaspClip, gaspVolume);
        }
    }
}