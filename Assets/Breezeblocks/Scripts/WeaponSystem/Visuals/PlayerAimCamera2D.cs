using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using Breezeblocks.Input;
using Breezeblocks.Settings;

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

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float ConfiguredFallbackFollowSmoothTime => Mathf.Max(0f, followSmoothTime);

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float ConfiguredPointerFollowDeadZoneRatio => Mathf.Clamp01(pointerFollowDeadZoneRatio);

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
    private IPointerInputReader pointerInputReader;

    // Executes the Awake routine.
    private void Awake()
    {
        pointerInputReader ??= new RewiredPlayerInputReader();
        CacheReferences();
        CacheBaseComposerOffset();
    }

    // Executes the OnEnable routine.
    private void OnEnable()
    {
        pointerInputReader ??= new RewiredPlayerInputReader();
        CacheReferences();
        CacheBaseComposerOffset();
    }

    // Executes the LateUpdate routine.
    private void LateUpdate()
    {
        CacheReferences();
        UpdateScreenshakeState();

        if (TryUpdateCinemachineAimOffset())
            return;

        UpdateFallbackTransform();
    }

    // Executes the SetFollowTarget routine.
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;

        if (cinemachineCamera != null)
            cinemachineCamera.Follow = target;
    }

    // Executes the SetAimState routine.
    public void SetAimState(bool isAiming, float maxAimPanDistance)
    {
        IsAiming = isAiming;
        MaxAimPanDistance = Mathf.Max(0f, maxAimPanDistance);
    }

    /// <summary>
    /// Returns the current orthographic size from the active Cinemachine or fallback camera.
    /// </summary>
    public bool TryGetOrthographicSize(out float orthographicSize)
    {
        CacheReferences();

        if (cinemachineCamera != null)
        {
            orthographicSize = cinemachineCamera.Lens.OrthographicSize;
            return true;
        }

        if (targetCamera != null && targetCamera.orthographic)
        {
            orthographicSize = targetCamera.orthographicSize;
            return true;
        }

        orthographicSize = 0f;
        return false;
    }

    /// <summary>
    /// Applies an orthographic size to the active Cinemachine or fallback camera.
    /// </summary>
    public void SetOrthographicSize(float orthographicSize)
    {
        CacheReferences();
        float clampedSize = Mathf.Max(0.01f, orthographicSize);

        if (cinemachineCamera != null)
        {
            LensSettings lens = cinemachineCamera.Lens;
            lens.OrthographicSize = clampedSize;
            cinemachineCamera.Lens = lens;
            return;
        }

        if (targetCamera != null && targetCamera.orthographic)
            targetCamera.orthographicSize = clampedSize;
    }

    // Executes the PlayScreenshake routine.
    public void PlayScreenshake(float power, float duration)
    {
        if (!GameSettingsRuntime.ScreenshakeEnabled)
            return;

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

    // Executes the CacheReferences routine.
    private void CacheReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
            if (cinemachineCamera == null)
                cinemachineCamera = GetComponentInChildren<CinemachineCamera>(true);

            if (cinemachineCamera == null)
                cinemachineCamera = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<CinemachineCamera>(gameObject);
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

    // Executes the CacheBaseComposerOffset routine.
    private void CacheBaseComposerOffset()
    {
        if (_hasBaseComposerOffset || positionComposer == null)
            return;

        _baseComposerOffset = positionComposer.TargetOffset;
        _hasBaseComposerOffset = true;
    }

    // Executes the TryUpdateCinemachineAimOffset routine.
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
        if (UsesExactPointerFollowAim())
        {
            _composerVelocity = Vector3.zero;
            positionComposer.TargetOffset = desiredComposerOffset;
        }
        else
        {
            positionComposer.TargetOffset = Vector3.SmoothDamp(
                positionComposer.TargetOffset,
                desiredComposerOffset,
                ref _composerVelocity,
                ResolveActiveSmoothTime());
        }

        return true;
    }

    // Executes the UpdateFallbackTransform routine.
    private void UpdateFallbackTransform()
    {
        if (followTarget == null)
            return;

        Vector3 desiredPosition = followTarget.position + followOffset + CalculateAimPanOffset() + CalculateScreenshakeOffset();
        if (UsesExactPointerFollowAim())
        {
            _fallbackVelocity = Vector3.zero;
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _fallbackVelocity, ResolveActiveSmoothTime());
        }
    }

    // Executes the CalculateAimPanOffset routine.
    private Vector3 CalculateAimPanOffset()
    {
        if (!IsAiming || MaxAimPanDistance <= 0f || targetCamera == null)
            return Vector3.zero;

        if (aimPanMode == AimCameraPanMode.PointerFollow)
            return CalculatePointerFollowOffset();

        return CalculateEdgePanOffset();
    }

    // Executes the CalculatePointerFollowOffset routine.
    private Vector3 CalculatePointerFollowOffset()
    {
        if (followTarget == null || targetCamera == null)
            return Vector3.zero;

        float maxDistance = MaxAimPanDistance * panDistanceMultiplier;
        if (maxDistance <= 0f)
            return Vector3.zero;

        Vector3 targetScreen = targetCamera.WorldToScreenPoint(followTarget.position);
        Vector2 pointerScreenPosition = pointerInputReader != null
            ? pointerInputReader.GetScreenPositionOrDefault()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 screenDelta = pointerScreenPosition - new Vector2(targetScreen.x, targetScreen.y);

        if (targetCamera.orthographic)
        {
            float unitsPerPixelY = (targetCamera.orthographicSize * 2f) / Mathf.Max(1f, Screen.height);
            float unitsPerPixelX = unitsPerPixelY * targetCamera.aspect;
            Vector2 worldOffset = new Vector2(screenDelta.x * unitsPerPixelX, screenDelta.y * unitsPerPixelY);
            worldOffset = Vector2.ClampMagnitude(worldOffset, maxDistance);
            return new Vector3(worldOffset.x, worldOffset.y, 0f);
        }

        float depth = Mathf.Abs(targetCamera.transform.position.z - followTarget.position.z);
        Vector3 targetWorldOnScreenPlane = targetCamera.ScreenToWorldPoint(new Vector3(targetScreen.x, targetScreen.y, depth));
        Vector3 mouseWorld = targetCamera.ScreenToWorldPoint(new Vector3(pointerScreenPosition.x, pointerScreenPosition.y, depth));
        Vector2 perspectiveOffset = Vector2.ClampMagnitude((Vector2)(mouseWorld - targetWorldOnScreenPlane), maxDistance);
        return new Vector3(perspectiveOffset.x, perspectiveOffset.y, 0f);
    }

    // Executes the CalculateEdgePanOffset routine.
    private Vector3 CalculateEdgePanOffset()
    {
        Vector2 pointerScreenPosition = pointerInputReader != null
            ? pointerInputReader.GetScreenPositionOrDefault()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 viewport = new Vector2(
            Screen.width > 0 ? pointerScreenPosition.x / Screen.width : 0.5f,
            Screen.height > 0 ? pointerScreenPosition.y / Screen.height : 0.5f);

        Vector2 edgeInput = new Vector2(
            EvaluateEdgePan(viewport.x),
            EvaluateEdgePan(viewport.y));

        edgeInput = Vector2.ClampMagnitude(edgeInput, 1f);
        return new Vector3(edgeInput.x, edgeInput.y, 0f) * (MaxAimPanDistance * panDistanceMultiplier);
    }

    // Executes the ResolveActiveSmoothTime routine.
    private float ResolveActiveSmoothTime()
    {
        return IsAiming
            ? Mathf.Max(0f, aimFollowSmoothTime)
            : Mathf.Max(0f, returnToPlayerSmoothTime);
    }

    // Executes the EvaluateEdgePan routine.
    private float EvaluateEdgePan(float viewportValue)
    {
        if (viewportValue <= edgePanThreshold)
            return -Mathf.InverseLerp(edgePanThreshold, 0f, viewportValue);

        float upperThreshold = 1f - edgePanThreshold;
        if (viewportValue >= upperThreshold)
            return Mathf.InverseLerp(upperThreshold, 1f, viewportValue);

        return 0f;
    }

    // Executes the CacheBaseNoiseState routine.
    private void CacheBaseNoiseState()
    {
        if (_hasBaseNoiseState || noiseComponent == null)
            return;

        _baseNoiseAmplitudeGain = noiseComponent.AmplitudeGain;
        _baseNoiseFrequencyGain = noiseComponent.FrequencyGain;
        _hasBaseNoiseState = true;
    }

    // Executes the UpdateScreenshakeState routine.
    private void UpdateScreenshakeState()
    {
        if (!GameSettingsRuntime.ScreenshakeEnabled)
            shakeEndTime = float.NegativeInfinity;

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

    // Executes the CalculateScreenshakeOffset routine.
    private Vector3 CalculateScreenshakeOffset()
    {
        if (!GameSettingsRuntime.ScreenshakeEnabled)
            return Vector3.zero;

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

    // Executes the EvaluateRemainingShakeFactor routine.
    private float EvaluateRemainingShakeFactor(float remainingTime)
    {
        if (remainingTime <= 0f || shakeDuration <= 0f)
            return 0f;

        return Mathf.Clamp01(remainingTime / shakeDuration);
    }

    // Executes the UsesExactPointerFollowAim routine.
    private bool UsesExactPointerFollowAim()
    {
        return IsAiming && aimPanMode == AimCameraPanMode.PointerFollow;
    }

    private bool UsesEdgePanMode => aimPanMode == AimCameraPanMode.EdgePan;
    private bool UsesPointerFollowMode => aimPanMode == AimCameraPanMode.PointerFollow;
}
}
