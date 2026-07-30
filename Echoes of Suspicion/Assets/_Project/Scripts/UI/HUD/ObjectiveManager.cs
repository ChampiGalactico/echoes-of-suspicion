using Mirror;
using UnityEngine;

/// <summary>
/// Server-authoritative objective manager.
///
/// Puzzle systems call the static helpers to update objectives per role.
/// The manager sends TargetRpcs to each player so their local
/// ObjectiveDisplay shows the correct text.
///
/// SETUP:
/// Place on a persistent GameObject in the scene with a NetworkIdentity.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class ObjectiveManager : NetworkBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ObjectiveManager] Duplicate instance destroyed.");
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Static helpers (call from server) ────────────────

    /// <summary>Set the Runner's objective text.</summary>
    public static void SetRunnerObjective(string text)
    {
        if (Instance == null) return;
        Instance.ServerSetObjective(PlayerRole.Runner, text);
    }

    /// <summary>Set the Guide's objective text.</summary>
    public static void SetGuideObjective(string text)
    {
        if (Instance == null) return;
        Instance.ServerSetObjective(PlayerRole.Guide, text);
    }

    /// <summary>Set objectives for both players at once.</summary>
    public static void SetObjectives(string runnerText, string guideText)
    {
        SetRunnerObjective(runnerText);
        SetGuideObjective(guideText);
    }

    // ── Server logic ─────────────────────────────────────

    [Server]
    private void ServerSetObjective(PlayerRole role, string text)
    {
        var player = PlayerUtils.FindPlayerByRole(role);
        if (player == null)
        {
            Debug.LogWarning($"[ObjectiveManager] Player with role {role} not found.");
            return;
        }

        TargetSetObjective(player.connectionToClient, role, text);
    }

    // ── Client ───────────────────────────────────────────

    [TargetRpc]
    private void TargetSetObjective(NetworkConnectionToClient target, PlayerRole role, string text)
    {
        var display = ObjectiveDisplay.GetDisplay(role);
        if (display != null)
            display.SetObjective(text);
        else
            Debug.LogWarning($"[ObjectiveManager] No ObjectiveDisplay found for {role}.");
    }
}
