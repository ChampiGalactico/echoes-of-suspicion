using UnityEngine;

/// <summary>
/// Datos de un personaje jugable: identidad y stats base.
/// Los stats son multiplicadores (1 = neutro) que otros sistemas
/// (throw, movimiento, percepción) consultan a través de CharacterStatsProvider.
///
/// No contiene lógica de gameplay ni distingue rol — el mismo personaje
/// puede ser Guía o Corredor, y cada sistema decide cómo usar estos valores
/// según el rol activo.
///
/// Se crea desde Unity con: Create → Echoes → Player → Character Data.
/// </summary>
[CreateAssetMenu(fileName = "New Character Data", menuName = "Echoes/Player/Character Data")]
public sealed class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterName = "Unnamed Character";

    [TextArea(2, 4)]
    public string archetype;

    [TextArea(3, 6), Tooltip("Descripción corta para la pantalla de selección de personaje.")]
    public string shortDescription;

    [Header("Health")]
    [Tooltip("Vida máxima del Corredor con este personaje. Valor absoluto, no multiplicador.")]
    public float maxHealth = 100f;

    [Header("Stats (multiplicadores, 1 = neutro)")]
    [Tooltip("Corredor: fuerza de lanzamiento y futuro daño cuerpo a cuerpo.")]
    [Range(0.5f, 2f)]
    public float strengthMultiplier = 1f;

    [Tooltip("Corredor: duración del sprint antes de cansarse.")]
    [Range(0.5f, 2f)]
    public float staminaMultiplier = 1f;

    [Tooltip("Corredor: velocidad de movimiento y sigilo. Guía: velocidad de crafteo.")]
    [Range(0.5f, 2f)]
    public float agilityMultiplier = 1f;

    [Tooltip("Corredor: detecta criaturas cercanas por instinto (latido). Guía: radio de percepción en el mapa.")]
    [Range(0.5f, 2f)]
    public float perceptionMultiplier = 1f;

    [Header("Biome Link")]
    [Tooltip("Nombre de la escena del bioma que corresponde a este personaje cuando actúa de Guía. Placeholder hasta tener la arquitectura de biomas por personaje.")]
    public string guideBiomeSceneName;
}