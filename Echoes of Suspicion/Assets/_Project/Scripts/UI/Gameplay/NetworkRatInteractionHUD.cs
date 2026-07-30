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

    [Header("Denied Feedback")]
    [SerializeField]
    private TMP_Text deniedFeedbackText;

    [SerializeField]
    private float deniedFeedbackDuration = 2f;

    private float deniedFeedbackTimer;

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

        InteractableDoor.OnLocalDeniedFeedback += ShowDeniedFeedback;
    }

    public override void OnStopLocalPlayer()
    {
        InteractableDoor.OnLocalDeniedFeedback -= ShowDeniedFeedback;

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

        UpdateDeniedFeedback();
        RefreshHud();
    }

    private void RefreshHud()
    {
        if (interactor.HasInteractionTarget)
        {
            SetCrosshair(true);

            string prompt = ResolvePromptTokens(
                interactor.CurrentInteractionPrompt);

            if (interactor.IsCurrentTargetInteractable)
                SetPrompt($"[E] {prompt}");
            else
                SetPrompt(prompt);

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

    /// <summary>
    /// Reemplaza tokens especiales en el prompt:
    ///   {item} → DisplayName del item activo (o itemName si no tiene PuzzleItemData)
    /// </summary>
    private string ResolvePromptTokens(string raw)
    {
        if (!raw.Contains("{item}")) return raw;

        string itemName = GetActiveItemDisplayName();
        return raw.Replace("{item}", itemName ?? "???");
    }

    private string GetActiveItemDisplayName()
    {
        if (inventory == null) return null;

        InventorySlot slot = inventory.ActiveSlot;
        if (slot.IsEmpty) return null;

        // Intentar PuzzleItemData.DisplayName primero.
        if (slot.itemNetId != 0 &&
            NetworkClient.spawned.TryGetValue(slot.itemNetId, out NetworkIdentity identity))
        {
            var pickable = identity.GetComponent<EOS.Puzzles.PickableItem>();
            if (pickable != null &&
                pickable.PuzzleData != null &&
                !string.IsNullOrEmpty(pickable.PuzzleData.DisplayName))
            {
                return pickable.PuzzleData.DisplayName;
            }
        }

        // Fallback: ItemData.itemName del registro.
        ItemData data = inventory.ActiveItemData;
        return data != null ? data.itemName : null;
    }

    private void ShowDeniedFeedback(string message)
    {
        if (deniedFeedbackText == null)
        {
            // Fallback: mostrar en el promptText si no hay
            // un texto dedicado para feedback.
            SetPrompt(message);
            deniedFeedbackTimer = deniedFeedbackDuration;
            return;
        }

        deniedFeedbackText.text = message;
        deniedFeedbackText.gameObject.SetActive(true);
        deniedFeedbackTimer = deniedFeedbackDuration;
    }

    private void UpdateDeniedFeedback()
    {
        if (deniedFeedbackTimer <= 0f)
            return;

        deniedFeedbackTimer -= Time.deltaTime;

        if (deniedFeedbackTimer <= 0f)
        {
            if (deniedFeedbackText != null)
                deniedFeedbackText.gameObject.SetActive(false);
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
