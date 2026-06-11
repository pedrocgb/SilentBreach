using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Lockpick Tumbler View")]
public sealed class LockpickTumblerView : MonoBehaviour
{
    private const float MinimumShakeDuration = 0.02f;
    private const float MinimumConfiguredDepth = 0.01f;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform movableRect;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform shakeTargetRect;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform selectorAnchor;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform springRect;

    [FoldoutGroup("Visuals")]
    [SerializeField] private GameObject selectedObject;

    [FoldoutGroup("Visuals")]
    [SerializeField] private GameObject lockedObject;

    [FoldoutGroup("Visuals"), MinValue(0f), SuffixLabel("px", true)]
    [FormerlySerializedAs("compressedSpringScaleYMultiplier")]
    [SerializeField] private float maxSpringBottomInset = 220f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsVisible => gameObject.activeSelf;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool NeedsSelectorTracking => depthTween != null || currentDepth > 0f || currentShakeIntensity > 0f;

    private Tween depthTween;
    private Vector2 defaultMovableAnchoredPosition;
    private Vector2 defaultShakeAnchoredPosition;
    private Vector3 defaultSelectorWorldOffsetFromMovable;
    private Vector2 defaultSpringOffsetMin;
    private Vector2 currentShakeOffset;
    private Vector2 currentDepthBasePosition;
    private float configuredMaxDepth = 1f;
    private float currentDepth;
    private float currentShakeIntensity;
    private float currentShakeStepElapsed;
    private float currentShakeCycleDuration = MinimumShakeDuration;
    private Vector2 currentShakeStrength;
    private int currentShakeVibrato = 1;
    private float currentShakeRandomness;
    private int shakeStepIndex;

