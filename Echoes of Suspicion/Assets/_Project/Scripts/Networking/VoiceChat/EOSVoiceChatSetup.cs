using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Mirror;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Networks;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Filters;
using Adrenak.BRW;

/// <summary>
/// Setup propio de UniVoice para Echoes of Suspicion.
///
/// Reemplaza UniVoiceMirrorSetupSample con las siguientes mejoras:
/// 1. Corrige el bug de threading del sample original donde el PEER_INIT
///    del host se envía desde un thread pool (Task.Delay) y falla
///    silenciosamente en la conexión local de Mirror.
/// 2. Selección de micrófono en runtime (SwitchMicrophone).
/// 3. Control de volumen por peer (SetPeerVolume) y master (SetMasterVolume).
/// 4. Filtros Concentus toggleables en runtime (ConcentusEnabled).
/// 5. Expone la misma API estática que el sample para compatibilidad.
///
/// REQUIERE en el proyecto:
/// - Mirror + símbolo MIRROR definido
/// - UniVoice 4.x con los paquetes UniMic, BRW, Concentus
///
/// USO: Colocar en el mismo GameObject que el EOSNetworkManager
///      (EOSNetworkSession prefab). Quitar UniVoiceMirrorSetupSample.
/// </summary>
public sealed class EOSVoiceChatSetup : MonoBehaviour
{
    const string TAG = "[EOSVoiceChatSetup]";

    // ===== CONFIGURACIÓN =====

    [Header("Filtros")]
    [SerializeField, Tooltip("Activa la compresión Opus (Concentus) para reducir el tamaño de los frames de audio. Desactivar solo para debug.")]
    private bool concentusEnabled = true;

    [Header("Audio Playback")]
    [SerializeField, Tooltip("Latencia objetivo del buffer de reproducción (segundos). Menor = menos delay, pero más riesgo de cortes/repeticiones. Default UniVoice: 0.25.")]
    private float playbackTargetLatency = 0.2f;

    [SerializeField, Range(0f, 0.2f), Tooltip("Desviación máxima de pitch para corrección de latencia. 0.05 = máx 5%. Valores altos causan voz aguda.")]
    private float playbackPitchMaxCorrection = 0.03f;

    [SerializeField, Range(0f, 5f), Tooltip("Ganancia proporcional del controlador de pitch. Menor = corrección más suave.")]
    private float playbackPitchGain = 0.3f;

    [SerializeField, Range(0f, 0.05f), Tooltip("Zona muerta del pitch (segundos). Dentro de esta banda no se corrige.")]
    private float playbackPitchDeadzone = 0.03f;

    [SerializeField, Range(0f, 1f), Tooltip("Escala de corrección hacia abajo (pitch < 1). Valores bajos evitan que el audio se frene y cause repeticiones. Default UniVoice: 0.25.")]
    private float playbackDownwardPitchScale = 0.1f;

    [SerializeField, Range(0f, 0.15f), Tooltip("Buffer extra de seguridad antes de iniciar playback (segundos). Previene arranques prematuros.")]
    private float playbackStartSafetyMargin = 0.05f;

    [Header("Host Init Fix")]
    [SerializeField, Tooltip("Segundos a esperar antes de verificar si el host client recibió su PEER_INIT.")]
    private float hostInitCheckDelay = 0.5f;

    [SerializeField, Tooltip("Segundos entre reintentos si el host client sigue sin inicializar.")]
    private float hostInitRetryInterval = 0.3f;

    [SerializeField, Tooltip("Cantidad máxima de reintentos para inicializar el host client.")]
    private int hostInitMaxRetries = 6;

    // ===== API ESTÁTICA (misma firma que UniVoiceMirrorSetupSample) =====

    /// <summary>
    /// True si el setup se completó exitosamente.
    /// </summary>
    public static bool HasSetUp { get; private set; }

    /// <summary>
    /// El servidor de audio (relay entre peers).
    /// </summary>
    public static IAudioServer<int> AudioServer { get; private set; }

    /// <summary>
    /// La sesión del cliente local (input + output + networking).
    /// </summary>
    public static ClientSession<int> ClientSession { get; private set; }

    // ===== MICRÓFONO =====

    /// <summary>
    /// El UniMicInput activo. Null si no hay micrófono disponible.
    /// </summary>
    private UniMicInput activeMicInput;

    /// <summary>
    /// El dispositivo de micrófono actualmente en uso. Null si no hay mic.
    /// </summary>
    public Mic.Device ActiveDevice => activeMicInput?.Device;

