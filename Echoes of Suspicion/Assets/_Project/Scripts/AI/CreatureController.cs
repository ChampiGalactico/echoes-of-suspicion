using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Componente principal de una criatura.
///
/// - Vive en el servidor (Server Authority): los cálculos de IA se hacen aquí.
/// - Sincroniza posición a los clientes automáticamente vía NetworkTransform.
/// - Sincroniza el estado actual como SyncVar para que los clientes puedan
///   reproducir animaciones o efectos distintos según el estado.
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
    [SyncVar]
    private CreatureStateType CurrentStateType = CreatureStateType.Patrol;

    /// <summary>
    /// Flag que indica si la criatura puede ser aturdida ahora mismo.
    /// Se resetea a true cuando vuelve a Patrol.
    /// </summary>
    public bool CanBeStunned { get; private set; } = true;

    // Acceso a los datos y componentes (los estados los necesitan).
    public CreatureData Data => data;

    public NavMeshAgent Agent { get; private set; }
    public Transform[] Waypoints => patrolWaypoints;

    public ICreatureState CurrentState { get; private set; }

    [Header("Debug")]
    [SerializeField, Tooltip("Muestra en consola los eventos de ruido recibidos.")]
    private bool showDebugLogs = true;

    /// <summary>
    /// Se dispara cada vez que CUALQUIER criatura cambia de estado. targetNetId
    /// es null si el nuevo estado no tiene target (ej. Patrol).
    /// </summary>
    public static event System.Action<CreatureController, CreatureStateType, uint?> OnAnyCreatureStateChanged;

    /// <summary>
    /// Asigna waypoints a la criatura después de spawneada.
    /// Debe llamarse ANTES del primer Update — típicamente desde el spawner.
    /// Solo tiene efecto en el servidor.
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

        // Solo el servidor corre la máquina de estados. Los clientes ven la posición
        // vía NetworkTransform y el estado vía [SyncVar].
        ChangeState(new PatrolState(this));

        Debug.Log($"[CreatureController] {data.creatureName} spawneada en el servidor.");
    }

    private void Update()
    {
        // Solo el servidor procesa la lógica de IA.
        if (!isServer)
        {
            return;
        }

        CurrentState?.Update();
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

        CurrentStateType = GetStateType(newState);

        if (showDebugLogs)
        {
            Debug.Log($"[CreatureController] Cambio de estado: {CurrentStateType}");
        }

        // Si el nuevo estado no tiene target propio (ej. Patrol), usamos el
        // target del estado ANTERIOR — así el jugador que estaba siendo
        // investigado se entera de que la amenaza terminó, en vez de que el
        // evento se descarte silenciosamente por no tener a quién avisar.
        uint? targetNetId = newState is ITargetedState targeted
            ? targeted.TargetPlayerNetId
            : previousTargetNetId;

        OnAnyCreatureStateChanged?.Invoke(this, CurrentStateType, targetNetId);
    }

    /// <summary>
    /// Marca que la criatura ya no puede ser aturdida (después de un stun exitoso).
    /// Se resetea cuando la criatura vuelve a Patrol.
    /// </summary>
    public void ConsumeStunAvailability()
    {
        CanBeStunned = false;
    }

    /// <summary>
    /// Restaura la capacidad de ser aturdida. Llamado desde PatrolState.Enter().
    /// </summary>
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

        // Radio de audición (amarillo).
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, data.hearingRadius);

        // Radio de "detección cercana" que dispara Chase (naranja).
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, data.hearingRadius * 0.3f);

        // Radio de visión (rojo) — aún no implementado funcionalmente, pero
        // lo dejamos visible para cuando construyamos la percepción visual.
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