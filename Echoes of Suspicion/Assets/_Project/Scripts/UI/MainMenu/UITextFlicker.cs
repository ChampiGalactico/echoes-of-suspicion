using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
[RequireComponent(typeof(RectTransform))]
public sealed class UITextFlicker : MonoBehaviour
{
    [Header("Opacidad")]
    [SerializeField, Range(0f, 1f)]
    private float minimumAlpha = 0.85f;

    [SerializeField, Range(0f, 1f)]
    private float maximumAlpha = 1f;

    [SerializeField, Min(0.1f)]
    private float flickerSpeed = 6f;

    [Header("Fallo ocasional")]
    [SerializeField, Min(0.1f)]
    private float glitchIntervalMin = 2f;

    [SerializeField, Min(0.1f)]
    private float glitchIntervalMax = 6f;

    [SerializeField, Range(0f, 1f)]
    private float glitchAlpha = 0.25f;

    [SerializeField, Min(0.01f)]
    private float glitchDuration = 0.06f;

    [Header("Respiración de escala")]
    [SerializeField, Range(0f, 0.1f)]
    private float scaleAmount = 0.01f;

    [SerializeField, Min(0.1f)]
    private float scaleSpeed = 1.5f;

    [Header("Flotación")]
    [SerializeField, Min(0f)]
    private float floatAmount = 8f;

    [SerializeField, Min(0.1f)]
    private float floatSpeed = 1.2f;

    [SerializeField, Min(0f)]
    private float horizontalFloatAmount = 0f;

    private TMP_Text textComponent;
    private RectTransform rectTransform;

    private Color originalColor;
    private Vector3 originalScale;
    private Vector2 originalAnchoredPosition;

    private float nextGlitchTime;
    private float glitchEndTime;
    private bool isGlitching;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
        rectTransform = GetComponent<RectTransform>();

        originalColor = textComponent.color;
        originalScale = rectTransform.localScale;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        ScheduleNextGlitch();
    }

    private void Update()
    {
        float currentTime = Time.unscaledTime;

        UpdateGlitch(currentTime);
        UpdateAlpha(currentTime);
        UpdateScale(currentTime);
        UpdateFloating(currentTime);
    }

    private void UpdateGlitch(float currentTime)
    {
        if (!isGlitching && currentTime >= nextGlitchTime)
        {
            isGlitching = true;
            glitchEndTime = currentTime + glitchDuration;
        }

        if (isGlitching && currentTime >= glitchEndTime)
        {
            isGlitching = false;
            ScheduleNextGlitch();
        }
    }

    private void UpdateAlpha(float currentTime)
    {
        float alpha;

        if (isGlitching)
        {
            alpha = glitchAlpha;
        }
        else
        {
            float wave = (Mathf.Sin(currentTime * flickerSpeed) + 1f) * 0.5f;
            alpha = Mathf.Lerp(minimumAlpha, maximumAlpha, wave);
        }

        Color color = originalColor;
        color.a = alpha;
        textComponent.color = color;
    }

    private void UpdateScale(float currentTime)
    {
        float wave = Mathf.Sin(currentTime * scaleSpeed) * scaleAmount;
        rectTransform.localScale = originalScale * (1f + wave);
    }

    private void UpdateFloating(float currentTime)
    {
        float verticalOffset =
            Mathf.Sin(currentTime * floatSpeed) * floatAmount;

        float horizontalOffset =
            Mathf.Cos(currentTime * floatSpeed * 0.7f) * horizontalFloatAmount;

        rectTransform.anchoredPosition =
            originalAnchoredPosition +
            new Vector2(horizontalOffset, verticalOffset);
    }

    private void ScheduleNextGlitch()
    {
        float minimum = Mathf.Min(glitchIntervalMin, glitchIntervalMax);
        float maximum = Mathf.Max(glitchIntervalMin, glitchIntervalMax);

        nextGlitchTime =
            Time.unscaledTime + Random.Range(minimum, maximum);
    }

    private void OnDisable()
    {
        if (textComponent != null)
        {
            textComponent.color = originalColor;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale;
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }
}