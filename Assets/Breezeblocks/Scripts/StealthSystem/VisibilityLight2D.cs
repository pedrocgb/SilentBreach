using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
[AddComponentMenu("Breezeblocks/Stealth/Visibility Light 2D")]
public class VisibilityLight2D : MonoBehaviour
{
    private const float MinimumOuterRadius = 0.01f;
    private const float MinimumScale = 0.0001f;

    [FoldoutGroup("Visibility")]
    [Tooltip("If false, this light remains visual only and never affects stealth visibility.")]
    [SerializeField] private bool affectsVisibility = true;

    [FoldoutGroup("Visibility")]
    [Tooltip("Multiplier applied to the light's visibility contribution.")]
    [SerializeField, MinValue(0f)] private float lightIntensityMultiplier = 1f;

    [FoldoutGroup("Visibility")]
    [Tooltip("Distance from the light center where the contribution is still treated as full strength.")]
    [SerializeField, MinValue(0f)] private float innerRadius = 1f;

    [FoldoutGroup("Visibility")]
    [Tooltip("Maximum distance at which this light can affect player visibility.")]
    [SerializeField, MinValue(MinimumOuterRadius)] private float outerRadius = 3f;

    [FoldoutGroup("Visibility")]
    [Tooltip("Curve evaluated between inner and outer radius. 0 = inner edge, 1 = outer edge.")]
    [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [FoldoutGroup("Light2D Sync")]
    [Tooltip("If enabled, radius values are copied from an attached Light2D when available.")]
    [SerializeField] private bool syncRadiusFromLight2D = true;

    [FoldoutGroup("Light2D Sync")]
    [Tooltip("If enabled, the attached Light2D intensity is copied into the visibility multiplier.")]
    [SerializeField] private bool syncIntensityFromLight2D;

    [FoldoutGroup("Occlusion")]
    [Tooltip("Optional obstacle blocking check between the light and the player sample point.")]
    [SerializeField] private bool useLineOfSightBlocking;

    [FoldoutGroup("Occlusion"), ShowIf(nameof(useLineOfSightBlocking))]
    [Tooltip("Only colliders on these layers can block this light's visibility contribution.")]
    [SerializeField] private LayerMask obstacleMask;

    [FoldoutGroup("Occlusion"), ShowIf(nameof(useLineOfSightBlocking))]
    [Tooltip("How often to refresh the LOS cache for the current sample point.")]
    [SerializeField, MinValue(0.02f), SuffixLabel("s", true)] private float occlusionCheckInterval = 0.1f;

    [FoldoutGroup("Occlusion"), ShowIf(nameof(useLineOfSightBlocking))]
    [Tooltip("If the sample point moves farther than this, LOS is refreshed immediately.")]
    [SerializeField, MinValue(0f)] private float occlusionResampleDistance = 0.15f;

    [FoldoutGroup("Trigger")]
    [Tooltip("If enabled, the trigger collider radius is automatically matched to the outer radius.")]
    [SerializeField] private bool autoResizeTrigger = true;

    [FoldoutGroup("Trigger")]
    [Tooltip("Optional local offset for the trigger center.")]
    [SerializeField] private Vector2 triggerOffset;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool debugDraw = true;

    [FoldoutGroup("Debug"), ShowIf(nameof(debugDraw))]
    [SerializeField] private Color affectingLightColor = new(1f, 0.85f, 0.2f, 0.85f);

    [FoldoutGroup("Debug"), ShowIf(nameof(debugDraw))]
    [SerializeField] private Color ignoredLightColor = new(0.45f, 0.45f, 0.45f, 0.75f);

    [FoldoutGroup("Debug"), ShowIf(nameof(debugDraw))]
    [SerializeField] private Color blockedLightColor = new(1f, 0.35f, 0.35f, 0.8f);

    private readonly HashSet<PlayerVisibility> _trackedPlayers = new();

    private CircleCollider2D _triggerCollider;
    private Light2D _light2D;
    private bool _cachedOccluded;
    private float _lastOcclusionCheckTime = float.NegativeInfinity;
    private Vector2 _lastOcclusionSamplePoint;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool AffectsVisibility => affectsVisibility;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float LightIntensityMultiplier => lightIntensityMultiplier;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float InnerRadius => innerRadius;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float OuterRadius => outerRadius;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 WorldCenter => _triggerCollider != null ? transform.TransformPoint(_triggerCollider.offset) : transform.position;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    private int TrackedPlayerCount => _trackedPlayers.Count;

    [FoldoutGroup("State"), ShowIf(nameof(useLineOfSightBlocking)), ShowInInspector, ReadOnly]
    private bool CachedOccluded => _cachedOccluded;

    private void Reset()
    {
        CacheReferences();
        SyncFromAttachedLight2D();
        ClampSettings();
        SyncTriggerCollider();
    }

    private void Awake()
    {
        CacheReferences();
        SyncTriggerCollider();
    }

    private void OnEnable()
    {
        CacheReferences();
        SyncTriggerCollider();
    }

    private void OnDisable()
    {
        UnregisterTrackedPlayers();
    }

    private void OnValidate()
    {
        CacheReferences();
        SyncFromAttachedLight2D();
        ClampSettings();
        SyncTriggerCollider();
    }

    public float EvaluateContribution(Vector2 samplePosition, float currentTime)
    {
        if (!IsActiveForVisibility())
            return 0f;

        Vector2 origin = WorldCenter;
        float distance = Vector2.Distance(origin, samplePosition);
        if (distance >= outerRadius)
            return 0f;

        if (useLineOfSightBlocking && obstacleMask.value != 0 && IsOccluded(samplePosition, currentTime))
            return 0f;

        float contribution = CalculateDistanceContribution(distance);
        return Mathf.Max(0f, contribution * lightIntensityMultiplier);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRegisterPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryRegisterPlayer(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        TryUnregisterPlayer(other);
    }

    private void TryRegisterPlayer(Collider2D other)
    {
        if (!affectsVisibility || other == null)
            return;

        PlayerVisibility player = other.GetComponentInParent<PlayerVisibility>();
        if (player == null)
            return;

        if (_trackedPlayers.Add(player))
            player.RegisterLight(this);
    }

    private void TryUnregisterPlayer(Collider2D other)
    {
        if (other == null)
            return;

        PlayerVisibility player = other.GetComponentInParent<PlayerVisibility>();
        if (player == null)
            return;

        if (_trackedPlayers.Remove(player))
            player.UnregisterLight(this);
    }

    private void UnregisterTrackedPlayers()
    {
        foreach (PlayerVisibility player in _trackedPlayers)
        {
            if (player != null)
                player.UnregisterLight(this);
        }

        _trackedPlayers.Clear();
    }

    private bool IsActiveForVisibility()
    {
        if (!affectsVisibility || !isActiveAndEnabled)
            return false;

        return _light2D == null || _light2D.enabled;
    }

    private float CalculateDistanceContribution(float distance)
    {
        if (distance <= innerRadius)
            return 1f;

        if (outerRadius <= innerRadius + Mathf.Epsilon)
            return 1f;

        float normalizedDistance = Mathf.InverseLerp(innerRadius, outerRadius, distance);
        return Mathf.Clamp01(falloffCurve.Evaluate(normalizedDistance));
    }

    private bool IsOccluded(Vector2 samplePosition, float currentTime)
    {
        float resampleDistanceSqr = occlusionResampleDistance * occlusionResampleDistance;
        bool needsRefresh = currentTime - _lastOcclusionCheckTime >= occlusionCheckInterval ||
                            (samplePosition - _lastOcclusionSamplePoint).sqrMagnitude >= resampleDistanceSqr;

        if (needsRefresh)
        {
            _lastOcclusionCheckTime = currentTime;
            _lastOcclusionSamplePoint = samplePosition;
            _cachedOccluded = Physics2D.Linecast(WorldCenter, samplePosition, obstacleMask).collider != null;
        }

        return _cachedOccluded;
    }

    private void CacheReferences()
    {
        if (_triggerCollider == null)
            _triggerCollider = GetComponent<CircleCollider2D>();

        if (_light2D == null)
            _light2D = GetComponent<Light2D>();
    }

    private void SyncFromAttachedLight2D()
    {
        if (_light2D == null)
            return;

        if (syncRadiusFromLight2D)
        {
            outerRadius = Mathf.Max(MinimumOuterRadius, _light2D.pointLightOuterRadius);
            innerRadius = Mathf.Clamp(_light2D.pointLightInnerRadius, 0f, outerRadius);
        }

        if (syncIntensityFromLight2D)
            lightIntensityMultiplier = Mathf.Max(0f, _light2D.intensity);
    }

    private void SyncTriggerCollider()
    {
        if (_triggerCollider == null)
            return;

        _triggerCollider.isTrigger = true;
        _triggerCollider.offset = triggerOffset;

        if (!autoResizeTrigger)
            return;

        float scale = GetLargestAbsScale();
        _triggerCollider.radius = outerRadius / scale;
    }

    private float GetLargestAbsScale()
    {
        Vector3 scale = transform.lossyScale;
        float largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return Mathf.Max(MinimumScale, largest);
    }

    private void ClampSettings()
    {
        lightIntensityMultiplier = Mathf.Max(0f, lightIntensityMultiplier);
        innerRadius = Mathf.Max(0f, innerRadius);
        outerRadius = Mathf.Max(MinimumOuterRadius, outerRadius);
        innerRadius = Mathf.Min(innerRadius, outerRadius);
        occlusionCheckInterval = Mathf.Max(0.02f, occlusionCheckInterval);
        occlusionResampleDistance = Mathf.Max(0f, occlusionResampleDistance);
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDraw)
            return;

        Color outerColor = affectsVisibility ? affectingLightColor : ignoredLightColor;
        if (useLineOfSightBlocking && _cachedOccluded)
            outerColor = blockedLightColor;

        Vector3 center = WorldCenter;
        Gizmos.color = outerColor;
        Gizmos.DrawWireSphere(center, outerRadius);

        Gizmos.color = new Color(outerColor.r, outerColor.g, outerColor.b, Mathf.Clamp01(outerColor.a + 0.1f));
        Gizmos.DrawWireSphere(center, innerRadius);
    }
}