    /// <summary>
    /// Lista de dispositivos de micrófono disponibles.
    /// </summary>
    public IReadOnlyList<Mic.Device> AvailableDevices => Mic.AvailableDevices;

    /// <summary>
    /// Se dispara cuando se cambia de micrófono.
    /// Pasa el nuevo Mic.Device (o null si no hay mic).
    /// MicrophoneNoiseSource y otros sistemas deben suscribirse
    /// para reconectarse al nuevo device.
    /// </summary>
    public event Action<Mic.Device> OnDeviceChanged;

    // ===== VOLUMEN =====

    private float masterVolume = 1f;

    /// <summary>
    /// Volumen maestro aplicado a todos los peers (0-1).
    /// </summary>
    public float MasterVolume => masterVolume;

    // ===== FILTROS CONCENTUS =====

    private ConcentusEncodeFilter concentusEncodeFilter;
    private bool concentusDecodeRegistered;

    /// <summary>
    /// Activa/desactiva la compresión Opus (Concentus) en runtime.
    /// IMPORTANTE: ambos extremos (host y client) deben tener el mismo
    /// valor, o el audio llegará corrupto. Usar solo para debug local.
    /// </summary>
    public bool ConcentusEnabled
    {
        get => concentusEnabled;
        set
        {
            if (concentusEnabled == value)
            {
                return;
            }

            concentusEnabled = value;

            if (ClientSession == null)
            {
                return;
            }

            if (concentusEnabled)
            {
                EnableConcentus();
            }
            else
            {
                DisableConcentus();
            }

            Debug.Log($"{TAG} Concentus {(concentusEnabled ? "activado" : "desactivado")}.");
        }
    }

    // ===== CICLO DE VIDA =====

    private void Start()
    {
        if (HasSetUp)
        {
            Debug.unityLogger.Log(LogType.Log, TAG, "UniVoice ya fue configurado. Ignorando.");
            return;
        }

        HasSetUp = Setup();

        if (HasSetUp)
        {
            StartCoroutine(EnsureHostClientInitialized());
        }
    }

    private void OnDestroy()
    {
        if (AudioServer != null)
        {
            AudioServer.Dispose();
            AudioServer = null;
        }

        if (ClientSession != null)
        {
            ClientSession.Dispose();
            ClientSession = null;
        }

        activeMicInput = null;
        concentusEncodeFilter = null;
        concentusDecodeRegistered = false;
        HasSetUp = false;
    }

    // ===== SETUP =====

    private bool Setup()
    {
        Debug.unityLogger.Log(LogType.Log, TAG, "Iniciando setup de UniVoice...");

        bool ok = true;

        if (!SetupAudioServer())
        {
            Debug.unityLogger.Log(LogType.Error, TAG, "No se pudo crear el AudioServer.");
            ok = false;
        }

        if (!SetupClientSession())
        {
            Debug.unityLogger.Log(LogType.Error, TAG, "No se pudo crear el ClientSession.");
            ok = false;
        }

        if (ok)
        {
            Debug.unityLogger.Log(LogType.Log, TAG, "UniVoice configurado exitosamente.");
        }

        return ok;
    }

    private bool SetupAudioServer()
    {
#if MIRROR
        AudioServer = new MirrorServer();

        AudioServer.OnServerStart += () =>
        {
            Debug.unityLogger.Log(LogType.Log, TAG, "AudioServer iniciado.");
        };

        AudioServer.OnServerStop += () =>
        {
            Debug.unityLogger.Log(LogType.Log, TAG, "AudioServer detenido.");
        };

        return true;
#else
        Debug.unityLogger.Log(LogType.Error, TAG, "Símbolo MIRROR no definido.");
        return false;
#endif
    }

