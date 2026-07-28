using UnityEngine;

/// <summary>
/// Define la correspondencia entre el espacio del mundo (plano XZ del bioma
/// activo) y el espacio normalizado (0-1, 0-1) del mapa esquemático del Guía.
///
/// Se coloca UNA instancia por escena de bioma, con dos Transform hijos
/// (normalmente vacíos, solo posición) marcando qué punto del mundo
/// corresponde a la esquina inferior-izquierda y cuál a la esquina
/// superior-derecha de la imagen de fondo del mapa en el HUD del Guía.
///
/// GuideMapView busca la instancia activa vía MapBounds.Current — no hace
/// falta que se conozcan entre sí ni que estén en la misma escena de red
/// (el Guía nunca visita físicamente el bioma del Corredor, pero SÍ recibe
/// la escena/geometría de referencia cargada para poder dibujar el mapa).
/// </summary>
public sealed class MapBounds : MonoBehaviour
{
    [Header("Auto-cálculo desde geometría (recomendado)")]
    [SerializeField, Tooltip("Layer que contiene TODA la geometría que debe verse en el mapa (paredes, puertas). Única fuente de verdad para el layer: GuideMapCameraRig lee este mismo valor para su Culling Mask, así nunca quedan desincronizados entre sí.")]
    private LayerMask geometryLayer;

    [SerializeField, Tooltip("Si está activo, IGNORA los dos Transform de abajo y calcula las esquinas automáticamente a partir del bounding box real de toda la geometría en Geometry Layer. Elimina el error humano de colocar los Transforms 'casi, pero no exacto' en la esquina correcta — que es justo lo que causaba el desalineamiento entre el ícono del Corredor y las paredes del mapa.")]
    private bool autoComputeFromGeometry = true;

    [Header("Esquinas manuales (respaldo / si Auto está desactivado)")]
    [SerializeField, Tooltip("Punto del mundo que corresponde a la esquina inferior-izquierda de la imagen del mapa.")]
    private Transform worldMinCorner;

    [SerializeField, Tooltip("Punto del mundo que corresponde a la esquina superior-derecha de la imagen del mapa.")]
    private Transform worldMaxCorner;

    [Header("Padding (por lado, no siempre simétrico)")]
    [SerializeField, Min(0f), Tooltip("Margen extra hacia IZQUIERDA (X-) agregado a la esquina mínima.")]
    private float paddingMinX = 1f;

    [SerializeField, Min(0f), Tooltip("Margen extra hacia ABAJO (Z-) agregado a la esquina mínima.")]
    private float paddingMinZ = 1f;

    [SerializeField, Min(0f), Tooltip("Margen extra hacia DERECHA (X+) agregado a la esquina máxima.")]
    private float paddingMaxX = 1f;

    [SerializeField, Min(0f), Tooltip("Margen extra hacia ARRIBA (Z+) agregado a la esquina máxima.")]
    private float paddingMaxZ = 1f;

    [Header("Orientación")]
    [SerializeField, Tooltip("Invierte el eje horizontal (izquierda/derecha) del ícono del Corredor/criaturas. IMPORTANTE: debe quedar en false, y en cambio el volteo horizontal se aplica en el RawImage (UV Rect X=1, W=-1) — esa combinación es la que mantiene al ícono alineado con las paredes Y corrige la dirección real/mapa al mismo tiempo.")]
    private bool invertHorizontal = false;

    [SerializeField, Tooltip("Igual que Invert Horizontal, pero para arriba/abajo. Normalmente false.")]
    private bool invertVertical = true;

    /// <summary>Instancia activa del bioma actual. Se sobreescribe si se carga otro bioma con su propio MapBounds.</summary>
    public static MapBounds Current { get; private set; }

    /// <summary>Layer de la geometría del mapa. GuideMapCameraRig lo usa para su Culling Mask — única fuente de verdad, ver comentario del campo.</summary>
    public LayerMask GeometryLayer => geometryLayer;

