#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for ItemData.
/// Shows the itemId as a locked field once assigned.
/// Provides a button to generate a unique ID for new items.
/// Includes an interactive 3D orbital preview to visualize
/// held item offsets without running the game.
/// </summary>
[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
    private PreviewRenderUtility previewUtility;
    private GameObject previewItemInstance;
    private ItemData cachedItem;
    private GameObject cachedPrefab;
    private Vector2 previewDrag;

    private void OnDisable()
    {
        CleanupPreview();
    }

    private void OnDestroy()
    {
        CleanupPreview();
    }

    public override void OnInspectorGUI()
    {
        ItemData item = (ItemData)target;

        if (item.ItemId < 0)
        {
            EditorGUILayout.HelpBox(
                "This item has no ID yet. Click the button below to assign one. " +
                "Once assigned, it cannot be changed.",
                MessageType.Warning);

            if (GUILayout.Button("Generate Unique ID"))
            {
                AssignUniqueId(item);
            }

            EditorGUILayout.Space();
        }
        else
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Item ID (locked)", item.ItemId);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
        }

        DrawDefaultInspector();
        DrawHeldModelPreview(item);
    }

    // =========================================================================
    //  HELD MODEL PREVIEW
    // =========================================================================

    private void DrawHeldModelPreview(ItemData item)
    {
        GameObject prefab = item.heldModelPrefab != null
            ? item.heldModelPrefab
            : item.worldPrefab;

        if (prefab == null)
            return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Held Model Preview", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Arrastra para rotar. Los offsets se reflejan en tiempo real.",
            MessageType.Info);

        if (GUI.changed)
            Repaint();

        Rect previewRect = GUILayoutUtility.GetRect(
            256, 300,
            GUILayout.ExpandWidth(true)
        );

        HandlePreviewInput(previewRect);

        if (Event.current.type == EventType.Repaint)
        {
            DrawOrbitalPreview(previewRect, item, prefab);
        }
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event evt = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (evt.type)
        {
            case EventType.MouseDown:
                if (rect.Contains(evt.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    evt.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId)
                {
                    previewDrag += evt.delta;
                    evt.Use();
                    Repaint();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    evt.Use();
                }
                break;
        }
    }

    // =========================================================================
    //  ORBITAL PREVIEW
    // =========================================================================

    private void DrawOrbitalPreview(Rect rect, ItemData item, GameObject prefab)
    {
        EnsurePreviewUtility();
        EnsurePreviewItemInstance(item, prefab);

        if (previewItemInstance == null)
            return;

        // Apply offsets.
        previewItemInstance.transform.localPosition = item.heldPositionOffset;
        previewItemInstance.transform.localRotation =
            Quaternion.Euler(item.heldRotationOffset);
        previewItemInstance.transform.localScale =
            prefab.transform.localScale * item.heldScale;

        previewUtility.BeginPreview(rect, GUIStyle.none);

        float orbitX = previewDrag.x * 0.5f;
        float orbitY = Mathf.Clamp(previewDrag.y * 0.5f, -89f, 89f);

        Bounds bounds = CalculateBounds(previewItemInstance);
        float distance = bounds.extents.magnitude * 2.5f;
        distance = Mathf.Max(distance, 0.5f);
        Vector3 center = bounds.center;

        Quaternion cameraRotation = Quaternion.Euler(-orbitY, orbitX, 0f);
        Vector3 cameraPosition =
            center + cameraRotation * (Vector3.back * distance);

        Camera cam = previewUtility.camera;
        cam.transform.position = cameraPosition;
        cam.transform.LookAt(center);
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = distance * 10f;

        previewUtility.lights[0].transform.rotation =
            Quaternion.Euler(30f, 30f + orbitX, 0f);
        previewUtility.lights[0].intensity = 1.2f;

        cam.Render();

        Texture result = previewUtility.EndPreview();
        GUI.DrawTexture(rect, result, ScaleMode.ScaleToFit);
    }

    // =========================================================================
    //  PREVIEW INSTANCE MANAGEMENT
    // =========================================================================

    private void EnsurePreviewUtility()
    {
        if (previewUtility == null)
        {
            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.clearFlags = CameraClearFlags.SolidColor;
            previewUtility.camera.backgroundColor =
                new Color(0.12f, 0.12f, 0.12f, 1f);
        }
    }

    private void EnsurePreviewItemInstance(ItemData item, GameObject prefab)
    {
        if (
            previewItemInstance != null &&
            cachedItem == item &&
            cachedPrefab == prefab
        )
        {
            return;
        }

        CleanupPreviewItem();

        cachedItem = item;
        cachedPrefab = prefab;

        previewItemInstance =
            (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        StripNonVisual(previewItemInstance);

        previewUtility.AddSingleGO(previewItemInstance);
    }

    private static void StripNonVisual(GameObject obj)
    {
        // 1. PickableItem depende de NetworkPickupItem -> quitarlo primero.
        foreach (EOS.Puzzles.PickableItem pi in
            obj.GetComponentsInChildren<EOS.Puzzles.PickableItem>(true))
        {
            if (pi != null) DestroyImmediate(pi);
        }

        // 2. MonoBehaviours restantes, EXCEPTO NetworkIdentity
        //    (hereda MonoBehaviour y tiene dependientes).
        foreach (MonoBehaviour mb in
            obj.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            if (mb is Mirror.NetworkIdentity) continue;
            DestroyImmediate(mb);
        }

        // 3. NetworkIdentity (ya sin dependientes).
        foreach (Mirror.NetworkIdentity ni in
            obj.GetComponentsInChildren<Mirror.NetworkIdentity>(true))
        {
            if (ni != null) DestroyImmediate(ni);
        }

        // 4. Rigidbody (ya sin RequireComponent apuntandole).
        foreach (Rigidbody rb in
            obj.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb != null) DestroyImmediate(rb);
        }

        // 5. Colliders — desactivar, no destruir (mas seguro).
        foreach (Collider col in
            obj.GetComponentsInChildren<Collider>(true))
        {
            if (col != null) col.enabled = false;
        }
    }

    private void CleanupPreviewItem()
    {
        if (previewItemInstance != null)
        {
            DestroyImmediate(previewItemInstance);
            previewItemInstance = null;
        }

        cachedItem = null;
        cachedPrefab = null;
    }

    private void CleanupPreview()
    {
        CleanupPreviewItem();

        if (previewUtility != null)
        {
            previewUtility.Cleanup();
            previewUtility = null;
        }
    }

    private static Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers =
            obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one * 0.5f);

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    // =========================================================================
    //  ID MANAGEMENT
    // =========================================================================

    [MenuItem("CONTEXT/ItemData/Generate Unique ID")]
    private static void GenerateIdMenu(MenuCommand command)
    {
        ItemData item = (ItemData)command.context;

        if (item.ItemId >= 0)
        {
            Debug.LogWarning(
                $"[ItemData] '{item.itemName}' already has ID {item.ItemId}. " +
                "IDs cannot be changed once assigned.",
                item);
            return;
        }

        AssignUniqueId(item);
    }

    private static void AssignUniqueId(ItemData item)
    {
        if (item.ItemId >= 0)
            return;

        int nextId = FindNextAvailableId();

        SerializedObject so = new SerializedObject(item);
        SerializedProperty idProp = so.FindProperty("itemId");

        Undo.RecordObject(item, "Assign Item ID");
        idProp.intValue = nextId;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ItemData] Assigned ID {nextId} to '{item.itemName}'.", item);
    }

    private static int FindNextAvailableId()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemData");
        int maxId = -1;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ItemData data = AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (data != null && data.ItemId > maxId)
                maxId = data.ItemId;
        }

        return maxId + 1;
    }
}
#endif
