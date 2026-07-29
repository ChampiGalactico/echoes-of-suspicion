using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla el medidor visual de ruido.
///
/// Runner:
/// muestra el nivel de su propio micrófono.
///
/// Guide:
/// muestra por red el nivel del micrófono del Runner.
///
/// El objeto MicrophoneNoiseSource se busca después de que Mirror haya
/// creado al jugador local y sincronizado los roles.
/// </summary>
public sealed class MicrophoneWidgetUI : MonoBehaviour
{
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

    private MicrophoneNoiseSource noiseSource;

    private float displayedLevel;
    private float targetLevel;
    private bool isDangerous;

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
            targetLevel =
                Mathf.Clamp01(debugNoiseLevel);

            isDangerous =
                debugDanger;
        }
        else
        {
            /*
             * Unity considera null un componente destruido,
             * incluso si todavía existe una referencia C#.
             */
            if (noiseSource == null)
            {
                targetLevel = 0f;
                isDangerous = false;

                TryFindNoiseSource();
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
                responseSpeed *
                Time.unscaledDeltaTime
            );

        RefreshSegments();
        RefreshDangerLine();
    }

    /// <summary>
    /// Busca la fuente de micrófono que debe observar este HUD.
    ///
    /// Runner:
    /// utiliza MicrophoneNoiseSource del jugador local.
    ///
    /// Guide:
    /// busca MicrophoneNoiseSource del jugador con rol Runner.
    /// </summary>
    private void TryFindNoiseSource()
    {
        if (noiseSource != null)
        {
            return;
        }

        NetworkIdentity localPlayerIdentity =
            NetworkClient.localPlayer;

        if (localPlayerIdentity == null)
        {
            return;
        }

        CharacterStatsProvider localStatsProvider =
            localPlayerIdentity.GetComponent<CharacterStatsProvider>();

        if (localStatsProvider == null)
        {
            return;
        }

        /*
         * Espera hasta que Mirror haya sincronizado
         * el personaje y su rol.
         */
        if (localStatsProvider.Character == null)
        {
            return;
        }

        if (localStatsProvider.Role == PlayerRole.Runner)
        {
            MicrophoneNoiseSource localNoiseSource =
                localPlayerIdentity.GetComponent<MicrophoneNoiseSource>();

            if (localNoiseSource != null)
            {
                BindToNoiseSource(localNoiseSource);
            }

            return;
        }

        /*
         * El jugador local es Guide.
         * Debemos encontrar el micrófono del Runner remoto.
         */
        MicrophoneNoiseSource[] sources =
            FindObjectsByType<MicrophoneNoiseSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        foreach (MicrophoneNoiseSource source in sources)
        {
            if (source == null)
            {
                continue;
            }

            CharacterStatsProvider sourceStatsProvider =
                source.GetComponent<CharacterStatsProvider>();

            if (sourceStatsProvider == null)
            {
                continue;
            }

            if (sourceStatsProvider.Character == null)
            {
                continue;
            }

            if (sourceStatsProvider.Role != PlayerRole.Runner)
            {
                continue;
            }

            BindToNoiseSource(source);
            return;
        }
    }

    private void BindToNoiseSource(
        MicrophoneNoiseSource source
    )
    {
        if (source == null)
        {
            return;
        }

        UnsubscribeFromNoiseSource();

        noiseSource = source;

        /*
         * DisplayHudLevel y DisplayDangerState escogen
         * automáticamente entre:
         *
         * - Valor local inmediato para Carmen.
         * - Valor sincronizado por Mirror para Carlos.
         */
        targetLevel =
            Mathf.Clamp01(
                noiseSource.DisplayHudLevel
            );

        isDangerous =
            noiseSource.DisplayDangerState;

        noiseSource.HudLevelChanged +=
            HandleHudLevelChanged;

        noiseSource.DangerStateChanged +=
            HandleDangerStateChanged;

        Debug.Log(
            noiseSource.isLocalPlayer
                ? "[MicrophoneWidgetUI] Vinculado al micrófono local."
                : "[MicrophoneWidgetUI] Vinculado al micrófono remoto del Runner.",
            this
        );
    }

    private void UnsubscribeFromNoiseSource()
    {
        if (noiseSource == null)
        {
            noiseSource = null;
            return;
        }

        noiseSource.HudLevelChanged -=
            HandleHudLevelChanged;

        noiseSource.DangerStateChanged -=
            HandleDangerStateChanged;

        noiseSource = null;
    }

    private void HandleHudLevelChanged(
        float level
    )
    {
        if (useDebugValues)
        {
            return;
        }

        targetLevel =
            Mathf.Clamp01(level);
    }

    private void HandleDangerStateChanged(
        bool danger
    )
    {
        if (useDebugValues)
        {
            return;
        }

        isDangerous = danger;
    }

    private void RefreshSegments()
    {
        if (
            noiseSegments == null ||
            noiseSegments.Length == 0
        )
        {
            return;
        }

        float clampedLevel =
            Mathf.Clamp01(displayedLevel);

        int activeSegmentCount =
            clampedLevel <= 0f
                ? 0
                : Mathf.CeilToInt(
                    clampedLevel *
                    noiseSegments.Length
                );

        for (
            int index = 0;
            index < noiseSegments.Length;
            index++
        )
        {
            Image segment =
                noiseSegments[index];

            if (segment == null)
            {
                continue;
            }

            bool isActive =
                index < activeSegmentCount;

            Color segmentColor =
                GetSegmentColor(index);

            Color fillColor =
                segmentColor;

            fillColor.a =
                isActive
                    ? 1f
                    : inactiveFillAlpha;

            segment.color =
                fillColor;

            Outline segmentOutline =
                segment.GetComponent<Outline>();

            if (segmentOutline == null)
            {
                continue;
            }

            Color outlineColor =
                segmentColor;

            outlineColor.a =
                isActive
                    ? activeOutlineAlpha
                    : inactiveOutlineAlpha;

            segmentOutline.effectColor =
                outlineColor;
        }
    }

    private void RefreshDangerLine()
    {
        if (dangerThresholdLine == null)
        {
            return;
        }

        Color lineColor =
            dangerLineColor;

        if (isDangerous)
        {
            float pulse =
                Mathf.PingPong(
                    Time.unscaledTime *
                    dangerPulseSpeed,
                    1f
                );

            lineColor.a =
                Mathf.Lerp(
                    minimumDangerPulseAlpha,
                    1f,
                    pulse
                );
        }
        else
        {
            lineColor.a =
                normalDangerLineAlpha;
        }

        dangerThresholdLine.color =
            lineColor;
    }

    private Color GetSegmentColor(
        int index
    )
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
        inactiveFillAlpha =
            Mathf.Clamp01(inactiveFillAlpha);

        inactiveOutlineAlpha =
            Mathf.Clamp01(inactiveOutlineAlpha);

        activeOutlineAlpha =
            Mathf.Clamp01(activeOutlineAlpha);

        normalDangerLineAlpha =
            Mathf.Clamp01(normalDangerLineAlpha);

        minimumDangerPulseAlpha =
            Mathf.Clamp01(
                minimumDangerPulseAlpha
            );

        if (!Application.isPlaying)
        {
            displayedLevel =
                useDebugValues
                    ? debugNoiseLevel
                    : 0f;

            isDangerous =
                useDebugValues &&
                debugDanger;

            RefreshSegments();
            RefreshDangerLine();
        }
    }
}