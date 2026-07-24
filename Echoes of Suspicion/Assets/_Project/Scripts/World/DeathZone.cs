using Mirror;
using UnityEngine;

/// <summary>
/// Zona de muerte instantánea para el Corredor: pozos, precipicios, lugares
/// prohibidos del bioma, etc. Colócala en la escena con un Collider en modo
/// Trigger cubriendo la zona peligrosa.
///
/// No requiere configuración — solo arrastrar el prefab/objeto a la escena
/// y ajustar el tamaño del Collider al área deseada.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class DeathZone : NetworkBehaviour
{
    private void Reset()
    {
        // Recordatorio visual al agregar el componente: el Collider debe ser Trigger.
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServer)
        {
            return;
        }

        var health = other.GetComponent<PlayerHealth>();
        health?.Kill();
    }
}