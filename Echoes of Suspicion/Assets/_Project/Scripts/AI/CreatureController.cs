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
    private CreatureStateType currentStateType = CreatureStateType.Patrol;

    /// <summary>
    /// Flag que indica si la criatura puede ser aturdida ahora mismo.
    /// Se resetea a true cuando vuelve a Patrol.
    /// </summary>
    public bool CanBeStunned { get; private set; } = true;

    // Acceso a los datos y componentes (los estados los necesitan).
    public CreatureData Data => data;
    public NavMeshAgent Agent { get; private set; }
    public Transform[] Waypoints => patrolWaypoints;

    private ICreatureState currentState;

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

        currentState?.Update();
    }

    /// <summary>
    /// Cambia el estado de la criatura.
    /// Solo funciona en el servidor.
    /// </summary>
    public void ChangeState(ICreatureState newState)
    {
        if (!isServer)
        {
            Debug.LogWarning("[CreatureController] ChangeState llamado en el cliente. Se ignora.");
            return;
        }

        currentState?.Exit();
        currentState = newState;

        // Actualiza el tipo sincronizado según el estado concreto.
        currentStateType = GetStateType(newState);

        currentState?.Enter();

        Debug.Log($"[CreatureController] Cambio de estado: {currentStateType}");
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
            _ => CreatureStateType.Patrol
        };
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
    Enraged,
    Stunned,
    Attacking
}