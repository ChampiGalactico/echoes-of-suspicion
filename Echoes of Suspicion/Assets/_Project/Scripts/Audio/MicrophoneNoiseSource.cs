using System;
using Adrenak.UniMic;
using Mirror;
using UnityEngine;

/// <summary>
/// Detecta el ruido del micrófono del jugador local y publica eventos
/// para los sistemas de gameplay.
///
/// No abre el micrófono directamente: utiliza el audio que
/// UniVoice/UniMic ya está capturando para evitar conflictos
/// por doble captura.
///
/// También expone al HUD:
/// - Nivel continuo del micrófono entre 0 y 1.
/// - Estado de peligro usando el mismo umbral de las criaturas.
/// - Estado de mute.
///
/// El nivel visual y el estado de peligro se sincronizan mediante
/// Mirror para que el Guía pueda observar el micrófono del Corredor.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(CharacterStatsProvider))]
public sealed class MicrophoneNoiseSource : NetworkBehaviour
{
    [Header("Noise Detection")]

    [SerializeField, Range(0f, 1f)]
    [Tooltip(
        "Volumen mínimo para considerarse ruido (0-1). " +
        "Con ganancia aplicada, valores típicos: susurro ~0.05, " +
        "hablar normal ~0.15, gritar ~0.5."
    )]
    private float noiseThreshold = 0.15f;

    [SerializeField, Range(1f, 20f)]
    [Tooltip(
        "Multiplicador de amplificación del RMS. " +
        "Sube este valor si el micrófono capta muy suave. " +
        "Valor típico: 5-10."
    )]
    private float gainMultiplier = 5f;

    [SerializeField]
    [Tooltip(
        "Intervalo mínimo entre publicaciones al bus, en segundos."
    )]
    private float publishInterval = 0.1f;

    [Header("HUD Feedback")]

    [SerializeField, Range(0.05f, 1f)]
    [Tooltip(
        "Nivel RMS con ganancia que representará el micrófono " +
        "completamente lleno en el HUD."
    )]
    private float hudMaxRms = 0.5f;

    [SerializeField, Min(1f)]
    [Tooltip(
        "Velocidad con la que el relleno visual responde al sonido."
    )]
    private float hudResponseSpeed = 10f;

    [SerializeField, Min(0.05f)]
    [Tooltip(
        "Tiempo sin recibir frames de audio antes de vaciar " +
        "el indicador."
    )]
    private float noFrameTimeout = 0.25f;

    [SerializeField, Min(0f)]
    [Tooltip(
        "Tiempo adicional que permanece activo el estado de peligro. " +
        "Evita que el indicador rojo parpadee demasiado rápido."
    )]
    private float dangerHoldTime = 0.15f;

    [Header("HUD Network Replication")]

    [SerializeField, Min(0.02f)]
    [Tooltip(
        "Intervalo mínimo entre actualizaciones visuales " +
        "enviadas al servidor."
    )]
    private float hudNetworkInterval = 0.05f;

    [SerializeField, Range(0.001f, 0.25f)]
    [Tooltip(
        "Cambio mínimo del nivel visual necesario para enviar " +
        "una nueva actualización."
    )]
    private float hudNetworkMinDelta = 0.01f;

    [Header("Debug")]

    [SerializeField]
    private bool showDebugLogs;

    [Header("Debug Controls")]

    [SerializeField]
    private UnityEngine.InputSystem.Key muteToggleKey =
        UnityEngine.InputSystem.Key.M;

    [SerializeField]
    private bool isMuted;

    /*
     * Estos valores son escritos por el servidor.
     *
     * El propietario del micrófono utiliza los valores locales para
     * evitar latencia. Los demás jugadores usan estas copias
     * sincronizadas.
     */
    [SyncVar(hook = nameof(HandleReplicatedHudLevelChanged))]
    private float replicatedHudLevel;

    [SyncVar(hook = nameof(HandleReplicatedDangerStateChanged))]
    private bool replicatedDangerState;

    private Mic.Device subscribedDevice;
    private CharacterStatsProvider statsProvider;

    private float lastPublishTime;
    private float lastAudioFrameTime =
        float.NegativeInfinity;

    private float lastDangerTime =
        float.NegativeInfinity;

