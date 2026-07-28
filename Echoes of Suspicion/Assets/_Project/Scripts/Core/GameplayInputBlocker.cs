using System;
using UnityEngine;

/// <summary>
/// Estado global que indica si el gameplay debe ignorar el input local.
///
/// Se utiliza cuando una interfaz bloqueante está abierta:
/// menú de partida, documentos, puzzles, etc.
/// </summary>
public static class GameplayInputBlocker
{
    public static bool IsBlocked
    {
        get;
        private set;
    }

    public static event Action<bool> BlockStateChanged;

    public static void SetBlocked(bool blocked)
    {
        if (IsBlocked == blocked)
        {
            return;
        }

        IsBlocked = blocked;
        BlockStateChanged?.Invoke(blocked);
    }

    /// <summary>
    /// Evita que el estado estático permanezca activo
    /// al detener y volver a iniciar Play Mode.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetState()
    {
        IsBlocked = false;
        BlockStateChanged = null;
    }
}