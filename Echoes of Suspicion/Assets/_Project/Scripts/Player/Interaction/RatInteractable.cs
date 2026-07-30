using Mirror;
using UnityEngine;

/// <summary>
/// Clase base para objetos interactuables sincronizados.
/// </summary>
public abstract class RatInteractable : NetworkBehaviour
{
    [Header("Interaction")]
    [SerializeField]
    protected string interactionPrompt = "Interactuar";

    public string InteractionPrompt => interactionPrompt;

    /// <summary>
    /// Devuelve el prompt contextual para este interactor.
    /// Subclases pueden sobreescribirlo (ej: puerta muestra "Necesitas la llave"
    /// si el jugador no tiene el item requerido).
    /// </summary>
    public virtual string GetInteractionPrompt(GameObject interactor)
    {
        return interactionPrompt;
    }

    /// <summary>
    /// Indica si el interactor puede realmente activar la interacción.
    /// Si devuelve false, el HUD muestra el prompt sin "[E]".
    /// </summary>
    public virtual bool IsInteractableBy(GameObject interactor)
    {
        return true;
    }

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