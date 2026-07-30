using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Lightweight scripted creature for the demo jumpscare.
///
/// Unlike CreatureController (AI + NavMesh + perception), this component
/// does a simple scripted sequence: walk 2 steps toward a target, then
/// lunge/attack. No pathfinding, no state machine — just animation +
/// transform movement + footstep sounds.
///
/// SETUP:
/// 1. Create a prefab with:
///    - The creature model FBX as a child (with Animator + SkinnedMeshRenderer).
///    - This component on the root.
///    - A NetworkIdentity on the root.
///    - An AudioSource on the root.
/// 2. The Animator should have:
///    - "StateIndex" (int): 0 = walk, 2 = chase/run (matching CreatureAnimator).
///    - "Attack" (trigger): lunge animation.
/// 3. Register the prefab in NetworkManager's spawnable list.
/// 4. DemoFinalEventManager spawns and calls Execute().
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(AudioSource))]
public sealed class JumpscareCreature : NetworkBehaviour
{
    [Header("Animation")]

    [SerializeField, Tooltip("Animator on the model child. Auto-found if null.")]
    private Animator animator;

    [Header("Movement")]

    [SerializeField, Tooltip("Walk speed during the approach steps.")]
    private float walkSpeed = 2.5f;

    [SerializeField, Tooltip("Lunge speed (much faster).")]
    private float lungeSpeed = 12f;

    [SerializeField, Tooltip("Number of steps before the lunge.")]
    private int stepCount = 2;

    [SerializeField, Tooltip("Distance per step (meters).")]
    private float stepDistance = 0.8f;

    [SerializeField, Tooltip("Pause between steps (seconds).")]
    private float stepPause = 0.35f;

    [Header("Audio")]

    [SerializeField, Tooltip("Footstep sound. Played once per step.")]
    private AudioClip footstepClip;

    [SerializeField, Range(0f, 1f)]
    private float footstepVolume = 0.9f;

    [SerializeField, Tooltip("Scream/growl when the creature starts running.")]
    private AudioClip lungeClip;

    [SerializeField, Range(0f, 1f)]
    private float lungeVolume = 1f;

    // ── Animator hashes ──────────────────────────────────

    private static readonly int StateIndexHash = Animator.StringToHash("StateIndex");

    // Walk = StateIndex 0 (Patrol/Unsteady Walk in CreatureAnimator).
    // Chase = StateIndex 2 (run toward player — sequence ends here).
    private const int WALK_STATE = 0;
    private const int CHASE_STATE = 2;

    // ── State ────────────────────────────────────────────

    [SyncVar]
    private Vector3 _syncPosition;

    [SyncVar]
    private Quaternion _syncRotation;

    private AudioSource _audioSource;
    private bool _executing;

    // ── Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.spatialBlend = 1f;
        _audioSource.playOnAwake = false;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        // Smooth sync on non-authority clients.
        if (!isServer && !_executing)
        {
            transform.position = Vector3.Lerp(
                transform.position, _syncPosition, Time.deltaTime * 15f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, _syncRotation, Time.deltaTime * 15f);
        }
    }

    // ── Public API ───────────────────────────────────────

    /// <summary>
    /// Start the jumpscare sequence toward a target position.
    /// Called by DemoFinalEventManager on the server.
    /// </summary>
    [Server]
    public void Execute(Vector3 targetPosition)
    {
        if (_executing) return;
        _executing = true;

        // Face the target.
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);

        _syncRotation = transform.rotation;

        StartCoroutine(ServerJumpscareSequence(targetPosition));
        RpcStartSequence(targetPosition);
    }

    // ── Server sequence ──────────────────────────────────

    [Server]
    private IEnumerator ServerJumpscareSequence(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0f;

        // Walk steps.
        for (int i = 0; i < stepCount; i++)
        {
            Vector3 stepTarget = transform.position + direction * stepDistance;
            float stepTime = stepDistance / walkSpeed;
            float elapsed = 0f;
            Vector3 start = transform.position;

            while (elapsed < stepTime)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(start, stepTarget, elapsed / stepTime);
                _syncPosition = transform.position;
                yield return null;
            }

            transform.position = stepTarget;
            _syncPosition = transform.position;

            yield return new WaitForSeconds(stepPause);
        }

        // Lunge toward target.
        Vector3 lungeStart = transform.position;
        float lungeDistance = Vector3.Distance(lungeStart, targetPosition);
        float lungeTime = lungeDistance / lungeSpeed;
        float lungeElapsed = 0f;

        while (lungeElapsed < lungeTime)
        {
            lungeElapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(
                lungeStart, targetPosition,
                lungeElapsed / lungeTime);
            _syncPosition = transform.position;
            yield return null;
        }

        transform.position = targetPosition;
        _syncPosition = transform.position;
    }

    // ── Client sequence ──────────────────────────────────

    [ClientRpc]
    private void RpcStartSequence(Vector3 targetPosition)
    {
        _executing = true;
        StartCoroutine(ClientJumpscareSequence(targetPosition));
    }

    private IEnumerator ClientJumpscareSequence(Vector3 targetPosition)
    {
        // Walk animation.
        if (animator != null)
            animator.SetInteger(StateIndexHash, WALK_STATE);

        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0f;

        // Steps with footstep sounds.
        for (int i = 0; i < stepCount; i++)
        {
            // Play footstep.
            if (footstepClip != null)
                _audioSource.PlayOneShot(footstepClip, footstepVolume);

            float stepTime = stepDistance / walkSpeed;
            yield return new WaitForSeconds(stepTime + stepPause);
        }

        // Switch to chase/run animation and play lunge sound.
        if (animator != null)
            animator.SetInteger(StateIndexHash, CHASE_STATE);

        if (lungeClip != null)
            _audioSource.PlayOneShot(lungeClip, lungeVolume);
    }
}
