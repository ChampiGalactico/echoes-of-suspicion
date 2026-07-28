using Mirror;
using UnityEngine;

/// <summary>
/// Objeto legible en el mundo: una hoja, nota adhesiva, carta, etc.
/// Cuando el Runner pulsa E, el servidor valida la distancia y envía
/// un TargetRpc al cliente para que abra la UI de lectura.
///
/// Requiere un Collider (no trigger) en el mismo GameObject o hijos
/// para que el spherecast de NetworkRatInteractor lo detecte.
/// </summary>
public class ReadableInteractable : RatInteractable
{
    [Header("Contenido")]
    [SerializeField] private ReadableData _readableData;

    private void Awake()
    {
        // Copiar el prompt del asset al campo base para que el HUD
        // lo lea correctamente (InteractionPrompt no es virtual).
        if (_readableData != null && !string.IsNullOrEmpty(_readableData.InteractionPrompt))
            interactionPrompt = _readableData.InteractionPrompt;
    }

    public override bool CanPreviewInteraction(GameObject interactor)
    {
        if (_readableData == null) return false;

        // No mostrar prompt si la UI de lectura ya está abierta.
        if (ReadableUI.Instance != null && ReadableUI.Instance.IsOpen)
            return false;

        return base.CanPreviewInteraction(interactor);
    }

    [Server]
    public override bool CanServerInteract(NetworkIdentity interactor)
    {
        if (_readableData == null) return false;
        return base.CanServerInteract(interactor);
    }

    [Server]
    public override void ServerInteract(NetworkIdentity interactor)
    {
        // Solo enviar al cliente que interactuó.
        NetworkConnectionToClient conn = interactor.connectionToClient;
        if (conn != null)
            TargetShowReadable(conn);
    }

    /// <summary>
    /// Se ejecuta SOLO en el cliente del jugador que interactuó.
    /// Abre la UI de lectura con los datos locales del ScriptableObject
    /// (que es idéntico en todas las máquinas).
    /// </summary>
    [TargetRpc]
    private void TargetShowReadable(NetworkConnectionToClient target)
    {
        if (ReadableUI.Instance == null)
        {
            Debug.LogWarning("[ReadableInteractable] No hay ReadableUI en la escena.");
            return;
        }

        ReadableUI.Instance.Show(_readableData);
    }
}
