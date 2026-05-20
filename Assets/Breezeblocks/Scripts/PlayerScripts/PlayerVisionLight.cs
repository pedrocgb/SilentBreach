using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[RequireComponent(typeof(Light2D))]
public class PlayerVisionLight : MonoBehaviour
{
    private const float MinDirectionSqr = 0.0001f;

    private float maxViewRadius = 8f;

    private float minViewRadius = 3f;

    private float viewAngle = 120f;

    private float innerRadiusFraction = 0.5f;

    private float innerAngleFraction = 0.3f;

    private bool lookAtMouse = true;

    private float rotationSmoothing = 720f;

    private float rotationOffset = -90f;

    private float visionLevel01 = 1f;

    private Vector2 externalDirection = Vector2.right;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    private float CurrentRadius => Mathf.Lerp(minViewRadius, maxViewRadius, visionLevel01);

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    private Vector2 CurrentDirection => ResolveDirection(allowMouseWhenNotPlaying: true, out var dir) ? dir : Vector2.right;

    public float RotationSmoothing
    {
        get => rotationSmoothing;
        set => rotationSmoothing = Mathf.Max(0f, value);
    }

    public Vector2 FacingDirection
    {
        get
        {
            float angle = transform.eulerAngles.z - rotationOffset;
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }
    }

    private Light2D _light2D;
    private Camera _cam;
    private bool _externallyDrivenThisFrame;
    private bool _inputBlocked;

    private float _lastOuterRadius = -1f;
    private float _lastInnerRadius = -1f;
    private float _lastOuterAngle = -1f;
    private float _lastInnerAngle = -1f;

    private void OnEnable()
    {
        CacheRefs();
        ApplyShapeIfChanged(force: true);

        if (!Application.isPlaying)
        {
            ApplyRotationImmediate();
        }
    }

    private void Awake()
    {
        CacheRefs();
        ApplyShapeIfChanged(force: true);

        if (Application.isPlaying)
        {
            ApplyRotationImmediate();
        }
    }

