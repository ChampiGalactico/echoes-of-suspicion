using System.Collections.Generic;
using EOS.GuideRoom;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EOS.EditorTools.GuideRoom
{
    /// <summary>
    /// Genera una sala física de archivos para Carlos y doce carpetas
    /// escaneables. Los tres archivos activos del Puzzle 1 reflejan la
    /// implementación actual de la sala de Carmen; los archivos restantes
    /// preservan el diseño narrativo de los Puzzles 1, 2 y 3.
    /// </summary>
    public static class GuideArchiveRoomBuilder
    {
        private const string RoomName = "CarlosArchiveRoom";

        private const string DataFolder =
            "Assets/_Project/ScriptableObjects/GuideRoom/Archive/Carlos";

        private const string PrefabFolder =
            "Assets/_Project/Prefabs/GuideRoom/Archive/Carlos";

        private const string MaterialFolder =
            "Assets/_Project/Art/Materials/GuideRoom/Archive/Carlos";

        private readonly struct FolderSpec
        {
            public readonly string Key;
            public readonly string DisplayName;
            public readonly string Title;
            public readonly string Subtitle;
            public readonly string Body;
            public readonly Color Color;
            public readonly string Zone;

            public FolderSpec(
                string key,
                string displayName,
                string title,
                string subtitle,
                string body,
                Color color,
                string zone
            )
            {
                Key = key;
                DisplayName = displayName;
                Title = title;
                Subtitle = subtitle;
                Body = body;
                Color = color;
                Zone = zone;
            }
        }

        [MenuItem("EOS/Guide Room/Build Carlos Archive Room")]
        public static void BuildArchiveRoom()
        {
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            FolderSpec[] specs = CreateFolderSpecs();
            Dictionary<string, GameObject> prefabs = new();

            foreach (FolderSpec spec in specs)
            {
                prefabs[spec.Key] = BuildFolderAssets(spec);
            }

            Transform parent = ResolveParent();
            Transform existing = FindExistingRoom(parent);

            if (
                existing != null &&
                !EditorUtility.DisplayDialog(
                    "Sala de archivos de Carlos",
                    "Ya existe CarlosArchiveRoom. Se reconstruirá la sala " +
                    "y se conservarán los assets generados.",
                    "Reconstruir",
                    "Cancelar"
                )
            )
            {
                return;
            }

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject room = new(RoomName);
            Undo.RegisterCreatedObjectUndo(room, "Build Carlos Archive Room");

            if (parent != null)
            {
                room.transform.SetParent(parent, false);
            }

            BuildRoomShell(room.transform);

            Transform puzzleOneZone = BuildArchiveZone(
                room.transform,
                "P01_OrdenTrabajo",
                "P01 // ORDEN DE TRABAJO",
                new Vector3(-3.25f, 0f, 2.75f),
                3.0f
            );

            Transform puzzleTwoZone = BuildArchiveZone(
                room.transform,
                "P02_DiagnosticoSinPulso",
                "P02 // DIAGNÓSTICO SIN PULSO",
                new Vector3(0f, 0f, 2.75f),
                2.6f
            );

            Transform puzzleThreeZone = BuildArchiveZone(
                room.transform,
                "P03_HoraQueNoAvanza",
                "P03 // LA HORA QUE NO AVANZA",
                new Vector3(3.25f, 0f, 2.75f),
                3.0f
            );

            PlacePuzzleOneFolders(puzzleOneZone, specs, prefabs);
            PlacePuzzleTwoFolders(puzzleTwoZone, specs, prefabs);
            PlacePuzzleThreeFolders(puzzleThreeZone, specs, prefabs);

            GuideFolderProjectIntegrator.RepairAll();

            EditorSceneManager.MarkSceneDirty(room.scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = room;

            Debug.Log(
                "[GuideArchiveRoomBuilder] Sala construida con " +
                $"{specs.Length} carpetas escaneables."
            );
        }

        private static FolderSpec[] CreateFolderSpecs()
        {
            Color activeCritical = new(0.42f, 0.12f, 0.08f, 1f);
            Color reserve = new(0.18f, 0.28f, 0.14f, 1f);
            Color diagnostic = new(0.34f, 0.28f, 0.10f, 1f);
            Color memory = new(0.24f, 0.22f, 0.16f, 1f);

            return new[]
            {
                new FolderSpec(
                    "P01_Combustible_Activo",
                    "COMBUSTIBLE // ACTIVO",
                    "MANUAL — SISTEMA DE COMBUSTIBLE",
                    "ORDEN 00247 // CRÍTICO // PASO 1 DE 3",
                    "LECTURA QUE DEBE DESCRIBIR CARMEN:\n" +
                    "Perforación por oxidación en el tanque, fuga activa y " +
                    "filtro de combustible obstruido.\n\n" +
                    "RESPUESTA DE LA DEMO ACTUAL:\n" +
                    "Indicar el elemento identificado como FuelTank. Debe " +
                    "colocarse primero.\n\n" +
                    "SECUENCIA ACTUAL COMPLETA:\n" +
                    "1. FuelTank\n2. Unscrew\n3. RedDuctTape",
                    activeCritical,
                    "P01"
                ),
                new FolderSpec(
                    "P01_Bateria_Activo",
                    "BATERÍA // ACTIVO",
                    "MANUAL — BATERÍA Y CONEXIONES",
                    "ORDEN 00247 // CRÍTICO // PASO 2 DE 3",
                    "LECTURA QUE DEBE DESCRIBIR CARMEN:\n" +
                    "Terminales con corrosión severa, voltaje en reposo de " +
                    "9.3 V y caída inmediata bajo carga.\n\n" +
                    "RESPUESTA DE LA DEMO ACTUAL:\n" +
                    "Indicar Unscrew, representado por la herramienta de " +
                    "desatornillado. Debe colocarse después de FuelTank.\n\n" +
                    "SECUENCIA ACTUAL COMPLETA:\n" +
                    "1. FuelTank\n2. Unscrew\n3. RedDuctTape",
                    activeCritical,
                    "P01"
                ),
                new FolderSpec(
                    "P01_Motor_Activo",
                    "MOTOR // ACTIVO",
                    "MANUAL — MOTOR",
                    "ORDEN 00247 // CRÍTICO // PASO 3 DE 3",
                    "LECTURA QUE DEBE DESCRIBIR CARMEN:\n" +
                    "Compresión crítica en los cilindros 2 y 3, junta de " +
                    "culata quemada y señales de mezcla con refrigerante.\n\n" +
                    "RESPUESTA DE LA DEMO ACTUAL:\n" +
                    "Indicar RedDuctTape, la cinta adhesiva roja. Debe " +
                    "colocarse al final.\n\n" +
                    "SECUENCIA ACTUAL COMPLETA:\n" +
                    "1. FuelTank\n2. Unscrew\n3. RedDuctTape",
                    activeCritical,
                    "P01"
                ),
                CreateReserveSpec(
                    "P01_Frenos_Reserva",
                    "FRENOS // RESERVA",
                    "FRENOS",
                    reserve
                ),
                CreateReserveSpec(
                    "P01_Refrigeracion_Reserva",
                    "REFRIGERACIÓN // RESERVA",
                    "REFRIGERACIÓN",
                    reserve
                ),
                CreateReserveSpec(
                    "P01_Electrico_Reserva",
                    "ELÉCTRICO // RESERVA",
                    "SISTEMA ELÉCTRICO",
                    reserve
                ),
                CreateReserveSpec(
                    "P01_Transmision_Reserva",
                    "TRANSMISIÓN // RESERVA",
                    "TRANSMISIÓN",
                    reserve
                ),
                new FolderSpec(
                    "P02_TablaDiagnostico",
                    "TABLA DE DIAGNÓSTICO",
                    "DIAGNÓSTICO SIN PULSO",
                    "TEMPERATURA // VOLTAJE // PRESIÓN",
                    "MÓDULOS DISPONIBLES:\n" +
                    "Combustible, Batería, Refrigeración y Encendido.\n\n" +
                    "REGLAS:\n" +
                    "• Temperatura alta + voltaje estable: ajustar " +
                    "Refrigeración.\n" +
                    "• Presión baja + voltaje bajo: activar Batería antes " +
                    "de Combustible.\n" +
                    "• Si solo un indicador está fuera de Estable, reparar " +
                    "el módulo asociado.\n" +
                    "• Encendido siempre se activa al final.\n\n" +
                    "Carlos debe escuchar los tres valores, deducir la " +
                    "secuencia y comunicarla antes de que venza el tiempo.",
                    diagnostic,
                    "P02"
                ),
                new FolderSpec(
                    "P03_Memoria_0620",
                    "MEMORIA // 06:20",
                    "FRAGMENTO DE MEMORIA 01",
                    "06:20 // TRABAJO",
                    "A las 6:20 todavía estaba trabajando.\n\n" +
                    "Relacionar este fragmento con la fotografía en la que " +
                    "Carlos aparece trabajando.",
                    memory,
                    "P03"
                ),
                new FolderSpec(
                    "P03_Memoria_0705",
                    "MEMORIA // 07:05",
                    "FRAGMENTO DE MEMORIA 02",
                    "07:05 // FAMILIA",
                    "A las 7:05 recibí la llamada.\n\n" +
                    "Relacionar este fragmento con la fotografía de Carlos " +
                    "junto a su familia.",
                    memory,
                    "P03"
                ),
                new FolderSpec(
                    "P03_Memoria_0840",
                    "MEMORIA // 08:40",
                    "FRAGMENTO DE MEMORIA 03",
                    "08:40 // HOSPITAL",
                    "A las 8:40 el reloj dejó de importar.\n\n" +
                    "Relacionar este fragmento con la fotografía del pasillo " +
                    "del hospital.",
                    memory,
                    "P03"
                ),
                new FolderSpec(
                    "P03_NotaOrdenEmocional",
                    "NOTA // ORDEN EMOCIONAL",
                    "INSTRUCCIÓN MANUSCRITA",
                    "NO ES UNA SECUENCIA CRONOLÓGICA",
                    "No ordenes las horas según el reloj. Ordénalas según " +
                    "lo que perdiste primero.\n\n" +
                    "El reloj central debe quedar en la última hora después " +
                    "de activar las fotografías en el orden emocional.",
                    memory,
                    "P03"
                )
            };
        }

        private static FolderSpec CreateReserveSpec(
            string key,
            string displayName,
            string systemName,
            Color color
        )
        {
            return new FolderSpec(
                key,
                displayName,
                $"MANUAL — {systemName}",
                "ARCHIVO DE RESERVA // ORDEN DE TRABAJO",
                "Este sistema forma parte del conjunto de carpetas del " +
                "taller. Su lectura de diagnóstico, nivel de urgencia y " +
                "herramienta asociada deben definirse al activar la " +
                "randomización de tres sistemas por partida.\n\n" +
                "No contiene una respuesta inventada: permanece como señuelo " +
                "hasta que exista una pista y una herramienta equivalentes " +
                "en la sala de Carmen.",
                color,
                "P01"
            );
        }

        private static GameObject BuildFolderAssets(FolderSpec spec)
        {
            string documentPath = $"{DataFolder}/DOC_{spec.Key}.asset";
            string folderDataPath = $"{DataFolder}/FOLDER_{spec.Key}.asset";
            string itemDataPath = $"{DataFolder}/ITEM_{spec.Key}.asset";
            string prefabPath = $"{PrefabFolder}/PF_{spec.Key}.prefab";

            DocumentData document = GetOrCreateDocument(documentPath, spec);
            GuideFolderData folderData = GetOrCreateFolderData(
                folderDataPath,
                spec,
                document
            );

            ItemData itemData = GetOrCreateItemData(itemDataPath, spec);
            GameObject prefab = BuildFolderPrefab(
                prefabPath,
                spec,
                folderData,
                itemData
            );

            itemData.worldPrefab = prefab;
            itemData.heldModelPrefab = prefab;
            itemData.heldScale = 0.72f;

            EnsureUniqueItemId(itemData);

            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(folderData);
            EditorUtility.SetDirty(itemData);

            return prefab;
        }

        private static DocumentData GetOrCreateDocument(
            string path,
            FolderSpec spec
        )
        {
            DocumentData document =
                AssetDatabase.LoadAssetAtPath<DocumentData>(path);

            if (document == null)
            {
                document = ScriptableObject.CreateInstance<DocumentData>();
                AssetDatabase.CreateAsset(document, path);
            }

            document.VerticalAlignment = DocumentVerticalAlignment.Top;
            document.InteractionPrompt = "Leer archivo";
            document.Sections = new[]
            {
                new DocumentSection
                {
                    Type = SectionType.Title,
                    Text = spec.Title,
                    ShowDivider = true
                },
                new DocumentSection
                {
                    Type = SectionType.Subtitle,
                    Text = spec.Subtitle,
                    ShowDivider = true
                },
                new DocumentSection
                {
                    Type = SectionType.Body,
                    Text = spec.Body
                }
            };

            return document;
        }

        private static GuideFolderData GetOrCreateFolderData(
            string path,
            FolderSpec spec,
            DocumentData document
        )
        {
            GuideFolderData folderData =
                AssetDatabase.LoadAssetAtPath<GuideFolderData>(path);

            if (folderData == null)
            {
                folderData = ScriptableObject.CreateInstance<GuideFolderData>();
                AssetDatabase.CreateAsset(folderData, path);
            }

            SerializedObject serialized = new(folderData);
            serialized.FindProperty("folderId").stringValue =
                $"carlos.archive.{spec.Key.ToLowerInvariant()}";
            serialized.FindProperty("displayName").stringValue =
                spec.DisplayName;
            serialized.FindProperty("folderColor").colorValue = spec.Color;

            SerializedProperty documents =
                serialized.FindProperty("documents");
            documents.arraySize = 1;

            SerializedProperty entry =
                documents.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("document").objectReferenceValue = document;
            entry.FindPropertyRelative("note").objectReferenceValue = null;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return folderData;
        }

        private static ItemData GetOrCreateItemData(
            string path,
            FolderSpec spec
        )
        {
            ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (itemData == null)
            {
                itemData = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(itemData, path);
            }

            itemData.itemName = $"Carpeta: {spec.DisplayName}";
            itemData.description =
                "Carpeta física compatible con la terminal de archivos de Carlos.";
            itemData.isStackable = false;
            itemData.maxStack = 1;
            itemData.startsInInventory = false;
            itemData.heldScale = 0.72f;

            return itemData;
        }

        private static GameObject BuildFolderPrefab(
            string path,
            FolderSpec spec,
            GuideFolderData folderData,
            ItemData itemData
        )
        {
            Material folderMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_{spec.Key}.mat",
                spec.Color,
                0f,
                0.18f,
                false
            );

            Material labelMaterial = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_Label_{spec.Zone}.mat",
                new Color(0.76f, 0.73f, 0.52f, 1f),
                0f,
                0.12f,
                false
            );

            GameObject root = new($"PF_{spec.Key}");
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            root.layer = interactableLayer >= 0 ? interactableLayer : 0;

            root.AddComponent<NetworkIdentity>();

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.linearDamping = 1.5f;
            body.angularDamping = 2f;

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.035f, 0f);
            collider.size = new Vector3(0.72f, 0.09f, 0.48f);

            NetworkPickupItem pickup = root.AddComponent<NetworkPickupItem>();
            GuideFolderItem folderItem = root.AddComponent<GuideFolderItem>();

            CreateFolderVisual(
                root.transform,
                folderMaterial,
                labelMaterial,
                interactableLayer
            );

            SerializedObject pickupSerialized = new(pickup);
            pickupSerialized.FindProperty("itemData").objectReferenceValue = itemData;
            pickupSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject folderSerialized = new(folderItem);
            folderSerialized.FindProperty("folderData").objectReferenceValue =
                folderData;
            folderSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return prefab;
        }

        private static void CreateFolderVisual(
            Transform parent,
            Material folderMaterial,
            Material labelMaterial,
            int layer
        )
        {
            GameObject folderBase = CreateCube(
                "FolderBase",
                parent,
                new Vector3(0f, 0.03f, 0f),
                new Vector3(0.72f, 0.045f, 0.48f),
                folderMaterial,
                keepCollider: false
            );

            GameObject folderTop = CreateCube(
                "FolderTop",
                parent,
                new Vector3(0f, 0.065f, 0.015f),
                new Vector3(0.68f, 0.025f, 0.43f),
                folderMaterial,
                keepCollider: false
            );

            GameObject tab = CreateCube(
                "FolderTab",
                parent,
                new Vector3(-0.20f, 0.085f, 0.18f),
                new Vector3(0.24f, 0.035f, 0.10f),
                folderMaterial,
                keepCollider: false
            );

            GameObject label = CreateCube(
                "FolderLabel",
                parent,
                new Vector3(0f, 0.084f, -0.02f),
                new Vector3(0.42f, 0.009f, 0.18f),
                labelMaterial,
                keepCollider: false
            );

            folderBase.layer = layer >= 0 ? layer : 0;
            folderTop.layer = layer >= 0 ? layer : 0;
            tab.layer = layer >= 0 ? layer : 0;
            label.layer = layer >= 0 ? layer : 0;
        }

        private static void EnsureUniqueItemId(ItemData target)
        {
            SerializedObject targetSerialized = new(target);
            SerializedProperty idProperty =
                targetSerialized.FindProperty("itemId");

            if (idProperty == null)
            {
                Debug.LogError(
                    "[GuideArchiveRoomBuilder] ItemData no expone itemId.",
                    target
                );
                return;
            }

            HashSet<int> usedIds = new();
            int highestId = -1;
            string[] guids = AssetDatabase.FindAssets("t:ItemData");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);

                if (item == null || item == target)
                {
                    continue;
                }

                usedIds.Add(item.ItemId);
                highestId = Mathf.Max(highestId, item.ItemId);
            }

            if (idProperty.intValue < 0 || usedIds.Contains(idProperty.intValue))
            {
                idProperty.intValue = highestId + 1;
                targetSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Transform ResolveParent()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null || !selected.scene.IsValid())
            {
                return null;
            }

            return selected.transform;
        }

        private static Transform FindExistingRoom(Transform parent)
        {
            if (parent != null)
            {
                return parent.Find(RoomName);
            }

            GameObject existing = GameObject.Find(RoomName);
            return existing != null ? existing.transform : null;
        }

        private static void BuildRoomShell(Transform root)
        {
            Material wall = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_Archive_Wall.mat",
                new Color(0.055f, 0.065f, 0.055f, 1f),
                0.15f,
                0.18f,
                false
            );

            Material floor = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_Archive_Floor.mat",
                new Color(0.08f, 0.09f, 0.075f, 1f),
                0.25f,
                0.22f,
                false
            );

            Material metal = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_Archive_Metal.mat",
                new Color(0.12f, 0.16f, 0.12f, 1f),
                0.65f,
                0.28f,
                false
            );

            CreateCube(
                "Floor",
                root,
                new Vector3(0f, -0.12f, 0f),
                new Vector3(10.5f, 0.24f, 7.2f),
                floor,
                keepCollider: true
            );

            CreateCube(
                "BackWall",
                root,
                new Vector3(0f, 2.25f, 3.55f),
                new Vector3(10.5f, 4.5f, 0.20f),
                wall,
                keepCollider: true
            );

            CreateCube(
                "LeftWall",
                root,
                new Vector3(-5.25f, 2.25f, 0f),
                new Vector3(0.20f, 4.5f, 7.2f),
                wall,
                keepCollider: true
            );

            CreateCube(
                "RightWall",
                root,
                new Vector3(5.25f, 2.25f, 0f),
                new Vector3(0.20f, 4.5f, 7.2f),
                wall,
                keepCollider: true
            );

            CreateCube(
                "CeilingBeam",
                root,
                new Vector3(0f, 4.35f, 2.9f),
                new Vector3(10.5f, 0.18f, 0.22f),
                metal,
                keepCollider: true
            );

            CreateWorldLabel(
                "ArchiveTitle",
                root,
                "ARCHIVO TÉCNICO // CARLOS",
                new Vector3(0f, 3.85f, 3.40f),
                new Vector3(0f, 180f, 0f),
                4.8f
            );
        }

        private static Transform BuildArchiveZone(
            Transform parent,
            string name,
            string label,
            Vector3 localPosition,
            float width
        )
        {
            GameObject zone = new(name);
            Undo.RegisterCreatedObjectUndo(zone, $"Create {name}");
            zone.transform.SetParent(parent, false);
            zone.transform.localPosition = localPosition;

            Material metal = GetOrCreateMaterial(
                $"{MaterialFolder}/MAT_Archive_Metal.mat",
                new Color(0.12f, 0.16f, 0.12f, 1f),
                0.65f,
                0.28f,
                false
            );

            for (int shelfIndex = 0; shelfIndex < 3; shelfIndex++)
            {
                float height = 0.55f + shelfIndex * 0.90f;

                CreateCube(
                    $"Shelf_{shelfIndex + 1:00}",
                    zone.transform,
                    new Vector3(0f, height, 0f),
                    new Vector3(width, 0.10f, 0.95f),
                    metal,
                    keepCollider: true
                );
            }

            CreateCube(
                "LeftSupport",
                zone.transform,
                new Vector3(-width * 0.5f + 0.05f, 1.45f, 0f),
                new Vector3(0.10f, 2.8f, 0.90f),
                metal,
                keepCollider: true
            );

            CreateCube(
                "RightSupport",
                zone.transform,
                new Vector3(width * 0.5f - 0.05f, 1.45f, 0f),
                new Vector3(0.10f, 2.8f, 0.90f),
                metal,
                keepCollider: true
            );

            CreateWorldLabel(
                "ZoneLabel",
                zone.transform,
                label,
                new Vector3(0f, 3.12f, -0.02f),
                new Vector3(0f, 180f, 0f),
                2.4f
            );

            return zone.transform;
        }

        private static void PlacePuzzleOneFolders(
            Transform zone,
            IReadOnlyList<FolderSpec> specs,
            IReadOnlyDictionary<string, GameObject> prefabs
        )
        {
            List<FolderSpec> filtered = FilterByZone(specs, "P01");

            for (int index = 0; index < filtered.Count; index++)
            {
                int shelf = index < 3 ? 2 : index < 6 ? 1 : 0;
                int slot = index < 3 ? index : index < 6 ? index - 3 : 1;
                float x = -0.82f + slot * 0.82f;
                float y = 0.64f + shelf * 0.90f;

                PlaceFolder(
                    prefabs[filtered[index].Key],
                    zone,
                    filtered[index].DisplayName,
                    new Vector3(x, y, -0.02f),
                    Quaternion.Euler(0f, 0f, 0f)
                );
            }
        }

        private static void PlacePuzzleTwoFolders(
            Transform zone,
            IReadOnlyList<FolderSpec> specs,
            IReadOnlyDictionary<string, GameObject> prefabs
        )
        {
            List<FolderSpec> filtered = FilterByZone(specs, "P02");

            for (int index = 0; index < filtered.Count; index++)
            {
                PlaceFolder(
                    prefabs[filtered[index].Key],
                    zone,
                    filtered[index].DisplayName,
                    new Vector3(0f, 2.44f, -0.02f),
                    Quaternion.identity
                );
            }
        }

        private static void PlacePuzzleThreeFolders(
            Transform zone,
            IReadOnlyList<FolderSpec> specs,
            IReadOnlyDictionary<string, GameObject> prefabs
        )
        {
            List<FolderSpec> filtered = FilterByZone(specs, "P03");

            for (int index = 0; index < filtered.Count; index++)
            {
                int shelf = index < 2 ? 2 : 1;
                int slot = index % 2;
                float x = slot == 0 ? -0.55f : 0.55f;
                float y = 0.64f + shelf * 0.90f;

                PlaceFolder(
                    prefabs[filtered[index].Key],
                    zone,
                    filtered[index].DisplayName,
                    new Vector3(x, y, -0.02f),
                    Quaternion.identity
                );
            }
        }

        private static List<FolderSpec> FilterByZone(
            IReadOnlyList<FolderSpec> specs,
            string zone
        )
        {
            List<FolderSpec> filtered = new();

            foreach (FolderSpec spec in specs)
            {
                if (spec.Zone == zone)
                {
                    filtered.Add(spec);
                }
            }

            return filtered;
        }

        private static void PlaceFolder(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation
        )
        {
            if (prefab == null)
            {
                return;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (instance == null)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Place {name}");
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider
        )
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Undo.RegisterCreatedObjectUndo(cube, $"Create {name}");
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            Renderer renderer = cube.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = cube.GetComponent<Collider>();

                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return cube;
        }

        private static void CreateWorldLabel(
            string name,
            Transform parent,
            string textValue,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            float fontSize
        )
        {
            GameObject label = new(name);
            Undo.RegisterCreatedObjectUndo(label, $"Create {name}");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.Euler(localEulerAngles);

            TextMeshPro text = label.AddComponent<TextMeshPro>();
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.45f, 1f, 0.55f, 1f);
            text.rectTransform.sizeDelta = new Vector2(8f, 1.2f);
        }

        private static Material GetOrCreateMaterial(
            string path,
            Color baseColor,
            float metallic,
            float smoothness,
            bool emission
        )
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path)
                };

                AssetDatabase.CreateAsset(material, path);
            }

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

            if (emission)
            {
                material.EnableKeyword("_EMISSION");

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", baseColor * 2.5f);
                }
            }

            EditorUtility.SetDirty(material);
            return material;
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
