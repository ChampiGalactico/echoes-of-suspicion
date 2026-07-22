using Mirror;
using UnityEngine;

/// <summary>
/// Percepción por sonido de la criatura.
/// Se suscribe al NoiseEventBus y reacciona a los ruidos que caen dentro
/// de su rango de audición y superan la intensidad mínima.
///
/// Vive en el servidor. Los clientes no ejecutan esta lógica.
/// </summary>
[RequireComponent(typeof(CreatureController))]
public sealed class CreatureNoisePerception : NetworkBehaviour
{
    [Header("Debug")]
    [SerializeField, Tooltip("Muestra en consola los eventos de ruido recibidos.")]
    private bool showDebugLogs = true;

    private CreatureController creature;

    private void Awake()
    {
        creature = GetComponent<CreatureController>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NoiseEventBus.OnNoiseGenerated += HandleNoise;
        Debug.Log("[CreatureNoisePerception] Suscrita al NoiseEventBus.");
    }

    public override void OnStopServer()
    {
        NoiseEventBus.OnNoiseGenerated -= HandleNoise;
        base.OnStopServer();
    }

    /// <summary>
    /// Se llama cada vez que alguien publica un evento de ruido.
    /// Solo actuamos si es lo suficientemente cerca e intenso.
    /// </summary>
    private void HandleNoise(NoiseEvent noiseEvent)
    {
        // Verificar intensidad mínima según los datos de la criatura.
        if (noiseEvent.intensity < creature.Data.minNoiseIntensity)
        {
            return;
        }

        // Verificar distancia.
        float distance = Vector3.Distance(transform.position, noiseEvent.worldPosition);

        if (distance > creature.Data.hearingRadius)
        {
            return;
        }

        // Ruido válido detectado.
        if (showDebugLogs)
        {
            Debug.Log($"[CreatureNoisePerception] 👂 Ruido detectado: " +
                      $"intensidad {noiseEvent.intensity:F2} " +
                      $"a {distance:F1}m de distancia. " +
                      $"Cambio a AlertState.");
        }

        // Cambiar al estado de alerta con la posición del ruido.
        creature.ChangeState(new AlertState(creature, noiseEvent.worldPosition));
    }
}