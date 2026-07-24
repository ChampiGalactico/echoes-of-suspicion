using UnityEngine;


/// <summary>
/// Representa un evento de ruido que ocurrió en el mundo.
/// Contiene toda la información que necesita cualquier suscriptor (criatura, HUD, mapa).
/// Es una struct porque son datos simples y de corta vida.
/// </summary>
public readonly struct NoiseEvent
{
    /// <summary>
    /// Posición en el mundo donde se generó el ruido.
    /// </summary>
    public readonly Vector3 worldPosition;

    /// <summary>
    /// Intensidad del ruido, normalizada entre 0 (silencio) y 1 (máximo volumen).
    /// La criatura y el HUD deciden qué hacer según esta intensidad.
    /// </summary>
    public readonly float intensity;

    /// <summary>
    /// Fuente del ruido (voz, movimiento, envío de objetos, etc.).
    /// Útil para que los suscriptores reaccionen distinto según el origen.
    /// </summary>
    public readonly NoiseSource source;

    /// <summary>
    /// ID de red del jugador que generó el ruido.
    /// Sirve para que la criatura sepa a quién perseguir, o para que el HUD del Corredor
    /// muestre solo su propio ruido.
    /// </summary>
    public readonly uint sourcePlayerNetId;

    public NoiseEvent(Vector3 worldPosition, float intensity, NoiseSource source, uint sourcePlayerNetId)
    {
        this.worldPosition = worldPosition;
        this.intensity = intensity;
        this.source = source;
        this.sourcePlayerNetId = sourcePlayerNetId;
    }
}

/// <summary>
/// Tipos de fuentes de ruido en el juego.
/// Sirve para que los suscriptores reaccionen distinto según el origen.
/// </summary>
public enum NoiseSource
{
    Voice,          // Voz del jugador captada por micrófono.
    Footsteps,      // Pasos al caminar/correr.
    Jump,           // Aterrizaje al saltar.
    ObjectSend,     // Sonido del punto de envío al transmitir.
    ObjectImpact    // Cuando un objeto lanzado impacta algo.
}
