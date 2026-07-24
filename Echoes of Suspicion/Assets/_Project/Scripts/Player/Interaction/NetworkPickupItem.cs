using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class NetworkPickupItem : RatInteractable
{
    [Header("Held Pose")]
    [SerializeField]
    private Vector3 heldLocalPosition = Vector3.zero;

    [SerializeField]
    private Vector3 heldLocalEulerAngles = Vector3.zero;

    [Header("Physics")]
    [SerializeField]
    private Rigidbody itemRigidbody;

    [SerializeField]
    private Collider[] itemColliders;

    [SerializeField, Range(0f, 1f)]
    private float inheritedPlayerVelocityMultiplier = 0.35f;

    // Null significa que el objeto está libre.
    [SyncVar(hook = nameof(OnHolderChanged))]
    private NetworkIdentity holderIdentity;

    private Transform resolvedHoldSocket;

    private ThrowableNoiseSource throwableNoiseSource;

    public bool IsHeld => holderIdentity != null;

    private void Awake()
    {
        if (itemRigidbody == null)
        {
            itemRigidbody = GetComponent<Rigidbody>();
        }

        if (throwableNoiseSource == null)
        {
            throwableNoiseSource =
                GetComponent<ThrowableNoiseSource>();
        }

        if (itemColliders == null || itemColliders.Length == 0)
        {
            itemColliders =
                GetComponentsInChildren<Collider>(true);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (holderIdentity == null)
        {
            ApplyFreePresentation();
        }
        else
        {
            ApplyHeldPresentation();
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshPresentation();
    }

    public override bool CanPreviewInteraction(
        GameObject interactor)
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
    public void ServerDrop(
        NetworkIdentity requester)
    {
        ServerRelease(
            requester,
            Vector3.zero,
            0f);
    }

    [Server]
    public void ServerThrow(
        NetworkIdentity requester,
        Vector3 throwDirection,
        float throwImpulse)
    {
        if (throwDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        ServerRelease(
            requester,
            throwDirection.normalized,
            Mathf.Max(0f, throwImpulse));
    }

    private void ServerRelease(
        NetworkIdentity requester,
        Vector3 throwDirection,
        float throwImpulse)
    {
        if (requester == null ||
            holderIdentity != requester)
        {
            return;
        }

        ResolveDropPose(
            requester,
            out Vector3 releasePosition,
            out Quaternion releaseRotation);

        Vector3 inheritedVelocity =
            GetInteractorVelocity(requester) *
            inheritedPlayerVelocityMultiplier;

        if (throwableNoiseSource != null)
        {
            throwableNoiseSource.ServerSetSourcePlayer(
                requester.netId);
        }

        NetworkRatInteractor ratInteractor =
            requester.GetComponent<NetworkRatInteractor>();

        if (ratInteractor != null)
        {
            ratInteractor.ServerClearPickup(
                netIdentity);
        }

        holderIdentity = null;

        transform.SetPositionAndRotation(
            releasePosition,
            releaseRotation);

        ApplyFreePresentation();

        if (itemRigidbody == null)
        {
            return;
        }

        itemRigidbody.linearVelocity =
            inheritedVelocity;

        itemRigidbody.angularVelocity =
            Vector3.zero;

        if (throwImpulse > 0f)
        {
            itemRigidbody.AddForce(
                throwDirection * throwImpulse,
                ForceMode.Impulse);
        }

        itemRigidbody.WakeUp();
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

    private static Vector3 GetInteractorVelocity(
        NetworkIdentity requester)
    {
        CharacterController characterController =
            requester.GetComponent<CharacterController>();

        return characterController != null
            ? characterController.velocity
            : Vector3.zero;
    }

    private void OnHolderChanged(
        NetworkIdentity previousHolder,
        NetworkIdentity newHolder)
    {
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (IsHeld)
        {
            ApplyHeldPresentation();
        }
        else
        {
            ApplyFreePresentation();
        }
    }

    private void ApplyHeldPresentation()
    {
        resolvedHoldSocket = null;
        SetCollidersEnabled(false);

        if (itemRigidbody != null)
        {
            itemRigidbody.useGravity = false;
            itemRigidbody.isKinematic = true;
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }

        TryResolveHoldSocket();
    }

    private void ApplyFreePresentation()
    {
        resolvedHoldSocket = null;
        SetCollidersEnabled(true);

        if (itemRigidbody == null)
        {
            return;
        }

        if (isServer)
        {
            // Solo el servidor ejecuta la física real.
            itemRigidbody.isKinematic = false;
            itemRigidbody.useGravity = true;
            itemRigidbody.WakeUp();
        }
        else
        {
            // Los clientes reciben la pose desde NetworkTransform.
            itemRigidbody.useGravity = false;
            itemRigidbody.isKinematic = true;
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }
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