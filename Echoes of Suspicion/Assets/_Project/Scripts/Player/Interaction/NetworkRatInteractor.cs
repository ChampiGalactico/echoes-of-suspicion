using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Unified interaction system.
/// Detects RatInteractable objects via spherecast and delegates
/// interaction to the target's ServerInteract.
///
/// No longer holds items directly — all items go through NetworkInventory.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkRatInteractor : NetworkBehaviour
{
    [Header("Raycast")]
    [SerializeField]
    private Transform interactionOrigin;

    [SerializeField, Min(0.1f)]
    private float interactionDistance = 2.2f;

    [SerializeField, Min(0.01f)]
    private float interactionRadius = 0.18f;

    [SerializeField]
    private LayerMask interactionMask;

    [Header("Server Validation")]
    [SerializeField, Min(0.1f)]
    private float maximumServerDistance = 2.75f;

    [Header("Debug")]
    [SerializeField]
    private bool drawDebugRay = true;

    private NetworkInventory inventory;
    private RatInteractable currentTarget;

    public RatInteractable CurrentTarget => currentTarget;

    public bool HasInteractionTarget => currentTarget != null;

    public string CurrentInteractionPrompt =>
        currentTarget != null
            ? currentTarget.InteractionPrompt
            : string.Empty;

    /// <summary>Whether the active inventory slot has an item.</summary>
    public bool HasActiveItem
    {
        get
        {
            if (inventory == null) return false;
            return !inventory.ActiveSlot.IsEmpty;
        }
    }

    private void Awake()
    {
        inventory = GetComponent<NetworkInventory>();
    }

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
    }

    private RatInteractable DetectTarget()
    {
        bool didHit = Physics.SphereCast(
            interactionOrigin.position,
            interactionRadius,
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

        if (keyboard == null)
        {
            return;
        }

        bool pressedInteract =
            keyboard.eKey.wasPressedThisFrame;

        if (!pressedInteract || currentTarget == null)
        {
            return;
        }

        NetworkIdentity targetIdentity =
            currentTarget.netIdentity;

        if (targetIdentity == null || targetIdentity.netId == 0)
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
    private void CmdTryInteract(NetworkIdentity targetIdentity)
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

    [Server]
    private bool IsWithinServerRange(RatInteractable target)
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
               maximumServerDistance * maximumServerDistance;
    }

    private void DrawInteractionRay()
    {
        if (!drawDebugRay || interactionOrigin == null)
        {
            return;
        }

        Color rayColor =
            currentTarget != null ? Color.green : Color.red;

        Vector3 origin = interactionOrigin.position;
        Vector3 direction = interactionOrigin.forward;

        Debug.DrawRay(origin, direction * interactionDistance, rayColor);

        Debug.DrawRay(
            origin + interactionOrigin.right * interactionRadius,
            direction * interactionDistance, rayColor);

        Debug.DrawRay(
            origin - interactionOrigin.right * interactionRadius,
            direction * interactionDistance, rayColor);

        Debug.DrawRay(
            origin + interactionOrigin.up * interactionRadius,
            direction * interactionDistance, rayColor);

        Debug.DrawRay(
            origin - interactionOrigin.up * interactionRadius,
            direction * interactionDistance, rayColor);
    }

    private void OnDisable()
    {
        currentTarget = null;
    }
}
