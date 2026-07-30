using Mirror;
using UnityEngine;

/// <summary>
/// Central controller for all fullscreen screen effects.
/// Drives the "Echoes/ScreenEffects" shader via global properties.
///
/// Effects:
///   - Damage:    red vignette flash + pain sound.
///   - Heal:      green vignette flash + heal sound.
///   - Low health: persistent pulsing red vignette.
///   - Alert:     continuous rapid black flicker while the creature investigates.
///   - Chase:     continuous crimson heartbeat while the creature chases.
///   - Attack:    handled by the damage flash (health changes trigger it).
///
/// SETUP:
/// 1. Create a Material using the "Echoes/ScreenEffects" shader.
/// 2. In your URP Renderer Data, add a "Full Screen Pass Renderer Feature":
///      - Pass Material → the material you created.
///      - Injection Point → "After Rendering Post Processing".
/// 3. Place this script on any GameObject in the scene.
/// 4. Assign AudioClips (optional).
/// </summary>
[DisallowMultipleComponent]
public sealed class ScreenEffectsController : MonoBehaviour
{
    // ── Shader property IDs ──────────────────────────────────

    private static readonly int FlashColorId =
        Shader.PropertyToID("_ScreenFX_FlashColor");
    private static readonly int FlashAmountId =
        Shader.PropertyToID("_ScreenFX_FlashAmount");
    private static readonly int LowHealthAmountId =
        Shader.PropertyToID("_ScreenFX_LowHealthAmount");
    private static readonly int DetectColorId =
        Shader.PropertyToID("_ScreenFX_DetectColor");
    private static readonly int DetectAmountId =
        Shader.PropertyToID("_ScreenFX_DetectAmount");

    // ── Inspector ────────────────────────────────────────────

    [Header("Damage / Heal Flash")]

    [SerializeField]
    private Color damageColor = new Color(0.8f, 0f, 0f, 0.7f);

    [SerializeField]
    private Color healColor = new Color(0f, 0.8f, 0.2f, 0.5f);

    [SerializeField, Min(0.1f)]
    private float fadeSpeed = 3f;

    [SerializeField, Range(0f, 1f)]
    private float damageFlashIntensity = 0.85f;

    [SerializeField, Range(0f, 1f)]
    private float healFlashIntensity = 0.5f;

    [Header("Low Health Pulse")]

    [SerializeField, Range(0f, 1f)]
    private float lowHealthThreshold = 0.3f;

    [SerializeField, Min(0f)]
    private float lowHealthPulseSpeed = 2f;

    [SerializeField, Range(0f, 1f)]
    private float lowHealthMinAlpha = 0.1f;

    [SerializeField, Range(0f, 1f)]
    private float lowHealthMaxAlpha = 0.4f;

    [Header("Alert (continuous black flicker)")]

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Max intensity of each black flicker while the creature is alert.")]
    private float alertFlickerIntensity = 0.85f;

    [SerializeField, Min(1f)]
    [Tooltip("Speed of the flicker (higher = faster).")]
    private float alertFlickerSpeed = 8f;

    [Header("Chase (crimson heartbeat)")]

    [SerializeField]
    [Tooltip("Color of the chase vignette pulse.")]
    private Color chaseColor = new Color(0.45f, 0.08f, 0.06f); // crimson / maroon

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Max intensity of the heartbeat during chase.")]
    private float chaseHeartbeatIntensity = 0.6f;

    [SerializeField, Min(0f)]
    [Tooltip("Speed of the heartbeat pulse.")]
    private float chaseHeartbeatSpeed = 3.5f;

    [Header("Detection Transitions")]

    [SerializeField, Min(0f)]
    [Tooltip("How fast the effect fades in/out when state changes.")]
    private float detectBlendSpeed = 3f;

    [Header("Audio")]

    [SerializeField]
    private AudioClip[] damageSounds;

    [SerializeField]
    private AudioClip[] healSounds;

    [SerializeField]
    private AudioClip alertSound;

    [SerializeField, Range(0f, 1f)]
    private float soundVolume = 0.7f;

    // ── Runtime state ────────────────────────────────────────

    private AudioSource audioSource;
    private NetworkIdentity boundPlayer;
    private PlayerHealth playerHealth;

    // Flash (damage / heal).
    private float flashAmount;
    private Color flashColor;
    private float healthRatio = 1f;
    private float previousHealth = -1f;

    // Detection state.
    private bool isAlerted;
    private bool isBeingChased;
    private float detectBlend; // 0..1 — blends in/out on state change

