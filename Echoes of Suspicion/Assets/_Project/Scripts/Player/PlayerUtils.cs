using Mirror;
using UnityEngine;

/// <summary>
/// Utilidades compartidas entre estados de criatura para buscar
/// referencias a jugadores conectados en el servidor.
/// </summary>
public static class PlayerUtils
{
    /// <summary>
    /// Busca el Transform del jugador con el netId dado entre las conexiones activas del servidor.
    /// Retorna null si no se encuentra (jugador desconectado, netId inválido, etc.).
    /// </summary>
    public static Transform FindPlayerTransformByNetId(uint netId)
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity != null && conn.identity.netId == netId)
            {
                return conn.identity.transform;
            }
        }

        return null;
    }

    public static CharacterStatsProvider FindPlayerByRole(PlayerRole role)
    {
        foreach (var conn in Mirror.NetworkServer.connections.Values)
        {
            if (conn.identity == null)
            {
                continue;
            }

            var provider = conn.identity.GetComponent<CharacterStatsProvider>();
            if (provider != null && provider.Role == role)
            {
                return provider;
            }
        }

        return null;
    }
}