    private bool SetupClientSession()
    {
#if MIRROR
        // --- Networking ---
        IAudioClient<int> client = new MirrorClient();

        client.OnJoined += (id, peerIds) =>
        {
            Debug.unityLogger.Log(LogType.Log, TAG, $"Eres el Peer ID {id}.");
        };

        client.OnLeft += () =>
        {
            Debug.unityLogger.Log(LogType.Log, TAG, "Dejaste el chatroom.");
        };

        client.OnPeerJoined += id =>
        {
            Debug.unityLogger.Log(LogType.Log, TAG, $"Peer {id} se unió.");
        };

        client.OnPeerLeft += id =>
        {
            Debug.unityLogger.Log(LogType.Log, TAG, $"Peer {id} se fue.");
        };

        // --- Audio Input ---
        IAudioInput input;

        Mic.Init();

        if (Mic.AvailableDevices.Count == 0)
        {
            Debug.unityLogger.Log(LogType.Log, TAG,
                "No hay micrófono disponible. Solo se podrá escuchar, no transmitir.");
            input = new EmptyAudioInput();
            activeMicInput = null;
        }
        else
        {
            var mic = Mic.AvailableDevices[0];
            mic.StartRecording(60);
            Debug.unityLogger.Log(LogType.Log, TAG,
                $"Grabando con mic: {mic.Name}, freq: {mic.SamplingFrequency}, frame: {mic.FrameDurationMS}ms.");
            activeMicInput = new UniMicInput(mic);
            input = activeMicInput;
        }

        // --- Audio Output ---
        IAudioOutputFactory outputFactory = new TunedOutputFactory(
            playbackTargetLatency,
            playbackPitchMaxCorrection,
            playbackPitchGain,
            playbackPitchDeadzone,
            playbackDownwardPitchScale,
            playbackStartSafetyMargin
        );

        // --- Session ---
        ClientSession = new ClientSession<int>(client, input, outputFactory);

        // --- Filtros ---
        if (concentusEnabled)
        {
            EnableConcentus();
        }

        return true;
#else
        Debug.unityLogger.Log(LogType.Error, TAG, "Símbolo MIRROR no definido.");
        return false;
#endif
    }

    // ===== SELECCIÓN DE MICRÓFONO =====

    /// <summary>
    /// Cambia el micrófono activo por el dispositivo en el índice dado.
    /// El índice corresponde a <see cref="AvailableDevices"/>.
    ///
    /// Detiene la grabación del mic actual, inicia el nuevo, reconecta
    /// el UniMicInput al ClientSession, y dispara OnDeviceChanged para
    /// que MicrophoneNoiseSource se resubscriba automáticamente.
    /// </summary>
    public void SwitchMicrophone(int deviceIndex)
    {
        if (Mic.AvailableDevices == null || Mic.AvailableDevices.Count == 0)
        {
            Debug.LogWarning($"{TAG} No hay dispositivos de micrófono disponibles.");
            return;
        }

        if (deviceIndex < 0 || deviceIndex >= Mic.AvailableDevices.Count)
        {
            Debug.LogWarning($"{TAG} Índice de dispositivo fuera de rango: {deviceIndex}. " +
                             $"Disponibles: 0-{Mic.AvailableDevices.Count - 1}.");
            return;
        }

        var newDevice = Mic.AvailableDevices[deviceIndex];

        // Si es el mismo, no hacer nada.
        if (activeMicInput != null && activeMicInput.Device == newDevice)
        {
            Debug.Log($"{TAG} El mic seleccionado ya es el activo: {newDevice.Name}.");
            return;
        }

        // Detener el mic actual.
        if (activeMicInput?.Device != null && activeMicInput.Device.IsRecording)
        {
            activeMicInput.Device.StopRecording();
        }

        // Iniciar el nuevo.
        if (!newDevice.IsRecording)
        {
            newDevice.StartRecording(60);
        }

        // Si no teníamos UniMicInput (caso: empezamos sin mic y ahora hay uno),
        // crear uno nuevo y reconectar al ClientSession.
        if (activeMicInput == null)
        {
            activeMicInput = new UniMicInput(newDevice);

            if (ClientSession != null)
            {
                ClientSession.Input = activeMicInput;
            }
        }
        else
        {
            // UniMicInput.Device setter se desuscribe del viejo y suscribe al nuevo.
            activeMicInput.Device = newDevice;
        }

        Debug.Log($"{TAG} Micrófono cambiado a: {newDevice.Name}.");

        OnDeviceChanged?.Invoke(newDevice);
    }

    // ===== VOLUMEN POR PEER =====

    /// <summary>
    /// Ajusta el volumen de un peer específico (0-1).
    /// El volumen final es peerVolume * masterVolume.
    /// </summary>
    public void SetPeerVolume(int peerId, float volume)
    {
        var source = GetPeerAudioSource(peerId);

        if (source == null)
        {
            return;
        }

        source.volume = Mathf.Clamp01(volume) * masterVolume;
    }

