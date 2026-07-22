using UnityEngine;

/// <summary>
/// La criatura escuchó un ruido y va a investigar.
/// Al llegar al punto del ruido, espera N segundos y vuelve a patrullar.
/// </summary>
public sealed class AlertState : ICreatureState
{
    private readonly CreatureController creature;
    private readonly Vector3 noisePosition;

    private float investigationEndTime;
    private bool hasReachedNoise;

    public AlertState(CreatureController creature, Vector3 noisePosition)
    {
        this.creature = creature;
        this.noisePosition = noisePosition;
    }

    public void Enter()
    {
        creature.Agent.speed = creature.Data.alertSpeed;
        creature.Agent.SetDestination(noisePosition);
        hasReachedNoise = false;

        Debug.Log($"[AlertState] Investigando ruido en {noisePosition}");
    }

    public void Update()
    {
        // Fase 1: aún caminando hacia el ruido.
        if (!hasReachedNoise)
        {
            if (HasReachedDestination())
            {
                hasReachedNoise = true;
                investigationEndTime = Time.time + creature.Data.investigationTime;
                Debug.Log("[AlertState] Llegó al punto del ruido. Investigando...");
            }
            return;
        }

        // Fase 2: en el punto del ruido, esperando.
        if (Time.time >= investigationEndTime)
        {
            Debug.Log("[AlertState] Terminó de investigar. Vuelve a patrullar.");
            creature.ChangeState(new PatrolState(creature));
        }
    }

    public void Exit()
    {
        // No hay nada que limpiar.
    }

    private bool HasReachedDestination()
    {
        return !creature.Agent.pathPending &&
               creature.Agent.remainingDistance <= creature.Agent.stoppingDistance + 0.1f;
    }
}