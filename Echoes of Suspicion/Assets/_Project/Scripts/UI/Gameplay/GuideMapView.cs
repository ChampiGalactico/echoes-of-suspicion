using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mapa esquemático del Guía (Propuesta_Tecnica, sección 7): dibuja la
/// geometría abstracta del bioma activo (imagen de fondo asignada en el
/// Canvas del Guía por bioma), la posición en tiempo real del Corredor, y
/// las criaturas detectadas dentro de su radio de percepción compartido.
///
/// Vive en el Canvas del HUD del Guía, junto al resto de paneles (estación
/// de crafteo, inventario). No es un NetworkBehaviour: es puramente de
/// presentación en el cliente del Guía. Los datos que consume ya llegan
/// replicados/filtrados por Mirror:
/// - Posición del Corredor: vía NetworkTransform normal (todos los clientes
///   la reciben; este script solo necesita encontrar el objeto correcto).
/// - Criaturas detectadas: vía RunnerCreatureAwareness.OnMapBlipsUpdated,
///   que se dispara ÚNICAMENTE en la conexión del Guía (TargetRpc del lado
///   servidor), así que ya viene pre-filtrado — este script no decide qué
///   se ve, solo lo dibuja.
///
/// Este script NO depende de si el Canvas está en modo Overlay o World
/// Space — es el mismo comportamiento tanto para un HUD como para un
/// monitor físico en la escena del cuarto del Guía; el Rect Transform de
/// mapArea funciona igual en ambos casos.
///
/// Como el monitor es un objeto físico dentro del mundo compartido (no un
/// overlay pegado a la cámara), este componente también existe/corre en la
/// máquina del Corredor si comparten la misma escena de Unity, aunque el
/// Corredor nunca llegue a ver ese cuarto. Por eso trae un "gate" de rol:
/// mapPanelCanvasGroup se oculta y la lógica de actualización se salta por
/// completo salvo que el jugador LOCAL sea el Guía — de paso, si algún día
/// el Corredor llegara a ver el monitor, la pantalla se ve apagada/vacía,
/// coherente con que nunca debería enterarse del contenido del cuarto del
/// Guía. El rol se re-chequea periódicamente (no una sola vez) porque puede
/// cambiar entre actos (intercambio de roles).
/// </summary>
public sealed class GuideMapView : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField, Tooltip("Muestra en consola cada paso: rol detectado, si encontró al Corredor, si falta MapBounds. Útil mientras se arma el monitor por primera vez.")]
    private bool showDebugLogs = true;

    [Header("Local Role Gate")]
    [SerializeField, Tooltip("CanvasGroup que envuelve TODO el panel del mapa (fondo + íconos). Se oculta (alpha 0) si el jugador local no es el Guía. Puede ser el mismo Canvas del monitor o un panel hijo.")]
    private CanvasGroup mapPanelCanvasGroup;

    [SerializeField, Min(0.1f), Tooltip("Cada cuántos segundos se revisa si el jugador local sigue siendo el Guía (el rol puede cambiar entre actos, por el intercambio de roles).")]
    private float roleCheckInterval = 1f;

    [Header("Map Rect")]
    [SerializeField, Tooltip("RectTransform que representa el área del mapa (mismo tamaño/pivote que la imagen de fondo). Los íconos se posicionan dentro de este rect.")]
    private RectTransform mapArea;

    [Header("Runner Icon")]
    [SerializeField, Tooltip("Ícono que representa al Corredor en el mapa. Se mueve en tiempo real siguiendo su posición real (NetworkTransform).")]
    private RectTransform runnerIcon;

    [Header("Creature Icons")]
    [SerializeField, Tooltip("Prefab de ícono de criatura (GuideMapCreatureIcon). Se instancia una vez por criatura netId detectada. Todas las criaturas usan el mismo punto — el color se define en el propio prefab, no por código.")]
    private GuideMapCreatureIcon creatureIconPrefab;

    [Header("Refresh")]
    [SerializeField, Min(0.1f), Tooltip("Cada cuántos segundos se busca/re-intenta encontrar al Corredor en escena. No hace falta cada frame; solo se usa mientras aún no se encontró.")]
    private float runnerLookupInterval = 1f;

    private RunnerCreatureAwareness trackedRunnerAwareness;
    private Transform trackedRunnerTransform;
    private float runnerLookupTimer;

    private bool isLocalPlayerGuide;
    private float roleCheckTimer;
    private bool loggedNoLocalPlayerWarning;
    private bool loggedMapBoundsWarning;
    private float runnerPositionLogTimer;

    private readonly Dictionary<uint, GuideMapCreatureIcon> creatureIcons = new Dictionary<uint, GuideMapCreatureIcon>();
    private readonly HashSet<uint> blipsSeenThisUpdate = new HashSet<uint>();

    private void OnEnable()
    {
        RefreshLocalRoleGate();
        FindAndSubscribeToRunner();
    }

    private void OnDisable()
    {
        UnsubscribeFromRunner();
    }

    private void Update()
    {
        roleCheckTimer += Time.deltaTime;
        if (roleCheckTimer >= roleCheckInterval)
        {
            roleCheckTimer = 0f;
            RefreshLocalRoleGate();
        }

        if (!isLocalPlayerGuide)
        {
            // Pantalla "apagada" para cualquiera que no sea el Guía local:
            // no buscamos al Corredor, no actualizamos íconos, no gastamos ciclos.
            return;
        }

        if (trackedRunnerAwareness == null)
        {
            runnerLookupTimer += Time.deltaTime;
            if (runnerLookupTimer >= runnerLookupInterval)
            {
                runnerLookupTimer = 0f;
                FindAndSubscribeToRunner();
            }
        }

        UpdateRunnerIcon();
        TickStaleCreatureIcons();
    }

    /// <summary>
    /// Revisa si el jugador LOCAL (isLocalPlayer) tiene rol Guide ahora mismo,
    /// y muestra/oculta mapPanelCanvasGroup en consecuencia. Se llama al
    /// activarse el componente y luego cada roleCheckInterval, porque el rol
    /// puede cambiar durante la partida (intercambio de roles entre actos).
    /// </summary>
    private void RefreshLocalRoleGate()
    {
        bool wasGuide = isLocalPlayerGuide;
        isLocalPlayerGuide = false;

        var providers = FindObjectsByType<CharacterStatsProvider>(FindObjectsSortMode.None);
        bool foundLocalPlayer = false;

        foreach (var provider in providers)
        {
            if (!provider.isLocalPlayer)
            {
                continue;
            }

            foundLocalPlayer = true;
            isLocalPlayerGuide = provider.Role == PlayerRole.Guide;
            break;
        }

        if (showDebugLogs && !foundLocalPlayer && !loggedNoLocalPlayerWarning)
        {
            // Normal durante el primer segundo mientras Mirror todavía está
            // spawneando al Player local — si esto se repite indefinidamente,
            // es que este GuideMapView no está en la misma escena/conexión
            // que el Player, o el Player local aún no tiene CharacterStatsProvider.
            Debug.Log("[GuideMapView] Todavía no se encontró ningún Player local con CharacterStatsProvider en escena.");
            loggedNoLocalPlayerWarning = true;
        }

        if (foundLocalPlayer)
        {
            loggedNoLocalPlayerWarning = false;
        }

        if (showDebugLogs && isLocalPlayerGuide != wasGuide)
        {
            Debug.Log($"[GuideMapView] Rol local detectado: {(isLocalPlayerGuide ? "Guide (mapa visible)" : "no-Guide (mapa oculto)")}");
        }

        if (mapPanelCanvasGroup != null)
        {
            mapPanelCanvasGroup.alpha = isLocalPlayerGuide ? 1f : 0f;
            mapPanelCanvasGroup.interactable = isLocalPlayerGuide;
            mapPanelCanvasGroup.blocksRaycasts = isLocalPlayerGuide;
        }
        else if (showDebugLogs && isLocalPlayerGuide)
        {
            Debug.LogWarning("[GuideMapView] Map Panel Canvas Group no está asignado — el panel no se ocultará para el Corredor, pero el mapa del Guía debería seguir funcionando igual.");
        }

        // Si dejó de ser Guía (cambio de rol entre actos), soltamos la
        // suscripción al Corredor anterior para no seguir procesando en vano.
        if (wasGuide && !isLocalPlayerGuide)
        {
            UnsubscribeFromRunner();
        }
    }

    /// <summary>
    /// Busca entre los Players en escena cuál tiene rol Runner (el rol se
    /// sincroniza por SyncVar en CharacterStatsProvider, así que ya está
    /// disponible en el cliente del Guía sin RPC extra) y se suscribe a su
    /// RunnerCreatureAwareness para recibir los blips del mapa.
    /// </summary>
    private void FindAndSubscribeToRunner()
    {
        if (!isLocalPlayerGuide)
        {
            return;
        }

        var providers = FindObjectsByType<CharacterStatsProvider>(FindObjectsSortMode.None);
        foreach (var provider in providers)
        {
            if (provider.Role != PlayerRole.Runner)
            {
                continue;
            }

            var awareness = provider.GetComponent<RunnerCreatureAwareness>();
            if (awareness == null)
            {
                continue;
            }

            trackedRunnerAwareness = awareness;
            trackedRunnerTransform = provider.transform;
            trackedRunnerAwareness.OnMapBlipsUpdated += HandleMapBlipsUpdated;

            if (showDebugLogs)
            {
                Debug.Log($"[GuideMapView] Corredor encontrado y suscrito ({provider.name}).");
            }

            return;
        }

        if (showDebugLogs)
        {
            Debug.Log("[GuideMapView] No se encontró ningún Player con rol Runner todavía (reintenta cada runnerLookupInterval).");
        }
    }

    private void UnsubscribeFromRunner()
    {
        if (trackedRunnerAwareness != null)
        {
            trackedRunnerAwareness.OnMapBlipsUpdated -= HandleMapBlipsUpdated;
        }

        trackedRunnerAwareness = null;
        trackedRunnerTransform = null;
    }

    private void UpdateRunnerIcon()
    {
        if (runnerIcon == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("[GuideMapView] Runner Icon no está asignado en el Inspector.");
            }
            return;
        }

        if (trackedRunnerTransform == null)
        {
            return; // Ya se loguea en FindAndSubscribeToRunner.
        }

        if (MapBounds.Current == null)
        {
            if (showDebugLogs && !loggedMapBoundsWarning)
            {
                Debug.LogWarning("[GuideMapView] MapBounds.Current es null — no hay ningún MapBounds activo en la escena cargada. Sin esto, ni el ícono del Corredor ni el de las criaturas se pueden posicionar.");
                loggedMapBoundsWarning = true;
            }
            return;
        }

        loggedMapBoundsWarning = false;

        Vector2 uv = MapBounds.Current.WorldToMapUV(trackedRunnerTransform.position);
        Vector2 anchoredPosition = UvToAnchoredPosition(uv);
        runnerIcon.anchoredPosition = anchoredPosition;

        if (showDebugLogs)
        {
            runnerPositionLogTimer += Time.deltaTime;
            if (runnerPositionLogTimer >= 1f)
            {
                runnerPositionLogTimer = 0f;

                bool outOfBounds = uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f;
                string boundsWarning = outOfBounds
                    ? " ⚠ UV fuera de [0,1] — el Corredor está fuera del rectángulo que definen las esquinas del MapBounds, el punto se está dibujando fuera del área visible del mapa."
                    : string.Empty;

                Debug.Log($"[GuideMapView] Runner world={trackedRunnerTransform.position} → uv={uv} → anchoredPosition={anchoredPosition} (mapArea.rect={(mapArea != null ? mapArea.rect.ToString() : "null")}).{boundsWarning}");
            }
        }
    }

    /// <summary>
    /// Recibe la lista de criaturas actualmente dentro del radio compartido
    /// (ya filtrada por el servidor). Actualiza/crea íconos para las que
    /// siguen ahí, y marca como "última posición conocida" (stale) las que
    /// dejaron de aparecer.
    /// </summary>
    private void HandleMapBlipsUpdated(CreatureMapBlip[] blips)
    {
        if (MapBounds.Current == null)
        {
            return;
        }

        blipsSeenThisUpdate.Clear();

        foreach (var blip in blips)
        {
            blipsSeenThisUpdate.Add(blip.creatureNetId);

            Vector2 anchoredPosition = UvToAnchoredPosition(MapBounds.Current.WorldToMapUV(blip.worldPosition));

            if (!creatureIcons.TryGetValue(blip.creatureNetId, out var icon))
            {
                icon = CreateCreatureIcon(blip.creatureNetId);
            }

            icon.UpdateBlip(anchoredPosition);
        }

        foreach (var pair in creatureIcons)
        {
            if (!blipsSeenThisUpdate.Contains(pair.Key))
            {
                pair.Value.MarkStale();
            }
        }
    }

    private GuideMapCreatureIcon CreateCreatureIcon(uint creatureNetId)
    {
        // Todas las criaturas se ven igual en el mapa (un punto rojo) — el
        // color/sprite vive fijo en el Image del prefab, no se resuelve por
        // tipo de criatura. Si más adelante quieren diferenciar tipos, ahí
        // es donde se agregaría esa lógica de nuevo.
        var icon = Instantiate(creatureIconPrefab, mapArea != null ? mapArea : transform);
        icon.Initialize(creatureNetId);

        if (showDebugLogs)
        {
            Debug.Log($"[GuideMapView] Ícono creado para criatura netId={creatureNetId}.");
        }

        creatureIcons[creatureNetId] = icon;
        return icon;
    }

    private void TickStaleCreatureIcons()
    {
        if (creatureIcons.Count == 0)
        {
            return;
        }

        List<uint> expired = null;

        foreach (var pair in creatureIcons)
        {
            if (pair.Value.TickStaleAndCheckExpired(Time.deltaTime))
            {
                expired ??= new List<uint>();
                expired.Add(pair.Key);
            }
        }

        if (expired == null)
        {
            return;
        }

        foreach (var netId in expired)
        {
            if (creatureIcons.TryGetValue(netId, out var icon) && icon != null)
            {
                Destroy(icon.gameObject);
            }

            creatureIcons.Remove(netId);
        }
    }

    /// <summary>Convierte coordenadas normalizadas 0-1 a anchoredPosition dentro de mapArea.</summary>
    private Vector2 UvToAnchoredPosition(Vector2 uv)
    {
        if (mapArea == null)
        {
            return Vector2.zero;
        }

        Rect rect = mapArea.rect;
        return new Vector2(rect.x + uv.x * rect.width, rect.y + uv.y * rect.height);
    }
}
