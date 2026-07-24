using UnityEngine;

/// <summary>
/// Expone los puntos que utilizan los objetos agarrables.
/// No contiene lógica de red; el NetworkIdentity ya está en la raíz
/// del jugador.
/// </summary>
[DisallowMultipleComponent]
public sealed class RatHoldSocketProvider : MonoBehaviour
{
    [Header("Sockets")]
    [SerializeField]
    private Transform holdSocket;

    [SerializeField]
    private Transform dropOrigin;

    public Transform HoldSocket => holdSocket;

    public Transform DropOrigin => dropOrigin;

    public bool TryGetHoldSocket(out Transform socket)
    {
        socket = holdSocket;
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
        if (holdSocket == null)
        {
            Debug.LogWarning(
                "RatHoldSocketProvider no tiene HoldSocket asignado.",
                this);
        }

        if (dropOrigin == null)
        {
            Debug.LogWarning(
                "RatHoldSocketProvider no tiene DropOrigin asignado.",
                this);
        }
    }
#endif
}