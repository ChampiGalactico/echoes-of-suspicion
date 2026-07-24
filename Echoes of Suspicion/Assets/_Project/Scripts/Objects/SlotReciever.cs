using UnityEngine;

/// <summary>
/// TEMPORAL
/// 
/// Receptor que escucha el evento OnSlotCompleted de ObjectSlot
/// y spawnea un prefab cuando se activa.
/// 
/// </summary>
public class SlotReceiver : MonoBehaviour
{
    [Header("Qué spawnear")]
    [SerializeField] private GameObject prefabToSpawn;

    [Header("Dónde spawnear")]
    [Tooltip("Si está vacío, spawnea en la posición de este GameObject.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Opciones")]
    [Tooltip("Si es true, solo spawnea una vez y después se ignora.")]
    [SerializeField] private bool spawnOnlyOnce = true;

    [Tooltip("Si se asigna, solo reacciona a este slot específico. Si está vacío, reacciona a cualquier slot.")]
    [SerializeField] private ObjectSlot targetSlot;

    private bool _hasSpawned = false;

    private void OnEnable()
    {
        ObjectSlot.OnSlotCompleted += HandleSlotCompleted;
    }

    private void OnDisable()
    {
        ObjectSlot.OnSlotCompleted -= HandleSlotCompleted;
    }

    private void HandleSlotCompleted(ObjectSlot slot)
    {
        Debug.LogWarning($"[SlotReceiver] '{gameObject.name}' → Terminó");
        // Filtrar por slot específico si se configuró uno
        if (targetSlot != null && slot != targetSlot) return;

        // Si ya spawneó y es one-shot, ignorar
        if (spawnOnlyOnce && _hasSpawned) return;

        Debug.LogWarning($"[SlotReceiver] '{gameObject.name}' → Vamos a spawnear");

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[SlotReceiver] '{gameObject.name}' → No hay prefab asignado para spawnear.");
            return;
        }

        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameObject spawned = Instantiate(prefabToSpawn, position, rotation);
        _hasSpawned = true;

        Debug.Log($"[SlotReceiver] '{gameObject.name}' → Spawneado '{spawned.name}' " +
                   $"en respuesta al slot '{slot.gameObject.name}'.");
    }

    /// <summary>
    /// Resetea el estado para permitir spawnear de nuevo (útil si spawnOnlyOnce es true).
    /// </summary>
    public void ResetSpawn()
    {
        _hasSpawned = false;
    }
}