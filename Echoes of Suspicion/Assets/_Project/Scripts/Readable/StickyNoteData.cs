using UnityEngine;
using TMPro;

/// <summary>
/// Datos de una nota adhesiva en el mundo.
/// Para documentos con secciones (título, subtítulo, contenido, footer)
/// usar DocumentData en su lugar.
/// </summary>
[CreateAssetMenu(fileName = "NewStickyNote", menuName = "EOS/Readables/Sticky Note Data")]
public class StickyNoteData : ScriptableObject
{
    [TextArea(2, 6)]
    public string NoteText;

    [Tooltip("Imagen opcional que aparece en la nota.")]
    public Sprite NoteImage;

    [Tooltip("Fuente de la nota. Si es null, usa la fuente por defecto del TMP.")]
    public TMP_FontAsset NoteFont;

    [Tooltip("Tamaño de fuente. Si es 0 o menor, usa el tamaño por defecto del TMP.")]
    public float FontSize = 22f;

    public NoteColor StickyColor = NoteColor.Yellow;

    [Header("Interacción")]
    [Tooltip("Texto que muestra el HUD al apuntar al objeto.")]
    public string InteractionPrompt = "Leer";
}

public enum NoteColor
{
    Yellow,
    Pink,
    Blue,
    Green,
}