    private Vector3 autoMin;
    private Vector3 autoMax;
    private bool autoBoundsComputed;

    /// <summary>
    /// Esquina inferior-izquierda EFECTIVA (ya con el padding aplicado hacia
    /// afuera). Tanto GuideMapCameraRig (encuadre de cámara) como
    /// WorldToMapUV (posición de íconos) usan esta misma esquina — por eso
    /// nunca deberían desalinearse entre sí.
    /// </summary>
    public Vector3 WorldMin
    {
        get
        {
            Vector3 raw = (autoComputeFromGeometry && autoBoundsComputed)
                ? autoMin
                : (worldMinCorner != null ? worldMinCorner.position : Vector3.zero);

            return raw - new Vector3(paddingMinX, 0f, paddingMinZ);
        }
    }

    /// <summary>Esquina superior-derecha EFECTIVA (ya con el padding aplicado hacia afuera). Ver WorldMin.</summary>
    public Vector3 WorldMax
    {
        get
        {
            Vector3 raw = (autoComputeFromGeometry && autoBoundsComputed)
                ? autoMax
                : (worldMaxCorner != null ? worldMaxCorner.position : Vector3.zero);

            return raw + new Vector3(paddingMaxX, 0f, paddingMaxZ);
        }
    }

    /// <summary>True si hay esquinas utilizables (automáticas ya calculadas, o manuales asignadas). GuideMapCameraRig y GuideMapView lo usan para avisar con claridad si falta configurar algo.</summary>
    public bool IsConfigured => (autoComputeFromGeometry && autoBoundsComputed) || (worldMinCorner != null && worldMaxCorner != null);

    private void Awake()
    {
        Current = this;

        if (autoComputeFromGeometry)
        {
            ComputeBoundsFromGeometry();
        }
    }

    /// <summary>
    /// Escanea TODOS los Renderer de la escena, se queda solo con los que
    /// están en geometryLayer, y calcula el bounding box combinado real —
    /// nada de Transforms colocados a mano que puedan quedar "casi" bien.
    /// </summary>
    private void ComputeBoundsFromGeometry()
    {
        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        bool any = false;
        Bounds combined = default;

        foreach (var candidate in renderers)
        {
            if (((1 << candidate.gameObject.layer) & geometryLayer.value) == 0)
            {
                continue;
            }

            Bounds b = candidate.bounds;

            if (!any)
            {
                combined = b;
                any = true;
                continue;
            }

            combined.Encapsulate(b);
        }

        if (!any)
        {
            Debug.LogWarning("[MapBounds] Auto Compute From Geometry está activo pero no se encontró ningún Renderer en Geometry Layer — revisa que el layer esté bien elegido. Si hay Transforms manuales asignados, se usarán como respaldo.");
            autoBoundsComputed = false;
            return;
        }

        autoMin = combined.min;
        autoMax = combined.max;
        autoBoundsComputed = true;
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// Convierte una posición del mundo (usa X y Z; ignora la altura Y) a
    /// coordenadas normalizadas 0-1 dentro del rectángulo del mapa.
    /// Puede devolver valores fuera de [0,1] si la posición cae fuera de los
    /// límites definidos — quien consuma el resultado decide si recortarlo.
    /// </summary>
    public Vector2 WorldToMapUV(Vector3 worldPosition)
    {
        if (!IsConfigured)
        {
            return Vector2.zero;
        }

        // IMPORTANTE: usa WorldMin/WorldMax (con padding ya aplicado), no las
        // posiciones crudas de los Transform — así coincide exactamente con
        // el rectángulo que GuideMapCameraRig usa para encuadrar la cámara.
        Vector3 min = WorldMin;
        Vector3 max = WorldMax;

        float u = Mathf.InverseLerp(min.x, max.x, worldPosition.x);
        float v = Mathf.InverseLerp(min.z, max.z, worldPosition.z);

        if (invertHorizontal)
        {
            u = 1f - u;
        }

        if (invertVertical)
        {
            v = 1f - v;
        }

        return new Vector2(u, v);
    }
}
