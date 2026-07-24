using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class NetworkFirstPersonView : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform viewPivot;

    [SerializeField]
    private Camera playerCamera;

    [SerializeField]
    private AudioListener playerAudioListener;

    [SerializeField]
    private Renderer bodyRenderer;

    [Header("Look")]
    [SerializeField, Min(0f)]
    private float mouseSensitivity = 0.08f;

    [SerializeField, Range(-89f, 0f)]
    private float minimumPitch = -85f;

    [SerializeField, Range(0f, 89f)]
    private float maximumPitch = 85f;

    private Camera bootstrapCamera;
    private AudioListener bootstrapAudioListener;
    private float pitch;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        DisableBootstrapCamera();

        playerCamera.enabled = true;
        playerAudioListener.enabled = true;

        // En primera persona no queremos ver el interior de la c�psula.
        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = false;
        }

        LockCursor();
    }

    public override void OnStopLocalPlayer()
    {
        UnlockCursor();

        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        if (playerAudioListener != null)
        {
            playerAudioListener.enabled = false;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = true;
        }

        RestoreBootstrapCamera();

        base.OnStopLocalPlayer();
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        HandleCursorState();

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        Vector2 mouseDelta = mouse.delta.ReadValue();
        ApplyLook(mouseDelta);
    }

    private void ApplyLook(Vector2 mouseDelta)
    {
        float configuredSensitivity =
            mouseSensitivity *
            OptionsMenuController.MouseSensitivityMultiplier;

        float yawDelta =
            mouseDelta.x * configuredSensitivity;

        float pitchDelta =
            mouseDelta.y * configuredSensitivity;

        // El cuerpo completo rota horizontalmente.
        transform.Rotate(0f, yawDelta, 0f, Space.Self);

        // Solo el pivote de la cámara rota verticalmente.
        pitch -= pitchDelta;
        pitch = Mathf.Clamp(
            pitch,
            minimumPitch,
            maximumPitch
        );

        viewPivot.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleCursorState()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        if (mouse != null &&
            mouse.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    private void DisableBootstrapCamera()
    {
        bootstrapCamera = Camera.main;

        if (bootstrapCamera == null || bootstrapCamera == playerCamera)
        {
            return;
        }

        bootstrapAudioListener =
            bootstrapCamera.GetComponent<AudioListener>();

        bootstrapCamera.enabled = false;

        if (bootstrapAudioListener != null)
        {
            bootstrapAudioListener.enabled = false;
        }
    }

    private void RestoreBootstrapCamera()
    {
        if (bootstrapCamera != null)
        {
            bootstrapCamera.enabled = true;
        }

        if (bootstrapAudioListener != null)
        {
            bootstrapAudioListener.enabled = true;
        }
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}