    // ── Lifecycle ─────────────────────────────────────────────

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        ResetShaderProperties();
    }

    private void OnEnable()
    {
        CreatureController.OnAnyCreatureStateChanged += HandleCreatureStateChanged;
    }

    private void OnDisable()
    {
        CreatureController.OnAnyCreatureStateChanged -= HandleCreatureStateChanged;
        UnbindPlayer();
        ResetShaderProperties();
    }

    private void Update()
    {
        TryBindLocalPlayer();
        UpdateFlash();
        UpdateLowHealth();
        UpdateDetection();
    }

    // ── Flash (damage / heal) ────────────────────────────────

    private void UpdateFlash()
    {
        if (flashAmount > 0f)
        {
            flashAmount = Mathf.MoveTowards(
                flashAmount, 0f,
                fadeSpeed * Time.unscaledDeltaTime
            );
        }

        Shader.SetGlobalColor(FlashColorId, flashColor);
        Shader.SetGlobalFloat(FlashAmountId, flashAmount);
    }

    // ── Low health ───────────────────────────────────────────

    private void UpdateLowHealth()
    {
        float amount = 0f;

        if (healthRatio > 0f && healthRatio <= lowHealthThreshold)
        {
            float pulse = Mathf.PingPong(
                Time.unscaledTime * lowHealthPulseSpeed, 1f
            );

            amount = Mathf.Lerp(lowHealthMinAlpha, lowHealthMaxAlpha, pulse);
        }

        Shader.SetGlobalFloat(LowHealthAmountId, amount);
    }

    // ── Creature detection ───────────────────────────────────

    private void HandleCreatureStateChanged(
        CreatureController creature,
        CreatureStateType stateType,
        uint? targetNetId)
    {
        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null || targetNetId == null)
            return;

        if (targetNetId.Value != localPlayer.netId)
            return;

        switch (stateType)
        {
            case CreatureStateType.Alert:
                // Continuous black flicker — "it heard something."
                isAlerted = true;
                isBeingChased = false;
                if (alertSound != null)
                    audioSource.PlayOneShot(alertSound, soundVolume);
                break;

            case CreatureStateType.Chase:
                // Crimson heartbeat — "it's coming for you."
                isAlerted = false;
                isBeingChased = true;
                break;

            case CreatureStateType.Attacking:
                // Keep chase heartbeat. Damage flash handles hit feedback.
                isAlerted = false;
                isBeingChased = true;
                break;

            default:
                // Patrol, Search, Stunned — threat over.
                isAlerted = false;
                isBeingChased = false;
                break;
        }
    }

    private void UpdateDetection()
    {
        bool wantsDetect = isAlerted || isBeingChased;

        // Blend in/out smoothly.
        float blendTarget = wantsDetect ? 1f : 0f;
        detectBlend = Mathf.MoveTowards(
            detectBlend, blendTarget,
            detectBlendSpeed * Time.unscaledDeltaTime
        );

        if (detectBlend < 0.001f)
        {
            Shader.SetGlobalFloat(DetectAmountId, 0f);
            return;
        }

        float detectAmount;
        Color detectColor;

        if (isBeingChased)
        {
            // Chase: crimson heartbeat.
            detectColor = chaseColor;
            float t = Time.unscaledTime * chaseHeartbeatSpeed;
            float beat = Mathf.Abs(Mathf.Sin(t * Mathf.PI));
            beat = Mathf.Pow(beat, 3f);
            detectAmount = beat * chaseHeartbeatIntensity * detectBlend;
        }
        else
        {
            // Alert: rapid black flicker (continuous while alerted).
            detectColor = Color.black;
            float phase = Time.unscaledTime * alertFlickerSpeed;
            float frac = phase - Mathf.Floor(phase);
            float beat = Mathf.Sin(frac * Mathf.PI);
            beat = Mathf.Pow(beat, 2f);
            detectAmount = beat * alertFlickerIntensity * detectBlend;
        }

        Shader.SetGlobalColor(DetectColorId, detectColor);
        Shader.SetGlobalFloat(DetectAmountId, detectAmount);
    }

    // ── Player health binding ────────────────────────────────

    private void TryBindLocalPlayer()
    {
        NetworkIdentity localPlayer = NetworkClient.localPlayer;

        if (localPlayer == null || localPlayer == boundPlayer)
            return;

        UnbindPlayer();

        boundPlayer = localPlayer;
        playerHealth = boundPlayer.GetComponent<PlayerHealth>();

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            previousHealth = playerHealth.CurrentHealth;
            healthRatio = playerHealth.MaxHealth > 0f
                ? playerHealth.CurrentHealth / playerHealth.MaxHealth
                : 1f;
        }
    }

    private void UnbindPlayer()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= HandleHealthChanged;

        boundPlayer = null;
        playerHealth = null;
        previousHealth = -1f;
    }

    private void HandleHealthChanged(float current, float max)
    {
        healthRatio = max > 0f ? current / max : 1f;

        if (previousHealth < 0f)
        {
            previousHealth = current;
            return;
        }

        float delta = current - previousHealth;
        previousHealth = current;

        if (Mathf.Abs(delta) < 0.01f)
            return;

        if (delta < 0f)
        {
            FlashVignette(damageColor, damageFlashIntensity);
            PlayRandomSound(damageSounds);
        }
        else
        {
            FlashVignette(healColor, healFlashIntensity);
            PlayRandomSound(healSounds);
        }
    }

    private void FlashVignette(Color color, float intensity)
    {
        flashColor = color;
        flashAmount = intensity;
    }

    private void PlayRandomSound(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        audioSource.PlayOneShot(clip, soundVolume);
    }

    private void ResetShaderProperties()
    {
        Shader.SetGlobalColor(FlashColorId, Color.clear);
        Shader.SetGlobalFloat(FlashAmountId, 0f);
        Shader.SetGlobalFloat(LowHealthAmountId, 0f);
        Shader.SetGlobalColor(DetectColorId, Color.black);
        Shader.SetGlobalFloat(DetectAmountId, 0f);
    }
}
