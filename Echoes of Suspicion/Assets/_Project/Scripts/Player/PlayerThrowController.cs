using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Permite al jugador local lanzar un objeto (piedra, etc.).
/// La fuerza de lanzamiento se multiplica por el stat de Fuerza del personaje.
///
/// El cliente decide origen y dirección (desde su propia cámara) porque solo él
/// conoce hacia dónde apunta localmente. El servidor ejecuta el spawn real y la física.
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public sealed class PlayerThrowController : NetworkBehaviour
{
    [Header("Throw Point")]
    [SerializeField, Tooltip("Desde dónde sale el objeto (normalmente la cámara del jugador).")]
    private Transform throwOrigin;

    [SerializeField, Tooltip("Distancia hacia adelante desde throwOrigin para evitar que el objeto choque contra el propio jugador al spawnear.")]
    private float spawnForwardOffset = 0.8f;

    [Header("Throwable")]
    [SerializeField, Tooltip("Prefab a lanzar. Debe estar registrado como Spawnable Prefab en el NetworkManager.")]
    private GameObject throwablePrefab;

    [SerializeField, Tooltip("Fuerza base antes de aplicar el multiplicador de Fuerza del personaje.")]
    private float baseThrowForce = 12f;

    [Header("Input")]
    [SerializeField, Tooltip("Tecla/botón para lanzar. Por defecto click izquierdo.")]
    private Key throwKey = Key.None;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    private CharacterStatsProvider statsProvider;

    private void Awake()
    {
        statsProvider = GetComponent<CharacterStatsProvider>();
    }

    private void Update()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        if (WasThrowInputPressed())
        {
            Vector3 origin = throwOrigin.position + throwOrigin.forward * spawnForwardOffset;
            Vector3 direction = throwOrigin.forward;

            CmdThrowObject(origin, direction);
        }
    }

    private bool WasThrowInputPressed()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && throwKey != Key.None && keyboard[throwKey].wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    [Command]
    private void CmdThrowObject(Vector3 origin, Vector3 direction)
    {
        if (throwablePrefab == null)
        {
            Debug.LogWarning("[PlayerThrowController] No hay throwablePrefab asignado.");
            return;
        }

        GameObject thrown = Instantiate(throwablePrefab, origin, Quaternion.LookRotation(direction));

        var rb = thrown.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // La Fuerza del personaje amplifica cuán lejos/fuerte llega el objeto.
            float strengthMultiplier = statsProvider != null ? statsProvider.StrengthMultiplier : 1f;
            float finalForce = baseThrowForce * strengthMultiplier;

            rb.linearVelocity = direction.normalized * finalForce;
        }
        else
        {
            Debug.LogWarning("[PlayerThrowController] El throwablePrefab no tiene Rigidbody.");
        }

        NetworkServer.Spawn(thrown);

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerThrowController] Objeto lanzado por player {netId} " +
                      $"desde {origin} en dirección {direction} " +
                      $"(fuerza base {baseThrowForce} × Str {statsProvider?.StrengthMultiplier ?? 1f})");
        }
    }
}