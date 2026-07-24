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

    private CharacterStatsProvider statsProvider;

    [SerializeField, Min(0.01f)]
    private float interactionRadius = 0.18f;

    [SerializeField]
    private LayerMask interactionMask;

    [Header("Server Validation")]
    [SerializeField, Min(0.1f)]
    private float maximumServerDistance = 2.75f;

    [Header("Throwing")]
    [SerializeField, Min(0f)]
    private float throwImpulse = 2.4f;

    [SerializeField, Range(0f, 0.5f)]
    private float upwardThrowBias = 0.08f;

    [Header("Debug")]
    [SerializeField]
    private bool drawDebugRay = true;

    [SyncVar]
    private NetworkIdentity heldItemIdentity;

    private RatInteractable currentTarget;

    private CursorLockMode previousCursorLockState;
    private bool suppressThrowUntilMouseRelease;

    public NetworkIdentity HeldItemIdentity =>
        heldItemIdentity;

    public bool IsHoldingItem =>
        heldItemIdentity != null;

    public RatInteractable CurrentTarget =>
    currentTarget;

    public bool HasInteractionTarget =>
        currentTarget != null;

    public string CurrentInteractionPrompt =>
        currentTarget != null
            ? currentTarget.InteractionPrompt
            : string.Empty;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        previousCursorLockState = Cursor.lockState;
        suppressThrowUntilMouseRelease = false;
    }

    private void Update()
    {
        if (!isLocalPlayer || interactionOrigin == null)
        {
            return;
        }

        UpdateThrowInputGuard();
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

    private void UpdateThrowInputGuard()
    {
        Mouse mouse = Mouse.current;

        bool cursorWasJustLocked =
            previousCursorLockState != CursorLockMode.Locked &&
            Cursor.lockState == CursorLockMode.Locked;

        if (cursorWasJustLocked)
        {
            // El clic actual fue utilizado para recuperar
            // el control de la cámara.
            suppressThrowUntilMouseRelease = true;

            previousCursorLockState = Cursor.lockState;
            return;
        }

        // No habilitamos nuevamente el lanzamiento hasta
        // que el jugador haya soltado el botón.
        if (suppressThrowUntilMouseRelease &&
            (mouse == null || !mouse.leftButton.isPressed))
        {
            suppressThrowUntilMouseRelease = false;
        }

        previousCursorLockState = Cursor.lockState;
    }

    private void HandleInteractionInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        bool pressedInteract =
            keyboard != null &&
            keyboard.eKey.wasPressedThisFrame;

        // Mientras la rata sostiene un objeto:
        // E lo deja suavemente y clic izquierdo lo lanza.
        if (heldItemIdentity != null)
        {
            if (pressedInteract)
            {
                CmdDropHeldItem();
                return;
            }

            bool pressedThrow =
                mouse != null &&
                !suppressThrowUntilMouseRelease &&
                mouse.leftButton.wasPressedThisFrame &&
                Cursor.lockState == CursorLockMode.Locked;

            if (pressedThrow)
            {
                CmdThrowHeldItem(
                    interactionOrigin.forward);
            }

            return;
        }

        if (!pressedInteract || currentTarget == null)
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

    [Command]
    private void CmdThrowHeldItem(
        Vector3 requestedDirection)
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

        if (!TryNormalizeDirection(
                requestedDirection,
                out Vector3 throwDirection))
        {
            return;
        }

        throwDirection =
            (throwDirection +
            Vector3.up * upwardThrowBias).normalized;

        float strengthMultiplier =
            statsProvider != null
                ? Mathf.Max(
                    0f,
                    statsProvider.StrengthMultiplier)
                : 1f;

        float finalThrowImpulse =
            throwImpulse * strengthMultiplier;

        pickupItem.ServerThrow(
            netIdentity,
            throwDirection,
            finalThrowImpulse);
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
        if (!drawDebugRay || interactionOrigin == null)
        {
            return;
        }

        Color rayColor =
            currentTarget != null
                ? Color.green
                : Color.red;

        Vector3 origin = interactionOrigin.position;
        Vector3 direction = interactionOrigin.forward;

        Debug.DrawRay(
            origin,
            direction * interactionDistance,
            rayColor);

        Debug.DrawRay(
            origin + interactionOrigin.right * interactionRadius,
            direction * interactionDistance,
            rayColor);

        Debug.DrawRay(
            origin - interactionOrigin.right * interactionRadius,
            direction * interactionDistance,
            rayColor);

        Debug.DrawRay(
            origin + interactionOrigin.up * interactionRadius,
            direction * interactionDistance,
            rayColor);

        Debug.DrawRay(
            origin - interactionOrigin.up * interactionRadius,
            direction * interactionDistance,
            rayColor);
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

    private static bool TryNormalizeDirection(
        Vector3 requestedDirection,
        out Vector3 normalizedDirection)
    {
        normalizedDirection = Vector3.zero;

        if (!IsFinite(requestedDirection.x) ||
            !IsFinite(requestedDirection.y) ||
            !IsFinite(requestedDirection.z))
        {
            return false;
        }

        if (requestedDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        normalizedDirection =
            requestedDirection.normalized;

        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    private void OnDisable()
    {
        currentTarget = null;
    }

    private void Awake()
    {
        statsProvider =
            GetComponent<CharacterStatsProvider>();
    }
}