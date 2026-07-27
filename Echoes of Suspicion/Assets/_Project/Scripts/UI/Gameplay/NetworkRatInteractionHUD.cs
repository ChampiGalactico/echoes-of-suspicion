using Mirror;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NetworkRatInteractionHUD : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private NetworkRatInteractor interactor;

    [SerializeField]
    private NetworkInventory inventory;

    [SerializeField]
    private Canvas hudCanvas;

    [SerializeField]
    private CanvasGroup hudCanvasGroup;

    [SerializeField]
    private TMP_Text crosshairText;

    [SerializeField]
    private TMP_Text promptText;

    [Header("Crosshair")]
    [SerializeField]
    private string idleCrosshair = "•";

    [SerializeField]
    private string targetCrosshair = "+";

    private void Awake()
    {
        if (interactor == null)
        {
            interactor =
                GetComponent<NetworkRatInteractor>();
        }

        if (inventory == null)
        {
            inventory =
                GetComponent<NetworkInventory>();
        }

        if (hudCanvasGroup == null &&
            hudCanvas != null)
        {
            hudCanvasGroup =
                hudCanvas.GetComponent<CanvasGroup>();
        }

        SetHudVisible(false);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        SetHudVisible(false);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SetHudVisible(true);
        RefreshHud();
    }

    public override void OnStopLocalPlayer()
    {
        SetHudVisible(false);
        base.OnStopLocalPlayer();
    }

    private void Update()
    {
        if (!isLocalPlayer ||
            interactor == null)
        {
            return;
        }

        RefreshHud();
    }

    private void RefreshHud()
    {
        if (interactor.HasInteractionTarget)
        {
            SetCrosshair(true);

            SetPrompt(
                $"[E] {interactor.CurrentInteractionPrompt}");

            return;
        }

        // Show drop/throw hint if holding an item in active slot.
        if (interactor.HasActiveItem)
        {
            SetCrosshair(false);
            SetPrompt("[Clic] Lanzar   ·   [G] Soltar");
            return;
        }

        SetCrosshair(false);
        HidePrompt();
    }

    private void SetCrosshair(bool hasTarget)
    {
        if (crosshairText == null)
        {
            return;
        }

        crosshairText.text =
            hasTarget
                ? targetCrosshair
                : idleCrosshair;

        crosshairText.gameObject.SetActive(true);
    }

    private void SetPrompt(string message)
    {
        if (promptText == null)
        {
            return;
        }

        promptText.text = message;
        promptText.gameObject.SetActive(true);
    }

    private void HidePrompt()
    {
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
    }

    private void SetHudVisible(bool isVisible)
    {
        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha =
                isVisible ? 1f : 0f;

            hudCanvasGroup.interactable = false;
            hudCanvasGroup.blocksRaycasts = false;
        }

        if (hudCanvas != null)
        {
            hudCanvas.enabled = true;
        }
    }
}
