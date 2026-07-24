using Mirror;
using UnityEngine;

/// <summary>
/// La criatura está persiguiendo activamente a un jugador específico.
///
/// Mientras tiene línea de vista, lo sigue directamente.
/// Si PIERDE la línea de vista, no reacciona de inmediato — sigue persiguiendo
/// hacia la última posición conocida durante un pequeño período de gracia
/// (creature.Data.visionLossGracePeriod). Si recupera la vista dentro de ese
/// margen, todo sigue normal, sin ningún cambio de estado (esto absorbe
/// parpadeos cortos: esquinas, obstáculos pequeños, el jugador rodeándola por
/// la espalda). Solo si el margen se agota sin recuperar la vista, transiciona
/// a SearchState para investigar el último punto visto.
///
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

    // Período de gracia: cuándo empezó la pérdida de visión actual, y la
    // última posición conocida mientras dura esa pérdida (por si se agota
    // el margen y hay que pasar a SearchState con el punto correcto).
    private float visionLostTime;
    private Vector3 lastKnownPositionDuringGrace;

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

            // Recuperó la vista — cancela cualquier período de gracia en curso.
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
            if (wasVisibleLastCheck)
            {
                // Acabamos de perder la visión ESTE frame — arranca el período
                // de gracia, sin cambiar de estado todavía. Mientras dure la
                // gracia, la criatura sigue persiguiendo hacia esta posición.
                wasVisibleLastCheck = false;
                visionLostTime = Time.time;
                lastKnownPositionDuringGrace = targetTransform.position;

            }

            // Mientras dure el período de gracia, sigue persiguiendo hacia la
            // última posición conocida (no se queda quieta esperando).
            creature.Agent.SetDestination(lastKnownPositionDuringGrace);

            float timeSinceVisionLost = Time.time - visionLostTime;
            if (timeSinceVisionLost >= creature.Data.visionLossGracePeriod)
            {
                creature.ChangeState(new SearchState(creature, targetPlayerNetId, lastKnownPositionDuringGrace));
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