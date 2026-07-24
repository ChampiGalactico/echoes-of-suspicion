using UnityEngine;
using Adrenak.UniMic;
using Adrenak.UniVoice.Samples;

/// <summary>
/// Manager custom de voice chat para Echoes of Suspicion.
///
/// NO hereda del sample — coexiste con él en el mismo GameObject.
/// El sample hace el setup pesado de UniVoice + Mirror.
/// Este manager expone una API limpia (Mute/Unmute/Toggle) para el resto del juego.
///
/// USO desde otros scripts:
///     EOSVoiceManager.Instance.Mute();
///     EOSVoiceManager.Instance.Unmute();
///     EOSVoiceManager.Instance.ToggleMute();
///
/// REQUIERE: el GameObject debe tener también un UniVoiceMirrorSetupSample.
/// </summary>
[RequireComponent(typeof(UniVoiceMirrorSetupSample))]
public sealed class EOSVoiceManager : MonoBehaviour
{
    // ===== SINGLETON =====

    public static EOSVoiceManager Instance { get; private set; }

    // ===== ESTADO =====

    /// <summary>
    /// True si el jugador local está muteado (no transmite voz).
    /// </summary>
    public bool IsMuted { get; private set; }

    // ===== CICLO DE VIDA =====

    private void Awake()
    {
        Debug.Log("[EOSVoiceManager] Awake ejecutado.");

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[EOSVoiceManager] Ya existe una instancia. Destruyendo la duplicada.");
            Destroy(this);
            return;
        }

        Instance = this;
        Debug.Log("[EOSVoiceManager] Instance asignada correctamente.");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ===== API PÚBLICA =====

    public void Mute() => SetMuted(true);
    public void Unmute() => SetMuted(false);
    public void ToggleMute() => SetMuted(!IsMuted);

    // ===== IMPLEMENTACIÓN INTERNA =====

    private void SetMuted(bool muted)
    {
        IsMuted = muted;

        var devices = Mic.AvailableDevices;

        if (devices == null || devices.Count == 0)
        {
            Debug.LogWarning("[EOSVoiceManager] No se encontraron dispositivos de mic disponibles.");
            return;
        }

        var device = devices[0];

        if (muted)
        {
            if (device.IsRecording)
            {
                device.StopRecording();
                Debug.Log($"[EOSVoiceManager] 🎙️ Muteado. Device: {device.Name}");
            }
        }
        else
        {
            if (!device.IsRecording)
            {
                device.StartRecording(frameDurationMS: 60);
                Debug.Log($"[EOSVoiceManager] 🎙️ Desmuteado. Device: {device.Name}");
            }
        }
    }
}