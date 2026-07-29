using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Custom Editor para DocumentData que dibuja un preview visual
/// del documento en el Inspector sin necesidad de correr el juego.
/// </summary>
[CustomEditor(typeof(DocumentData))]
public class DocumentDataEditor : Editor
{
    private bool _showPreview = true;
    private Vector2 _scrollPos;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        _showPreview = EditorGUILayout.Foldout(_showPreview, "Preview", true,
            EditorStyles.foldoutHeader);

        if (!_showPreview) return;

        DocumentData data = (DocumentData)target;

        if (data.Sections == null || data.Sections.Length == 0)
        {
            EditorGUILayout.HelpBox("No hay secciones configuradas.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);

        float padding = 20f;
        float viewWidth = EditorGUIUtility.currentViewWidth - 40f;
        float contentWidth = viewWidth - padding * 2f;

        // Calcular alto real del contenido.
        float totalHeight = CalculateTotalHeight(data, contentWidth) + padding * 2f;

        // Imagen extra.
        if (data.ContentImage != null)
            totalHeight += 90f;

        // Scroll view con altura máxima de 500px.
        float viewHeight = Mathf.Min(totalHeight, 500f);
        _scrollPos = EditorGUILayout.BeginScrollView(
            _scrollPos, GUILayout.Height(viewHeight));

        // Reservar rect del tamaño real del contenido.
        Rect previewRect = GUILayoutUtility.GetRect(viewWidth, totalHeight);

        // Fondo crema.
        EditorGUI.DrawRect(previewRect, new Color(1f, 0.973f, 0.906f));
        Handles.DrawSolidRectangleWithOutline(previewRect, Color.clear,
            new Color(0.6f, 0.6f, 0.6f));

        float y = previewRect.y + padding;
        float contentX = previewRect.x + padding;

        // Vertical alignment.
        float contentHeight = totalHeight - padding * 2f;
        float availableHeight = previewRect.height - padding * 2f;

        if (data.VerticalAlignment == DocumentVerticalAlignment.Center)
            y += Mathf.Max(0, (availableHeight - contentHeight) * 0.5f);
        else if (data.VerticalAlignment == DocumentVerticalAlignment.Bottom)
            y += Mathf.Max(0, availableHeight - contentHeight);

        // Separar secciones normales y ancladas al fondo.
        var normalSections = new System.Collections.Generic.List<DocumentSection>();
        var bottomSections = new System.Collections.Generic.List<DocumentSection>();

        foreach (var section in data.Sections)
        {
            if (string.IsNullOrEmpty(section.Text)) continue;
            if (section.AnchorToBottom)
                bottomSections.Add(section);
            else
                normalSections.Add(section);
        }

        // Dibujar secciones normales.
        foreach (var section in normalSections)
        {
            GUIStyle style = BuildSectionStyle(section);

            float textHeight = style.CalcHeight(
                new GUIContent(section.Text), contentWidth);

            Rect textRect = new Rect(contentX, y, contentWidth, textHeight);
            EditorGUI.LabelField(textRect, section.Text, style);

            y += textHeight + 4f;

            if (section.ShowDivider)
            {
                Rect dividerRect = new Rect(contentX, y, contentWidth, 2f);
                EditorGUI.DrawRect(dividerRect, section.DividerColor);
                y += 6f;
            }
        }

        // Dibujar secciones ancladas al fondo.
        if (bottomSections.Count > 0)
        {
            float bottomY = previewRect.yMax - padding;

            for (int i = bottomSections.Count - 1; i >= 0; i--)
            {
                var section = bottomSections[i];
                GUIStyle style = BuildSectionStyle(section);

                float textHeight = style.CalcHeight(
                    new GUIContent(section.Text), contentWidth);

                bottomY -= textHeight;
                Rect textRect = new Rect(contentX, bottomY, contentWidth, textHeight);
                EditorGUI.LabelField(textRect, section.Text, style);

                bottomY -= 4f;
            }
        }

        // Imagen.
        if (data.ContentImage != null)
        {
            float imgHeight = 80f;
            Rect imgRect = new Rect(
                contentX + contentWidth * 0.25f, y,
                contentWidth * 0.5f, imgHeight);

            Texture2D tex = AssetPreview.GetAssetPreview(data.ContentImage);
            if (tex != null)
                GUI.DrawTexture(imgRect, tex, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(imgRect, new Color(0.8f, 0.8f, 0.8f));
        }

        EditorGUILayout.EndScrollView();
    }

    private float CalculateTotalHeight(DocumentData data, float width)
    {
        float normalHeight = 0f;
        float bottomHeight = 0f;

        foreach (var section in data.Sections)
        {
            if (string.IsNullOrEmpty(section.Text)) continue;

            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                richText = true,
                fontSize = Mathf.RoundToInt(section.EffectiveFontSize * 0.75f),
            };

            float h = style.CalcHeight(new GUIContent(section.Text), width) + 4f;

            if (section.AnchorToBottom)
                bottomHeight += h;
            else
            {
                normalHeight += h;
                if (section.ShowDivider)
                    normalHeight += 6f;
            }
        }

        // Garantizar espacio mínimo para que las secciones bottom no se solapen.
        float minHeight = normalHeight + bottomHeight + 40f;
        return Mathf.Max(normalHeight, minHeight);
    }

    private GUIStyle BuildSectionStyle(DocumentSection section)
    {
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            richText = true,
            fontSize = Mathf.RoundToInt(section.EffectiveFontSize * 0.75f),
            normal = { textColor = Color.black },
            alignment = TmpAlignmentToGUI(section.Alignment),
        };

        switch (section.Type)
        {
            case SectionType.Title:
                style.fontStyle = FontStyle.Bold;
                break;
            case SectionType.Subtitle:
                style.fontStyle = FontStyle.Italic;
                style.normal = new GUIStyleState
                    { textColor = new Color(0.33f, 0.33f, 0.33f) };
                break;
            case SectionType.Footer:
                style.normal = new GUIStyleState
                    { textColor = new Color(0.4f, 0.4f, 0.4f) };
                break;
            case SectionType.Caption:
                style.fontStyle = FontStyle.Italic;
                style.normal = new GUIStyleState
                    { textColor = new Color(0.47f, 0.47f, 0.47f) };
                break;
        }

        return style;
    }

    private static TextAnchor TmpAlignmentToGUI(TextAlignmentOptions alignment)
    {
        return alignment switch
        {
            TextAlignmentOptions.TopLeft      => TextAnchor.UpperLeft,
            TextAlignmentOptions.Top          => TextAnchor.UpperCenter,
            TextAlignmentOptions.TopRight     => TextAnchor.UpperRight,
            TextAlignmentOptions.Left         => TextAnchor.MiddleLeft,
            TextAlignmentOptions.Center       => TextAnchor.MiddleCenter,
            TextAlignmentOptions.Right        => TextAnchor.MiddleRight,
            TextAlignmentOptions.BottomLeft   => TextAnchor.LowerLeft,
            TextAlignmentOptions.Bottom       => TextAnchor.LowerCenter,
            TextAlignmentOptions.BottomRight  => TextAnchor.LowerRight,
            _                                 => TextAnchor.UpperLeft,
        };
    }
}
