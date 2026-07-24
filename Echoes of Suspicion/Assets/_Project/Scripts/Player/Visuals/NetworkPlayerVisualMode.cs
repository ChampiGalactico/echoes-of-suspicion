using Mirror;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class NetworkPlayerVisualMode : NetworkBehaviour
{
    [Header("Visual Roots")]
    [SerializeField]
    private GameObject firstPersonRig;

    [SerializeField]
    private GameObject thirdPersonModel;

    private Renderer[] firstPersonRenderers;
    private Renderer[] thirdPersonRenderers;
    private ShadowCastingMode[] originalThirdPersonShadowModes;

    private void Awake()
    {
        CacheRenderers();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyRemoteVisuals();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ApplyLocalVisuals();
    }

    private void CacheRenderers()
    {
        firstPersonRenderers =
            firstPersonRig != null
                ? firstPersonRig.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();

        thirdPersonRenderers =
            thirdPersonModel != null
                ? thirdPersonModel.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();

        originalThirdPersonShadowModes =
            new ShadowCastingMode[thirdPersonRenderers.Length];

        for (int i = 0; i < thirdPersonRenderers.Length; i++)
        {
            originalThirdPersonShadowModes[i] =
                thirdPersonRenderers[i].shadowCastingMode;
        }
    }

    private void ApplyLocalVisuals()
    {
        // Las patas de primera persona deben verse.
        SetActiveSafely(firstPersonRig, true);

        // El modelo completo debe permanecer activo para poder proyectar sombra.
        SetActiveSafely(thirdPersonModel, true);

        // Evita una segunda sombra generada por los brazos de primera persona.
        SetShadowMode(
            firstPersonRenderers,
            ShadowCastingMode.Off);

        // El cuerpo completo no se ve, pero proyecta la sombra de la rata.
        SetShadowMode(
            thirdPersonRenderers,
            ShadowCastingMode.ShadowsOnly);
    }

    private void ApplyRemoteVisuals()
    {
        // Los demás jugadores no deben ver las patas flotantes
        // que pertenecen a la cámara de primera persona.
        SetActiveSafely(firstPersonRig, false);

        // El modelo completo sí debe verse para jugadores remotos.
        SetActiveSafely(thirdPersonModel, true);

        RestoreThirdPersonShadowModes();
    }

    private void RestoreThirdPersonShadowModes()
    {
        int count = Mathf.Min(
            thirdPersonRenderers.Length,
            originalThirdPersonShadowModes.Length);

        for (int i = 0; i < count; i++)
        {
            Renderer targetRenderer = thirdPersonRenderers[i];

            if (targetRenderer != null)
            {
                targetRenderer.shadowCastingMode =
                    originalThirdPersonShadowModes[i];
            }
        }
    }

    private static void SetShadowMode(
        Renderer[] renderers,
        ShadowCastingMode shadowMode)
    {
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.shadowCastingMode = shadowMode;
            }
        }
    }

    private static void SetActiveSafely(
        GameObject target,
        bool isActive)
    {
        if (target != null && target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (firstPersonRig != null &&
            firstPersonRig == thirdPersonModel)
        {
            Debug.LogError(
                "First Person Rig y Third Person Model " +
                "no pueden apuntar al mismo GameObject.",
                this);
        }
    }
#endif
}