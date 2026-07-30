using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modo de selección de la fuente de micrófono que observa este HUD.
/// </summary>
public enum MicrophoneWidgetTarget
{
    /// <summary>El HUD muestra el micrófono del jugador local (por defecto).</summary>
    LocalPlayer,

    /// <summary>El HUD busca el micrófono remoto del jugador con rol Runner.</summary>
    RunnerRemote
}

/// <summary>
/// Controla el medidor visual de ruido.
///
/// Por defecto (LocalPlayer): cada jugador ve el nivel de SU PROPIO
/// micrófono. La barra de Carlos sube cuando habla Carlos, y la de Carmen
/// cuando habla Carmen — independientemente del rol.
///
/// RunnerRemote se conserva para pantallas futuras que necesiten monitorear
/// el micrófono remoto del Runner (p. ej. un panel del Guía). No es el modo
/// predeterminado del HUD principal.
///
/// Nota: este cambio afecta solo a la SELECCIÓN de fuente del HUD. No toca el
/// origen de ruido de gameplay: MicrophoneNoiseSource sigue publicando el
/// ruido del Guía en la posición del Runner cuando están separados.
/// </summary>
public sealed class MicrophoneWidgetUI : MonoBehaviour
{
    [Header("Fuente del HUD")]

    [SerializeField]
    [Tooltip("LocalPlayer (por defecto): micrófono del jugador local. " +
             "RunnerRemote: micrófono del Runner remoto (pantallas especiales).")]
    private MicrophoneWidgetTarget target = MicrophoneWidgetTarget.LocalPlayer;

    [SerializeField]
    [Min(0.05f)]
    [Tooltip("Intervalo entre reintentos de búsqueda de la fuente.")]
    private float rebindInterval = 0.25f;

    [Header("Segmentos: de abajo hacia arriba")]

    [SerializeField]
    private Image[] noiseSegments = new Image[7];

    [Header("Indicador de peligro")]

    [SerializeField]
    private Image dangerThresholdLine;

    [SerializeField]
    private Color dangerLineColor =
        new(1f, 0.188f, 0.188f, 1f);

    [SerializeField]
    [Range(0f, 1f)]
    private float normalDangerLineAlpha = 0.35f;

    [SerializeField]
    [Range(0f, 1f)]
    private float minimumDangerPulseAlpha = 0.45f;

    [SerializeField]
    [Min(0.1f)]
    private float dangerPulseSpeed = 5f;

    [Header("Colores")]

    [SerializeField]
    private Color greenColor =
        new(0f, 1f, 0.333f, 1f);

    [SerializeField]
    private Color yellowColor =
        new(1f, 0.831f, 0f, 1f);

    [SerializeField]
    private Color redColor =
        new(1f, 0.188f, 0.188f, 1f);

    [Header("Transparencia")]

    [SerializeField]
    [Range(0f, 1f)]
    private float inactiveFillAlpha = 0.05f;

    [SerializeField]
    [Range(0f, 1f)]
    private float inactiveOutlineAlpha = 0.15f;

    [SerializeField]
    [Range(0f, 1f)]
    private float activeOutlineAlpha = 1f;

    [Header("Respuesta visual")]

    [SerializeField]
    [Min(0.1f)]
    private float fillResponseSpeed = 12f;

    [SerializeField]
    [Min(0.1f)]
    private float fallResponseSpeed = 5f;

    [Header("Depuración")]

    [SerializeField]
    private bool useDebugValues;

    [SerializeField]
    [Range(0f, 1f)]
    private float debugNoiseLevel;

    [SerializeField]
    private bool debugDanger;

    [SerializeField]
    [Tooltip("Logs de vinculación de la fuente. Desactivado por defecto.")]
    private bool verboseLogging = false;

    private MicrophoneNoiseSource noiseSource;

    private float displayedLevel;
    private float targetLevel;
    private bool isDangerous;
    private float rebindTimer;

    private void Awake()
    {
        displayedLevel = 0f;
        targetLevel = 0f;
        isDangerous = false;

        RefreshSegments();
        RefreshDangerLine();
    }

    private void OnEnable()
    {
        displayedLevel = 0f;
        targetLevel = 0f;
        isDangerous = false;
        rebindTimer = 0f;

        TryFindNoiseSource();

        RefreshSegments();
        RefreshDangerLine();
    }

    private void OnDisable()
    {
        UnsubscribeFromNoiseSource();

        displayedLevel = 0f;
        targetLevel = 0f;
        isDangerous = false;
    }

    private void Update()
    {
        if (useDebugValues)
        {
            targetLevel = Mathf.Clamp01(debugNoiseLevel);
            isDangerous = debugDanger;
        }
        else
        {
            /*
             * Unity considera null un componente destruido, incluso si
             * todavía existe una referencia C#. Cuando la fuente desaparece
             * (respawn, cambio de escena) reintentamos con un intervalo, sin
             * ejecutar FindObjectsByType cada frame.
             */
            if (noiseSource == null)
            {
                targetLevel = 0f;
                isDangerous = false;

                rebindTimer -= Time.unscaledDeltaTime;
                if (rebindTimer <= 0f)
                {
                    rebindTimer = rebindInterval;
                    TryFindNoiseSource();
                }
            }
        }

        float responseSpeed =
            targetLevel > displayedLevel
                ? fillResponseSpeed
                : fallResponseSpeed;

        displayedLevel =
            Mathf.MoveTowards(
                displayedLevel,
                targetLevel,
                responseSpeed * Time.unscaledDeltaTime);

        RefreshSegments();
        RefreshDangerLine();
    }

