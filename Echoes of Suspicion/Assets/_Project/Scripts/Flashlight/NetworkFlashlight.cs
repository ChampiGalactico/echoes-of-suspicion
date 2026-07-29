using Mirror;
using UnityEngine;

/// <summary>
/// Networked flashlight — a permanent ability on the player.
/// Independent of the inventory system: works regardless of the active slot,
/// so the player can hold any item while keeping the flashlight on.
///
/// Controls:
///   F  → toggle on/off (always available)
///   R  → reload battery (consumes one from the battery counter in NetworkInventory)
///
/// Battery drains on the server; the light state is synced to all clients.
/// </summary>
[DisallowMultipleComponent]
public class NetworkFlashlight : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField]
    private FlashlightData config;

    [Header("References")]
    [SerializeField]
    private Light spotLight;

    [SerializeField]
    private NetworkInventory inventory;

    // ── Synced state ──────────────────────────────────────────

    [SyncVar(hook = nameof(OnIsOnChanged))]
    private bool isOn;

    [SyncVar]
    private float currentBattery;

    // ── Public accessors ──────────────────────────────────────

    public bool IsOn => isOn;
    public float CurrentBattery => currentBattery;

    /// <summary>Normalized 0-1 battery level for UI.</summary>
    public float BatteryNormalized
    {
        get
        {
            if (config == null || config.maxBattery <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(currentBattery / config.maxBattery);
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<NetworkInventory>();
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        isOn = false;
        currentBattery = config != null ? config.maxBattery : 0f;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshLight();
    }

    private void Update()
    {
        if (isServer)
        {
            ServerDrainBattery();
        }
    }

    // ── Server battery drain ──────────────────────────────────

    [Server]
    private void ServerDrainBattery()
    {
        if (!isOn || config == null)
        {
            return;
        }

        currentBattery -= config.drainPerSecond * Time.deltaTime;

        if (currentBattery <= 0f)
        {
            currentBattery = 0f;
            isOn = false;
        }
    }

    // ── Commands ──────────────────────────────────────────────

    [Command]
    public void CmdToggle()
    {
        if (config == null)
        {
            return;
        }

        if (isOn)
        {
            isOn = false;
            return;
        }

        // Can't turn on with no battery.
        if (currentBattery <= 0f)
        {
            return;
        }

        isOn = true;
    }

    [Command]
    public void CmdReloadBattery()
    {
        if (config == null)
        {
            return;
        }

        // Already full?
        if (currentBattery >= config.maxBattery)
        {
            return;
        }

        if (inventory == null || !inventory.ServerConsumeBattery())
        {
            return; // No batteries available.
        }

        currentBattery = config.maxBattery;
    }

    // ── Sync hooks & visuals ──────────────────────────────────

    private void OnIsOnChanged(bool oldValue, bool newValue)
    {
        RefreshLight();
    }

    private void RefreshLight()
    {
        if (spotLight == null)
        {
            return;
        }

        spotLight.enabled = isOn;

        if (config != null)
        {
            spotLight.range = config.lightRange;
            spotLight.intensity = config.lightIntensity;
            spotLight.spotAngle = config.spotAngle;
            spotLight.color = config.lightColor;
        }
    }
}
