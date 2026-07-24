using UnityEngine;

/// <summary>
/// La criatura está lo bastante cerca del jugador target para atacarlo.
/// Ataca cada attackCooldown segundos mientras el target siga dentro de
/// attackRadius. Si se aleja, vuelve a ChaseState (sigue la persecución).
/// </summary>
public sealed class AttackState : ICreatureState, ITargetedState
{
    private readonly CreatureController creature;
    private readonly uint targetPlayerNetId;
    public uint TargetPlayerNetId => targetPlayerNetId;

    private Transform targetTransform;
    private float lastAttackTime;

    public AttackState(CreatureController creature, uint targetPlayerNetId)
    {
        this.creature = creature;
        this.targetPlayerNetId = targetPlayerNetId;
    }

    public void Enter()
    {
        creature.Agent.speed = 0f; // se detiene para atacar, no sigue caminando encima del jugador
        creature.Agent.ResetPath();
        targetTransform = PlayerUtils.FindPlayerTransformByNetId(targetPlayerNetId);
        lastAttackTime = -creature.Data.attackCooldown; // permite atacar apenas entra

        Debug.Log($"[AttackState] Atacando a player {targetPlayerNetId}");
    }

    public void Update()
    {
        if (targetTransform == null)
        {
            targetTransform = PlayerUtils.FindPlayerTransformByNetId(targetPlayerNetId);

            if (targetTransform == null)
            {
                creature.ChangeState(new PatrolState(creature));
                return;
            }
        }

        float distance = Vector3.Distance(creature.transform.position, targetTransform.position);

        if (distance > creature.Data.attackRadius)
        {
            Debug.Log("[AttackState] Target salió del radio de ataque. Vuelve a perseguir.");
            creature.ChangeState(new ChaseState(creature, targetPlayerNetId));
            return;
        }

        if (Time.time - lastAttackTime >= creature.Data.attackCooldown)
        {
            lastAttackTime = Time.time;
            TryDealDamage(targetTransform);
        }
    }

    public void Exit()
    {
        // Restaurar velocidad para el próximo estado que la necesite (Chase la vuelve a setear igual).
    }

    private void TryDealDamage(Transform target)
    {
        var health = target.GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogWarning("[AttackState] El target no tiene PlayerHealth.");
            return;
        }

        health.TakeDamage(creature.Data.attackDamage);
        Debug.Log($"[AttackState] Golpe: {creature.Data.attackDamage} de daño a player {targetPlayerNetId}");
    }
}