    /// <summary>
    /// Caches default authoring references while editing.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        CacheDefaultPositions();
    }

    /// <summary>
    /// Caches runtime references and baseline positions before the tumbler is animated.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        CacheDefaultPositions();
        RefreshVisualState();
    }

    /// <summary>
    /// Stops active tweens and restores local authored state when the tumbler is disabled.
    /// </summary>
    private void OnDisable()
    {
        KillTweens();
        currentShakeIntensity = 0f;
        currentShakeOffset = Vector2.zero;
        currentShakeStepElapsed = 0f;
        RefreshVisualState();
    }

    /// <summary>
    /// Refreshes local authoring references while editing.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
        CacheDefaultPositions();
        maxSpringBottomInset = Mathf.Max(0f, maxSpringBottomInset);
        configuredMaxDepth = Mathf.Max(MinimumConfiguredDepth, configuredMaxDepth);
        RefreshVisualState();
    }

    /// <summary>
    /// Advances stepped shake feedback while a hotspot warning is active.
    /// </summary>
    private void Update()
    {
        if (currentShakeIntensity <= 0f)
            return;

        float stepDuration = ResolveShakeStepDuration();
        currentShakeStepElapsed += Time.unscaledDeltaTime;
        if (currentShakeStepElapsed < stepDuration)
            return;

        currentShakeStepElapsed -= stepDuration;
        AdvanceShakeStep();
    }

    /// <summary>
    /// Shows or hides the entire tumbler view for sessions with variable tumbler counts.
    /// </summary>
    public void SetRuntimeVisible(bool visible)
    {
        if (gameObject.activeSelf == visible)
            return;

        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Resets the tumbler visuals to the supplied state at the start of a lockpicking session.
    /// </summary>
    public void ResetVisualState(float depth, float maxDepth, bool isSelected, bool isLocked)
    {
        SetRuntimeVisible(true);
        StopHotspotShake();
        ConfigureDepthRange(maxDepth);
        SetDepthImmediate(depth);
        SetSelected(isSelected);
        SetLocked(isLocked);
    }

    /// <summary>
    /// Configures the maximum push depth used to normalize spring compression.
    /// </summary>
    public void ConfigureDepthRange(float maxDepth)
    {
        configuredMaxDepth = Mathf.Max(MinimumConfiguredDepth, maxDepth);
        RefreshVisualState();
    }

    /// <summary>
    /// Applies a tumbler depth immediately without tweening.
    /// </summary>
    public void SetDepthImmediate(float depth)
    {
        depthTween?.Kill();
        depthTween = null;
        ApplyDepth(depth);
    }

    /// <summary>
    /// Animates the tumbler back toward a target depth over time.
    /// </summary>
    public void TweenToDepth(float depth, float duration, Ease ease, Action onComplete = null)
    {
        depthTween?.Kill();
        depthTween = null;
        float targetDepth = Mathf.Max(0f, depth);
        if (duration <= 0f)
        {
            ApplyDepth(targetDepth);
            onComplete?.Invoke();
            return;
        }

        depthTween = DOVirtual
            .Float(currentDepth, targetDepth, duration, ApplyDepth)
            .SetEase(ease)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                depthTween = null;
                onComplete?.Invoke();
            });
    }

    /// <summary>
    /// Enables or disables the selected-state highlight for this tumbler.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectedObject != null)
            selectedObject.SetActive(selected);
    }

    /// <summary>
    /// Enables or disables the locked-state visual for this tumbler.
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (lockedObject != null)
            lockedObject.SetActive(locked);
    }

    /// <summary>
    /// Updates the stepped shake feedback so it can ramp in before the true release hotspot is reached.
    /// </summary>
    public void SetHotspotShakeIntensity(float normalizedIntensity, float cycleDuration, Vector2 strength, int vibrato, float randomness)
    {
        currentShakeIntensity = Mathf.Clamp01(normalizedIntensity);
        currentShakeCycleDuration = Mathf.Max(MinimumShakeDuration, cycleDuration);
        currentShakeStrength = new Vector2(Mathf.Abs(strength.x), Mathf.Abs(strength.y));
        currentShakeVibrato = Mathf.Max(1, vibrato);
        currentShakeRandomness = Mathf.Clamp(randomness, 0f, 180f);

        if (currentShakeIntensity <= 0f)
        {
            currentShakeStepElapsed = 0f;
            currentShakeOffset = Vector2.zero;
            RefreshVisualState();
            return;
        }

        if (currentShakeOffset == Vector2.zero)
            AdvanceShakeStep();
    }

    /// <summary>
    /// Stops the active hotspot shake and restores the tumbler visual to its rest offset.
    /// </summary>
    public void StopHotspotShake()
    {
        currentShakeIntensity = 0f;
        currentShakeStepElapsed = 0f;
        currentShakeOffset = Vector2.zero;
        RefreshVisualState();
    }

    /// <summary>
    /// Resolves the selector anchor position in the requested reference-space rect.
    /// </summary>
    public Vector2 GetSelectorLocalPosition(RectTransform referenceSpace, Camera canvasCamera)
    {
        RectTransform anchor = selectorAnchor != null ? selectorAnchor : movableRect;
        if (anchor == null || referenceSpace == null)
            return Vector2.zero;

        Vector3 anchorWorldPosition = anchor.position;
        if (movableRect != null &&
            selectorAnchor != null &&
            selectorAnchor != movableRect &&
            !selectorAnchor.IsChildOf(movableRect))
        {
            anchorWorldPosition = movableRect.position + defaultSelectorWorldOffsetFromMovable;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, anchorWorldPosition);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(referenceSpace, screenPoint, canvasCamera, out Vector2 localPoint))
            return localPoint;

        return anchor.anchoredPosition;
    }

    /// <summary>
    /// Caches optional child references used by the tumbler visuals.
    /// </summary>
    private void CacheReferences()
    {
        movableRect ??= transform as RectTransform;
        shakeTargetRect ??= movableRect;
        selectorAnchor ??= movableRect;
    }

    /// <summary>
    /// Stores the authored rest positions used as the baseline for depth and shake motion.
    /// </summary>
    private void CacheDefaultPositions()
    {
        if (movableRect != null)
            defaultMovableAnchoredPosition = movableRect.anchoredPosition;

        if (shakeTargetRect != null)
            defaultShakeAnchoredPosition = shakeTargetRect.anchoredPosition;

        if (springRect != null)
            defaultSpringOffsetMin = springRect.offsetMin;

        if (movableRect != null && selectorAnchor != null)
            defaultSelectorWorldOffsetFromMovable = selectorAnchor.position - movableRect.position;
    }

    /// <summary>
    /// Applies the current depth, spring bottom inset, and shake offset to the authored UI transforms.
    /// </summary>
    private void RefreshVisualState()
    {
        currentDepthBasePosition = defaultMovableAnchoredPosition + (Vector2.up * Mathf.Max(0f, currentDepth));

        if (movableRect != null)
            movableRect.anchoredPosition = shakeTargetRect == movableRect ? currentDepthBasePosition + currentShakeOffset : currentDepthBasePosition;

        if (shakeTargetRect != null)
            shakeTargetRect.anchoredPosition = shakeTargetRect == movableRect ? currentDepthBasePosition + currentShakeOffset : defaultShakeAnchoredPosition + currentShakeOffset;

        if (springRect != null)
        {
            float depthNormalized = configuredMaxDepth > 0f ? Mathf.Clamp01(currentDepth / configuredMaxDepth) : 0f;
            float springBottomTravel = Mathf.Abs(maxSpringBottomInset - defaultSpringOffsetMin.y);
            Vector2 springOffsetMin = springRect.offsetMin;
            springOffsetMin.x = defaultSpringOffsetMin.x;
            springOffsetMin.y = defaultSpringOffsetMin.y + (springBottomTravel * depthNormalized);
            springRect.offsetMin = springOffsetMin;
        }
    }

    /// <summary>
    /// Applies a new runtime depth value before refreshing all dependent visuals.
    /// </summary>
    private void ApplyDepth(float depth)
    {
        currentDepth = Mathf.Max(0f, depth);
        RefreshVisualState();
    }

    /// <summary>
    /// Calculates the current stepped shake interval from the configured cycle duration and vibrato.
    /// </summary>
    private float ResolveShakeStepDuration()
    {
        return Mathf.Max(MinimumShakeDuration, currentShakeCycleDuration / Mathf.Max(1, currentShakeVibrato));
    }

    /// <summary>
    /// Advances to the next shake step using a blend of alternating and randomized offsets.
    /// </summary>
    private void AdvanceShakeStep()
    {
        currentShakeStepElapsed = 0f;
        shakeStepIndex++;

        float intensity = Mathf.Clamp01(currentShakeIntensity);
        float alternatingDirection = (shakeStepIndex & 1) == 0 ? 1f : -1f;
        Vector2 alternatingOffset = new(
            currentShakeStrength.x * alternatingDirection,
            currentShakeStrength.y * -alternatingDirection);

        Vector2 randomOffset = new(
            UnityEngine.Random.Range(-currentShakeStrength.x, currentShakeStrength.x),
            UnityEngine.Random.Range(-currentShakeStrength.y, currentShakeStrength.y));

        float randomnessBlend = Mathf.Clamp01(currentShakeRandomness / 180f);
        currentShakeOffset = Vector2.Lerp(alternatingOffset, randomOffset, randomnessBlend) * intensity;
        RefreshVisualState();
    }

    /// <summary>
    /// Stops all active view tweens owned by this tumbler.
    /// </summary>
    private void KillTweens()
    {
        depthTween?.Kill();
        depthTween = null;
    }
}

}
