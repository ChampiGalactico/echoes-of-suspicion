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
    [SerializeField, Tooltip("Punto del mundo que corresponde a la esquina inferior-izquierda de la imagen del mapa.")]
    private Transform worldMinCorner;

    [SerializeField, Tooltip("Punto del mundo que corresponde a la esquina superior-derecha de la imagen del mapa.")]
    private Transform worldMaxCorner;

    /// <summary>Instancia activa del bioma actual. Se sobreescribe si se carga otro bioma con su propio MapBounds.</summary>
    public static MapBounds Current { get; private set; }

    /// <summary>Esquina inferior-izquierda del mundo, en coordenadas de mundo. Usado también por GuideMapCameraRig para encuadrar la cámara cenital.</summary>
    public Vector3 WorldMin => worldMinCorner != null ? worldMinCorner.position : Vector3.zero;

    /// <summary>Esquina superior-derecha del mundo, en coordenadas de mundo. Usado también por GuideMapCameraRig para encuadrar la cámara cenital.</summary>
    public Vector3 WorldMax => worldMaxCorner != null ? worldMaxCorner.position : Vector3.zero;

    /// <summary>True si ambas esquinas están asignadas. GuideMapCameraRig y GuideMapView lo usan para avisar con claridad si falta configurar algo, en vez de fallar en silencio con Vector3.zero.</summary>
    public bool IsConfigured => worldMinCorner != null && worldMaxCorner != null;

    private void Awake()
    {
        Current = this;
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
        if (worldMinCorner == null || worldMaxCorner == null)
        {
            return Vector2.zero;
        }

        Vector3 min = worldMinCorner.position;
        Vector3 max = worldMaxCorner.position;

        float u = Mathf.InverseLerp(min.x, max.x, worldPosition.x);
        float v = Mathf.InverseLerp(min.z, max.z, worldPosition.z);

        return new Vector2(u, v);
    }
}
