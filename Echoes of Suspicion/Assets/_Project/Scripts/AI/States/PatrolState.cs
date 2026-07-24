using UnityEngine;

/// <summary>
/// Estado por defecto: la criatura camina entre los waypoints configurados.
/// Se resetea la disponibilidad de aturdimiento al entrar aquí.
/// </summary>
public sealed class PatrolState : ICreatureState
{
    private readonly CreatureController creature;
    private int currentWaypointIndex;

    public PatrolState(CreatureController creature)
    {
        this.creature = creature;
    }

    public void Enter()
    {
        // Volver a Patrol restaura la capacidad de ser aturdida.
        creature.ResetStunAvailability();

        // Velocidad lenta al patrullar.
        creature.Agent.speed = creature.Data.patrolSpeed;

        // Empezar por el waypoint más cercano.
        currentWaypointIndex = FindClosestWaypointIndex();
        MoveToCurrentWaypoint();
    }

    public void Update()
    {
        // Si llegamos al waypoint actual, pasar al siguiente.
        if (HasReachedDestination())
        {
            AdvanceToNextWaypoint();
        }
    }

    public void Exit()
    {
        // No hay nada que limpiar al salir de Patrol.
    }

    private void MoveToCurrentWaypoint()
    {
        if (creature.Waypoints == null || creature.Waypoints.Length == 0)
        {
            Debug.LogWarning("[PatrolState] La criatura no tiene waypoints configurados.");
            return;
        }

        var target = creature.Waypoints[currentWaypointIndex];
        creature.Agent.SetDestination(target.position);
    }

    private void AdvanceToNextWaypoint()
    {
        currentWaypointIndex = (currentWaypointIndex + 1) % creature.Waypoints.Length;
        MoveToCurrentWaypoint();
    }

    private bool HasReachedDestination()
    {
        // El agente NavMesh considera que "llegó" cuando la distancia restante
        // es menor que su stopping distance + un margen.
        return !creature.Agent.pathPending &&
               creature.Agent.remainingDistance <= creature.Agent.stoppingDistance + 0.1f;
    }

    private int FindClosestWaypointIndex()
    {
        if (creature.Waypoints == null || creature.Waypoints.Length == 0)
        {
            return 0;
        }

        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < creature.Waypoints.Length; i++)
        {
            float distance = Vector3.Distance(
                creature.transform.position,
                creature.Waypoints[i].position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}