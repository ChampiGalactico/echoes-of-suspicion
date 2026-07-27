#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for ItemData.
/// Shows the itemId as a locked field once assigned.
/// Provides a button to generate a unique ID for new items.
/// </summary>
[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
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
            // Show the ID as a disabled (grayed out) field.
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Item ID (locked)", item.ItemId);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
        }

        DrawDefaultInspector();
    }

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
        {
            return;
        }

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
            {
                maxId = data.ItemId;
            }
        }

        return maxId + 1;
    }
}
#endif
