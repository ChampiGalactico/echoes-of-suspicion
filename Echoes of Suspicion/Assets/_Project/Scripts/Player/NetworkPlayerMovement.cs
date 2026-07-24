using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterStatsProvider))]
public sealed class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float walkSpeed = 6f;

    [SerializeField, Min(0f)]
    private float crouchSpeed = 2f;

    [SerializeField, Min(0f)]
    private float sprintSpeed = 9f;

    [Header("Jump")]
    [SerializeField, Min(0f)]
    private float jumpHeight = 1.5f;

    [Header("Gravity")]
    [SerializeField]
    private float gravity = -20f;

    [SerializeField]
    private float groundedVerticalVelocity = -2f;

    private CharacterController characterController;
    private CharacterStatsProvider statsProvider;
    private PlayerCrouchController crouchController;
    private PlayerSprintController sprintController;

    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        statsProvider = GetComponent<CharacterStatsProvider>();
        crouchController = GetComponent<PlayerCrouchController>();
        sprintController = GetComponent<PlayerSprintController>();
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        HandleJump(keyboard);

        Vector2 input = ReadKeyboardInput();
        float currentSpeed = GetCurrentSpeed();

        Vector3 horizontalMovement =
            (transform.right * input.x + transform.forward * input.y) * currentSpeed;

        ApplyGravity();

        Vector3 finalVelocity = horizontalMovement + Vector3.up * verticalVelocity;
        characterController.Move(finalVelocity * Time.deltaTime);
    }

    private float GetCurrentSpeed()
    {
        bool isCrouching = crouchController != null && crouchController.IsCrouching;
        bool isSprinting = sprintController != null && sprintController.IsSprinting;

        float baseSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        float agility = statsProvider != null ? statsProvider.AgilityMultiplier : 1f;


        return baseSpeed * agility;
    }

    private void HandleJump(Keyboard keyboard)
    {
        bool isCrouching = crouchController != null && crouchController.IsCrouching;

        if (keyboard.spaceKey.wasPressedThisFrame &&
            characterController.isGrounded &&
            !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
        }
    }

    private static Vector2 ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;

        if (keyboard.wKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed) input.y -= 1f;
        if (keyboard.dKey.isPressed) input.x += 1f;
        if (keyboard.aKey.isPressed) input.x -= 1f;

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