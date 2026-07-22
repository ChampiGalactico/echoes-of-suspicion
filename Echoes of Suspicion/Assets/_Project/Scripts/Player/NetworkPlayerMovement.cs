using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float walkSpeed = 5f;

    [SerializeField, Min(0f)]
    private float crouchSpeed = 2f;

    [Header("Jump")]
    [SerializeField, Min(0f)]
    private float jumpHeight = 1.5f;

    [Header("Crouch")]
    [SerializeField, Min(0f)]
    private float standingHeight = 2f;

    [SerializeField, Min(0f)]
    private float crouchingHeight = 1f;

    [Header("Gravity")]
    [SerializeField]
    private float gravity = -20f;

    [SerializeField]
    private float groundedVerticalVelocity = -2f;

    private CharacterController characterController;
    private float verticalVelocity;
    private bool isCrouching;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Cada cliente solo puede leer el input de su propio jugador.
        if (!isLocalPlayer)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        HandleCrouch(keyboard);
        HandleJump(keyboard);

        Vector2 input = ReadKeyboardInput();

        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;

        Vector3 horizontalMovement =
            transform.right * input.x +
            transform.forward * input.y;

        horizontalMovement *= currentSpeed;

        ApplyGravity();

        Vector3 finalVelocity =
            horizontalMovement +
            Vector3.up * verticalVelocity;

        characterController.Move(finalVelocity * Time.deltaTime);
    }

    private void HandleCrouch(Keyboard keyboard)
    {
        // Mantener presionado Ctrl para agacharse.
        bool wantsToCrouch = keyboard.leftCtrlKey.isPressed;

        if (wantsToCrouch != isCrouching)
        {
            isCrouching = wantsToCrouch;
            characterController.height =
                isCrouching ? crouchingHeight : standingHeight;
        }
    }

    private void HandleJump(Keyboard keyboard)
    {
        // Solo se puede saltar si está en el piso y no está agachado.
        if (keyboard.spaceKey.wasPressedThisFrame &&
            characterController.isGrounded &&
            !isCrouching)
        {
            // Fórmula física: v = sqrt(2 * altura * gravedad).
            verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
        }
    }

    private static Vector2 ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed)
        {
            input.y += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            input.y -= 1f;
        }

        if (keyboard.dKey.isPressed)
        {
            input.x += 1f;
        }

        if (keyboard.aKey.isPressed)
        {
            input.x -= 1f;
        }

        // Evita que moverse en diagonal sea más rápido.
        return Vector2.ClampMagnitude(input, 1f);
    }

    private void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVerticalVelocity;
        }

        verticalVelocity += gravity * Time.deltaTime;
    }
}