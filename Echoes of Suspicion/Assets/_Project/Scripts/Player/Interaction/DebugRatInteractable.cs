using Mirror;
using UnityEngine;

public sealed class DebugRatInteractable : RatInteractable
{
    [SerializeField]
    private float rotationDegrees = 45f;

    [SyncVar(hook = nameof(OnRotationStepChanged))]
    private int rotationStep;

    private Quaternion initialLocalRotation;

    private void Awake()
    {
        initialLocalRotation = transform.localRotation;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // También aplica correctamente el estado para clientes
        // que entren después de varias interacciones.
        ApplyRotation(rotationStep);
    }

    [Server]
    public override void ServerInteract(NetworkIdentity interactor)
    {
        rotationStep++;

        // El servidor y el Host comparten esta escena.
        ApplyRotation(rotationStep);

        Debug.Log(
            $"{interactor.name} interactuó con {name}. " +
            $"Paso de rotación: {rotationStep}.",
            this);
    }

    private void OnRotationStepChanged(
        int previousStep,
        int newStep)
    {
        ApplyRotation(newStep);
    }

    private void ApplyRotation(int step)
    {
        transform.localRotation =
            initialLocalRotation *
            Quaternion.Euler(
                0f,
                step * rotationDegrees,
                0f);
    }
}