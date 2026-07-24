using Mirror;
using UnityEngine;

/// <summary>
/// Clase base para objetos interactuables sincronizados.
/// </summary>
public abstract class RatInteractable : NetworkBehaviour
{
    [Header("Interaction")]
    [SerializeField]
    private string interactionPrompt = "Interactuar";

    public string InteractionPrompt => interactionPrompt;

    /// <summary>
    /// Comprobación local para decidir si el objeto
    /// puede mostrarse como seleccionable.
    /// </summary>
    public virtual bool CanPreviewInteraction(GameObject interactor)
    {
        return interactor != null;
    }

    /// <summary>
    /// Validación adicional ejecutada en el servidor.
    /// </summary>
    [Server]
    public virtual bool CanServerInteract(NetworkIdentity interactor)
    {
        return interactor != null;
    }

    /// <summary>
    /// Cada interactuable concreto debe implementar
    /// su comportamiento autoritativo.
    /// </summary>
    public abstract void ServerInteract(NetworkIdentity interactor);
}