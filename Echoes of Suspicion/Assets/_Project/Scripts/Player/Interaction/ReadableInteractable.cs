using Mirror;
using UnityEngine;

/// <summary>
/// Objeto legible en el mundo: una hoja, nota adhesiva, carta, etc.
/// Cuando el Runner pulsa E, el servidor valida la distancia y envía
/// un TargetRpc al cliente para que abra la UI de lectura.
///
/// Asignar UNO de los dos campos: _documentData o _stickyData.
/// El que esté asignado determina qué panel abre ReadableUI.
///
/// Requiere un Collider (no trigger) en el mismo GameObject o hijos
/// para que el spherecast de NetworkRatInteractor lo detecte.
/// </summary>
public class ReadableInteractable : RatInteractable
{
    [Header("Contenido (asignar UNO de los dos)")]
    [SerializeField] private DocumentData _documentData;
    [SerializeField] private StickyNoteData _stickyData;

    private bool IsDocument => _documentData != null;
    private bool HasData => _documentData != null || _stickyData != null;

    private void Awake()
    {
        // Copiar el prompt del asset al campo base.
        if (IsDocument && !string.IsNullOrEmpty(_documentData.InteractionPrompt))
            interactionPrompt = _documentData.InteractionPrompt;
        else if (_stickyData != null && !string.IsNullOrEmpty(_stickyData.InteractionPrompt))
            interactionPrompt = _stickyData.InteractionPrompt;
    }

    public override bool CanPreviewInteraction(GameObject interactor)
    {
        if (!HasData) return false;

        if (ReadableUI.Instance != null && ReadableUI.Instance.IsOpen)
            return false;

        return base.CanPreviewInteraction(interactor);
    }

    [Server]
    public override bool CanServerInteract(NetworkIdentity interactor)
    {
        if (!HasData) return false;
        return base.CanServerInteract(interactor);
    }

    [Server]
    public override void ServerInteract(NetworkIdentity interactor)
    {
        NetworkConnectionToClient conn = interactor.connectionToClient;
        if (conn != null)
            TargetShowReadable(conn);
    }

    [TargetRpc]
    private void TargetShowReadable(NetworkConnectionToClient target)
    {
        if (ReadableUI.Instance == null)
        {
            Debug.LogWarning("[ReadableInteractable] No hay ReadableUI en la escena.");
            return;
        }

        if (IsDocument)
            ReadableUI.Instance.ShowDocument(_documentData);
        else
            ReadableUI.Instance.ShowStickyNote(_stickyData);
    }
}
