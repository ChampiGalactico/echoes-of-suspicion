using UnityEngine;

/// <summary>
/// Sincroniza las animaciones de la criatura con su estado actual.
///
/// Funciona tanto en servidor como en clientes: lee el CreatureStateType
/// (sincronizado por SyncVar) y ajusta los parámetros del Animator.
///
/// Setup en Unity:
///   1. El modelo FBX (rig) va como hijo del GameObject con CreatureController.
///   2. Este componente va en el MISMO GameObject que el CreatureController (padre).
///   3. El Animator va en el hijo que tiene el modelo (el SkinnedMeshRenderer).
///   4. Crear un Animator Controller con dos parámetros:
///        - "Speed" (float): controla blend entre Idle/Walk/Run.
///        - "Attack" (trigger): dispara la animación de ataque.
///   5. Configurar un Blend Tree 1D con el parámetro "Speed":
///        - 0.0 → Idle (o Walk con speed 0 si no tienes Idle)
///        - 0.5 → Walk
///        - 1.0 → Run
/// </summary>
[RequireComponent(typeof(CreatureController))]
public sealed class CreatureAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Animator del modelo hijo. Si no se asigna, lo busca en los hijos.")]
    private Animator animator;

    [Header("Tuning")]
    [SerializeField, Tooltip("Qué tan rápido transiciona entre animaciones (suavizado).")]
    private float dampTime = 0.15f;

    private CreatureController creature;

    // Hashes cacheados para rendimiento.
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private void Awake()
    {
        creature = GetComponent<CreatureController>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("[CreatureAnimator] No se encontró Animator en los hijos. Asigna uno en el Inspector.");
        }
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        float targetSpeed = GetSpeedForState(creature.StateType);
        animator.SetFloat(SpeedHash, targetSpeed, dampTime, Time.deltaTime);
    }

    /// <summary>
    /// Mapea cada estado a un valor de Speed para el Blend Tree.
    /// 0 = Idle, 0.5 = Walk, 1 = Run.
    /// </summary>
    private static float GetSpeedForState(CreatureStateType state)
    {
        return state switch
        {
            CreatureStateType.Patrol   => 0.5f, // Walk
            CreatureStateType.Alert    => 0.5f, // Walk
            CreatureStateType.Search   => 0.5f, // Walk
            CreatureStateType.Chase    => 1.0f, // Run
            CreatureStateType.Enraged  => 1.0f, // Run
            CreatureStateType.Attacking => 0.0f, // Se detiene para atacar
            CreatureStateType.Stunned  => 0.0f, // Detenida
            _                          => 0.0f
        };
    }

    /// <summary>
    /// Dispara la animación de ataque. Llamar desde AttackState
    /// cuando la criatura golpea.
    /// </summary>
    public void TriggerAttack()
    {
        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
    }
}
