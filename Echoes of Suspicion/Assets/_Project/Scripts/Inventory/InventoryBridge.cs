using Mirror;
using UnityEngine;

/// <summary>
/// Wires NetworkInventory events to other systems.
/// Attach to the player prefab alongside NetworkInventory.
///
/// Currently a thin pass-through — ready for future systems
/// that need to react to inventory changes (UI, audio cues, etc.).
/// </summary>
[DisallowMultipleComponent]
public class InventoryBridge : NetworkBehaviour
{
    [SerializeField]
    private NetworkInventory inventory;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<NetworkInventory>();
        }
    }
}
