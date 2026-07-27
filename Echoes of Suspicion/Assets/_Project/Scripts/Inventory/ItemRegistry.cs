using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry of all ItemData assets in the game.
/// ScriptableObjects can't be sent over Mirror directly,
/// so each ItemData has a stable itemId (int) that is synced instead.
///
/// Place one instance in the scene (or on the NetworkManager).
/// The list auto-populates in the Editor — no need to drag items manually.
/// IDs are defined on each ItemData asset, NOT by list position,
/// so adding/removing/reordering items never breaks saved data.
/// </summary>
public class ItemRegistry : MonoBehaviour
{
    [SerializeField]
    private List<ItemData> registeredItems = new List<ItemData>();

    public static ItemRegistry Instance { get; private set; }

    private Dictionary<int, ItemData> idToData;
    private Dictionary<ItemData, int> dataToId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ItemRegistry] Duplicate registry found. Destroying this one.");
            Destroy(this);
            return;
        }

        Instance = this;
        BuildLookups();
    }

    private void BuildLookups()
    {
        idToData = new Dictionary<int, ItemData>();
        dataToId = new Dictionary<ItemData, int>();

        foreach (ItemData item in registeredItems)
        {
            if (item == null)
            {
                continue;
            }

            if (item.ItemId < 0)
            {
                Debug.LogWarning(
                    $"[ItemRegistry] '{item.itemName}' has no valid itemId (-1). " +
                    "Right-click the asset → Generate Unique ID.",
                    item);
                continue;
            }

            if (idToData.ContainsKey(item.ItemId))
            {
                Debug.LogError(
                    $"[ItemRegistry] Duplicate itemId {item.ItemId}: " +
                    $"'{item.itemName}' and '{idToData[item.ItemId].itemName}'. " +
                    "Each item must have a unique ID.",
                    item);
                continue;
            }

            idToData[item.ItemId] = item;
            dataToId[item] = item.ItemId;
        }
    }

    public int GetId(ItemData data)
    {
        if (data != null && dataToId.TryGetValue(data, out int id))
        {
            return id;
        }

        return -1;
    }

    public ItemData GetData(int id)
    {
        if (idToData.TryGetValue(id, out ItemData data))
        {
            return data;
        }

        return null;
    }

    public IReadOnlyList<ItemData> AllItems => registeredItems;

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoPopulate();
    }

    [ContextMenu("Force Refresh Item List")]
    private void AutoPopulate()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");

        List<ItemData> found = new List<ItemData>(guids.Length);

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);

            if (item != null)
            {
                found.Add(item);
            }
        }

        // Sort by itemId for readability in the Inspector.
        found.Sort((a, b) => a.ItemId.CompareTo(b.ItemId));

        if (!ListsMatch(registeredItems, found))
        {
            registeredItems = found;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Warn about items without IDs.
        foreach (ItemData item in found)
        {
            if (item.ItemId < 0)
            {
                Debug.LogWarning(
                    $"[ItemRegistry] '{item.name}' has no itemId. " +
                    "Right-click → Generate Unique ID.",
                    item);
            }
        }

        // Check for duplicate IDs.
        HashSet<int> seen = new HashSet<int>();
        foreach (ItemData item in found)
        {
            if (item.ItemId >= 0 && !seen.Add(item.ItemId))
            {
                Debug.LogError(
                    $"[ItemRegistry] Duplicate itemId {item.ItemId} on '{item.name}'.",
                    item);
            }
        }
    }

    private static bool ListsMatch(List<ItemData> a, List<ItemData> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
#endif
}
