using UnityEngine;

/// <summary>
/// Comportamiento de un ícono individual de criatura en el mapa del Guía.
/// GuideMapView instancia uno por criatura netId detectada.
///
/// Mientras la criatura sigue dentro del radio compartido, GuideMapView
/// llama a UpdateBlip() cada actualización y el ícono se mantiene sólido.
/// Cuando deja de aparecer en las actualizaciones, GuideMapView lo pasa a
/// modo "última posición conocida" (MarkStale), que hace un fade progresivo
/// hasta desaparecer. Si el rastro se pierde por completo o queda visible
/// como última posición conocida es una decisión de balance pendiente
/// (Propuesta_Tecnica, sección 7) — por eso el tiempo de fade y el alpha
/// intermedio son ajustables desde el inspector del prefab, sin tocar código.
///
/// Apariencia: todas las criaturas usan el mismo punto (sin distinguir tipo
/// por sprite/color). Simplemente pon el Image del prefab en rojo desde el
/// Inspector — este script no toca sprite ni color por código.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class GuideMapCreatureIcon : MonoBehaviour
{
    [SerializeField, Tooltip("Segundos que tarda en desvanecerse por completo tras perder el rastro (0 = desaparece instantáneamente).")]
    private float staleFadeOutDuration = 3f;

    [SerializeField, Range(0f, 1f), Tooltip("Alpha del ícono al momento de perder el rastro, antes de empezar a desvanecerse. En 0, el ícono desaparece de inmediato al salir del radio.")]
    private float staleAlpha = 0.4f;

    public uint CreatureNetId { get; private set; }

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private bool isStale;
    private float staleTimer;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>Configura el ícono recién creado. Llamar una sola vez, justo después de Instantiate().</summary>
    public void Initialize(uint creatureNetId)
    {
        CreatureNetId = creatureNetId;
        isStale = false;
        staleTimer = 0f;
        canvasGroup.alpha = 1f;
    }

    /// <summary>Actualiza la posición en el mapa mientras la criatura sigue detectada dentro del radio compartido.</summary>
    public void UpdateBlip(Vector2 anchoredPosition)
    {
        isStale = false;
        staleTimer = 0f;
        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    /// <summary>La criatura salió del radio compartido: entra en modo "última posición conocida" y empieza a desvanecerse.</summary>
    public void MarkStale()
    {
        if (isStale)
        {
            return;
        }

        isStale = true;
        staleTimer = 0f;
        canvasGroup.alpha = staleAlpha;
    }

    /// <summary>
    /// Avanza el fade si está en modo stale. Devuelve true cuando el fade
    /// terminó, para que GuideMapView destruya este ícono.
    /// </summary>
    public bool TickStaleAndCheckExpired(float deltaTime)
    {
        if (!isStale)
        {
            return false;
        }

        if (staleFadeOutDuration <= 0f)
        {
            return true;
        }

        staleTimer += deltaTime;
        float t = Mathf.Clamp01(staleTimer / staleFadeOutDuration);
        canvasGroup.alpha = Mathf.Lerp(staleAlpha, 0f, t);

        return t >= 1f;
    }
}
