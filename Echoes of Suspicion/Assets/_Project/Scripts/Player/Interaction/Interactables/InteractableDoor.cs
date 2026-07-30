using System.Collections;
using EOS.Puzzles;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Puerta interactiva reutilizable para Echoes of Suspicion.
/// Hereda de RatInteractable → el sistema de raycast la detecta automáticamente.
/// Sincronizada por red con SyncVar.
///
/// SETUP MÍNIMO:
/// 1. GameObject con pivot en la bisagra (InitialDoor en tu caso).
/// 2. Modelo de la puerta como hijo.
/// 3. BoxCollider en el padre (ajustado al tamaño de la puerta).
/// 4. Este script en el padre.
/// 5. NetworkIdentity en el padre (requerido por RatInteractable/NetworkBehaviour).
/// 6. El objeto debe estar en el Layer que detecta el interactionMask del NetworkRatInteractor.
/// 7. Elegir DoorMode en el Inspector.
/// </summary>
public class InteractableDoor : RatInteractable
{
    // ===== MODO DE LA PUERTA =====

    public enum DoorMode
    {
        FreeInteraction,  // Presionar E para abrir/cerrar
        RequiresItem,     // Necesita un item en el inventario
        PuzzleLinked,     // Se abre automáticamente al resolver un Puzzle
        ExternalOnly      // Solo se controla por código (OpenDoor/CloseDoor)
    }

    [Header("Modo de apertura")]
    [SerializeField] private DoorMode doorMode = DoorMode.FreeInteraction;

