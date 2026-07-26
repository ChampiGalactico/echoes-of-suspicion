using UnityEngine;

namespace EOS.Puzzles
{
    public enum NoiseLevel { Low, Medium, High }

    /// <summary>
    /// Bus estático que conecta los puzzles con la criatura y la vida del
    /// Guía sin que ninguna de las dos partes se conozca directamente.
    /// La criatura se suscribe a OnNoiseGenerated; el sistema de vida del
    /// Guía se suscribe a OnGuideHealthPenalty.
    /// </summary>
    public static class PuzzleEvents
    {
        public static event System.Action<Vector3, NoiseLevel> OnNoiseGenerated;
        public static void RaiseNoiseGenerated(Vector3 position, NoiseLevel level) =>
            OnNoiseGenerated?.Invoke(position, level);

        public static event System.Action<float> OnGuideHealthPenalty;
        public static void RaiseGuideHealthPenalty(float amount) =>
            OnGuideHealthPenalty?.Invoke(amount);
    }
}
