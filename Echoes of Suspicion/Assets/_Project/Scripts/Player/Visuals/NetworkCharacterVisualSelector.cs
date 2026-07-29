using Mirror;
using UnityEngine;

/// <summary>
/// Activa el modelo y el conjunto visual correspondiente
/// al personaje asignado por CharacterStatsProvider.
///
/// Índice 0: Carmen.
/// Índice 1: Carlos.
/// </summary>
[DisallowMultipleComponent]
public sealed class NetworkCharacterVisualSelector :
    NetworkBehaviour
{
    private const int CarmenIndex = 0;
    private const int CarlosIndex = 1;

    [Header("Player")]
    [SerializeField]
    private CharacterStatsProvider statsProvider;

    [SerializeField]
    private NetworkRatAnimatorDriver animatorDriver;

    [Header("Third Person")]
    [SerializeField]
    private GameObject carmenThirdPersonVisual;

    [SerializeField]
    private Animator carmenAnimator;

    [SerializeField]
    private GameObject carlosThirdPersonVisual;

    [SerializeField]
    private Animator carlosAnimator;

    [Header("First Person Parts")]
    [Tooltip(
        "Objetos FPS de Carmen. Normalmente un brazo " +
        "izquierdo y uno derecho.")]
    [SerializeField]
    private GameObject[] carmenFirstPersonParts;

    [Tooltip(
        "Objetos FPS de Carlos. Puede quedar vacío " +
        "hasta crear sus brazos.")]
    [SerializeField]
    private GameObject[] carlosFirstPersonParts;

    private int appliedCharacterIndex =
        int.MinValue;

    private void Awake()
    {
        if (statsProvider == null)
        {
            statsProvider =
                GetComponent<CharacterStatsProvider>();
        }

        if (animatorDriver == null)
        {
            animatorDriver =
                GetComponent<NetworkRatAnimatorDriver>();
        }

        SetCharacterObjectsActive(
            CarmenIndex,
            false);

        SetCharacterObjectsActive(
            CarlosIndex,
            false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        TryRefreshCharacter();
    }

    private void Update()
    {
        if (!isClient || statsProvider == null)
        {
            return;
        }

        TryRefreshCharacter();
    }

    private void TryRefreshCharacter()
    {
        int characterIndex =
            statsProvider.SelectedCharacterIndex;

        if (characterIndex < 0 ||
            characterIndex ==
            appliedCharacterIndex)
        {
            return;
        }

        ApplyCharacter(characterIndex);
    }

    private void ApplyCharacter(
        int characterIndex)
    {
        bool useCarmen =
            characterIndex == CarmenIndex;

        bool useCarlos =
            characterIndex == CarlosIndex;

        if (!useCarmen && !useCarlos)
        {
            Debug.LogWarning(
                $"[CharacterVisualSelector] " +
                $"Índice inválido: {characterIndex}.",
                this);

            return;
        }

        SetCharacterObjectsActive(
            CarmenIndex,
            useCarmen);

        SetCharacterObjectsActive(
            CarlosIndex,
            useCarlos);

        Animator selectedAnimator =
            useCarmen
                ? carmenAnimator
                : carlosAnimator;

        if (animatorDriver != null)
        {
            animatorDriver.SetAnimator(
                selectedAnimator);
        }

        appliedCharacterIndex =
            characterIndex;

        Debug.Log(
            $"[CharacterVisualSelector] " +
            $"Personaje visual activo: " +
            $"{(useCarmen ? "Carmen" : "Carlos")}.",
            this);
    }

    private void SetCharacterObjectsActive(
        int characterIndex,
        bool isActive)
    {
        if (characterIndex == CarmenIndex)
        {
            SetActive(
                carmenThirdPersonVisual,
                isActive);

            SetPartsActive(
                carmenFirstPersonParts,
                isActive);

            return;
        }

        SetActive(
            carlosThirdPersonVisual,
            isActive);

        SetPartsActive(
            carlosFirstPersonParts,
            isActive);
    }

    private static void SetPartsActive(
        GameObject[] parts,
        bool isActive)
    {
        if (parts == null)
        {
            return;
        }

        foreach (GameObject part in parts)
        {
            SetActive(
                part,
                isActive);
        }
    }

    private static void SetActive(
        GameObject target,
        bool isActive)
    {
        if (target != null &&
            target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }
}