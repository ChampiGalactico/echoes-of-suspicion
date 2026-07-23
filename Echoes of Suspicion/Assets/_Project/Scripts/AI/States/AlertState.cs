using UnityEngine;

/// <summary>
/// La criatura escuchó un ruido y va a investigar.
///
/// TARGET LOCK: se compromete con el jugador que causó el ruido durante
/// un tiempo mínimo antes de considerar reaccionar a otros jugadores.
///
/// Al llegar al punto del ruido, espera N segundos y vuelve a patrullar.
/// </summary>
public sealed class AlertState : ICreatureState
{
    private readonly CreatureController creature;
    private Vector3 currentNoisePosition;

    /// <summary>NetId del jugador que causó el ruido inicial.</summary>
    public uint TargetPlayerNetId { get; private set; }

    /// <summary>Timestamp hasta el cual estamos comprometidos con el target actual.</summary>
    private float commitmentEndTime;

    private float investigationEndTime;
    private bool hasReachedNoise;

    public AlertState(CreatureController creature, Vector3 noisePosition, uint targetPlayerNetId)
    {
        this.creature = creature;
        this.currentNoisePosition = noisePosition;
        this.TargetPlayerNetId = targetPlayerNetId;
    }

    public void Enter()
    {
        creature.Agent.speed = creature.Data.alertSpeed;
        creature.Agent.SetDestination(currentNoisePosition);
        hasReachedNoise = false;

        // Compromiso con este target por N segundos.
        commitmentEndTime = Time.time + creature.Data.targetCommitmentTime;

        Debug.Log($"[AlertState] Investigando ruido de player {TargetPlayerNetId} en {currentNoisePosition}");
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

                // Verificar si el jugador target sigue cerca — si sí, pasar a Chase directamente.
                CheckForChaseTransition();
            }
            return;
        }

        // Fase 2: en el punto del ruido, esperando (y verificando si el target reapareció cerca).
        CheckForChaseTransition();

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

    /// <summary>
    /// Si el jugador target está dentro del radio de detección "cercana", transiciona a Chase.
    /// </summary>
    private void CheckForChaseTransition()
    {
        Transform targetTransform = CreaturePerceptionUtils.FindPlayerTransformByNetId(TargetPlayerNetId);

        if (targetTransform == null)
        {
            return;
        }

        float distance = Vector3.Distance(creature.transform.position, targetTransform.position);

        // Umbral de "está cerca de verdad" — usamos una fracción del hearing radius
        // para que no confunda con el radio completo de escucha.
        float closeDistanceThreshold = creature.Data.hearingRadius * 0.3f;

        if (distance <= closeDistanceThreshold)
        {
            Debug.Log($"[AlertState] Player {TargetPlayerNetId} está cerca. Cambia a ChaseState.");
            creature.ChangeState(new ChaseState(creature, TargetPlayerNetId));
        }
    }

    /// <summary>
    /// Método público llamado desde CreatureNoisePerception cuando llega un ruido nuevo
    /// mientras la criatura ya está en Alert.
    /// Decide si actualizar el destino según si es el mismo target o si vale la pena cambiar.
    /// </summary>
    public void OnNoiseReceived(NoiseEvent noiseEvent)
    {
        // Si es del mismo target que ya estamos siguiendo → actualizar destino.
        if (noiseEvent.sourcePlayerNetId == TargetPlayerNetId)
        {
            RefreshDestination(noiseEvent.worldPosition);
            return;
        }

        // Si es otro jugador y todavía estamos comprometidos con el target actual → ignorar.
        if (Time.time < commitmentEndTime)
        {
            Debug.Log($"[AlertState] Ignoro ruido de player {noiseEvent.sourcePlayerNetId} " +
                      $"(sigo comprometida con player {TargetPlayerNetId})");
            return;
        }

        // Compromiso terminado. ¿El nuevo target está más cerca que el anterior?
        float distanceToNewNoise = Vector3.Distance(
            creature.transform.position,
            noiseEvent.worldPosition);

        float distanceToCurrentTarget = Vector3.Distance(
            creature.transform.position,
            currentNoisePosition);

        if (distanceToNewNoise < distanceToCurrentTarget)
        {
            Debug.Log($"[AlertState] Cambio de target: player {TargetPlayerNetId} " +
                      $"→ player {noiseEvent.sourcePlayerNetId} (más cerca).");

            TargetPlayerNetId = noiseEvent.sourcePlayerNetId;
            currentNoisePosition = noiseEvent.worldPosition;

            // Nuevo compromiso.
            commitmentEndTime = Time.time + creature.Data.targetCommitmentTime;

            RefreshDestination(noiseEvent.worldPosition);
        }
    }

    private void RefreshDestination(Vector3 newPosition)
    {
        currentNoisePosition = newPosition;
        creature.Agent.SetDestination(newPosition);

        // Si ya había llegado al destino anterior, ahora tiene que caminar de nuevo.
        if (hasReachedNoise)
        {
            hasReachedNoise = false;
        }
    }

    private bool HasReachedDestination()
    {
        return !creature.Agent.pathPending &&
               creature.Agent.remainingDistance <= creature.Agent.stoppingDistance + 0.1f;
    }
}