    private float targetHudLevel;
    private bool targetDangerState;

    private float lastNotifiedHudLevel = -1f;
    private bool lastNotifiedDangerState;

    private float lastHudNetworkSendTime =
        float.NegativeInfinity;

    private float lastSentHudLevel = -1f;
    private bool lastSentDangerState;

    /// <summary>
    /// Nivel suavizado calculado en el jugador propietario.
    /// Siempre está entre 0 y 1.
    /// </summary>
    public float CurrentHudLevel
    {
        get;
        private set;
    }

    /// <summary>
    /// Indica si la voz local está superando el umbral que genera
    /// eventos de ruido para las criaturas.
    /// </summary>
    public bool IsNoiseDangerous
    {
        get;
        private set;
    }

    /// <summary>
    /// Nivel que debe utilizar el HUD.
    ///
    /// El propietario usa el valor local inmediatamente.
    /// Los demás jugadores utilizan el valor sincronizado.
    /// </summary>
    public float DisplayHudLevel =>
        isLocalPlayer
            ? CurrentHudLevel
            : replicatedHudLevel;

    /// <summary>
    /// Estado de peligro que debe utilizar el HUD.
    ///
    /// El propietario usa el resultado local.
    /// Los demás jugadores utilizan el resultado sincronizado.
    /// </summary>
    public bool DisplayDangerState =>
        isLocalPlayer
            ? IsNoiseDangerous
            : replicatedDangerState;

    /// <summary>
    /// Indica si el jugador local tiene el micrófono muteado.
    /// </summary>
    public bool IsMuted => isMuted;

    /// <summary>
    /// Umbral utilizado para considerar la voz como ruido.
    /// </summary>
    public float NoiseThreshold => noiseThreshold;

    /// <summary>
    /// Se emite cuando cambia el nivel visual del micrófono.
    /// El valor está normalizado entre 0 y 1.
    /// </summary>
    public event Action<float> HudLevelChanged;

    /// <summary>
    /// Se emite cuando entra o sale del estado de ruido peligroso.
    /// </summary>
    public event Action<bool> DangerStateChanged;

    /// <summary>
    /// Se emite cuando cambia el estado de mute.
    /// </summary>
    public event Action<bool> MuteStateChanged;

    private void Awake()
    {
        statsProvider =
            GetComponent<CharacterStatsProvider>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        ResetHudState();
        SubscribeToUniVoiceMic();
        SubscribeToDeviceChanged();
    }

    public override void OnStopLocalPlayer()
    {
        UnsubscribeFromDeviceChanged();
        UnsubscribeFromUniVoiceMic();
        ResetHudState();

        base.OnStopLocalPlayer();
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        HandleMuteToggle();

        /*
         * UniVoice puede tardar algunos frames en inicializarse.
         */
        if (subscribedDevice == null)
        {
            SubscribeToUniVoiceMic();
        }

        /*
         * Si dejaron de llegar frames de audio, vacía
         * progresivamente el indicador.
         */
        if (
            Time.unscaledTime - lastAudioFrameTime >
            noFrameTimeout
        )
        {
            targetHudLevel = 0f;
            targetDangerState = false;
        }

        if (isMuted)
        {
            targetHudLevel = 0f;
            targetDangerState = false;
        }

        UpdateHudFeedback();
        ReplicateHudState();
    }

    private void UpdateHudFeedback()
    {
        float smoothing =
            1f -
            Mathf.Exp(
                -hudResponseSpeed *
                Time.unscaledDeltaTime
            );

        CurrentHudLevel = Mathf.Lerp(
            CurrentHudLevel,
            targetHudLevel,
            smoothing
        );

        if (CurrentHudLevel < 0.001f)
        {
            CurrentHudLevel = 0f;
        }

        bool dangerStillActive =
            Time.unscaledTime - lastDangerTime <=
            dangerHoldTime;

        bool newDangerState =
            !isMuted &&
            (
                targetDangerState ||
                dangerStillActive
            );

        IsNoiseDangerous = newDangerState;

        /*
         * Evita emitir eventos por diferencias
         * visualmente insignificantes.
         */
        if (
            Mathf.Abs(
                CurrentHudLevel -
                lastNotifiedHudLevel
            ) >= 0.005f
        )
        {
            lastNotifiedHudLevel =
                CurrentHudLevel;

            HudLevelChanged?.Invoke(
                CurrentHudLevel
            );
        }

        if (
            IsNoiseDangerous !=
            lastNotifiedDangerState
        )
        {
            lastNotifiedDangerState =
                IsNoiseDangerous;

            DangerStateChanged?.Invoke(
                IsNoiseDangerous
            );
        }
    }

