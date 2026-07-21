using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class NetworkPlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float moveSpeed = 5f;

    [Header("Gravity")]
    [SerializeField]
    private float gravity = -20f;

    [SerializeField]
    private float groundedVerticalVelocity = -2f;

    private CharacterController characterController;
    private float verticalVelocity;

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

        Vector2 input = ReadKeyboardInput();

        Vector3 horizontalMovement =
            transform.right * input.x +
            transform.forward * input.y;

        horizontalMovement *= moveSpeed;

        ApplyGravity();

        Vector3 finalVelocity =
            horizontalMovement +
            Vector3.up * verticalVelocity;

        characterController.Move(finalVelocity * Time.deltaTime);
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