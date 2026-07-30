using UnityEngine;

/// <summary>
/// Base ScriptableObject for any item that can exist in the inventory.
/// Create concrete assets from: Create → Echoes → Inventory → Item Data.
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Echoes/Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField, HideInInspector]
    private int itemId = -1;

    /// <summary>
    /// Stable numeric ID for network serialization.
    /// Assigned once via the Editor and locked forever after.
    /// </summary>
    public int ItemId => itemId;

    public string itemName = "Unnamed Item";

    [TextArea(2, 4)]
    public string description;

    public Sprite icon;

    [Header("Inventory")]
    [Tooltip("If true, multiple units of this item can share one slot.")]
    public bool isStackable;

    [Min(1)]
    public int maxStack = 1;

    [Tooltip("If true, one instance is placed in the inventory on spawn.")]
    public bool startsInInventory;

    [Tooltip("The prefab spawned in the world when the player drops or throws this item.")]
    public GameObject worldPrefab;

    [Header("Held Visual")]
    [Tooltip("Model shown in the player's hand when this item is in the active slot. " +
             "Should be a pure visual (mesh + materials, no colliders/rigidbody). " +
             "If null, worldPrefab is used as fallback.")]
    public GameObject heldModelPrefab;

    [Tooltip("Scale of the model when held in hand. 1 = original size. " +
             "Reduce for large items like boxes.")]
    [Min(0.01f)]
    public float heldScale = 1f;

    [Tooltip("Rotación del modelo en la mano (Euler angles). " +
             "Usa el preview del Inspector para ajustar visualmente.")]
    public Vector3 heldRotationOffset = Vector3.zero;

    [Tooltip("Offset de posición del modelo en la mano. " +
             "Ajusta si el pivot del modelo no está centrado.")]
    public Vector3 heldPositionOffset = Vector3.zero;

    [Header("Held Tint (opcional)")]
    [Tooltip("Si useHeldTint es true, el visual en mano se tiñe con heldTint " +
             "mediante MaterialPropertyBlock (sin tocar el material compartido). " +
             "Pensado para carpetas que comparten un mismo PF_GuideFolder_HeldVisual " +
             "pero deben conservar su color. NO se sincroniza por red: es un " +
             "metadato local del asset, idéntico en todos los clientes.")]
    public bool useHeldTint = false;

    [Tooltip("Color de tinte del visual en mano cuando useHeldTint es true.")]
    public Color heldTint = Color.white;
}
