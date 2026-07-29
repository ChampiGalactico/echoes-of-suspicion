using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Renderiza el contenido de un StickyNoteData o DocumentData dentro de
/// un Canvas en World Space que TÚ posicionas y dimensionas manualmente.
///
/// ── Configuración ────────────────────────────────────────────────
///
///   1. Crear un hijo Canvas (Render Mode: World Space).
///   2. Ajustar su RectTransform para que cubra la hoja/nota.
///   3. Arrastrar el Canvas al campo _targetCanvas.
///   4. Asignar uno de los dos: _documentData o _stickyData.
///   5. El script pone UN SOLO TextMeshProUGUI que llena el canvas.
///
/// ──────────────────────────────────────────────────────────────────
/// </summary>
[ExecuteInEditMode]
public class ReadableWorldDisplay : MonoBehaviour
{
    [Header("Contenido (asignar UNO de los dos)")]
    [SerializeField] private DocumentData _documentData;
    [SerializeField] private StickyNoteData _stickyData;

    [Header("Canvas")]
    [Tooltip("Canvas hijo en World Space. Crearlo y posicionarlo manualmente.")]
    [SerializeField] private Canvas _targetCanvas;

    [Header("Fuentes TMP (fallback si el data no tiene font)")]
    [SerializeField] private TMP_FontAsset _documentFont;
    [SerializeField] private TMP_FontAsset _handwrittenFont;

    [Header("Texto")]
    [SerializeField] private float _maxFontSize = 24f;
    [SerializeField] private float _minFontSize = 18f;
    [SerializeField] private Color _textColor = new Color(0.1f, 0.1f, 0.1f);
    [SerializeField] private float _margins = 2f;

    [Header("Previsualización")]
    [Tooltip("Máximo de caracteres del contenido a mostrar en el mundo. " +
             "El resto se lee con E en el overlay. -1 = todo.")]
    [SerializeField] private int _maxPreviewChars = 80;

    private bool IsDocument => _documentData != null;
    private bool HasData => _documentData != null || _stickyData != null;

    private const string GENERATED_NAME = "__ReadableText__";

    private void Awake()
    {
        Rebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += Rebuild;
    }
#endif

    [ContextMenu("Force Rebuild")]
    private void Rebuild()
    {
        if (this == null) return;

        if (_targetCanvas == null || !HasData) return;

#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif

        ClearGenerated();
        CreateTextElement();
    }

    /// <summary>
    /// Agregar o ajustar un CanvasScaler con Dynamic Pixels Per Unit alto.
    /// Usar solo en notas donde el texto no se ve por escala pequeña.
    /// </summary>
    [ContextMenu("Fix: Add CanvasScaler (small notes)")]
    private void AddCanvasScaler()
    {
        if (_targetCanvas == null) return;

        CanvasScaler scaler = _targetCanvas.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = _targetCanvas.gameObject.AddComponent<CanvasScaler>();

        scaler.dynamicPixelsPerUnit = 100f;
        Debug.Log("[ReadableWorldDisplay] CanvasScaler agregado con dynamicPixelsPerUnit = 100.", this);
    }

    private void ClearGenerated()
    {
        if (_targetCanvas == null) return;
        for (int i = _targetCanvas.transform.childCount - 1; i >= 0; i--)
        {
            var child = _targetCanvas.transform.GetChild(i);
            if (child.name == GENERATED_NAME)
                DestroyImmediate(child.gameObject, true);
        }
    }

    private void CreateTextElement()
    {
        GameObject go = new GameObject(GENERATED_NAME);
        go.transform.SetParent(_targetCanvas.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = BuildFormattedText();
        tmp.color = _textColor;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = _minFontSize;
        tmp.fontSizeMax = _maxFontSize;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.raycastTarget = false;
        tmp.richText = true;
        tmp.margin = new Vector4(_margins, _margins, _margins, _margins);

        // Determinar fuente.
        TMP_FontAsset font = null;

        if (IsDocument)
        {
            font = _documentData.DefaultFont != null
                ? _documentData.DefaultFont
                : _documentFont;
        }
        else
        {
            font = _stickyData.NoteFont != null
                ? _stickyData.NoteFont
                : (_handwrittenFont != null ? _handwrittenFont : _documentFont);

            tmp.alignment = TextAlignmentOptions.Center;
        }

        if (font != null)
            tmp.font = font;
    }

    private string BuildFormattedText()
    {
        if (!IsDocument)
            return _stickyData.NoteText ?? "";

        // Documento: construir preview con rich text de todas las secciones.
        var sb = new System.Text.StringBuilder();
        int totalChars = 0;

        if (_documentData.Sections == null) return "";

        foreach (var section in _documentData.Sections)
        {
            if (string.IsNullOrEmpty(section.Text)) continue;

            // Aplicar estilo según SectionType.
            string styled = section.Type switch
            {
                SectionType.Title    => $"<b>{section.Text}</b>",
                SectionType.Subtitle => $"<i><color=#555555>{section.Text}</color></i>",
                SectionType.Footer   => $"<size=80%><color=#666666>{section.Text}</color></size>",
                SectionType.Caption  => $"<size=85%><color=#777777>{section.Text}</color></size>",
                _                    => section.Text,
            };

            sb.AppendLine(styled);

            if (section.ShowDivider)
                sb.AppendLine("──────────────────");

            totalChars += section.Text.Length;

            // Truncar el preview si excede el máximo.
            if (_maxPreviewChars >= 0 && totalChars >= _maxPreviewChars)
            {
                sb.Append("...");
                break;
            }
        }

        return sb.ToString();
    }
}
