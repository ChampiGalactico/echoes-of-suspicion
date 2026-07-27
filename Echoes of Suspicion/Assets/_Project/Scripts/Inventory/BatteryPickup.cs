using Mirror;
using UnityEngine;

/// <summary>
/// A battery pickup in the world. Does NOT go into an inventory slot —
/// it simply increments the player's battery counter.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class BatteryPickup : RatInteractable
{
    [Header("Battery")]
    [SerializeField, Min(1)]
    private int chargeUnits = 1;

    [Server]
    public override bool CanServerInteract(NetworkIdentity interactor)
    {
        return interactor != null &&
               interactor.GetComponent<NetworkInventory>() != null;
    }

    [Server]
    public override void ServerInteract(NetworkIdentity interactor)
    {
        if (!CanServerInteract(interactor))
        {
            return;
        }

        NetworkInventory inventory =
            interactor.GetComponent<NetworkInventory>();

        inventory.ServerAddBatteries(chargeUnits);
        NetworkServer.Destroy(gameObject);
    }
}
