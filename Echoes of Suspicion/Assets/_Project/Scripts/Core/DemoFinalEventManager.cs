using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Orchestrates the final event of the demo after Puzzle 3 is solved:
///
/// 1. Heartbeat goes to panic mode.
/// 2. Doors open simultaneously — blue door for Runner, room door for Guide.
/// 3. A JumpscareCreature spawns in each doorway.
/// 4. Each creature walks 2 steps (with footstep sounds), then lunges.
/// 5. Screen fades to black on all clients.
/// 6. "Continuará..." text appears.
///
/// Uses JumpscareCreature (lightweight model + Animator) instead of the
/// full CreatureController prefab — no AI, no NavMesh, just a scripted
/// animation sequence.
///
/// SETUP:
/// - Place on a persistent GameObject in the biome scene.
/// - Assign all references in the Inspector.
/// - The finalPuzzle's OnPuzzleSolved UnityEvent should call TriggerFinalEvent().
///   Alternatively, DemoProgressionManager calls it.
/// - Create a JumpscareCreature prefab: creature model FBX child with
///   Animator + JumpscareCreature + NetworkIdentity + AudioSource on root.
///   Register it in NetworkManager's spawnable prefabs.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class DemoFinalEventManager : NetworkBehaviour
{
    [Header("Puzzle Trigger")]

    [SerializeField, Tooltip("The final puzzle. When solved, the event starts.")]
    private EOS.Puzzles.Puzzle finalPuzzle;

    [Header("Runner Jumpscare")]

    [SerializeField, Tooltip("The blue exit door (Runner side).")]
    private InteractableDoor runnerDoor;

    [SerializeField, Tooltip("Spawn point in the Runner's door frame.")]
    private Transform runnerCreatureSpawn;

    [Header("Guide Jumpscare")]

    [SerializeField, Tooltip("A door in the Guide's room.")]
    private InteractableDoor guideDoor;

    [SerializeField, Tooltip("Spawn point in the Guide's door frame.")]
    private Transform guideCreatureSpawn;

    [Header("Creature")]

    [SerializeField, Tooltip("JumpscareCreature prefab (model + Animator, no AI). " +
                             "Must have JumpscareCreature + NetworkIdentity.")]
    private GameObject jumpscareCreaturePrefab;

    [Header("Heartbeat")]

    [SerializeField, Tooltip("DemoHeartbeatManager to put into panic mode.")]
    private EOS.Puzzles.DemoHeartbeatManager heartbeatManager;

    [Header("Timing")]

    [SerializeField, Min(0f)]
    private float delayBeforeDoorOpens = 0.5f;

    [SerializeField, Min(0f)]
    private float delayBeforeCreature = 1.0f;

    [SerializeField, Min(0f), Tooltip("Time for creature to walk + lunge before fade.")]
    private float creatureSequenceDuration = 2.5f;

    [SerializeField, Min(0.1f)]
    private float fadeDuration = 0.5f;

    [SerializeField, Min(0f)]
    private float continueTextDuration = 4.0f;

    [Header("UI (auto-created if null)")]

    [SerializeField, Tooltip("Full-screen black panel for fade. Auto-created on Canvas if null.")]
    private CanvasGroup fadePanel;

    [SerializeField, Tooltip("'Continuará...' text. Auto-created if null.")]
    private TMP_Text continueText;

    // ── State ────────────────────────────────────────────

    [SyncVar]
    private bool _triggered;

    private GameObject _runnerCreatureObj;
    private GameObject _guideCreatureObj;

    // ── Lifecycle ────────────────────────────────────────

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (finalPuzzle != null)
            finalPuzzle.OnPuzzleSolved.AddListener(TriggerFinalEvent);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureUIExists();

        if (fadePanel != null)
        {
            fadePanel.alpha = 0f;
            fadePanel.gameObject.SetActive(false);
        }
    }

    // ── Public API ───────────────────────────────────────

    /// <summary>
    /// Start the final event sequence. Called by puzzle or DemoProgressionManager.
    /// </summary>
    [Server]
    public void TriggerFinalEvent()
    {
        if (_triggered) return;
        _triggered = true;

        Debug.Log("[DemoFinal] Final event triggered!");
        StartCoroutine(FinalEventSequence());
    }

    // ── Server sequence ──────────────────────────────────

    [Server]
    private IEnumerator FinalEventSequence()
    {
        // 1. Panic heartbeat.
        if (heartbeatManager != null)
            heartbeatManager.ActivatePanicMode();

        yield return new WaitForSeconds(delayBeforeDoorOpens);

        // 2. Open both doors simultaneously.
        if (runnerDoor != null)
            runnerDoor.OpenDoor();

        if (guideDoor != null)
            guideDoor.OpenDoor();

        // 2b. Freeze both players and rotate camera toward their creature spawn.
        var runner = PlayerUtils.FindPlayerByRole(PlayerRole.Runner);
        var guide = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);

        if (runner != null && runnerCreatureSpawn != null)
        {
            TargetFreezeAndLookAt(
                runner.connectionToClient,
                runnerCreatureSpawn.position);
        }

        if (guide != null && guideCreatureSpawn != null)
        {
            TargetFreezeAndLookAt(
                guide.connectionToClient,
                guideCreatureSpawn.position);
        }

        yield return new WaitForSeconds(delayBeforeCreature);

        // 3. Spawn creatures in both doorways.
        if (jumpscareCreaturePrefab != null)
        {
            // Runner's creature.
            if (runnerCreatureSpawn != null && runner != null)
            {
                _runnerCreatureObj = Instantiate(
                    jumpscareCreaturePrefab,
                    runnerCreatureSpawn.position,
                    runnerCreatureSpawn.rotation);

                NetworkServer.Spawn(_runnerCreatureObj);

                var jumpscare = _runnerCreatureObj.GetComponent<JumpscareCreature>();
                if (jumpscare != null)
                    jumpscare.Execute(runner.transform.position);
            }

            // Guide's creature.
            if (guideCreatureSpawn != null && guide != null)
            {
                _guideCreatureObj = Instantiate(
                    jumpscareCreaturePrefab,
                    guideCreatureSpawn.position,
                    guideCreatureSpawn.rotation);

                NetworkServer.Spawn(_guideCreatureObj);

                var jumpscare = _guideCreatureObj.GetComponent<JumpscareCreature>();
                if (jumpscare != null)
                    jumpscare.Execute(guide.transform.position);
            }
        }

        // 4. Wait for creatures to walk + lunge.
        yield return new WaitForSeconds(creatureSequenceDuration);

        // 5. Fade to black on all clients.
        RpcFadeToBlack(fadeDuration);

        yield return new WaitForSeconds(fadeDuration + 0.2f);

        // 6. Show "Continuará..." on all clients.
        RpcShowContinueText();

        // 7. Stop heartbeat.
        if (heartbeatManager != null)
            heartbeatManager.StopHeartbeat();

        yield return new WaitForSeconds(continueTextDuration);

        // 8. Clean up — destroy both jumpscare creatures.
        if (_runnerCreatureObj != null)
            NetworkServer.Destroy(_runnerCreatureObj);
        if (_guideCreatureObj != null)
            NetworkServer.Destroy(_guideCreatureObj);

        // Could load main menu here, or just stay on the black screen.
        // EOSNetworkManager.singleton.ServerChangeScene("MainMenu");
    }

    // ── Client RPCs ──────────────────────────────────────

    /// <summary>
    /// Freeze a specific player and rotate their camera to face the creature spawn.
    /// Uses TargetRpc so each player looks at THEIR own creature's position.
    /// </summary>
    [TargetRpc]
    private void TargetFreezeAndLookAt(
        NetworkConnectionToClient target, Vector3 lookAtPosition)
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) return;

        // ── Rotate body (yaw) to face the creature ──
        Vector3 direction = lookAtPosition - localPlayer.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            localPlayer.transform.rotation = Quaternion.LookRotation(direction);

        // ── Rotate viewPivot (pitch) toward creature height ──
        // viewPivot is the camera pivot child — find it via the player camera.
        var fpView = localPlayer.GetComponent<NetworkFirstPersonView>();
        if (fpView != null)
        {
            Camera cam = fpView.GetComponentInChildren<Camera>();
            Transform viewPivot = cam != null ? cam.transform.parent : null;

            if (viewPivot != null && viewPivot != localPlayer.transform)
            {
                Vector3 toCreature = lookAtPosition - cam.transform.position;
                float horizontalDist = new Vector2(toCreature.x, toCreature.z).magnitude;
                float pitchAngle = Mathf.Atan2(toCreature.y, horizontalDist) * Mathf.Rad2Deg;

                // Clamp to reasonable range (same as NetworkFirstPersonView limits).
                pitchAngle = Mathf.Clamp(pitchAngle, -85f, 85f);
                viewPivot.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);
            }

            fpView.enabled = false;
        }

        // ── Freeze movement and interaction ──
        var mov = localPlayer.GetComponent<NetworkPlayerMovement>();
        var inter = localPlayer.GetComponent<NetworkRatInteractor>();

        if (mov != null) mov.enabled = false;
        if (inter != null) inter.enabled = false;
    }

    [ClientRpc]
    private void RpcFadeToBlack(float duration)
    {
        StartCoroutine(ClientFadeToBlack(duration));
    }

    [ClientRpc]
    private void RpcShowContinueText()
    {
        if (continueText != null)
        {
            continueText.gameObject.SetActive(true);
            continueText.alpha = 0f;
            StartCoroutine(FadeInText(continueText, 1.0f));
        }
    }

    // ── Client animations ────────────────────────────────

    private IEnumerator ClientFadeToBlack(float duration)
    {
        EnsureUIExists();

        if (fadePanel == null) yield break;

        fadePanel.gameObject.SetActive(true);
        fadePanel.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadePanel.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        fadePanel.alpha = 1f;
    }

    private IEnumerator FadeInText(TMP_Text text, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            text.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        text.alpha = 1f;
    }

    // ── UI setup ─────────────────────────────────────────

    private void EnsureUIExists()
    {
        if (fadePanel != null && continueText != null) return;

        // Find or create a Canvas.
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DemoFinalCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // On top of everything.
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Fade panel.
        if (fadePanel == null)
        {
            GameObject panelObj = new GameObject("FadePanel");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = panelObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = panelObj.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;

            fadePanel = panelObj.AddComponent<CanvasGroup>();
            fadePanel.alpha = 0f;
            panelObj.SetActive(false);
        }

        // Continue text.
        if (continueText == null)
        {
            GameObject textObj = new GameObject("ContinueText");
            textObj.transform.SetParent(fadePanel.transform, false);

            RectTransform rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 100f);

            continueText = textObj.AddComponent<TextMeshProUGUI>();
            continueText.text = "Continuará...";
            continueText.fontSize = 48f;
            continueText.alignment = TextAlignmentOptions.Center;
            continueText.color = Color.white;
            continueText.alpha = 0f;

            textObj.SetActive(false);
        }
    }
}