    /// <summary>
    /// Obtiene el volumen configurado de un peer (sin multiplicar por master).
    /// </summary>
    public float GetPeerVolume(int peerId)
    {
        var source = GetPeerAudioSource(peerId);

        if (source == null)
        {
            return 0f;
        }

        // Revertir el master para devolver el volumen "puro" del peer.
        return masterVolume > 0f
            ? Mathf.Clamp01(source.volume / masterVolume)
            : 0f;
    }

    /// <summary>
    /// Ajusta el volumen maestro aplicado a todos los peers (0-1).
    /// Recalcula el volumen de cada AudioSource existente.
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        // Guardar los volúmenes individuales antes del cambio.
        var peerVolumes = new Dictionary<int, float>();

        if (ClientSession != null)
        {
            foreach (var kvp in ClientSession.PeerOutputs)
            {
                peerVolumes[kvp.Key] = GetPeerVolume(kvp.Key);
            }
        }

        masterVolume = Mathf.Clamp01(volume);

        // Reaplicar con el nuevo master.
        foreach (var kvp in peerVolumes)
        {
            SetPeerVolume(kvp.Key, kvp.Value);
        }

        Debug.Log($"{TAG} Volumen maestro: {masterVolume:F2}.");
    }

    /// <summary>
    /// Obtiene el AudioSource de un peer si existe.
    /// </summary>
    private AudioSource GetPeerAudioSource(int peerId)
    {
        if (ClientSession == null)
        {
            Debug.LogWarning($"{TAG} ClientSession no está inicializado.");
            return null;
        }

        if (!ClientSession.PeerOutputs.TryGetValue(peerId, out IAudioOutput output))
        {
            Debug.LogWarning($"{TAG} No existe output para el peer {peerId}.");
            return null;
        }

        if (output is StreamedAudioSourceOutput streamedOutput)
        {
            return streamedOutput.Stream.UnityAudioSource;
        }

        Debug.LogWarning($"{TAG} El output del peer {peerId} no es StreamedAudioSourceOutput.");
        return null;
    }

    // ===== PLAYBACK TUNING =====

    /// <summary>
    /// Factory custom que crea StreamedAudioSourceOutput con parámetros
    /// de playback configurados ANTES de que llegue cualquier audio.
    /// Esto evita que el pitch controller reaccione a un cambio de
    /// targetLatency mid-playback.
    /// </summary>
    private class TunedOutputFactory : IAudioOutputFactory
    {
        private readonly float targetLatency;
        private readonly float pitchMaxCorrection;
        private readonly float pitchGain;
        private readonly float pitchDeadzone;
        private readonly float downwardPitchScale;
        private readonly float startSafetyMargin;

        public TunedOutputFactory(
            float targetLatency,
            float pitchMaxCorrection,
            float pitchGain,
            float pitchDeadzone,
            float downwardPitchScale,
            float startSafetyMargin)
        {
            this.targetLatency = targetLatency;
            this.pitchMaxCorrection = pitchMaxCorrection;
            this.pitchGain = pitchGain;
            this.pitchDeadzone = pitchDeadzone;
            this.downwardPitchScale = downwardPitchScale;
            this.startSafetyMargin = startSafetyMargin;
        }

        public IAudioOutput Create()
        {
            var output = StreamedAudioSourceOutput.New();
            var stream = output.Stream;

            stream.TargetLatency = targetLatency;
            stream.PitchMaxCorrection = pitchMaxCorrection;
            stream.PitchProportionalGain = pitchGain;
            stream.PitchDeadZoneSec = pitchDeadzone;
            stream.DownwardPitchCorrectionScale = downwardPitchScale;
            stream.StartSafetyMarginSec = startSafetyMargin;

            Debug.Log($"[EOSVoiceChatSetup] Output creado con tuning: " +
                      $"latency={targetLatency}s, " +
                      $"pitchMax={pitchMaxCorrection}, " +
                      $"downScale={downwardPitchScale}, " +
                      $"safetyMargin={startSafetyMargin}s.");

            return output;
        }
    }

    // ===== FILTROS CONCENTUS =====

    private void EnableConcentus()
    {
        if (ClientSession == null)
        {
            return;
        }

        if (concentusEncodeFilter == null)
        {
            concentusEncodeFilter = new ConcentusEncodeFilter();
        }

        if (!ClientSession.InputFilters.Contains(concentusEncodeFilter))
        {
            ClientSession.InputFilters.Add(concentusEncodeFilter);
            Debug.unityLogger.Log(LogType.Log, TAG, "ConcentusEncodeFilter registrado.");
        }

        if (!concentusDecodeRegistered)
        {
            ClientSession.AddOutputFilter<ConcentusDecodeFilter>(() => new ConcentusDecodeFilter());
            concentusDecodeRegistered = true;
            Debug.unityLogger.Log(LogType.Log, TAG, "ConcentusDecodeFilter registrado.");
        }
    }

    private void DisableConcentus()
    {
        if (ClientSession == null)
        {
            return;
        }

        if (concentusEncodeFilter != null)
        {
            ClientSession.InputFilters.Remove(concentusEncodeFilter);
            Debug.unityLogger.Log(LogType.Log, TAG, "ConcentusEncodeFilter removido.");
        }

        if (concentusDecodeRegistered)
        {
            ClientSession.RemoveOutputFilter<ConcentusDecodeFilter>();
            concentusDecodeRegistered = false;
            Debug.unityLogger.Log(LogType.Log, TAG, "ConcentusDecodeFilter removido.");
        }
    }

    // ===== HOST INIT FIX =====
    //
    // MirrorServer internamente usa Task.Delay(100) para enviar el PEER_INIT
    // al cliente del host (connId 0). Esa continuación corre en un thread pool
    // y falla silenciosamente en la conexión local de Mirror.
    //
    // Esta coroutine detecta si el host client quedó sin inicializar
    // (ID == -1) y reenvía el PEER_INIT desde el main thread.

    private IEnumerator EnsureHostClientInitialized()
    {
        // Esperar hasta que Mirror entre en modo Host (server + client activos).
        // El coroutine anterior fallaba porque checaba una sola vez tras 0.5s,
        // y si el usuario aún no había hosteado, salía silenciosamente.
        float waitTimeout = 60f;
        float waited = 0f;

        while ((!NetworkServer.active || !NetworkClient.active) && waited < waitTimeout)
        {
            yield return null;
            waited += Time.deltaTime;
        }

        if (!NetworkServer.active || !NetworkClient.active)
        {
            Debug.Log($"{TAG} EnsureHostClientInitialized: timeout esperando Host mode ({waitTimeout}s). " +
                      "Este cliente es solo client, no host. Coroutine finalizada.");
            yield break;
        }

        Debug.Log($"{TAG} Host mode detectado. Esperando {hostInitCheckDelay}s para que llegue PEER_INIT original...");

        // Dar tiempo al PEER_INIT original (Task.Delay del MirrorServer).
        yield return new WaitForSeconds(hostInitCheckDelay);

        if (ClientSession == null)
        {
            Debug.LogWarning($"{TAG} EnsureHostClientInitialized: ClientSession es null. Abortando.");
            yield break;
        }

        int currentId = (int)(object)ClientSession.Client.ID;
        Debug.Log($"{TAG} Host client ID actual: {currentId}");

        int retries = 0;

        while (currentId == -1 && retries < hostInitMaxRetries)
        {
            Debug.LogWarning($"{TAG} Host client ID es -1 (PEER_INIT no llegó). " +
                             $"Reintento {retries + 1}/{hostInitMaxRetries}...");

            SendHostPeerInit();

            retries++;
            yield return new WaitForSeconds(hostInitRetryInterval);

            currentId = (int)(object)ClientSession.Client.ID;
        }

        if (currentId != -1)
        {
            Debug.Log($"{TAG} Host client inicializado correctamente con ID {currentId}.");
        }
        else
        {
            Debug.LogError($"{TAG} No se pudo inicializar el host client después de {hostInitMaxRetries} reintentos.");
        }
    }

    private void SendHostPeerInit()
    {
#if MIRROR
        if (!NetworkServer.connections.TryGetValue(0, out NetworkConnectionToClient conn))
        {
            Debug.LogWarning($"{TAG} No se encontró la conexión del host (connId 0) en NetworkServer.connections.");
            return;
        }

        // Obtener los IDs de los otros peers conectados (todos menos el host).
        var otherPeerIds = new List<int>();

        foreach (var kvp in NetworkServer.connections)
        {
            if (kvp.Key != 0)
            {
                otherPeerIds.Add(kvp.Key);
            }
        }

        var packet = new BytesWriter()
            .WriteString(MirrorMessageTags.PEER_INIT)
            .WriteInt(0)
            .WriteIntArray(otherPeerIds.ToArray());

        var message = new MirrorMessage
        {
            data = packet.Bytes
        };

        conn.Send(message);

        Debug.Log($"{TAG} PEER_INIT reenviado al host client (connId 0) con {otherPeerIds.Count} peer(s).");
#endif
    }
}
