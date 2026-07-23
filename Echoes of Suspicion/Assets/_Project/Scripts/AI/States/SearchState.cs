using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// La criatura perdió la visión de un jugador que estaba persiguiendo activamente.
/// Va a investigar la última posición donde lo vio ("se escondió aquí, sigo cerca").
///
/// Si recupera contacto (visual o auditivo) durante la búsqueda, retoma la caza.
/// Si se agota el tiempo sin nada, vuelve a patrullar.
/// </summary>
public sealed class SearchState : ICreatureState
{
    private readonly CreatureController creature;
    private readonly uint targetPlayerNetId;
    private readonly Vector3 lastKnownPosition;

    private CreatureVisualPerception visualPerception;
    private float searchEndTime;
    private bool hasReachedSearchPoint;

    private float nextLookChangeTime;
    private Quaternion targetLookRotation;
    private const float LookChangeInterval = 2f;
    private const float LookRotationSpeed = 180f; // grados por segundo

    public SearchState(CreatureController creature, uint targetPlayerNetId, Vector3 lastKnownPosition)
    {
        this.creature = creature;
        this.targetPlayerNetId = targetPlayerNetId;
        this.lastKnownPosition = lastKnownPosition;
    }

    public void Enter()
    {
        visualPerception = creature.GetComponent<CreatureVisualPerception>();
        creature.Agent.speed = creature.Data.alertSpeed;
        creature.Agent.SetDestination(lastKnownPosition);
        hasReachedSearchPoint = false;

        targetLookRotation = creature.transform.rotation;
        nextLookChangeTime = 0f; // fuerza elegir un nuevo ángulo apenas llegue al punto

        Debug.Log($"[SearchState] Perdió de vista a player {targetPlayerNetId}. " +
                  $"Va a investigar dónde se escondió.");
    }

    public void Update()
    {
        Transform targetTransform = CreaturePerceptionUtils.FindPlayerTransformByNetId(targetPlayerNetId);

        // Si lo vuelve a ver, retoma la caza directamente.
        if (targetTransform != null &&
            visualPerception != null &&
            visualPerception.HasLineOfSight(targetTransform))
        {
            Debug.Log("[SearchState] Recuperó la visión del jugador. Vuelve a perseguir.");
            creature.ChangeState(new ChaseState(creature, targetPlayerNetId));
            return;
        }

        if (!hasReachedSearchPoint)
        {
            if (HasReachedDestination())
            {
                hasReachedSearchPoint = true;
                searchEndTime = Time.time + creature.Data.searchDuration;
                Debug.Log("[SearchState] Llegó al punto donde lo vio. Buscando alrededor...");
            }
            return;
        }

        HandleLookAround();

        if (Time.time >= searchEndTime)
        {
            Debug.Log("[SearchState] No encontró al jugador. Vuelve a patrullar.");
            creature.ChangeState(new PatrolState(creature));
        }
    }

    public void Exit()
    {
    }

    /// <summary>
    /// Si escucha al mismo jugador mientras busca, redirige la búsqueda hacia el nuevo ruido.
    /// </summary>
    public void OnNoiseReceived(NoiseEvent noiseEvent)
    {
        if (noiseEvent.sourcePlayerNetId != targetPlayerNetId)
        {
            return;
        }

        creature.Agent.SetDestination(noiseEvent.worldPosition);
        hasReachedSearchPoint = false;

        Debug.Log("[SearchState] Escuchó de nuevo al jugador. Redirige búsqueda.");
    }

    private bool HasReachedDestination()
    {
        return !creature.Agent.pathPending &&
               creature.Agent.remainingDistance <= creature.Agent.stoppingDistance + 0.1f;
    }

    private void HandleLookAround()
    {
        if (Time.time >= nextLookChangeTime)
        {
            // Elige un nuevo ángulo aleatorio para "mirar" (relativo a la orientación actual).
            float randomYaw = Random.Range(-180f, 180f);
            targetLookRotation = Quaternion.Euler(0f, creature.transform.eulerAngles.y + randomYaw, 0f);
            nextLookChangeTime = Time.time + LookChangeInterval;
        }

        creature.transform.rotation = Quaternion.RotateTowards(
            creature.transform.rotation,
            targetLookRotation,
            LookRotationSpeed * Time.deltaTime);
    }
}