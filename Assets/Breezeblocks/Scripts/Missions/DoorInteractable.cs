using System;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

public enum DoorDefaultState
{
    None,
    Open,
    Closed
}

[DefaultExecutionOrder(-6000)]
[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Door Interactable")]
public class DoorInteractable : PlayerWorldInteractable
{
    [Serializable]
    private sealed class DoorAudioDefinition
    {
        [InlineProperty, HideLabel]
        public AudioClipSet Sfx = new();

        public NoiseType NoiseType = NoiseType.Common;

        [MinValue(0f)]
        public float NoiseAmount = 0.35f;

        public bool ExtremeNoise;

        public void Validate()
        {
            Sfx ??= new AudioClipSet();
            Sfx.Validate();
            NoiseAmount = Mathf.Max(0f, NoiseAmount);
        }
    }

    private const float MinimumAnimationDuration = 0f;
    private const float MinimumVisibilitySampleInterval = 0.02f;
    private static readonly System.Collections.Generic.List<DoorInteractable> ActiveDoorsInternal = new();

    [FoldoutGroup("References")]
    [SerializeField] private Transform doorPivot;

    [FoldoutGroup("References")]
    [SerializeField] private Collider2D blockingCollider;

    [FoldoutGroup("References")]
    [Tooltip("Optional static collider representing the doorway area used for local A* tag updates.")]
    [SerializeField] private Collider2D pathTagBoundsCollider;

    [FoldoutGroup("References")]
    [SerializeField] private Transform interactionPoint;

    [FoldoutGroup("References")]
    [SerializeField] private Transform audioOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("Awareness")]
    [SerializeField] private DoorDefaultState defaultState = DoorDefaultState.None;

    [FoldoutGroup("Awareness")]
    [SerializeField] private Transform awarenessSamplePoint;

    [FoldoutGroup("Awareness"), MinValue(MinimumVisibilitySampleInterval), SuffixLabel("s", true)]
    [SerializeField] private float visibilitySampleInterval = 0.1f;

    [FoldoutGroup("State")]
    [SerializeField] private bool startOpen;

    [FoldoutGroup("Animation"), SuffixLabel("deg", true)]
    [SerializeField] private float closedLocalAngle;

    [FoldoutGroup("Animation"), SuffixLabel("deg", true)]
    [SerializeField] private float openAngleOffset = 90f;

    [FoldoutGroup("Animation"), MinValue(MinimumAnimationDuration), SuffixLabel("s", true)]
    [SerializeField] private float animationDuration = 0.28f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease animationEase = Ease.OutCubic;

    [FoldoutGroup("Interaction")]
    [SerializeField] private bool allowPlayerInteraction = true;

    [FoldoutGroup("Interaction")]
    [SerializeField] private bool allowEnemyAutoOpen = true;

    [FoldoutGroup("Interaction")]
    [SerializeField] private string openInteractionLabel = "Open Door";

    [FoldoutGroup("Interaction")]
    [SerializeField] private string closeInteractionLabel = "Close Door";

    [FoldoutGroup("Pathfinding")]
    [SerializeField] private bool updateAstarDoorTags = true;

    [FoldoutGroup("Pathfinding"), Range(0, 31)]
    [SerializeField] private int openPathTag;

    [FoldoutGroup("Pathfinding"), Range(0, 31)]
    [SerializeField] private int closedPathTag = 1;

    [FoldoutGroup("Pathfinding")]
    [SerializeField] private bool flushGraphUpdatesImmediately = true;

    [FoldoutGroup("Pathfinding")]
    [SerializeField] private bool repathEnemiesAfterStateChange = true;

    [FoldoutGroup("SFX"), Title("Open"), InlineProperty, HideLabel]
    [SerializeField] private DoorAudioDefinition openAudio = new();

    [FoldoutGroup("SFX"), Title("Close"), InlineProperty, HideLabel]
    [SerializeField] private DoorAudioDefinition closeAudio = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsOpen => isOpen;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsTransitioning => isTransitioning;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentVisibility => GetCurrentVisibility();

    public static System.Collections.Generic.IReadOnlyList<DoorInteractable> ActiveDoors => ActiveDoorsInternal;
    public DoorDefaultState DefaultStateTag => defaultState;
    public bool HasDefaultStateTag => defaultState != DoorDefaultState.None;
    public bool IsInDefaultState => !HasDefaultStateTag || isOpen == (defaultState == DoorDefaultState.Open);
    public Vector2 AwarenessSamplePosition => awarenessSamplePoint != null ? (Vector2)awarenessSamplePoint.position : (Vector2)InteractionPosition;
    public Bounds AwarenessBounds => ResolveAwarenessBounds();

    public override string InteractionDisplayName =>
        isOpen
            ? (string.IsNullOrWhiteSpace(closeInteractionLabel) ? base.InteractionDisplayName : closeInteractionLabel)
            : (string.IsNullOrWhiteSpace(openInteractionLabel) ? base.InteractionDisplayName : openInteractionLabel);

    public override Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : base.InteractionPosition;

    public event Action<DoorInteractable, bool> DoorStateChanged;

    private Tween rotationTween;
    private bool isOpen;
    private bool isTransitioning;
    private bool pendingTargetOpenState;
    private Bounds closedPathBounds;
    private bool hasClosedPathBounds;
    private float cachedVisibility = 1f;
    private float nextVisibilitySampleTime = float.NegativeInfinity;

    private void Reset()
    {
        ResolveReferences();

        if (doorPivot != null)
            closedLocalAngle = NormalizeAngle(doorPivot.localEulerAngles.z);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!ActiveDoorsInternal.Contains(this))
            ActiveDoorsInternal.Add(this);
    }

    private void Awake()
    {
        ResolveReferences();
        ValidateState();
        CacheClosedPathBounds();
        bool initialOpenState = ResolveInitialOpenState();
        ApplyDoorVisualStateImmediate(initialOpenState);
        isOpen = initialOpenState;
        isTransitioning = false;
        pendingTargetOpenState = initialOpenState;
    }

    private void Start()
    {
        ApplyDoorPathTags(flushGraphUpdates: true, repathEnemies: false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ActiveDoorsInternal.Remove(this);
        rotationTween?.Kill();
        rotationTween = null;
        isTransitioning = false;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        ValidateState();
        ResolveReferences();

        openInteractionLabel = openInteractionLabel != null ? openInteractionLabel.Trim() : string.Empty;
        closeInteractionLabel = closeInteractionLabel != null ? closeInteractionLabel.Trim() : string.Empty;

        if (!Application.isPlaying && doorPivot != null)
            ApplyDoorVisualStateImmediate(ResolveInitialOpenState());
    }

    public override bool CanInteract(GameObject interactorRoot)
    {
        return allowPlayerInteraction &&
               !isTransitioning &&
               base.CanInteract(interactorRoot);
    }

    public bool CanBeAutoOpenedByEnemy(EnemyMovementController enemyMovementController)
    {
        return enemyMovementController != null &&
               allowEnemyAutoOpen &&
               !isTransitioning &&
               !isOpen &&
               IsInteractionEnabled &&
               isActiveAndEnabled;
    }

    public bool TryOpenForEnemy(EnemyMovementController enemyMovementController)
    {
        return TrySetOpen(true, playFeedback: true, enemyMovementController != null ? enemyMovementController.gameObject : null);
    }

    public bool TryRestoreDefaultState(GameObject interactorRoot = null)
    {
        if (!TryResolveDefaultOpenState(out bool shouldBeOpen))
            return false;

        return TrySetOpen(shouldBeOpen, playFeedback: true, interactorRoot);
    }

    public float GetCurrentVisibility(bool forceRefresh = false)
    {
        if (!Application.isPlaying)
            return VisibilityLight2D.EvaluateTotalVisibilityAt(AwarenessSamplePosition, Time.time);

        if (!forceRefresh && Time.time < nextVisibilitySampleTime)
            return cachedVisibility;

        cachedVisibility = VisibilityLight2D.EvaluateTotalVisibilityAt(AwarenessSamplePosition, Time.time);
        nextVisibilitySampleTime = Time.time + visibilitySampleInterval;
        return cachedVisibility;
    }

    public bool TrySetOpen(bool open, bool playFeedback = true, GameObject interactorRoot = null)
    {
        if (isTransitioning || isOpen == open)
            return false;

        pendingTargetOpenState = open;
        isTransitioning = true;

        if (playFeedback)
            PlayDoorFeedback(open);

        rotationTween?.Kill();
        rotationTween = null;

        if (animationDuration <= 0f || doorPivot == null)
        {
            ApplyDoorVisualStateImmediate(open);
            CompleteDoorStateChange(open);
            return true;
        }

        Vector3 localEulerAngles = doorPivot.localEulerAngles;
        localEulerAngles.z = ResolveTargetAngle(open);
        rotationTween = doorPivot
            .DOLocalRotate(localEulerAngles, animationDuration, RotateMode.Fast)
            .SetEase(animationEase)
            .OnComplete(() =>
            {
                rotationTween = null;
                CompleteDoorStateChange(open);
            });
        return true;
    }

    protected override bool Interact(GameObject interactorRoot)
    {
        return TrySetOpen(!isOpen, playFeedback: true, interactorRoot);
    }

    private void ResolveReferences()
    {
        if (doorPivot == null)
            doorPivot = transform;

        if (blockingCollider == null)
            blockingCollider = GetComponentInChildren<Collider2D>();

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;
    }

    private void ValidateState()
    {
        animationDuration = Mathf.Max(MinimumAnimationDuration, animationDuration);
        visibilitySampleInterval = Mathf.Max(MinimumVisibilitySampleInterval, visibilitySampleInterval);
        openAudio ??= new DoorAudioDefinition();
        closeAudio ??= new DoorAudioDefinition();
        openAudio.Validate();
        closeAudio.Validate();
        openPathTag = Mathf.Clamp(openPathTag, 0, 31);
        closedPathTag = Mathf.Clamp(closedPathTag, 0, 31);
    }

    private void CacheClosedPathBounds()
    {
        Collider2D boundsSource = ResolvePathBoundsSource();
        if (boundsSource == null)
        {
            hasClosedPathBounds = false;
            return;
        }

        if (pathTagBoundsCollider != null)
        {
            Physics2D.SyncTransforms();
            closedPathBounds = pathTagBoundsCollider.bounds;
            hasClosedPathBounds = closedPathBounds.size.sqrMagnitude > Mathf.Epsilon;
            return;
        }

        if (doorPivot == null)
        {
            Physics2D.SyncTransforms();
            closedPathBounds = boundsSource.bounds;
            hasClosedPathBounds = closedPathBounds.size.sqrMagnitude > Mathf.Epsilon;
            return;
        }

        Quaternion previousLocalRotation = doorPivot.localRotation;
        ApplyDoorVisualStateImmediate(open: false);
        Physics2D.SyncTransforms();
        closedPathBounds = boundsSource.bounds;
        hasClosedPathBounds = closedPathBounds.size.sqrMagnitude > Mathf.Epsilon;
        doorPivot.localRotation = previousLocalRotation;
        Physics2D.SyncTransforms();
    }

    private Collider2D ResolvePathBoundsSource()
    {
        if (pathTagBoundsCollider != null)
            return pathTagBoundsCollider;

        return blockingCollider;
    }

    private Bounds ResolveAwarenessBounds()
    {
        Collider2D boundsSource = ResolvePathBoundsSource();
        if (boundsSource != null)
            return boundsSource.bounds;

        return new Bounds(transform.position, Vector3.zero);
    }

    private void CompleteDoorStateChange(bool open)
    {
        ApplyDoorVisualStateImmediate(open);
        isOpen = open;
        isTransitioning = false;
        pendingTargetOpenState = open;
        ApplyDoorPathTags(flushGraphUpdatesImmediately, repathEnemiesAfterStateChange);
        DoorStateChanged?.Invoke(this, isOpen);
    }

    private void ApplyDoorVisualStateImmediate(bool open)
    {
        if (doorPivot == null)
            return;

        Vector3 localEulerAngles = doorPivot.localEulerAngles;
        localEulerAngles.z = ResolveTargetAngle(open);
        doorPivot.localEulerAngles = localEulerAngles;
    }

    private float ResolveTargetAngle(bool open)
    {
        return NormalizeAngle(open ? closedLocalAngle + openAngleOffset : closedLocalAngle);
    }

    private bool ResolveInitialOpenState()
    {
        return defaultState switch
        {
            DoorDefaultState.Open => true,
            DoorDefaultState.Closed => false,
            _ => startOpen
        };
    }

    private bool TryResolveDefaultOpenState(out bool shouldBeOpen)
    {
        shouldBeOpen = false;
        switch (defaultState)
        {
            case DoorDefaultState.Open:
                shouldBeOpen = true;
                return true;

            case DoorDefaultState.Closed:
                shouldBeOpen = false;
                return true;

            default:
                return false;
        }
    }

    private void ApplyDoorPathTags(bool flushGraphUpdates, bool repathEnemies)
    {
        if (!Application.isPlaying || !updateAstarDoorTags || !hasClosedPathBounds || AstarPath.active == null)
            return;

        GraphUpdateObject graphUpdate = new(closedPathBounds)
        {
            updatePhysics = false,
            modifyTag = true,
            setTag = isOpen ? openPathTag : closedPathTag
        };

        AstarPath.active.UpdateGraphs(graphUpdate);
        if (flushGraphUpdates)
            AstarPath.active.FlushGraphUpdates();

        if (!repathEnemies)
            return;

        EnemyMovementController[] enemyControllers = FindObjectsByType<EnemyMovementController>(FindObjectsSortMode.None);
        for (int i = 0; i < enemyControllers.Length; i++)
            enemyControllers[i]?.HandleEnvironmentPathingChanged();
    }

    private void PlayDoorFeedback(bool opening)
    {
        DoorAudioDefinition audioDefinition = opening ? openAudio : closeAudio;
        if (audioDefinition == null)
            return;

        Vector3 feedbackPosition = audioOrigin != null ? audioOrigin.position : transform.position;

        if (audioDefinition.NoiseAmount > 0f)
            NoiseManager.EmitNoise(feedbackPosition, audioDefinition.NoiseAmount, audioDefinition.NoiseType, gameObject, audioDefinition.ExtremeNoise);

        if (audioDefinition.Sfx == null || !audioDefinition.Sfx.HasAnyClip)
            return;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        worldSfxManager?.PlayClipSetAt(feedbackPosition, audioDefinition.Sfx, audioDefinition.NoiseType);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
            angle += 360f;

        return angle;
    }
}

}
