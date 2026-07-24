using Mirror;
using TMPro;
using UnityEngine;

public sealed class NetworkMenuController : MonoBehaviour
{
    [Header("Red")]
    [SerializeField]
    private EOSNetworkManager networkManager;

    [Header("Interfaz para unirse")]
    [SerializeField]
    private TMP_InputField addressInputField;

    public void HostGame()
    {
        if (!ValidateNetworkManager() || NetworkSessionIsActive())
        {
            return;
        }

        Debug.Log(
            "[NetworkMenuController] Iniciando partida como host."
        );

        networkManager.StartHost();
    }

    public void JoinGame()
    {
        if (!ValidateNetworkManager() || NetworkSessionIsActive())
        {
            return;
        }

        if (addressInputField == null)
        {
            Debug.LogError(
                "NetworkMenuController: falta asignar AddressInputField.",
                this
            );

            return;
        }

        string address = addressInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(address))
        {
            Debug.LogWarning(
                "NetworkMenuController: escribe la IP o dirección del host.",
                this
            );

            addressInputField.ActivateInputField();
            return;
        }

        networkManager.networkAddress = address;

        Debug.Log(
            $"[NetworkMenuController] Intentando conectar con: {address}"
        );

        networkManager.StartClient();
    }

    private bool ValidateNetworkManager()
    {
        if (networkManager != null)
        {
            return true;
        }

        Debug.LogError(
            "NetworkMenuController: falta asignar el EOSNetworkManager.",
            this
        );

        return false;
    }

    private static bool NetworkSessionIsActive()
    {
        if (!NetworkServer.active && !NetworkClient.active)
        {
            return false;
        }

        Debug.LogWarning(
            "NetworkMenuController: ya existe una sesión de red activa o en proceso."
        );

        return true;
    }
}