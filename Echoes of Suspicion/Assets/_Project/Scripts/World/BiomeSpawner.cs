using Mirror;
using UnityEngine;


// <summary> 
// El BiomeSpawner es el lugar correcto para todo lo que sea "contenido específico de este bioma" que necesite 
// inicializarse cuando el server arranca en esa escena. Candidatos naturales a futuro:

// - Spawn points de jugadores por rol — guideSpawnPoint / runnerSpawnPoint. Es el más importante y probablemente 
// el próximo que se agregue.

// - Materiales recolectables del bioma, si necesitan spawnearse dinámicamente 
// (si son estáticos ya colocados en la escena, no necesitan spawner).

// - Puzzles del bioma, si tienen estado inicial que configurar en el servidor.

// - Puerta de salida / trigger de fin de bioma — referencia que otros sistemas puedan consultar.

// Cuando el servidor arranca en esta escena, spawnea las criaturas configuradas.
// </summary>
public sealed class BiomeSpawner : NetworkBehaviour
{
    [Header("Creatures")]
    [SerializeField, Tooltip("Configuración de criaturas a spawnear en este bioma.")]
    private CreatureSpawnConfig[] creatureSpawns;

    [Header("Players Spawn Points")]
    [SerializeField] private Transform guideSpawnPoint;
    [SerializeField] private Transform runnerSpawnPoint;

    public override void OnStartServer()
    {
        base.OnStartServer();
        SpawnCreatures();
    }

    private void SpawnCreatures()
    {
        if (creatureSpawns == null || creatureSpawns.Length == 0)
        {
            Debug.LogWarning("[BiomeSpawner] No hay criaturas configuradas para este bioma.");
            return;
        }

        foreach (var config in creatureSpawns)
        {
            if (config.creaturePrefab == null || config.spawnPoint == null)
            {
                Debug.LogWarning("[BiomeSpawner] Config de criatura incompleta, saltando.");
                continue;
            }

            var creature = Instantiate(
                config.creaturePrefab,
                config.spawnPoint.position,
                config.spawnPoint.rotation);

            var controller = creature.GetComponent<CreatureController>();
            if (controller != null && config.patrolWaypoints != null)
            {
                controller.SetPatrolWaypoints(config.patrolWaypoints);
            }

            NetworkServer.Spawn(creature);

            Debug.Log($"[BiomeSpawner] Criatura spawneada: {config.creaturePrefab.name} " +
                      $"en {config.spawnPoint.position} " +
                      $"con {config.patrolWaypoints?.Length ?? 0} waypoints.");
        }
    }
}

/// <summary>
/// Configuración de una criatura para spawnear al arrancar el server en este bioma.
/// Contiene el prefab, dónde spawnearla y su ruta de patrullaje.
/// </summary>
[System.Serializable]
public sealed class CreatureSpawnConfig
{
    [Tooltip("Prefab de la criatura.")]
    public GameObject creaturePrefab;

    [Tooltip("Punto donde spawnear.")]
    public Transform spawnPoint;

    [Tooltip("Waypoints por donde patrullará esta criatura específica.")]
    public Transform[] patrolWaypoints;
}