using UnityEngine;

/// <summary>
/// Reproduce el sonido del latido cuando RunnerCreatureAwareness dispara un pulso.
/// Puramente de presentación — no sabe nada de Perspicacia ni de criaturas,
/// solo escucha el evento y suena.
///
/// El sonido solo debe oírlo el propio jugador (es su percepción interna,
/// no algo que otros jugadores deban escuchar), por eso se reproduce en 2D
/// y solo si es el jugador local.
/// </summary>
[RequireComponent(typeof(RunnerCreatureAwareness))]
[RequireComponent(typeof(AudioSource))]
public sealed class HeartbeatAudioFeedback : MonoBehaviour
{
    [SerializeField, Tooltip("Clip del latido (un solo 'tun').")]
    private AudioClip heartbeatClip;

    [SerializeField, Range(0f, 1f)]
    private float volume = 0.7f;

    private RunnerCreatureAwareness awareness;
    private AudioSource audioSource;
    private Mirror.NetworkIdentity identity;

    private void Awake()
    {
        awareness = GetComponent<RunnerCreatureAwareness>();
        audioSource = GetComponent<AudioSource>();
        identity = GetComponent<Mirror.NetworkIdentity>();

        // 2D: es una sensación interna del jugador, no un sonido que "sale" de su cuerpo.
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        awareness.OnHeartbeatPulse += HandleHeartbeatPulse;
    }

    private void OnDisable()
    {
        awareness.OnHeartbeatPulse -= HandleHeartbeatPulse;
    }

    private void HandleHeartbeatPulse()
    {
        // Este evento viene del servidor (RunnerCreatureAwareness corre en servidor).
        // Solo queremos que suene en la máquina del propio jugador dueño.
        if (identity != null && !identity.isLocalPlayer)
        {
            return;
        }

        if (heartbeatClip != null)
        {
            audioSource.PlayOneShot(heartbeatClip, volume);
        }
    }
}