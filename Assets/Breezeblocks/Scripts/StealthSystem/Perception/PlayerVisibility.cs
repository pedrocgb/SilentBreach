using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Player Visibility")]
public class PlayerVisibility : MonoBehaviour
{
    private const float MinimumSampleInterval = 0.02f;

    [FoldoutGroup("References")]
    [Tooltip("Optional point used for light sampling. If empty, this transform position is used.")]
    [SerializeField] private Transform visibilitySamplePoint;

    [FoldoutGroup("References")]
    [Tooltip("Optional utility controller used to force maximum visibility while the flashlight is on.")]
    [SerializeField] private PlayerUtilityController playerUtilityController;

    private float visibilitySampleInterval = 0.05f;

    private float visibilityIncreaseSpeed = 3f;

    private float visibilityDecreaseSpeed = 2f;

    private float minimumVisibility;

    private float maximumVisibility = 1f;

    private float muzzleFlashVisibilityDuration = 0.35f;

    private bool debugDraw;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentVisibility => currentVisibility;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float TargetVisibility => targetVisibility;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int ActiveLightCount => activeLightCount;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float StrongestLightContribution => strongestLightContribution;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float MuzzleFlashVisibilityTimeRemaining => Mathf.Max(0f, muzzleFlashVisibilityUntil - Time.time);

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 SamplePosition => visibilitySamplePoint != null ? (Vector2)visibilitySamplePoint.position : (Vector2)transform.position;

    [FoldoutGroup("Debug"), ShowIf(nameof(debugDraw)), ShowInInspector, ReadOnly]
    private VisibilityLight2D StrongestLight => _strongestLight;

    [SerializeField, HideInInspector] private float currentVisibility;
    [SerializeField, HideInInspector] private float targetVisibility;
    [SerializeField, HideInInspector] private int activeLightCount;
    [SerializeField, HideInInspector] private float strongestLightContribution;

    private readonly List<VisibilityLight2D> _activeLights = new();
    private readonly Dictionary<VisibilityLight2D, int> _lightReferenceCounts = new();

    private float _nextSampleTime;
    private float muzzleFlashVisibilityUntil = float.NegativeInfinity;
    private VisibilityLight2D _strongestLight;

    private void Awake()
    {
        ResolveReferences();
        ClampSettings();
        currentVisibility = Mathf.Clamp(currentVisibility, minimumVisibility, maximumVisibility);
        targetVisibility = Mathf.Clamp(targetVisibility, minimumVisibility, maximumVisibility);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToUtilityController();
        _nextSampleTime = Time.time + visibilitySampleInterval;
    }

    private void OnDisable()
    {
        UnsubscribeFromUtilityController();
        _activeLights.Clear();
        _lightReferenceCounts.Clear();
        _strongestLight = null;
        activeLightCount = 0;
        strongestLightContribution = 0f;
        muzzleFlashVisibilityUntil = float.NegativeInfinity;
    }

    private void OnValidate()
    {
        ResolveReferences();
        ClampSettings();
        currentVisibility = Mathf.Clamp(currentVisibility, minimumVisibility, maximumVisibility);
        targetVisibility = Mathf.Clamp(targetVisibility, minimumVisibility, maximumVisibility);
    }

    private void Update()
    {
        if (IsMaximumVisibilityForced)
        {
            targetVisibility = maximumVisibility;
            currentVisibility = maximumVisibility;
            return;
        }

        if (Time.time >= _nextSampleTime)
        {
            RecalculateTargetVisibility();
            _nextSampleTime = Time.time + visibilitySampleInterval;
        }

        float speed = targetVisibility > currentVisibility ? visibilityIncreaseSpeed : visibilityDecreaseSpeed;
        currentVisibility = Mathf.MoveTowards(currentVisibility, targetVisibility, speed * Time.deltaTime);
        currentVisibility = Mathf.Clamp(currentVisibility, minimumVisibility, maximumVisibility);
    }

    public void RegisterLight(VisibilityLight2D light)
    {
        if (light == null)
            return;

        if (_lightReferenceCounts.TryGetValue(light, out int count))
        {
            _lightReferenceCounts[light] = count + 1;
            return;
        }

        _lightReferenceCounts.Add(light, 1);
        _activeLights.Add(light);
        activeLightCount = _activeLights.Count;
    }

    public void UnregisterLight(VisibilityLight2D light)
    {
        if (light == null || !_lightReferenceCounts.TryGetValue(light, out int count))
            return;

        if (count > 1)
        {
            _lightReferenceCounts[light] = count - 1;
            return;
        }

        _lightReferenceCounts.Remove(light);
        _activeLights.Remove(light);
        activeLightCount = _activeLights.Count;
    }

    public void ForceImmediateRefresh()
    {
        if (IsMaximumVisibilityForced)
        {
            targetVisibility = maximumVisibility;
            currentVisibility = maximumVisibility;
            return;
        }

        RecalculateTargetVisibility();
        currentVisibility = targetVisibility;
    }

