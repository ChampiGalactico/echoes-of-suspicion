using System;
using UnityEngine;

/// <summary>
/// Bus centralizado de eventos de ruido.
/// Cualquier sistema puede publicar (Publish) o suscribirse (OnNoiseGenerated).
///
/// Es una clase estática porque no tiene estado propio —
/// solo actúa como intermediario entre productores y consumidores.
///
/// USO:
///
///     // Productor (ej. micrófono):
///     NoiseEventBus.Publish(new NoiseEvent(...));
///
///     // Consumidor (ej. criatura):
///     private void OnEnable()  { NoiseEventBus.OnNoiseGenerated += HandleNoise; }
///     private void OnDisable() { NoiseEventBus.OnNoiseGenerated -= HandleNoise; }
///     private void HandleNoise(NoiseEvent evt) { ... }
/// </summary>
public static class NoiseEventBus
{
    /// <summary>
    /// Se dispara cada vez que alguien publica un evento de ruido.
    /// Los suscriptores reciben el NoiseEvent completo.
    /// </summary>
    public static event Action<NoiseEvent> OnNoiseGenerated;

    /// <summary>
    /// Publica un evento de ruido a todos los suscriptores.
    /// Solo lo llaman los "productores" (micrófono, sistema de pasos, punto de envío, etc.).
    /// </summary>
    public static void Publish(NoiseEvent noiseEvent)
    {
        // El "?" protege de NullReferenceException si no hay suscriptores.
        OnNoiseGenerated?.Invoke(noiseEvent);

        Debug.Log($"[NoiseEventBus] 🔊 Ruido publicado: {noiseEvent.source} " +
                  $"intensidad {noiseEvent.intensity:F2} " +
                  $"en {noiseEvent.worldPosition}");
    }

    /// <summary>
    /// Limpia todos los suscriptores.
    /// Útil al cambiar de escena para evitar referencias a objetos destruidos.
    /// Llamar desde el GameManager cuando cambia de gameplay a menú, por ejemplo.
    /// </summary>
    public static void ClearAllSubscribers()
    {
        OnNoiseGenerated = null;
    }
}
