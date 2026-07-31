using EOS.GuideRoom;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace EOS.EditorTools.GuideRoom
{
    /// <summary>
    /// Construye o reconstruye la terminal visual del Guía usando las
    /// dimensiones reales de CarlosWorkshopTesting en feat/guide-room-puzzles.
    /// </summary>
    public static class GuideTerminalBuilder
    {
        private const string FontPath =
            "Assets/_Project/UI/MainMenu/Fonts/Audiowide_SDF.asset";

        private const string MaterialFolder =
            "Assets/_Project/Art/Materials/GuideRoom";

        private static readonly Color BackgroundColor =
            new(0f, 0f, 0f, 0.965f);

        private static readonly Color HeaderColor =
            new(0.012f, 0.065f, 0.030f, 0.96f);

        private static readonly Color PanelColor =
            new(0.006f, 0.025f, 0.016f, 0.72f);

        private static readonly Color BrightGreen =
            new(0.22f, 1f, 0.32f, 1f);

        private static readonly Color SoftGreen =
            new(0.55f, 0.86f, 0.58f, 1f);

        private static readonly Color MutedText =
            new(0.62f, 0.66f, 0.63f, 1f);

        [MenuItem("EOS/Guide Room/Build or Refresh Main Terminal")]
        private static void BuildOrRefresh()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Guide Terminal",
                    "Selecciona MainScreen o ScreenAnchor en la Hierarchy.",
                    "Entendido");
                return;
            }

            GameObject mainScreen = ResolveMainScreen(selected);

            if (mainScreen == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Guide Terminal",
                    "Se reconstruirá ScreenCanvas y se conservarán " +
                    "ScreenFrame, ScreenSurface y la posición de MainScreen.",
                    "Reconstruir",
                    "Cancelar"))
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Guide Main Terminal");

            EnsurePhysicalScreen(mainScreen.transform);

            TMP_FontAsset displayFont =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            if (displayFont == null)
            {
                displayFont = TMP_Settings.defaultFontAsset;
                Debug.LogWarning(
                    $"[GuideTerminalBuilder] No se encontró {FontPath}. " +
                    "Se usará la fuente TMP predeterminada.");
            }

            RectTransform canvasRect = BuildCanvas(mainScreen.transform);
            BuildInterface(canvasRect, displayFont);

            EditorSceneManager.MarkSceneDirty(mainScreen.scene);
            Selection.activeGameObject = canvasRect.gameObject;
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log(
                "[GuideTerminalBuilder] Terminal construida correctamente.");
        }

        [MenuItem(
            "EOS/Guide Room/Build or Refresh Main Terminal",
            true)]
        private static bool ValidateBuildOrRefresh()
        {
            return Selection.activeGameObject != null;
        }

        private static GameObject ResolveMainScreen(GameObject selected)
        {
            if (selected.name == "MainScreen")
            {
                return selected;
            }

            Transform existing = selected.transform.Find("MainScreen");

            if (existing != null)
            {
                return existing.gameObject;
            }

            if (selected.name != "ScreenAnchor")
            {
                bool createHere = EditorUtility.DisplayDialog(
                    "Guide Terminal",
                    "El objeto seleccionado no se llama MainScreen ni " +
                    "ScreenAnchor. ¿Crear MainScreen como hijo?",
                    "Crear",
                    "Cancelar");

                if (!createHere)
                {
                    return null;
                }
            }

            GameObject mainScreen = new("MainScreen");

            Undo.RegisterCreatedObjectUndo(
                mainScreen,
                "Create MainScreen");

            mainScreen.transform.SetParent(selected.transform, false);
            return mainScreen;
        }

        private static void EnsurePhysicalScreen(Transform mainScreen)
        {
            Transform frame = mainScreen.Find("ScreenFrame");

            if (frame == null)
            {
                GameObject frameObject =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);

                frameObject.name = "ScreenFrame";

                Undo.RegisterCreatedObjectUndo(
                    frameObject,
                    "Create ScreenFrame");

                frameObject.transform.SetParent(mainScreen, false);
                frame = frameObject.transform;
            }

            frame.localPosition =
                new Vector3(0f, -0.57f, 0.000001f);
            frame.localRotation =
                Quaternion.Euler(0f, -90f, 0f);
            frame.localScale =
                new Vector3(7.4f, 4.4f, 0.22f);

            Transform surface = mainScreen.Find("ScreenSurface");

            if (surface == null)
            {
                GameObject surfaceObject =
                    GameObject.CreatePrimitive(PrimitiveType.Cube);

                surfaceObject.name = "ScreenSurface";

                Undo.RegisterCreatedObjectUndo(
                    surfaceObject,
                    "Create ScreenSurface");

                surfaceObject.transform.SetParent(mainScreen, false);
                surface = surfaceObject.transform;
            }

            surface.localPosition =
                new Vector3(0.1f, -0.57f, 0.002f);
            surface.localRotation =
                Quaternion.Euler(0f, -90f, 0f);
            surface.localScale =
                new Vector3(7f, 4f, 0.04f);

            ApplyScreenMaterial(surface.GetComponent<Renderer>());
        }

        private static RectTransform BuildCanvas(Transform mainScreen)
        {
            Transform existing = mainScreen.Find("ScreenCanvas");
            RectTransform rect;

            if (existing != null)
            {
                rect = existing.GetComponent<RectTransform>();

                if (rect == null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    rect = CreateCanvas(mainScreen);
                }
                else
                {
                    ClearChildren(rect);
                    EnsureCanvasComponents(rect.gameObject);
                }
            }
            else
            {
                rect = CreateCanvas(mainScreen);
            }

            rect.name = "ScreenCanvas";
            rect.SetParent(mainScreen, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1360f, 760f);
            rect.localRotation = Quaternion.Euler(0f, -90f, 0f);
            rect.localScale = Vector3.one * 0.005f;
            rect.anchoredPosition = new Vector2(0.142f, -0.47f);

            Canvas canvas = rect.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            return rect;
        }

        private static RectTransform CreateCanvas(Transform parent)
        {
            GameObject canvasObject = new(
                "ScreenCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Undo.RegisterCreatedObjectUndo(
                canvasObject,
                "Create ScreenCanvas");

            RectTransform rect =
                canvasObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            return rect;
        }

        private static void EnsureCanvasComponents(GameObject canvasObject)
        {
            if (canvasObject.GetComponent<Canvas>() == null)
            {
                Undo.AddComponent<Canvas>(canvasObject);
            }

            if (canvasObject.GetComponent<CanvasScaler>() == null)
            {
                Undo.AddComponent<CanvasScaler>(canvasObject);
            }

            if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            {
                Undo.AddComponent<GraphicRaycaster>(canvasObject);
            }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Undo.DestroyObjectImmediate(
                    parent.GetChild(index).gameObject);
            }
        }

        private static void BuildInterface(
            RectTransform canvas,
            TMP_FontAsset displayFont)
        {
            Image background =
                CreateImage("Background", canvas, BackgroundColor);

            Stretch(
                background.rectTransform,
                left: 0f,
                right: 0f,
                top: 19f,
                bottom: -19f);

            background.rectTransform.localPosition = new Vector3(
                background.rectTransform.localPosition.x,
                background.rectTransform.localPosition.y,
                -4f);

            Image headerBar =
                CreateImage("HeaderBar", canvas, HeaderColor);

            SetTopStretch(
                headerBar.rectTransform,
                left: 35f,
                right: 35f,
                top: 0f,
                height: 90f);

            TMP_Text headerText = CreateText(
                "HeaderText",
                headerBar.rectTransform,
                displayFont,
                "TERMINAL DE ARCHIVOS",
                42f,
                Color.white,
                TextAlignmentOptions.Center);

            Stretch(
                headerText.rectTransform,
                left: 25f,
                right: 310f,
                top: 0f,
                bottom: 0f);

            TMP_Text stationIdText = CreateText(
                "StationIdText",
                headerBar.rectTransform,
                displayFont,
                "ID: 00-7A-GUIDE",
                20f,
                MutedText,
                TextAlignmentOptions.MidlineRight);

            SetAnchoredRect(
                stationIdText.rectTransform,
                anchorMin: new Vector2(1f, 0f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(1f, 0.5f),
                anchoredPosition: new Vector2(-25f, 0f),
                sizeDelta: new Vector2(260f, 0f));

            Image contentFrame =
                CreateImage("ContentFrame", canvas, PanelColor);

            Stretch(
                contentFrame.rectTransform,
                left: 45f,
                right: 45f,
                top: 120f,
                bottom: 85f);

            Outline contentOutline =
                Undo.AddComponent<Outline>(contentFrame.gameObject);

            contentOutline.effectColor = new Color(
                BrightGreen.r,
                BrightGreen.g,
                BrightGreen.b,
                0.42f);

            contentOutline.effectDistance = new Vector2(2f, -2f);

            RectTransform waitingPanel =
                CreateRect("WaitingPanel", contentFrame.rectTransform);

            Stretch(
                waitingPanel,
                left: 30f,
                right: 30f,
                top: 30f,
                bottom: 30f);

            TMP_Text statusText = CreateText(
                "StatusText",
                waitingPanel,
                displayFont,
                "INSERTE UNA CARPETA",
                54f,
                BrightGreen,
                TextAlignmentOptions.Center);

            SetAnchoredRect(
                statusText.rectTransform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: new Vector2(0f, 25f),
                sizeDelta: new Vector2(1050f, 95f));

            TMP_Text subStatusText = CreateText(
                "SubStatusText",
                waitingPanel,
                displayFont,
                "- SISTEMA EN ESPERA -",
                23f,
                MutedText,
                TextAlignmentOptions.Center);

            SetAnchoredRect(
                subStatusText.rectTransform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: new Vector2(0f, -48f),
                sizeDelta: new Vector2(800f, 50f));

            RectTransform documentPanel =
                CreateRect("DocumentPanel", contentFrame.rectTransform);

            Stretch(
                documentPanel,
                left: 32f,
                right: 32f,
                top: 25f,
                bottom: 25f);

            TMP_Text folderNameText = CreateText(
                "FolderNameText",
                documentPanel,
                displayFont,
                "CARPETA",
                20f,
                SoftGreen,
                TextAlignmentOptions.MidlineLeft);

            SetTopStretch(
                folderNameText.rectTransform,
                left: 0f,
                right: 0f,
                top: 0f,
                height: 42f);

            TMP_Text documentTitleText = CreateText(
                "DocumentTitleText",
                documentPanel,
                displayFont,
                "DOCUMENTO",
                31f,
                BrightGreen,
                TextAlignmentOptions.MidlineLeft);

            SetTopStretch(
                documentTitleText.rectTransform,
                left: 0f,
                right: 140f,
                top: 46f,
                height: 55f);

            TMP_Text pageText = CreateText(
                "PageText",
                documentPanel,
                displayFont,
                "01 / 01",
                20f,
                MutedText,
                TextAlignmentOptions.MidlineRight);

            SetAnchoredRect(
                pageText.rectTransform,
                anchorMin: new Vector2(1f, 1f),
                anchorMax: new Vector2(1f, 1f),
                pivot: new Vector2(1f, 1f),
                anchoredPosition: new Vector2(0f, -48f),
                sizeDelta: new Vector2(130f, 50f));

            TMP_Text documentBodyText = CreateText(
                "DocumentBodyText",
                documentPanel,
                TMP_Settings.defaultFontAsset ?? displayFont,
                "Contenido del documento.",
                25f,
                new Color(0.86f, 0.9f, 0.87f, 1f),
                TextAlignmentOptions.TopLeft);

            Stretch(
                documentBodyText.rectTransform,
                left: 0f,
                right: 0f,
                top: 112f,
                bottom: 0f);

            documentBodyText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            documentBodyText.overflowMode = TextOverflowModes.Overflow;
            documentPanel.gameObject.SetActive(false);

            TMP_Text footerText = CreateText(
                "FooterText",
                canvas,
                displayFont,
                "SISTEMA DE CONSULTA  //  EN ESPERA",
                21f,
                MutedText,
                TextAlignmentOptions.MidlineLeft);

            SetBottomStretch(
                footerText.rectTransform,
                left: 45f,
                right: 330f,
                bottom: 20f,
                height: 45f);

            TMP_Text versionText = CreateText(
                "VersionText",
                canvas,
                displayFont,
                "v1.0.0",
                20f,
                MutedText,
                TextAlignmentOptions.MidlineRight);

            SetAnchoredRect(
                versionText.rectTransform,
                anchorMin: new Vector2(1f, 0f),
                anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(1f, 0f),
                anchoredPosition: new Vector2(-70f, 20f),
                sizeDelta: new Vector2(180f, 45f));

            Image statusDot =
                CreateImage("StatusDot", canvas, BrightGreen);

            SetAnchoredRect(
                statusDot.rectTransform,
                anchorMin: new Vector2(1f, 0f),
                anchorMax: new Vector2(1f, 0f),
                pivot: new Vector2(0.5f, 0.5f),
                anchoredPosition: new Vector2(-42f, 42f),
                sizeDelta: new Vector2(16f, 16f));

            GuideTerminalView terminalView =
                canvas.GetComponent<GuideTerminalView>();

            if (terminalView == null)
            {
                terminalView =
                    Undo.AddComponent<GuideTerminalView>(canvas.gameObject);
            }

            terminalView.Configure(
                headerText,
                stationIdText,
                waitingPanel.gameObject,
                statusText,
                subStatusText,
                documentPanel.gameObject,
                folderNameText,
                documentTitleText,
                documentBodyText,
                pageText,
                footerText,
                versionText);

            EditorUtility.SetDirty(terminalView);
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color)
        {
            GameObject gameObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);

            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject gameObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);

            TextMeshProUGUI tmp =
                gameObject.GetComponent<TextMeshProUGUI>();

            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableAutoSizing = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent)
        {
            GameObject gameObject =
                new(name, typeof(RectTransform));

            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");

            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(
            RectTransform rect,
            float left,
            float right,
            float top,
            float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void SetTopStretch(
            RectTransform rect,
            float left,
            float right,
            float top,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-(left + right), height);
            rect.localScale = Vector3.one;
        }

        private static void SetBottomStretch(
            RectTransform rect,
            float left,
            float right,
            float bottom,
            float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition =
                new Vector2((left - right) * 0.5f, bottom);
            rect.sizeDelta = new Vector2(-(left + right), height);
            rect.localScale = Vector3.one;
        }

        private static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }

        private static void ApplyScreenMaterial(Renderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            EnsureFolder(MaterialFolder);

            const string materialPath =
                MaterialFolder + "/MAT_MainScreenSurface.mat";

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            if (material == null)
            {
                Shader shader =
                    Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader)
                {
                    name = "MAT_MainScreenSurface",
                    color = new Color(0.006f, 0.008f, 0.007f, 1f)
                };

                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", 0f);
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.2f);
                }

                AssetDatabase.CreateAsset(material, materialPath);
            }

            renderer.sharedMaterial = material;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for (int index = 1; index < parts.Length; index++)
            {
                string next = $"{current}/{parts[index]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }
    }
}
