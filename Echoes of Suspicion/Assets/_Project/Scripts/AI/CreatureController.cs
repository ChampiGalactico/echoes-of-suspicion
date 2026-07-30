using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Componente principal de una criatura.
///
/// - Vive en el servidor (Server Authority): los cálculos de IA se hacen aquí.
/// - Sincroniza el destino del NavMeshAgent a los clientes para que cada uno
///   navegue localmente (movimiento 100% fluido, sin depender de NetworkTransform).
/// - Sincroniza el estado actual como SyncVar para que los clientes puedan
///   reproducir animaciones o efectos distintos según el estado.
///
/// IMPORTANTE: quitar NetworkTransformReliable del prefab. La posición se
/// sincroniza vía NavMeshAgent local en cada cliente.
///
/// La lógica real está en las clases de estado (PatrolState, AlertState, etc.).
/// Este componente solo orquesta.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class CreatureController : NetworkBehaviour
{
    [Header("Configuration")]
    [SerializeField, Tooltip("Datos del tipo de criatura (velocidades, radios, etc.).")]
    private CreatureData data;

    [Header("Patrol")]
    [SerializeField, Tooltip("Puntos por donde la criatura patrulla. Se colocan en la escena.")]
    private Transform[] patrolWaypoints;

    /// <summary>
    /// Estado actual sincronizado por red. Los clientes lo usan para
    /// reproducir animaciones o efectos distintos.
    /// </summary>
    [SyncVar(hook = nameof(OnStateTypeChanged))]
    private CreatureStateType CurrentStateType = CreatureStateType.Patrol;

    /// <summary>
    /// NetId del jugador objetivo actual. Sincronizado para que los clientes
    /// puedan saber si ellos son el target (efectos de pantalla, etc.).
    /// Se usa uint porque Mirror no soporta uint? como SyncVar; 0 = sin target.
    /// </summary>
    [SyncVar]
    private uint syncTargetNetId;

    // ── Destination sync ─────────────────────────────────────
    // El servidor sincroniza el destino y la velocidad del agente.
    // Los clientes navegan localmente al mismo punto.

    [SyncVar(hook = nameof(OnDestinationChanged))]
    private Vector3 syncDestination;

    [SyncVar(hook = nameof(OnAgentSpeedChanged))]
    private float syncAgentSpeed;

    [SyncVar(hook = nameof(OnAgentStoppedChanged))]
    private bool syncAgentStopped;

    private Vector3 lastSentDestination;
    private float lastSentSpeed;
    private bool lastSentStopped;

    // Distancia mínima para considerar un cambio de destino.
    private const float DestinationSyncThreshold = 0.05f;

    /// <summary>
    /// Flag que indica si la criatura puede ser aturdida ahora mismo.
    /// Se resetea a true cuando vuelve a Patrol.
    /// </summary>
    public bool CanBeStunned { get; private set; } = true;

    // Acceso a los datos y componentes (los estados los necesitan).
    public CreatureData Data => data;

    /// <summary>Acceso público al estado sincronizado.</summary>
    public CreatureStateType StateType => CurrentStateType;

    public NavMeshAgent Agent { get; private set; }
    public Transform[] Waypoints => patrolWaypoints;

    public ICreatureState CurrentState { get; private set; }

    [Header("Debug")]
    [SerializeField, Tooltip("Muestra en consola los eventos de ruido recibidos.")]
    private bool showDebugLogs = true;

    /// <summary>
    /// Se dispara cada vez que CUALQUIER criatura cambia de estado.
    /// </summary>
    public static event System.Action<CreatureController, CreatureStateType, uint?> OnAnyCreatureStateChanged;

    /// <summary>
    /// Asigna waypoints a la criatura después de spawneada.
    /// </summary>
    public void SetPatrolWaypoints(Transform[] waypoints)
    {
        patrolWaypoints = waypoints;
        Debug.Log($"[CreatureController] Waypoints asignados: {waypoints?.Length ?? 0}");
    }

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        ChangeState(new PatrolState(this));

        // Initialize sync values.
        syncDestination = transform.position;
        syncAgentSpeed = Agent.speed;
        syncAgentStopped = Agent.isStopped;
        lastSentDestination = syncDestination;
        lastSentSpeed = syncAgentSpeed;
        lastSentStopped = syncAgentStopped;

        Debug.Log($"[CreatureController] {data.creatureName} spawneada en el servidor.");
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // On non-host clients, configure the NavMeshAgent to navigate
        // locally using synced destinations.
        if (!isServer)
        {
            // Apply the current synced state.
            Agent.speed = syncAgentSpeed;
            Agent.isStopped = syncAgentStopped;

            if (!syncAgentStopped)
            {
                Agent.SetDestination(syncDestination);
            }
        }
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        CurrentState?.Update();

        // Sync NavMeshAgent state to clients.
        SyncAgentState();
    }

    /// <summary>
    /// Checks if the agent's destination/speed/stopped changed and
    /// updates the SyncVars so clients can follow.
    /// </summary>
    private void SyncAgentState()
    {
        // Sync destination.
        if (Agent.hasPath)
        {
            Vector3 dest = Agent.destination;
            if (Vector3.Distance(dest, lastSentDestination) > DestinationSyncThreshold)
            {
                syncDestination = dest;
                lastSentDestination = dest;
            }
        }

        // Sync speed.
        float speed = Agent.speed;
        if (!Mathf.Approximately(speed, lastSentSpeed))
        {
            syncAgentSpeed = speed;
            lastSentSpeed = speed;
        }

        // Sync stopped state.
        bool stopped = Agent.isStopped;
        if (stopped != lastSentStopped)
        {
            syncAgentStopped = stopped;
            lastSentStopped = stopped;
        }
    }

    // ── SyncVar hooks (clients only) ─────────────────────────

    private void OnDestinationChanged(Vector3 oldVal, Vector3 newVal)
    {
        if (isServer) return;

        if (Agent.enabled && !syncAgentStopped)
        {
            Agent.SetDestination(newVal);
        }
    }

    private void OnAgentSpeedChanged(float oldVal, float newVal)
    {
        if (isServer) return;

        if (Agent.enabled)
        {
            Agent.speed = newVal;
        }
    }

    private void OnStateTypeChanged(CreatureStateType oldVal, CreatureStateType newVal)
    {
        // On clients, fire the event so ScreenEffectsController and other
        // client-side listeners react to state changes.
        if (isServer) return;

        uint? targetId = syncTargetNetId != 0 ? syncTargetNetId : (uint?)null;
        OnAnyCreatureStateChanged?.Invoke(this, newVal, targetId);
    }

    private void OnAgentStoppedChanged(bool oldVal, bool newVal)
    {
        if (isServer) return;

        if (Agent.enabled)
        {
            Agent.isStopped = newVal;

            if (!newVal)
            {
                Agent.SetDestination(syncDestination);
            }
        }
    }

    /// <summary>
    /// Cambia el estado de la criatura.
    /// Solo funciona en el servidor.
    /// </summary>
    public void ChangeState(ICreatureState newState)
    {
        uint? previousTargetNetId = CurrentState is ITargetedState prevTargeted
            ? prevTargeted.TargetPlayerNetId
            : (uint?)null;

        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();

        uint? targetNetId = newState is ITargetedState targeted
            ? targeted.TargetPlayerNetId
            : previousTargetNetId;

        // Update synced target BEFORE state type so the hook has the right target.
        syncTargetNetId = targetNetId ?? 0;
        CurrentStateType = GetStateType(newState);

        if (showDebugLogs)
        {
            Debug.Log($"[CreatureController] Cambio de estado: {CurrentStateType}");
        }

        // Fire locally on the server (the SyncVar hook fires on clients).
        OnAnyCreatureStateChanged?.Invoke(this, CurrentStateType, targetNetId);
    }

    public void ConsumeStunAvailability()
    {
        CanBeStunned = false;
    }

    public void ResetStunAvailability()
    {
        CanBeStunned = true;
    }

    private static CreatureStateType GetStateType(ICreatureState state)
    {
        return state switch
        {
            PatrolState => CreatureStateType.Patrol,
            AlertState => CreatureStateType.Alert,
            ChaseState => CreatureStateType.Chase,
            SearchState => CreatureStateType.Search,
            AttackState => CreatureStateType.Attacking,
            _ => CreatureStateType.Patrol
        };
    }

    private void OnDrawGizmosSelected()
    {
        if (data == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.hearingRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, data.hearingRadius * 0.3f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, data.visionRadius);

        if (Agent != null && Agent.hasPath)
        {
            Vector3 destination = Agent.destination;

            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(destination, 0.4f);
            Gizmos.DrawLine(transform.position, destination);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(destination + Vector3.up * 0.6f, $"Target: {CurrentStateType}");
#endif
        }
    }
}

/// <summary>
/// Tipos de estado. Sincronizado por red vía SyncVar para que los clientes
/// puedan reproducir animaciones distintas según el estado actual.
/// </summary>
public enum CreatureStateType
{
    Patrol,
    Alert,
    Chase,
    Search,
    Enraged,
    Stunned,
    Attacking
}
