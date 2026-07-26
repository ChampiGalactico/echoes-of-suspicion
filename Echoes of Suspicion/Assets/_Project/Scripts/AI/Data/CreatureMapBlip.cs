using UnityEngine;

/// <summary>
/// Snapshot de una criatura detectada dentro del radio de percepción
/// compartido del Corredor (ver RunnerCreatureAwareness), enviado al Guía
/// para dibujar su mapa esquemático (Propuesta_Tecnica, sección 7).
///
/// Mirror serializa este struct automáticamente porque todos sus campos son
/// tipos simples soportados (uint, Vector3, enum) — no hace falta un Writer
/// custom para usarlo como parámetro de TargetRpc.
///
/// Solo lleva el netId, la posición y el estado: el resto de la apariencia
/// (ícono, color) se resuelve del lado del cliente vía NetworkClient.spawned,
/// leyendo el CreatureData real de la criatura — así no se duplica esa
/// información en cada actualización de red.
/// </summary>
public struct CreatureMapBlip
{
    public uint creatureNetId;
    public Vector3 worldPosition;
    public CreatureStateType state;

    public CreatureMapBlip(uint creatureNetId, Vector3 worldPosition, CreatureStateType state)
    {
        this.creatureNetId = creatureNetId;
        this.worldPosition = worldPosition;
        this.state = state;
    }
}
