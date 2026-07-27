using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local-player input for the inventory system.
///
/// Controls:
///   1-5         → select slot directly
///   Scroll      → cycle slots
///   E           → interact (handled by NetworkRatInteractor)
///   F           → toggle flashlight
///   R           → reload battery
///   G           → drop active item gently
///   Left Click  → throw active item (parabolic, influenced by strength + aim angle)
/// </summary>
[DisallowMultipleComponent]
public class InventoryInputHandler : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private NetworkInventory inventory;

    [SerializeField]
    private NetworkFlashlight flashlight;

    [SerializeField]
    private NetworkHeldItemVisual heldVisual;

    [Header("Throw")]
    [SerializeField, Tooltip("Transform whose forward direction is used as throw direction (typically the camera).")]
    private Transform throwOrigin;

    [Header("Scroll")]
    [SerializeField]
    private bool invertScroll;

    // ── Cursor lock guard ─────────────────────────────────────
    // Prevents the click that re-locks the cursor from also
    // triggering a throw.
    private CursorLockMode previousCursorLockState;
    private bool suppressThrowUntilRelease;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<NetworkInventory>();
        if (flashlight == null)
            flashlight = GetComponent<NetworkFlashlight>();
        if (heldVisual == null)
            heldVisual = GetComponent<NetworkHeldItemVisual>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        previousCursorLockState = Cursor.lockState;
        suppressThrowUntilRelease = false;
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null)
        {
            return;
        }

        UpdateThrowGuard(mouse);
        HandleNumberKeys(keyboard);
        HandleScrollWheel(mouse);
        HandleFlashlightToggle(keyboard);
        HandleBatteryReload(keyboard);
        HandleDrop(keyboard);
        HandleThrow(mouse);
    }

    // ── Slot selection ────────────────────────────────────────

    private void HandleNumberKeys(Keyboard keyboard)
    {
        if (keyboard.digit1Key.wasPressedThisFrame) SetSlot(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) SetSlot(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) SetSlot(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) SetSlot(3);
        else if (keyboard.digit5Key.wasPressedThisFrame) SetSlot(4);
    }

    private void HandleScrollWheel(Mouse mouse)
    {
        if (mouse == null)
        {
            return;
        }

        float scrollY = mouse.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) < 0.01f)
        {
            return;
        }

        int direction = scrollY > 0f ? -1 : 1;
        if (invertScroll)
        {
            direction = -direction;
        }

        int newIndex = inventory.ActiveSlotIndex + direction;

        if (newIndex < 0)
            newIndex = NetworkInventory.SlotCount - 1;
        else if (newIndex >= NetworkInventory.SlotCount)
            newIndex = 0;

        SetSlot(newIndex);
    }

    private void SetSlot(int index)
    {
        if (index == inventory.ActiveSlotIndex)
        {
            return;
        }

        inventory.CmdSetActiveSlot(index);
    }

    // ── Flashlight ────────────────────────────────────────────

    private void HandleFlashlightToggle(Keyboard keyboard)
    {
        if (flashlight != null && keyboard.fKey.wasPressedThisFrame)
        {
            flashlight.CmdToggle();
        }
    }

    private void HandleBatteryReload(Keyboard keyboard)
    {
        if (flashlight != null && keyboard.rKey.wasPressedThisFrame)
        {
            flashlight.CmdReloadBattery();
        }
    }

    // ── Drop (gentle) ─────────────────────────────────────────

    private void HandleDrop(Keyboard keyboard)
    {
        if (keyboard.gKey.wasPressedThisFrame)
        {
            if (inventory.ActiveSlot.IsEmpty)
            {
                return;
            }

            // Clear visual immediately for responsive feel.
            if (heldVisual != null)
            {
                heldVisual.ClearVisualImmediate();
            }

            inventory.CmdDropActiveItem();
        }
    }

    // ── Throw (parabolic) ─────────────────────────────────────

    private void UpdateThrowGuard(Mouse mouse)
    {
        bool cursorWasJustLocked =
            previousCursorLockState != CursorLockMode.Locked &&
            Cursor.lockState == CursorLockMode.Locked;

        if (cursorWasJustLocked)
        {
            suppressThrowUntilRelease = true;
            previousCursorLockState = Cursor.lockState;
            return;
        }

        if (suppressThrowUntilRelease &&
            (mouse == null || !mouse.leftButton.isPressed))
        {
            suppressThrowUntilRelease = false;
        }

        previousCursorLockState = Cursor.lockState;
    }

    private void HandleThrow(Mouse mouse)
    {
        if (mouse == null || suppressThrowUntilRelease)
        {
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        if (inventory.ActiveSlot.IsEmpty)
        {
            return;
        }

        // Get throw direction from camera.
        Vector3 throwDir = throwOrigin != null
            ? throwOrigin.forward
            : transform.forward;

        // Clear visual immediately.
        if (heldVisual != null)
        {
            heldVisual.ClearVisualImmediate();
        }

        inventory.CmdThrowActiveItem(throwDir);
    }
}