    private void OnValidate()
    {
        CacheRefs();

        minViewRadius = Mathf.Max(0f, minViewRadius);
        maxViewRadius = Mathf.Max(minViewRadius, maxViewRadius);
        rotationSmoothing = Mathf.Max(0f, rotationSmoothing);

        if (!Application.isPlaying)
        {
            ApplyShapeIfChanged(force: true);
            ApplyRotationImmediate();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (_externallyDrivenThisFrame)
        {
            _externallyDrivenThisFrame = false;
            ApplyShapeIfChanged(force: false);
            return;
        }

        if (_inputBlocked)
        {
            ApplyShapeIfChanged(force: false);
            return;
        }

        UpdateRotation(Time.deltaTime);
        ApplyShapeIfChanged(force: false);
    }

    public void SetVisionLevel01(float t)
    {
        visionLevel01 = Mathf.Clamp01(t);
    }

    public void SetExternalDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude > MinDirectionSqr)
            externalDirection = dir.normalized;
    }

    public Vector2 DriveExternalDirection(Vector2 dir, float smoothing, float deltaTime)
    {
        if (dir.sqrMagnitude <= MinDirectionSqr)
            return FacingDirection;

        ApplyExternalDirection(dir, smoothing, deltaTime);
        return FacingDirection;
    }

    public Vector2 DriveMouseLook(float smoothing, float deltaTime)
    {
        if (_inputBlocked)
            return FacingDirection;

        lookAtMouse = true;
        RotationSmoothing = smoothing;
        UpdateRotation(deltaTime);
        _externallyDrivenThisFrame = true;
        return FacingDirection;
    }

    public void ApplyExternalDirection(Vector2 dir, float smoothing, float deltaTime)
    {
        if (dir.sqrMagnitude <= MinDirectionSqr)
            return;

        lookAtMouse = false;
        RotationSmoothing = smoothing;
        externalDirection = dir.normalized;
        UpdateRotation(deltaTime);
        _externallyDrivenThisFrame = true;
    }

    public void SetInputBlocked(bool blocked)
    {
        _inputBlocked = blocked;

        if (!blocked)
            return;

        lookAtMouse = false;
        if (FacingDirection.sqrMagnitude > MinDirectionSqr)
            externalDirection = FacingDirection;

        _externallyDrivenThisFrame = false;
    }

    public void ApplySettings(PlayerVisionLightSettings settings)
    {
        if (settings == null)
            return;

        maxViewRadius = Mathf.Max(0f, settings.MaxViewRadius);
        minViewRadius = Mathf.Max(0f, settings.MinViewRadius);
        viewAngle = Mathf.Clamp(settings.ViewAngle, 0f, 360f);
        innerRadiusFraction = Mathf.Clamp01(settings.InnerRadiusFraction);
        innerAngleFraction = Mathf.Clamp01(settings.InnerAngleFraction);
        lookAtMouse = settings.LookAtMouse;
        rotationSmoothing = Mathf.Max(0f, settings.RotationSmoothing);
        rotationOffset = settings.RotationOffset;
        visionLevel01 = Mathf.Clamp01(settings.VisionLevel01);
        externalDirection = settings.ExternalDirection.sqrMagnitude > MinDirectionSqr
            ? settings.ExternalDirection.normalized
            : Vector2.right;

        CacheRefs();
        maxViewRadius = Mathf.Max(minViewRadius, maxViewRadius);
        ApplyShapeIfChanged(force: true);
        ApplyRotationImmediate();
    }

    private void CacheRefs()
    {
        if (_light2D == null)
            _light2D = GetComponent<Light2D>();

        if (_cam == null)
            _cam = Camera.main;
    }

    private void UpdateRotation(float deltaTime)
    {
        if (!ResolveDirection(allowMouseWhenNotPlaying: false, out var dir))
            return;

        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffset;

        if (rotationSmoothing <= 0f)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, targetAngle);
            return;
        }

        float currentAngle = transform.eulerAngles.z;
        float maxDelta = rotationSmoothing * deltaTime;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxDelta);
        transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
    }

    private void ApplyRotationImmediate()
    {
        if (!ResolveDirection(allowMouseWhenNotPlaying: false, out var dir))
            dir = Vector2.right;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + rotationOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private bool ResolveDirection(bool allowMouseWhenNotPlaying, out Vector2 dir)
    {
        dir = Vector2.zero;

        if (lookAtMouse && _cam != null && (Application.isPlaying || allowMouseWhenNotPlaying))
        {
            Vector3 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 origin = transform.position;
            dir = (Vector2)mouseWorld - origin;

            if (dir.sqrMagnitude < MinDirectionSqr)
                return false;

            dir.Normalize();
            return true;
        }

        if (externalDirection.sqrMagnitude < MinDirectionSqr)
            return false;

        dir = externalDirection.normalized;
        return true;
    }

    private void ApplyShapeIfChanged(bool force)
    {
        if (_light2D == null)
            return;

        float radius = Mathf.Lerp(minViewRadius, maxViewRadius, visionLevel01);
        float outerRadius = radius;
        float innerRadius = radius * innerRadiusFraction;
        float outerAngle = viewAngle;
        float innerAngle = viewAngle * innerAngleFraction;

        if (!force &&
            Mathf.Approximately(outerRadius, _lastOuterRadius) &&
            Mathf.Approximately(innerRadius, _lastInnerRadius) &&
            Mathf.Approximately(outerAngle, _lastOuterAngle) &&
            Mathf.Approximately(innerAngle, _lastInnerAngle))
        {
            return;
        }

        _light2D.pointLightOuterRadius = outerRadius;
        _light2D.pointLightInnerRadius = innerRadius;
        _light2D.pointLightOuterAngle = outerAngle;
        _light2D.pointLightInnerAngle = innerAngle;

        _lastOuterRadius = outerRadius;
        _lastInnerRadius = innerRadius;
        _lastOuterAngle = outerAngle;
        _lastInnerAngle = innerAngle;
    }
}
