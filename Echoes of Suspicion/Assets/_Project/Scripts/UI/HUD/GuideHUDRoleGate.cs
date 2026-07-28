using Mirror;
using UnityEngine;

/// <summary>
/// Activa únicamente el HUD correspondiente al rol del jugador local.
///
/// El Canvas principal permanece activo para que los elementos compartidos,
/// como InGameMenu, sigan disponibles para Guide y Runner.
/// </summary>
[RequireComponent(typeof(Canvas))]
public sealed class GuideHUDRoleGate : MonoBehaviour
{
    [Header("Paneles por rol")]

    [SerializeField]
    [Tooltip("Panel exclusivo del Guía/Carlos.")]
    private GameObject guideHUD;

    [SerializeField]
    [Tooltip("Panel exclusivo del Corredor/Carmen.")]
    private GameObject runnerHUD;

    private Canvas hudCanvas;

    private NetworkIdentity resolvedLocalPlayer;
    private bool roleResolved;

    private void Awake()
    {
        hudCanvas = GetComponent<Canvas>();

        /*
         * El Canvas debe permanecer activo porque contiene elementos
         * compartidos, como el menú de pausa.
         */
        hudCanvas.enabled = true;

        HideRoleHUDs();
    }

    private void OnEnable()
    {
        roleResolved = false;
        resolvedLocalPlayer = null;

        HideRoleHUDs();
    }

    private void Update()
    {
        NetworkIdentity localPlayerIdentity =
            NetworkClient.localPlayer;

        if (localPlayerIdentity == null)
        {
            return;
        }

        /*
         * Evita resolver el mismo jugador repetidamente,
         * pero permite volver a resolver si cambia la sesión.
         */
        if (
            roleResolved &&
            resolvedLocalPlayer == localPlayerIdentity
        )
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
                localPlayerIdentity
            );

            return;
        }

        /*
         * Espera hasta que Mirror sincronice el personaje
         * y sus estadísticas.
         */
        if (statsProvider.Character == null)
        {
            return;
        }

        resolvedLocalPlayer = localPlayerIdentity;
        roleResolved = true;

        bool isGuide =
            statsProvider.Role == PlayerRole.Guide;

        SetRoleHUDs(isGuide);

        Debug.Log(
            isGuide
                ? "[GuideHUDRoleGate] HUD de Carlos activado."
                : "[GuideHUDRoleGate] HUD de Carmen activado.",
            this
        );
    }

    private void SetRoleHUDs(bool isGuide)
    {
        if (guideHUD != null)
        {
            guideHUD.SetActive(isGuide);
        }
        else
        {
            Debug.LogWarning(
                "[GuideHUDRoleGate] Guide HUD no está asignado.",
                this
            );
        }

        if (runnerHUD != null)
        {
            runnerHUD.SetActive(!isGuide);
        }
        else if (!isGuide)
        {
            Debug.LogWarning(
                "[GuideHUDRoleGate] Runner HUD todavía no está asignado.",
                this
            );
        }
    }

    private void HideRoleHUDs()
    {
        if (guideHUD != null)
        {
            guideHUD.SetActive(false);
        }

        if (runnerHUD != null)
        {
            runnerHUD.SetActive(false);
        }
    }
}