    /// <summary>
    /// Busca la fuente de micrófono que debe observar este HUD según el modo:
    ///
    /// LocalPlayer (por defecto): MicrophoneNoiseSource del jugador local, sin
    /// depender del rol. Sirve para Guide y Runner por igual.
    ///
    /// RunnerRemote: MicrophoneNoiseSource del jugador con rol Runner (para
    /// pantallas que monitorean al Runner de forma remota).
    /// </summary>
    private void TryFindNoiseSource()
    {
        if (noiseSource != null)
        {
            return;
        }

        if (target == MicrophoneWidgetTarget.LocalPlayer)
        {
            TryBindLocalPlayer();
        }
        else
        {
            TryBindRunnerRemote();
        }
    }

    private void TryBindLocalPlayer()
    {
        NetworkIdentity localPlayerIdentity = NetworkClient.localPlayer;

        // Mirror puede no haber creado todavía al jugador local: reintentamos.
        if (localPlayerIdentity == null)
        {
            return;
        }

        MicrophoneNoiseSource localNoiseSource =
            localPlayerIdentity.GetComponent<MicrophoneNoiseSource>();

        if (localNoiseSource != null)
        {
            BindToNoiseSource(localNoiseSource);
        }
    }

    private void TryBindRunnerRemote()
    {
        MicrophoneNoiseSource[] sources =
            FindObjectsByType<MicrophoneNoiseSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

        foreach (MicrophoneNoiseSource source in sources)
        {
            if (source == null)
            {
                continue;
            }

            CharacterStatsProvider stats =
                source.GetComponent<CharacterStatsProvider>();

            if (stats == null || stats.Character == null)
            {
                continue;
            }

            if (stats.Role != PlayerRole.Runner)
            {
                continue;
            }

            BindToNoiseSource(source);
            return;
        }
    }

    private void BindToNoiseSource(MicrophoneNoiseSource source)
    {
        if (source == null)
        {
            return;
        }

        // Evita suscripciones duplicadas: siempre desvincula antes de vincular.
        UnsubscribeFromNoiseSource();

        noiseSource = source;

        // Restablece nivel y peligro al cambiar de fuente.
        targetLevel = Mathf.Clamp01(noiseSource.DisplayHudLevel);
        isDangerous = noiseSource.DisplayDangerState;
        displayedLevel = targetLevel;

        noiseSource.HudLevelChanged += HandleHudLevelChanged;
        noiseSource.DangerStateChanged += HandleDangerStateChanged;

        if (verboseLogging)
        {
            Debug.Log(
                target == MicrophoneWidgetTarget.LocalPlayer
                    ? "[MicrophoneWidgetUI] Vinculado al micrófono local."
                    : "[MicrophoneWidgetUI] Vinculado al micrófono remoto del Runner.",
                this);
        }
    }

    private void UnsubscribeFromNoiseSource()
    {
        if (noiseSource == null)
        {
            return;
        }

        noiseSource.HudLevelChanged -= HandleHudLevelChanged;
        noiseSource.DangerStateChanged -= HandleDangerStateChanged;

        noiseSource = null;
    }

    private void HandleHudLevelChanged(float level)
    {
        if (useDebugValues)
        {
            return;
        }

        targetLevel = Mathf.Clamp01(level);
    }

    private void HandleDangerStateChanged(bool danger)
    {
        if (useDebugValues)
        {
            return;
        }

        isDangerous = danger;
    }

    private void RefreshSegments()
    {
        if (noiseSegments == null || noiseSegments.Length == 0)
        {
            return;
        }

        float clampedLevel = Mathf.Clamp01(displayedLevel);

        int activeSegmentCount =
            clampedLevel <= 0f
                ? 0
                : Mathf.CeilToInt(clampedLevel * noiseSegments.Length);

        for (int index = 0; index < noiseSegments.Length; index++)
        {
            Image segment = noiseSegments[index];

            if (segment == null)
            {
                continue;
            }

            bool isActive = index < activeSegmentCount;

            Color segmentColor = GetSegmentColor(index);
            Color fillColor = segmentColor;
            fillColor.a = isActive ? 1f : inactiveFillAlpha;
            segment.color = fillColor;

            Outline segmentOutline = segment.GetComponent<Outline>();

            if (segmentOutline == null)
            {
                continue;
            }

            Color outlineColor = segmentColor;
            outlineColor.a = isActive ? activeOutlineAlpha : inactiveOutlineAlpha;
            segmentOutline.effectColor = outlineColor;
        }
    }

    private void RefreshDangerLine()
    {
        if (dangerThresholdLine == null)
        {
            return;
        }

        Color lineColor = dangerLineColor;

        if (isDangerous)
        {
            float pulse =
                Mathf.PingPong(Time.unscaledTime * dangerPulseSpeed, 1f);

            lineColor.a = Mathf.Lerp(minimumDangerPulseAlpha, 1f, pulse);
        }
        else
        {
            lineColor.a = normalDangerLineAlpha;
        }

        dangerThresholdLine.color = lineColor;
    }

    private Color GetSegmentColor(int index)
    {
        if (index <= 2)
        {
            return greenColor;
        }

        if (index <= 4)
        {
            return yellowColor;
        }

        return redColor;
    }

    private void OnValidate()
    {
        inactiveFillAlpha = Mathf.Clamp01(inactiveFillAlpha);
        inactiveOutlineAlpha = Mathf.Clamp01(inactiveOutlineAlpha);
        activeOutlineAlpha = Mathf.Clamp01(activeOutlineAlpha);
        normalDangerLineAlpha = Mathf.Clamp01(normalDangerLineAlpha);
        minimumDangerPulseAlpha = Mathf.Clamp01(minimumDangerPulseAlpha);

        if (!Application.isPlaying)
        {
            displayedLevel = useDebugValues ? debugNoiseLevel : 0f;
            isDangerous = useDebugValues && debugDanger;

            RefreshSegments();
            RefreshDangerLine();
        }
    }
}
