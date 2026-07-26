using UnityEngine;

namespace EOS.Puzzles
{
    /// <summary>
    /// Datos de un item que un SlotActor puede recibir: herramienta, producto,
    /// llave, documento. Es un ScriptableObject porque es información
    /// compartida y estática — no necesita sincronizarse por red, ya que
    /// existe igual en todas las máquinas desde que arranca el juego.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPuzzleItem", menuName = "EOS/Puzzles/PuzzleItemData")]
    public class PuzzleItemData : ScriptableObject
    {
        [Tooltip("Id único, usado por PuzzleValidation.Matches para comparar.")]
        public string ItemId;

        public string DisplayName;

        [Tooltip("Para filtrar qué SlotActors aceptan este item (ej: 'Tool', 'Product').")]
        public string ItemTag;

        [Tooltip("Usado por SumEquals (ej: el precio de un producto).")]
        public float NumericValue;

        public Sprite Icon;
        public GameObject WorldModel;
    }
}
