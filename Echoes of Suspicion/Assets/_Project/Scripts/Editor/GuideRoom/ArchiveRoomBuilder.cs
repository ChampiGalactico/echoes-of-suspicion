using System.Collections.Generic;
using EOS.GuideRoom;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EOS.EditorTools.GuideRoom
{
    /// <summary>
    /// Genera la SALA DE ARCHIVOS del Guía para el Bioma 1 (Taller de Carlos):
    ///
    /// - Un estante físico ("ArchiveShelf") bajo el anchor seleccionado.
    /// - Seis carpetas físicas de sistema (Motor, Combustible, Batería,
    ///   Frenos, Refrigeración, Transmisión) como prefabs recogibles,
    ///   compatibles con la bandeja-escáner (GuideFolderItem + NetworkPickupItem).
    /// - Cada prefab queda cableado a su GuideFolderData y su ItemData ya
    ///   incluidos en el paquete (carpeta Archive).
    ///
    /// Las tres carpetas RELEVANTES (Motor, Combustible, Batería) coinciden con
    /// los tres sistemas realmente implementados en el puzzle CarRepair del
    /// Corredor. Las otras tres son señuelo, tal como describe el diseño.
    ///
    /// Requiere que el paquete GuideFolderScanner ya esté en el proyecto
    /// (GuideFolderData / GuideFolderItem / FolderScannerDock).
    /// </summary>
    public static class ArchiveRoomBuilder
    {
        private const string ArchiveDataFolder =
            "Assets/_Project/ScriptableObjects/GuideRoom/Archive";

        private const string PrefabFolder =
            "Assets/_Project/Prefabs/GuideRoom/Archive";

        private const string MaterialFolder =
            "Assets/_Project/Art/Materials/GuideRoom/Archive";

        // Definición de las seis carpetas. El nombre de asset debe coincidir
        // con los .asset incluidos en el paquete.
        private struct FolderDef
        {
            public string key;
            public string folderAsset;   // GuideFolderData asset name
            public string itemAsset;     // ItemData asset name
            public string prefabName;
            public Color color;
            public bool relevant;
        }

        private static readonly FolderDef[] Folders =
        {
            new FolderDef{ key="Motor",        folderAsset="Folder_Motor",        itemAsset="Item_Folder_Motor",        prefabName="PF_GuideFolder_Motor",        color=new Color(0.62f,0.12f,0.10f,1f), relevant=true  },
            new FolderDef{ key="Combustible",  folderAsset="Folder_Combustible",  itemAsset="Item_Folder_Combustible",  prefabName="PF_GuideFolder_Combustible",  color=new Color(0.72f,0.55f,0.12f,1f), relevant=true  },
            new FolderDef{ key="Bateria",      folderAsset="Folder_Bateria",      itemAsset="Item_Folder_Bateria",      prefabName="PF_GuideFolder_Bateria",      color=new Color(0.72f,0.55f,0.12f,1f), relevant=true  },
            new FolderDef{ key="Frenos",       folderAsset="Folder_Frenos",       itemAsset="Item_Folder_Frenos",       prefabName="PF_GuideFolder_Frenos",       color=new Color(0.20f,0.42f,0.24f,1f), relevant=false },
            new FolderDef{ key="Refrigeracion",folderAsset="Folder_Refrigeracion",itemAsset="Item_Folder_Refrigeracion",prefabName="PF_GuideFolder_Refrigeracion",color=new Color(0.20f,0.42f,0.24f,1f), relevant=false },
            new FolderDef{ key="Transmision",  folderAsset="Folder_Transmision",  itemAsset="Item_Folder_Transmision",  prefabName="PF_GuideFolder_Transmision",  color=new Color(0.20f,0.42f,0.24f,1f), relevant=false },
        };

        [MenuItem("EOS/Guide Room/Archive/Build Archive Shelf")]
        private static void BuildShelf()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Archive Room",
                    "Selecciona el anchor de la sala de archivos en la Hierarchy " +
                    "(por ejemplo ArchiveAnchor bajo GuideRoom).",
                    "Entendido");
                return;
            }

            Transform anchor = ResolveAnchor(selected.transform);
            if (anchor == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Archive Shelf");

            Transform existing = anchor.Find("ArchiveShelf");
            GameObject shelf = existing != null
                ? existing.gameObject
                : CreateShelfRoot(anchor);

            ClearChildren(shelf.transform);
            BuildShelfVisuals(shelf);

            EditorSceneManager.MarkSceneDirty(shelf.scene);
            Selection.activeGameObject = shelf;
            Undo.CollapseUndoOperations(group);

            Debug.Log("[ArchiveRoomBuilder] Estante de archivos construido.");
        }

        [MenuItem("EOS/Guide Room/Archive/Build Archive Folders")]
        private static void BuildFolders()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            var createdPrefabs = new List<GameObject>();

            foreach (FolderDef def in Folders)
            {
                GuideFolderData folderData = LoadArchiveAsset<GuideFolderData>(def.folderAsset);
                ItemData itemData = LoadArchiveAsset<ItemData>(def.itemAsset);

                if (folderData == null || itemData == null)
                {
                    Debug.LogError(
                        $"[ArchiveRoomBuilder] Falta {def.folderAsset} o {def.itemAsset} " +
                        $"en {ArchiveDataFolder}. ¿Importaste el paquete completo?");
                    continue;
                }

                GameObject prefab = BuildFolderPrefab(def, folderData, itemData);
                if (prefab != null)
                {
                    createdPrefabs.Add(prefab);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshLoadedItemRegistries();

            Debug.Log(
                $"[ArchiveRoomBuilder] {createdPrefabs.Count} carpetas de sistema " +
                "creadas en " + PrefabFolder);
        }

        [MenuItem("EOS/Guide Room/Archive/Build Complete Archive Room")]
        private static void BuildComplete()
        {
            BuildShelf();
            BuildFolders();
        }

        // ─────────────────────────────────────────────────────────────

        private static GameObject BuildFolderPrefab(
            FolderDef def,
            GuideFolderData folderData,
            ItemData itemData)
        {
            Material folderMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_Folder_{def.key}.mat",
                def.color, metallic: 0f, smoothness: 0.16f);

            Material labelMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_Folder_Label.mat",
                new Color(0.80f, 0.78f, 0.62f, 1f), metallic: 0f, smoothness: 0.10f);

            GameObject folderObject = new(def.prefabName);
            Undo.RegisterCreatedObjectUndo(folderObject, "Create Archive Folder");

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

            GameObject folderBase = CreateCube("FolderBase", folderObject.transform,
                new Vector3(0f, 0.03f, 0f), new Vector3(0.72f, 0.045f, 0.48f), folderMaterial);
            RemoveCollider(folderBase);

            GameObject folderTop = CreateCube("FolderTop", folderObject.transform,
                new Vector3(0f, 0.065f, 0.015f), new Vector3(0.68f, 0.025f, 0.43f), folderMaterial);
            RemoveCollider(folderTop);

            GameObject tab = CreateCube("FolderTab", folderObject.transform,
                new Vector3(-0.20f, 0.085f, 0.18f), new Vector3(0.24f, 0.035f, 0.10f), folderMaterial);
            RemoveCollider(tab);

            GameObject label = CreateCube("FolderLabel", folderObject.transform,
                new Vector3(0f, 0.084f, -0.02f), new Vector3(0.42f, 0.009f, 0.18f), labelMaterial);
            RemoveCollider(label);

            // Wire ItemData into pickup.
            SerializedObject pickupSO = new(pickup);
            pickupSO.FindProperty("itemData").objectReferenceValue = itemData;
            pickupSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire GuideFolderData into the folder identity.
            SerializedObject folderSO = new(folderItem);
            folderSO.FindProperty("folderData").objectReferenceValue = folderData;
            folderSO.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath = $"{PrefabFolder}/{def.prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(folderObject, prefabPath);
            Object.DestroyImmediate(folderObject);

            // Point ItemData's world/held prefab back at the generated prefab.
            itemData.worldPrefab = prefab;
            itemData.heldModelPrefab = prefab;
            itemData.heldScale = 0.72f;
            AssignUniqueItemIdIfNeeded(itemData);
            EditorUtility.SetDirty(itemData);

            return prefab;
        }

        private static Transform ResolveAnchor(Transform selected)
        {
            if (selected.name == "ArchiveAnchor")
            {
                return selected;
            }

            Transform child = selected.Find("ArchiveAnchor");
            if (child != null)
            {
                return child;
            }

            bool create = EditorUtility.DisplayDialog(
                "Archive Room",
                "El objeto seleccionado no contiene ArchiveAnchor. ¿Crearlo como hijo?",
                "Crear", "Cancelar");

            if (!create)
            {
                return null;
            }

            GameObject anchor = new("ArchiveAnchor");
            Undo.RegisterCreatedObjectUndo(anchor, "Create ArchiveAnchor");
            anchor.transform.SetParent(selected, false);
            return anchor.transform;
        }

        private static void ClearChildren(Transform root)
        {
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Undo.DestroyObjectImmediate(root.GetChild(index).gameObject);
            }
        }

        private static GameObject CreateShelfRoot(Transform anchor)
        {
            GameObject root = new("ArchiveShelf");
            Undo.RegisterCreatedObjectUndo(root, "Create ArchiveShelf");
            root.transform.SetParent(anchor, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static void BuildShelfVisuals(GameObject root)
        {
            EnsureFolder(MaterialFolder);

            Material frameMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_ShelfFrame.mat",
                new Color(0.13f, 0.11f, 0.09f, 1f), metallic: 0.35f, smoothness: 0.20f);

            Material plankMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_ShelfPlank.mat",
                new Color(0.22f, 0.16f, 0.11f, 1f), metallic: 0.05f, smoothness: 0.12f);

            // Two side panels.
            RemoveCollider(CreateCube("SideLeft", root.transform,
                new Vector3(-1.1f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.5f), frameMaterial));
            RemoveCollider(CreateCube("SideRight", root.transform,
                new Vector3(1.1f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.5f), frameMaterial));
            RemoveCollider(CreateCube("Back", root.transform,
                new Vector3(0f, 1.0f, 0.24f), new Vector3(2.26f, 2.0f, 0.04f), frameMaterial));

            // Four shelves.
            for (int i = 0; i < 4; i++)
            {
                float y = 0.30f + i * 0.5f;
                RemoveCollider(CreateCube($"Plank_{i}", root.transform,
                    new Vector3(0f, y, 0f), new Vector3(2.2f, 0.05f, 0.48f), plankMaterial));
            }

            // Spawn markers where the six folder prefabs can be placed by hand.
            for (int i = 0; i < Folders.Length; i++)
            {
                GameObject marker = new($"FolderSlot_{Folders[i].key}");
                Undo.RegisterCreatedObjectUndo(marker, "Create FolderSlot");
                marker.transform.SetParent(root.transform, false);
                float shelfY = 0.55f + (i / 3) * 0.5f;
                float x = -0.7f + (i % 3) * 0.7f;
                marker.transform.localPosition = new Vector3(x, shelfY, 0f);
            }
        }

        // ── Helpers (idénticos en espíritu a FolderScannerBuilder) ──

        private static T LoadArchiveAsset<T>(string assetName) where T : Object
        {
            string path = $"{ArchiveDataFolder}/{assetName}.asset";
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void AssignUniqueItemIdIfNeeded(ItemData target)
        {
            SerializedObject so = new(target);
            SerializedProperty idProp = so.FindProperty("itemId");

            if (idProp != null && idProp.intValue >= 0 && !IdCollides(target, idProp.intValue))
            {
                return;
            }

            int highest = -1;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && item != target)
                {
                    highest = Mathf.Max(highest, item.ItemId);
                }
            }

            if (idProp != null)
            {
                idProp.intValue = highest + 1;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool IdCollides(ItemData target, int id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && item != target && item.ItemId == id)
                {
                    return true;
                }
            }
            return false;
        }

        private static void RefreshLoadedItemRegistries()
        {
            ItemRegistry[] registries = Object.FindObjectsByType<ItemRegistry>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            List<ItemData> allItems = new();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null)
                {
                    allItems.Add(item);
                }
            }
            allItems.Sort((a, b) => a.ItemId.CompareTo(b.ItemId));

            foreach (ItemRegistry registry in registries)
            {
                SerializedObject so = new(registry);
                SerializedProperty list = so.FindProperty("registeredItems");
                if (list == null)
                {
                    continue;
                }

                list.arraySize = allItems.Count;
                for (int i = 0; i < allItems.Count; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = allItems[i];
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(registry);
            }
        }

        private static GameObject CreateCube(
            string name, Transform parent, Vector3 localPos, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Undo.RegisterCreatedObjectUndo(cube, $"Create {name}");
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            return cube;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Material GetOrCreateMaterial(
            string path, Color baseColor, float metallic, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path)
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
            material.color = baseColor;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
