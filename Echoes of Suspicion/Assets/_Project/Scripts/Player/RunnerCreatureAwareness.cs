using Mirror;
using UnityEngine;

/// <summary>
/// Detecta criaturas cercanas al Corredor combinando la Perspicacia de
/// ambos jugadores (Corredor + Guía). Solo tiene efecto real cuando este
/// Player tiene rol Runner.
///
/// El radio compartido (lo que vería el Guía en su mapa) usa ambos
/// multiplicadores de Perspicacia, con un tope máximo para evitar radios
/// absurdos.
///
/// El "latido" es una habilidad exclusiva de Perspicacia por encima de 1:
/// no es un radio que se achica, es una habilidad que directamente no existe
/// con Perspicacia baja o neutra. Cuando existe, dispara un pulso puntual
/// cada cierto intervalo mientras haya una criatura dentro del radio — con
/// Perspicacia baja el intervalo es largo (pulsos espaciados), con Perspicacia
/// alta es corto (casi constante, se siente como racha). Es determinístico,
/// no depende de probabilidad.
///
/// Vive en el servidor. El evento OnHeartbeatPulse se replica solo al dueño
/// del Player mediante TargetRpc — es una sensación interna suya, no algo
/// que otros jugadores deban percibir.
/// </summary>
[RequireComponent(typeof(CharacterStatsProvider))]
public sealed class RunnerCreatureAwareness : NetworkBehaviour
{
    [Header("Shared Radius (mapa del Guía)")]
    [SerializeField, Tooltip("Radio base antes de multiplicadores.")]
    private float baseSharedRadius = 20f;

    [SerializeField, Tooltip("Tope máximo del radio compartido, sin importar qué tan altos sean los multiplicadores combinados.")]
    private float maxSharedRadio = 50f;

    [Header("Heartbeat Pulse Interval")]
    [SerializeField, Tooltip("Tiempo entre latidos con la Perspicacia más baja considerada 'con habilidad' (justo encima de 1).")]
    private float slowestHeartbeatInterval = 3f;

    [SerializeField, Tooltip("Tiempo entre latidos con la Perspicacia máxima esperada (ver maxExpectedPerception).")]
    private float fastestHeartbeatInterval = 1f;

    [SerializeField, Tooltip("Multiplicador de Perspicacia considerado el máximo posible, para escalar el intervalo.")]
    private float maxExpectedPerception = 2f;

    [Header("Check Settings")]
    [SerializeField, Tooltip("Cada cuántos segundos se revisa si hay criaturas cerca (optimización).")]
    private float checkInterval = 0.3f;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    /// <summary>Se dispara SOLO en la máquina del dueño del Player, cada vez que "siente" un pulso de latido.</summary>
    public event System.Action OnHeartbeatPulse;

    private CharacterStatsProvider statsProvider;
    private float lastCheckTime;
    private float lastHeartbeatTime;

    public override void OnStartServer()
    {
        base.OnStartServer();
        statsProvider = GetComponent<CharacterStatsProvider>();
    }

    private void Update()
    {
        if (!isServer)
        {
            return;
        }

        if (statsProvider.Role != PlayerRole.Runner)
        {
            return;
        }

        if (Time.time - lastCheckTime < checkInterval)
        {
            return;
        }

        lastCheckTime = Time.time;
        CheckNearbyCreatures();
    }

    private void CheckNearbyCreatures()
    {
        float myPerception = statsProvider.PerceptionMultiplier;

        var guideProvider = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);
        float guidePerception = guideProvider != null ? guideProvider.PerceptionMultiplier : 1f;

        float sharedRadius = Mathf.Min(baseSharedRadius * myPerception * guidePerception, maxSharedRadio);

        bool hasHeartbeatAbility = myPerception > 1f;

        var creatures = FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
        bool creatureNearby = false;

        foreach (var creature in creatures)
        {
            float distance = Vector3.Distance(transform.position, creature.transform.position);

            if (distance <= sharedRadius)
            {
                creatureNearby = true;
            }

            // TODO: cuando exista el mapa del Guía, publicar aquí qué criaturas
            // caen dentro de sharedRadius para que ese sistema las muestre.
        }

        if (!hasHeartbeatAbility || !creatureNearby)
        {
            return;
        }

        float t = Mathf.InverseLerp(1f, maxExpectedPerception, myPerception);
        float pulseInterval = Mathf.Lerp(slowestHeartbeatInterval, fastestHeartbeatInterval, t);

        if (Time.time - lastHeartbeatTime < pulseInterval)
        {
            return;
        }

        lastHeartbeatTime = Time.time;

        if (showDebugLogs)
        {
            Debug.Log($"[RunnerCreatureAwareness] 💓 Pulso de latido (intervalo {pulseInterval:F1}s)");
        }

        TargetHeartbeatPulse(connectionToClient);
    }

    [TargetRpc]
    private void TargetHeartbeatPulse(NetworkConnectionToClient target)
    {
        OnHeartbeatPulse?.Invoke();
    }
}