using UnityEngine;

/// <summary>
/// Configura automáticamente una cámara cenital (top-down) que genera el
/// mapa esquemático del Guía a partir de la geometría REAL del bioma, en vez
/// de depender de una imagen dibujada a mano.
///
/// Usa exactamente las mismas dos esquinas que ya definiste en MapBounds
/// (misma fuente de verdad) para encuadrarse — así lo que ve esta cámara
/// coincide siempre con el rectángulo donde GuideMapView posiciona los
/// íconos del Corredor y las criaturas. Si mueven las esquinas de MapBounds,
/// esta cámara se reencuadra sola al reiniciar la escena, sin tocar nada más.
///
/// Requiere en el Editor (una sola vez por bioma, configuración, no arte):
/// 1. Un Layer dedicado (ej. "MapGeometry") asignado SOLO a las paredes y
///    puertas que quieras ver en el mapa (nunca al piso, props, personajes
///    o criaturas — esos se dibujan aparte como íconos 2D). Este layer se
///    configura UNA vez en el campo Geometry Layer de MapBounds — esta
///    cámara lo lee de ahí, no tiene su propio campo duplicado.
/// 2. Esa geometría debe verse blanca. Dos formas, de más simple a más
///    "automática":
///    a) Asignarle a esas paredes un material Unlit blanco compartido
///       (selección múltiple + drag del material, un solo paso).
///    b) (Más prolijo, URP) Crear un Renderer URP dedicado con un "Render
///       Objects" Renderer Feature que fuerza un material Unlit blanco SOLO
///       para el layer "MapGeometry", y asignar ese Renderer a esta cámara
///       (Camera → Rendering → Renderer). Así ni siquiera hace falta tocar
///       los materiales originales de las paredes.
/// 3. Una Render Texture (Assets → Create → Render Texture) asignada al
///    campo Target Texture de esta cámara, y esa misma Render Texture
///    puesta en un RawImage dentro del Canvas del monitor del Guía (en el
///    lugar donde antes iba la imagen de fondo dibujada a mano).
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class GuideMapCameraRig : MonoBehaviour
{
    [SerializeField, Tooltip("MapBounds del bioma a encuadrar. Si se deja vacío, usa MapBounds.Current en Start() (el que esté activo en la escena cargada).")]
    private MapBounds bounds;

    [SerializeField, Min(1f), Tooltip("Altura sobre el punto más alto del bioma desde la que mira la cámara hacia abajo. Ajusta si alguna pared muy alta queda cortada por el Far Clip Plane.")]
    private float heightAboveScene = 30f;

    [Header("Debug")]
    [SerializeField, Tooltip("Deja en false salvo que estés depurando el encuadre de la cámara del mapa.")]
    private bool showDebugLogs = false;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        var activeBounds = bounds != null ? bounds : MapBounds.Current;

        if (activeBounds == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("[GuideMapCameraRig] No hay ningún MapBounds asignado ni activo (MapBounds.Current) — no se puede encuadrar la cámara del mapa.");
            }
            return;
        }

        if (!activeBounds.IsConfigured)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning("[GuideMapCameraRig] El MapBounds encontrado no tiene sus dos esquinas (World Min/Max Corner) asignadas.");
            }
            return;
        }

        ConfigureCamera(activeBounds);
    }

    private void ConfigureCamera(MapBounds activeBounds)
    {
        // WorldMin/WorldMax ya incluyen el padding definido en MapBounds —
        // NO se le agrega padding propio acá, para garantizar que este
        // encuadre sea idéntico, byte a byte, al que usa WorldToMapUV para
        // posicionar los íconos. Si cada uno calculara su propio margen por
        // separado, cualquier diferencia futura entre ambos volvería a
        // desalinear el ícono del Corredor respecto a las paredes del mapa.
        Vector3 min = activeBounds.WorldMin;
        Vector3 max = activeBounds.WorldMax;

        float worldWidth = Mathf.Abs(max.x - min.x);
        float worldDepth = Mathf.Abs(max.z - min.z);

        if (worldWidth <= 0f || worldDepth <= 0f)
        {
            Debug.LogWarning("[GuideMapCameraRig] El rectángulo de MapBounds tiene ancho o profundidad 0 — revisa que World Min Corner y World Max Corner sean puntos distintos.");
            return;
        }

        Vector3 center = new Vector3((min.x + max.x) * 0.5f, min.y + heightAboveScene, (min.z + max.z) * 0.5f);

        transform.position = center;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // mirando estrictamente hacia abajo (-Y)

        cam.orthographic = true;
        // orthographicSize es la MITAD de la altura vertical visible — por
        // eso se divide worldDepth entre 2 (el ancho lo controla el aspect).
        cam.orthographicSize = worldDepth * 0.5f;
        cam.aspect = worldWidth / worldDepth;
        // El layer viene de MapBounds (única fuente de verdad) — así el
        // mismo layer que se usa para CALCULAR el bounding box automático
        // (ComputeBoundsFromGeometry) es exactamente el que la cámara
        // renderiza. Ya no hay un campo separado acá que pueda quedar
        // desincronizado del de MapBounds.
        cam.cullingMask = activeBounds.GeometryLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        // Alpha 0, no negro sólido: así el "vacío" del mapa (donde no hay
        // paredes) queda REALMENTE transparente en la Render Texture, y deja
        // ver la textura/material real del monitor debajo. Las paredes (que
        // sí se dibujan, opacas) tapan igual esa transparencia donde
        // corresponde. Requiere que la Render Texture tenga canal alpha
        // (el formato "Default" ya lo trae) y que el RawImage use un shader
        // que respete alpha (el UI/Default de Unity ya lo hace).
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = heightAboveScene + 50f;

        // CRÍTICO: esta cámara mira el nivel desde un ángulo/altura que el
        // Occlusion Culling horneado del proyecto NUNCA contempló (está
        // pensado para cámaras a altura de jugador moviéndose por el suelo).
        // Sin desactivar esto, Unity puede ocultar la mayoría del laberinto
        // basándose en datos de visibilidad que no aplican aquí, dejando
        // visible solo la celda/habitación desde la que "cree" que se mira.
        cam.useOcclusionCulling = false;

        if (showDebugLogs)
        {
            Debug.Log($"[GuideMapCameraRig] Cámara encuadrada: centro={center}, orthographicSize={cam.orthographicSize:F1}, aspect={cam.aspect:F2}, targetTexture={(cam.targetTexture != null ? cam.targetTexture.name : "NINGUNA (falta asignarla)")}.");
        }
    }
}
