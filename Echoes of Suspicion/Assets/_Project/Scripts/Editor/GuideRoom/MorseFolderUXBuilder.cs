using System.Collections.Generic;
using System.Text;
using EOS.GuideRoom;
using Mirror;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EOS.EditorTools.GuideRoom
{
    /// <summary>
    /// Herramientas de Editor para el pulido UX de carpetas/inventario/Morse.
    ///
    /// Menús:
    ///   EOS > Morse & Folders > Create or Refresh Assets
    ///   EOS > Morse & Folders > Repair Folder Held Visuals
    ///   EOS > Morse & Folders > Apply Inventory Notification HUD
    ///   EOS > Morse & Folders > Validate Complete Setup
    ///
    /// Todos los builders son idempotentes y registran Undo donde tocan escena.
    /// No modifican escenas al importar; solo desde estos menús explícitos.
    /// </summary>
    public static class MorseFolderUXBuilder
    {
        private const string HeldVisualFolder =
            "Assets/_Project/Prefabs/GuideRoom/Folders";

        private const string MaterialFolder =
            "Assets/_Project/Art/Materials/GuideRoom";

        private const string HeldVisualName = "PF_GuideFolder_HeldVisual";

        private const bool VerboseLogging = false;

        // =====================================================================
        //  MENU 1 — Create or Refresh Assets
        // =====================================================================

        [MenuItem("EOS/Morse & Folders/Create or Refresh Assets")]
        public static void CreateOrRefreshAssets()
        {
            // El puzzle Morse y su carpeta (dos documentos) se generan con el
            // menú ya existente EOS > Puzzles > Morse > Create or Refresh Morse
            // Assets. Aquí solo garantizamos las piezas UX de este paquete:
            //   - el visual de mano compartido,
            //   - los ajustes de legibilidad de la terminal,
            //   - las etiquetas de letra de los paneles.
            EnsureHeldVisualPrefab();
            RefreshTerminalReadabilityInOpenScene();
            RefreshMorsePanelLabelsInOpenScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[MorseFolderUX] Assets UX creados/actualizados. Recuerda " +
                "ejecutar también 'EOS > Puzzles > Morse > Create or Refresh " +
                "Morse Assets' para dividir la carpeta en dos documentos.");
        }

        // =====================================================================
        //  MENU 2 — Repair Folder Held Visuals
        // =====================================================================

        [MenuItem("EOS/Morse & Folders/Repair Folder Held Visuals")]
        public static void RepairFolderHeldVisuals()
        {
            GameObject heldVisual = EnsureHeldVisualPrefab();
            if (heldVisual == null)
            {
                Debug.LogError(
                    "[MorseFolderUX] No se pudo crear/obtener el visual de mano.");
                return;
            }

            List<string> repaired = new();
            List<string> failed = new();

            foreach (ItemData item in FindAllFolderItemData())
            {
                Color tint;
                bool hasTint = TryResolveFolderColor(item, out tint);

                bool changed = false;

                if (item.heldModelPrefab == null)
                {
                    item.heldModelPrefab = heldVisual;
                    changed = true;
                }

                if (item.heldScale <= 0f)
                {
                    item.heldScale = 0.72f;
                    changed = true;
                }

                if (hasTint && !item.useHeldTint)
                {
                    item.useHeldTint = true;
                    item.heldTint = tint;
                    changed = true;
                }
                else if (hasTint && item.useHeldTint &&
                         item.heldTint != tint)
                {
                    item.heldTint = tint;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(item);
                    repaired.Add(item.name);
                }
            }

            AssetDatabase.SaveAssets();

            StringBuilder sb = new();
            sb.AppendLine("=== Repair Folder Held Visuals ===");
            sb.AppendLine($"Reparados: {repaired.Count}");
            foreach (string r in repaired) sb.AppendLine("  • " + r);
            if (failed.Count > 0)
            {
                sb.AppendLine($"No reparables: {failed.Count}");
                foreach (string f in failed) sb.AppendLine("  ✗ " + f);
            }
            Debug.Log(sb.ToString());
        }

        // =====================================================================
        //  MENU 3 — Apply Inventory Notification HUD
        // =====================================================================

        [MenuItem("EOS/Morse & Folders/Apply Inventory Notification HUD")]
        public static void ApplyInventoryNotificationHUD()
        {
            List<GameObject> playerPrefabs = FindPlayerPrefabsFromNetworkManager();
            if (playerPrefabs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Inventory Notification",
                    "No se encontraron prefabs de jugador en el NetworkManager. " +
                    "Revisa EOSNetworkSession / playerPrefab / spawnPrefabs.",
                    "Entendido");
                return;
            }

            StringBuilder report = new();
            report.AppendLine("=== Apply Inventory Notification HUD ===");

            foreach (GameObject prefab in playerPrefabs)
            {
                string result = ApplyNotificationToPlayerPrefab(prefab);
                report.AppendLine($"  {prefab.name}: {result}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
        }

        private static string ApplyNotificationToPlayerPrefab(GameObject prefabAsset)
        {
            string path = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(path))
            {
                return "sin ruta de asset";
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                NetworkRatInteractionHUD hud =
                    root.GetComponentInChildren<NetworkRatInteractionHUD>(true);

                if (hud == null)
                {
                    return "no tiene NetworkRatInteractionHUD (omitido)";
                }

                SerializedObject so = new(hud);
                SerializedProperty textProp =
                    so.FindProperty("inventoryNotificationText");
                SerializedProperty groupProp =
                    so.FindProperty("inventoryNotificationGroup");

                if (textProp == null)
                {
                    return "el HUD no expone inventoryNotificationText " +
                           "(¿aplicaste el script modificado?)";
                }

                if (textProp.objectReferenceValue != null)
                {
                    return "ya configurado (idempotente)";
                }

                // Crear el elemento visual dentro del canvas del HUD.
                Canvas hudCanvas = hud.GetComponentInChildren<Canvas>(true);
                if (hudCanvas == null)
                {
                    return "no se encontró Canvas del HUD";
                }

                GameObject notifGo = new("InventoryNotification",
                    typeof(RectTransform), typeof(CanvasGroup));
                notifGo.transform.SetParent(hudCanvas.transform, false);

                RectTransform rect = notifGo.GetComponent<RectTransform>();
                // Abajo-centro, sin tapar el centro ni el prompt.
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 140f);
                rect.sizeDelta = new Vector2(680f, 90f);

                GameObject textGo = new("Text",
                    typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                textGo.transform.SetParent(notifGo.transform, false);
                RectTransform textRect = textGo.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                TMPro.TextMeshProUGUI tmp =
                    textGo.GetComponent<TMPro.TextMeshProUGUI>();
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontSize = 26f;
                tmp.color = new Color(0.30f, 1f, 0.45f, 1f); // verde neón sobrio
                tmp.text = "GUARDADO EN EL INVENTARIO";
                tmp.raycastTarget = false;

                CanvasGroup group = notifGo.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;

                textProp.objectReferenceValue = tmp;
                if (groupProp != null)
                {
                    groupProp.objectReferenceValue = group;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return "notificación añadida";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =====================================================================
        //  MENU 4 — Validate Complete Setup
        // =====================================================================

        [MenuItem("EOS/Morse & Folders/Validate Complete Setup")]
        public static void ValidateCompleteSetup()
        {
            List<string> errors = new();
            List<string> warnings = new();

            // ── Carpeta Morse y documentos ──
            GuideFolderData morseFolder = FindAssetByName<GuideFolderData>(
                "MorseGuideFolder");
            if (morseFolder == null)
            {
                warnings.Add("No se encontró MorseGuideFolder (ejecuta el " +
                             "builder del Morse).");
            }
            else
            {
                int count = morseFolder.DocumentCount;
                if (count < 2)
                {
                    warnings.Add($"MorseGuideFolder tiene {count} documento(s). " +
                                 "Se esperaban 2 (instrucciones + tabla). " +
                                 "Ejecuta 'EOS > Puzzles > Morse > Create or " +
                                 "Refresh Morse Assets'.");
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (morseFolder.GetDocument(i).IsEmpty)
                            errors.Add($"MorseGuideFolder: documento {i} vacío.");
                    }
                }
            }

            // ── ItemData de carpetas: held visual ──
            foreach (ItemData item in FindAllFolderItemData())
            {
                if (item.ItemId < 0)
                    warnings.Add($"ItemData '{item.name}' sin itemId asignado.");

                if (item.heldModelPrefab == null && item.worldPrefab == null)
                    errors.Add($"ItemData '{item.name}': heldModelPrefab y " +
                               "worldPrefab son nulos → invisible en mano.");

                if (item.heldScale <= 0f)
                    errors.Add($"ItemData '{item.name}': heldScale <= 0.");

                if (!IsItemRegistered(item))
                    warnings.Add($"ItemData '{item.name}' no está en ningún " +
                                 "ItemRegistry cargado.");
            }

            // ── Held visual prefab ──
            GameObject held = FindAssetByName<GameObject>(HeldVisualName);
            if (held == null)
            {
                warnings.Add($"No existe {HeldVisualName} (ejecuta 'Repair " +
                             "Folder Held Visuals').");
            }
            else
            {
                if (held.GetComponentInChildren<Renderer>(true) == null)
                    errors.Add($"{HeldVisualName} no tiene Renderer.");
                if (held.GetComponentInChildren<NetworkIdentity>(true) != null)
                    errors.Add($"{HeldVisualName} tiene NetworkIdentity (debe " +
                               "ser visual puro).");
                if (held.GetComponentInChildren<Collider>(true) != null)
                    warnings.Add($"{HeldVisualName} tiene Collider (mejor sin él).");
            }

            // ── Hold socket en jugadores ──
            foreach (GameObject player in FindPlayerPrefabsFromNetworkManager())
            {
                if (player.GetComponentInChildren<RatHoldSocketProvider>(true) == null)
                    warnings.Add($"Player '{player.name}' sin RatHoldSocketProvider.");

                NetworkRatInteractionHUD hud =
                    player.GetComponentInChildren<NetworkRatInteractionHUD>(true);
                if (hud == null)
                {
                    warnings.Add($"Player '{player.name}' sin " +
                                 "NetworkRatInteractionHUD.");
                }
                else
                {
                    SerializedObject so = new(hud);
                    SerializedProperty p =
                        so.FindProperty("inventoryNotificationText");
                    if (p == null || p.objectReferenceValue == null)
                        warnings.Add($"Player '{player.name}': HUD sin " +
                                     "inventoryNotificationText (ejecuta 'Apply " +
                                     "Inventory Notification HUD').");
                }
            }

            // ── Morse en escena: paneles/emisores/coordinator/trigger ──
            ValidateMorseSceneObjects(errors, warnings);

            // ── PuzzleDoor vs llave ──
            ValidateDoorKeyConflicts(warnings);

            // ── Reporte ──
            StringBuilder sb = new();
            sb.AppendLine("=== Validate Complete Setup (Morse & Folders) ===");
            sb.AppendLine(errors.Count == 0
                ? "ERRORES: ninguno"
                : $"ERRORES ({errors.Count}):");
            foreach (string e in errors) sb.AppendLine("  ✗ " + e);
            if (warnings.Count > 0)
            {
                sb.AppendLine($"ADVERTENCIAS ({warnings.Count}):");
                foreach (string w in warnings) sb.AppendLine("  • " + w);
            }

            if (errors.Count > 0) Debug.LogError(sb.ToString());
            else if (warnings.Count > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        private static void ValidateMorseSceneObjects(
            List<string> errors, List<string> warnings)
        {
            var coordinators = Object.FindObjectsByType<
                EOS.Puzzles.Morse.MorsePuzzleCoordinator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (coordinators.Length == 0)
            {
                warnings.Add("No hay MorsePuzzleCoordinator en la escena abierta.");
                return;
            }

            var panels = Object.FindObjectsByType<EOS.Puzzles.Morse.MorsePanel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            HashSet<string> seen = new();
            foreach (var panel in panels)
            {
                SerializedObject so = new(panel);
                SerializedProperty labelProp = so.FindProperty("symbolLabel");
                if (labelProp != null && labelProp.objectReferenceValue == null)
                    warnings.Add($"Panel '{panel.name}' sin etiqueta de letra " +
                                 "(symbolLabel).");
                seen.Add(panel.SymbolId);
            }

            foreach (string sym in new[] {"E","T","A","N","S","M","D","U","G","R"})
                if (!seen.Contains(sym))
                    warnings.Add($"Falta panel para el símbolo '{sym}'.");
        }

        private static void ValidateDoorKeyConflicts(List<string> warnings)
        {
            // Advertir si una InteractableDoor sigue exigiendo llave mientras
            // podría estar vinculada al Morse vía PuzzleDoor. No modificar nada.
            var doors = Object.FindObjectsByType<InteractableDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var door in doors)
            {
                SerializedObject so = new(door);
                SerializedProperty req = so.FindProperty("requiredItem");
                if (req != null && req.objectReferenceValue != null)
                {
                    warnings.Add($"InteractableDoor '{door.name}' exige un item " +
                                 "(llave). Si está vinculada al Morse vía " +
                                 "PuzzleDoor, revisa que no quede doblemente " +
                                 "bloqueada. (No se modificó nada.)");
                }
            }
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        private static GameObject EnsureHeldVisualPrefab()
        {
            EnsureFolder(HeldVisualFolder);
            EnsureFolder(MaterialFolder);

            string path = $"{HeldVisualFolder}/{HeldVisualName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing; // idempotente
            }

            Material mat = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_GuideFolder_HeldVisual.mat",
                new Color(0.80f, 0.78f, 0.62f, 1f));

            // Cuerpo de carpeta simple (sin scripts, sin física, sin red).
            GameObject root = new(HeldVisualName);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.30f, 0.02f, 0.22f);
            RemoveCollider(body);
            body.GetComponent<Renderer>().sharedMaterial = mat;

            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Cover";
            cover.transform.SetParent(root.transform, false);
            cover.transform.localPosition = new Vector3(0f, 0.012f, 0.006f);
            cover.transform.localScale = new Vector3(0.285f, 0.008f, 0.20f);
            RemoveCollider(cover);
            cover.GetComponent<Renderer>().sharedMaterial = mat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            if (VerboseLogging)
                Debug.Log($"[MorseFolderUX] Creado {HeldVisualName}.");

            return prefab;
        }

        private static void RefreshTerminalReadabilityInOpenScene()
        {
            foreach (var terminal in Object.FindObjectsByType<GuideTerminalView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                terminal.ApplyReadabilitySettings();
                EditorUtility.SetDirty(terminal);
            }
        }

        private static void RefreshMorsePanelLabelsInOpenScene()
        {
            foreach (var panel in Object.FindObjectsByType<
                EOS.Puzzles.Morse.MorsePanel>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                EditorUtility.SetDirty(panel);
            }
        }

        private static List<ItemData> FindAllFolderItemData()
        {
            List<ItemData> result = new();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item == null) continue;

                // Es "de carpeta" si su prefab tiene GuideFolderItem, o si su
                // nombre lo indica.
                bool isFolder = false;
                GameObject wp = item.worldPrefab != null
                    ? item.worldPrefab
                    : item.heldModelPrefab;
                if (wp != null && wp.GetComponentInChildren<GuideFolderItem>(true) != null)
                    isFolder = true;
                if (!isFolder && (item.name.Contains("Folder") ||
                                   item.name.Contains("GuideFolder")))
                    isFolder = true;

                if (isFolder) result.Add(item);
            }
            return result;
        }

        private static bool TryResolveFolderColor(ItemData item, out Color color)
        {
            color = Color.white;
            GameObject wp = item.worldPrefab != null
                ? item.worldPrefab
                : item.heldModelPrefab;
            if (wp == null) return false;

            GuideFolderItem folderItem =
                wp.GetComponentInChildren<GuideFolderItem>(true);
            if (folderItem != null && folderItem.FolderData != null)
            {
                color = folderItem.FolderData.FolderColor;
                return true;
            }
            return false;
        }

        private static bool IsItemRegistered(ItemData item)
        {
            foreach (var registry in Object.FindObjectsByType<ItemRegistry>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (ItemData registered in registry.AllItems)
                    if (registered == item) return true;
            }
            // También revisar el registry embebido en prefabs (EOSNetworkSession).
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                ItemRegistry reg = go.GetComponentInChildren<ItemRegistry>(true);
                if (reg == null) continue;
                foreach (ItemData registered in reg.AllItems)
                    if (registered == item) return true;
            }
            return false;
        }

        private static List<GameObject> FindPlayerPrefabsFromNetworkManager()
        {
            List<GameObject> result = new();

            // Buscar el prefab del NetworkManager (EOSNetworkSession) y leer
            // playerPrefab + spawnPrefabs que tengan NetworkRatInteractionHUD.
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                NetworkManager nm = go.GetComponentInChildren<NetworkManager>(true);
                if (nm == null) continue;

                SerializedObject so = new(nm);
                SerializedProperty playerPrefab = so.FindProperty("playerPrefab");
                if (playerPrefab != null &&
                    playerPrefab.objectReferenceValue is GameObject pp &&
                    pp.GetComponentInChildren<NetworkRatInteractionHUD>(true) != null &&
                    !result.Contains(pp))
                {
                    result.Add(pp);
                }
            }
            return result;
        }

        private static T FindAssetByName<T>(string assetName) where T : Object
        {
            string filter = typeof(GameObject).IsAssignableFrom(typeof(T))
                ? $"{assetName} t:Prefab"
                : $"{assetName} t:{typeof(T).Name}";
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null &&
                    System.IO.Path.GetFileNameWithoutExtension(path) == assetName)
                    return asset;
            }
            return null;
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

        private static void RemoveCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
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
    }
}
