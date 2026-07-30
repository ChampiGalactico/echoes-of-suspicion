using UnityEngine;

namespace EOS.Puzzles.Morse
{
    /// <summary>
    /// Configuración del puzzle Morse. Es un ScriptableObject local: todos
    /// los clientes pueden tener el mismo asset, pero NO se sincroniza por
    /// red. El servidor es la autoridad del estado seleccionado (secuencia,
    /// índice) y solo replica identificadores pequeños.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MorsePuzzleDefinition",
        menuName = "EOS/Puzzles/Morse Puzzle Definition")]
    public sealed class MorsePuzzleDefinition : ScriptableObject
    {
        [Header("Secuencia")]

        [Tooltip("Símbolos permitidos. Deben ser un subconjunto del alfabeto " +
                 "MVP: E T A N S M D U G R.")]
        [SerializeField]
        private string[] allowedSymbols =
        {
            "E", "T", "A", "N", "S", "M", "D", "U", "G", "R"
        };

        [Tooltip("Cuántos símbolos distintos tiene la secuencia.")]
        [SerializeField, Min(1)]
        private int sequenceLength = 3;

        [Header("Fallo")]

        [Tooltip("Daño aplicado al Runner por un panel incorrecto " +
                 "(valor positivo).")]
        [SerializeField, Min(0f)]
        private float damageOnFailure = 12f;

        [Tooltip("Penalización de vida del Guía en un fallo (0 = ninguna).")]
        [SerializeField, Min(0f)]
        private float guideHealthPenaltyOnFailure = 0f;

        [Tooltip("Nivel de ruido publicado en un fallo.")]
        [SerializeField]
        private NoiseLevel failureNoiseLevel = NoiseLevel.High;

        [Tooltip("Intensidad (0..1) del NoiseEvent audible por la criatura.")]
        [SerializeField, Range(0f, 1f)]
        private float failureNoiseIntensity = 0.95f;

        [Tooltip("Si es true, un fallo reinicia toda la secuencia. Por " +
                 "defecto false: solo repite el paso actual.")]
        [SerializeField]
        private bool resetWholeSequenceOnFailure = false;

        [Header("Retrasos (segundos)")]

        [Tooltip("Espera tras un fallo antes de repetir el patrón del paso.")]
        [SerializeField, Min(0f)]
        private float retryDelay = 1.25f;

        [Tooltip("Espera tras un acierto antes de arrancar el siguiente paso.")]
        [SerializeField, Min(0f)]
        private float advanceDelay = 0.6f;

        [Tooltip("Bloqueo anti doble-interacción mientras se procesa.")]
        [SerializeField, Min(0f)]
        private float interactionCooldown = 0.4f;

        // ─── Accessors ───

        public string[] AllowedSymbols => allowedSymbols;
        public int SequenceLength => Mathf.Max(1, sequenceLength);
        public float DamageOnFailure => damageOnFailure;
        public float GuideHealthPenaltyOnFailure => guideHealthPenaltyOnFailure;
        public NoiseLevel FailureNoiseLevel => failureNoiseLevel;
        public float FailureNoiseIntensity => Mathf.Clamp01(failureNoiseIntensity);
        public bool ResetWholeSequenceOnFailure => resetWholeSequenceOnFailure;
        public float RetryDelay => retryDelay;
        public float AdvanceDelay => advanceDelay;
        public float InteractionCooldown => interactionCooldown;
    }
}
