using Mirror;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class NetworkRatFirstPersonAnimator : NetworkBehaviour
{
    [Header("References")]
    [SerializeField]
    private CharacterController characterController;

    [SerializeField]
    private Transform armsRoot;

    [SerializeField]
    private Transform leftPawRoot;

    [SerializeField]
    private Transform rightPawRoot;

    [Header("Idle")]
    [SerializeField, Min(0f)]
    private float idleFrequency = 1.6f;

    [SerializeField, Min(0f)]
    private float idleAmplitude = 0.008f;

    [Header("Walking")]
    [SerializeField, Min(0f)]
    private float movementThreshold = 0.1f;

    [SerializeField, Min(0f)]
    private float referenceMovementSpeed = 5f;

    [SerializeField, Min(0f)]
    private float walkFrequency = 9f;

    [SerializeField, Min(0f)]
    private float verticalAmplitude = 0.025f;

    [SerializeField, Min(0f)]
    private float forwardAmplitude = 0.035f;

    [SerializeField, Min(0f)]
    private float armsBobAmplitude = 0.012f;

    [SerializeField, Min(0f)]
    private float rotationAmplitude = 7f;

    [SerializeField, Min(0f)]
    private float rollAmplitude = 3f;

    [Header("Airborne")]
    [SerializeField, Min(0f)]
    private float airborneLift = 0.055f;

    [SerializeField]
    private float airborneBackwardOffset = -0.035f;

    [SerializeField]
    private float airbornePitch = -12f;

    [Header("Smoothing")]
    [SerializeField, Min(0.01f)]
    private float smoothing = 14f;

    private Vector3 armsBasePosition;
    private Vector3 leftBasePosition;
    private Vector3 rightBasePosition;

    private Quaternion leftBaseRotation;
    private Quaternion rightBaseRotation;

    private float animationPhase;

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

        // Volvemos a capturar la pose por si el prefab recibió
        // ajustes antes de que Mirror creara al jugador.
        CaptureBasePose();
        ResetPoseImmediately();
    }

    private void Update()
    {
        // Las patas de primera persona solamente se animan
        // en el jugador propietario de esta instancia.
        if (!isLocalPlayer)
        {
            return;
        }

        if (characterController == null ||
            armsRoot == null ||
            leftPawRoot == null ||
            rightPawRoot == null)
        {
            return;
        }

        Vector3 planarVelocity = characterController.velocity;
        planarVelocity.y = 0f;

        float planarSpeed = planarVelocity.magnitude;
        bool isMoving = planarSpeed > movementThreshold;
        bool isGrounded = characterController.isGrounded;

        Vector3 targetArmsPosition = armsBasePosition;
        Vector3 targetLeftPosition = leftBasePosition;
        Vector3 targetRightPosition = rightBasePosition;

        Quaternion targetLeftRotation = leftBaseRotation;
        Quaternion targetRightRotation = rightBaseRotation;

        if (!isGrounded)
        {
            ApplyAirbornePose(
                ref targetLeftPosition,
                ref targetRightPosition,
                ref targetLeftRotation,
                ref targetRightRotation);
        }
        else if (isMoving)
        {
            ApplyWalkingPose(
                planarSpeed,
                ref targetArmsPosition,
                ref targetLeftPosition,
                ref targetRightPosition,
                ref targetLeftRotation,
                ref targetRightRotation);
        }
        else
        {
            ApplyIdlePose(
                ref targetArmsPosition,
                ref targetLeftPosition,
                ref targetRightPosition);
        }

        float interpolation =
            1f - Mathf.Exp(-smoothing * Time.deltaTime);

        armsRoot.localPosition = Vector3.Lerp(
            armsRoot.localPosition,
            targetArmsPosition,
            interpolation);

        leftPawRoot.localPosition = Vector3.Lerp(
            leftPawRoot.localPosition,
            targetLeftPosition,
            interpolation);

        rightPawRoot.localPosition = Vector3.Lerp(
            rightPawRoot.localPosition,
            targetRightPosition,
            interpolation);

        leftPawRoot.localRotation = Quaternion.Slerp(
            leftPawRoot.localRotation,
            targetLeftRotation,
            interpolation);

        rightPawRoot.localRotation = Quaternion.Slerp(
            rightPawRoot.localRotation,
            targetRightRotation,
            interpolation);
    }

    private void ApplyIdlePose(
        ref Vector3 targetArmsPosition,
        ref Vector3 targetLeftPosition,
        ref Vector3 targetRightPosition)
    {
        animationPhase += Time.deltaTime * idleFrequency;

        float breathing =
            Mathf.Sin(animationPhase) * idleAmplitude;

        targetArmsPosition.y += breathing;
        targetLeftPosition.y += breathing * 0.6f;
        targetRightPosition.y += breathing * 0.6f;
    }

    private void ApplyWalkingPose(
        float planarSpeed,
        ref Vector3 targetArmsPosition,
        ref Vector3 targetLeftPosition,
        ref Vector3 targetRightPosition,
        ref Quaternion targetLeftRotation,
        ref Quaternion targetRightRotation)
    {
        float speedMultiplier = Mathf.Lerp(
            0.8f,
            1.35f,
            Mathf.InverseLerp(
                0f,
                referenceMovementSpeed,
                planarSpeed));

        animationPhase +=
            Time.deltaTime *
            walkFrequency *
            speedMultiplier;

        float leftWave = Mathf.Sin(animationPhase);
        float rightWave = -leftWave;

        float leftForwardWave = Mathf.Cos(animationPhase);
        float rightForwardWave =
            Mathf.Cos(animationPhase + Mathf.PI);

        targetLeftPosition += new Vector3(
            0f,
            leftWave * verticalAmplitude,
            leftForwardWave * forwardAmplitude);

        targetRightPosition += new Vector3(
            0f,
            rightWave * verticalAmplitude,
            rightForwardWave * forwardAmplitude);

        targetArmsPosition.y +=
            Mathf.Abs(leftWave) * armsBobAmplitude;

        targetLeftRotation =
            leftBaseRotation *
            Quaternion.Euler(
                leftWave * rotationAmplitude,
                0f,
                leftWave * rollAmplitude);

        targetRightRotation =
            rightBaseRotation *
            Quaternion.Euler(
                rightWave * rotationAmplitude,
                0f,
                -rightWave * rollAmplitude);
    }

    private void ApplyAirbornePose(
        ref Vector3 targetLeftPosition,
        ref Vector3 targetRightPosition,
        ref Quaternion targetLeftRotation,
        ref Quaternion targetRightRotation)
    {
        // Las patas se acercan un poco al centro y se elevan.
        targetLeftPosition += new Vector3(
            0.025f,
            airborneLift,
            airborneBackwardOffset);

        targetRightPosition += new Vector3(
            -0.025f,
            airborneLift,
            airborneBackwardOffset);

        targetLeftRotation =
            leftBaseRotation *
            Quaternion.Euler(
                airbornePitch,
                0f,
                -4f);

        targetRightRotation =
            rightBaseRotation *
            Quaternion.Euler(
                airbornePitch,
                0f,
                4f);
    }

    private void CaptureBasePose()
    {
        if (armsRoot != null)
        {
            armsBasePosition = armsRoot.localPosition;
        }

        if (leftPawRoot != null)
        {
            leftBasePosition = leftPawRoot.localPosition;
            leftBaseRotation = leftPawRoot.localRotation;
        }

        if (rightPawRoot != null)
        {
            rightBasePosition = rightPawRoot.localPosition;
            rightBaseRotation = rightPawRoot.localRotation;
        }
    }

    private void ResetPoseImmediately()
    {
        if (armsRoot != null)
        {
            armsRoot.localPosition = armsBasePosition;
        }

        if (leftPawRoot != null)
        {
            leftPawRoot.localPosition = leftBasePosition;
            leftPawRoot.localRotation = leftBaseRotation;
        }

        if (rightPawRoot != null)
        {
            rightPawRoot.localPosition = rightBasePosition;
            rightPawRoot.localRotation = rightBaseRotation;
        }
    }
}