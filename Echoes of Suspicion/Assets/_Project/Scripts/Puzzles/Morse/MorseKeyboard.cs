using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Genera procedimentalmente un teclado QWERTY-ES (27 teclas: A-Z + Ñ)
    /// compuesto de MorsePanels, más un display superior que muestra el
    /// progreso de la palabra.
    ///
    /// Cada tecla es un hijo con su propio NetworkIdentity + MorsePanel.
    /// El teclado en sí NO tiene NetworkIdentity (evita conflictos de
    /// NI anidados en Mirror).
    ///
    /// SETUP:
    /// 1. Colocar en el pasillo donde el Runner verá el teclado.
    /// 2. Asignar font (Audiowide_SDF).
    /// 3. Asignar coordinator (MorsePuzzleCoordinator).
    /// 4. ContextMenu → "Generate Keyboard" para crear las teclas.
    /// 5. Conectar el array panels del coordinator a GetAllPanels().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MorseKeyboard : MonoBehaviour
    {
        // QWERTY-ES layout
        private static readonly string[] Row1 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
        private static readonly string[] Row2 = { "A", "S", "D", "F", "G", "H", "J", "K", "L", "Ñ" };
        private static readonly string[] Row3 = { "Z", "X", "C", "V", "B", "N", "M" };

        [Header("Layout")]

        [SerializeField, Tooltip("Tamaño de cada tecla (ancho, alto, profundidad).")]
        private Vector3 keySize = new(0.14f, 0.14f, 0.04f);

        [SerializeField, Tooltip("Espacio entre teclas.")]
        private float keyGap = 0.02f;

        [SerializeField, Tooltip("Offset horizontal de cada fila (simula escalonado QWERTY).")]
        private float rowStagger = 0.04f;

        [Header("Display")]

        [SerializeField, Tooltip("Altura del display sobre las teclas.")]
        private float displayHeight = 0.12f;

        [SerializeField, Tooltip("Alto del display.")]
        private float displayPanelHeight = 0.18f;

        [Header("Colors")]

        [SerializeField]
        private Color keyColor = new(0.20f, 0.22f, 0.26f, 1f);

        [SerializeField]
        private Color keySuccessColor = new(0.20f, 0.80f, 0.35f, 1f);

        [SerializeField]
        private Color keyFailureColor = new(0.85f, 0.20f, 0.20f, 1f);

        [SerializeField]
        private Color keySolvedColor = new(0.16f, 0.62f, 0.30f, 1f);

        [SerializeField]
        private Color displayBgColor = new(0.01f, 0.04f, 0.02f, 0.95f);

        [SerializeField]
        private Color displayTextColor = new(0.22f, 1f, 0.32f, 1f);

        [SerializeField]
        private Color letterLabelColor = new(0.85f, 0.85f, 0.85f, 1f);

        [Header("References")]

        [SerializeField, Tooltip("Fuente para las letras y el display (Audiowide_SDF).")]
        private TMP_FontAsset font;

        [SerializeField, Tooltip("Coordinador del puzzle Morse.")]
        private MorsePuzzleCoordinator coordinator;

        // ── Cached references ────────────────────────────────

        private TMP_Text _displayText;
        private MorsePanel[] _allPanels;

        /// <summary>El TMP del display de progreso de la palabra.</summary>
        public TMP_Text DisplayText
        {
            get
            {
                if (_displayText == null)
                    FindExistingReferences();
                return _displayText;
            }
        }

        /// <summary>Todas las teclas-panel generadas.</summary>
        public MorsePanel[] GetAllPanels()
        {
            if (_allPanels == null || _allPanels.Length == 0)
                _allPanels = GetComponentsInChildren<MorsePanel>();
            return _allPanels;
        }

        // ── Display API ──────────────────────────────────────

        /// <summary>
        /// Actualiza el display con el texto dado (ej: "S _ _ _").
        /// Llamado por el coordinator via RPC.
        /// </summary>
        public void UpdateWordDisplay(string text)
        {
            if (_displayText == null)
                FindExistingReferences();

            if (_displayText != null)
                _displayText.text = text;
        }

        // ── Lifecycle ────────────────────────────────────────

        private void Awake()
        {
            FindExistingReferences();
            _allPanels = GetComponentsInChildren<MorsePanel>();
        }

        // ── Find existing ────────────────────────────────────

        private void FindExistingReferences()
        {
            Transform display = transform.Find("KeyboardDisplay");
            if (display != null)
                _displayText = display.GetComponentInChildren<TMP_Text>();
        }

        // ── Procedural Generation ────────────────────────────

        [ContextMenu("Generate Keyboard")]
        private void EditorGenerateKeyboard()
        {
            // Destroy old generated children.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Key_") || child.name == "KeyboardDisplay"
                    || child.name == "KeyboardBackplate")
                {
                    SafeDestroy(child.gameObject);
                }
            }

            _displayText = null;
            _allPanels = null;

            BuildKeyboard();
        }

        private void BuildKeyboard()
        {
            float stride = keySize.x + keyGap;

            // ── Backplate ──
            float totalWidth = 10 * stride - keyGap + 0.06f;
            float totalHeight = 3 * (keySize.y + keyGap) + displayPanelHeight + displayHeight + 0.06f;

            GameObject backplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backplate.name = "KeyboardBackplate";
            backplate.transform.SetParent(transform, false);
            backplate.transform.localPosition = new Vector3(
                (10 * stride - keyGap) * 0.5f - stride * 0.5f,
                (3 * (keySize.y + keyGap) + displayPanelHeight + displayHeight) * 0.5f - keySize.y * 0.5f,
                keySize.z * 0.5f + 0.005f);
            backplate.transform.localScale = new Vector3(totalWidth, totalHeight, 0.02f);

            var bpRenderer = backplate.GetComponent<Renderer>();
            if (bpRenderer != null)
            {
                bpRenderer.sharedMaterial = CreateTempMaterial(new Color(0.12f, 0.12f, 0.14f));
            }

            var bpCol = backplate.GetComponent<Collider>();
            if (bpCol != null) SafeDestroy(bpCol);

            // ── Rows of keys ──
            float row3Y = 0f;
            float row2Y = keySize.y + keyGap;
            float row1Y = 2f * (keySize.y + keyGap);

            BuildRow(Row1, row1Y, 0f, stride);
            BuildRow(Row2, row2Y, rowStagger, stride);
            BuildRow(Row3, row3Y, rowStagger * 2f, stride);

            // ── Word display above keys ──
            float displayY = 3f * (keySize.y + keyGap) + displayHeight;
            BuildWordDisplay(displayY, totalWidth - 0.06f);

            _allPanels = GetComponentsInChildren<MorsePanel>();
        }

        private void BuildRow(string[] letters, float y, float xOffset, float stride)
        {
            for (int i = 0; i < letters.Length; i++)
            {
                float x = xOffset + i * stride;
                BuildKey(letters[i], new Vector3(x, y, 0f));
            }
        }

        private void BuildKey(string letter, Vector3 localPos)
        {
            GameObject keyGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            keyGo.name = $"Key_{letter}";
            keyGo.transform.SetParent(transform, false);
            keyGo.transform.localPosition = localPos;
            keyGo.transform.localScale = keySize;

            int interactable = LayerMask.NameToLayer("Interactable");
            keyGo.layer = interactable >= 0 ? interactable : 0;

            var renderer = keyGo.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateTempMaterial(keyColor);

            // NetworkIdentity (required by MorsePanel / RatInteractable).
            keyGo.AddComponent<NetworkIdentity>();

            // MorsePanel component.
            MorsePanel panel = keyGo.AddComponent<MorsePanel>();

            // Wire panel fields via reflection since they're private serialized.
#if UNITY_EDITOR
            var so = new UnityEditor.SerializedObject(panel);
            so.FindProperty("symbolId").stringValue = letter;
            so.FindProperty("targetRenderer").objectReferenceValue = renderer;
            so.FindProperty("idleColor").colorValue = keyColor;
            so.FindProperty("successColor").colorValue = keySuccessColor;
            so.FindProperty("failureColor").colorValue = keyFailureColor;
            so.FindProperty("solvedColor").colorValue = keySolvedColor;

            if (coordinator != null)
                so.FindProperty("coordinator").objectReferenceValue = coordinator;

            so.ApplyModifiedPropertiesWithoutUndo();
#endif

            // Letter label (3D TMP above the key face).
            GameObject labelGo = new GameObject($"Label_{letter}");
            labelGo.transform.SetParent(keyGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.51f);
            labelGo.transform.localRotation = Quaternion.identity;
            labelGo.transform.localScale = new Vector3(
                1f / keySize.x * 0.08f,
                1f / keySize.y * 0.08f,
                1f);

            var label = labelGo.AddComponent<TextMeshPro>();
            label.text = letter;
            label.fontSize = 6f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = letterLabelColor;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            if (font != null)
                label.font = font;

            // Wire symbolLabel on the panel.
#if UNITY_EDITOR
            var so2 = new UnityEditor.SerializedObject(panel);
            so2.FindProperty("symbolLabel").objectReferenceValue = label;
            so2.ApplyModifiedPropertiesWithoutUndo();
#endif
        }

        private void BuildWordDisplay(float y, float width)
        {
            GameObject displayGo = new GameObject("KeyboardDisplay");
            displayGo.transform.SetParent(transform, false);
            displayGo.transform.localPosition = new Vector3(
                (10 * (keySize.x + keyGap) - keyGap) * 0.5f - (keySize.x + keyGap) * 0.5f,
                y,
                0f);

            Canvas canvas = displayGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = displayGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600f, 100f);
            rt.localScale = Vector3.one * (width / 400f);

            // Background
            var bg = displayGo.AddComponent<Image>();
            bg.color = displayBgColor;
            bg.raycastTarget = false;

            // Text
            GameObject textGo = new GameObject("DisplayText");
            textGo.transform.SetParent(displayGo.transform, false);

            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10f, 5f);
            textRt.offsetMax = new Vector2(-10f, -5f);

            _displayText = textGo.AddComponent<TextMeshProUGUI>();
            _displayText.text = "";
            _displayText.fontSize = 48f;
            _displayText.alignment = TextAlignmentOptions.Center;
            _displayText.color = displayTextColor;
            _displayText.textWrappingMode = TextWrappingModes.NoWrap;
            _displayText.characterSpacing = 12f;

            if (font != null)
                _displayText.font = font;
        }

        // ── Helpers ──────────────────────────────────────────

        private static Material CreateTempMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
#endif
                Destroy(obj);
        }
    }
}
