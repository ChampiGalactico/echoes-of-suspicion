using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/*
	Documentation: https://mirror-networking.gitbook.io/docs/components/network-manager
	API Reference: https://mirror-networking.com/docs/api/Mirror.NetworkManager.html
*/
/// <summary>
/// NetworkManager custom para Echoes of Suspicion.
/// Extiende el NetworkManager de Mirror con la lógica específica del juego:
/// integración de UniVoice, eventos de conexión de jugadores, y estado de la sesión.
///
/// Es agnóstico de contenido de bioma — el spawn de criaturas, puntos de spawn
/// por rol, puzzles, etc. viven en BiomeSpawner (por escena), no aquí.
/// </summary>
public class EOSNetworkManager : NetworkManager
{

    private bool areProtagonistsReunited;

    public static bool AreProtagonistsReunited =>
        singleton != null && ((EOSNetworkManager)singleton).areProtagonistsReunited;

    /// <summary>
    /// Marca a Guía y Corredor como reunidos físicamente (ej. trigger al final
    /// del Acto 2). A partir de este punto, la criatura percibe y puede dañar
    /// a AMBOS jugadores, no solo al Corredor.
    ///
    /// TODO: esto solo cambia el estado en el servidor. Cuando se construya la
    /// mecánica real del final del Acto 2, replicar a los clientes si hace
    /// falta (ej. vía ClientRpc desde algún NetworkBehaviour del GameManager).
    /// </summary>
    public void SetProtagonistsReunited(bool reunited)
    {
        areProtagonistsReunited = reunited;
    }
    // Overrides the base singleton so we don't
    // have to cast to this type everywhere.
    public static EOSNetworkManager Singleton => (EOSNetworkManager)NetworkManager.singleton;

    // ===== EVENTOS QUE OTROS SISTEMAS PUEDEN ESCUCHAR =====

    /// <summary>
    /// Se dispara en el server cuando un jugador se conecta a la partida.
    /// Otros sistemas (ej. GameManager) pueden suscribirse para reaccionar.
    /// </summary>
    public static event System.Action<NetworkConnectionToClient> OnPlayerJoined;

    /// <summary>
    /// Se dispara en el server cuando un jugador se desconecta.
    /// </summary>
    public static event System.Action<NetworkConnectionToClient> OnPlayerLeft;

    /// <summary>
    /// Se dispara cuando el server (host) arranca.
    /// </summary>
    public static event System.Action OnServerReady_Event;

    /// <summary>
    /// Se dispara en el cliente cuando conecta exitosamente al servidor.
    /// </summary>
    public static event System.Action OnClientConnectedToServer;

    #region Unity Callbacks

    /* public override void Awake() { } */
    /* public override void OnValidate() { } */
    /* public override void Start() { } */
    /* public override void LateUpdate() { } */
    /* public override void OnDestroy() { } */

    #endregion

    #region Start & Stop

    /* public override void ConfigureHeadlessFrameRate() { } */
    /* public override void OnApplicationQuit() { } */

    #endregion

    #region Scene Management

    /* public override void ServerChangeScene(string newSceneName) { } */
    /* public override void OnServerChangeScene(string newSceneName) { } */
    /* public override void OnServerSceneChanged(string sceneName) { } */
    /* public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) { } */
    /* public override void OnClientSceneChanged() { } */

    #endregion

    #region Server System Callbacks

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"[EOSNetworkManager] 🟣 Jugador conectado. ConnectionId: {conn.connectionId}");
        OnPlayerJoined?.Invoke(conn);
    }

    public override void OnServerReady(NetworkConnectionToClient conn)
    {
        Debug.Log($"[EOSNetworkManager] 🟢 Cliente listo para recibir el mundo. ConnectionId: {conn.connectionId}");
        base.OnServerReady(conn);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // TODO: reemplazar por selección real de rol/personaje cuando exista el lobby.
        PlayerRole role = conn.connectionId == 0 ? PlayerRole.Guide : PlayerRole.Runner;
        int characterIndex = conn.connectionId == 0 ? 0 : 1;

        var biomeSpawner = FindAnyObjectByType<BiomeSpawner>();
        Transform spawnPoint = role == PlayerRole.Guide
            ? biomeSpawner?.GuideSpawnPoint
            : biomeSpawner?.RunnerSpawnPoint;

        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        if (spawnPoint == null)
        {
            Debug.LogWarning($"[EOSNetworkManager] No hay spawn point configurado para {role}. Usando origen.");
        }

        GameObject player = Instantiate(playerPrefab, position, rotation);
        NetworkServer.AddPlayerForConnection(conn, player);

        var statsProvider = player.GetComponent<CharacterStatsProvider>();
        if (statsProvider != null)
        {
            statsProvider.SetHardcodedRole(role);
            statsProvider.SetHardcodedCharacter(characterIndex);
            Debug.Log($"[EOSNetworkManager] ConnectionId {conn.connectionId} → {role}, personaje índice {characterIndex}, spawn en {position}.");
        }
        else
        {
            Debug.LogWarning("[EOSNetworkManager] El Player no tiene CharacterStatsProvider.");
        }

        var stamina = player.GetComponent<PlayerStamina>();
        stamina?.RecalculateFromStats();

        var health = player.GetComponent<PlayerHealth>();
        health?.RecalculateFromStats();
    }



    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"[EOSNetworkManager] 🟤 Jugador desconectado. ConnectionId: {conn.connectionId}");
        OnPlayerLeft?.Invoke(conn);
        base.OnServerDisconnect(conn);
    }

    /* public override void OnServerError(NetworkConnectionToClient conn, TransportError transportError, string message) { } */
    /* public override void OnServerTransportException(NetworkConnectionToClient conn, Exception exception) { } */

    #endregion

    #region Client System Callbacks

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("[EOSNetworkManager] 🟢 Cliente conectado al servidor.");
        OnClientConnectedToServer?.Invoke();
    }

    public override void OnClientDisconnect()
    {
        Debug.Log("[EOSNetworkManager] 🟤 Cliente desconectado del servidor.");
        base.OnClientDisconnect();
    }

    /* public override void OnClientNotReady() { } */
    /* public override void OnClientError(TransportError transportError, string message) { } */
    /* public override void OnClientTransportException(Exception exception) { } */

    #endregion

    #region Start & Stop Callbacks

    /* public override void OnStartHost() { } */

    public override void OnStartServer()
    {
        base.OnStartServer();

        Debug.Log("[EOSNetworkManager] 🟢 Server arrancado.");
        OnServerReady_Event?.Invoke();
    }

    /* public override void OnStartClient() { } */
    /* public override void OnStopHost() { } */
    /* public override void OnStopServer() { } */
    /* public override void OnStopClient() { } */

    #endregion
}