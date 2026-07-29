using Mirror;
using UnityEngine;

/// <summary>
/// Alimenta el Animator del modelo visual usando el desplazamiento
/// observado del jugador.
///
/// Funciona tanto para el jugador local como para sus copias remotas,
/// ya que no depende de CharacterController.velocity.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkRatAnimatorDriver : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private Transform movementSource;

    [SerializeField]
    private PlayerSprintController sprintController;
    
    [SerializeField]
    private PlayerCrouchController crouchController;

    [Header("Animator Parameters")]
    [SerializeField]
    private string moveSpeedParameter = "MoveSpeed";

    [SerializeField]
    private string crouchParameter = "IsCrouching";

    [Header("Blend Values")]
    [SerializeField, Range(0f, 1f)]
    private float walkBlendValue = 0.67f;

    [SerializeField, Range(0f, 1f)]
    private float runBlendValue = 1f;

    [Header("Detection")]
    [SerializeField, Min(0f)]
    private float movementThreshold = 0.08f;

    [SerializeField, Min(0.1f)]
    private float teleportDistance = 2f;

    [SerializeField, Min(0f)]
    private float dampingTime = 0.12f;

    private int moveSpeedHash;
    private int crouchHash;
    private Vector3 previousPosition;
    private bool hasPreviousPosition;

    private void Awake()
    {
        if (movementSource == null)
        {
            movementSource = transform;
        }

        if (sprintController == null)
        {
            sprintController =
                GetComponent<PlayerSprintController>();
        }

        moveSpeedHash =
            Animator.StringToHash(moveSpeedParameter);

        if (crouchController == null)
        {
            crouchController =
                GetComponent<PlayerCrouchController>();
        }

        crouchHash =
            Animator.StringToHash(
                crouchParameter);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ResetPositionSampling();
    }

    private void LateUpdate()
    {
        if (!isClient ||
            animator == null ||
            movementSource == null)
        {
            return;
        }

        bool isCrouching =
            crouchController != null &&
            crouchController.IsCrouching;

        animator.SetBool(
            crouchHash,
            isCrouching);

        Vector3 currentPosition =
            movementSource.position;

        if (!hasPreviousPosition)
        {
            previousPosition = currentPosition;
            hasPreviousPosition = true;
            SetMoveSpeed(0f);
            return;
        }

        Vector3 displacement =
            currentPosition - previousPosition;

        previousPosition = currentPosition;

        // Ignoramos la altura para que saltar o caer
        // no active la locomoción horizontal.
        displacement.y = 0f;

        // Evita un frame de carrera cuando el jugador
        // aparece, reaparece o es teletransportado.
        if (displacement.sqrMagnitude >
            teleportDistance * teleportDistance)
        {
            SetMoveSpeed(0f);
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= Mathf.Epsilon)
        {
            return;
        }

        float planarSpeed =
            displacement.magnitude / deltaTime;

        bool isMoving =
            planarSpeed >= movementThreshold;

        float targetBlend = 0f;

        if (isMoving)
        {
            bool isSprinting =
                sprintController != null &&
                sprintController.IsSprinting;

            targetBlend =
                isSprinting
                    ? runBlendValue
                    : walkBlendValue;
        }

        SetMoveSpeed(targetBlend);
    }

    private void SetMoveSpeed(float targetValue)
    {
        animator.SetFloat(
            moveSpeedHash,
            targetValue,
            dampingTime,
            Time.deltaTime);
    }

    private void ResetPositionSampling()
    {
        hasPreviousPosition = false;

        if (animator != null)
        {
            animator.SetFloat(
                moveSpeedHash,
                0f);
        }
    }

    public void SetAnimator(
        Animator targetAnimator)
    {
        animator = targetAnimator;

        ResetPositionSampling();

        if (animator == null)
        {
            return;
        }

        bool isCrouching =
            crouchController != null &&
            crouchController.IsCrouching;

        animator.SetBool(
            crouchHash,
            isCrouching);

        animator.SetFloat(
            moveSpeedHash,
            0f);
    }

    private void OnDisable()
    {
        hasPreviousPosition = false;
    }
}