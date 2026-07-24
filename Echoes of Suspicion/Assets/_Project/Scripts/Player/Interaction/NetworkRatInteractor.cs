using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class NetworkRatInteractor : NetworkBehaviour
{
    [Header("Raycast")]
    [SerializeField]
    private Transform interactionOrigin;

    [SerializeField, Min(0.1f)]
    private float interactionDistance = 2.2f;

    [SerializeField]
    private LayerMask interactionMask;

    [Header("Server Validation")]
    [SerializeField, Min(0.1f)]
    private float maximumServerDistance = 2.75f;

    [Header("Debug")]
    [SerializeField]
    private bool drawDebugRay = true;

    [SyncVar]
    private NetworkIdentity heldItemIdentity;

    private RatInteractable currentTarget;

    public NetworkIdentity HeldItemIdentity =>
        heldItemIdentity;

    public bool IsHoldingItem =>
        heldItemIdentity != null;

    private void Update()
    {
        if (!isLocalPlayer || interactionOrigin == null)
        {
            return;
        }

        UpdateCurrentTarget();
        HandleInteractionInput();
        DrawInteractionRay();
    }

    private void UpdateCurrentTarget()
    {
        RatInteractable detectedTarget = DetectTarget();

        if (detectedTarget == currentTarget)
        {
            return;
        }

        currentTarget = detectedTarget;

        if (currentTarget != null && !IsHoldingItem)
        {
            Debug.Log(
                $"[E] {currentTarget.InteractionPrompt}: " +
                currentTarget.name,
                currentTarget);
        }
    }

    private RatInteractable DetectTarget()
    {
        if (IsHoldingItem)
        {
            return null;
        }

        bool didHit = Physics.Raycast(
            interactionOrigin.position,
            interactionOrigin.forward,
            out RaycastHit hit,
            interactionDistance,
            interactionMask,
            QueryTriggerInteraction.Ignore);

        if (!didHit)
        {
            return null;
        }

        RatInteractable interactable =
            hit.collider.GetComponentInParent<RatInteractable>();

        if (interactable == null ||
            !interactable.CanPreviewInteraction(gameObject))
        {
            return null;
        }

        return interactable;
    }

    private void HandleInteractionInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null ||
            !keyboard.eKey.wasPressedThisFrame)
        {
            return;
        }

        // Si ya sostiene algo, E lo suelta.
        if (heldItemIdentity != null)
        {
            CmdDropHeldItem();
            return;
        }

        if (currentTarget == null)
        {
            return;
        }

        NetworkIdentity targetIdentity =
            currentTarget.netIdentity;

        if (targetIdentity == null ||
            targetIdentity.netId == 0)
        {
            Debug.LogWarning(
                $"{currentTarget.name} no está registrado " +
                "como objeto de red.",
                currentTarget);

            return;
        }

        CmdTryInteract(targetIdentity);
    }

    [Command]
    private void CmdTryInteract(
        NetworkIdentity targetIdentity)
    {
        if (targetIdentity == null)
        {
            return;
        }

        RatInteractable target =
            targetIdentity.GetComponent<RatInteractable>();

        if (target == null ||
            !IsWithinServerRange(target) ||
            !target.CanServerInteract(netIdentity))
        {
            return;
        }

        target.ServerInteract(netIdentity);
    }

    [Command]
    private void CmdDropHeldItem()
    {
        if (heldItemIdentity == null)
        {
            return;
        }

        NetworkPickupItem pickupItem =
            heldItemIdentity.GetComponent<NetworkPickupItem>();

        if (pickupItem == null)
        {
            heldItemIdentity = null;
            return;
        }

        pickupItem.ServerDrop(netIdentity);
    }

    [Server]
    public bool ServerCanAcceptPickup(
        NetworkIdentity itemIdentity)
    {
        return itemIdentity != null &&
               heldItemIdentity == null;
    }

    [Server]
    public bool ServerTryAssignPickup(
        NetworkIdentity itemIdentity)
    {
        if (!ServerCanAcceptPickup(itemIdentity))
        {
            return false;
        }

        heldItemIdentity = itemIdentity;
        return true;
    }

    [Server]
    public void ServerClearPickup(
        NetworkIdentity itemIdentity)
    {
        if (heldItemIdentity == itemIdentity)
        {
            heldItemIdentity = null;
        }
    }

    [Server]
    private bool IsWithinServerRange(
        RatInteractable target)
    {
        Vector3 serverOrigin =
            interactionOrigin != null
                ? interactionOrigin.position
                : transform.position;

        Collider targetCollider =
            target.GetComponentInChildren<Collider>();

        Vector3 closestPoint =
            targetCollider != null
                ? targetCollider.ClosestPoint(serverOrigin)
                : target.transform.position;

        float squaredDistance =
            (closestPoint - serverOrigin).sqrMagnitude;

        return squaredDistance <=
               maximumServerDistance *
               maximumServerDistance;
    }

    private void DrawInteractionRay()
    {
        if (!drawDebugRay)
        {
            return;
        }

        Debug.DrawRay(
            interactionOrigin.position,
            interactionOrigin.forward *
            interactionDistance,
            currentTarget != null
                ? Color.green
                : Color.red);
    }

    public override void OnStopServer()
    {
        // El servidor todavía conoce qué objeto llevaba este jugador.
        // Lo soltamos antes de que desaparezca su NetworkIdentity.
        if (NetworkServer.active && heldItemIdentity != null)
        {
            NetworkIdentity itemToDrop = heldItemIdentity;

            NetworkPickupItem pickupItem =
                itemToDrop.GetComponent<NetworkPickupItem>();

            if (pickupItem != null)
            {
                pickupItem.ServerDrop(netIdentity);
            }
            else
            {
                // Protección por si el objeto fue destruido o perdió
                // inesperadamente su componente de pickup.
                heldItemIdentity = null;
            }
        }

        base.OnStopServer();
    }

    private void OnDisable()
    {
        currentTarget = null;
    }
}