    [Header("Configuración de animación")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float animationDuration = 0.8f;
    [SerializeField] private AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Opciones generales")]
    [Tooltip("La puerta se abre hacia el lado opuesto del jugador.")]
    [SerializeField] private bool openAwayFromPlayer = true;

    [Tooltip("Permite cerrar la puerta interactuando de nuevo.")]
    [SerializeField] private bool canClose = true;

    [Tooltip("Si consume el item al abrir (solo RequiresItem).")]
    [SerializeField] private bool consumeItemOnUse = false;

    // --- RequiresItem ---
    [Header("Requiere Item (solo si DoorMode = RequiresItem)")]
    [Tooltip("El PuzzleItemData que el jugador debe tener. Filtra por ItemId y opcionalmente por ItemTag.")]
    [SerializeField] private PuzzleItemData requiredItem;

    [Tooltip("Si está activo, además del ItemId se valida que el ItemTag coincida.")]
    [SerializeField] private bool filterByTag = true;

    // --- PuzzleLinked ---
    [Header("Vinculada a Puzzle (solo si DoorMode = PuzzleLinked)")]
    [Tooltip("Arrastra aquí el GameObject que tiene el script Puzzle.")]
    [SerializeField] private MonoBehaviour linkedPuzzle; // Cambiar a Puzzle cuando exista

    [Header("Audio (opcional)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioClip lockedSound;

    [SerializeField, Min(1f)]
    [Tooltip("Distancia máxima a la que se escucha el sonido de la puerta.")]
    private float audioMaxDistance = 15f;

    [Header("Feedback visual (puerta bloqueada)")]
    [SerializeField]
    [Tooltip("Mensaje que se muestra cuando el jugador no tiene el item requerido.")]
    private string deniedMessage = "Necesitas una llave";

    // ===== ESTADO SINCRONIZADO =====

    [SyncVar(hook = nameof(OnSyncStateChanged))]
    private DoorSyncState syncState;

    private struct DoorSyncState
    {
        public bool isOpen;
        public float openDirection; // 1 o -1
    }

    // ===== ESTADO LOCAL =====
    private bool isAnimating = false;
    private bool hasInitialized = false;
    private Quaternion closedRotation;
    private AudioSource audioSource;
    private NavMeshObstacle navObstacle;

    /// <summary>
    /// Evento estático para que el HUD muestre mensajes de feedback.
    /// Parámetro: mensaje a mostrar.
    /// Solo se dispara en el cliente local que intentó interactuar.
    /// </summary>
    public static event System.Action<string> OnLocalDeniedFeedback;

    // ===== PROPIEDADES =====
    public bool IsOpen => syncState.isOpen;
    public bool IsAnimating => isAnimating;
    public DoorMode Mode => doorMode;

    // ===== EVENTOS =====
    public event System.Action OnDoorOpened;
    public event System.Action OnDoorClosed;
    public event System.Action OnDoorDenied;

    // =========================================================================
    //  UNITY LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        closedRotation = transform.localRotation;
        SetupAudio();
        SetupNavMeshObstacle();
    }

    private void Start()
    {
        // --- PuzzleLinked: suscribirse al evento OnSolved ---
        // DESCOMENTA cuando la clase Puzzle exista:
        //
        // if (doorMode == DoorMode.PuzzleLinked && linkedPuzzle != null)
        // {
        //     Puzzle puzzle = linkedPuzzle as Puzzle;
        //     if (puzzle != null)
        //     {
        //         puzzle.OnSolved += HandlePuzzleSolved;
        //         if (puzzle.IsSolved && isServer) ServerSetOpen(1f);
        //     }
        // }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // Snap silencioso al estado actual para clientes que entren tarde.
        // No animar ni reproducir sonido en el estado inicial.
        SnapToState(syncState);
        hasInitialized = true;
    }

    private void OnDestroy()
    {
        // DESCOMENTA cuando Puzzle exista:
        //
        // if (doorMode == DoorMode.PuzzleLinked && linkedPuzzle != null)
        // {
        //     Puzzle puzzle = linkedPuzzle as Puzzle;
        //     if (puzzle != null)
        //         puzzle.OnSolved -= HandlePuzzleSolved;
        // }
    }

    // =========================================================================
    //  RATINTERACTABLE — Punto de entrada del sistema de interacción
    // =========================================================================

    /// <summary>
    /// Controla si el prompt "Interactuar" se muestra al apuntar.
    /// No se muestra si la puerta es PuzzleLinked o ExternalOnly.
    /// </summary>
    public override bool CanPreviewInteraction(GameObject interactor)
    {
        if (doorMode == DoorMode.PuzzleLinked ||
            doorMode == DoorMode.ExternalOnly)
            return false;

        if (isAnimating)
            return false;

        return base.CanPreviewInteraction(interactor);
    }

    /// <summary>
    /// Devuelve el mensaje contextual según si el jugador tiene la llave o no.
    /// </summary>
    public override string GetInteractionPrompt(GameObject interactor)
    {
        if (doorMode == DoorMode.RequiresItem &&
            requiredItem != null &&
            !HasRequiredItem(interactor))
        {
            return deniedMessage;
        }

        return base.GetInteractionPrompt(interactor);
    }

    /// <summary>
    /// Indica si el jugador realmente puede usar esta puerta ahora mismo.
    /// </summary>
    public override bool IsInteractableBy(GameObject interactor)
    {
        if (doorMode == DoorMode.RequiresItem &&
            requiredItem != null &&
            !HasRequiredItem(interactor))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Comprobación client-side de si el jugador tiene el item requerido.
    /// Los slots del inventario están sincronizados vía SyncList.
    /// </summary>
    private bool HasRequiredItem(GameObject interactor)
    {
        if (interactor == null) return false;

        var inv = interactor.GetComponent<NetworkInventory>();
        if (inv == null) return false;

        for (int i = 0; i < NetworkInventory.SlotCount; i++)
        {
            InventorySlot slot = inv.GetSlot(i);
            if (slot.IsEmpty || slot.itemNetId == 0) continue;

            if (!NetworkClient.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity identity))
                continue;

            var pickable = identity.GetComponent<EOS.Puzzles.PickableItem>();
            if (pickable == null || pickable.PuzzleData == null) continue;

            EOS.Puzzles.PuzzleItemData data = pickable.PuzzleData;

            if (data.ItemId != requiredItem.ItemId) continue;
            if (filterByTag && data.ItemTag != requiredItem.ItemTag) continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Validación en el servidor antes de ejecutar la interacción.
    /// </summary>
    [Server]
    public override bool CanServerInteract(NetworkIdentity interactor)
    {
        if (isAnimating)
            return false;

        if (doorMode == DoorMode.PuzzleLinked ||
            doorMode == DoorMode.ExternalOnly)
            return false;

        return base.CanServerInteract(interactor);
    }

    /// <summary>
    /// Ejecutado en el servidor cuando el jugador presiona E.
    /// </summary>
    [Server]
    public override void ServerInteract(NetworkIdentity interactor)
    {
        switch (doorMode)
        {
            case DoorMode.FreeInteraction:
                ServerToggleDoor(interactor);
                break;

            case DoorMode.RequiresItem:
                ServerTryOpenWithItem(interactor);
                break;

            case DoorMode.PuzzleLinked:
            case DoorMode.ExternalOnly:
                ServerPlayDenied(interactor);
                break;
        }
    }

    // =========================================================================
    //  LÓGICA POR MODO (SERVER)
    // =========================================================================

    [Server]
    private void ServerToggleDoor(NetworkIdentity interactor)
    {
        if (syncState.isOpen && canClose)
        {
            ServerSetClosed();
        }
        else if (!syncState.isOpen)
        {
            float direction = CalculateOpenDirection(interactor.transform.position);
            ServerSetOpen(direction);
        }
    }

    [Server]
    private void ServerTryOpenWithItem(NetworkIdentity interactor)
    {
        if (requiredItem == null)
        {
            ServerToggleDoor(interactor);
            return;
        }

        var inventory = interactor.GetComponent<NetworkInventory>();
        if (inventory == null)
        {
            ServerPlayDenied(interactor);
            return;
        }

        int foundSlot = FindMatchingSlot(inventory);

        if (foundSlot < 0)
        {
            ServerPlayDenied(interactor);
            return;
        }

        if (consumeItemOnUse)
            inventory.ServerRemoveItem(foundSlot);

        ServerToggleDoor(interactor);
    }

    /// <summary>
    /// Busca en el inventario un slot cuyo PickableItem coincida con el
    /// requiredItem por ItemId y opcionalmente por ItemTag.
    /// </summary>
    [Server]
    private int FindMatchingSlot(NetworkInventory inventory)
    {
        for (int i = 0; i < NetworkInventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (slot.IsEmpty || slot.itemNetId == 0) continue;

            if (!NetworkServer.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity identity))
                continue;

            PickableItem pickable = identity.GetComponent<PickableItem>();
            if (pickable == null || pickable.PuzzleData == null) continue;

            PuzzleItemData data = pickable.PuzzleData;

            if (data.ItemId != requiredItem.ItemId) continue;

            if (filterByTag && data.ItemTag != requiredItem.ItemTag) continue;

            return i;
        }

        return -1;
    }

    private void HandlePuzzleSolved()
    {
        if (isServer && !syncState.isOpen)
            ServerSetOpen(1f);
    }

    // =========================================================================
    //  CAMBIOS DE ESTADO (SERVER → TODOS LOS CLIENTES via SyncVar)
    // =========================================================================

    [Server]
    private void ServerSetOpen(float direction)
    {
        syncState = new DoorSyncState
        {
            isOpen = true,
            openDirection = direction
        };
        // El hook OnSyncStateChanged se dispara automáticamente en los clientes.
        // En el server/host, aplicamos manualmente:
        ApplyDoorRotation(syncState);
    }

    [Server]
    private void ServerSetClosed()
    {
        // Guardamos la dirección actual para que la animación de cierre sea correcta
        float currentDirection = syncState.openDirection;
        syncState = new DoorSyncState
        {
            isOpen = false,
            openDirection = currentDirection
        };
        ApplyDoorRotation(syncState);
    }

    /// <summary>
    /// Hook del SyncVar. Se ejecuta en los clientes cuando el estado cambia.
    /// Ignora cambios antes de la inicialización para evitar sonidos al arrancar.
    /// </summary>
    private void OnSyncStateChanged(DoorSyncState oldState, DoorSyncState newState)
    {
        if (!hasInitialized)
            return;

        ApplyDoorRotation(newState);
    }

    // =========================================================================
    //  ANIMACIÓN (ejecuta en todos: server + clientes)
    // =========================================================================

    private void ApplyDoorRotation(DoorSyncState state)
    {
        StopAllCoroutines(); // Cancelar animación previa si hay una
        Quaternion targetOpenRotation = closedRotation *
            Quaternion.AngleAxis(openAngle * state.openDirection, rotationAxis);

        if (state.isOpen)
            StartCoroutine(AnimateDoor(closedRotation, targetOpenRotation, opening: true));
        else
            StartCoroutine(AnimateDoor(targetOpenRotation, closedRotation, opening: false));
    }

    private IEnumerator AnimateDoor(Quaternion from, Quaternion to, bool opening)
    {
        isAnimating = true;
        PlaySound(opening ? openSound : closeSound);

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = animationCurve.Evaluate(elapsed / animationDuration);
            transform.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }

        transform.localRotation = to;
        isAnimating = false;

        // Actualizar el NavMeshObstacle: abierta = libre, cerrada = bloquea.
        UpdateNavObstacle(opening);

        if (opening)
            OnDoorOpened?.Invoke();
        else
            OnDoorClosed?.Invoke();
    }

    // =========================================================================
    //  API PÚBLICA (para puzzles, eventos, cinemáticas)
    // =========================================================================

    /// <summary>
    /// Abre la puerta sin condiciones. Solo llamar en el server.
    /// Ejemplos: puzzle resuelto, trigger, cinemática.
    /// </summary>
    [Server]
    public void OpenDoor()
    {
        if (!syncState.isOpen && !isAnimating)
            ServerSetOpen(1f);
    }

    /// <summary>
    /// Abre considerando la posición del jugador. Solo server.
    /// </summary>
    [Server]
    public void OpenDoor(Vector3 playerPosition)
    {
        if (!syncState.isOpen && !isAnimating)
            ServerSetOpen(CalculateOpenDirection(playerPosition));
    }

    /// <summary>Cierra la puerta. Solo server.</summary>
    [Server]
    public void CloseDoor()
    {
        if (syncState.isOpen && !isAnimating)
            ServerSetClosed();
    }

    /// <summary>Cambia el modo en runtime. Solo server.</summary>
    [Server]
    public void SetMode(DoorMode newMode)
    {
        doorMode = newMode;
    }

    // =========================================================================
    //  UTILIDADES
    // =========================================================================

    private float CalculateOpenDirection(Vector3 playerPosition)
    {
        if (!openAwayFromPlayer) return 1f;

        Vector3 doorToPlayer = (playerPosition - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, doorToPlayer);
        return (dot > 0) ? -1f : 1f;
    }

    /// <summary>
    /// Envía feedback de puerta bloqueada solo al jugador que intentó
    /// interactuar, no a todos los clientes.
    /// </summary>
    [Server]
    private void ServerPlayDenied(NetworkIdentity interactor)
    {
        // Sonido espacial para todos los que estén cerca.
        RpcPlayDeniedSound();

        // Mensaje visual solo para quien interactuó.
        if (interactor.connectionToClient != null)
        {
            TargetShowDeniedMessage(
                interactor.connectionToClient,
                deniedMessage
            );
        }

        OnDoorDenied?.Invoke();
    }

    [ClientRpc]
    private void RpcPlayDeniedSound()
    {
        PlaySound(lockedSound);
    }

    [TargetRpc]
    private void TargetShowDeniedMessage(
        NetworkConnectionToClient target,
        string message
    )
    {
        OnLocalDeniedFeedback?.Invoke(message);
    }

    /// <summary>
    /// Aplica la rotación de la puerta instantáneamente sin
    /// animación ni sonido. Usado en la inicialización del cliente.
    /// </summary>
    private void SnapToState(DoorSyncState state)
    {
        StopAllCoroutines();

        if (state.isOpen)
        {
            Quaternion targetRotation = closedRotation *
                Quaternion.AngleAxis(
                    openAngle * state.openDirection,
                    rotationAxis
                );
            transform.localRotation = targetRotation;
        }
        else
        {
            transform.localRotation = closedRotation;
        }

        UpdateNavObstacle(state.isOpen);
    }

    private void SetupAudio()
    {
        if (openSound != null || closeSound != null || lockedSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
            audioSource.maxDistance = audioMaxDistance;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1f;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    // =========================================================================
    //  NAVMESH — Bloquea el paso de criaturas cuando la puerta está cerrada
    // =========================================================================

    /// <summary>
    /// Crea un NavMeshObstacle si no existe. Empieza activo (puerta cerrada = bloquea).
    /// </summary>
    private void SetupNavMeshObstacle()
    {
        navObstacle = GetComponent<NavMeshObstacle>();
        if (navObstacle == null)
            navObstacle = gameObject.AddComponent<NavMeshObstacle>();

        navObstacle.carving = true;
        navObstacle.shape = NavMeshObstacleShape.Box;

        // Intentar ajustar el tamaño al collider de la puerta.
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            navObstacle.size = box.size;
            navObstacle.center = box.center;
        }

        // Puerta cerrada = obstáculo activo.
        navObstacle.enabled = !syncState.isOpen;
    }

    /// <summary>
    /// Activa/desactiva el obstáculo del NavMesh según el estado de la puerta.
    /// </summary>
    private void UpdateNavObstacle(bool doorIsOpen)
    {
        if (navObstacle != null)
            navObstacle.enabled = !doorIsOpen;
    }
}