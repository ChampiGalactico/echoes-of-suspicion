using UnityEngine;

/// <summary>
/// Datos de un objeto legible: hoja de diagnóstico, nota adhesiva,
/// carta, recibo, etc. Es un ScriptableObject para poder reutilizar
/// el mismo contenido en varios objetos del mundo si hace falta.
/// </summary>
[CreateAssetMenu(fileName = "NewReadable", menuName = "EOS/Readables/ReadableData")]
public class ReadableData : ScriptableObject
{
    public ReadableType Type;

    [Header("Documento (solo si Type = Document)")]
    public string Title;
    public string Subtitle;

    [TextArea(4, 12)]
    public string Content;

    [Tooltip("Imagen opcional que aparece debajo del contenido.")]
    public Sprite ContentImage;

    [Header("Nota adhesiva (solo si Type = StickyNote)")]
    [TextArea(2, 6)]
    public string NoteText;

    public Sprite NoteImage;
    public NoteColor StickyColor = NoteColor.Yellow;

    [Header("Interacción")]
    [Tooltip("Texto que muestra el HUD al apuntar al objeto.")]
    public string InteractionPrompt = "Leer";
}

public enum ReadableType
{
    Document,
    StickyNote,
}

public enum NoteColor
{
    Yellow,
    Pink,
    Blue,
    Green,
}
