using UnityEngine;

/// <summary>
/// Configuration asset for the flashlight.
/// Standalone ScriptableObject — NOT an inventory item.
/// The flashlight is a permanent ability on the player, always available.
///
/// Create from: Create → Echoes → Flashlight Data.
/// </summary>
[CreateAssetMenu(fileName = "New Flashlight Config", menuName = "Echoes/Flashlight Data")]
public class FlashlightData : ScriptableObject
{
    [Header("Battery")]
    [Tooltip("Maximum battery charge.")]
    [Min(1f)]
    public float maxBattery = 100f;

    [Tooltip("Battery units drained per second while the flashlight is on. " +
             "Adjust in Play Mode to test different durations.")]
    [Min(0.01f)]
    public float drainPerSecond = 2f;

    [Header("Light")]
    [Min(0.1f)]
    public float lightRange = 15f;

    [Min(0.1f)]
    public float lightIntensity = 1.5f;

    [Range(10f, 90f)]
    public float spotAngle = 45f;

    public Color lightColor = Color.white;
}
