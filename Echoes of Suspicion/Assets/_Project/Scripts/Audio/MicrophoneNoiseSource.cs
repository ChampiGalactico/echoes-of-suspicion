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

    private PlayerRole role;

    private CharacterStatsProvider statsProvider;

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

    private void Awake()
    {
        statsProvider = GetComponent<CharacterStatsProvider>();
        role = statsProvider.Role;
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

        ResolveNoiseIdentity(out Vector3 noisePosition, out uint noiseSourceNetId);

        NoiseEvent noiseEvent = new NoiseEvent(
            worldPosition: noisePosition,
            intensity: intensity,
            source: NoiseSource.Voice,
            sourcePlayerNetId: noiseSourceNetId
        );

        // Publica localmente para el HUD del propio jugador.
        NoiseEventBus.Publish(noiseEvent);

        // Manda al servidor para que la criatura lo escuche.
        CmdReportNoise(noiseEvent);
    }

    /// <summary>
    /// Decide dónde "suena" este ruido y a nombre de quién.
    ///
    /// - Si soy Runner: siempre mi propia posición y mi propio netId (caso normal).
    /// - Si soy Guide y NO estamos reunidos: mi voz se transmite por el altavoz al
    ///   entorno del Corredor — el ruido debe sonar en SU posición, y a nombre
    ///   suyo (sourcePlayerNetId = netId del Runner). Así toda la lógica de
    ///   percepción de la criatura (que filtra por sourcePlayerNetId) funciona
    ///   sin ningún cambio: cree que fue el Corredor quien hizo ruido.
    /// - Si soy Guide y SÍ estamos reunidos (Acto 2, reencuentro): ya estamos
    ///   físicamente en el mismo lugar, así que mi propia posición y mi propio
    ///   netId son correctos — la criatura puede perseguirme a mí directamente.
    /// </summary>
    private void ResolveNoiseIdentity(out Vector3 noisePosition, out uint noiseSourceNetId)
    {
        noisePosition = transform.position;
        noiseSourceNetId = netId;

        if (statsProvider.Role != PlayerRole.Guide)
        {
            return;
        }

        if (EOSNetworkManager.AreProtagonistsReunited)
        {
            return;
        }

        var runnerProvider = PlayerUtils.FindPlayerByRole(PlayerRole.Runner);
        if (runnerProvider != null)
        {
            noisePosition = runnerProvider.transform.position;
            noiseSourceNetId = runnerProvider.netId;
        }
    }

    [Command]
    private void CmdReportNoise(NoiseEvent noiseEvent)
    {
        Debug.Log($"[Server] 📨 Mensaje recibido del cliente. " +
                  $"Player netId real: {netId}, " +
                  $"reportado como: {noiseEvent.sourcePlayerNetId}, " +
                  $"connectionId: {connectionToClient?.connectionId}, " +
                  $"intensidad: {noiseEvent.intensity:F2}");

        NoiseEventBus.Publish(noiseEvent);
        RpcNotifyNoise(noiseEvent);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcNotifyNoise(NoiseEvent noiseEvent)
    {
        if (isServer)
        {
            return;
        }

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