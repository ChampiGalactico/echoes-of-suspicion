using Mirror;
using UnityEngine;
using Adrenak.UniMic;

/// <summary>
/// Detecta el ruido del micrófono del jugador local y publica eventos al bus.
/// NO abre el mic directamente — se apoya en UniVoice/UniMic para acceder al audio
/// que ya se está capturando (evita conflictos de doble captura).
///
/// Se pega al prefab del Player.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public sealed class MicrophoneNoiseSource : NetworkBehaviour
{
    [Header("Noise Detection")]
    [SerializeField, Range(0f, 1f), Tooltip("Volumen mínimo para considerarse ruido (0-1). " +
        "Con ganancia aplicada, valores típicos: susurro ~0.05, hablar normal ~0.15, gritar ~0.5.")]
    private float noiseThreshold = 0.15f;

    [SerializeField, Range(1f, 20f), Tooltip("Multiplicador de amplificación del RMS. " +
        "Sube este valor si tu mic capta muy suave. Valor típico: 5-10.")]
    private float gainMultiplier = 5f;

    [SerializeField, Tooltip("Intervalo mínimo entre publicaciones al bus (segundos).")]
    private float publishInterval = 0.1f;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = false;

    [Header("Debug Controls")]
    [SerializeField]
    private UnityEngine.InputSystem.Key muteToggleKey = UnityEngine.InputSystem.Key.M;

    [SerializeField]
    private bool isMuted = false;

    private Mic.Device subscribedDevice;
    private float lastPublishTime;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SubscribeToUniVoiceMic();
    }

    public override void OnStopLocalPlayer()
    {
        UnsubscribeFromUniVoiceMic();
        base.OnStopLocalPlayer();
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        HandleMuteToggle();

        // Reintentar suscripción si no lo logramos al inicio (UniVoice puede tardar en arrancar).
        if (subscribedDevice == null)
        {
            SubscribeToUniVoiceMic();
        }
    }

    private void SubscribeToUniVoiceMic()
    {
        var devices = Mic.AvailableDevices;

        if (devices == null || devices.Count == 0)
        {
            return;
        }

        var device = devices[0];

        // Ya suscrito al mismo device, no hacer nada.
        if (subscribedDevice == device)
        {
            return;
        }

        // Si estábamos suscritos a otro device, desuscribirse.
        if (subscribedDevice != null)
        {
            subscribedDevice.OnFrameCollected -= OnAudioFrameCollected;
        }

        subscribedDevice = device;
        subscribedDevice.OnFrameCollected += OnAudioFrameCollected;

        Debug.Log($"[MicrophoneNoiseSource] Suscrito al mic de UniVoice: {device.Name}");
    }

    private void UnsubscribeFromUniVoiceMic()
    {
        if (subscribedDevice != null)
        {
            subscribedDevice.OnFrameCollected -= OnAudioFrameCollected;
            subscribedDevice = null;
            Debug.Log("[MicrophoneNoiseSource] Desuscrito del mic de UniVoice.");
        }
    }

    /// <summary>
    /// Callback que UniVoice llama cada vez que llega un frame de audio del mic.
    /// El buffer 'samples' contiene los samples de este frame — calculamos RMS y publicamos.
    /// </summary>
    private void OnAudioFrameCollected(int frequency, int channels, float[] samples)
    {
        // Si está muteado o no somos el jugador local, ignorar.
        if (isMuted || !isLocalPlayer)
        {
            return;
        }

        // Solo publicar cada X segundos (throttling).
        if (Time.time - lastPublishTime < publishInterval)
        {
            return;
        }

        float rawRms = CalculateRMS(samples);
        float rms = rawRms * gainMultiplier;

        if (showDebugLogs)
        {
            Debug.Log($"[MicrophoneNoiseSource] RMS raw: {rawRms:F4}, " +
                      $"RMS gain: {rms:F4} (umbral: {noiseThreshold:F4})");
        }

        if (rms >= noiseThreshold)
        {
            lastPublishTime = Time.time;
            PublishNoiseEvent(rms);
        }
    }

    private static float CalculateRMS(float[] samples)
    {
        float sumOfSquares = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            sumOfSquares += samples[i] * samples[i];
        }

        float mean = sumOfSquares / samples.Length;
        return Mathf.Sqrt(mean);
    }

    private void PublishNoiseEvent(float rms)
    {
        float intensity = Mathf.Clamp01(rms);

        // Publica localmente para el HUD del propio jugador.
        NoiseEvent localNoiseEvent = new NoiseEvent(
            worldPosition: transform.position,
            intensity: intensity,
            source: NoiseSource.Voice,
            sourcePlayerNetId: netId
        );
        NoiseEventBus.Publish(localNoiseEvent);

        // Manda al servidor para que la criatura lo escuche.
        CmdReportNoise(transform.position, intensity, NoiseSource.Voice);
    }

    [Command]
    private void CmdReportNoise(Vector3 position, float intensity, NoiseSource source)
    {
        Debug.Log($"[Server] 📨 Mensaje recibido del cliente. " +
                  $"Player netId: {netId}, " +
                  $"connectionId: {connectionToClient?.connectionId}, " +
                  $"intensidad: {intensity:F2}");

        NoiseEvent noiseEvent = new NoiseEvent(
            worldPosition: position,
            intensity: intensity,
            source: source,
            sourcePlayerNetId: netId
        );
        NoiseEventBus.Publish(noiseEvent);

        RpcNotifyNoise(position, intensity, source);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcNotifyNoise(Vector3 position, float intensity, NoiseSource source)
    {
        if (isServer)
        {
            return;
        }

        NoiseEvent noiseEvent = new NoiseEvent(
            worldPosition: position,
            intensity: intensity,
            source: source,
            sourcePlayerNetId: netId
        );
        NoiseEventBus.Publish(noiseEvent);
    }

    private void HandleMuteToggle()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard[muteToggleKey].wasPressedThisFrame)
        {
            isMuted = !isMuted;

            // Mutear el detector (no publica eventos).
            // No hay que apagar ningún mic aquí — UniVoice sigue con su lógica propia.

            // Mutea también el voice chat de UniVoice.
            if (EOSVoiceManager.Instance != null)
            {
                if (isMuted) EOSVoiceManager.Instance.Mute();
                else EOSVoiceManager.Instance.Unmute();
            }

            Debug.Log($"[MicrophoneNoiseSource] 🎙️ Mute: {(isMuted ? "ON" : "OFF")}");
        }
    }
}