using System.Collections.Generic;
using EOS.GuideRoom;
using Mirror;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EOS.EditorTools.GuideRoom
{
    /// <summary>
    /// Genera:
    /// - La bandeja-escáner industrial bajo FolderSlotAnchor.
    /// - Materiales base.
    /// - Una carpeta física de ejemplo.
    /// - ItemData, GuideFolderData y DocumentData de ejemplo.
    ///
    /// El resultado queda completamente editable.
    /// </summary>
    public static class FolderScannerBuilder
    {
        private const string MaterialFolder =
            "Assets/_Project/Art/Materials/GuideRoom/FolderScanner";

        private const string DataFolder =
            "Assets/_Project/ScriptableObjects/GuideRoom/Folders";

        private const string PrefabFolder =
            "Assets/_Project/Prefabs/GuideRoom/Folders";

        private const string PressSoundPath =
            "Assets/_Project/Audio/SFX/press_button.mp3";

        private const string SuccessSoundPath =
            "Assets/_Project/Audio/SFX/correct.mp3";

        private const string ErrorSoundPath =
            "Assets/_Project/Audio/SFX/wrong_button.mp3";

        [MenuItem(
            "EOS/Guide Room/Build Folder Scanner Tray")]
        private static void BuildScanner()
        {
            GameObject selected =
                Selection.activeGameObject;

            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Folder Scanner",
                    "Selecciona FolderSlotAnchor en la Hierarchy.",
                    "Entendido"
                );

                return;
            }

            Transform anchor =
                ResolveAnchor(selected.transform);

            if (anchor == null)
            {
                return;
            }

            Transform existing =
                anchor.Find("FolderScannerDock");

            if (
                existing != null &&
                !EditorUtility.DisplayDialog(
                    "Folder Scanner",
                    "Ya existe FolderScannerDock. " +
                    "Se reconstruirá su contenido visual.",
                    "Reconstruir",
                    "Cancelar"
                )
            )
            {
                return;
            }

            Undo.IncrementCurrentGroup();

            int undoGroup =
                Undo.GetCurrentGroup();

            Undo.SetCurrentGroupName(
                "Build Folder Scanner Tray"
            );

            GameObject root =
                existing != null
                    ? existing.gameObject
                    : CreateScannerRoot(anchor);

            ClearVisualChildren(root.transform);

            BuildScannerVisuals(root);

            ConfigureScannerComponents(root);

            EditorSceneManager.MarkSceneDirty(
                root.scene
            );

            Selection.activeGameObject = root;

            Undo.CollapseUndoOperations(
                undoGroup
            );

            Debug.Log(
                "[FolderScannerBuilder] " +
                "Bandeja-escáner construida."
            );
        }

        [MenuItem(
            "EOS/Guide Room/Create Sample Guide Folder")]
        private static void CreateSampleFolder()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);

            Material folderMaterial =
                GetOrCreateMaterial(
                    $"{MaterialFolder}/MAT_GuideFolder_Sample.mat",
                    new Color(
                        0.27f,
                        0.38f,
                        0.20f,
                        1f
                    ),
                    metallic: 0f,
                    smoothness: 0.18f,
                    emission: false
                );

            Material labelMaterial =
                GetOrCreateMaterial(
                    $"{MaterialFolder}/MAT_GuideFolder_Label.mat",
                    new Color(
                        0.78f,
                        0.76f,
                        0.58f,
                        1f
                    ),
                    metallic: 0f,
                    smoothness: 0.12f,
                    emission: false
                );

            string readablePath =
                $"{DataFolder}/SampleFolderDocument.asset";

            DocumentData readable =
                AssetDatabase.LoadAssetAtPath<
                    DocumentData>(readablePath);

            if (readable == null)
            {
                readable =
                    ScriptableObject.CreateInstance<
                        DocumentData>();

                readable.VerticalAlignment =
                    DocumentVerticalAlignment.Top;

                readable.Sections =
                    new[]
                    {
                        new DocumentSection
                        {
                            Type = SectionType.Title,
                            Text = "REGISTRO DE MANTENIMIENTO"
                        },
                        new DocumentSection
                        {
                            Type = SectionType.Subtitle,
                            Text =
                                "SISTEMA DE VENTILACIÓN // ARCHIVO 01"
                        },
                        new DocumentSection
                        {
                            Type = SectionType.Body,
                            Text =
                                "Documento de prueba para validar la " +
                                "bandeja-escáner. Sustituye este contenido " +
                                "por los archivos reales del puzzle."
                        }
                    };

                readable.InteractionPrompt =
                    "Leer documento";

                AssetDatabase.CreateAsset(
                    readable,
                    readablePath
                );
            }

            string folderDataPath =
                $"{DataFolder}/SampleGuideFolder.asset";

            GuideFolderData folderData =
                AssetDatabase.LoadAssetAtPath<
                    GuideFolderData>(folderDataPath);

            if (folderData == null)
            {
                folderData =
                    ScriptableObject.CreateInstance<
                        GuideFolderData>();

                AssetDatabase.CreateAsset(
                    folderData,
                    folderDataPath
                );
            }

            ConfigureFolderData(
                folderData,
                readable
            );

            string itemDataPath =
                $"{DataFolder}/SampleGuideFolderItem.asset";

            ItemData itemData =
                AssetDatabase.LoadAssetAtPath<
                    ItemData>(itemDataPath);

            if (itemData == null)
            {
                itemData =
                    ScriptableObject.CreateInstance<
                        ItemData>();

                AssetDatabase.CreateAsset(
                    itemData,
                    itemDataPath
                );
            }

            ConfigureItemData(itemData);

            GameObject folderObject =
                new("PF_GuideFolder_Sample");

            Undo.RegisterCreatedObjectUndo(
                folderObject,
                "Create Sample Guide Folder"
            );

            folderObject.layer =
                LayerMask.NameToLayer(
                    "Interactable"
                ) >= 0
                    ? LayerMask.NameToLayer(
                        "Interactable"
                    )
                    : 0;

            NetworkIdentity identity =
                folderObject.AddComponent<
                    NetworkIdentity>();

            Rigidbody rigidbody =
                folderObject.AddComponent<
                    Rigidbody>();

            rigidbody.mass = 0.35f;
            rigidbody.linearDamping = 1.5f;
            rigidbody.angularDamping = 2f;

            BoxCollider collider =
                folderObject.AddComponent<
                    BoxCollider>();

            collider.center =
                new Vector3(
                    0f,
                    0.035f,
                    0f
                );

            collider.size =
                new Vector3(
                    0.72f,
                    0.09f,
                    0.48f
                );

            NetworkPickupItem pickup =
                folderObject.AddComponent<
                    NetworkPickupItem>();

            GuideFolderItem folderItem =
                folderObject.AddComponent<
                    GuideFolderItem>();

            GameObject folderBase =
                CreateCube(
                    "FolderBase",
                    folderObject.transform,
                    new Vector3(
                        0f,
                        0.03f,
                        0f
                    ),
                    new Vector3(
                        0.72f,
                        0.045f,
                        0.48f
                    ),
                    folderMaterial
                );

            RemoveCollider(folderBase);

            GameObject folderTop =
                CreateCube(
                    "FolderTop",
                    folderObject.transform,
                    new Vector3(
                        0f,
                        0.065f,
                        0.015f
                    ),
                    new Vector3(
                        0.68f,
                        0.025f,
                        0.43f
                    ),
                    folderMaterial
                );

            RemoveCollider(folderTop);

            GameObject tab =
                CreateCube(
                    "FolderTab",
                    folderObject.transform,
                    new Vector3(
                        -0.20f,
                        0.085f,
                        0.18f
                    ),
                    new Vector3(
                        0.24f,
                        0.035f,
                        0.10f
                    ),
                    folderMaterial
                );

            RemoveCollider(tab);

            GameObject label =
                CreateCube(
                    "FolderLabel",
                    folderObject.transform,
                    new Vector3(
                        0f,
                        0.084f,
                        -0.02f
                    ),
                    new Vector3(
                        0.42f,
                        0.009f,
                        0.18f
                    ),
                    labelMaterial
                );

            RemoveCollider(label);

            SerializedObject pickupSerialized =
                new(pickup);

            pickupSerialized.FindProperty(
                "itemData"
            ).objectReferenceValue =
                itemData;

            pickupSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject folderSerialized =
                new(folderItem);

            folderSerialized.FindProperty(
                "folderData"
            ).objectReferenceValue =
                folderData;

            folderSerialized.ApplyModifiedPropertiesWithoutUndo();

            string prefabPath =
                $"{PrefabFolder}/PF_GuideFolder_Sample.prefab";

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(
                    folderObject,
                    prefabPath
                );

            Object.DestroyImmediate(folderObject);

            itemData.worldPrefab = prefab;
            itemData.heldModelPrefab = prefab;
            itemData.heldScale = 0.72f;

            AssignUniqueItemIdIfNeeded(
                itemData
            );

            EditorUtility.SetDirty(itemData);
            EditorUtility.SetDirty(folderData);
            EditorUtility.SetDirty(readable);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshLoadedItemRegistries();

            Selection.activeObject = prefab;

            Debug.Log(
                "[FolderScannerBuilder] Carpeta de ejemplo creada en " +
                prefabPath
            );
        }

        [MenuItem(
            "EOS/Guide Room/Build Complete Folder Station")]
        private static void BuildCompleteStation()
        {
            BuildScanner();
            CreateSampleFolder();
        }

        private static Transform ResolveAnchor(
            Transform selected
        )
        {
            if (
                selected.name ==
                "FolderSlotAnchor"
            )
            {
                return selected;
            }

            Transform child =
                selected.Find(
                    "FolderSlotAnchor"
                );

            if (child != null)
            {
                return child;
            }

            bool create =
                EditorUtility.DisplayDialog(
                    "Folder Scanner",
                    "El objeto seleccionado no contiene " +
                    "FolderSlotAnchor. ¿Crearlo como hijo?",
                    "Crear",
                    "Cancelar"
                );

            if (!create)
            {
                return null;
            }

            GameObject anchor =
                new("FolderSlotAnchor");

            Undo.RegisterCreatedObjectUndo(
                anchor,
                "Create FolderSlotAnchor"
            );

            anchor.transform.SetParent(
                selected,
                false
            );

            return anchor.transform;
        }

        private static GameObject CreateScannerRoot(
            Transform anchor
        )
        {
            GameObject root =
                new("FolderScannerDock");

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create FolderScannerDock"
            );

            root.transform.SetParent(
                anchor,
                false
            );

            root.transform.localPosition =
                Vector3.zero;

            root.transform.localRotation =
                Quaternion.identity;

            root.transform.localScale =
                Vector3.one;

            int interactableLayer =
                LayerMask.NameToLayer(
                    "Interactable"
                );

            if (interactableLayer >= 0)
            {
                root.layer =
                    interactableLayer;
            }
            else
            {
                Debug.LogWarning(
                    "[FolderScannerBuilder] No existe " +
                    "la capa Interactable. " +
                    "Asígnala manualmente."
                );
            }

            root.AddComponent<NetworkIdentity>();

            BoxCollider collider =
                root.AddComponent<BoxCollider>();

            collider.center =
                new Vector3(
                    0f,
                    0.48f,
                    0f
                );

            collider.size =
                new Vector3(
                    2.65f,
                    1.15f,
                    1.65f
                );

            AudioSource audio =
                root.AddComponent<AudioSource>();

            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 9f;

            root.AddComponent<
                FolderScannerVisuals>();

            root.AddComponent<
                FolderScannerDock>();

            return root;
        }

        private static void ClearVisualChildren(
            Transform root
        )
        {
            for (
                int index =
                    root.childCount - 1;
                index >= 0;
                index--
            )
            {
                Undo.DestroyObjectImmediate(
                    root.GetChild(index)
                        .gameObject
                );
            }
        }

        private static void BuildScannerVisuals(
            GameObject root
        )
        {
            EnsureFolder(MaterialFolder);

            Material bodyMaterial =
                GetOrCreateMaterial(
                    $"{MaterialFolder}/MAT_ScannerBody.mat",
                    new Color(
                        0.10f,
                        0.12f,
                        0.11f,
                        1f
                    ),
                    metallic: 0.55f,
                    smoothness: 0.24f,
                    emission: false
                );

            Material trayMaterial =
                GetOrCreateMaterial(
                    $"{MaterialFolder}/MAT_ScannerTray.mat",
                    new Color(
                        0.035f,
                        0.05f,
                        0.042f,
                        1f
                    ),
                    metallic: 0.25f,
                    smoothness: 0.18f,
                    emission: false
                );

            Material slotMaterial =
                GetOrCreateMaterial(
                    $"{MaterialFolder}/MAT_ScannerSlot.mat",
                    new Color(
                        0.004f,
                        0.006f,
                        0.005f,
                        1f
                    ),
                    metallic: 0f,
                    smoothness: 0.08f,
                    emission: false
                );

            Material greenEmission =
                GetOrCreateMaterial(
                    $"{MaterialFolder}/MAT_ScannerEmission.mat",
                    new Color(
                        0.05f,
                        0.65f,
                        0.18f,
                        1f
                    ),
                    metallic: 0f,
                    smoothness: 0.25f,
                    emission: true
                );

            Material folderMaterial =
                GetOrCreateMaterial(
                    $"{MaterialFolder}/MAT_ScannerFolderPreview.mat",
                    new Color(
                        0.27f,
                        0.38f,
                        0.20f,
                        1f
                    ),
                    metallic: 0f,
                    smoothness: 0.15f,
                    emission: false
                );

            GameObject body =
                CreateCube(
                    "ScannerBody",
                    root.transform,
                    new Vector3(
                        0f,
                        0.16f,
                        0f
                    ),
                    new Vector3(
                        2.40f,
                        0.32f,
                        1.35f
                    ),
                    bodyMaterial
                );

            RemoveCollider(body);

            GameObject tray =
                CreateCube(
                    "TrayBed",
                    root.transform,
                    new Vector3(
                        0f,
                        0.36f,
                        -0.08f
                    ),
                    new Vector3(
                        1.95f,
                        0.07f,
                        0.96f
                    ),
                    trayMaterial
                );

            RemoveCollider(tray);

            GameObject back =
                CreateCube(
                    "BackHousing",
                    root.transform,
                    new Vector3(
                        0f,
                        0.52f,
                        0.52f
                    ),
                    new Vector3(
                        2.32f,
                        0.72f,
                        0.34f
                    ),
                    bodyMaterial
                );

            RemoveCollider(back);

            GameObject slot =
                CreateCube(
                    "SlotMouth",
                    root.transform,
                    new Vector3(
                        0f,
                        0.48f,
                        0.335f
                    ),
                    new Vector3(
                        1.55f,
                        0.12f,
                        0.11f
                    ),
                    slotMaterial
                );

            RemoveCollider(slot);

            GameObject leftRail =
                CreateCube(
                    "LeftRail",
                    root.transform,
                    new Vector3(
                        -1.02f,
                        0.43f,
                        -0.08f
                    ),
                    new Vector3(
                        0.10f,
                        0.18f,
                        1.02f
                    ),
                    bodyMaterial
                );

            RemoveCollider(leftRail);

            GameObject rightRail =
                CreateCube(
                    "RightRail",
                    root.transform,
                    new Vector3(
                        1.02f,
                        0.43f,
                        -0.08f
                    ),
                    new Vector3(
                        0.10f,
                        0.18f,
                        1.02f
                    ),
                    bodyMaterial
                );

            RemoveCollider(rightRail);

            GameObject scannerBeam =
                CreateCube(
                    "ScannerBeam",
                    root.transform,
                    new Vector3(
                        0f,
                        0.415f,
                        -0.42f
                    ),
                    new Vector3(
                        1.82f,
                        0.018f,
                        0.045f
                    ),
                    greenEmission
                );

            RemoveCollider(scannerBeam);

            scannerBeam.SetActive(false);

            GameObject statusLight =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere
                );

            statusLight.name =
                "StatusLight";

            Undo.RegisterCreatedObjectUndo(
                statusLight,
                "Create StatusLight"
            );

            statusLight.transform.SetParent(
                root.transform,
                false
            );

            statusLight.transform.localPosition =
                new Vector3(
                    0.92f,
                    0.83f,
                    0.47f
                );

            statusLight.transform.localScale =
                Vector3.one * 0.12f;

            statusLight.GetComponent<Renderer>()
                .sharedMaterial =
                    greenEmission;

            RemoveCollider(statusLight);

            GameObject folderAnchor =
                new("FolderVisualAnchor");

            Undo.RegisterCreatedObjectUndo(
                folderAnchor,
                "Create FolderVisualAnchor"
            );

            folderAnchor.transform.SetParent(
                root.transform,
                false
            );

            folderAnchor.transform.localPosition =
                new Vector3(
                    0f,
                    0.405f,
                    -0.08f
                );

            GameObject folderVisual =
                new("FolderVisual");

            Undo.RegisterCreatedObjectUndo(
                folderVisual,
                "Create FolderVisual"
            );

            folderVisual.transform.SetParent(
                folderAnchor.transform,
                false
            );

            GameObject folderBase =
                CreateCube(
                    "FolderBody",
                    folderVisual.transform,
                    new Vector3(
                        0f,
                        0.02f,
                        0f
                    ),
                    new Vector3(
                        1.62f,
                        0.045f,
                        0.78f
                    ),
                    folderMaterial
                );

            RemoveCollider(folderBase);

            GameObject folderTab =
                CreateCube(
                    "FolderTab",
                    folderVisual.transform,
                    new Vector3(
                        -0.48f,
                        0.055f,
                        0.30f
                    ),
                    new Vector3(
                        0.42f,
                        0.035f,
                        0.16f
                    ),
                    folderMaterial
                );

            RemoveCollider(folderTab);

            GameObject labelCanvasObject =
                new(
                    "FolderLabelCanvas",
                    typeof(RectTransform),
                    typeof(Canvas)
                );

            Undo.RegisterCreatedObjectUndo(
                labelCanvasObject,
                "Create FolderLabelCanvas"
            );

            labelCanvasObject.transform.SetParent(
                folderVisual.transform,
                false
            );

            Canvas labelCanvas =
                labelCanvasObject.GetComponent<
                    Canvas>();

            labelCanvas.renderMode =
                RenderMode.WorldSpace;

            RectTransform labelRect =
                labelCanvasObject.GetComponent<
                    RectTransform>();

            labelRect.sizeDelta =
                new Vector2(
                    500f,
                    120f
                );

            labelRect.localPosition =
                new Vector3(
                    0f,
                    0.052f,
                    -0.03f
                );

            labelRect.localRotation =
                Quaternion.Euler(
                    90f,
                    0f,
                    0f
                );

            labelRect.localScale =
                Vector3.one * 0.001f;

            GameObject labelTextObject =
                new(
                    "FolderLabelText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI)
                );

            Undo.RegisterCreatedObjectUndo(
                labelTextObject,
                "Create FolderLabelText"
            );

            labelTextObject.transform.SetParent(
                labelCanvasObject.transform,
                false
            );

            RectTransform textRect =
                labelTextObject.GetComponent<
                    RectTransform>();

            textRect.anchorMin =
                Vector2.zero;

            textRect.anchorMax =
                Vector2.one;

            textRect.offsetMin =
                Vector2.zero;

            textRect.offsetMax =
                Vector2.zero;

            TextMeshProUGUI labelText =
                labelTextObject.GetComponent<
                    TextMeshProUGUI>();

            labelText.text = "ARCHIVO";
            labelText.fontSize = 42f;
            labelText.alignment =
                TextAlignmentOptions.Center;

            labelText.color =
                new Color(
                    0.08f,
                    0.11f,
                    0.08f,
                    1f
                );

            labelText.raycastTarget = false;

            folderVisual.SetActive(false);

            GameObject ejectPoint =
                new("EjectPoint");

            Undo.RegisterCreatedObjectUndo(
                ejectPoint,
                "Create EjectPoint"
            );

            ejectPoint.transform.SetParent(
                root.transform,
                false
            );

            ejectPoint.transform.localPosition =
                new Vector3(
                    0f,
                    0.72f,
                    -0.95f
                );
        }

        private static void ConfigureScannerComponents(
            GameObject root
        )
        {
            FolderScannerVisuals visuals =
                root.GetComponent<
                    FolderScannerVisuals>();

            FolderScannerDock dock =
                root.GetComponent<
                    FolderScannerDock>();

            AudioSource audio =
                root.GetComponent<AudioSource>();

            Transform scannerBeam =
                root.transform.Find(
                    "ScannerBeam"
                );

            Renderer statusRenderer =
                root.transform.Find(
                    "StatusLight"
                )?.GetComponent<Renderer>();

            GameObject folderVisual =
                root.transform.Find(
                    "FolderVisualAnchor/FolderVisual"
                )?.gameObject;

            Renderer folderBody =
                root.transform.Find(
                    "FolderVisualAnchor/FolderVisual/FolderBody"
                )?.GetComponent<Renderer>();

            Renderer folderTab =
                root.transform.Find(
                    "FolderVisualAnchor/FolderVisual/FolderTab"
                )?.GetComponent<Renderer>();

            TMP_Text folderLabel =
                root.transform.Find(
                    "FolderVisualAnchor/FolderVisual/" +
                    "FolderLabelCanvas/FolderLabelText"
                )?.GetComponent<TMP_Text>();

            Transform ejectPoint =
                root.transform.Find(
                    "EjectPoint"
                );

            SerializedObject visualsSerialized =
                new(visuals);

            visualsSerialized.FindProperty(
                "folderVisualRoot"
            ).objectReferenceValue =
                folderVisual;

            SerializedProperty rendererArray =
                visualsSerialized.FindProperty(
                    "folderRenderers"
                );

            rendererArray.arraySize = 2;

            rendererArray.GetArrayElementAtIndex(
                0
            ).objectReferenceValue =
                folderBody;

            rendererArray.GetArrayElementAtIndex(
                1
            ).objectReferenceValue =
                folderTab;

            visualsSerialized.FindProperty(
                "folderLabel"
            ).objectReferenceValue =
                folderLabel;

            visualsSerialized.FindProperty(
                "scannerBeam"
            ).objectReferenceValue =
                scannerBeam;

            visualsSerialized.FindProperty(
                "statusLightRenderer"
            ).objectReferenceValue =
                statusRenderer;

            visualsSerialized.FindProperty(
                "audioSource"
            ).objectReferenceValue =
                audio;

            visualsSerialized.FindProperty(
                "insertSound"
            ).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<
                    AudioClip>(PressSoundPath);

            visualsSerialized.FindProperty(
                "scanCompleteSound"
            ).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<
                    AudioClip>(SuccessSoundPath);

            visualsSerialized.FindProperty(
                "rejectSound"
            ).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<
                    AudioClip>(ErrorSoundPath);

            visualsSerialized.ApplyModifiedPropertiesWithoutUndo();

            GuideTerminalView terminalView =
                Object.FindFirstObjectByType<
                    GuideTerminalView>(
                        FindObjectsInactive.Include
                    );

            SerializedObject dockSerialized =
                new(dock);

            dockSerialized.FindProperty(
                "ejectPoint"
            ).objectReferenceValue =
                ejectPoint;

            dockSerialized.FindProperty(
                "visuals"
            ).objectReferenceValue =
                visuals;

            dockSerialized.FindProperty(
                "terminalView"
            ).objectReferenceValue =
                terminalView;

            dockSerialized.FindProperty(
                "scanDuration"
            ).floatValue =
                1.15f;

            dockSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (terminalView == null)
            {
                Debug.LogWarning(
                    "[FolderScannerBuilder] No se encontró " +
                    "GuideTerminalView. Genera primero la terminal " +
                    "principal o asígnala manualmente."
                );
            }

            EditorUtility.SetDirty(visuals);
            EditorUtility.SetDirty(dock);
        }

        private static void ConfigureFolderData(
            GuideFolderData folderData,
            DocumentData readable
        )
        {
            SerializedObject serialized =
                new(folderData);

            serialized.FindProperty(
                "folderId"
            ).stringValue =
                "folder.sample.maintenance";

            serialized.FindProperty(
                "displayName"
            ).stringValue =
                "MANTENIMIENTO";

            serialized.FindProperty(
                "folderColor"
            ).colorValue =
                new Color(
                    0.27f,
                    0.38f,
                    0.20f,
                    1f
                );

            SerializedProperty documents =
                serialized.FindProperty(
                    "documents"
                );

            documents.arraySize = 1;

            SerializedProperty firstEntry =
                documents.GetArrayElementAtIndex(0);

            firstEntry.FindPropertyRelative(
                "document"
            ).objectReferenceValue =
                readable;

            firstEntry.FindPropertyRelative(
                "note"
            ).objectReferenceValue =
                null;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureItemData(
            ItemData itemData
        )
        {
            itemData.itemName =
                "Carpeta de mantenimiento";

            itemData.description =
                "Carpeta compatible con la terminal del Guía.";

            itemData.isStackable = false;
            itemData.maxStack = 1;
            itemData.startsInInventory = false;
            itemData.heldScale = 0.72f;

            EditorUtility.SetDirty(itemData);
        }

        private static void AssignUniqueItemIdIfNeeded(
            ItemData target
        )
        {
            SerializedObject targetSerialized =
                new(target);

            SerializedProperty itemIdProperty =
                targetSerialized.FindProperty(
                    "itemId"
                );

            if (
                itemIdProperty != null &&
                itemIdProperty.intValue >= 0
            )
            {
                return;
            }

            int highestId = -1;

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:ItemData"
                );

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guid
                    );

                ItemData item =
                    AssetDatabase.LoadAssetAtPath<
                        ItemData>(path);

                if (
                    item != null &&
                    item != target
                )
                {
                    highestId =
                        Mathf.Max(
                            highestId,
                            item.ItemId
                        );
                }
            }

            if (itemIdProperty != null)
            {
                itemIdProperty.intValue =
                    highestId + 1;

                targetSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void RefreshLoadedItemRegistries()
        {
            ItemRegistry[] registries =
                Object.FindObjectsByType<
                    ItemRegistry>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    );

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:ItemData"
                );

            List<ItemData> allItems =
                new();

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guid
                    );

                ItemData item =
                    AssetDatabase.LoadAssetAtPath<
                        ItemData>(path);

                if (item != null)
                {
                    allItems.Add(item);
                }
            }

            allItems.Sort(
                (left, right) =>
                    left.ItemId.CompareTo(
                        right.ItemId
                    )
            );

            foreach (
                ItemRegistry registry
                in registries
            )
            {
                SerializedObject serialized =
                    new(registry);

                SerializedProperty list =
                    serialized.FindProperty(
                        "registeredItems"
                    );

                list.arraySize =
                    allItems.Count;

                for (
                    int index = 0;
                    index < allItems.Count;
                    index++
                )
                {
                    list.GetArrayElementAtIndex(
                        index
                    ).objectReferenceValue =
                        allItems[index];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(registry);
            }
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material
        )
        {
            GameObject cube =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube
                );

            cube.name = name;

            Undo.RegisterCreatedObjectUndo(
                cube,
                $"Create {name}"
            );

            cube.transform.SetParent(
                parent,
                false
            );

            cube.transform.localPosition =
                localPosition;

            cube.transform.localRotation =
                Quaternion.identity;

            cube.transform.localScale =
                localScale;

            Renderer renderer =
                cube.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial =
                    material;
            }

            return cube;
        }

        private static void RemoveCollider(
            GameObject gameObject
        )
        {
            Collider collider =
                gameObject.GetComponent<
                    Collider>();

            if (collider != null)
            {
                Object.DestroyImmediate(
                    collider
                );
            }
        }

        private static Material GetOrCreateMaterial(
            string path,
            Color baseColor,
            float metallic,
            float smoothness,
            bool emission
        )
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<
                    Material>(path);

            if (material != null)
            {
                return material;
            }

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit"
                );

            if (shader == null)
            {
                shader =
                    Shader.Find("Standard");
            }

            material =
                new Material(shader)
                {
                    name =
                        System.IO.Path.GetFileNameWithoutExtension(
                            path
                        )
                };

            if (
                material.HasProperty(
                    "_BaseColor"
                )
            )
            {
                material.SetColor(
                    "_BaseColor",
                    baseColor
                );
            }

            material.color = baseColor;

            if (
                material.HasProperty(
                    "_Metallic"
                )
            )
            {
                material.SetFloat(
                    "_Metallic",
                    metallic
                );
            }

            if (
                material.HasProperty(
                    "_Smoothness"
                )
            )
            {
                material.SetFloat(
                    "_Smoothness",
                    smoothness
                );
            }

            if (emission)
            {
                material.EnableKeyword(
                    "_EMISSION"
                );

                if (
                    material.HasProperty(
                        "_EmissionColor"
                    )
                )
                {
                    material.SetColor(
                        "_EmissionColor",
                        baseColor * 2.5f
                    );
                }
            }

            AssetDatabase.CreateAsset(
                material,
                path
            );

            return material;
        }

        private static void EnsureFolder(
            string folderPath
        )
        {
            string[] parts =
                folderPath.Split('/');

            string current =
                parts[0];

            for (
                int index = 1;
                index < parts.Length;
                index++
            )
            {
                string next =
                    $"{current}/{parts[index]}";

                if (
                    !AssetDatabase.IsValidFolder(
                        next
                    )
                )
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]
                    );
                }

                current = next;
            }
        }
    }
}
