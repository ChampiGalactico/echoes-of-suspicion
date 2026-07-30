using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controlador de la UI de lectura. Muestra documentos y notas adhesivas
/// en pantalla completa con overlay oscuro. Congela al jugador mientras
/// lee y se cierra con E o ESC.
///
/// Instancia única en la escena (singleton por conveniencia).
/// Colocar en el Canvas del HUD del Runner.
///
/// ── Jerarquía esperada en el Canvas ──────────────────────────────
///
///   ReadableUI  (este script + CanvasGroup)
///   ├── Overlay              (Image, color #000000, alpha ~0.85)
///   │
///   ├── DocumentPanel        (Image, color crema #FFF8E7, centrado)
///   │   └── DocScrollContent (contenedor vertical — secciones se generan aquí)
///   │
///   └── StickyNotePanel      (Image, color amarillo, centrado)
///       ├── NoteText         (TMP_Text)
///       └── NoteImage        (Image, preserveAspect)
///
/// ──────────────────────────────────────────────────────────────────
/// </summary>
public class ReadableUI : MonoBehaviour
{
    public static ReadableUI Instance { get; private set; }

    [Header("General")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeDuration = 0.2f;

    [Header("Document Panel")]
    [SerializeField] private GameObject _documentPanel;
    [SerializeField] private Transform _docContentParent;
    [SerializeField] private Image _docImage;

    [Header("Document Defaults")]
    [SerializeField] private float _dividerHeight = 2f;

    [Header("Sticky Note Panel")]
    [SerializeField] private GameObject _stickyNotePanel;
    [SerializeField] private Image _stickyNoteBackground;
    [SerializeField] private TMP_Text _noteText;
    [SerializeField] private Image _noteImage;
    [SerializeField] private float _stickyTiltAngle = 2f;

    [Header("Sticky Note Colors")]
    [SerializeField] private Color _yellowColor = new Color(1f, 0.95f, 0.6f);
    [SerializeField] private Color _pinkColor = new Color(1f, 0.7f, 0.78f);
    [SerializeField] private Color _blueColor = new Color(0.7f, 0.85f, 1f);
    [SerializeField] private Color _greenColor = new Color(0.7f, 1f, 0.75f);

    public bool IsOpen { get; private set; }

    // Cache de componentes del jugador para congelar/descongelar.
    private NetworkPlayerMovement _cachedMovement;
    private NetworkFirstPersonView _cachedView;
    private NetworkRatInteractor _cachedInteractor;

    // Fade
    private float _fadeTarget;

    // Prevenir que la misma pulsación de E que abre también cierre.
    private bool _justOpened;

    // Elementos generados dinámicamente para documentos.
    private readonly List<GameObject> _generatedElements = new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Hide(immediate: true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Fade suave.
        if (!Mathf.Approximately(_canvasGroup.alpha, _fadeTarget))
        {
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha,
                _fadeTarget,
                Time.unscaledDeltaTime / Mathf.Max(_fadeDuration, 0.01f));

            if (_canvasGroup.alpha <= 0f && _fadeTarget <= 0f)
                _canvasGroup.gameObject.SetActive(false);
        }

        if (!IsOpen) return;

        if (_justOpened)
        {
            _justOpened = false;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
        {
            Hide();
        }
    }

    // ─── API Pública ───────────────────────────────────────────────

    public void ShowDocument(DocumentData data)
    {
        if (data == null || IsOpen) return;

        IsOpen = true;
        _justOpened = true;

        _documentPanel.SetActive(true);
        _stickyNotePanel.SetActive(false);

        PopulateDocument(data);
        FadeIn();
        FreezePlayer();
    }

    public void ShowStickyNote(StickyNoteData data)
    {
        if (data == null || IsOpen) return;

        IsOpen = true;
        _justOpened = true;

        _documentPanel.SetActive(false);
        _stickyNotePanel.SetActive(true);

        PopulateStickyNote(data);
        FadeIn();
        FreezePlayer();
    }

    public void Hide(bool immediate = false)
    {
        if (!IsOpen && !immediate) return;

        IsOpen = false;
        _fadeTarget = 0f;
        _canvasGroup.blocksRaycasts = false;

        if (immediate)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.gameObject.SetActive(false);
        }

        UnfreezePlayer();
    }

    // ─── Poblar contenido ──────────────────────────────────────────

    private void PopulateDocument(DocumentData data)
    {
        ClearGeneratedElements();

        // Quitar ContentSizeFitter si existe (impide que funcione el vertical alignment).
        ContentSizeFitter fitter = _docContentParent.GetComponent<ContentSizeFitter>();
        if (fitter != null) Destroy(fitter);

        // Alineación vertical del contenido dentro del panel.
        VerticalLayoutGroup layout = _docContentParent.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childAlignment = data.VerticalAlignment switch
            {
                DocumentVerticalAlignment.Top    => TextAnchor.UpperLeft,
                DocumentVerticalAlignment.Center => TextAnchor.MiddleLeft,
                DocumentVerticalAlignment.Bottom => TextAnchor.LowerLeft,
                _                                => TextAnchor.UpperLeft,
            };
        }

