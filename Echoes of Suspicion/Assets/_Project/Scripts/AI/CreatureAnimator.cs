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
///   4. Crear un Animator Controller con un parámetro:
///        - "StateIndex" (int): controla qué animación se reproduce.
///   5. Crear 4 estados en el Animator Controller:
///        - "Patrol"     → clip Unsteady Walk        (StateIndex == 0)
///        - "Alert"      → clip Limping Walk 3        (StateIndex == 1)
///        - "Chase"      → clip Male Head Down Charge (StateIndex == 2)
///        - "LookAround" → clip Look Around           (StateIndex == 3)
///   6. Crear transiciones desde Any State a cada uno, con condición:
///        - Any State → Patrol:     StateIndex equals 0
///        - Any State → Alert:      StateIndex equals 1
///        - Any State → Chase:      StateIndex equals 2
///        - Any State → LookAround: StateIndex equals 3
///   7. En cada transición: desactivar "Has Exit Time", poner Transition Duration en ~0.2.
///   8. Activar Loop Time en todos los clips desde el FBX.
/// </summary>
[RequireComponent(typeof(CreatureController))]
public sealed class CreatureAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Animator del modelo hijo. Si no se asigna, lo busca en los hijos.")]
    private Animator animator;

    private CreatureController creature;

    // Hashes cacheados para rendimiento.
    private static readonly int StateIndexHash = Animator.StringToHash("StateIndex");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private int lastStateIndex = -1;

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

        int stateIndex = GetStateIndex(creature);

        // Solo actualizar si cambió, para no interrumpir la animación en curso.
        if (stateIndex != lastStateIndex)
        {
            animator.SetInteger(StateIndexHash, stateIndex);
            lastStateIndex = stateIndex;
        }
    }

    /// <summary>
    /// Mapea cada estado de la IA a un índice de animación.
    /// 0 = Patrol     (Unsteady Walk)
    /// 1 = Alert      (Limping Walk 3 In Place)
    /// 2 = Chase      (Male Head Down Charge)
    /// 3 = LookAround (Look Around)
    /// </summary>
    private static int GetStateIndex(CreatureController creature)
    {
        // Caso especial: SearchState tiene dos fases.
        // Caminando al punto → Limping Walk, detenida mirando → Look Around.
        if (creature.StateType == CreatureStateType.Search &&
            creature.CurrentState is SearchState search &&
            search.IsLookingAround)
        {
            return 3; // Look Around
        }

        return creature.StateType switch
        {
            CreatureStateType.Patrol    => 0, // Unsteady Walk
            CreatureStateType.Alert     => 1, // Limping Walk 3
            CreatureStateType.Search    => 1, // Limping Walk 3 (caminando al punto)
            CreatureStateType.Chase     => 2, // Male Head Down Charge
            CreatureStateType.Enraged   => 2, // Male Head Down Charge
            CreatureStateType.Attacking => 2, // Se queda en charge mientras ataca
            CreatureStateType.Stunned   => 0, // Vuelve a Unsteady Walk (detenida)
            _                           => 0
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