    public void ApplySettings(PlayerVisibilitySettings settings)
    {
        if (settings == null)
            return;

        visibilitySampleInterval = settings.VisibilitySampleInterval;
        visibilityIncreaseSpeed = settings.VisibilityIncreaseSpeed;
        visibilityDecreaseSpeed = settings.VisibilityDecreaseSpeed;
        minimumVisibility = settings.MinimumVisibility;
        maximumVisibility = settings.MaximumVisibility;
        muzzleFlashVisibilityDuration = settings.MuzzleFlashVisibilityDuration;
        debugDraw = settings.DebugDraw;

        ClampSettings();
        currentVisibility = Mathf.Clamp(currentVisibility, minimumVisibility, maximumVisibility);
        targetVisibility = Mathf.Clamp(targetVisibility, minimumVisibility, maximumVisibility);
    }

    public void ApplyMuzzleFlashVisibility()
    {
        ForceMaximumVisibility(muzzleFlashVisibilityDuration);
    }

    public void ForceMaximumVisibility(float duration)
    {
        if (duration <= 0f)
            return;

        muzzleFlashVisibilityUntil = Mathf.Max(muzzleFlashVisibilityUntil, Time.time + duration);
        targetVisibility = maximumVisibility;
        currentVisibility = maximumVisibility;
    }

    private void ResolveReferences()
    {
        if (playerUtilityController == null)
        {
            playerUtilityController = GetComponent<PlayerUtilityController>();
            if (playerUtilityController == null)
                playerUtilityController = GetComponentInParent<PlayerUtilityController>();

            if (playerUtilityController == null)
                playerUtilityController = GetComponentInChildren<PlayerUtilityController>(true);
        }
    }

    private void SubscribeToUtilityController()
    {
        if (playerUtilityController == null)
            return;

        playerUtilityController.UtilityStateChanged -= HandleUtilityStateChanged;
        playerUtilityController.UtilityStateChanged += HandleUtilityStateChanged;
    }

    private void UnsubscribeFromUtilityController()
    {
        if (playerUtilityController == null)
            return;

        playerUtilityController.UtilityStateChanged -= HandleUtilityStateChanged;
    }

    private void HandleUtilityStateChanged()
    {
        if (IsMaximumVisibilityForced)
        {
            targetVisibility = maximumVisibility;
            currentVisibility = maximumVisibility;
            return;
        }

        _nextSampleTime = Time.time;
        RecalculateTargetVisibility();
    }

    private void RecalculateTargetVisibility()
    {
        float combinedContribution = 0f;
        float highestContribution = 0f;
        VisibilityLight2D brightestLight = null;
        Vector2 samplePosition = SamplePosition;
        float currentTime = Time.time;

        for (int i = _activeLights.Count - 1; i >= 0; i--)
        {
            VisibilityLight2D light = _activeLights[i];
            if (light == null || !light.isActiveAndEnabled)
            {
                RemoveLightAt(i, light);
                continue;
            }

            float contribution = light.EvaluateContribution(samplePosition, currentTime);
            if (contribution <= 0f)
                continue;

            combinedContribution += contribution;
            if (contribution > highestContribution)
            {
                highestContribution = contribution;
                brightestLight = light;
            }

            if (combinedContribution >= maximumVisibility)
            {
                combinedContribution = maximumVisibility;
                break;
            }
        }

        targetVisibility = Mathf.Clamp(combinedContribution, minimumVisibility, maximumVisibility);
        activeLightCount = _activeLights.Count;
        strongestLightContribution = highestContribution;
        _strongestLight = brightestLight;
    }

    private void RemoveLightAt(int index, VisibilityLight2D light)
    {
        _activeLights.RemoveAt(index);

        if (light != null)
            _lightReferenceCounts.Remove(light);

        activeLightCount = _activeLights.Count;
    }

    private void ClampSettings()
    {
        visibilitySampleInterval = Mathf.Max(MinimumSampleInterval, visibilitySampleInterval);
        visibilityIncreaseSpeed = Mathf.Max(0f, visibilityIncreaseSpeed);
        visibilityDecreaseSpeed = Mathf.Max(0f, visibilityDecreaseSpeed);
        minimumVisibility = Mathf.Clamp01(minimumVisibility);
        maximumVisibility = Mathf.Clamp(maximumVisibility, minimumVisibility, 1f);
        muzzleFlashVisibilityDuration = Mathf.Max(0f, muzzleFlashVisibilityDuration);
    }

    private bool IsFlashlightVisibilityForced => playerUtilityController != null && playerUtilityController.HasActiveFlashlight;
    private bool IsMuzzleFlashVisibilityForced => Time.time < muzzleFlashVisibilityUntil;
    private bool IsMaximumVisibilityForced => IsFlashlightVisibilityForced || IsMuzzleFlashVisibilityForced;

    private void OnDrawGizmosSelected()
    {
        if (!debugDraw)
            return;

        Vector3 samplePosition = SamplePosition;
        Gizmos.color = Color.Lerp(Color.black, Color.white, Application.isPlaying ? currentVisibility : targetVisibility);
        Gizmos.DrawSphere(samplePosition, 0.12f);

        if (!Application.isPlaying)
            return;

        for (int i = 0; i < _activeLights.Count; i++)
        {
            VisibilityLight2D light = _activeLights[i];
            if (light == null)
                continue;

            Color lineColor = light == _strongestLight
                ? new Color(1f, 0.85f, 0.2f, 0.9f)
                : new Color(0.6f, 0.8f, 1f, 0.55f);

            Gizmos.color = lineColor;
            Gizmos.DrawLine(samplePosition, light.WorldCenter);
        }
    }
}