    /// <summary>
    /// Envía periódicamente al servidor el estado visual
    /// del micrófono para que pueda replicarse a otros jugadores.
    /// </summary>
    private void ReplicateHudState()
    {
        if (
            Time.unscaledTime -
            lastHudNetworkSendTime <
            hudNetworkInterval
        )
        {
            return;
        }

        bool levelChanged =
            Mathf.Abs(
                CurrentHudLevel -
                lastSentHudLevel
            ) >= hudNetworkMinDelta;

        bool dangerChanged =
            IsNoiseDangerous !=
            lastSentDangerState;

        if (!levelChanged && !dangerChanged)
        {
            return;
        }

        lastHudNetworkSendTime =
            Time.unscaledTime;

        lastSentHudLevel =
            CurrentHudLevel;

        lastSentDangerState =
            IsNoiseDangerous;

        /*
         * En una partida host, el jugador local también es
         * el servidor y no necesita enviarse un Command.
         */
        if (isServer)
        {
            ServerSetReplicatedHudState(
                CurrentHudLevel,
                IsNoiseDangerous
            );

            return;
        }

        CmdSyncHudState(
            CurrentHudLevel,
            IsNoiseDangerous
        );
    }

    /// <summary>
    /// Recibe en el servidor el estado visual calculado
    /// por el jugador propietario.
    /// </summary>
    [Command]
    private void CmdSyncHudState(
        float hudLevel,
        bool dangerState
    )
    {
        ServerSetReplicatedHudState(
            hudLevel,
            dangerState
        );
    }

    /// <summary>
    /// Guarda los valores validados en los SyncVar.
    /// </summary>
    [Server]
    private void ServerSetReplicatedHudState(
        float hudLevel,
        bool dangerState
    )
    {
        hudLevel =
            Mathf.Clamp01(hudLevel);

        bool levelChanged =
            !Mathf.Approximately(
                replicatedHudLevel,
                hudLevel
            );

        bool dangerChanged =
            replicatedDangerState !=
            dangerState;

        replicatedHudLevel =
            hudLevel;

        replicatedDangerState =
            dangerState;

        /*
         * En el host, los hooks de SyncVar no siempre son la vía
         * adecuada para actualizar la UI del servidor local.
         *
         * Emitimos el evento directamente para objetos remotos,
         * como el Runner observado por el Guide host.
         */
        if (!isLocalPlayer && levelChanged)
        {
            HudLevelChanged?.Invoke(
                replicatedHudLevel
            );
        }

        if (!isLocalPlayer && dangerChanged)
        {
            DangerStateChanged?.Invoke(
                replicatedDangerState
            );
        }
    }

    private void HandleReplicatedHudLevelChanged(
        float oldValue,
        float newValue
    )
    {
        /*
         * El propietario ya utiliza sus mediciones locales.
         * El host recibe el evento directamente desde el servidor.
         */
        if (isLocalPlayer || isServer)
        {
            return;
        }

        HudLevelChanged?.Invoke(
            Mathf.Clamp01(newValue)
        );
    }

    private void HandleReplicatedDangerStateChanged(
        bool oldValue,
        bool newValue
    )
    {
        if (isLocalPlayer || isServer)
        {
            return;
        }

        DangerStateChanged?.Invoke(newValue);
    }

    private void SubscribeToDeviceChanged()
    {
        EOSVoiceChatSetup setup =
            FindAnyObjectByType<EOSVoiceChatSetup>();

        if (setup != null)
        {
            setup.OnDeviceChanged +=
                OnMicDeviceChanged;
        }
    }

    private void UnsubscribeFromDeviceChanged()
    {
        EOSVoiceChatSetup setup =
            FindAnyObjectByType<EOSVoiceChatSetup>();

        if (setup != null)
        {
            setup.OnDeviceChanged -=
                OnMicDeviceChanged;
        }
    }

