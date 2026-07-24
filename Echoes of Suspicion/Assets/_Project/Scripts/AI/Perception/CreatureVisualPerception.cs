using Mirror;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Percepción visual de la criatura: detecta jugadores dentro de un cono
/// frontal, respetando obstáculos (paredes bloquean la visión).
///
/// Vive en el servidor. Revisa periódicamente (no cada frame) para
/// optimizar rendimiento.
/// </summary>
[RequireComponent(typeof(CreatureController))]
public sealed class CreatureVisualPerception : NetworkBehaviour
{
    [Header("Check Settings")]
    [SerializeField, Tooltip("Cada cuántos segundos se revisa la visión (optimización).")]
    private float checkInterval = 0.2f;

    [SerializeField, Tooltip("Altura desde la base de la criatura donde está su 'ojo' para el raycast.")]
    private float eyeHeight = 1.6f;

    [SerializeField, Tooltip("Capas que bloquean la línea de vista (paredes, obstáculos).")]
    private LayerMask obstacleMask = ~0; // Por defecto, todas las capas bloquean.

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    private CreatureController creature;
    private float lastCheckTime;

    private void Awake()
    {
        creature = GetComponent<CreatureController>();
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (Time.time - lastCheckTime < checkInterval)
        {
            return;
        }

        lastCheckTime = Time.time;
        CheckForVisiblePlayers();
    }

    private void CheckForVisiblePlayers()
    {
        // Si ya está en Chase o Search, ya tiene un target — no buscar otros.
        if (creature.CurrentState is ChaseState || creature.CurrentState is SearchState)
        {
            return;
        }

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null)
            {
                continue;
            }

            var provider = conn.identity.GetComponent<CharacterStatsProvider>();
            if (!EOSNetworkManager.AreProtagonistsReunited &&
                (provider == null || provider.Role != PlayerRole.Runner))
            {
                continue;
            }

            Transform playerTransform = conn.identity.transform;
            
            if (HasLineOfSight(playerTransform))
            {
                if (showDebugLogs)
                {
                    Debug.Log($"[CreatureVisualPerception] 👁️ Jugador {conn.identity.netId} " +
                              $"visible. Cambia a ChaseState.");
                }

                creature.ChangeState(new ChaseState(creature, conn.identity.netId));
                return;
            }
        }
    }

    public bool HasLineOfSight(Transform player, float radiusBonus = 0f)
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = player.position - eyePosition;

        float distance = toPlayer.magnitude;
        if (distance > creature.Data.visionRadius + radiusBonus)
        {
            return false;
        }

        // 2. Chequeo de ángulo.
        Vector3 directionToPlayer = toPlayer.normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        if (angle > creature.Data.visionAngle * 0.5f)
        {
            return false;
        }

        // 3. Chequeo de línea de vista (el más caro computacionalmente hablando).
        if (Physics.Raycast(eyePosition, directionToPlayer, out RaycastHit hit, distance, obstacleMask))
        {
            // Si el raycast pega contra algo que NO es el jugador, hay un obstáculo en medio.
            if (!hit.transform.IsChildOf(player) && hit.transform != player)
            {
                return false;
            }
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (creature == null || creature.Data == null)
        {
            return;
        }

#if UNITY_EDITOR
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        float halfAngle = creature.Data.visionAngle * 0.5f;

        Handles.color = new Color(1f, 0f, 0f, 0.35f); // más opaco (antes 0.15f)
        Handles.DrawSolidArc(
            eyePosition,
            Vector3.up,
            Quaternion.Euler(0, -halfAngle, 0) * transform.forward,
            creature.Data.visionAngle,
            creature.Data.visionRadius);

        Handles.color = new Color(1f, 0f, 0f, 1f); // contorno sólido y brillante
        for (int i = 0; i < 3; i++) // dibuja el contorno varias veces = más grueso
        {
            Handles.DrawWireArc(
                eyePosition,
                Vector3.up,
                Quaternion.Euler(0, -halfAngle, 0) * transform.forward,
                creature.Data.visionAngle,
                creature.Data.visionRadius);
        }
#endif
    }
}