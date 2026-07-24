using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NetworkPickupItem : RatInteractable
{
    [Header("Held Pose")]
    [SerializeField]
    private Vector3 heldLocalPosition = Vector3.zero;

    [SerializeField]
    private Vector3 heldLocalEulerAngles = Vector3.zero;

    [Header("Colliders")]
    [SerializeField]
    private Collider[] itemColliders;

    // Pose autoritativa utilizada cuando el objeto está libre.
    [SyncVar(hook = nameof(OnDroppedPositionChanged))]
    private Vector3 droppedPosition;

    [SyncVar(hook = nameof(OnDroppedRotationChanged))]
    private Quaternion droppedRotation;

    // Jugador que sostiene el objeto. Null significa que está libre.
    [SyncVar(hook = nameof(OnHolderChanged))]
    private NetworkIdentity holderIdentity;

    private Transform resolvedHoldSocket;

    public bool IsHeld => holderIdentity != null;

    private void Awake()
    {
        if (itemColliders == null || itemColliders.Length == 0)
        {
            itemColliders =
                GetComponentsInChildren<Collider>(true);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        droppedPosition = transform.position;
        droppedRotation = transform.rotation;

        ApplyDroppedPresentation();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshPresentation();
    }

    public override bool CanPreviewInteraction(GameObject interactor)
    {
        return !IsHeld &&
               base.CanPreviewInteraction(interactor);
    }

    [Server]
    public override bool CanServerInteract(
        NetworkIdentity interactor)
    {
        if (interactor == null || holderIdentity != null)
        {
            return false;
        }

        NetworkRatInteractor ratInteractor =
            interactor.GetComponent<NetworkRatInteractor>();

        return ratInteractor != null &&
               ratInteractor.ServerCanAcceptPickup(netIdentity);
    }

    [Server]
    public override void ServerInteract(
        NetworkIdentity interactor)
    {
        if (!CanServerInteract(interactor))
        {
            return;
        }

        NetworkRatInteractor ratInteractor =
            interactor.GetComponent<NetworkRatInteractor>();

        if (ratInteractor == null ||
            !ratInteractor.ServerTryAssignPickup(netIdentity))
        {
            return;
        }

        holderIdentity = interactor;
        ApplyHeldPresentation();
    }

    [Server]
    public void ServerDrop(NetworkIdentity requester)
    {
        if (requester == null ||
            holderIdentity != requester)
        {
            return;
        }

        ResolveDropPose(
            requester,
            out Vector3 position,
            out Quaternion rotation);

        droppedPosition = position;
        droppedRotation = rotation;

        NetworkRatInteractor ratInteractor =
            requester.GetComponent<NetworkRatInteractor>();

        if (ratInteractor != null)
        {
            ratInteractor.ServerClearPickup(netIdentity);
        }

        holderIdentity = null;

        transform.SetPositionAndRotation(
            droppedPosition,
            droppedRotation);

        ApplyDroppedPresentation();
    }

    private void LateUpdate()
    {
        if (holderIdentity == null)
        {
            return;
        }

        if (resolvedHoldSocket == null &&
            !TryResolveHoldSocket())
        {
            return;
        }

        Vector3 targetPosition =
            resolvedHoldSocket.TransformPoint(
                heldLocalPosition);

        Quaternion targetRotation =
            resolvedHoldSocket.rotation *
            Quaternion.Euler(heldLocalEulerAngles);

        transform.SetPositionAndRotation(
            targetPosition,
            targetRotation);
    }

    private bool TryResolveHoldSocket()
    {
        resolvedHoldSocket = null;

        if (holderIdentity == null)
        {
            return false;
        }

        RatHoldSocketProvider provider =
            holderIdentity.GetComponent<RatHoldSocketProvider>();

        return provider != null &&
               provider.TryGetHoldSocket(
                   out resolvedHoldSocket);
    }

    private static void ResolveDropPose(
        NetworkIdentity requester,
        out Vector3 position,
        out Quaternion rotation)
    {
        RatHoldSocketProvider provider =
            requester.GetComponent<RatHoldSocketProvider>();

        if (provider != null &&
            provider.TryGetDropPose(
                out position,
                out rotation))
        {
            return;
        }

        position =
            requester.transform.position +
            requester.transform.forward;

        rotation = Quaternion.identity;
    }

    private void OnHolderChanged(
        NetworkIdentity previousHolder,
        NetworkIdentity newHolder)
    {
        RefreshPresentation();
    }

    private void OnDroppedPositionChanged(
        Vector3 previousPosition,
        Vector3 newPosition)
    {
        if (!IsHeld)
        {
            transform.position = newPosition;
        }
    }

    private void OnDroppedRotationChanged(
        Quaternion previousRotation,
        Quaternion newRotation)
    {
        if (!IsHeld)
        {
            transform.rotation = newRotation;
        }
    }

    private void RefreshPresentation()
    {
        if (IsHeld)
        {
            ApplyHeldPresentation();
        }
        else
        {
            ApplyDroppedPresentation();
        }
    }

    private void ApplyHeldPresentation()
    {
        resolvedHoldSocket = null;
        SetCollidersEnabled(false);
        TryResolveHoldSocket();
    }

    private void ApplyDroppedPresentation()
    {
        resolvedHoldSocket = null;

        transform.SetPositionAndRotation(
            droppedPosition,
            droppedRotation);

        SetCollidersEnabled(true);
    }

    private void SetCollidersEnabled(bool isEnabled)
    {
        if (itemColliders == null)
        {
            return;
        }

        foreach (Collider itemCollider in itemColliders)
        {
            if (itemCollider != null)
            {
                itemCollider.enabled = isEnabled;
            }
        }
    }
}