    /// <summary>
    /// Callback de EOSVoiceChatSetup.OnDeviceChanged.
    /// Se desuscribe del micrófono anterior y se suscribe al nuevo.
    /// </summary>
    private void OnMicDeviceChanged(
        Mic.Device newDevice
    )
    {
        if (!isLocalPlayer)
        {
            return;
        }

        UnsubscribeFromUniVoiceMic();

        if (newDevice != null)
        {
            SubscribeToUniVoiceMic(newDevice);
        }
    }

    private void SubscribeToUniVoiceMic(
        Mic.Device specificDevice = null
    )
    {
        Mic.Device device;

        if (specificDevice != null)
        {
            device = specificDevice;
        }
        else
        {
            var devices =
                Mic.AvailableDevices;

            if (
                devices == null ||
                devices.Count == 0
            )
            {
                return;
            }

            device = devices[0];
        }

        /*
         * Ya estamos suscritos al mismo dispositivo.
         */
        if (subscribedDevice == device)
        {
            return;
        }

        /*
         * Si estábamos suscritos a otro dispositivo,
         * lo soltamos primero.
         */
        if (subscribedDevice != null)
        {
            subscribedDevice.OnFrameCollected -=
                OnAudioFrameCollected;
        }

        subscribedDevice = device;

        subscribedDevice.OnFrameCollected +=
            OnAudioFrameCollected;

        Debug.Log(
            "[MicrophoneNoiseSource] Suscrito al mic de " +
            $"UniVoice: {device.Name}"
        );
    }

    private void UnsubscribeFromUniVoiceMic()
    {
        if (subscribedDevice == null)
        {
            return;
        }

        subscribedDevice.OnFrameCollected -=
            OnAudioFrameCollected;

        subscribedDevice = null;

        Debug.Log(
            "[MicrophoneNoiseSource] Desuscrito del mic " +
            "de UniVoice."
        );
    }

    /// <summary>
    /// UniVoice llama este método cada vez que recibe
    /// un frame de audio.
    ///
    /// Se calcula el RMS para actualizar el HUD y publicar
    /// ruido cuando se supera el umbral.
    /// </summary>
    private void OnAudioFrameCollected(
        int frequency,
        int channels,
        float[] samples
    )
    {
        if (!isLocalPlayer)
        {
            return;
        }

        lastAudioFrameTime =
            Time.unscaledTime;

        if (isMuted)
        {
            targetHudLevel = 0f;
            targetDangerState = false;
            return;
        }

        float rawRms =
            CalculateRMS(samples);

        float rms =
            rawRms *
            gainMultiplier;

        /*
         * El HUD se actualiza siempre, incluso por debajo
         * del umbral de peligro.
         */
        targetHudLevel =
            Mathf.InverseLerp(
                0f,
                hudMaxRms,
                rms
            );

        targetDangerState =
            rms >= noiseThreshold;

        if (targetDangerState)
        {
            lastDangerTime =
                Time.unscaledTime;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[MicrophoneNoiseSource] RMS raw: {rawRms:F4}, " +
                $"RMS gain: {rms:F4}, " +
                $"HUD: {targetHudLevel:F2}, " +
                $"umbral: {noiseThreshold:F4}"
            );
        }

        /*
         * El throttling limita los eventos de gameplay,
         * pero no limita la actualización visual del HUD.
         */
        if (
            Time.time -
            lastPublishTime <
            publishInterval
        )
        {
            return;
        }

