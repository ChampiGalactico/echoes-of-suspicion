using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Orchestrates the final event of the demo after the last puzzle is solved.
///
/// CLIENT-LOCAL creature spawn: each client instantiates and drives its own
/// creature independently. No NetworkServer.Spawn — avoids sync lag and
/// floor-clipping issues. Both players see their own creature.
///
/// Sequence:
/// 1. Heartbeat → panic mode.
/// 2. Doors open (Runner + Guide).
/// 3. Players frozen.
/// 4. Chase screen effect activates.
/// 5. Each client: spawns creature locally → camera lerps → creature walks + lunges.
/// 6. Fade to black → "Continuará..."
///
/// SETUP:
/// - Place on a persistent GameObject in the biome scene.
/// - Assign all references in the Inspector.
/// - DemoProgressionManager calls TriggerFinalEvent() when all puzzles complete.
/// - jumpscareCreaturePrefab: creature model FBX child with Animator.
///   Animator needs "StateIndex" (int): 0=walk, 2=chase.
///   Does NOT need NetworkIdentity for this system.
///
/// DEBUG: Toggle debugTestOnStart to auto-trigger the jumpscare 2 seconds
///        after the server starts. Useful for testing without playing puzzles.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class DemoFinalEventManager : NetworkBehaviour
{
    [Header("Debug Test")]

    [SerializeField, Tooltip("If true, triggers the jumpscare 2s after server starts. " +
                             "For testing only — disable before shipping!")]
    private bool debugTestOnStart = false;

    [Header("Puzzle Trigger")]

    [SerializeField, Tooltip("Optional: final puzzle node (IPuzzleNode). " +
                             "Not needed if DemoProgressionManager calls TriggerFinalEvent().")]
    private MonoBehaviour finalPuzzleNode;

    [Header("Runner Jumpscare")]

    [SerializeField, Tooltip("The blue exit door (Runner side).")]
    private InteractableDoor runnerDoor;

    [SerializeField, Tooltip("Spawn point in front of the Runner's door.")]
    private Transform runnerCreatureSpawn;

    [Header("Guide Jumpscare")]

    [SerializeField, Tooltip("A door in the Guide's room.")]
    private InteractableDoor guideDoor;

    [SerializeField, Tooltip("Spawn point in front of the Guide's door.")]
    private Transform guideCreatureSpawn;

    [Header("Creature")]

    [SerializeField, Tooltip("Creature prefab (model child + Animator). " +
                             "Spawned locally on each client — no NetworkIdentity needed.")]
    private GameObject jumpscareCreaturePrefab;

    [Header("Creature Movement")]

    [SerializeField, Tooltip("Walk speed during approach steps.")]
    private float creatureWalkSpeed = 2.5f;

    [SerializeField, Tooltip("Lunge speed toward player.")]
    private float creatureLungeSpeed = 12f;

    [SerializeField, Tooltip("Number of walk steps before lunge.")]
    private int creatureStepCount = 3;

    [SerializeField, Tooltip("Distance per step (meters).")]
    private float creatureStepDistance = 0.9f;

    [SerializeField, Tooltip("Pause between steps (seconds).")]
    private float creatureStepPause = 0.2f;

    [SerializeField, Tooltip("Y rotation offset for the creature model. " +
                             "If the model faces sideways, set to 90 or -90 or 180.")]
    private float creatureModelYOffset = 0f;

    [Header("Creature Audio")]

    [SerializeField, Tooltip("Footstep sound. Played once per step.")]
    private AudioClip creatureFootstepClip;

    [SerializeField, Range(0f, 1f)]
    private float creatureFootstepVolume = 0.9f;

    [SerializeField, Tooltip("Scream/growl when the creature lunges.")]
    private AudioClip creatureLungeClip;

    [SerializeField, Range(0f, 1f)]
    private float creatureLungeVolume = 1f;

    [Header("Jumpscare Music")]

    [SerializeField, Tooltip("Background music/ambience that plays when the creature appears " +
                             "and continues through the fade and Continuará screen.")]
    private AudioClip jumpscareMusic;

    [SerializeField, Range(0f, 1f)]
    private float jumpscareMusicVolume = 0.8f;

    [SerializeField, Tooltip("Fade in duration for the music (seconds).")]
    private float jumpscareMusicFadeIn = 0.3f;

    [Header("Heartbeat")]

    [SerializeField, Tooltip("DemoHeartbeatManager to put into panic mode.")]
    private EOS.Puzzles.DemoHeartbeatManager heartbeatManager;

    [Header("Alert Screen Effect (black flicker)")]

    [SerializeField, Range(0f, 1f), Tooltip("Max intensity of the black flicker.")]
    private float alertFlickerIntensity = 0.85f;

    [SerializeField, Min(1f), Tooltip("Speed of the flicker (higher = faster).")]
    private float alertFlickerSpeed = 8f;

    [Header("Timing")]

    [SerializeField, Min(0f)]
    private float delayBeforeDoorOpens = 0.5f;

    [SerializeField, Min(0f)]
    private float doorOpenDuration = 1.0f;

    [SerializeField, Min(0.1f)]
    private float cameraLookDuration = 0.6f;

    [SerializeField, Min(0.1f)]
    private float fadeDuration = 0.4f;

    [SerializeField, Min(0f)]
    private float continueTextDuration = 4.0f;

    [Header("UI (auto-created if null)")]

    [SerializeField]
    private CanvasGroup fadePanel;

    [SerializeField]
    private TMP_Text continueText;

    [Header("Glitch")]

    [SerializeField] private float glitchMinInterval = 0.08f;
    [SerializeField] private float glitchMaxInterval = 0.4f;
    [SerializeField] private float glitchMaxOffset = 6f;
    [SerializeField] private float glitchMinAlpha = 0.3f;

    // ── Shader property IDs (match ScreenEffectsController) ──

    private static readonly int DetectColorId =
        Shader.PropertyToID("_ScreenFX_DetectColor");
    private static readonly int DetectAmountId =
        Shader.PropertyToID("_ScreenFX_DetectAmount");

    // ── Animator hashes ─────────────────────────────────

    private static readonly int StateIndexHash = Animator.StringToHash("StateIndex");
    private const int WALK_STATE = 0;
    private const int CHASE_STATE = 2;

    // ── State ────────────────────────────────────────────

    [SyncVar]
    private bool _triggered;

    private GameObject _localCreature;
    private Coroutine _chaseShaderRoutine;
    private AudioSource _musicSource;

    // ── Lifecycle ────────────────────────────────────────

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (finalPuzzleNode != null)
        {
            var node = finalPuzzleNode as EOS.Puzzles.IPuzzleNode;
            if (node != null)
                node.OnSolved += _ => TriggerFinalEvent();
            else
                Debug.LogWarning("[DemoFinal] finalPuzzleNode does not implement IPuzzleNode.", finalPuzzleNode);
        }

        if (debugTestOnStart)
        {
            Debug.LogWarning("[DemoFinal] DEBUG MODE — auto-triggering jumpscare in 10 seconds!");
            StartCoroutine(DebugAutoTrigger());
        }
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

    // ── Debug ────────────────────────────────────────────

    [Server]
    private IEnumerator DebugAutoTrigger()
    {
        yield return new WaitForSeconds(10f);
        TriggerFinalEvent();
    }

    // ── Public API ───────────────────────────────────────

    [Server]
    public void TriggerFinalEvent()
    {
        if (_triggered) return;
        _triggered = true;

        Debug.Log("[DemoFinal] Final event triggered!");
        StartCoroutine(ServerFinalSequence());
    }

    // ── Server: orchestration only ──────────────────────

    [Server]
    private IEnumerator ServerFinalSequence()
    {
        // 1. Panic heartbeat.
        if (heartbeatManager != null)
            heartbeatManager.ActivatePanicMode();

        yield return new WaitForSeconds(delayBeforeDoorOpens);

        // 2. Open doors.
        if (runnerDoor != null) runnerDoor.OpenDoor();
        if (guideDoor != null) guideDoor.OpenDoor();

        // 3. Send each player their jumpscare sequence (client-local).
        var runner = PlayerUtils.FindPlayerByRole(PlayerRole.Runner);
        var guide = PlayerUtils.FindPlayerByRole(PlayerRole.Guide);

        Vector3 runnerSpawnPos = runnerCreatureSpawn != null
            ? runnerCreatureSpawn.position : Vector3.zero;
        Quaternion runnerSpawnRot = runnerCreatureSpawn != null
            ? runnerCreatureSpawn.rotation : Quaternion.identity;

        Vector3 guideSpawnPos = guideCreatureSpawn != null
            ? guideCreatureSpawn.position : Vector3.zero;
        Quaternion guideSpawnRot = guideCreatureSpawn != null
            ? guideCreatureSpawn.rotation : Quaternion.identity;

        if (runner != null)
        {
            Debug.Log($"[DemoFinal] Sending jumpscare to Runner at spawn {runnerSpawnPos}");
            TargetRunJumpscare(runner.connectionToClient, runnerSpawnPos, runnerSpawnRot);
        }
        else
            Debug.LogWarning("[DemoFinal] No Runner found!");

        if (guide != null)
        {
            Debug.Log($"[DemoFinal] Sending jumpscare to Guide at spawn {guideSpawnPos}");
            TargetRunJumpscare(guide.connectionToClient, guideSpawnPos, guideSpawnRot);
        }
        else
            Debug.LogWarning("[DemoFinal] No Guide found!");

        // 4. Wait for full sequence to finish, then stop heartbeat.
        float totalDuration = doorOpenDuration + cameraLookDuration + 6f +
                              fadeDuration + 0.2f + continueTextDuration;
        yield return new WaitForSeconds(totalDuration);

        if (heartbeatManager != null)
            heartbeatManager.StopHeartbeat();
    }

    // ── Client: full jumpscare runs locally ──────────────

    [TargetRpc]
    private void TargetRunJumpscare(
        NetworkConnectionToClient target, Vector3 spawnPos, Quaternion spawnRot)
    {
        StartCoroutine(ClientFullJumpscare(spawnPos, spawnRot));
    }

    private IEnumerator ClientFullJumpscare(Vector3 spawnPos, Quaternion spawnRot)
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) yield break;

        Debug.Log($"[DemoFinal] Client jumpscare starting. Creature spawn: {spawnPos}, " +
                  $"Player pos: {localPlayer.transform.position}");

        // ── Freeze player ──
        FreezeLocalPlayer();

        // ── Activate alert screen effect ──
        _chaseShaderRoutine = StartCoroutine(DriveAlertShader());

        // ── Spawn creature HIDDEN (renderers off) ──
        if (jumpscareCreaturePrefab != null)
        {
            _localCreature = Instantiate(jumpscareCreaturePrefab, spawnPos, spawnRot);

            // Strip ALL NetworkBehaviours first (they depend on NetworkIdentity).
            foreach (var nb in _localCreature.GetComponents<NetworkBehaviour>())
                Destroy(nb);
            var ca = _localCreature.GetComponent<CreatureAnimator>();
            if (ca != null) Destroy(ca);
            var ni = _localCreature.GetComponent<NetworkIdentity>();
            if (ni != null) Destroy(ni);
            var nma = _localCreature.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nma != null)
            {
                nma.isStopped = true;
                nma.enabled = false;
                Destroy(nma);
            }

            // Hide all renderers.
            SetCreatureVisible(_localCreature, false);

            Debug.Log($"[DemoFinal] Creature instantiated HIDDEN at {spawnPos}");
        }
        else
        {
            Debug.LogError("[DemoFinal] jumpscareCreaturePrefab is null!");
        }

        // ── Wait for door to open ──
        yield return new WaitForSeconds(doorOpenDuration);

        // ── Find the player's CAMERA (the real "eyes") ──
        Camera playerCam = null;
        var fpView = localPlayer.GetComponent<NetworkFirstPersonView>();
        if (fpView != null)
            playerCam = fpView.GetComponentInChildren<Camera>();
        if (playerCam == null)
            playerCam = Camera.main;

        Transform targetTransform = playerCam != null
            ? playerCam.transform : localPlayer.transform;

        Debug.Log($"[DemoFinal] Target transform: {targetTransform.name} " +
                  $"at {targetTransform.position} " +
                  $"(localPlayer root at {localPlayer.transform.position})");

        // ── Reveal creature: face camera, enable renderers ──
        if (_localCreature != null)
        {
            // DISABLE ROOT MOTION — the walk animation was overriding our Lerp.
            Animator anim = _localCreature.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                Debug.Log("[DemoFinal] Root motion DISABLED on creature Animator.");
            }

            // Face toward the player camera.
            Vector3 camPos = targetTransform.position;
            Vector3 toCamera = camPos - _localCreature.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.001f)
                _localCreature.transform.rotation = FacingRotation(toCamera.normalized);

            // Strip physics components that could fight our position.
            var rb = _localCreature.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; Destroy(rb); }
            var cc = _localCreature.GetComponent<CharacterController>();
            if (cc != null) { cc.enabled = false; Destroy(cc); }
            var col = _localCreature.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }

            // Match creature Y to player feet so it's on the floor.
            Vector3 pos = _localCreature.transform.position;
            pos.y = localPlayer.transform.position.y;
            _localCreature.transform.position = pos;

            Debug.Log($"[DemoFinal] Creature Y set to {pos.y} (player feet). Spawn was Y={spawnPos.y}");

            SetCreatureVisible(_localCreature, true);

            // ── Start jumpscare music ──
            PlayJumpscareMusic();

            Debug.Log($"[DemoFinal] Creature REVEALED at {_localCreature.transform.position}, " +
                      $"camera at {camPos}, " +
                      $"direction: {toCamera.normalized}, " +
                      $"distance: {toCamera.magnitude:F1}m");
        }

        // ── Camera lerp toward creature ──
        if (_localCreature != null)
            yield return StartCoroutine(ClientSmoothLookAt(
                _localCreature.transform.position, cameraLookDuration));

        // ── Creature walks toward player (does NOT reach them) ──
        // Walk and fade run in PARALLEL — fade starts after the walk steps.
        Coroutine walkRoutine = null;
        if (_localCreature != null)
        {
            walkRoutine = StartCoroutine(CreatureWalkOnly(
                _localCreature, targetTransform));
        }

        // Wait for walk steps to finish, then immediate fade.
        if (walkRoutine != null)
            yield return walkRoutine;

        // ── Stop alert shader ──
        if (_chaseShaderRoutine != null)
        {
            StopCoroutine(_chaseShaderRoutine);
            _chaseShaderRoutine = null;
        }

        // ── Fade to black (creature still walking via chase anim during fade) ──
        yield return StartCoroutine(ClientFadeToBlack(fadeDuration));

        Shader.SetGlobalFloat(DetectAmountId, 0f);

        // ── Show "Continuará..." ──
        ShowContinueText();

        yield return new WaitForSeconds(continueTextDuration);

        // ── Cleanup ──
        if (_localCreature != null)
            Destroy(_localCreature);
        if (_musicSource != null)
            Destroy(_musicSource.gameObject);

        Debug.Log("[DemoFinal] Client jumpscare complete.");
    }

    // ── Creature visibility ──────────────────────────────

    private static void SetCreatureVisible(GameObject creature, bool visible)
    {
        foreach (var r in creature.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;
    }

    // ── Jumpscare music ──────────────────────────────────

    private void PlayJumpscareMusic()
    {
        if (jumpscareMusic == null) return;

        // Create a dedicated 2D AudioSource so the music is non-positional.
        GameObject musicObj = new GameObject("JumpscareMusic");
        _musicSource = musicObj.AddComponent<AudioSource>();
        _musicSource.clip = jumpscareMusic;
        _musicSource.spatialBlend = 0f; // 2D — same volume everywhere
        _musicSource.loop = true;
        _musicSource.volume = 0f;
        _musicSource.Play();

        // Fade in.
        StartCoroutine(FadeMusicIn(_musicSource, jumpscareMusicVolume, jumpscareMusicFadeIn));
    }

    private IEnumerator FadeMusicIn(AudioSource source, float targetVol, float duration)
    {
        if (source == null || duration <= 0f)
        {
            if (source != null) source.volume = targetVol;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (source != null)
                source.volume = Mathf.Lerp(0f, targetVol, elapsed / duration);
            yield return null;
        }

        if (source != null)
            source.volume = targetVol;
    }

    // ── Chase screen shader (client-local) ───────────────

    /// <summary>
    /// Drives the ScreenEffects alert shader (black flicker) directly via
    /// global properties, since the jumpscare creature has no CreatureController
    /// to trigger ScreenEffectsController's detection logic.
    /// </summary>
    private IEnumerator DriveAlertShader()
    {
        Shader.SetGlobalColor(DetectColorId, Color.black);

        while (true)
        {
            float phase = Time.unscaledTime * alertFlickerSpeed;
            float frac = phase - Mathf.Floor(phase);
            float beat = Mathf.Sin(frac * Mathf.PI);
            beat = Mathf.Pow(beat, 2f);
            float amount = beat * alertFlickerIntensity;
            Shader.SetGlobalFloat(DetectAmountId, amount);
            yield return null;
        }
    }

    // ── Creature animation (client-local) ────────────────

    /// <summary>
    /// Walks the creature a few steps toward the player, then switches to
    /// chase animation. The creature does NOT reach the player — the fade
    /// to black covers the screen before it arrives.
    /// </summary>
    private IEnumerator CreatureWalkOnly(GameObject creature, Transform target)
    {
        Animator anim = creature.GetComponentInChildren<Animator>();
        AudioSource audio = creature.GetComponent<AudioSource>();
        if (audio == null) audio = creature.AddComponent<AudioSource>();
        audio.spatialBlend = 1f;

        // CRITICAL: disable root motion so the animation doesn't fight our Lerp.
        if (anim != null)
            anim.applyRootMotion = false;

        // Walk animation.
        if (anim != null) anim.SetInteger(StateIndexHash, WALK_STATE);

        // Steps — each step recalculates direction toward the player.
        for (int i = 0; i < creatureStepCount; i++)
        {
            if (creatureFootstepClip != null)
                audio.PlayOneShot(creatureFootstepClip, creatureFootstepVolume);

            Vector3 dir = FlatDirection(creature.transform.position, target.position);

            Vector3 stepStart = creature.transform.position;
            Vector3 stepEnd = stepStart + dir * creatureStepDistance;
            // Keep on floor.
            stepEnd.y = stepStart.y;
            float stepTime = creatureStepDistance / creatureWalkSpeed;
            float elapsed = 0f;

            while (elapsed < stepTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stepTime);
                creature.transform.position = Vector3.Lerp(stepStart, stepEnd, t);
                creature.transform.rotation = FacingRotation(
                    FlatDirection(creature.transform.position, target.position));
                yield return null;
            }

            creature.transform.position = stepEnd;
            yield return new WaitForSeconds(creatureStepPause);
        }

        // Switch to chase animation (creature keeps charging during the fade).
        if (anim != null) anim.SetInteger(StateIndexHash, CHASE_STATE);

        if (creatureLungeClip != null)
            audio.PlayOneShot(creatureLungeClip, creatureLungeVolume);
    }

    /// <summary>Returns a normalized flat (Y=0) direction from a to b.</summary>
    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        d.y = 0f;
        return d.sqrMagnitude > 0.001f ? d.normalized : Vector3.forward;
    }

    /// <summary>
    /// Builds a rotation that faces 'dir' with an extra Y offset for the model.
    /// Use when the FBX's visual forward doesn't match Unity's +Z.
    /// </summary>
    private Quaternion FacingRotation(Vector3 dir)
    {
        Quaternion look = Quaternion.LookRotation(dir);
        if (Mathf.Abs(creatureModelYOffset) > 0.01f)
            look *= Quaternion.Euler(0f, creatureModelYOffset, 0f);
        return look;
    }

    /// <summary>Returns horizontal distance between two points.</summary>
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        d.y = 0f;
        return d.magnitude;
    }

    // ── Player freeze ────────────────────────────────────

    private void FreezeLocalPlayer()
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) return;

        var mov = localPlayer.GetComponent<NetworkPlayerMovement>();
        var fpView = localPlayer.GetComponent<NetworkFirstPersonView>();
        var inter = localPlayer.GetComponent<NetworkRatInteractor>();

        if (mov != null) mov.enabled = false;
        if (fpView != null) fpView.enabled = false;
        if (inter != null) inter.enabled = false;
    }

    // ── Camera smooth look ───────────────────────────────

    private IEnumerator ClientSmoothLookAt(Vector3 lookAtPosition, float duration)
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) yield break;

        Transform body = localPlayer.transform;

        var fpView = localPlayer.GetComponent<NetworkFirstPersonView>();
        Camera cam = fpView != null ? fpView.GetComponentInChildren<Camera>() : null;
        Transform viewPivot = cam != null ? cam.transform.parent : null;
        if (viewPivot == body) viewPivot = null;

        // Yaw.
        Vector3 dirFlat = lookAtPosition - body.position;
        dirFlat.y = 0f;
        Quaternion startYaw = body.rotation;
        Quaternion targetYaw = dirFlat.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(dirFlat) : startYaw;

        // Pitch.
        Quaternion startPitch = viewPivot != null
            ? viewPivot.localRotation : Quaternion.identity;
        Quaternion targetPitch = startPitch;

        if (viewPivot != null && cam != null)
        {
            Vector3 toTarget = lookAtPosition - cam.transform.position;
            float hDist = new Vector2(toTarget.x, toTarget.z).magnitude;
            float pitchAngle = Mathf.Atan2(toTarget.y, hDist) * Mathf.Rad2Deg;
            pitchAngle = Mathf.Clamp(pitchAngle, -85f, 85f);
            targetPitch = Quaternion.Euler(pitchAngle, 0f, 0f);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            body.rotation = Quaternion.Slerp(startYaw, targetYaw, t);
            if (viewPivot != null)
                viewPivot.localRotation = Quaternion.Slerp(startPitch, targetPitch, t);

            yield return null;
        }

        body.rotation = targetYaw;
        if (viewPivot != null) viewPivot.localRotation = targetPitch;
    }

    // ── Fade + text ──────────────────────────────────────

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

    private void ShowContinueText()
    {
        EnsureUIExists();

        if (continueText != null)
        {
            continueText.gameObject.SetActive(true);
            continueText.alpha = 0f;
            StartCoroutine(FadeInTextThenGlitch(continueText, 1.0f));
        }
    }

    private IEnumerator FadeInTextThenGlitch(TMP_Text text, float fadeInDuration)
    {
        RectTransform rt = text.GetComponent<RectTransform>();
        Vector2 basePos = rt != null ? rt.anchoredPosition : Vector2.zero;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            text.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        text.alpha = 1f;

        while (true)
        {
            if (rt != null) rt.anchoredPosition = basePos;
            text.alpha = 1f;

            yield return new WaitForSecondsRealtime(
                Random.Range(glitchMinInterval, glitchMaxInterval));

            if (rt != null)
            {
                rt.anchoredPosition = basePos + new Vector2(
                    Random.Range(-glitchMaxOffset, glitchMaxOffset),
                    Random.Range(-glitchMaxOffset * 0.5f, glitchMaxOffset * 0.5f));
            }

            text.alpha = Random.Range(glitchMinAlpha, 0.85f);

            yield return null;
            if (Random.value > 0.5f) yield return null;
        }
    }

    // ── UI setup ─────────────────────────────────────────

    private Canvas _ownCanvas;

    private void EnsureUIExists()
    {
        if (fadePanel != null && continueText != null) return;

        // ALWAYS create a dedicated ScreenSpaceOverlay canvas.
        // Do NOT reuse existing canvases — they may be world-space.
        if (_ownCanvas == null)
        {
            GameObject canvasObj = new GameObject("DemoFinalCanvas");
            _ownCanvas = canvasObj.AddComponent<Canvas>();
            _ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _ownCanvas.sortingOrder = 999;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Canvas canvas = _ownCanvas;

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
            continueText.color = new Color(0.22f, 1f, 0.32f, 1f);
            continueText.alpha = 0f;

            var font = Resources.Load<TMP_FontAsset>("Fonts/Audiowide_SDF");
            if (font == null)
            {
#if UNITY_EDITOR
                font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/_Project/UI/MainMenu/Fonts/Audiowide_SDF.asset");
#endif
            }
            if (font != null)
                continueText.font = font;

            textObj.SetActive(false);
        }
    }
}
