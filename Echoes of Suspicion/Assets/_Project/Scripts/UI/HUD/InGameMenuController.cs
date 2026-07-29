using Mirror;
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

        // Don't open pause menu if the player is reading a document.
        if (!IsOpen && ReadableUI.Instance != null && ReadableUI.Instance.IsOpen)
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
    public void ReturnToMainMenu()
    {
        NetworkManager networkManager = NetworkManager.singleton;

        if (networkManager == null)
        {
            Debug.LogError(
                "InGameMenuController: no se encontró el NetworkManager.",
                this
            );

            return;
        }

        // Liberamos cualquier bloqueo local antes de abandonar la partida.
        GameplayInputBlocker.SetBlocked(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        /*
        * Host:
        * detiene simultáneamente el servidor y su cliente local.
        */
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            networkManager.StopHost();
            return;
        }

        /*
        * Cliente:
        * se desconecta del host y vuelve a la Offline Scene.
        */
        if (NetworkClient.active)
        {
            networkManager.StopClient();
            return;
        }

        /*
        * Protección para una posible instancia server-only.
        */
        if (NetworkServer.active)
        {
            networkManager.StopServer();
            return;
        }

        Debug.LogWarning(
            "InGameMenuController: no existe una sesión de red activa.",
            this
        );
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