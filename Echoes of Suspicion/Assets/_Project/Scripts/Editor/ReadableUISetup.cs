using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Editor utility that builds the ReadableUI hierarchy automatically.
/// Use via menu: Tools > Echoes > Setup ReadableUI
///
/// El DocumentPanel ahora genera sus secciones dinámicamente en runtime,
/// así que solo necesitamos un contenedor vacío con VerticalLayoutGroup.
/// </summary>
public static class ReadableUISetup
{
    [MenuItem("Tools/Echoes/Setup ReadableUI")]
    public static void Setup()
    {
        ReadableUI existing = Object.FindFirstObjectByType<ReadableUI>();
        GameObject root;

        if (existing != null)
        {
            root = existing.gameObject;
            Debug.Log("[ReadableUISetup] Found existing ReadableUI. Wiring references...");
        }
        else
        {
            Canvas screenCanvas = FindScreenSpaceCanvas();
            if (screenCanvas == null)
            {
                Debug.LogError("[ReadableUISetup] No Screen Space canvas found in scene. " +
                               "Create one first (your HUD canvas) and run again.");
                return;
            }

            root = new GameObject("ReadableUI");
            root.transform.SetParent(screenCanvas.transform, false);

            RectTransform rootRect = root.AddComponent<RectTransform>();
            StretchFull(rootRect);

            root.AddComponent<ReadableUI>();
            Debug.Log("[ReadableUISetup] Created ReadableUI under " + screenCanvas.name);
        }

        // CanvasGroup on root.
        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        if (cg == null) cg = root.AddComponent<CanvasGroup>();

        // ── Overlay ────────────────────────────────────────────────
        GameObject overlay = CreateChild(root, "Overlay");
        Image overlayImg = overlay.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.85f);
        overlayImg.raycastTarget = true;
        StretchFull(overlay.GetComponent<RectTransform>());

        // ── Document Panel ─────────────────────────────────────────
        GameObject docPanel = CreateChild(root, "DocumentPanel");
        Image docBg = docPanel.AddComponent<Image>();
        docBg.color = HexColor("#FFF8E7");
        RectTransform docRect = docPanel.GetComponent<RectTransform>();
        docRect.anchorMin = new Vector2(0.15f, 0.1f);
        docRect.anchorMax = new Vector2(0.85f, 0.9f);
        docRect.offsetMin = Vector2.zero;
        docRect.offsetMax = Vector2.zero;

        // DocContentParent — contenedor donde ReadableUI genera secciones.
        GameObject docContentParent = CreateChild(docPanel, "DocContentParent");
        StretchFull(docContentParent.GetComponent<RectTransform>());

        VerticalLayoutGroup docLayout = docContentParent.AddComponent<VerticalLayoutGroup>();
        docLayout.padding = new RectOffset(30, 30, 25, 25);
        docLayout.spacing = 8f;
        docLayout.childAlignment = TextAnchor.UpperLeft;
        docLayout.childControlWidth = true;
        docLayout.childControlHeight = true;
        docLayout.childForceExpandWidth = true;
        docLayout.childForceExpandHeight = false;

        // DocImage (fuera del content parent, al final del panel).
        GameObject docImage = CreateChild(docPanel, "DocImage");
        Image docImg = docImage.AddComponent<Image>();
        docImg.preserveAspect = true;
        docImg.raycastTarget = false;
        LayoutElement imgLE = docImage.AddComponent<LayoutElement>();
        imgLE.preferredHeight = 150f;
        docImage.SetActive(false);

        // ── Sticky Note Panel ──────────────────────────────────────
        GameObject stickyPanel = CreateChild(root, "StickyNotePanel");
        Image stickyBg = stickyPanel.AddComponent<Image>();
        stickyBg.color = new Color(1f, 0.95f, 0.6f);
        RectTransform stickyRect = stickyPanel.GetComponent<RectTransform>();
        stickyRect.anchorMin = new Vector2(0.3f, 0.2f);
        stickyRect.anchorMax = new Vector2(0.7f, 0.8f);
        stickyRect.offsetMin = Vector2.zero;
        stickyRect.offsetMax = Vector2.zero;

        VerticalLayoutGroup stickyLayout = stickyPanel.AddComponent<VerticalLayoutGroup>();
        stickyLayout.padding = new RectOffset(20, 20, 20, 20);
        stickyLayout.spacing = 10f;
        stickyLayout.childAlignment = TextAnchor.MiddleCenter;
        stickyLayout.childControlWidth = true;
        stickyLayout.childControlHeight = false;
        stickyLayout.childForceExpandWidth = true;
        stickyLayout.childForceExpandHeight = false;

        // NoteText
        GameObject noteText = CreateTMPChild(stickyPanel, "NoteText", "Texto de la nota...",
            fontSize: 22, height: 200, alignment: TextAlignmentOptions.Center);
        LayoutElement noteLE = noteText.GetComponent<LayoutElement>();
        if (noteLE == null) noteLE = noteText.AddComponent<LayoutElement>();
        noteLE.flexibleHeight = 1f;

        // NoteImage
        GameObject noteImage = CreateChild(stickyPanel, "NoteImage");
        Image noteImg = noteImage.AddComponent<Image>();
        noteImg.preserveAspect = true;
        noteImg.raycastTarget = false;
        LayoutElement noteImgLE = noteImage.AddComponent<LayoutElement>();
        noteImgLE.preferredHeight = 100f;
        noteImage.SetActive(false);

        stickyPanel.SetActive(false);

        // ── Wire SerializedObject references ───────────────────────
        ReadableUI ui = root.GetComponent<ReadableUI>();
        SerializedObject so = new SerializedObject(ui);

        so.FindProperty("_canvasGroup").objectReferenceValue = cg;
        so.FindProperty("_documentPanel").objectReferenceValue = docPanel;
        so.FindProperty("_docContentParent").objectReferenceValue = docContentParent.transform;
        so.FindProperty("_docImage").objectReferenceValue = docImg;
        so.FindProperty("_stickyNotePanel").objectReferenceValue = stickyPanel;
        so.FindProperty("_stickyNoteBackground").objectReferenceValue = stickyBg;
        so.FindProperty("_noteText").objectReferenceValue = noteText.GetComponent<TMP_Text>();
        so.FindProperty("_noteImage").objectReferenceValue = noteImg;

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(root);
        Undo.RegisterCreatedObjectUndo(root, "Setup ReadableUI");

        Debug.Log("[ReadableUISetup] Done! All references wired. " +
                  "Check the Inspector and adjust fonts/colors as needed.");
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static Canvas FindScreenSpaceCanvas()
    {
        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay ||
                c.renderMode == RenderMode.ScreenSpaceCamera)
                return c;
        }
        return null;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static GameObject CreateChild(GameObject parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static GameObject CreateTMPChild(
        GameObject parent, string name, string placeholder,
        float fontSize = 16f, float height = 30f,
        bool bold = false, bool italic = false,
        Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        GameObject go = CreateChild(parent, name);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = placeholder;
        tmp.fontSize = fontSize;
        tmp.fontStyle = (bold ? FontStyles.Bold : FontStyles.Normal) |
                        (italic ? FontStyles.Italic : FontStyles.Normal);
        tmp.color = color ?? Color.black;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.richText = true;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        return go;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
