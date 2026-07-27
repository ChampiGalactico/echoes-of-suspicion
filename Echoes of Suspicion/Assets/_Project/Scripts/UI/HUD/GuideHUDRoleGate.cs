using Mirror;
using UnityEngine;

[RequireComponent(typeof(Canvas))]
public sealed class GuideHUDRoleGate : MonoBehaviour
{
    private Canvas hudCanvas;
    private bool roleResolved;

    private void Awake()
    {
        hudCanvas = GetComponent<Canvas>();

        // Evita que el HUD aparezca antes de conocer el rol local.
        hudCanvas.enabled = false;
    }

    private void Update()
    {
        if (roleResolved)
        {
            return;
        }

        NetworkIdentity localPlayerIdentity =
            NetworkClient.localPlayer;

        if (localPlayerIdentity == null)
        {
            return;
        }

        CharacterStatsProvider statsProvider =
            localPlayerIdentity.GetComponent<CharacterStatsProvider>();

        if (statsProvider == null)
        {
            Debug.LogError(
                "[GuideHUDRoleGate] El jugador local no tiene " +
                "CharacterStatsProvider.",
                this
            );

            enabled = false;
            return;
        }

        // Espera hasta que Mirror haya sincronizado el personaje.
        if (statsProvider.Character == null)
        {
            return;
        }

        roleResolved = true;

        if (statsProvider.Role == PlayerRole.Guide)
        {
            hudCanvas.enabled = true;

            Debug.Log(
                "[GuideHUDRoleGate] HUD del Guía activado.",
                this
            );

            return;
        }

        Debug.Log(
            "[GuideHUDRoleGate] El jugador local es Runner. " +
            "El HUD del Guía permanecerá oculto.",
            this
        );

        gameObject.SetActive(false);
    }
}