        if (data.Sections != null)
        {
            // Separar secciones normales de las ancladas al fondo.
            foreach (var section in data.Sections)
            {
                if (string.IsNullOrEmpty(section.Text)) continue;

                if (section.AnchorToBottom)
                {
                    // Se crea como hijo directo del panel, anclado abajo.
                    GameObject bottomGo = CreateBottomAnchoredElement(
                        section.Text,
                        section.EffectiveFontSize,
                        section.Font != null ? section.Font : data.DefaultFont,
                        section.Alignment);

                    _generatedElements.Add(bottomGo);
                    continue;
                }

                GameObject textGo = CreateTextElement(
                    section.Text,
                    section.EffectiveFontSize,
                    section.Font != null ? section.Font : data.DefaultFont,
                    section.Alignment);

                _generatedElements.Add(textGo);

                if (section.ShowDivider)
                {
                    GameObject divider = CreateDivider(section.DividerColor);
                    _generatedElements.Add(divider);
                }
            }
        }

        bool hasImage = data.ContentImage != null;
        _docImage.gameObject.SetActive(hasImage);
        if (hasImage) _docImage.sprite = data.ContentImage;
    }

    private void PopulateStickyNote(StickyNoteData data)
    {
        _noteText.text = data.NoteText;

        if (data.FontSize > 0f)
            _noteText.fontSize = data.FontSize;

        if (data.NoteFont != null)
            _noteText.font = data.NoteFont;

        _stickyNoteBackground.color = data.StickyColor switch
        {
            NoteColor.Yellow => _yellowColor,
            NoteColor.Pink   => _pinkColor,
            NoteColor.Blue   => _blueColor,
            NoteColor.Green  => _greenColor,
            _                => _yellowColor,
        };

        float tilt = Random.Range(-_stickyTiltAngle, _stickyTiltAngle);
        _stickyNotePanel.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        bool hasImage = data.NoteImage != null;
        _noteImage.gameObject.SetActive(hasImage);
        if (hasImage) _noteImage.sprite = data.NoteImage;
    }

    // ─── Generación dinámica de elementos ──────────────────────────

    private GameObject CreateTextElement(
        string text, float fontSize, TMP_FontAsset font,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        GameObject go = new GameObject("Section", typeof(RectTransform));
        go.transform.SetParent(_docContentParent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = false;
        tmp.color = Color.black;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;

        if (font != null)
            tmp.font = font;

        // LayoutElement para que el VerticalLayoutGroup lo dimensione.
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = -1; // Auto-calculado por ContentSizeFitter.
        le.flexibleWidth = 1f;

        // ContentSizeFitter para que el alto se ajuste al texto.
        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go;
    }

    private GameObject CreateDivider(Color color)
    {
        GameObject go = new GameObject("Divider", typeof(RectTransform));
        go.transform.SetParent(_docContentParent, false);

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = _dividerHeight;
        le.flexibleWidth = 1f;

        return go;
    }

    private GameObject CreateBottomAnchoredElement(
        string text, float fontSize, TMP_FontAsset font,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("BottomAnchored", typeof(RectTransform));
        go.transform.SetParent(_documentPanel.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(30f, 15f);
        rt.offsetMax = new Vector2(-30f, 55f);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = false;
        tmp.color = Color.black;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.richText = true;

        if (font != null) tmp.font = font;

        return go;
    }

    private void ClearGeneratedElements()
    {
        foreach (var go in _generatedElements)
        {
            if (go != null) Destroy(go);
        }

        _generatedElements.Clear();
    }

    // ─── Helpers ───────────────────────────────────────────────────

    private void FadeIn()
    {
        _canvasGroup.gameObject.SetActive(true);
        _fadeTarget = 1f;
        _canvasGroup.blocksRaycasts = true;
    }

    // ─── Congelar / descongelar jugador ────────────────────────────

    private void FreezePlayer()
    {
        CachePlayerComponents();

        if (_cachedMovement != null) _cachedMovement.enabled = false;
        if (_cachedView != null) _cachedView.enabled = false;
        if (_cachedInteractor != null) _cachedInteractor.enabled = false;
    }

    private void UnfreezePlayer()
    {
        if (_cachedMovement != null) _cachedMovement.enabled = true;
        if (_cachedView != null) _cachedView.enabled = true;
        if (_cachedInteractor != null) _cachedInteractor.enabled = true;
    }

    private void CachePlayerComponents()
    {
        if (_cachedMovement != null) return;

        var localPlayer = Mirror.NetworkClient.localPlayer;
        if (localPlayer == null) return;

        _cachedMovement = localPlayer.GetComponent<NetworkPlayerMovement>();
        _cachedView = localPlayer.GetComponent<NetworkFirstPersonView>();
        _cachedInteractor = localPlayer.GetComponent<NetworkRatInteractor>();
    }
}
