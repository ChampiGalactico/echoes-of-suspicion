using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja el crouch local con respuesta inmediata y sincroniza
/// el estado confirmado para las representaciones remotas.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public sealed class PlayerCrouchController : NetworkBehaviour
{
    [Header("Crouch")]
    [SerializeField, Min(0.1f)]
    private float standingHeight = 2f;

    [SerializeField, Min(0.1f)]
    private float crouchingHeight = 1f;

    private CharacterController characterController;

    private Vector3 standingCenter;
    private Vector3 crouchingCenter;

    // Estado confirmado y distribuido por el servidor.
    [SyncVar(hook = nameof(OnCrouchingChanged))]
    private bool networkCrouching;

    // Predicción sencilla para que el jugador local no espere
    // el viaje de red antes de cambiar postura y velocidad.
    private bool localPredictedCrouching;

    [Header("First Person View")]
    [SerializeField]
    private Transform viewPivot;

    [SerializeField, Min(0f)]
    private float crouchingViewDrop = 0.55f;

    [SerializeField, Min(0.01f)]
    private float viewTransitionSpeed = 4.5f;
    private Vector3 standingViewLocalPosition;
    private Vector3 crouchingViewLocalPosition;
    private Vector3 targetViewLocalPosition;

    public bool IsCrouching =>
        isLocalPlayer
            ? localPredictedCrouching
            : networkCrouching;

    public event System.Action<bool> OnCrouchStateChanged;

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        standingCenter =
            characterController.center;

        crouchingCenter = standingCenter;

        float heightDifference =
            Mathf.Max(
                0f,
                standingHeight - crouchingHeight);

        // Al bajar el centro junto con la altura,
        // las patas permanecen apoyadas en el piso.
        crouchingCenter.y -=
            heightDifference * 0.5f;

        if (viewPivot != null)
        {
            standingViewLocalPosition =
                viewPivot.localPosition;

            crouchingViewLocalPosition =
                standingViewLocalPosition +
                Vector3.down * crouchingViewDrop;

            targetViewLocalPosition =
                standingViewLocalPosition;
        }

        ApplyCrouchPresentation(false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        localPredictedCrouching =
            networkCrouching;

        ApplyCrouchPresentation(
            networkCrouching);
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        bool wantsToCrouch =
            !GameplayInputBlocker.IsBlocked &&
            keyboard != null &&
            keyboard.leftCtrlKey.isPressed;

        if (wantsToCrouch == localPredictedCrouching)
        {
            return;
        }

        // Respuesta inmediata en el jugador propietario.
        localPredictedCrouching = wantsToCrouch;

        ApplyCrouchPresentation(wantsToCrouch);
        CmdSetCrouchIntent(wantsToCrouch);
    }

    [Command]
    private void CmdSetCrouchIntent(
        bool wantsToCrouch)
    {
        networkCrouching =
            wantsToCrouch;
    }

    private void OnCrouchingChanged(
        bool previousValue,
        bool newValue)
    {
        // Corrige la predicción local con el valor
        // confirmado por el servidor.
        if (isLocalPlayer)
        {
            localPredictedCrouching =
                newValue;
        }

        ApplyCrouchPresentation(
            newValue);

        OnCrouchStateChanged?.Invoke(
            newValue);
    }

    private void ApplyCrouchPresentation(
        bool crouching)
    {
        if (characterController != null)
        {
            characterController.height =
                crouching
                    ? crouchingHeight
                    : standingHeight;

            characterController.center =
                crouching
                    ? crouchingCenter
                    : standingCenter;
        }

        if (viewPivot != null)
        {
            targetViewLocalPosition =
                crouching
                    ? crouchingViewLocalPosition
                    : standingViewLocalPosition;
        }
    }

    private void LateUpdate()
    {
        if (viewPivot == null)
        {
            return;
        }

        viewPivot.localPosition =
            Vector3.MoveTowards(
                viewPivot.localPosition,
                targetViewLocalPosition,
                viewTransitionSpeed *
                Time.deltaTime);
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        crouchingHeight =
            Mathf.Min(
                crouchingHeight,
                standingHeight);
    }
#endif
}