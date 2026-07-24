using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class OptionsMenuController : MonoBehaviour
{
    private const string VolumeKey = "settings.masterVolume";
    private const string SensitivityKey = "settings.mouseSensitivity";

    private const float DefaultVolume = 0.8f;
    private const float DefaultSensitivity = 1f;

    [Header("Volumen")]
    [SerializeField]
    private Slider volumeSlider;

    [SerializeField]
    private TMP_Text volumeValueText;

    [Header("Sensibilidad")]
    [SerializeField]
    private Slider sensitivitySlider;

    [SerializeField]
    private TMP_Text sensitivityValueText;

    [Header("Prueba de audio")]
    [SerializeField]
    private AudioSource testAudioSource;

    private AudioClip testAudioClip;

    public static float MouseSensitivityMultiplier
    {
        get;
        private set;
    } = DefaultSensitivity;

    private void Awake()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        LoadSettings();
        CreateTestAudioClip();
    }

    private void OnEnable()
    {
        if (volumeSlider == null || sensitivitySlider == null)
        {
            return;
        }

        volumeSlider.onValueChanged.AddListener(SetVolume);
        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
    }

    private void OnDisable()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(SetVolume);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveListener(SetSensitivity);
        }
    }

    private void OnDestroy()
    {
        if (testAudioClip != null)
        {
            Destroy(testAudioClip);
        }
    }

    private bool ValidateReferences()
    {
        bool interfaceReferencesAreValid =
            volumeSlider != null &&
            volumeValueText != null &&
            sensitivitySlider != null &&
            sensitivityValueText != null;

        if (!interfaceReferencesAreValid)
        {
            Debug.LogError(
                "OptionsMenuController: faltan referencias de la interfaz.",
                this
            );

            return false;
        }

        if (testAudioSource == null)
        {
            Debug.LogWarning(
                "OptionsMenuController: falta asignar el AudioSource de prueba.",
                this
            );
        }

        return true;
    }

    private void LoadSettings()
    {
        float savedVolume = PlayerPrefs.GetFloat(
            VolumeKey,
            DefaultVolume
        );

        float savedSensitivity = PlayerPrefs.GetFloat(
            SensitivityKey,
            DefaultSensitivity
        );

        volumeSlider.SetValueWithoutNotify(savedVolume);
        sensitivitySlider.SetValueWithoutNotify(savedSensitivity);

        ApplyVolume(savedVolume, saveValue: false);
        ApplySensitivity(savedSensitivity, saveValue: false);
    }

    public void SetVolume(float value)
    {
        ApplyVolume(value, saveValue: true);
    }

    public void SetSensitivity(float value)
    {
        ApplySensitivity(value, saveValue: true);
    }

    public void PlayTestAudio()
    {
        if (testAudioSource == null || testAudioClip == null)
        {
            Debug.LogWarning(
                "OptionsMenuController: no se pudo reproducir la prueba de audio.",
                this
            );

            return;
        }

        testAudioSource.Stop();
        testAudioSource.PlayOneShot(testAudioClip);
    }

    private void ApplyVolume(float value, bool saveValue)
    {
        float normalizedValue = Mathf.Clamp01(value);

        AudioListener.volume = normalizedValue;

        volumeValueText.text =
            $"{Mathf.RoundToInt(normalizedValue * 100f)}%";

        if (!saveValue)
        {
            return;
        }

        PlayerPrefs.SetFloat(VolumeKey, normalizedValue);
        PlayerPrefs.Save();
    }

    private void ApplySensitivity(float value, bool saveValue)
    {
        float clampedValue = Mathf.Clamp(value, 0.1f, 3f);

        MouseSensitivityMultiplier = clampedValue;
        sensitivityValueText.text = clampedValue.ToString("0.00");

        if (!saveValue)
        {
            return;
        }

        PlayerPrefs.SetFloat(SensitivityKey, clampedValue);
        PlayerPrefs.Save();
    }

    private void CreateTestAudioClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.25f;
        const float frequency = 660f;
        const float amplitude = 0.25f;

        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float progress = (float)i / sampleCount;

            // Disminuye progresivamente el volumen para evitar un corte brusco.
            float fadeOut = 1f - progress;

            samples[i] =
                Mathf.Sin(2f * Mathf.PI * frequency * time) *
                amplitude *
                fadeOut;
        }

        testAudioClip = AudioClip.Create(
            "OptionsTestTone",
            sampleCount,
            1,
            sampleRate,
            false
        );

        testAudioClip.SetData(samples, 0);
    }
}