        if (targetDangerState)
        {
            lastPublishTime =
                Time.time;

            PublishNoiseEvent(rms);
        }
    }

    private static float CalculateRMS(
        float[] samples
    )
    {
        if (
            samples == null ||
            samples.Length == 0
        )
        {
            return 0f;
        }

        float sumOfSquares = 0f;

        for (
            int index = 0;
            index < samples.Length;
            index++
        )
        {
            sumOfSquares +=
                samples[index] *
                samples[index];
        }

        float mean =
            sumOfSquares /
            samples.Length;

        return Mathf.Sqrt(mean);
    }

    private void PublishNoiseEvent(
        float rms
    )
    {
        float intensity =
            Mathf.Clamp01(rms);

        ResolveNoiseIdentity(
            out Vector3 noisePosition,
            out uint noiseSourceNetId
        );

        NoiseEvent noiseEvent =
            new NoiseEvent(
                worldPosition: noisePosition,
                intensity: intensity,
                source: NoiseSource.Voice,
                sourcePlayerNetId: noiseSourceNetId
            );

        /*
         * Publicación local para el HUD y otros sistemas
         * del propietario.
         */
        NoiseEventBus.Publish(noiseEvent);

        /*
         * Reporte al servidor para que la criatura
         * escuche el ruido.
         */
        CmdReportNoise(noiseEvent);
    }

    /// <summary>
    /// Decide dónde se origina el ruido y a nombre
    /// de qué jugador.
    ///
    /// Runner:
    /// usa su posición y netId.
    ///
    /// Guide separado del Runner:
    /// su voz sale por el altavoz ubicado en el entorno del Runner.
    ///
    /// Guide reunido físicamente con el Runner:
    /// usa su propia posición y netId.
    /// </summary>
    private void ResolveNoiseIdentity(
        out Vector3 noisePosition,
        out uint noiseSourceNetId
    )
    {
        noisePosition =
            transform.position;

        noiseSourceNetId =
            netId;

        if (
            statsProvider == null ||
            statsProvider.Role != PlayerRole.Guide
        )
        {
            return;
        }

        if (
            EOSNetworkManager
                .AreProtagonistsReunited
        )
        {
            return;
        }

        CharacterStatsProvider runnerProvider =
            PlayerUtils.FindPlayerByRole(
                PlayerRole.Runner
            );

        if (runnerProvider != null)
        {
            noisePosition =
                runnerProvider.transform.position;

            noiseSourceNetId =
                runnerProvider.netId;
        }
    }

    [Command]
    private void CmdReportNoise(
        NoiseEvent noiseEvent
    )
    {
        Debug.Log(
            "[Server] 📨 Mensaje recibido del cliente. " +
            $"Player netId real: {netId}, " +
            $"reportado como: {noiseEvent.sourcePlayerNetId}, " +
            $"connectionId: {connectionToClient?.connectionId}, " +
            $"intensidad: {noiseEvent.intensity:F2}"
        );

        NoiseEventBus.Publish(noiseEvent);
        RpcNotifyNoise(noiseEvent);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcNotifyNoise(
        NoiseEvent noiseEvent
    )
    {
        if (isServer)
        {
            return;
        }

        NoiseEventBus.Publish(noiseEvent);
    }

    private void HandleMuteToggle()
    {
        var keyboard =
            UnityEngine.InputSystem.Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (
            !keyboard[muteToggleKey]
                .wasPressedThisFrame
        )
        {
            return;
        }

        isMuted =
            !isMuted;

        if (isMuted)
        {
            targetHudLevel = 0f;
            CurrentHudLevel = 0f;

            targetDangerState = false;
            IsNoiseDangerous = false;

            lastNotifiedHudLevel = 0f;
            lastNotifiedDangerState = false;

            HudLevelChanged?.Invoke(0f);
            DangerStateChanged?.Invoke(false);
        }

        MuteStateChanged?.Invoke(isMuted);

        if (EOSVoiceManager.Instance != null)
        {
            if (isMuted)
            {
                EOSVoiceManager.Instance.Mute();
            }
            else
            {
                EOSVoiceManager.Instance.Unmute();
            }
        }

        Debug.Log(
            "[MicrophoneNoiseSource] 🎙️ Mute: " +
            $"{(isMuted ? "ON" : "OFF")}"
        );
    }

    private void ResetHudState()
    {
        targetHudLevel = 0f;
        CurrentHudLevel = 0f;

        targetDangerState = false;
        IsNoiseDangerous = false;

        lastAudioFrameTime =
            float.NegativeInfinity;

        lastDangerTime =
            float.NegativeInfinity;

        lastNotifiedHudLevel = -1f;
        lastNotifiedDangerState = false;

        lastHudNetworkSendTime =
            float.NegativeInfinity;

        lastSentHudLevel = -1f;
        lastSentDangerState = false;
    }
}