using System;

namespace EOS.Puzzles
{
    /// <summary>
    /// Contrato mínimo que cualquier actor de puzzle debe cumplir.
    /// Un "actor" es cualquier mesh interactuable que guarda un valor
    /// (bool, string, float, o una referencia a un item).
    ///
    /// Los puzzles NUNCA conocen el tipo concreto de actor (Toggle, Teclado,
    /// Slot...). Solo llaman GetValue() y escuchan OnValueChanged.
    /// Esto es lo que permite que un ValidationType nuevo funcione con
    /// cualquier combinación de actores sin cambiar el código del actor,
    /// y que un actor nuevo funcione con cualquier validación existente.
    /// </summary>
    public interface IPuzzleActor
    {
        /// <summary>Id único dentro del puzzle al que pertenece.</summary>
        string ActorId { get; }

        /// <summary>Si el actor acepta interacción en este momento.</summary>
        bool CanInteract { get; }

        /// <summary>
        /// El valor actual del actor, boxeado como object.
        /// Puede ser bool, string, float o PuzzleItemData según el tipo de actor.
        /// </summary>
        object GetValue();

        /// <summary>
        /// Se dispara cada vez que el valor cambia. El LeafPuzzle que posee
        /// este actor se suscribe para saber cuándo re-evaluar.
        /// </summary>
        event Action OnValueChanged;
    }
}
