using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Enemy World Status UI")]
public class EnemyWorldStatusUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private EnemyVisionAI enemyVisionAI;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private ActorIncapacitationController incapacitationController;

    [FoldoutGroup("References")]
    [SerializeField] private ActorHealth actorHealth;

    [FoldoutGroup("Visibility UI")]
    [SerializeField] private GameObject visibilityRoot;

    [FoldoutGroup("Visibility UI")]
    [SerializeField] private Image visibilityFillImage;

    [FoldoutGroup("Alert UI")]
    [SerializeField] private GameObject alertRoot;

    [FoldoutGroup("Suspicious UI")]
    [SerializeField] private GameObject suspiciousRoot;

    [FoldoutGroup("Incapacitated UI")]
    [SerializeField] private GameObject incapacitatedRoot;

    [FoldoutGroup("Sleeping UI")]
    [SerializeField] private GameObject sleepingRoot;

    [FoldoutGroup("Sleeping UI")]
    [SerializeField] private Image sleepingImage;

    [FoldoutGroup("Sleeping UI")]
    [SerializeField] private Vector2 sleepingStartAnchoredPosition = Vector2.zero;

    [FoldoutGroup("Sleeping UI")]
    [SerializeField] private Vector2 sleepingEndAnchoredPosition = new(0.35f, 0.35f);

    [FoldoutGroup("Sleeping UI"), MinValue(0f)]
    [SerializeField] private float sleepingStartScale = 0.05f;

    [FoldoutGroup("Sleeping UI"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float sleepingLoopDuration = 1.1f;

    [FoldoutGroup("Sleeping UI")]
    [SerializeField] private Ease sleepingScaleEase = Ease.Linear;

    [FoldoutGroup("Sleeping UI")]
    [SerializeField] private Ease sleepingMoveEase = Ease.OutSine;

    private RectTransform sleepingRectTransform;
    private Sequence sleepingSequence;
    private Color sleepingImageDefaultColor = Color.white;
    private bool sleepingImageDefaultColorCached;

    /// <summary>
    /// Caches parent references and refreshes the initial runtime widget state.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        EnsureVisibilityFillConfiguration();
        Refresh();
    }

    /// <summary>
    /// Resolves scene references and applies the initial world-status UI state.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        EnsureVisibilityFillConfiguration();
        Refresh();
    }

    /// <summary>
    /// Keeps sleeping animation authoring values within safe runtime ranges while editing.
    /// </summary>
    private void OnValidate()
    {
        sleepingStartScale = Mathf.Clamp01(sleepingStartScale);
        sleepingLoopDuration = Mathf.Max(0.01f, sleepingLoopDuration);
        CacheReferences();
    }

    /// <summary>
    /// Subscribes to incapacitation changes and refreshes the world-status UI when enabled.
    /// </summary>
    private void OnEnable()
    {
        CacheReferences();
        EnsureVisibilityFillConfiguration();
        if (incapacitationController != null)
            incapacitationController.IncapacitationStateChanged += HandleIncapacitationChanged;

        if (actorHealth != null)
            actorHealth.SleepingStateChanged += HandleSleepingStateChanged;

        Refresh();
    }

    /// <summary>
    /// Unsubscribes from status notifications and stops any active sleeping tween when disabled.
    /// </summary>
    private void OnDisable()
    {
        if (incapacitationController != null)
            incapacitationController.IncapacitationStateChanged -= HandleIncapacitationChanged;

        if (actorHealth != null)
            actorHealth.SleepingStateChanged -= HandleSleepingStateChanged;

        StopSleepingAnimation(true);
    }

    /// <summary>
    /// Refreshes the UI continuously so the detection meter tracks the live vision value.
    /// </summary>
    private void Update()
    {
        Refresh();
    }

    /// <summary>
    /// Refreshes the world-status UI when the owning actor enters or leaves incapacitation.
    /// </summary>
    private void HandleIncapacitationChanged(bool isIncapacitated)
    {
        Refresh();
    }

    /// <summary>
    /// Refreshes the world-status UI when the owning actor enters or leaves sleep.
    /// </summary>
    private void HandleSleepingStateChanged(bool isSleeping)
    {
        Refresh();
    }

    /// <summary>
    /// Resolves parent-owned components that provide the enemy state displayed by this UI.
    /// </summary>
    private void CacheReferences()
    {
        if (enemyVisionAI == null)
            enemyVisionAI = GetComponentInParent<EnemyVisionAI>();

        if (enemyMovementController == null)
            enemyMovementController = GetComponentInParent<EnemyMovementController>();

        if (incapacitationController == null)
            incapacitationController = GetComponentInParent<ActorIncapacitationController>();

        if (actorHealth == null)
            actorHealth = GetComponentInParent<ActorHealth>();

        if (sleepingRoot != null)
        {
            sleepingRectTransform = sleepingRoot.transform as RectTransform;

            if (sleepingImage == null)
                sleepingImage = sleepingRoot.GetComponentInChildren<Image>(true);
        }

        if (sleepingImage != null && !sleepingImageDefaultColorCached)
        {
            sleepingImageDefaultColor = sleepingImage.color;
            sleepingImageDefaultColorCached = true;
        }
    }

    /// <summary>
    /// Configures the visibility meter image so `fillAmount` always drives a vertical bar at runtime.
    /// </summary>
    private void EnsureVisibilityFillConfiguration()
    {
        if (visibilityFillImage == null)
            return;

        visibilityFillImage.type = Image.Type.Filled;
        visibilityFillImage.fillMethod = Image.FillMethod.Vertical;
    }

    /// <summary>
    /// Refreshes world-status widgets without disabling the owning enemy object when dead.
    /// </summary>
    private void Refresh()
    {
        bool isDead = actorHealth != null && actorHealth.IsDead;
        if (isDead)
        {
            SetRootActive(visibilityRoot, false);
            SetRootActive(alertRoot, false);
            SetRootActive(suspiciousRoot, false);
            SetRootActive(incapacitatedRoot, false);
            SetSleepingRootActive(false);

            return;
        }

        bool isIncapacitated = incapacitationController != null && incapacitationController.IsIncapacitated;
        EnemyState currentState = enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Disabled;
        bool isSleeping = !isIncapacitated &&
                          ((actorHealth != null && actorHealth.IsSleeping) ||
                           currentState == EnemyState.Sleeping);
        bool isAlert = !isIncapacitated &&
                       !isSleeping &&
                       (currentState == EnemyState.Detected ||
                        currentState == EnemyState.Alert ||
                        currentState == EnemyState.Fleeing ||
                        (enemyVisionAI != null && enemyVisionAI.CurrentDetectionValue >= 0.999f));
        float detectionValue = enemyVisionAI != null ? Mathf.Clamp01(enemyVisionAI.CurrentDetectionValue) : 0f;
        bool showVisibility = !isIncapacitated && !isSleeping && !isAlert && detectionValue > 0f;
        bool isSuspicious = !isIncapacitated &&
                            !isSleeping &&
                            !isAlert &&
                            !showVisibility &&
                            enemyMovementController != null &&
                            (currentState == EnemyState.Suspicious ||
                             currentState == EnemyState.Searching ||
                             enemyMovementController.IsDoorBellReactionActive);

        if (visibilityFillImage != null)
            visibilityFillImage.fillAmount = detectionValue;

        SetRootActive(visibilityRoot, showVisibility);
        SetRootActive(alertRoot, isAlert);
        SetRootActive(suspiciousRoot, isSuspicious);
        SetRootActive(incapacitatedRoot, isIncapacitated);
        SetSleepingRootActive(isSleeping);
    }

    /// <summary>
    /// Shows or hides the sleeping indicator and starts or stops its loop animation.
    /// </summary>
    private void SetSleepingRootActive(bool active)
    {
        if (sleepingRoot == null)
            return;

        if (!active)
        {
            StopSleepingAnimation(true);
            SetRootActive(sleepingRoot, false);
            return;
        }

        SetRootActive(sleepingRoot, true);
        StartSleepingAnimation();
    }

    /// <summary>
    /// Starts the reusable sleeping indicator loop using scale, diagonal motion, and delayed fade.
    /// </summary>
    private void StartSleepingAnimation()
    {
        CacheReferences();

        if (sleepingRectTransform == null || sleepingSequence != null)
            return;

        ResetSleepingVisual();

        float duration = Mathf.Max(0.01f, sleepingLoopDuration);
        float fadeStartPercent = sleepingStartScale >= 0.5f ? 0f : Mathf.InverseLerp(sleepingStartScale, 1f, 0.5f);
        float fadeStartTime = duration * fadeStartPercent;
        float fadeDuration = Mathf.Max(0.01f, duration - fadeStartTime);

        sleepingSequence = DOTween.Sequence();
        sleepingSequence.SetLink(gameObject);
        sleepingSequence.SetLoops(-1, LoopType.Restart);
        sleepingSequence.AppendCallback(ResetSleepingVisual);
        sleepingSequence.Append(sleepingRectTransform.DOScale(Vector3.one, duration).SetEase(sleepingScaleEase));
        sleepingSequence.Join(sleepingRectTransform.DOAnchorPos(sleepingEndAnchoredPosition, duration).SetEase(sleepingMoveEase));

        if (sleepingImage != null)
            sleepingSequence.Insert(fadeStartTime, sleepingImage.DOFade(0f, fadeDuration).SetEase(Ease.Linear));
    }

    /// <summary>
    /// Stops the sleeping indicator tween and optionally returns the UI to its first frame.
    /// </summary>
    private void StopSleepingAnimation(bool resetVisual)
    {
        if (sleepingSequence != null)
        {
            sleepingSequence.Kill();
            sleepingSequence = null;
        }

        if (resetVisual)
            ResetSleepingVisual();
    }

    /// <summary>
    /// Restores the sleeping indicator to the starting size, position, and opacity.
    /// </summary>
    private void ResetSleepingVisual()
    {
        if (sleepingRectTransform != null)
        {
            sleepingRectTransform.anchoredPosition = sleepingStartAnchoredPosition;
            sleepingRectTransform.localScale = Vector3.one * sleepingStartScale;
        }

        SetSleepingImageAlpha(1f);
    }

    /// <summary>
    /// Updates the sleeping indicator opacity while preserving its configured image color.
    /// </summary>
    private void SetSleepingImageAlpha(float alpha)
    {
        if (sleepingImage == null)
            return;

        Color color = sleepingImageDefaultColorCached ? sleepingImageDefaultColor : sleepingImage.color;
        color.a = Mathf.Clamp01(alpha);
        sleepingImage.color = color;
    }

    /// <summary>
    /// Applies a root active state only when it actually changes to avoid redundant SetActive calls.
    /// </summary>
    private static void SetRootActive(GameObject root, bool active)
    {
        if (root == null || root.activeSelf == active)
            return;

        root.SetActive(active);
    }
}

}
