using UnityEngine;

/// <summary>
/// Configuración de un tipo de criatura.
/// Cada tipo (Basica, Rapida, etc.) tiene su propio asset con distintos valores.
///
/// Se crea desde Unity con: Create → Echoes → Creature Data.
/// </summary>
[CreateAssetMenu(fileName = "New Creature Data", menuName = "Echoes/AI/Creature Data")]
public sealed class CreatureData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Nombre visible del tipo de criatura, para debug y UI.")]
    public string creatureName = "Unnamed Creature";

    [Header("Movement")]
    [Tooltip("Velocidad cuando está patrullando (baja).")]
    public float patrolSpeed = 6f;

    [Tooltip("Velocidad cuando está alerta o investigando un ruido.")]
    public float alertSpeed = 8f;

    [Tooltip("Velocidad cuando persigue al jugador o está enfurecida.")]
    public float chaseSpeed = 12f;

    [Header("Noise Perception")]
    [Tooltip("Radio en el que puede escuchar ruidos. Si el ruido está más lejos, no reacciona.")]
    public float hearingRadius = 20f;

    [Tooltip("Intensidad mínima del ruido para que la criatura reaccione (0-1).")]
    [Range(0f, 1f)]
    public float minNoiseIntensity = 0.1f;

    [Header("Visual Perception")]
    [Tooltip("Radio en el que puede ver al jugador.")]
    public float visionRadius = 10f;

    [Tooltip("Ángulo del cono de visión frontal en grados (0-360).")]
    [Range(0f, 360f)]
    public float visionAngle = 90f;

    [Header("Investigation")]
    [Tooltip("Tiempo que espera en el punto del ruido antes de volver a patrullar.")]
    public float investigationTime = 4f;

    [Header("Stun")]
    [Tooltip("Duración del aturdimiento cuando la aturden con luz.")]
    public float stunDuration = 2f;

    [Header("Attack")]
    [Tooltip("Radio dentro del cual la criatura puede atacar al Corredor.")]
    public float attackRadius = 1.5f;

    [Tooltip("Daño que hace cada ataque exitoso al Corredor.")]
    public float attackDamage = 20f;

    [Tooltip("Segundos entre ataques (cooldown).")]
    public float attackCooldown = 1f;
}