using Mirror;
using UnityEngine;

/// <summary>
/// La criatura está persiguiendo activamente a un jugador específico.
///
/// Mientras tiene línea de vista, lo sigue directamente.
/// Si PIERDE la línea de vista de golpe (se escondió), transiciona a SearchState
/// para investigar el último punto visto.
/// Si nunca lo tuvo a la vista (solo por cercanía/ruido) y pierde el rastro por
/// distancia o silencio prolongado, vuelve directo a Patrol.
/// </summary>
public sealed class ChaseState : ICreatureState, ITargetedState
{
    private readonly CreatureController creature;
    private readonly uint targetPlayerNetId;
    public uint TargetPlayerNetId => targetPlayerNetId;

    private CreatureVisualPerception visualPerception;
    private Transform targetTransform;
    private float lastContactTime;
    private bool wasVisibleLastCheck;

    public ChaseState(CreatureController creature, uint targetPlayerNetId)
    {
        this.creature = creature;
        this.targetPlayerNetId = targetPlayerNetId;
    }

    public void Enter()
    {
        creature.Agent.speed = creature.Data.chaseSpeed;
        visualPerception = creature.GetComponent<CreatureVisualPerception>();
        targetTransform = PlayerUtils.FindPlayerTransformByNetId(targetPlayerNetId);
        lastContactTime = Time.time;

        // Asumimos contacto inicial (por vista o por cercanía que disparó este estado).
        wasVisibleLastCheck = true;

        Debug.Log($"[ChaseState] Persiguiendo activamente a player {targetPlayerNetId}");
    }

    public void Update()
    {
        if (targetTransform == null)
        {
            targetTransform = PlayerUtils.FindPlayerTransformByNetId(targetPlayerNetId);

            if (targetTransform == null)
            {
                Debug.Log("[ChaseState] Target perdido (desconectado). Vuelve a patrullar.");
                creature.ChangeState(new PatrolState(creature));
                return;
            }
        }

        bool canSeeNow = visualPerception != null && visualPerception.HasLineOfSight(targetTransform, creature.Data.chaseVisionRadiusBonus);

        if (canSeeNow)
        {
            lastContactTime = Time.time;
            creature.Agent.SetDestination(targetTransform.position);
            wasVisibleLastCheck = true;

            float distanceToTarget = Vector3.Distance(creature.transform.position, targetTransform.position);
            if (distanceToTarget <= creature.Data.attackRadius)
            {
                Debug.Log("[ChaseState] Target dentro del radio de ataque. Cambia a AttackState.");
                creature.ChangeState(new AttackState(creature, targetPlayerNetId));
                return;
            }
        }
        else
        {
            // Acabamos de perder la visión — investigar dónde se escondió.
            if (wasVisibleLastCheck)
            {
                Vector3 lastKnownPosition = targetTransform.position;
                wasVisibleLastCheck = false;

                Debug.Log("[ChaseState] Perdió de vista al jugador. Investiga dónde se escondió.");
                creature.ChangeState(new SearchState(creature, targetPlayerNetId, lastKnownPosition));
                return;
            }

            // No hay vista, pero podría seguir oyéndolo si sigue cerca.
            float distance = Vector3.Distance(creature.transform.position, targetTransform.position);
            if (distance <= creature.Data.hearingRadius)
            {
                lastContactTime = Time.time;
            }
        }

        // Si no hay contacto (ni visual ni cercanía) por mucho tiempo, se rinde.
        if (Time.time - lastContactTime >= creature.Data.loseTargetTime)
        {
            Debug.Log("[ChaseState] Perdió el rastro del jugador. Vuelve a patrullar.");
            creature.ChangeState(new PatrolState(creature));
        }
    }

    public void Exit()
    {
    }

    /// <summary>
    /// Si escucha al mismo jugador mientras lo persigue, refresca el contacto.
    /// </summary>
    public void OnNoiseReceived(NoiseEvent noiseEvent)
    {
        if (noiseEvent.sourcePlayerNetId == targetPlayerNetId)
        {
            lastContactTime = Time.time;
        }
    }
}