using Mirror;
using UnityEngine;

/// <summary>
/// Expone los stats del personaje elegido al resto de sistemas del Player
/// (PlayerThrowController, movimiento, percepción) sin que ellos conozcan
/// CharacterData directamente.
///
/// El personaje y el rol se asignan desde afuera (EOSNetworkManager.OnServerAddPlayer)
/// y se sincronizan por red mediante SyncVar, para que tanto el dueño como
/// los demás clientes vean los mismos stats aplicados.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public sealed class CharacterStatsProvider : NetworkBehaviour
{
    [Header("Available Characters")]
    [SerializeField, Tooltip("Índice 0 = personaje mujer, índice 1 = personaje hombre.")]
    private CharacterData[] availableCharacters;

    [SyncVar(hook = nameof(OnCharacterIndexChanged))]
    private int selectedCharacterIndex = -1;

    [SyncVar]
    private PlayerRole currentRole;

    private CharacterData currentCharacter;

    public float StrengthMultiplier => currentCharacter != null ? currentCharacter.strengthMultiplier : 1f;
    public float StaminaMultiplier => currentCharacter != null ? currentCharacter.staminaMultiplier : 1f;
    public float AgilityMultiplier => currentCharacter != null ? currentCharacter.agilityMultiplier : 1f;
    public float PerceptionMultiplier => currentCharacter != null ? currentCharacter.perceptionMultiplier : 1f;
    public CharacterData Character => currentCharacter;
    public PlayerRole Role => currentRole;

    public override void OnStartServer()
    {
        base.OnStartServer();
        // El personaje y el rol los asigna EOSNetworkManager.OnServerAddPlayer
        // justo después de este punto. Nada que hacer aquí todavía.
    }

    /// <summary>
    /// Asigna el rol desde afuera (llamado por EOSNetworkManager al conectar).
    /// Solo tiene efecto en el servidor.
    /// </summary>
    public void SetHardcodedRole(PlayerRole role)
    {
        if (!isServer)
        {
            return;
        }

        currentRole = role;
    }

    /// <summary>
    /// Asigna el índice de personaje desde afuera (llamado por EOSNetworkManager
    /// al conectar). Solo tiene efecto en el servidor.
    /// </summary>
    public void SetHardcodedCharacter(int index)
    {
        if (!isServer)
        {
            return;
        }

        selectedCharacterIndex = index;
    }

    private void OnCharacterIndexChanged(int oldIndex, int newIndex)
    {
        if (availableCharacters == null || newIndex < 0 || newIndex >= availableCharacters.Length)
        {
            Debug.LogWarning($"[CharacterStatsProvider] Índice de personaje inválido: {newIndex}");
            return;
        }

        currentCharacter = availableCharacters[newIndex];
        Debug.Log($"[CharacterStatsProvider] Personaje asignado: {currentCharacter.characterName} " +
                  $"(Str {StrengthMultiplier}, Sta {StaminaMultiplier}, Agi {AgilityMultiplier}, Per {PerceptionMultiplier})");
    }
}

/// <summary>
/// Rol actual del jugador en la partida. Determina cómo se expresan los
/// stats de CharacterData (ver tabla de diseño) y qué sistemas se activan.
/// </summary>
public enum PlayerRole
{
    Runner,
    Guide
}