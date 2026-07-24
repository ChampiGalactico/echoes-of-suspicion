using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slot que detecta cuántos objetos están encima de él (excluyéndose a sí mismo).
/// Cuando la cantidad alcanza el umbral requerido, emite un evento.
/// 
/// Requiere: un Collider marcado como IsTrigger en este GameObject.
/// Los objetos que entren deben tener Rigidbody (o ser hijos de uno).
/// 
/// TEMPORAL — eliminar cuando ya no se necesite.
/// </summary>
public class ObjectSlot : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Cantidad de objetos necesarios para activar el slot.")]
    [Min(1)]
    [SerializeField] private int requiredCount = 3;

    // ===== EVENTO =====
    /// <summary>
    /// Se dispara cuando el slot se completa (tiene >= requiredCount objetos).
    /// Pasa la referencia del slot que se activó.
    /// </summary>
    public static event Action<ObjectSlot> OnSlotCompleted;

    /// <summary>
    /// Se dispara cuando el slot deja de estar completo (un objeto salió).
    /// </summary>
    public static event Action<ObjectSlot> OnSlotUncompleted;

    // ===== ESTADO =====
    private readonly HashSet<Collider> _objectsInSlot = new HashSet<Collider>();
    private bool _isCompleted = false;

    public int CurrentCount => _objectsInSlot.Count;
    public int RequiredCount => requiredCount;
    public bool IsCompleted => _isCompleted;

    private void OnTriggerEnter(Collider other)
    {
        // Ignorar al jugador
        if (other.CompareTag("Player")) return;

        if (_objectsInSlot.Add(other))
        {
            Debug.Log($"[ObjectSlot] '{gameObject.name}' → entró '{other.gameObject.name}'. " +
                       $"Objetos: {_objectsInSlot.Count}/{requiredCount}");

            CheckCompletion();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_objectsInSlot.Remove(other))
        {
            Debug.Log($"[ObjectSlot] '{gameObject.name}' → salió '{other.gameObject.name}'. " +
                       $"Objetos: {_objectsInSlot.Count}/{requiredCount}");

            // Si estaba completo y ahora ya no
            if (_isCompleted && _objectsInSlot.Count < requiredCount)
            {
                _isCompleted = false;
                OnSlotUncompleted?.Invoke(this);
                Debug.Log($"[ObjectSlot] '{gameObject.name}' → ¡DESACTIVADO!");
            }
        }
    }

    private void CheckCompletion()
    {
        if (!_isCompleted && _objectsInSlot.Count >= requiredCount)
        {
            _isCompleted = true;
            OnSlotCompleted?.Invoke(this);
            Transform hermano = transform.parent.Find("PhysicalSlot");
            if (hermano != null)
            {
                hermano.GetComponent<Renderer>().material.color = Color.limeGreen;
            }
            Debug.Log($"[ObjectSlot] '{gameObject.name}' → ¡COMPLETADO! Emitiendo evento.");
        }
    }

    /// <summary>
    /// Limpia objetos destruidos del tracking (por si alguno se destruye dentro del trigger).
    /// Llamar desde Update si los objetos pueden ser destruidos mientras están en el slot.
    /// </summary>
    public void CleanupNulls()
    {
        int before = _objectsInSlot.Count;
        _objectsInSlot.RemoveWhere(c => c == null);

        if (_objectsInSlot.Count != before)
        {
            Debug.Log($"[ObjectSlot] '{gameObject.name}' → limpiados {before - _objectsInSlot.Count} objetos nulos.");

            if (_isCompleted && _objectsInSlot.Count < requiredCount)
            {
                _isCompleted = false;
                OnSlotUncompleted?.Invoke(this);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _isCompleted ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}