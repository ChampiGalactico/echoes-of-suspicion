using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Vida física del Corredor.
///
/// Recibe daño, muere y reaparece en el RunnerSpawnPoint con
/// invulnerabilidad temporal. La presentación visual se maneja
/// mediante eventos para mantener este componente separado del HUD.
/// </summary>
[RequireComponent(typeof(CharacterStatsProvider))]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerHealth : NetworkBehaviour
{
    [Header("Respawn")]

    [SerializeField, Min(0f)]
    [Tooltip("Segundos de invulnerabilidad después de reaparecer.")]
    private float invulnerabilityDuration = 2f;

    [Header("Fall Death")]

    [SerializeField]
    [Tooltip("Si el Corredor cae por debajo de esta altura Y, muere.")]
    private float fallDeathYThreshold = -10f;

    [Header("Debug")]

    [SerializeField]
    private bool showDebugLogs = true;

    /*
     * Sincronizamos tanto el máximo como la vida actual.
     * El HUD necesita ambos valores para calcular correctamente
     * el porcentaje de llenado.
     */
    [SyncVar(hook = nameof(HandleMaxHealthSynced))]
    private float maxHealth;

    [SyncVar(hook = nameof(HandleHealthSynced))]
    private float currentHealth;

    private CharacterStatsProvider statsProvider;
    private CharacterController characterController;

    private bool isInvulnerable;
    private float invulnerabilityEndTime;

    public float CurrentHealth => currentHealth;

    public float MaxHealth => maxHealth;

    public bool IsInvulnerable => isInvulnerable;

    /// <summary>
    /// Parámetros: vida actual y vida máxima.
    /// </summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>
    /// Se dispara en el jugador propietario antes de reaparecer.
    /// </summary>
    public event Action OnDied;

    /// <summary>
    /// Se dispara en el jugador propietario después de reaparecer.
    /// </summary>
    public event Action OnRespawned;

    private void Awake()
    {
        statsProvider =
            GetComponent<CharacterStatsProvider>();

        characterController =
            GetComponent<CharacterController>();
    }

    private void Start()
    {
        if (isServer)
        {
            RecalculateFromStats();
        }
    }

    /// <summary>
    /// Llamado por EOSNetworkManager después de asignar el personaje.
    /// </summary>
    [Server]
    public void RecalculateFromStats()
    {
        if (statsProvider == null)
        {
            Debug.LogError(
                "PlayerHealth: no se encontró CharacterStatsProvider.",
                this
            );

            return;
        }

        maxHealth =
            statsProvider.Character != null
                ? statsProvider.Character.maxHealth
                : 100f;

        currentHealth = maxHealth;

        NotifyHealthChanged();
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (
            isInvulnerable &&
            Time.time >= invulnerabilityEndTime
        )
        {
            isInvulnerable = false;
        }

        if (transform.position.y < fallDeathYThreshold)
        {
            Kill();
        }
    }

    /// <summary>
    /// Aplica daño al Corredor.
    /// El Guía normalmente no tiene vida física.
    /// </summary>
    [Server]
    public void TakeDamage(float amount)
    {
        if (
            statsProvider.Role != PlayerRole.Runner &&
            !EOSNetworkManager.AreProtagonistsReunited
        )
        {
            return;
        }

        if (
            isInvulnerable ||
            currentHealth <= 0f ||
            amount <= 0f
        )
        {
            return;
        }

        currentHealth = Mathf.Max(
            0f,
            currentHealth - amount
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"[PlayerHealth] " +
                $"{statsProvider.Character?.characterName} " +
                $"recibió {amount} de daño. " +
                $"Vida: {currentHealth:F0}/{maxHealth:F0}",
                this
            );
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Mata al Corredor directamente.
    /// </summary>
    [Server]
    public void Kill()
    {
        if (
            statsProvider.Role != PlayerRole.Runner &&
            !EOSNetworkManager.AreProtagonistsReunited
        )
        {
            return;
        }

        if (
            isInvulnerable ||
            currentHealth <= 0f
        )
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
            Debug.Log(
                $"[PlayerHealth] " +
                $"{statsProvider.Character?.characterName} murió. " +
                "Respawneando...",
                this
            );
        }

        TargetNotifyDied(connectionToClient);
        Respawn();
    }

    [Server]
    private void Respawn()
    {
        BiomeSpawner biomeSpawner =
            FindAnyObjectByType<BiomeSpawner>();

        Transform respawnPoint =
            biomeSpawner != null
                ? biomeSpawner.RunnerSpawnPoint
                : null;

        Vector3 position =
            respawnPoint != null
                ? respawnPoint.position
                : transform.position;

        Quaternion rotation =
            respawnPoint != null
                ? respawnPoint.rotation
                : transform.rotation;

        if (respawnPoint == null)
        {
            Debug.LogWarning(
                "PlayerHealth: no hay RunnerSpawnPoint " +
                "configurado en el BiomeSpawner.",
                this
            );
        }

        TeleportTo(position, rotation);

        currentHealth = maxHealth;

        isInvulnerable = true;

        invulnerabilityEndTime =
            Time.time + invulnerabilityDuration;

        PlayerStamina stamina =
            GetComponent<PlayerStamina>();

        stamina?.RecalculateFromStats();

        TargetNotifyRespawned(
            connectionToClient,
            position,
            rotation
        );
    }

    private void TeleportTo(
        Vector3 position,
        Quaternion rotation
    )
    {
        characterController.enabled = false;

        transform.SetPositionAndRotation(
            position,
            rotation
        );

        characterController.enabled = true;
    }

    private void HandleMaxHealthSynced(
        float oldValue,
        float newValue
    )
    {
        NotifyHealthChanged();
    }

    private void HandleHealthSynced(
        float oldValue,
        float newValue
    )
    {
        NotifyHealthChanged();
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(
            currentHealth,
            maxHealth
        );
    }

    [TargetRpc]
    private void TargetNotifyDied(
        NetworkConnectionToClient target
    )
    {
        OnDied?.Invoke();
    }

    [TargetRpc]
    private void TargetNotifyRespawned(
        NetworkConnectionToClient target,
        Vector3 position,
        Quaternion rotation
    )
    {
        TeleportTo(position, rotation);
        OnRespawned?.Invoke();
    }
}