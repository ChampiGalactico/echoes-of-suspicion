using Mirror;
using UnityEngine;

/// <summary>
/// Vida del Corredor: recibe daño, muere, respawnea en un punto fijo con
/// invulnerabilidad temporal. Estilo It Takes Two — sin penalización dura,
/// respawnea infinitamente. Solo tiene efecto real con rol Runner.
///
/// El teletransporte se aplica tanto en el servidor como en la máquina del
/// dueño (vía TargetRpc): como el movimiento es client-authoritative
/// (NetworkPlayerMovement mueve localmente), si solo el servidor movía el
/// transform, el Update() del cliente lo sobreescribía al siguiente frame.
///
/// No sabe nada de fade/UI — expone eventos para que la presentación
/// (pantalla negra, HUD) se conecte por separado.
/// </summary>
[RequireComponent(typeof(CharacterStatsProvider))]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerHealth : NetworkBehaviour
{
    [Header("Respawn")]
    [SerializeField, Min(0f), Tooltip("Segundos de invulnerabilidad tras respawnear.")]
    private float invulnerabilityDuration = 2f;

    [Header("Fall Death")]
    [SerializeField, Tooltip("Si el Corredor cae por debajo de esta altura Y, muere.")]
    private float fallDeathYThreshold = -10f;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    [SyncVar(hook = nameof(HandleHealthSynced))]
    private float currentHealth;

    private CharacterStatsProvider statsProvider;
    private CharacterController characterController;
    private float maxHealth;
    private bool isInvulnerable;
    private float invulnerabilityEndTime;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsInvulnerable => isInvulnerable;

    /// <summary>(current, max) — se dispara en el dueño del Player al cambiar la vida.</summary>
    public event System.Action<float, float> OnHealthChanged;

    /// <summary>Se dispara en el dueño del Player al morir, antes de teletransportar al respawn.</summary>
    public event System.Action OnDied;

    /// <summary>Se dispara en el dueño del Player justo después de reaparecer.</summary>
    public event System.Action OnRespawned;

    private void Awake()
    {
        statsProvider = GetComponent<CharacterStatsProvider>();
        characterController = GetComponent<CharacterController>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        // El personaje se asigna después (EOSNetworkManager.OnServerAddPlayer),
        // así que la vida real se calcula en RecalculateFromStats(), no aquí.
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
    /// para que la vida máxima use los datos reales.
    /// </summary>
    public void RecalculateFromStats()
    {
        maxHealth = statsProvider.Character != null ? statsProvider.Character.maxHealth : 100f;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (isInvulnerable && Time.time >= invulnerabilityEndTime)
        {
            isInvulnerable = false;
        }

        if (transform.position.y < fallDeathYThreshold)
        {
            Kill();
        }
    }

    /// <summary>
    /// Aplica daño al Corredor. Ignorado si es invulnerable o si no es Runner
    /// (el Guía no tiene vida física).
    /// </summary>
    [Server]
    public void TakeDamage(float amount)
    {
        if (statsProvider.Role != PlayerRole.Runner && !EOSNetworkManager.AreProtagonistsReunited)
        {
            return;
        }

        if (isInvulnerable || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        Debug.Log($"[PlayerHealth DEBUG] TakeDamage llamado. currentHealth={currentHealth}, maxHealth={maxHealth}, isInvulnerable={isInvulnerable}, role={statsProvider.Role}");

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerHealth] {statsProvider.Character?.characterName} recibió {amount} de daño. " +
                      $"Vida: {currentHealth:F0}/{maxHealth:F0}");
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Mata al Corredor directamente, sin pasar por daño gradual.
    /// Útil para caídas fatales o zonas prohibidas del mapa (ver DeathZone).
    /// </summary>
    [Server]
    public void Kill()
    {
        if (statsProvider.Role != PlayerRole.Runner && !EOSNetworkManager.AreProtagonistsReunited)
        {
            return;
        }

        if (isInvulnerable || currentHealth <= 0f)
        {
            return;
        }

        currentHealth = 0f;
        Die();
    }

    [Server]
    private void Die()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerHealth] {statsProvider.Character?.characterName} murió. Respawneando...");
        }

        TargetNotifyDied(connectionToClient);
        Respawn();
    }

    [Server]
    private void Respawn()
    {
        var biomeSpawner = FindAnyObjectByType<BiomeSpawner>();
        Transform respawnPoint = biomeSpawner != null ? biomeSpawner.RunnerSpawnPoint : null;

        Vector3 position = respawnPoint != null ? respawnPoint.position : transform.position;
        Quaternion rotation = respawnPoint != null ? respawnPoint.rotation : transform.rotation;

        if (respawnPoint == null)
        {
            Debug.LogWarning("[PlayerHealth] No hay RunnerSpawnPoint configurado en el BiomeSpawner.");
        }

        TeleportTo(position, rotation);

        currentHealth = maxHealth;
        isInvulnerable = true;
        invulnerabilityEndTime = Time.time + invulnerabilityDuration;

        // Reinicia la Stamina también al respawnear, con los mismos stats del personaje.
        var stamina = GetComponent<PlayerStamina>();
        stamina?.RecalculateFromStats();

        TargetNotifyRespawned(connectionToClient, position, rotation);
    }

    private void TeleportTo(Vector3 position, Quaternion rotation)
    {
        characterController.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        characterController.enabled = true;
    }

    [TargetRpc]
    private void TargetNotifyDied(NetworkConnectionToClient target)
    {
        OnDied?.Invoke();
    }

    [TargetRpc]
    private void TargetNotifyRespawned(NetworkConnectionToClient target, Vector3 position, Quaternion rotation)
    {
        // El dueño mueve también su copia LOCAL — si no, su propio Update()
        // de movimiento sobreescribe el teleport del servidor al siguiente frame.
        TeleportTo(position, rotation);
        OnRespawned?.Invoke();
    }

    private void HandleHealthSynced(float oldValue, float newValue)
    {
        OnHealthChanged?.Invoke(newValue, maxHealth);
    }
}