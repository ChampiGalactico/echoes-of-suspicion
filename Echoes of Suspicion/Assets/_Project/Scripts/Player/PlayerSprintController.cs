using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Maneja el estado de correr. El cliente solo ENVÍA su intención (Shift
/// presionado + no agachado); el servidor decide con autoridad completa
/// (incluyendo CanSprint, que depende de isExhausted, solo válido en el
/// servidor) y sincroniza el resultado final vía SyncVar. Así movimiento,
/// pasos y stamina siempre leen el mismo valor confirmado — sin que el
/// cliente duplique la decisión con datos parciales.
///
/// PlayerStamina puede forzar el corte (ForceStopSprinting) si la stamina
/// se agota MIENTRAS el jugador sigue corriendo, sin esperar a que el
/// cliente vuelva a mandar un nuevo Command.
/// </summary>
public sealed class PlayerSprintController : NetworkBehaviour
{
    private PlayerStamina stamina;
    private PlayerCrouchController crouchController;
    private bool lastSentIntent;

    [SyncVar(hook = nameof(OnIsSprintingChanged))]
    private bool isSprinting;

    public bool IsSprinting => isSprinting;

    /// <summary>Se dispara cuando IsSprinting cambia de valor (no cada frame).</summary>
    public event System.Action<bool> OnSprintStateChanged;

    private void Awake()
    {
        stamina = GetComponent<PlayerStamina>();
        crouchController = GetComponent<PlayerCrouchController>();
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

        bool isCrouching = crouchController != null && crouchController.IsCrouching;
        bool wantsToSprint = keyboard.leftShiftKey.isPressed && !isCrouching;

        if (wantsToSprint != lastSentIntent)
        {
            lastSentIntent = wantsToSprint;
            CmdSetSprintIntent(wantsToSprint);
        }
    }

    [Command]
    private void CmdSetSprintIntent(bool wantsToSprint)
    {
        bool actualSprinting = wantsToSprint && stamina.CanSprint;

        isSprinting = actualSprinting;
        stamina.IsSprinting = actualSprinting;
    }

    /// <summary>
    /// Llamado por PlayerStamina cuando la stamina se agota MIENTRAS el
    /// jugador sigue corriendo, sin esperar un nuevo Command del cliente.
    /// </summary>
    [Server]
    public void ForceStopSprinting()
    {
        isSprinting = false;
        stamina.IsSprinting = false;
    }

    private void OnIsSprintingChanged(bool oldValue, bool newValue)
    {
        OnSprintStateChanged?.Invoke(newValue);
    }
}