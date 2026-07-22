using Mirror;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class NetworkRatCameraMotion : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private Transform playerCamera;

    [SerializeField]
    private Transform cameraMotionRoot;

    [Header("Idle Breathing")]
    [SerializeField, Min(0f)]
    private float idleFrequency = 1.4f;

    [SerializeField, Min(0f)]
    private float idleVerticalAmplitude = 0.003f;

    [SerializeField, Min(0f)]
    private float idlePitchAmplitude = 0.12f;

    [Header("Walking Bob")]
    [SerializeField, Min(0f)]
    private float movementThreshold = 0.1f;

    [SerializeField, Min(0.01f)]
    private float referenceMovementSpeed = 5f;

    [SerializeField, Min(0f)]
    private float walkFrequency = 7.5f;

    [SerializeField, Min(0f)]
    private float walkVerticalAmplitude = 0.012f;

    [SerializeField, Min(0f)]
    private float walkHorizontalAmplitude = 0.008f;

    [SerializeField, Min(0f)]
    private float walkPitchAmplitude = 0.35f;

    [SerializeField, Min(0f)]
    private float walkRollAmplitude = 0.5f;

    [Header("Airborne")]
    [SerializeField]
    private float airborneVerticalOffset = -0.012f;

    [SerializeField]
    private float airbornePitch = -0.8f;

    [Header("Landing")]
    [SerializeField, Min(0f)]
    private float landingImpactScale = 0.004f;

    [SerializeField, Min(0f)]
    private float maximumLandingOffset = 0.025f;

    [SerializeField, Min(0.01f)]
    private float landingRecovery = 12f;

    [Header("First-Person Rig Response")]
    [SerializeField, Range(0f, 2f)]
    private float rigPositionMultiplier = 0.8f;

    [SerializeField, Range(0f, 2f)]
    private float rigRotationMultiplier = 1.1f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)]
    private float smoothing = 18f;

    private Vector3 cameraBasePosition;
    private Quaternion cameraBaseRotation;

    private Vector3 motionRootBasePosition;
    private Quaternion motionRootBaseRotation;

    private float animationPhase;
    private float landingOffset;
    private float lastAirborneVerticalVelocity;
    private bool wasGrounded;

    private void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        CaptureBasePose();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        CaptureBasePose();

        wasGrounded =
            characterController != null &&
            characterController.isGrounded;

        lastAirborneVerticalVelocity = 0f;
        landingOffset = 0f;

        ResetPoseImmediately();
    }

    public override void OnStopLocalPlayer()
    {
        ResetPoseImmediately();
        base.OnStopLocalPlayer();
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer)
        {
            return;
        }

        if (characterController == null ||
            playerCamera == null ||
            cameraMotionRoot == null)
        {
            return;
        }

        Vector3 planarVelocity = characterController.velocity;
        planarVelocity.y = 0f;

        float planarSpeed = planarVelocity.magnitude;
        bool isMoving = planarSpeed > movementThreshold;
        bool isGrounded = characterController.isGrounded;

        UpdateLandingState(isGrounded);

        Vector3 positionOffset = Vector3.zero;
        Vector3 rotationOffset = Vector3.zero;

        if (!isGrounded)
        {
            ApplyAirborneMotion(
                ref positionOffset,
                ref rotationOffset);
        }
        else if (isMoving)
        {
            ApplyWalkingMotion(
                planarSpeed,
                ref positionOffset,
                ref rotationOffset);
        }
        else
        {
            ApplyIdleMotion(
                ref positionOffset,
                ref rotationOffset);
        }

        landingOffset = Mathf.Lerp(
            landingOffset,
            0f,
            GetExponentialInterpolation(landingRecovery));

        positionOffset.y += landingOffset;

        ApplySmoothedPose(positionOffset, rotationOffset);

        wasGrounded = isGrounded;
    }

    private void ApplyIdleMotion(
        ref Vector3 positionOffset,
        ref Vector3 rotationOffset)
    {
        animationPhase += Time.deltaTime * idleFrequency;

        float wave = Mathf.Sin(animationPhase);

        positionOffset.y +=
            wave * idleVerticalAmplitude;

        rotationOffset.x +=
            wave * idlePitchAmplitude;
    }

    private void ApplyWalkingMotion(
        float planarSpeed,
        ref Vector3 positionOffset,
        ref Vector3 rotationOffset)
    {
        float normalizedSpeed = Mathf.Clamp01(
            planarSpeed / referenceMovementSpeed);

        float frequencyMultiplier = Mathf.Lerp(
            0.8f,
            1.3f,
            normalizedSpeed);

        animationPhase +=
            Time.deltaTime *
            walkFrequency *
            frequencyMultiplier;

        float sideWave = Mathf.Sin(animationPhase);

        // Dos movimientos verticales por ciclo completo:
        // uno por cada paso.
        float verticalWave =
            Mathf.Sin(animationPhase * 2f);

        positionOffset.x +=
            sideWave * walkHorizontalAmplitude;

        positionOffset.y +=
            verticalWave * walkVerticalAmplitude;

        rotationOffset.x +=
            verticalWave * walkPitchAmplitude;

        rotationOffset.z +=
            -sideWave * walkRollAmplitude;
    }

    private void ApplyAirborneMotion(
        ref Vector3 positionOffset,
        ref Vector3 rotationOffset)
    {
        positionOffset.y += airborneVerticalOffset;
        rotationOffset.x += airbornePitch;
    }

    private void UpdateLandingState(bool isGrounded)
    {
        if (!isGrounded)
        {
            lastAirborneVerticalVelocity =
                characterController.velocity.y;

            return;
        }

        if (wasGrounded)
        {
            return;
        }

        float impactSpeed = Mathf.Abs(
            Mathf.Min(lastAirborneVerticalVelocity, 0f));

        landingOffset = -Mathf.Min(
            impactSpeed * landingImpactScale,
            maximumLandingOffset);

        lastAirborneVerticalVelocity = 0f;
    }

    private void ApplySmoothedPose(
        Vector3 positionOffset,
        Vector3 rotationOffset)
    {
        float interpolation =
            GetExponentialInterpolation(smoothing);

        Vector3 cameraTargetPosition =
            cameraBasePosition +
            positionOffset;

        Quaternion cameraTargetRotation =
            cameraBaseRotation *
            Quaternion.Euler(rotationOffset);

        playerCamera.localPosition = Vector3.Lerp(
            playerCamera.localPosition,
            cameraTargetPosition,
            interpolation);

        playerCamera.localRotation = Quaternion.Slerp(
            playerCamera.localRotation,
            cameraTargetRotation,
            interpolation);

        Vector3 rigTargetPosition =
            motionRootBasePosition +
            positionOffset * rigPositionMultiplier;

        Quaternion rigTargetRotation =
            motionRootBaseRotation *
            Quaternion.Euler(
                rotationOffset * rigRotationMultiplier);

        cameraMotionRoot.localPosition = Vector3.Lerp(
            cameraMotionRoot.localPosition,
            rigTargetPosition,
            interpolation);

        cameraMotionRoot.localRotation = Quaternion.Slerp(
            cameraMotionRoot.localRotation,
            rigTargetRotation,
            interpolation);
    }

    private float GetExponentialInterpolation(float speed)
    {
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }

    private void CaptureBasePose()
    {
        if (playerCamera != null)
        {
            cameraBasePosition =
                playerCamera.localPosition;

            cameraBaseRotation =
                playerCamera.localRotation;
        }

        if (cameraMotionRoot != null)
        {
            motionRootBasePosition =
                cameraMotionRoot.localPosition;

            motionRootBaseRotation =
                cameraMotionRoot.localRotation;
        }
    }

    private void ResetPoseImmediately()
    {
        if (playerCamera != null)
        {
            playerCamera.localPosition =
                cameraBasePosition;

            playerCamera.localRotation =
                cameraBaseRotation;
        }

        if (cameraMotionRoot != null)
        {
            cameraMotionRoot.localPosition =
                motionRootBasePosition;

            cameraMotionRoot.localRotation =
                motionRootBaseRotation;
        }
    }
}