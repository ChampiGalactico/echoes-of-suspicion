using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Renderiza el contenido de un ReadableData dentro de un Canvas en
/// World Space que TÚ posicionas y dimensionas manualmente.
///
/// ── Configuración ────────────────────────────────────────────────
///
///   1. Crear un hijo Canvas (Render Mode: World Space).
///   2. Ajustar su RectTransform para que cubra la hoja/nota.
///   3. Arrastrar el Canvas al campo _targetCanvas.
///   4. Asignar ReadableData y fuente TMP.
///   5. El script pone UN SOLO TextMeshProUGUI que llena el canvas.
///
/// ──────────────────────────────────────────────────────────────────
/// </summary>
[ExecuteInEditMode]
public class ReadableWorldDisplay : MonoBehaviour
{
    [Header("Datos")]
    [SerializeField] private ReadableData _readableData;

    [Header("Canvas")]
    [Tooltip("Canvas hijo en World Space. Crearlo y posicionarlo manualmente.")]
    [SerializeField] private Canvas _targetCanvas;

    [Header("Fuentes TMP")]
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

    private void Rebuild()
    {
        if (this == null) return;
        if (_targetCanvas == null || _readableData == null) return;

#if UNITY_EDITOR
        // No modificar prefab assets — solo instancias en escena.
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;
#endif

        ClearGenerated();
        CreateTextElement();
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
        // Un solo GameObject con TextMeshProUGUI que llena todo el canvas.
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

        bool isSticky = _readableData.Type == ReadableType.StickyNote;
        TMP_FontAsset font = isSticky
            ? (_handwrittenFont != null ? _handwrittenFont : _documentFont)
            : _documentFont;

        if (font != null)
            tmp.font = font;

        if (isSticky)
            tmp.alignment = TextAlignmentOptions.Center;
    }

    private string BuildFormattedText()
    {
        if (_readableData.Type == ReadableType.StickyNote)
            return _readableData.NoteText ?? "";

        // Documento: construir todo con rich text tags.
        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(_readableData.Title))
        {
            sb.Append("<b>");
            sb.Append(_readableData.Title);
            sb.AppendLine("</b>");
        }

        if (!string.IsNullOrEmpty(_readableData.Subtitle))
        {
            sb.Append("<i><color=#555555>");
            sb.Append(_readableData.Subtitle);
            sb.AppendLine("</color></i>");
        }

        if (!string.IsNullOrEmpty(_readableData.Title) ||
            !string.IsNullOrEmpty(_readableData.Subtitle))
        {
            sb.AppendLine("──────────────────");
        }

        if (!string.IsNullOrEmpty(_readableData.Content))
        {
            sb.AppendLine();

            string content = _readableData.Content;

            // Truncar para que solo se vea un preview en el mundo.
            // El jugador lee el texto completo con E (overlay).
            if (_maxPreviewChars >= 0 && content.Length > _maxPreviewChars)
            {
                content = content.Substring(0, _maxPreviewChars) + "...";
            }

            sb.Append(content);
        }

        return sb.ToString();
    }
}
