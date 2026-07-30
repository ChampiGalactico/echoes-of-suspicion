using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Maneja la muerte definitiva de un jugador durante la demo.
///
/// Cuando cualquier jugador muere (vida = 0), en vez de respawnear:
/// 1. Pantalla negra en todos los clientes.
/// 2. Texto con flicker: "El corredor/guía murió... Experimento inconcluso."
/// 3. Dos botones: "Reintentar" (recarga la escena) y "Menú principal" (desconecta LOCAL).
///
/// Cada jugador decide por su cuenta si vuelve al menú o reintenta.
/// - "Menú principal" desconecta solo al jugador que lo presiona.
/// - "Reintentar" recarga la escena para todos (solo funciona si lo presiona el host).
///
/// SETUP:
/// 1. Colocar en un GameObject persistente en la escena del bioma.
/// 2. Agregar NetworkIdentity.
/// 3. Asignar font (Audiowide_SDF del MainMenu).
/// 4. Opcionalmente asignar buttonPrefab (del menú de pausa) y gameOverClip.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class GameOverManager : NetworkBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [Header("UI Style")]

    [SerializeField, Tooltip("Fuente para el texto de Game Over (ej. Audiowide_SDF).")]
    private TMP_FontAsset font;

    [SerializeField, Tooltip("Color del texto de Game Over.")]
    private Color textColor = new Color(0f, 0.9f, 0.7f, 1f);

    [SerializeField, Tooltip("Prefab de botón del menú de pausa. Si es null, se crea uno básico.")]
    private GameObject buttonPrefab;

    [Header("Audio")]

    [SerializeField, Tooltip("Clip que suena al morir (triste/tenso). Opcional.")]
    private AudioClip gameOverClip;

    [SerializeField, Range(0f, 1f)]
    private float clipVolume = 0.6f;

    [Header("Timing")]

    [SerializeField, Min(0.1f), Tooltip("Duración del fade a negro.")]
    private float fadeDuration = 0.8f;

    [SerializeField, Min(0f), Tooltip("Pausa antes de mostrar el texto tras el fade.")]
    private float textDelay = 0.5f;

    [SerializeField, Min(0f), Tooltip("Tiempo que se muestra el texto antes de los botones.")]
    private float buttonsDelay = 1.5f;

    // ── State ────────────────────────────────────────────

    [SyncVar]
    private bool _gameOver;

    // ── Client UI (auto-created) ─────────────────────────

    private GameObject _canvasObj;
    private CanvasGroup _fadePanel;
    private TMP_Text _deathText;
    private GameObject _buttonsContainer;
    private AudioSource _audioSource;

    // ── Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (isServer)
            PlayerHealth.DeathOverride = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        PlayerHealth.DeathOverride = HandlePlayerDeath;
    }

    public override void OnStopServer()
    {
        PlayerHealth.DeathOverride = null;
        base.OnStopServer();
    }

    // ── Server: death intercepted ────────────────────────

    [Server]
    private void HandlePlayerDeath(PlayerHealth deadPlayer)
    {
        if (_gameOver) return;
        _gameOver = true;

        var stats = deadPlayer.GetComponent<CharacterStatsProvider>();
        string roleName = stats != null && stats.Role == PlayerRole.Guide
            ? "El guía"
            : "El corredor";

        Debug.Log($"[GameOver] {roleName} murió. Game Over.");

        FreezeAllPlayers();
        RpcShowGameOver(roleName);
    }

    [Server]
    private void FreezeAllPlayers()
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) continue;
            TargetFreezePlayer(conn, true);
        }
    }

    [TargetRpc]
    private void TargetFreezePlayer(
        NetworkConnectionToClient target, bool freeze)
    {
        var local = NetworkClient.localPlayer;
        if (local == null) return;

        var mov = local.GetComponent<NetworkPlayerMovement>();
        var inter = local.GetComponent<NetworkRatInteractor>();
        var view = local.GetComponent<NetworkFirstPersonView>();

        if (mov != null) mov.enabled = !freeze;
        if (inter != null) inter.enabled = !freeze;
        if (view != null) view.enabled = !freeze;

        GameplayInputBlocker.SetBlocked(freeze);
        Cursor.lockState = freeze ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = freeze;
    }

    // ── Server: retry ────────────────────────────────────

    /// <summary>
    /// Called by the host player's Command to reload the biome scene.
    /// </summary>
    [Server]
    public void ServerRetry()
    {
        if (!_gameOver) return;

        Debug.Log("[GameOver] Retrying — reloading scene.");

        _gameOver = false;
        RpcCleanupGameOver();

        string currentScene = SceneManager.GetActiveScene().name;
        NetworkManager.singleton.ServerChangeScene(currentScene);
    }

    // ── Client RPCs ──────────────────────────────────────

    [ClientRpc]
    private void RpcShowGameOver(string roleName)
    {
        StartCoroutine(ClientGameOverSequence(roleName));
    }

    [ClientRpc]
    private void RpcCleanupGameOver()
    {
        if (_canvasObj != null)
            Destroy(_canvasObj);

        _canvasObj = null;
        _fadePanel = null;
        _deathText = null;
        _buttonsContainer = null;

        var local = NetworkClient.localPlayer;
        if (local != null)
        {
            var mov = local.GetComponent<NetworkPlayerMovement>();
            var inter = local.GetComponent<NetworkRatInteractor>();
            var view = local.GetComponent<NetworkFirstPersonView>();

            if (mov != null) mov.enabled = true;
            if (inter != null) inter.enabled = true;
            if (view != null) view.enabled = true;
        }

        GameplayInputBlocker.SetBlocked(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ── Client sequence ──────────────────────────────────

    private IEnumerator ClientGameOverSequence(string roleName)
    {
        Debug.Log($"[GameOver] ClientGameOverSequence started. Role: {roleName}");
        EnsureUIExists();

        // Play game over sound.
        if (gameOverClip != null && _audioSource != null)
            _audioSource.PlayOneShot(gameOverClip, clipVolume);

        // 1. Fade to black.
        if (_fadePanel != null)
        {
            _fadePanel.gameObject.SetActive(true);
            _fadePanel.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadePanel.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            _fadePanel.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(textDelay);

        // 2. Show death text.
        if (_deathText != null)
        {
            _deathText.text = $"{roleName} murió...\nExperimento inconcluso";
            _deathText.gameObject.SetActive(true);

            float textElapsed = 0f;
            float textFadeDuration = 0.8f;
            while (textElapsed < textFadeDuration)
            {
                textElapsed += Time.unscaledDeltaTime;
                _deathText.alpha = Mathf.Clamp01(textElapsed / textFadeDuration);
                yield return null;
            }

            _deathText.alpha = 1f;

            var flicker = _deathText.GetComponent<UITextFlicker>();
            if (flicker != null) flicker.enabled = true;
        }

        yield return new WaitForSecondsRealtime(buttonsDelay);

        // 3. Show buttons.
        if (_buttonsContainer != null)
            _buttonsContainer.SetActive(true);
    }

    // ── Client: button callbacks ─────────────────────────

    private void OnRetryClicked()
    {
        if (_buttonsContainer != null)
            _buttonsContainer.SetActive(false);

        var local = NetworkClient.localPlayer;
        if (local == null) return;

        var health = local.GetComponent<PlayerHealth>();
        if (health != null)
            health.CmdRequestGameAction(GameOverAction.Retry);
    }

    /// <summary>
    /// Desconecta SOLO al jugador local. Cada uno decide por su cuenta.
    /// Misma lógica que InGameMenuController.ReturnToMainMenu().
    /// </summary>
    private void OnMenuClicked()
    {
        if (_buttonsContainer != null)
            _buttonsContainer.SetActive(false);

        GameplayInputBlocker.SetBlocked(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var nm = NetworkManager.singleton;
        if (nm == null) return;

        // Host: detiene servidor + cliente local.
        if (NetworkServer.active && NetworkClient.isConnected)
        {
            nm.StopHost();
            return;
        }

        // Client: se desconecta y vuelve a offline scene.
        if (NetworkClient.active)
        {
            nm.StopClient();
            return;
        }

        if (NetworkServer.active)
        {
            nm.StopServer();
        }
    }

    // ── UI auto-creation ─────────────────────────────────

    private void EnsureUIExists()
    {
        if (_fadePanel != null) return;

        // Canvas propio en overlay.
        _canvasObj = new GameObject("GameOverCanvas");
        var canvas = _canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = _canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _canvasObj.AddComponent<GraphicRaycaster>();

        // AudioSource.
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.spatialBlend = 0f;
        _audioSource.playOnAwake = false;

        // ── Fade panel ──
        var panelObj = new GameObject("GameOverPanel");
        panelObj.transform.SetParent(canvas.transform, false);

        var panelRt = panelObj.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        var panelImg = panelObj.AddComponent<Image>();
        panelImg.color = Color.black;
        panelImg.raycastTarget = true;

        _fadePanel = panelObj.AddComponent<CanvasGroup>();
        _fadePanel.alpha = 0f;
        panelObj.SetActive(false);

        // ── Death text ──
        var textObj = new GameObject("DeathText");
        textObj.transform.SetParent(panelObj.transform, false);

        var textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.5f, 0.55f);
        textRt.anchorMax = new Vector2(0.5f, 0.55f);
        textRt.sizeDelta = new Vector2(800f, 200f);

        _deathText = textObj.AddComponent<TextMeshProUGUI>();
        _deathText.fontSize = 52f;
        _deathText.alignment = TextAlignmentOptions.Center;
        _deathText.color = textColor;
        _deathText.alpha = 0f;

        if (font != null)
            _deathText.font = font;

        // Flicker desactivado hasta que se muestre.
        var flicker = textObj.AddComponent<UITextFlicker>();
        flicker.enabled = false;

        textObj.SetActive(false);

        // ── Buttons container ──
        _buttonsContainer = new GameObject("ButtonsContainer");
        _buttonsContainer.transform.SetParent(panelObj.transform, false);

        var btnContRt = _buttonsContainer.AddComponent<RectTransform>();
        btnContRt.anchorMin = new Vector2(0.5f, 0.3f);
        btnContRt.anchorMax = new Vector2(0.5f, 0.3f);
        btnContRt.sizeDelta = new Vector2(550f, 60f);

        var hlg = _buttonsContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 50f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        CreateButton(_buttonsContainer.transform, "Reintentar", OnRetryClicked);
        CreateButton(_buttonsContainer.transform, "Menú principal", OnMenuClicked);

        _buttonsContainer.SetActive(false);
    }

    private void CreateButton(
        Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        // Si hay un prefab asignado, instanciarlo.
        if (buttonPrefab != null)
        {
            var instance = Instantiate(buttonPrefab, parent);
            instance.name = label;

            // Buscar el TMP_Text del prefab y asignar el label.
            var tmp = instance.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                tmp.text = label;

            // Buscar el Button y agregar el callback.
            var btn = instance.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(onClick);
            }

            return;
        }

        // ── Fallback: crear botón por código ──
        var btnObj = new GameObject(label);
        btnObj.transform.SetParent(parent, false);

        var btnRt = btnObj.AddComponent<RectTransform>();
        btnRt.sizeDelta = new Vector2(220f, 55f);

        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        // Borde grueso: 2 Outlines apilados para mayor grosor.
        var outline1 = btnObj.AddComponent<Outline>();
        outline1.effectColor = new Color(0f, 0.8f, 0.6f, 0.8f);
        outline1.effectDistance = new Vector2(3f, 3f);

        var outline2 = btnObj.AddComponent<Outline>();
        outline2.effectColor = new Color(0f, 0.8f, 0.6f, 0.5f);
        outline2.effectDistance = new Vector2(5f, 5f);

        var button = btnObj.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        var colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        colors.highlightedColor = new Color(0f, 0.35f, 0.25f, 0.95f);
        colors.pressedColor = new Color(0f, 0.2f, 0.15f, 1f);
        colors.selectedColor = new Color(0f, 0.35f, 0.25f, 0.95f);
        button.colors = colors;

        // Label.
        var textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);

        var textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10f, 5f);
        textRt.offsetMax = new Vector2(-10f, -5f);

        var tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = label;
        tmpText.fontSize = 20f;
        tmpText.alignment = TextAlignmentOptions.Center;
        tmpText.color = new Color(0f, 0.9f, 0.7f, 1f); // Verde tipo terminal.

        if (font != null)
            tmpText.font = font;
    }
}

/// <summary>
/// Acción solicitada por el jugador tras Game Over.
/// </summary>
public enum GameOverAction : byte
{
    Retry,
    Menu
}
