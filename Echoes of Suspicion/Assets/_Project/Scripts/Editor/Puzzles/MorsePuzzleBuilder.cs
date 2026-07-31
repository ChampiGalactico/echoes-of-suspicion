using System.Collections.Generic;
using EOS.GuideRoom;
using EOS.Puzzles.Morse;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EOS.EditorTools.Puzzles
{
    /// <summary>
    /// Herramienta de Editor para el puzzle Morse MVP. Crea/actualiza los
    /// assets, construye un setup de prueba bajo el objeto seleccionado,
    /// coloca la carpeta del Guía y valida el montaje.
    ///
    /// Todos los builders son idempotentes: reejecutarlos no genera copias
    /// numeradas descontroladas. No modifican escenas al importar; solo desde
    /// los menús explícitos.
    /// </summary>
    public static class MorsePuzzleBuilder
    {
        private const string AssetFolder =
            "Assets/_Project/ScriptableObjects/Puzzles/Morse";

        private const string PrefabFolder =
            "Assets/_Project/Prefabs/Puzzles/Morse";

        private const string MaterialFolder =
            "Assets/_Project/Art/Materials/Puzzles/Morse";

        private const string DefinitionName = "MorsePuzzleDefinition";
        private const string ManualDocName = "MorseManualDocument";
        private const string CodeTableDocName = "MorseCodeTableDocument";
        private const string FolderDataName = "MorseGuideFolder";
        private const string ItemDataName = "MorseGuideFolderItem";
        private const string FolderPrefabName = "PF_GuideFolder_MorseManual";

        private static readonly string[] SymbolIds =
            { "E", "T", "A", "N", "S", "M", "D", "U", "G", "R" };

        // =====================================================================
        //  MENU 1 — Create or Refresh Morse Assets
        // =====================================================================

        [MenuItem("EOS/Puzzles/Morse/Create or Refresh Morse Assets")]
        public static void CreateOrRefreshAssets()
        {
            EnsureFolder(AssetFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            MorsePuzzleDefinition definition = LoadOrCreateAsset<MorsePuzzleDefinition>(
                $"{AssetFolder}/{DefinitionName}.asset");

            DocumentData manualDoc =
                LoadOrCreateAsset<DocumentData>(
                    $"{AssetFolder}/{ManualDocName}.asset");
            PopulateManualDocument(manualDoc);
            EditorUtility.SetDirty(manualDoc);

            DocumentData codeTableDoc =
                LoadOrCreateAsset<DocumentData>(
                    $"{AssetFolder}/{CodeTableDocName}.asset");
            PopulateCodeTableDocument(codeTableDoc);
            EditorUtility.SetDirty(codeTableDoc);

            GuideFolderData folderData = LoadOrCreateAsset<GuideFolderData>(
                $"{AssetFolder}/{FolderDataName}.asset");
            ConfigureFolderData(
                folderData,
                manualDoc,
                codeTableDoc
            );

            ItemData itemData = LoadOrCreateAsset<ItemData>(
                $"{AssetFolder}/{ItemDataName}.asset");
            ConfigureItemData(itemData);

            GameObject folderPrefab = BuildFolderPrefab(folderData, itemData);

            // Prefabs provisionales de panel y emisor.
            BuildPanelPrefab();
            BuildEmitterPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Registrar item + prefab de carpeta usando el integrador existente.
            RunIntegratorIfPresent();

            Debug.Log(
                "[MorsePuzzleBuilder] Assets Morse creados/actualizados. " +
                $"Definición: {definition != null}, carpeta: {folderPrefab != null}, " +
                "documentos: 2.");
        }

        // =====================================================================
        //  MENU 2 — Build Test Setup Under Selection
        // =====================================================================

        [MenuItem("EOS/Puzzles/Morse/Build Test Setup Under Selection")]
        public static void BuildTestSetup()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Morse", "Selecciona un objeto en la Hierarchy bajo el " +
                    "que construir el puzzle de prueba.", "Entendido");
                return;
            }

            MorsePuzzleDefinition definition = LoadAsset<MorsePuzzleDefinition>(
                $"{AssetFolder}/{DefinitionName}.asset");
            if (definition == null)
            {
                EditorUtility.DisplayDialog(
                    "Morse", "Ejecuta primero 'Create or Refresh Morse Assets'.",
                    "Entendido");
                return;
            }

            Transform existing = selected.transform.Find("MorsePuzzle_MVP");
            if (existing != null)
            {
                bool refresh = EditorUtility.DisplayDialog(
                    "Morse", "Ya existe MorsePuzzle_MVP bajo la selección. " +
                    "¿Refrescarlo? (se recrea sin duplicar)", "Refrescar", "Cancelar");
                if (!refresh)
                {
                    return;
                }

                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Morse Test Setup");

            GameObject root = new("MorsePuzzle_MVP");
            Undo.RegisterCreatedObjectUndo(root, "Create MorsePuzzle_MVP");
            root.transform.SetParent(selected.transform, false);

            // Coordinator
            GameObject coordinatorGo = CreateChild(root.transform, "Coordinator");
            coordinatorGo.AddComponent<NetworkIdentity>();
            MorsePuzzleCoordinator coordinator =
                coordinatorGo.AddComponent<MorsePuzzleCoordinator>();

            // Emitter (single)
            GameObject emitterGo = CreateChild(root.transform, "Emitter");
            emitterGo.transform.localPosition = new Vector3(0f, 1f, 4f);
            MorseEmitter singleEmitter = emitterGo.AddComponent<MorseEmitter>();

            // Keyboard
            GameObject keyboardGo = CreateChild(root.transform, "Keyboard");
            keyboardGo.transform.localPosition = new Vector3(-0.7f, 0.8f, 6f);
            MorseKeyboard keyboard = keyboardGo.AddComponent<MorseKeyboard>();

            // StartTrigger
            GameObject triggerGo = CreateChild(root.transform, "StartTrigger");
            triggerGo.transform.localPosition = new Vector3(0f, 1f, -2f);
            BoxCollider triggerCollider = triggerGo.AddComponent<BoxCollider>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector3(4f, 2f, 2f);
            triggerGo.AddComponent<NetworkIdentity>();
            MorsePuzzleStartTrigger trigger =
                triggerGo.AddComponent<MorsePuzzleStartTrigger>();
            SetSerialized(trigger, "coordinator", coordinator);

            // DoorHook (empty placeholder — user wires a PuzzleDoor here)
            GameObject doorHook = CreateChild(root.transform, "DoorHook");
            doorHook.transform.localPosition = new Vector3(0f, 1f, 8f);

            // Wire coordinator
            coordinator.EditorConfigure(definition, singleEmitter, keyboard);
            SetSerialized(coordinator, "definition", definition);
            SetSerialized(coordinator, "emitter", (Object)singleEmitter);
            SetSerialized(coordinator, "keyboard", (Object)keyboard);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Undo.CollapseUndoOperations(group);

            Debug.Log(
                "[MorsePuzzleBuilder] Setup de prueba construido bajo " +
                $"'{selected.name}'. Recuerda enlazar DoorHook a un PuzzleDoor.");
        }

        // =====================================================================
        //  MENU 3 — Place Guide Manual Under Selection
        // =====================================================================

        [MenuItem("EOS/Puzzles/Morse/Place Guide Manual Under Selection")]
        public static void PlaceGuideManual()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Morse", "Selecciona el Transform (p. ej. un estante) bajo " +
                    "el que instanciar la carpeta.", "Entendido");
                return;
            }

            GameObject prefab = LoadAsset<GameObject>(
                $"{PrefabFolder}/{FolderPrefabName}.prefab");
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Morse", "No existe el prefab de la carpeta. Ejecuta " +
                    "'Create or Refresh Morse Assets' primero.", "Entendido");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                prefab, selected.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Place Morse Manual");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;

            Debug.Log(
                $"[MorsePuzzleBuilder] Carpeta '{FolderPrefabName}' colocada " +
                $"bajo '{selected.name}'.");
        }

        // =====================================================================
        //  MENU 4 — Validate Morse Setup
        // =====================================================================

        [MenuItem("EOS/Puzzles/Morse/Validate Morse Setup")]
        public static void ValidateSetup()
        {
            List<string> problems = new();
            List<string> notes = new();

            MorsePuzzleDefinition def = LoadAsset<MorsePuzzleDefinition>(
                $"{AssetFolder}/{DefinitionName}.asset");
            if (def == null) problems.Add("Falta MorsePuzzleDefinition asset.");

            if (
                LoadAsset<DocumentData>(
                    $"{AssetFolder}/{ManualDocName}.asset"
                ) == null
            )
            {
                problems.Add(
                    "Falta MorseManualDocument (DocumentData)."
                );
            }

            if (
                LoadAsset<DocumentData>(
                    $"{AssetFolder}/{CodeTableDocName}.asset"
                ) == null
            )
            {
                problems.Add(
                    "Falta MorseCodeTableDocument (DocumentData)."
                );
            }
            if (LoadAsset<GuideFolderData>($"{AssetFolder}/{FolderDataName}.asset") == null)
                problems.Add("Falta MorseGuideFolder (GuideFolderData).");

            ItemData item = LoadAsset<ItemData>($"{AssetFolder}/{ItemDataName}.asset");
            if (item == null) problems.Add("Falta MorseGuideFolderItem (ItemData).");

            GameObject folderPrefab = LoadAsset<GameObject>(
                $"{PrefabFolder}/{FolderPrefabName}.prefab");
            if (folderPrefab == null)
                problems.Add($"Falta el prefab {FolderPrefabName}.");
            else if (folderPrefab.GetComponent<GuideFolderItem>() == null)
                problems.Add("El prefab de carpeta no tiene GuideFolderItem.");

            // Registro de inventario
            if (item != null)
            {
                bool registered = IsItemRegistered(item);
                if (!registered)
                    notes.Add("El ItemData no aparece en ningún ItemRegistry " +
                              "cargado. Ejecuta 'Repair Folder Registry and " +
                              "Network Prefabs'.");
            }

            // Spawn prefabs
            if (folderPrefab != null && !IsPrefabInAnySpawnList(folderPrefab))
            {
                notes.Add("El prefab de carpeta no está en NetworkManager." +
                          "spawnPrefabs. Ejecuta el integrador de carpetas.");
            }

            // Setup en escena
            MorsePuzzleCoordinator[] coordinators =
                Object.FindObjectsByType<MorsePuzzleCoordinator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (coordinators.Length == 0)
            {
                notes.Add("No hay MorsePuzzleCoordinator en la escena abierta " +
                          "(usa 'Build Test Setup Under Selection').");
            }

            foreach (MorsePuzzleCoordinator coord in coordinators)
            {
                ValidateCoordinatorInScene(coord, problems);
            }

            ReportValidation(problems, notes);
        }

        private static void ValidateCoordinatorInScene(
            MorsePuzzleCoordinator coord, List<string> problems)
        {
            SerializedObject so = new(coord);

            if (so.FindProperty("definition").objectReferenceValue == null)
                problems.Add($"[{coord.name}] Coordinator sin definición.");

            if (so.FindProperty("emitter").objectReferenceValue == null)
                problems.Add($"[{coord.name}] Coordinator sin emisor.");

            if (so.FindProperty("keyboard").objectReferenceValue == null)
                problems.Add($"[{coord.name}] Coordinator sin teclado (MorseKeyboard).");

            // Trigger — busca en el conjunto MorsePuzzle_MVP (padre del coord).
            Transform setupRoot = coord.transform.parent != null
                ? coord.transform.parent
                : coord.transform;
            MorsePuzzleStartTrigger trigger =
                setupRoot.GetComponentInChildren<MorsePuzzleStartTrigger>(true);
            if (trigger == null)
                problems.Add($"[{coord.name}] No hay MorsePuzzleStartTrigger " +
                             "en el conjunto.");
        }

        private static void ReportValidation(
            List<string> problems, List<string> notes)
        {
            System.Text.StringBuilder sb = new();
            sb.AppendLine("=== Validación Morse ===");

            if (problems.Count == 0)
                sb.AppendLine("SIN ERRORES CRÍTICOS.");
            else
            {
                sb.AppendLine($"ERRORES ({problems.Count}):");
                foreach (string p in problems) sb.AppendLine("  ✗ " + p);
            }

            if (notes.Count > 0)
            {
                sb.AppendLine($"NOTAS ({notes.Count}):");
                foreach (string n in notes) sb.AppendLine("  • " + n);
            }

            if (problems.Count > 0)
                Debug.LogError(sb.ToString());
            else
                Debug.Log(sb.ToString());
        }

        // =====================================================================
        //  FOLDER PREFAB (mismo patrón que FolderScannerBuilder)
        // =====================================================================

        private static GameObject BuildFolderPrefab(
            GuideFolderData folderData, ItemData itemData)
        {
            Material folderMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_GuideFolder_Morse.mat",
                new Color(0.16f, 0.28f, 0.42f, 1f));
            Material labelMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_GuideFolder_MorseLabel.mat",
                new Color(0.80f, 0.78f, 0.62f, 1f));

            GameObject folderObject = new(FolderPrefabName);
            int interactable = LayerMask.NameToLayer("Interactable");
            folderObject.layer = interactable >= 0 ? interactable : 0;

            folderObject.AddComponent<NetworkIdentity>();

            Rigidbody rb = folderObject.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.linearDamping = 1.5f;
            rb.angularDamping = 2f;

            BoxCollider collider = folderObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.035f, 0f);
            collider.size = new Vector3(0.72f, 0.09f, 0.48f);

            NetworkPickupItem pickup = folderObject.AddComponent<NetworkPickupItem>();
            GuideFolderItem folderItem = folderObject.AddComponent<GuideFolderItem>();

            RemoveCollider(CreateCube("FolderBase", folderObject.transform,
                new Vector3(0f, 0.03f, 0f), new Vector3(0.72f, 0.045f, 0.48f), folderMaterial));
            RemoveCollider(CreateCube("FolderTop", folderObject.transform,
                new Vector3(0f, 0.065f, 0.015f), new Vector3(0.68f, 0.025f, 0.43f), folderMaterial));
            RemoveCollider(CreateCube("FolderTab", folderObject.transform,
                new Vector3(-0.20f, 0.085f, 0.18f), new Vector3(0.24f, 0.035f, 0.10f), folderMaterial));
            RemoveCollider(CreateCube("FolderLabel", folderObject.transform,
                new Vector3(0f, 0.084f, -0.02f), new Vector3(0.42f, 0.009f, 0.18f), labelMaterial));

            SetSerialized(pickup, "itemData", itemData);
            SetSerialized(folderItem, "folderData", folderData);

            string prefabPath = $"{PrefabFolder}/{FolderPrefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(folderObject, prefabPath);
            Object.DestroyImmediate(folderObject);

            itemData.worldPrefab = prefab;
            itemData.heldModelPrefab = prefab;
            itemData.heldScale = 0.72f;
            AssignUniqueItemIdIfNeeded(itemData);
            EditorUtility.SetDirty(itemData);
            EditorUtility.SetDirty(folderData);

            return prefab;
        }

        private static void BuildPanelPrefab()
        {
            string path = $"{PrefabFolder}/PF_MorsePanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return; // idempotente
            }

            Material mat = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_MorsePanel.mat",
                new Color(0.20f, 0.22f, 0.26f, 1f));

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "PF_MorsePanel";
            int interactable = LayerMask.NameToLayer("Interactable");
            panel.layer = interactable >= 0 ? interactable : 0;
            panel.transform.localScale = new Vector3(0.5f, 0.5f, 0.12f);
            panel.GetComponent<Renderer>().sharedMaterial = mat;

            panel.AddComponent<NetworkIdentity>();
            MorsePanel panelComp = panel.AddComponent<MorsePanel>();
            SetSerialized(panelComp, "targetRenderer", panel.GetComponent<Renderer>());

            PrefabUtility.SaveAsPrefabAsset(panel, path);
            Object.DestroyImmediate(panel);
        }

        private static void BuildEmitterPrefab()
        {
            string path = $"{PrefabFolder}/PF_MorseEmitter.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            GameObject emitter = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            emitter.name = "PF_MorseEmitter";
            emitter.transform.localScale = Vector3.one * 0.4f;
            RemoveCollider(emitter);
            emitter.AddComponent<AudioSource>();
            emitter.AddComponent<MorseEmitter>();

            PrefabUtility.SaveAsPrefabAsset(emitter, path);
            Object.DestroyImmediate(emitter);
        }

        private static MorsePanel BuildPanelInstance(
            Transform parent, string symbol, int index,
            MorsePuzzleCoordinator coordinator)
        {
            Material mat = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_MorsePanel.mat",
                new Color(0.20f, 0.22f, 0.26f, 1f));

            GameObject panelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panelGo.name = $"Panel_{symbol}";
            Undo.RegisterCreatedObjectUndo(panelGo, "Create Morse Panel");
            panelGo.transform.SetParent(parent, false);
            panelGo.transform.localPosition =
                new Vector3((index % 5) * 0.8f, 1.2f + (index / 5) * 0.8f, 6f);
            panelGo.transform.localScale = new Vector3(0.5f, 0.5f, 0.12f);

            int interactable = LayerMask.NameToLayer("Interactable");
            panelGo.layer = interactable >= 0 ? interactable : 0;
            panelGo.GetComponent<Renderer>().sharedMaterial = mat;

            panelGo.AddComponent<NetworkIdentity>();
            MorsePanel panel = panelGo.AddComponent<MorsePanel>();
            SetSerialized(panel, "symbolId", symbol);
            SetSerialized(panel, "coordinator", coordinator);
            SetSerialized(panel, "targetRenderer", panelGo.GetComponent<Renderer>());

            return panel;
        }

        // =====================================================================
        //  DOCUMENT / DATA CONFIG
        // =====================================================================

        private static void PopulateManualDocument(
            DocumentData doc
        )
        {
            doc.VerticalAlignment =
                DocumentVerticalAlignment.Top;

            doc.InteractionPrompt =
                "Leer protocolo";

            doc.Sections = new[]
            {
                Section(
                    SectionType.Title,
                    "PROTOCOLO DE SEÑALES ACÚSTICAS"
                ),
                Section(
                    SectionType.Body,
                    "MANUAL OPERATIVO // 01 DE 02\n\n" +
                    "Los emisores del corredor transmiten " +
                    "pulsos cortos y largos. El operario debe " +
                    "describir la secuencia completa al Guía.\n\n" +
                    "PROCEDIMIENTO\n" +
                    "1. Escuche el patrón completo.\n" +
                    "2. Distinga pulsos cortos (·) y largos (—).\n" +
                    "3. Consulte la tabla de la página siguiente.\n" +
                    "4. Comunique únicamente la letra confirmada.\n\n" +
                    "ADVERTENCIA: una selección incorrecta " +
                    "repetirá la emisión actual."
                ),
            };
        }

        private static void PopulateCodeTableDocument(
            DocumentData doc
        )
        {
            doc.VerticalAlignment =
                DocumentVerticalAlignment.Top;

            doc.InteractionPrompt =
                "Consultar códigos";

            string table =
                "PULSO CORTO = ·    PULSO LARGO = —\n\n" +
                "E   ·\n" +
                "T   —\n" +
                "A   · —\n" +
                "N   — ·\n" +
                "S   · · ·\n" +
                "M   — —\n" +
                "D   — · ·\n" +
                "U   · · —\n" +
                "G   — — ·\n" +
                "R   · — ·";

            doc.Sections = new[]
            {
                Section(
                    SectionType.Title,
                    "TABLA DE CÓDIGOS MORSE"
                ),
                Section(
                    SectionType.Body,
                    "REFERENCIA OPERATIVA // 02 DE 02\n\n" +
                    table +
                    "\n\nCOMUNIQUE SOLO LA LETRA CONFIRMADA."
                ),
            };
        }

        private static DocumentSection Section(
            SectionType type,
            string text,
            bool divider = false
        )
        {
            return new DocumentSection
            {
                Type = type,
                Text = text,
                ShowDivider = divider,
            };
        }

        private static void ConfigureFolderData(
            GuideFolderData folderData,
            DocumentData manualDoc,
            DocumentData codeTableDoc
        )
        {
            SerializedObject so =
                new(folderData);

            so.FindProperty(
                "folderId"
            ).stringValue =
                "folder.morse.manual";

            so.FindProperty(
                "displayName"
            ).stringValue =
                "SEÑALES ACÚSTICAS";

            so.FindProperty(
                "folderColor"
            ).colorValue =
                new Color(
                    0.16f,
                    0.28f,
                    0.42f,
                    1f
                );

            SerializedProperty documents =
                so.FindProperty(
                    "documents"
                );

            documents.arraySize = 2;

            SerializedProperty manualEntry =
                documents.GetArrayElementAtIndex(0);

            manualEntry.FindPropertyRelative(
                "document"
            ).objectReferenceValue =
                manualDoc;

            manualEntry.FindPropertyRelative(
                "note"
            ).objectReferenceValue =
                null;

            SerializedProperty tableEntry =
                documents.GetArrayElementAtIndex(1);

            tableEntry.FindPropertyRelative(
                "document"
            ).objectReferenceValue =
                codeTableDoc;

            tableEntry.FindPropertyRelative(
                "note"
            ).objectReferenceValue =
                null;

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(
                folderData
            );
        }

        private static void ConfigureItemData(ItemData itemData)
        {
            itemData.itemName = "Manual de Señales Acústicas Industriales";
            itemData.description =
                "Carpeta de dos páginas con el protocolo operativo y la " +
                "tabla de códigos Morse. Escanéala en la terminal del Guía.";
            itemData.isStackable = false;
            itemData.maxStack = 1;
            itemData.startsInInventory = false;
            itemData.heldScale = 0.72f;
            EditorUtility.SetDirty(itemData);
        }

        // =====================================================================
        //  INTEGRACIÓN / REGISTROS
        // =====================================================================

        private static void RunIntegratorIfPresent()
        {
            System.Type integrator = FindTypeByName(
                "EOS.EditorTools.GuideRoom.GuideFolderProjectIntegrator");

            if (integrator == null)
            {
                Debug.LogWarning(
                    "[MorsePuzzleBuilder] No se encontró GuideFolderProjectIntegrator. " +
                    "Registra el ItemData y el prefab manualmente.");
                return;
            }

            var method = integrator.GetMethod("RepairAll",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);
            method?.Invoke(null, null);
        }

        private static bool IsItemRegistered(ItemData item)
        {
            ItemRegistry[] registries =
                Object.FindObjectsByType<ItemRegistry>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (ItemRegistry registry in registries)
            {
                SerializedObject so = new(registry);
                SerializedProperty list = so.FindProperty("registeredItems");
                if (list == null) continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == item)
                        return true;
                }
            }
            return false;
        }

        private static bool IsPrefabInAnySpawnList(GameObject prefab)
        {
            NetworkManager[] managers =
                Object.FindObjectsByType<NetworkManager>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (NetworkManager manager in managers)
            {
                SerializedObject so = new(manager);
                SerializedProperty list = so.FindProperty("spawnPrefabs");
                if (list == null) continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == prefab)
                        return true;
                }
            }
            return false;
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        private static T LoadAsset<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static T LoadOrCreateAsset<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            GameObject go = new(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateCube(
            string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = pos;
            cube.transform.localScale = scale;
            Renderer r = cube.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return cube;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
        }

        private static Material GetOrCreateMaterial(string path, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            mat = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetSerialized(Object target, string prop, Object value)
        {
            SerializedObject so = new(target);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null)
            {
                p.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetSerialized(Object target, string prop, string value)
        {
            SerializedObject so = new(target);
            SerializedProperty p = so.FindProperty(prop);
            if (p != null)
            {
                p.stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetSerializedArray<T>(
            Object target, string prop, T[] values) where T : Object
        {
            SerializedObject so = new(target);
            SerializedProperty list = so.FindProperty(prop);
            if (list == null) return;
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignUniqueItemIdIfNeeded(ItemData target)
        {
            SerializedObject so = new(target);
            SerializedProperty idProp = so.FindProperty("itemId");
            if (idProp != null && idProp.intValue >= 0) return;

            int highest = -1;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && item != target)
                    highest = Mathf.Max(highest, item.ItemId);
            }
            if (idProp != null)
            {
                idProp.intValue = highest + 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static System.Type FindTypeByName(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
