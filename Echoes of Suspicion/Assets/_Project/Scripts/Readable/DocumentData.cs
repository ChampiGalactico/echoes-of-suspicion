using UnityEngine;
using TMPro;

/// <summary>
/// Tipo predefinido de sección con tamaños de fuente por defecto.
/// Si se especifica FontSizeOverride en la sección, este valor se ignora.
/// </summary>
public enum SectionType
{
    Title,      // 28pt
    Subtitle,   // 20pt
    Body,       // 16pt
    Footer,     // 12pt
    Caption,    // 14pt
}

/// <summary>
/// Posición vertical del contenido dentro del panel del documento.
/// </summary>
public enum DocumentVerticalAlignment
{
    Top,
    Center,
    Bottom,
}

/// <summary>
/// Una sección de un documento legible: título, subtítulo, contenido,
/// footer, o cualquier bloque de texto que necesites.
/// </summary>
[System.Serializable]
public class DocumentSection
{
    public SectionType Type = SectionType.Body;

    [TextArea(2, 8)]
    public string Text;

    [Tooltip("Alineación horizontal y vertical del texto en esta sección.")]
    public TextAlignmentOptions Alignment = TextAlignmentOptions.TopLeft;

    [Tooltip("Anclar esta sección al fondo del panel, fuera del flujo vertical.")]
    public bool AnchorToBottom;

    [Tooltip("Mostrar una línea divisoria debajo de esta sección.")]
    public bool ShowDivider;

    [Tooltip("Color de la línea divisoria.")]
    public Color DividerColor = new Color(0.27f, 0.27f, 0.27f, 1f);

    [Tooltip("Fuente específica para esta sección. Si es null, usa DefaultFont del documento.")]
    public TMP_FontAsset Font;

    [Tooltip("Tamaño de fuente custom. Si es 0 o menor, usa el tamaño predefinido del SectionType.")]
    public float FontSizeOverride = -1f;

    /// <summary>Devuelve el tamaño de fuente efectivo: override si es > 0, sino el default del tipo.</summary>
    public float EffectiveFontSize => FontSizeOverride > 0f
        ? FontSizeOverride
        : Type switch
        {
            SectionType.Title    => 28f,
            SectionType.Subtitle => 20f,
            SectionType.Body     => 16f,
            SectionType.Footer   => 12f,
            SectionType.Caption  => 14f,
            _                    => 16f,
        };
}

/// <summary>
/// Datos de un documento legible con secciones configurables.
/// Cada sección tiene su tipo, texto, fuente opcional y divider opcional.
/// Para notas adhesivas simples, usar StickyNoteData en su lugar.
/// </summary>
[CreateAssetMenu(fileName = "NewDocument", menuName = "EOS/Readables/Document Data")]
public class DocumentData : ScriptableObject
{
    [Header("Fuente por defecto")]
    [Tooltip("Fuente base del documento. Las secciones sin fuente propia usan esta.")]
    public TMP_FontAsset DefaultFont;

    [Header("Layout")]
    [Tooltip("Posición vertical del contenido dentro del panel.")]
    public DocumentVerticalAlignment VerticalAlignment = DocumentVerticalAlignment.Top;

    [Header("Secciones")]
    [Tooltip("Cada entrada es un bloque de texto (título, subtítulo, cuerpo, footer, etc).")]
    public DocumentSection[] Sections;

    [Header("Imagen (opcional)")]
    [Tooltip("Imagen que aparece al final del documento.")]
    public Sprite ContentImage;

    [Header("Interacción")]
    [Tooltip("Texto que muestra el HUD al apuntar al objeto.")]
    public string InteractionPrompt = "Leer";
}
