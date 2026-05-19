using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public enum AimCameraPanMode
{
    PointerFollow,
    EdgePan
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Camera/Player Aim Camera 2D")]
public class PlayerAimCamera2D : MonoBehaviour
{
    [FoldoutGroup("References"), Tooltip("Target followed while not aiming and used as the base for panning.")]
    [SerializeField] private Transform followTarget;

    [FoldoutGroup("References"), Tooltip("Optional explicit Cinemachine camera reference. If empty, auto-finds one.")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [FoldoutGroup("References"), Tooltip("Optional explicit Position Composer reference. If empty, auto-finds one.")]
    [SerializeField] private CinemachinePositionComposer positionComposer;

    [FoldoutGroup("References"), Tooltip("Optional explicit Cinemachine noise component used for screenshake. If empty, auto-finds one on the active Cinemachine camera.")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin noiseComponent;

    [FoldoutGroup("References"), Tooltip("Camera used to read mouse position. Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [FoldoutGroup("Fallback Follow")]
    [SerializeField] private Vector3 followOffset = new(0f, 0f, -10f);

    [FoldoutGroup("Fallback Follow"), MinValue(0f)]
    [SerializeField] private float followSmoothTime = 0.08f;

    [FoldoutGroup("Aim Pan"), EnumToggleButtons]
    [SerializeField] private AimCameraPanMode aimPanMode = AimCameraPanMode.PointerFollow;

    [FoldoutGroup("Aim Pan"), MinValue(0f)]
    [SerializeField] private float aimFollowSmoothTime = 0.08f;

    [FoldoutGroup("Aim Pan"), MinValue(0f)]
    [SerializeField] private float returnToPlayerSmoothTime = 0.04f;

    [FoldoutGroup("Aim Pan"), ShowIf(nameof(UsesEdgePanMode)), Range(0.01f, 0.49f)]
    [SerializeField] private float edgePanThreshold = 0.15f;

    [FoldoutGroup("Aim Pan"), MinValue(0f)]
    [SerializeField] private float panDistanceMultiplier = 1f;

    [FoldoutGroup("Aim Pan"), ShowIf(nameof(UsesPointerFollowMode)), Range(0f, 1f)]
    [Tooltip("How far the pointer can drift from the player before the camera starts following it while aiming. This is a fraction of the max aim pan distance.")]
    [SerializeField] private float pointerFollowDeadZoneRatio = 0.2f;

    [FoldoutGroup("Screenshake"), MinValue(0f)]
    [Tooltip("Fallback frequency used when the Cinemachine noise component is not available.")]
    [SerializeField] private float fallbackShakeFrequency = 35f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAiming { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float MaxAimPanDistance { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool UsesCinemachineComposer => positionComposer != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float ScreenshakeTimeRemaining => Mathf.Max(0f, shakeEndTime - Time.unscaledTime);

    private Vector3 _fallbackVelocity;
    private Vector3 _composerVelocity;
    private Vector3 _baseComposerOffset;
    private bool _hasBaseComposerOffset;
    private float _baseNoiseAmplitudeGain;
    private float _baseNoiseFrequencyGain = 1f;
    private bool _hasBaseNoiseState;
    private float shakeStartTime = float.NegativeInfinity;
    private float shakeEndTime = float.NegativeInfinity;
    private float shakeAmplitude;
    private float shakeDuration;

    private void Awake()
    {
        CacheReferences();
        CacheBaseComposerOffset();
    }

    private void OnEnable()
    {
        CacheReferences();
        CacheBaseComposerOffset();
    }

    private void LateUpdate()
    {
        CacheReferences();
        UpdateScreenshakeState();

        if (TryUpdateCinemachineAimOffset())
            return;

        UpdateFallbackTransform();
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;

        if (cinemachineCamera != null)
            cinemachineCamera.Follow = target;
    }

    public void SetAimState(bool isAiming, float maxAimPanDistance)
    {
        IsAiming = isAiming;
        MaxAimPanDistance = Mathf.Max(0f, maxAimPanDistance);
    }

    public void PlayScreenshake(float power, float duration)
    {
        power = Mathf.Max(0f, power);
        duration = Mathf.Max(0f, duration);
        if (power <= 0f || duration <= 0f)
            return;

        float remainingTime = Mathf.Max(0f, shakeEndTime - Time.unscaledTime);
        shakeAmplitude = Mathf.Max(shakeAmplitude * EvaluateRemainingShakeFactor(remainingTime), power);
        shakeDuration = Mathf.Max(duration, remainingTime);
        shakeStartTime = Time.unscaledTime;
        shakeEndTime = shakeStartTime + shakeDuration;
    }

    private void CacheReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
            if (cinemachineCamera == null)
                cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (positionComposer == null)
        {
            positionComposer = GetComponent<CinemachinePositionComposer>();
            if (positionComposer == null && cinemachineCamera != null)
                positionComposer = cinemachineCamera.GetComponent<CinemachinePositionComposer>();
        }

        if (noiseComponent == null)
        {
            noiseComponent = GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noiseComponent == null && cinemachineCamera != null)
                noiseComponent = cinemachineCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
        }

        if (cinemachineCamera != null && followTarget != null)
            cinemachineCamera.Follow = followTarget;

        CacheBaseNoiseState();
    }

    private void CacheBaseComposerOffset()
    {
        if (_hasBaseComposerOffset || positionComposer == null)
            return;

        _baseComposerOffset = positionComposer.TargetOffset;
        _hasBaseComposerOffset = true;
    }

    private bool TryUpdateCinemachineAimOffset()
    {
        if (positionComposer == null)
            return false;

        CacheBaseComposerOffset();

        Vector3 desiredWorldOffset = CalculateAimPanOffset() + CalculateScreenshakeOffset();
        Vector3 desiredLocalOffset = followTarget != null
            ? followTarget.InverseTransformDirection(desiredWorldOffset)
            : desiredWorldOffset;

        Vector3 desiredComposerOffset = _baseComposerOffset + desiredLocalOffset;
        positionComposer.TargetOffset = Vector3.SmoothDamp(
            positionComposer.TargetOffset,
            desiredComposerOffset,
            ref _composerVelocity,
            ResolveActiveSmoothTime());

        return true;
    }

    private void UpdateFallbackTransform()
    {
        if (followTarget == null)
            return;

        Vector3 desiredPosition = followTarget.position + followOffset + CalculateAimPanOffset() + CalculateScreenshakeOffset();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _fallbackVelocity, ResolveActiveSmoothTime());
    }

    private Vector3 CalculateAimPanOffset()
    {
        if (!IsAiming || MaxAimPanDistance <= 0f || targetCamera == null)
            return Vector3.zero;

        if (aimPanMode == AimCameraPanMode.PointerFollow)
            return CalculatePointerFollowOffset();

        return CalculateEdgePanOffset();
    }

    private Vector3 CalculatePointerFollowOffset()
    {
        if (followTarget == null || targetCamera == null)
            return Vector3.zero;

        Vector3 targetPosition = followTarget.position;
        float depth = Mathf.Abs(targetCamera.transform.position.z - targetPosition.z);
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = depth;

        Vector3 mouseWorld = targetCamera.ScreenToWorldPoint(mouseScreen);
        Vector2 offset = (Vector2)(mouseWorld - targetPosition);
        float maxDistance = MaxAimPanDistance * panDistanceMultiplier;

        if (maxDistance <= 0f)
            return Vector3.zero;

        float deadZoneDistance = Mathf.Clamp01(pointerFollowDeadZoneRatio) * maxDistance;
        float offsetMagnitude = offset.magnitude;
        if (offsetMagnitude <= deadZoneDistance)
            return Vector3.zero;

        offset = Vector2.ClampMagnitude(offset, maxDistance);
        offsetMagnitude = offset.magnitude;

        float remainingDistance = Mathf.Max(0.0001f, maxDistance - deadZoneDistance);
        float normalizedDistance = Mathf.Clamp01((offsetMagnitude - deadZoneDistance) / remainingDistance);
        Vector2 adjustedOffset = offset.normalized * (normalizedDistance * maxDistance);
        return new Vector3(adjustedOffset.x, adjustedOffset.y, 0f);
    }

    private Vector3 CalculateEdgePanOffset()
    {
        Vector2 viewport = new Vector2(
            Screen.width > 0 ? Input.mousePosition.x / Screen.width : 0.5f,
            Screen.height > 0 ? Input.mousePosition.y / Screen.height : 0.5f);

        Vector2 edgeInput = new Vector2(
            EvaluateEdgePan(viewport.x),
            EvaluateEdgePan(viewport.y));

        edgeInput = Vector2.ClampMagnitude(edgeInput, 1f);
        return new Vector3(edgeInput.x, edgeInput.y, 0f) * (MaxAimPanDistance * panDistanceMultiplier);
    }

    private float ResolveActiveSmoothTime()
    {
        return IsAiming
            ? Mathf.Max(0f, aimFollowSmoothTime)
            : Mathf.Max(0f, returnToPlayerSmoothTime);
    }

    private float EvaluateEdgePan(float viewportValue)
    {
        if (viewportValue <= edgePanThreshold)
            return -Mathf.InverseLerp(edgePanThreshold, 0f, viewportValue);

        float upperThreshold = 1f - edgePanThreshold;
        if (viewportValue >= upperThreshold)
            return Mathf.InverseLerp(upperThreshold, 1f, viewportValue);

        return 0f;
    }

    private void CacheBaseNoiseState()
    {
        if (_hasBaseNoiseState || noiseComponent == null)
            return;

        _baseNoiseAmplitudeGain = noiseComponent.AmplitudeGain;
        _baseNoiseFrequencyGain = noiseComponent.FrequencyGain;
        _hasBaseNoiseState = true;
    }

    private void UpdateScreenshakeState()
    {
        float remainingTime = Mathf.Max(0f, shakeEndTime - Time.unscaledTime);
        float shakeFactor = EvaluateRemainingShakeFactor(remainingTime);
        if (noiseComponent != null && _hasBaseNoiseState)
        {
            noiseComponent.AmplitudeGain = _baseNoiseAmplitudeGain + (shakeAmplitude * shakeFactor);
            noiseComponent.FrequencyGain = _baseNoiseFrequencyGain;
        }

        if (remainingTime > 0f)
            return;

        shakeAmplitude = 0f;
        shakeDuration = 0f;
        shakeStartTime = float.NegativeInfinity;
        shakeEndTime = float.NegativeInfinity;

        if (noiseComponent != null && _hasBaseNoiseState)
        {
            noiseComponent.AmplitudeGain = _baseNoiseAmplitudeGain;
            noiseComponent.FrequencyGain = _baseNoiseFrequencyGain;
        }
    }

    private Vector3 CalculateScreenshakeOffset()
    {
        if (noiseComponent != null && noiseComponent.IsValid)
            return Vector3.zero;

        float remainingTime = Mathf.Max(0f, shakeEndTime - Time.unscaledTime);
        if (remainingTime <= 0f || shakeAmplitude <= 0f)
            return Vector3.zero;

        float shakeFactor = EvaluateRemainingShakeFactor(remainingTime);
        float frequency = Mathf.Max(0f, fallbackShakeFrequency);
        float sampleTime = Time.unscaledTime * frequency;
        float x = Mathf.PerlinNoise(sampleTime, 0.17f) * 2f - 1f;
        float y = Mathf.PerlinNoise(0.83f, sampleTime) * 2f - 1f;
        return new Vector3(x, y, 0f) * (shakeAmplitude * shakeFactor);
    }

    private float EvaluateRemainingShakeFactor(float remainingTime)
    {
        if (remainingTime <= 0f || shakeDuration <= 0f)
            return 0f;

        return Mathf.Clamp01(remainingTime / shakeDuration);
    }

    private bool UsesEdgePanMode => aimPanMode == AimCameraPanMode.EdgePan;
    private bool UsesPointerFollowMode => aimPanMode == AimCameraPanMode.PointerFollow;
}
}
