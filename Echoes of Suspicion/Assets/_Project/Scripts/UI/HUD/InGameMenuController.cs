using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InGameMenuController : MonoBehaviour
{
    [Header("Canvas Groups")]
    [SerializeField]
    private CanvasGroup inGameMenuGroup;

    [SerializeField]
    private CanvasGroup menuPanelGroup;

    [SerializeField]
    private CanvasGroup optionsPanelGroup;

    [Header("Startup")]
    [SerializeField]
    private bool startOpen;

    public bool IsOpen
    {
        get;
        private set;
    }

    private bool isOptionsOpen;

    private void Awake()
    {
        ShowMainMenu();
        SetMenuOpen(startOpen);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (
            keyboard == null ||
            !keyboard.escapeKey.wasPressedThisFrame
        )
        {
            return;
        }

        if (!IsOpen)
        {
            OpenMenu();
            return;
        }

        if (isOptionsOpen)
        {
            ShowMainMenu();
            return;
        }

        CloseMenu();
    }

    private void OnDisable()
    {
        // Evita que el input permanezca bloqueado al cambiar de escena
        // o destruir el HUD.
        if (IsOpen)
        {
            GameplayInputBlocker.SetBlocked(false);
        }

        IsOpen = false;
    }

    public void OpenMenu()
    {
        ShowMainMenu();
        SetMenuOpen(true);
    }

    public void CloseMenu()
    {
        ShowMainMenu();
        SetMenuOpen(false);
    }

    public void ShowOptions()
    {
        if (!IsOpen)
        {
            return;
        }

        isOptionsOpen = true;

        SetCanvasGroup(menuPanelGroup, false);
        SetCanvasGroup(optionsPanelGroup, true);
    }

    public void ShowMainMenu()
    {
        isOptionsOpen = false;

        SetCanvasGroup(menuPanelGroup, true);
        SetCanvasGroup(optionsPanelGroup, false);
    }

    private void SetMenuOpen(bool open)
    {
        IsOpen = open;

        GameplayInputBlocker.SetBlocked(open);

        SetCanvasGroup(inGameMenuGroup, open);

        if (!open)
        {
            ShowMainMenu();
        }

        Cursor.lockState = open
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = open;
    }

    private static void SetCanvasGroup(
        CanvasGroup group,
        bool visible
    )
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}