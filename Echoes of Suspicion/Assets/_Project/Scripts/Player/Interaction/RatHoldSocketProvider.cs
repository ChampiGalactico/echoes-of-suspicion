using UnityEngine;

/// <summary>
/// Proporciona sockets distintos para la representación local
/// en primera persona y para las copias remotas en tercera persona.
/// </summary>
[DisallowMultipleComponent]
public sealed class RatHoldSocketProvider : MonoBehaviour
{
    [Header("Player")]
    [SerializeField]
    private NetworkRatInteractor networkInteractor;

    [Header("Hold Sockets")]
    [SerializeField]
    private Transform firstPersonHoldSocket;

    [SerializeField]
    private Transform thirdPersonHoldSocket;

    [Header("Drop")]
    [SerializeField]
    private Transform dropOrigin;

    public Transform FirstPersonHoldSocket =>
        firstPersonHoldSocket;

    public Transform ThirdPersonHoldSocket =>
        thirdPersonHoldSocket;

    public Transform DropOrigin =>
        dropOrigin;

    private void Awake()
    {
        if (networkInteractor == null)
        {
            networkInteractor =
                GetComponent<NetworkRatInteractor>();
        }
    }

    public bool TryGetHoldSocket(
        out Transform socket)
    {
        bool useFirstPerson =
            networkInteractor != null &&
            networkInteractor.isLocalPlayer;

        socket =
            useFirstPerson
                ? firstPersonHoldSocket
                : thirdPersonHoldSocket;

        // Respaldo para evitar perder el objeto si una
        // referencia quedó sin asignar temporalmente.
        if (socket == null)
        {
            socket =
                useFirstPerson
                    ? thirdPersonHoldSocket
                    : firstPersonHoldSocket;
        }

        return socket != null;
    }

    public bool TryGetDropPose(
        out Vector3 position,
        out Quaternion rotation)
    {
        if (dropOrigin == null)
        {
            position = transform.position;
            rotation = transform.rotation;
            return false;
        }

        position = dropOrigin.position;
        rotation = dropOrigin.rotation;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (networkInteractor == null)
        {
            networkInteractor =
                GetComponent<NetworkRatInteractor>();
        }
    }
#endif
}