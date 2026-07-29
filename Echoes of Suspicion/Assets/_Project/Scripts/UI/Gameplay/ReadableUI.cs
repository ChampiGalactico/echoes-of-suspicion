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
///   ├── DocumentPanel        (Image, color crema #FFF8E7, centrado, ~60% pantalla)
///   │   ├── DocTitle         (TMP_Text — 28pt, bold)
///   │   ├── DocSubtitle      (TMP_Text — 18pt, italic, color gris)
///   │   ├── DocSeparator     (Image, height 2px, color gris oscuro)
///   │   ├── DocContent       (TMP_Text — 16pt, line spacing 1.4)
///   │   └── DocImage         (Image — preserveAspect, desactivado por defecto)
///   │
///   └── StickyNotePanel      (Image, color amarillo, centrado, ~35% pantalla)
///       ├── NoteText         (TMP_Text — 20pt, handwritten font, centrado)
///       └── NoteImage        (Image — preserveAspect, desactivado por defecto)
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
    [SerializeField] private TMP_Text _docTitle;
    [SerializeField] private TMP_Text _docSubtitle;
    [SerializeField] private GameObject _docSeparator;
    [SerializeField] private TMP_Text _docContent;
    [SerializeField] private Image _docImage;

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
    private float _fadeVelocity;

    // Prevenir que la misma pulsación de E que abre también cierre.
    private bool _justOpened;

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

            // Desactivar el GO al terminar el fade-out para ahorrar draw calls.
            if (_canvasGroup.alpha <= 0f && _fadeTarget <= 0f)
                _canvasGroup.gameObject.SetActive(false);
        }

        if (!IsOpen) return;

        // El primer frame después de abrir se ignora para que la E
        // que abrió la UI no la cierre inmediatamente.
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

    public void Show(ReadableData data)
    {
        if (data == null || IsOpen) return;

        IsOpen = true;
        _justOpened = true;

        // Mostrar el panel correcto.
        bool isDocument = data.Type == ReadableType.Document;
        _documentPanel.SetActive(isDocument);
        _stickyNotePanel.SetActive(!isDocument);

        if (isDocument)
            PopulateDocument(data);
        else
            PopulateStickyNote(data);

        // Fade in.
        _canvasGroup.gameObject.SetActive(true);
        _fadeTarget = 1f;
        _canvasGroup.blocksRaycasts = true;

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

    private void PopulateDocument(ReadableData data)
    {
        _docTitle.text = data.Title;

        bool hasSubtitle = !string.IsNullOrEmpty(data.Subtitle);
        _docSubtitle.gameObject.SetActive(hasSubtitle);
        if (hasSubtitle) _docSubtitle.text = data.Subtitle;

        _docSeparator.SetActive(hasSubtitle);

        _docContent.text = data.Content;

        bool hasImage = data.ContentImage != null;
        _docImage.gameObject.SetActive(hasImage);
        if (hasImage) _docImage.sprite = data.ContentImage;
    }

    private void PopulateStickyNote(ReadableData data)
    {
        _noteText.text = data.NoteText;

        // Color de la nota.
        _stickyNoteBackground.color = data.StickyColor switch
        {
            NoteColor.Yellow => _yellowColor,
            NoteColor.Pink => _pinkColor,
            NoteColor.Blue => _blueColor,
            NoteColor.Green => _greenColor,
            _ => _yellowColor,
        };

        // Inclinación casual.
        float tilt = Random.Range(-_stickyTiltAngle, _stickyTiltAngle);
        _stickyNotePanel.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // Imagen opcional.
        bool hasImage = data.NoteImage != null;
        _noteImage.gameObject.SetActive(hasImage);
        if (hasImage) _noteImage.sprite = data.NoteImage;
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
        // Solo buscar si no están cacheados o si el objeto fue destruido.
        if (_cachedMovement != null) return;

        // Buscar el jugador local. En Mirror, el localPlayer es único.
        var localPlayer = Mirror.NetworkClient.localPlayer;
        if (localPlayer == null) return;

        _cachedMovement = localPlayer.GetComponent<NetworkPlayerMovement>();
        _cachedView = localPlayer.GetComponent<NetworkFirstPersonView>();
        _cachedInteractor = localPlayer.GetComponent<NetworkRatInteractor>();
    }
}
