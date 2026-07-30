using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Editor para StickyNoteData que dibuja un preview visual
/// de la nota adhesiva en el Inspector sin necesidad de correr el juego.
/// </summary>
[CustomEditor(typeof(StickyNoteData))]
public class StickyNoteDataEditor : Editor
{
    private bool _showPreview = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        _showPreview = EditorGUILayout.Foldout(_showPreview, "Preview", true,
            EditorStyles.foldoutHeader);

        if (!_showPreview) return;

        StickyNoteData data = (StickyNoteData)target;

        if (string.IsNullOrEmpty(data.NoteText))
        {
            EditorGUILayout.HelpBox("NoteText está vacío.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);

        // Tamaño del preview (cuadrado-ish como una sticky note).
        float size = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, 300f);
        Rect noteRect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(true));

        // Color de fondo según el enum.
        Color bgColor = data.StickyColor switch
        {
            NoteColor.Yellow => new Color(1f, 0.95f, 0.6f),
            NoteColor.Pink   => new Color(1f, 0.7f, 0.78f),
            NoteColor.Blue   => new Color(0.7f, 0.85f, 1f),
            NoteColor.Green  => new Color(0.7f, 1f, 0.75f),
            _                => new Color(1f, 0.95f, 0.6f),
        };

        EditorGUI.DrawRect(noteRect, bgColor);
        Handles.DrawSolidRectangleWithOutline(noteRect, Color.clear,
            new Color(0.5f, 0.5f, 0.5f, 0.5f));

        // Texto centrado.
        float padding = 15f;
        Rect textRect = new Rect(
            noteRect.x + padding,
            noteRect.y + padding,
            noteRect.width - padding * 2f,
            noteRect.height - padding * 2f);

        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            richText = true,
            alignment = TextAnchor.MiddleCenter,
            fontSize = data.FontSize > 0f
                ? Mathf.RoundToInt(data.FontSize * 0.65f)
                : 14,
            normal = { textColor = new Color(0.15f, 0.15f, 0.15f) },
        };

        // Font hint.
        string fontName = data.NoteFont != null ? data.NoteFont.name : "Default";

        EditorGUI.LabelField(textRect, data.NoteText, style);

        // Imagen.
        if (data.NoteImage != null)
        {
            Rect imgRect = new Rect(
                noteRect.x + noteRect.width * 0.3f,
                noteRect.yMax - 60f,
                noteRect.width * 0.4f,
                50f);

            Texture2D tex = AssetPreview.GetAssetPreview(data.NoteImage);
            if (tex != null)
                GUI.DrawTexture(imgRect, tex, ScaleMode.ScaleToFit);
        }

        // Info de fuente.
        string sizeInfo = data.FontSize > 0f ? $"{data.FontSize}pt" : "Default";
        EditorGUILayout.LabelField($"Font: {fontName}  |  Size: {sizeInfo}", EditorStyles.miniLabel);
    }
}
