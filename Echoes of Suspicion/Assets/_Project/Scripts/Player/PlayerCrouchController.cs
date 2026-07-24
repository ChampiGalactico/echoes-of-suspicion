using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja el estado de agacharse. NetworkPlayerMovement consulta IsCrouching
/// para decidir velocidad y no necesita saber cómo se detecta el input.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerCrouchController : NetworkBehaviour
{
    [Header("Crouch")]
    [SerializeField, Min(0f)]
    private float standingHeight = 2f;

    [SerializeField, Min(0f)]
    private float crouchingHeight = 1f;

    private CharacterController characterController;

    public bool IsCrouching { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
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

        bool wantsToCrouch = keyboard.leftCtrlKey.isPressed;

        if (wantsToCrouch != IsCrouching)
        {
            IsCrouching = wantsToCrouch;
            characterController.height = IsCrouching ? crouchingHeight : standingHeight;
        }
    }
}