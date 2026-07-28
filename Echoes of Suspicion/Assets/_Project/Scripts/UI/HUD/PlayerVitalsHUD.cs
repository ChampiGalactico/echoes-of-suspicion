using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta las barras de vida y estamina del HUD con el jugador local.
///
/// El HUD existe en la escena, mientras que el jugador aparece después
/// de iniciar la sesión de red. Por eso este componente espera hasta que
/// NetworkClient.localPlayer esté disponible.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerVitalsHUD : MonoBehaviour
{
    [Header("Barras")]

    [SerializeField]
    private Image healthFill;

    [SerializeField]
    private Image staminaFill;

    [Header("Respuesta visual")]

    [SerializeField, Min(0f)]
    private float fillResponseSpeed = 10f;

    private NetworkIdentity boundPlayer;

    private PlayerHealth playerHealth;
    private PlayerStamina playerStamina;

    private float targetHealthFill = 1f;
    private float targetStaminaFill = 1f;

    private void OnEnable()
    {
        TryBindLocalPlayer();
    }

    private void Update()
    {
        /*
         * El jugador local puede no existir cuando se activa el HUD.
         * También puede cambiar al salir y volver a entrar a una partida.
         */
        if (
            boundPlayer == null ||
            NetworkClient.localPlayer != boundPlayer
        )
        {
            TryBindLocalPlayer();
        }

        UpdateBarVisuals();
    }

    private void OnDisable()
    {
        UnbindCurrentPlayer();
    }

    private void TryBindLocalPlayer()
    {
        NetworkIdentity localPlayer =
            NetworkClient.localPlayer;

        if (
            localPlayer == null ||
            localPlayer == boundPlayer
        )
        {
            return;
        }

        UnbindCurrentPlayer();

        boundPlayer = localPlayer;

        playerHealth =
            boundPlayer.GetComponent<PlayerHealth>();

        playerStamina =
            boundPlayer.GetComponent<PlayerStamina>();

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged +=
                HandleHealthChanged;

            HandleHealthChanged(
                playerHealth.CurrentHealth,
                playerHealth.MaxHealth
            );
        }
        else
        {
            Debug.LogWarning(
                "PlayerVitalsHUD: el jugador local no tiene PlayerHealth.",
                this
            );

            targetHealthFill = 1f;
        }

        if (playerStamina != null)
        {
            playerStamina.OnStaminaChanged +=
                HandleStaminaChanged;

            HandleStaminaChanged(
                playerStamina.CurrentStamina,
                playerStamina.MaxStamina
            );
        }
        else
        {
            Debug.LogWarning(
                "PlayerVitalsHUD: el jugador local no tiene PlayerStamina.",
                this
            );

            targetStaminaFill = 1f;
        }
    }

    private void UnbindCurrentPlayer()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -=
                HandleHealthChanged;
        }

        if (playerStamina != null)
        {
            playerStamina.OnStaminaChanged -=
                HandleStaminaChanged;
        }

        boundPlayer = null;
        playerHealth = null;
        playerStamina = null;
    }

    private void HandleHealthChanged(
        float currentHealth,
        float maximumHealth
    )
    {
        targetHealthFill = CalculateNormalizedValue(
            currentHealth,
            maximumHealth
        );
    }

    private void HandleStaminaChanged(
        float currentStamina,
        float maximumStamina
    )
    {
        targetStaminaFill = CalculateNormalizedValue(
            currentStamina,
            maximumStamina
        );
    }

    private void UpdateBarVisuals()
    {
        float step =
            fillResponseSpeed *
            Time.unscaledDeltaTime;

        if (healthFill != null)
        {
            healthFill.fillAmount =
                Mathf.MoveTowards(
                    healthFill.fillAmount,
                    targetHealthFill,
                    step
                );
        }

        if (staminaFill != null)
        {
            staminaFill.fillAmount =
                Mathf.MoveTowards(
                    staminaFill.fillAmount,
                    targetStaminaFill,
                    step
                );
        }
    }

    private static float CalculateNormalizedValue(
        float currentValue,
        float maximumValue
    )
    {
        if (maximumValue <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            currentValue / maximumValue
        );
    }
}