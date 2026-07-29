using Mirror;
using UnityEngine;

/// <summary>
/// Permite a la criatura abrir puertas FreeInteraction en su camino.
///
/// Lanza un raycast corto hacia adelante. Si detecta una InteractableDoor
/// que es FreeInteraction y está cerrada, la abre automáticamente.
///
/// Solo se ejecuta en el servidor (igual que toda la lógica de IA).
///
/// SETUP:
///   1. Agregar este componente al mismo GameObject que CreatureController.
///   2. Asegurarse de que las puertas tienen un Collider (no trigger) en un
///      layer que el raycast pueda detectar.
/// </summary>
[RequireComponent(typeof(CreatureController))]
public sealed class CreatureDoorInteraction : NetworkBehaviour
{
    [Header("Detection")]
    [SerializeField, Tooltip("Distancia del raycast para detectar puertas.")]
    private float detectionRange = 2.5f;

    [SerializeField, Tooltip("Layers donde están las puertas.")]
    private LayerMask doorLayerMask = ~0; // Default: todas las layers

    [Header("Timing")]
    [SerializeField, Tooltip("Segundos entre cada chequeo de puerta (para no hacer raycast cada frame).")]
    private float checkInterval = 0.25f;

    private CreatureController creature;
    private float nextCheckTime;
    private bool waitingForDoor;
    private InteractableDoor currentDoor;

    private void Awake()
    {
        creature = GetComponent<CreatureController>();
    }

    private void Update()
    {
        if (!isServer) return;

        // Esperando a que la puerta termine de abrirse.
        if (waitingForDoor)
            return;

        if (Time.time < nextCheckTime) return;
        nextCheckTime = Time.time + checkInterval;

        CheckForDoor();
    }

    private void CheckForDoor()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f; // Un poco arriba del suelo
        Vector3 direction = transform.forward;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, detectionRange, doorLayerMask))
            return;

        // Buscar InteractableDoor en el objeto impactado o en sus padres.
        InteractableDoor door = hit.collider.GetComponentInParent<InteractableDoor>();
        if (door == null) return;

        // Solo abrir puertas FreeInteraction que estén cerradas.
        if (door.Mode != InteractableDoor.DoorMode.FreeInteraction) return;
        if (door.IsOpen) return;
        if (door.IsAnimating) return;

        // Suscribirse al evento para saber cuándo terminó de abrirse.
        currentDoor = door;
        door.OnDoorOpened += HandleDoorOpened;

        // Abrir la puerta desde la posición de la criatura.
        door.OpenDoor(transform.position);

        // Pausar hasta que la animación termine.
        creature.Agent.isStopped = true;
        waitingForDoor = true;

        Debug.Log($"[CreatureDoorInteraction] Abriendo puerta: {door.name}");
    }

    private void HandleDoorOpened()
    {
        if (currentDoor != null)
            currentDoor.OnDoorOpened -= HandleDoorOpened;

        currentDoor = null;
        creature.Agent.isStopped = false;
        waitingForDoor = false;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizar el rayo de detección.
        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawRay(origin, transform.forward * detectionRange);
    }
}
