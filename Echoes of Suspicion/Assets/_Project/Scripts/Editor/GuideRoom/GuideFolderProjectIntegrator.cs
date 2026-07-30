using System.Collections.Generic;
using EOS.GuideRoom;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EOS.EditorTools.GuideRoom
{
    /// <summary>
    /// Sincroniza las carpetas del Guía con los dos registros que exige
    /// el flujo de inventario en red:
    /// 1) ItemRegistry debe conocer todos los ItemData.
    /// 2) NetworkManager debe registrar todos los prefabs de carpeta.
    ///
    /// También actualiza registros que viven dentro de prefabs persistentes,
    /// no solamente los componentes cargados en la escena abierta.
    /// </summary>
    public static class GuideFolderProjectIntegrator
    {
        private const string MenuPath =
            "EOS/Guide Room/Repair Folder Registry and Network Prefabs";

        [MenuItem(MenuPath)]
        public static void RepairAll()
        {
            List<ItemData> allItems = CollectAllItems();
            List<GameObject> folderPrefabs = CollectFolderPrefabs();

            ValidateItemIds(allItems);

            int sceneRegistries = RepairLoadedSceneRegistries(allItems);
            int sceneManagers = RepairLoadedSceneNetworkManagers(folderPrefabs);
            int prefabRegistries = 0;
            int prefabManagers = 0;

            RepairPersistentPrefabs(
                allItems,
                folderPrefabs,
                ref prefabRegistries,
                ref prefabManagers
            );

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[GuideFolderProjectIntegrator] Reparación finalizada. " +
                $"ItemData: {allItems.Count}; carpetas de red: {folderPrefabs.Count}; " +
                $"ItemRegistry en escenas: {sceneRegistries}; " +
                $"NetworkManager en escenas: {sceneManagers}; " +
                $"ItemRegistry en prefabs: {prefabRegistries}; " +
                $"NetworkManager en prefabs: {prefabManagers}."
            );
        }

        private static List<ItemData> CollectAllItems()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            List<ItemData> items = new(guids.Length);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

                if (item != null)
                {
                    items.Add(item);
                }
            }

            items.Sort((left, right) =>
            {
                int idComparison = left.ItemId.CompareTo(right.ItemId);

                return idComparison != 0
                    ? idComparison
                    : string.CompareOrdinal(left.name, right.name);
            });

            return items;
        }

        private static List<GameObject> CollectFolderPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            List<GameObject> folderPrefabs = new();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (
                    prefab != null &&
                    prefab.GetComponentInChildren<GuideFolderItem>(true) != null
                )
                {
                    folderPrefabs.Add(prefab);
                }
            }

            folderPrefabs.Sort((left, right) =>
                string.CompareOrdinal(
                    AssetDatabase.GetAssetPath(left),
                    AssetDatabase.GetAssetPath(right)
                )
            );

            return folderPrefabs;
        }

        private static int RepairLoadedSceneRegistries(
            IReadOnlyList<ItemData> allItems
        )
        {
            ItemRegistry[] registries =
                Object.FindObjectsByType<ItemRegistry>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (ItemRegistry registry in registries)
            {
                if (registry == null)
                {
                    continue;
                }

                WriteItemRegistry(registry, allItems);

                if (registry.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(
                        registry.gameObject.scene
                    );
                }
            }

            return registries.Length;
        }

        private static int RepairLoadedSceneNetworkManagers(
            IReadOnlyList<GameObject> folderPrefabs
        )
        {
            NetworkManager[] managers =
                Object.FindObjectsByType<NetworkManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (NetworkManager manager in managers)
            {
                if (manager == null)
                {
                    continue;
                }

                MergeSpawnPrefabs(manager, folderPrefabs);

                if (manager.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(
                        manager.gameObject.scene
                    );
                }
            }

            return managers.Length;
        }

        private static void RepairPersistentPrefabs(
            IReadOnlyList<ItemData> allItems,
            IReadOnlyList<GameObject> folderPrefabs,
            ref int repairedRegistries,
            ref int repairedManagers
        )
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefabAsset == null)
                {
                    continue;
                }

                bool hasRegistry =
                    prefabAsset.GetComponentInChildren<ItemRegistry>(true) != null;

                bool hasManager =
                    prefabAsset.GetComponentInChildren<NetworkManager>(true) != null;

                if (!hasRegistry && !hasManager)
                {
                    continue;
                }

                GameObject contents = null;

                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    bool changed = false;

                    ItemRegistry[] registries =
                        contents.GetComponentsInChildren<ItemRegistry>(true);

                    foreach (ItemRegistry registry in registries)
                    {
                        changed |= WriteItemRegistry(registry, allItems);
                        repairedRegistries++;
                    }

                    NetworkManager[] managers =
                        contents.GetComponentsInChildren<NetworkManager>(true);

                    foreach (NetworkManager manager in managers)
                    {
                        changed |= MergeSpawnPrefabs(
                            manager,
                            folderPrefabs
                        );

                        repairedManagers++;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                }
                finally
                {
                    if (contents != null)
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
            }
        }

        private static bool WriteItemRegistry(
            ItemRegistry registry,
            IReadOnlyList<ItemData> allItems
        )
        {
            SerializedObject serialized = new(registry);
            SerializedProperty list =
                serialized.FindProperty("registeredItems");

            if (list == null || !list.isArray)
            {
                Debug.LogError(
                    "[GuideFolderProjectIntegrator] No se encontró " +
                    $"registeredItems en {registry.name}.",
                    registry
                );

                return false;
            }

            bool changed = list.arraySize != allItems.Count;
            list.arraySize = allItems.Count;

            for (int index = 0; index < allItems.Count; index++)
            {
                SerializedProperty entry =
                    list.GetArrayElementAtIndex(index);

                if (entry.objectReferenceValue != allItems[index])
                {
                    entry.objectReferenceValue = allItems[index];
                    changed = true;
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(registry);
            }

            return changed;
        }

        private static bool MergeSpawnPrefabs(
            NetworkManager manager,
            IReadOnlyList<GameObject> folderPrefabs
        )
        {
            SerializedObject serialized = new(manager);
            SerializedProperty list =
                serialized.FindProperty("spawnPrefabs");

            if (list == null || !list.isArray)
            {
                Debug.LogError(
                    "[GuideFolderProjectIntegrator] No se encontró " +
                    $"spawnPrefabs en {manager.name}.",
                    manager
                );

                return false;
            }

            HashSet<GameObject> existing = new();
            bool changed = false;

            for (int index = list.arraySize - 1; index >= 0; index--)
            {
                SerializedProperty entry =
                    list.GetArrayElementAtIndex(index);

                GameObject prefab =
                    entry.objectReferenceValue as GameObject;

                if (prefab == null || existing.Contains(prefab))
                {
                    int previousSize = list.arraySize;
                    list.DeleteArrayElementAtIndex(index);

                    if (list.arraySize == previousSize)
                    {
                        list.DeleteArrayElementAtIndex(index);
                    }

                    changed = true;
                    continue;
                }

                existing.Add(prefab);
            }

            foreach (GameObject folderPrefab in folderPrefabs)
            {
                if (folderPrefab == null || existing.Contains(folderPrefab))
                {
                    continue;
                }

                int newIndex = list.arraySize;
                list.InsertArrayElementAtIndex(newIndex);
                list.GetArrayElementAtIndex(newIndex).objectReferenceValue =
                    folderPrefab;

                existing.Add(folderPrefab);
                changed = true;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }

            return changed;
        }

        private static void ValidateItemIds(
            IReadOnlyList<ItemData> allItems
        )
        {
            Dictionary<int, ItemData> firstById = new();

            foreach (ItemData item in allItems)
            {
                if (item == null)
                {
                    continue;
                }

                if (item.ItemId < 0)
                {
                    Debug.LogError(
                        "[GuideFolderProjectIntegrator] ItemData sin ID válido: " +
                        AssetDatabase.GetAssetPath(item),
                        item
                    );

                    continue;
                }

                if (firstById.TryGetValue(item.ItemId, out ItemData first))
                {
                    Debug.LogError(
                        "[GuideFolderProjectIntegrator] ID de item duplicado " +
                        $"({item.ItemId}): {first.name} y {item.name}. " +
                        "Corrige el ID antes de probar inventario en red.",
                        item
                    );
                }
                else
                {
                    firstById.Add(item.ItemId, item);
                }
            }
        }
    }
}
