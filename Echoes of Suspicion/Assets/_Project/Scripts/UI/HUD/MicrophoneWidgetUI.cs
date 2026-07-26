using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Representa visualmente el nivel de voz del jugador local.
///
/// - MicrophoneFill se llena de abajo hacia arriba.
/// - NoisePulseRing aparece y pulsa cuando MicrophoneNoiseSource
///   considera que la voz está generando ruido peligroso.
/// - Localiza automáticamente el MicrophoneNoiseSource del jugador local.
/// </summary>
public sealed class MicrophoneWidgetUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField]
    private Image microphoneFill;

    [SerializeField]
    private GameObject noisePulseRing;

    [SerializeField]
    private RectTransform noisePulseRingRect;

    [Header("Danger Pulse")]
    [SerializeField, Min(0.1f)]
    private float pulseSpeed = 6f;

    [SerializeField, Min(0.1f)]
    private float pulseScaleMin = 1f;

    [SerializeField, Min(0.1f)]
    private float pulseScaleMax = 1.18f;

    [Header("Source Search")]
    [SerializeField, Min(0.05f)]
    private float sourceSearchInterval = 0.25f;

    [Header("Sandbox Debug")]
    [Tooltip(
        "Actívalo únicamente para probar el widget en GuideHUDSandbox, " +
        "donde no existe todavía un jugador de red."
    )]
    [SerializeField]
    private bool useDebugValues;

    [SerializeField, Range(0f, 1f)]
    private float debugMicLevel;

    [SerializeField]
    private bool debugDanger;

    private MicrophoneNoiseSource microphoneSource;
    private Coroutine sourceSearchCoroutine;
    private bool dangerActive;

    private void OnEnable()
    {
        ResetVisuals();

        if (!useDebugValues)
        {
            StartSourceSearch();
        }
    }

    private void OnDisable()
    {
        StopSourceSearch();
        UnbindSource();
        ResetVisuals();
    }

    private void Update()
    {
        if (useDebugValues)
        {
            ApplyMicLevel(debugMicLevel);
            SetDangerState(debugDanger);
        }
        else if (microphoneSource == null)
        {
            StartSourceSearch();
        }

        AnimateDangerRing();
    }

    private void StartSourceSearch()
    {
        if (
            sourceSearchCoroutine != null ||
            useDebugValues ||
            !isActiveAndEnabled
        )
        {
            return;
        }

        sourceSearchCoroutine = StartCoroutine(SearchForSourceRoutine());
    }

    private void StopSourceSearch()
    {
        if (sourceSearchCoroutine == null)
        {
            return;
        }

        StopCoroutine(sourceSearchCoroutine);
        sourceSearchCoroutine = null;
    }

    private IEnumerator SearchForSourceRoutine()
    {
        WaitForSecondsRealtime wait =
            new WaitForSecondsRealtime(sourceSearchInterval);

        while (
            isActiveAndEnabled &&
            microphoneSource == null &&
            !useDebugValues
        )
        {
            TryBindLocalSource();

            if (microphoneSource == null)
            {
                yield return wait;
            }
        }

        sourceSearchCoroutine = null;
    }

    private void TryBindLocalSource()
    {
        MicrophoneNoiseSource[] sources =
            Object.FindObjectsByType<MicrophoneNoiseSource>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        foreach (MicrophoneNoiseSource candidate in sources)
        {
            if (candidate != null && candidate.isLocalPlayer)
            {
                BindSource(candidate);
                return;
            }
        }
    }

    private void BindSource(MicrophoneNoiseSource source)
    {
        if (microphoneSource == source)
        {
            return;
        }

        UnbindSource();

        microphoneSource = source;

        microphoneSource.HudLevelChanged += HandleHudLevelChanged;
        microphoneSource.DangerStateChanged += HandleDangerStateChanged;
        microphoneSource.MuteStateChanged += HandleMuteStateChanged;

        ApplyMicLevel(
            microphoneSource.IsMuted
                ? 0f
                : microphoneSource.CurrentHudLevel
        );

        SetDangerState(
            !microphoneSource.IsMuted &&
            microphoneSource.IsNoiseDangerous
        );

        Debug.Log(
            "[MicrophoneWidgetUI] Conectado al micrófono " +
            "del jugador local.",
            this
        );
    }

    private void UnbindSource()
    {
        if (microphoneSource == null)
        {
            return;
        }

        microphoneSource.HudLevelChanged -= HandleHudLevelChanged;
        microphoneSource.DangerStateChanged -= HandleDangerStateChanged;
        microphoneSource.MuteStateChanged -= HandleMuteStateChanged;

        microphoneSource = null;
    }

    private void HandleHudLevelChanged(float normalizedLevel)
    {
        ApplyMicLevel(normalizedLevel);
    }

    private void HandleDangerStateChanged(bool isDangerous)
    {
        SetDangerState(isDangerous);
    }

    private void HandleMuteStateChanged(bool isMuted)
    {
        if (isMuted)
        {
            ApplyMicLevel(0f);
            SetDangerState(false);
            return;
        }

        if (microphoneSource != null)
        {
            ApplyMicLevel(microphoneSource.CurrentHudLevel);
            SetDangerState(microphoneSource.IsNoiseDangerous);
        }
    }

    private void ApplyMicLevel(float normalizedLevel)
    {
        if (microphoneFill == null)
        {
            return;
        }

        microphoneFill.fillAmount = Mathf.Clamp01(normalizedLevel);
    }

    private void SetDangerState(bool isDangerous)
    {
        dangerActive = isDangerous;

        if (noisePulseRing != null)
        {
            noisePulseRing.SetActive(isDangerous);
        }

        if (!isDangerous && noisePulseRingRect != null)
        {
            noisePulseRingRect.localScale = Vector3.one;
        }
    }

    private void AnimateDangerRing()
    {
        if (!dangerActive || noisePulseRingRect == null)
        {
            return;
        }

        float pulse01 =
            (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

        float scale = Mathf.Lerp(
            pulseScaleMin,
            pulseScaleMax,
            pulse01
        );

        noisePulseRingRect.localScale =
            new Vector3(scale, scale, 1f);
    }

    private void ResetVisuals()
    {
        dangerActive = false;

        if (microphoneFill != null)
        {
            microphoneFill.fillAmount = 0f;
        }

        if (noisePulseRingRect != null)
        {
            noisePulseRingRect.localScale = Vector3.one;
        }

        if (noisePulseRing != null)
        {
            noisePulseRing.SetActive(false);
        }
    }
}