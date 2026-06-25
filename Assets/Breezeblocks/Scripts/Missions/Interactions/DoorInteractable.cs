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

public enum DoorOpeningStyle
{
    Normal,
    Upwards
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

        /// <summary>
        /// Validates the authored SFX and noise settings for this door action.
        /// </summary>
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

    [FoldoutGroup("References"), ShowIf(nameof(UsesUpwardsOpeningStyle))]
    [SerializeField] private SpriteRenderer upwardsDoorVisual;

    [FoldoutGroup("References")]
    [SerializeField] private Collider2D blockingCollider;

    [FoldoutGroup("References")]
    [Tooltip("Optional static collider representing the doorway area used for local A* tag updates.")]
    [SerializeField] private Collider2D pathTagBoundsCollider;

    [FoldoutGroup("References")]
    [SerializeField] private Transform interactionPoint;

    [FoldoutGroup("References")]
    [SerializeField] private Transform oppositeInteractionPoint;

    [FoldoutGroup("References")]
    [SerializeField] private Transform audioOrigin;

    [FoldoutGroup("Awareness")]
    [SerializeField] private DoorDefaultState defaultState = DoorDefaultState.None;

    [FoldoutGroup("Awareness")]
    [SerializeField] private Transform awarenessSamplePoint;

    [FoldoutGroup("Awareness"), MinValue(MinimumVisibilitySampleInterval), SuffixLabel("s", true)]
    [SerializeField] private float visibilitySampleInterval = 0.1f;

    [FoldoutGroup("Animation")]
    [SerializeField] private DoorOpeningStyle openingStyle = DoorOpeningStyle.Normal;

    [FoldoutGroup("Animation"), ShowIf(nameof(UsesNormalOpeningStyle)), SuffixLabel("deg", true)]
    [SerializeField] private float closedLocalAngle;

    [FoldoutGroup("Animation"), ShowIf(nameof(UsesNormalOpeningStyle)), SuffixLabel("deg", true)]
    [SerializeField] private float openAngleOffset = 90f;

    [FoldoutGroup("Animation"), MinValue(MinimumAnimationDuration), SuffixLabel("s", true)]
    [SerializeField] private float animationDuration = 0.28f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease animationEase = Ease.OutCubic;

    [FoldoutGroup("Interaction")]
    [SerializeField] private bool allowPlayerInteraction = true;

    [FoldoutGroup("Interaction")]
    [SerializeField] private bool manualOpen = true;

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
    public bool ManualOpen => manualOpen;
    public DoorLockState LockState => doorLockState;
    public bool HasTwoSidedInteractionPoints => interactionPoint != null && oppositeInteractionPoint != null;
    public bool HasDefaultStateTag => defaultState != DoorDefaultState.None;
    public bool IsInDefaultState => !HasDefaultStateTag || isOpen == (defaultState == DoorDefaultState.Open);
    public WorldStateChangeSource LastStateChangeSource => lastStateChangeSource;
    public GameObject LastStateChangeActor => lastStateChangeActor;
    public bool IsPlayerCausedDefaultStateMismatch => HasDefaultStateTag && !IsInDefaultState && lastStateChangeSource == WorldStateChangeSource.Player;
    public Vector2 AwarenessSamplePosition => awarenessSamplePoint != null ? (Vector2)awarenessSamplePoint.position : (Vector2)InteractionPosition;
    public Bounds AwarenessBounds => ResolveAwarenessBounds();

    public override string InteractionDisplayName =>
        doorLockState != null && doorLockState.IsLocked
            ? doorLockState.GetPlayerLockedInteractionDisplayName(null)
            : isOpen
                ? (string.IsNullOrWhiteSpace(closeInteractionLabel) ? base.InteractionDisplayName : closeInteractionLabel)
                : (string.IsNullOrWhiteSpace(openInteractionLabel) ? base.InteractionDisplayName : openInteractionLabel);

    public override Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : base.InteractionPosition;

    public event Action<DoorInteractable, bool> DoorStateChanged;

    private Tween doorAnimationTween;
    private bool isOpen;
    private bool isTransitioning;
    private Bounds closedPathBounds;
    private bool hasClosedPathBounds;
    private float cachedVisibility = 1f;
    private float nextVisibilitySampleTime = float.NegativeInfinity;
    private DoorLockState doorLockState;
    private WorldSfxManager worldSfxManager;
    private WorldStateChangeSource lastStateChangeSource = WorldStateChangeSource.System;
    private WorldStateChangeSource pendingStateChangeSource = WorldStateChangeSource.System;
    private GameObject lastStateChangeActor;
    private GameObject pendingStateChangeActor;

    /// <summary>
    /// Caches nearby authoring references and records the currently authored closed angle when reset.
    /// </summary>
    private void Reset()
    {
        ResolveReferences();

        if (doorPivot != null)
            closedLocalAngle = NormalizeAngle(doorPivot.localEulerAngles.z);
    }

    /// <summary>
    /// Registers the door in the runtime discovery list when it becomes active.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();

        if (!ActiveDoorsInternal.Contains(this))
            ActiveDoorsInternal.Add(this);
    }

    /// <summary>
    /// Caches runtime references, validates state, and applies the authored initial door state.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        ValidateState();
        CacheClosedPathBounds();
        bool initialOpenState = ResolveInitialOpenState();
        ApplyDoorVisualStateImmediate(initialOpenState);
        ApplyBlockingColliderState(initialOpenState);
        isOpen = initialOpenState;
        isTransitioning = false;
    }

    /// <summary>
    /// Applies the initial A* door tags once all runtime references are ready.
    /// </summary>
    private void Start()
    {
        ApplyDoorPathTags(flushGraphUpdates: true, repathEnemies: false);
    }

    /// <summary>
    /// Unregisters the door and stops any in-flight animation when it is disabled.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        ActiveDoorsInternal.Remove(this);
        doorAnimationTween?.Kill();
        doorAnimationTween = null;
        isTransitioning = false;
    }

    /// <summary>
    /// Clamps authoring values, trims labels, and previews the current default state in the editor.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        ValidateState();
        ResolveReferences();

        openInteractionLabel = openInteractionLabel != null ? openInteractionLabel.Trim() : string.Empty;
        closeInteractionLabel = closeInteractionLabel != null ? closeInteractionLabel.Trim() : string.Empty;

        if (!Application.isPlaying && doorPivot != null)
        {
            bool resolvedInitialOpenState = ResolveInitialOpenState();
            ApplyDoorVisualStateImmediate(resolvedInitialOpenState);
            ApplyBlockingColliderState(resolvedInitialOpenState);
        }
    }

    /// <summary>
    /// Resolves the door prompt label for the specific player standing near the door.
    /// </summary>
    public override string GetInteractionDisplayName(GameObject interactorRoot)
    {
        if (doorLockState != null && doorLockState.IsLocked)
            return doorLockState.GetPlayerLockedInteractionDisplayName(interactorRoot);

        return InteractionDisplayName;
    }

    /// <summary>
    /// Returns the closest configured door-side interaction point to the player.
    /// </summary>
    public override Vector3 GetClosestInteractionPosition(Vector3 origin)
    {
        Vector3 primaryPosition = interactionPoint != null ? interactionPoint.position : base.InteractionPosition;
        if (oppositeInteractionPoint == null)
            return primaryPosition;

        Vector3 oppositePosition = oppositeInteractionPoint.position;
        float primaryDistanceSqr = ((Vector2)(primaryPosition - origin)).sqrMagnitude;
        float oppositeDistanceSqr = ((Vector2)(oppositePosition - origin)).sqrMagnitude;
        return oppositeDistanceSqr < primaryDistanceSqr ? oppositePosition : primaryPosition;
    }

    /// <summary>
    /// Returns the side point the actor should approach before operating the door.
    /// </summary>
    public Vector2 GetApproachSidePosition(Vector2 actorPosition)
    {
        return (Vector2)GetClosestInteractionPosition(actorPosition);
    }

    /// <summary>
    /// Returns the side point opposite the actor so AI can move through after opening the door.
    /// </summary>
    public Vector2 GetExitSidePosition(Vector2 actorPosition)
    {
        Vector2 primaryPosition = interactionPoint != null ? (Vector2)interactionPoint.position : (Vector2)base.InteractionPosition;
        if (oppositeInteractionPoint != null)
        {
            Vector2 oppositePosition = (Vector2)oppositeInteractionPoint.position;
            float primaryDistanceSqr = (primaryPosition - actorPosition).sqrMagnitude;
            float oppositeDistanceSqr = (oppositePosition - actorPosition).sqrMagnitude;
            return oppositeDistanceSqr < primaryDistanceSqr ? primaryPosition : oppositePosition;
        }

        if (TryResolveBoundsSidePosition(actorPosition, oppositeSide: true, out Vector2 boundsExitPosition))
            return boundsExitPosition;

        return primaryPosition;
    }

    /// <summary>
    /// Returns an approach point from bounds when explicit side points are incomplete.
    /// </summary>
    public bool TryGetBoundsApproachSidePosition(Vector2 actorPosition, out Vector2 sidePosition)
    {
        return TryResolveBoundsSidePosition(actorPosition, oppositeSide: false, out sidePosition);
    }

    /// <summary>
    /// Returns whether the player may currently interact with this door or start its lockpick flow.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        if (!allowPlayerInteraction || isTransitioning || !base.CanInteract(interactorRoot))
            return false;

        return doorLockState == null ||
               !doorLockState.IsLocked ||
               doorLockState.CanPlayerAttemptUnlock(interactorRoot) ||
               doorLockState.CanPlayerInspectLockedState(interactorRoot);
    }

    /// <summary>
    /// Returns whether the supplied enemy may auto-open this door based on door state and lock overrides.
    /// </summary>
    public bool CanBeAutoOpenedByEnemy(EnemyMovementController enemyMovementController)
    {
        return enemyMovementController != null &&
               allowEnemyAutoOpen &&
               !isTransitioning &&
               !isOpen &&
               CanActorOperateDoor(enemyMovementController.gameObject) &&
               IsInteractionEnabled &&
               isActiveAndEnabled;
    }

    /// <summary>
    /// Attempts to open this door for the supplied enemy when auto-open rules allow it.
    /// </summary>
    public bool TryOpenForEnemy(EnemyMovementController enemyMovementController)
    {
        return TrySetOpen(true, playFeedback: true, enemyMovementController != null ? enemyMovementController.gameObject : null);
    }

    /// <summary>
    /// Attempts to close this door for the supplied enemy after it has traversed the doorway.
    /// </summary>
    public bool TryCloseForEnemy(EnemyMovementController enemyMovementController)
    {
        return TrySetOpen(false, playFeedback: true, enemyMovementController != null ? enemyMovementController.gameObject : null);
    }

    /// <summary>
    /// Restores the door to its authored default state when one exists and the actor may operate the door.
    /// </summary>
    public bool TryRestoreDefaultState(GameObject interactorRoot = null)
    {
        if (!TryResolveDefaultOpenState(out bool shouldBeOpen))
            return false;

        return TrySetOpen(shouldBeOpen, playFeedback: true, interactorRoot);
    }

    /// <summary>
    /// Returns the currently sampled light visibility for this door's awareness point.
    /// </summary>
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

    /// <summary>
    /// Attempts to change the door state, respecting lock rules before animating or applying feedback.
    /// </summary>
    public bool TrySetOpen(bool open, bool playFeedback = true, GameObject interactorRoot = null)
    {
        if (isTransitioning || isOpen == open || !CanActorOperateDoor(interactorRoot))
            return false;

        isTransitioning = true;
        pendingStateChangeActor = interactorRoot;
        pendingStateChangeSource = ResolveStateChangeSource(interactorRoot);
        // Keep the blocking collider disabled during close animation so moving actors are not pushed.
        ApplyBlockingColliderState(open: true);

        if (playFeedback)
            PlayDoorFeedback(open, interactorRoot);

        doorAnimationTween?.Kill();
        doorAnimationTween = null;

        if (animationDuration <= 0f || !CanAnimateDoorVisual())
        {
            ApplyDoorVisualStateImmediate(open);
            CompleteDoorStateChange(open);
            return true;
        }

        if (openingStyle == DoorOpeningStyle.Upwards)
        {
            doorAnimationTween = DOVirtual
                .Float(upwardsDoorVisual.color.a, open ? 0f : 1f, animationDuration, ApplyUpwardsDoorAlpha)
                .SetEase(animationEase)
                .OnComplete(() =>
                {
                    doorAnimationTween = null;
                    CompleteDoorStateChange(open);
                });
            return true;
        }

        Vector3 localEulerAngles = doorPivot.localEulerAngles;
        localEulerAngles.z = ResolveTargetAngle(open);
        doorAnimationTween = doorPivot
            .DOLocalRotate(localEulerAngles, animationDuration, RotateMode.Fast)
            .SetEase(animationEase)
            .OnComplete(() =>
            {
                doorAnimationTween = null;
                CompleteDoorStateChange(open);
            });
        return true;
    }

    /// <summary>
    /// Starts lockpicking while locked, otherwise toggles the door between open and closed.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        if (doorLockState != null && doorLockState.IsLocked)
        {
            if (doorLockState.CanPlayerAttemptUnlock(interactorRoot))
                return doorLockState.TryBeginLockpick(interactorRoot);

            InteractionPromptFeedback feedback = doorLockState.CreateBlockedAttemptFeedback();
            RequestInteractionFeedback(feedback);
            doorLockState.PlayBlockedAttemptWorldFeedback(GetClosestInteractionPosition(interactorRoot != null ? interactorRoot.transform.position : InteractionPosition), gameObject);
            return false;
        }

        if (!manualOpen)
            return false;

        return TrySetOpen(!isOpen, playFeedback: true, interactorRoot);
    }

    /// <summary>
    /// Resolves optional local references and same-object dependencies used by the door.
    /// </summary>
    private void ResolveReferences()
    {
        if (doorPivot == null)
            doorPivot = transform;

        if (blockingCollider == null)
            blockingCollider = GetComponentInChildren<Collider2D>();

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        doorLockState = GetComponent<DoorLockState>();
    }

    /// <summary>
    /// Clamps authored values and validates the nested feedback definitions.
    /// </summary>
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

    /// <summary>
    /// Caches the doorway bounds used to apply local A* graph tag updates for open and closed states.
    /// </summary>
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

        if (doorPivot == null || openingStyle == DoorOpeningStyle.Upwards)
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

    /// <summary>
    /// Resolves the collider used as the source for local pathfinding updates.
    /// </summary>
    private Collider2D ResolvePathBoundsSource()
    {
        if (pathTagBoundsCollider != null)
            return pathTagBoundsCollider;

        return blockingCollider;
    }

    /// <summary>
    /// Resolves the runtime awareness bounds used for room-door visibility checks.
    /// </summary>
    private Bounds ResolveAwarenessBounds()
    {
        Collider2D boundsSource = ResolvePathBoundsSource();
        if (boundsSource != null)
            return boundsSource.bounds;

        return new Bounds(transform.position, Vector3.zero);
    }

    /// <summary>
    /// Resolves a side point from doorway bounds when explicit front/back points are unavailable.
    /// </summary>
    private bool TryResolveBoundsSidePosition(Vector2 actorPosition, bool oppositeSide, out Vector2 sidePosition)
    {
        sidePosition = (Vector2)InteractionPosition;
        Bounds bounds = ResolveAwarenessBounds();
        if (bounds.size.sqrMagnitude <= Mathf.Epsilon)
            return false;

        Vector2 center = (Vector2)bounds.center;
        Vector2 direction = actorPosition - center;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = (Vector2)transform.up;

        direction.Normalize();
        if (oppositeSide)
            direction = -direction;

        float probeDistance = Mathf.Max(bounds.extents.x, bounds.extents.y) + 0.25f;
        sidePosition = center + (direction * probeDistance);
        return true;
    }

    /// <summary>
    /// Finalizes the new door state, refreshes pathfinding, and notifies any listeners or prompt UI.
    /// </summary>
    private void CompleteDoorStateChange(bool open)
    {
        ApplyDoorVisualStateImmediate(open);
        ApplyBlockingColliderState(open);
        isOpen = open;
        isTransitioning = false;
        lastStateChangeActor = pendingStateChangeActor;
        lastStateChangeSource = pendingStateChangeSource;
        pendingStateChangeActor = null;
        pendingStateChangeSource = WorldStateChangeSource.System;
        ApplyDoorPathTags(flushGraphUpdatesImmediately, repathEnemiesAfterStateChange);
        DoorStateChanged?.Invoke(this, isOpen);
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Applies the requested visual state immediately without tweening.
    /// </summary>
    private void ApplyDoorVisualStateImmediate(bool open)
    {
        if (openingStyle == DoorOpeningStyle.Upwards)
        {
            if (doorPivot != null)
            {
                Vector3 closedEulerAngles = doorPivot.localEulerAngles;
                closedEulerAngles.z = ResolveTargetAngle(open: false);
                doorPivot.localEulerAngles = closedEulerAngles;
            }

            ApplyUpwardsDoorAlpha(open ? 0f : 1f);
            return;
        }

        ApplyUpwardsDoorAlpha(1f);
        if (doorPivot == null)
            return;

        Vector3 localEulerAngles = doorPivot.localEulerAngles;
        localEulerAngles.z = ResolveTargetAngle(open);
        doorPivot.localEulerAngles = localEulerAngles;
    }

    /// <summary>
    /// Applies an exact alpha to the visual used by upwards-style doors.
    /// </summary>
    private void ApplyUpwardsDoorAlpha(float alpha)
    {
        if (upwardsDoorVisual == null)
            return;

        Color color = upwardsDoorVisual.color;
        color.a = Mathf.Clamp01(alpha);
        upwardsDoorVisual.color = color;
    }

    /// <summary>
    /// Returns whether the selected opening style has the visual reference needed for animation.
    /// </summary>
    private bool CanAnimateDoorVisual()
    {
        return openingStyle == DoorOpeningStyle.Upwards
            ? upwardsDoorVisual != null
            : doorPivot != null;
    }

    /// <summary>
    /// Returns whether Odin should expose normal-style rotation settings.
    /// </summary>
    private bool UsesNormalOpeningStyle()
    {
        return openingStyle == DoorOpeningStyle.Normal;
    }

    /// <summary>
    /// Returns whether Odin should expose upwards-style visual references.
    /// </summary>
    private bool UsesUpwardsOpeningStyle()
    {
        return openingStyle == DoorOpeningStyle.Upwards;
    }

    /// <summary>
    /// Toggles the gameplay blocking collider to match the open or closed door state.
    /// </summary>
    private void ApplyBlockingColliderState(bool open)
    {
        if (blockingCollider == null)
            return;

        blockingCollider.enabled = !open;
    }

    /// <summary>
    /// Resolves the local Z angle that corresponds to the requested open or closed state.
    /// </summary>
    private float ResolveTargetAngle(bool open)
    {
        return NormalizeAngle(open ? closedLocalAngle + openAngleOffset : closedLocalAngle);
    }

    /// <summary>
    /// Resolves initial open state from the lock setup, authored default-state tags, or the current configured door rotation.
    /// </summary>
    private bool ResolveInitialOpenState()
    {
        if (doorLockState != null && doorLockState.StartsLocked)
            return false;

        return defaultState switch
        {
            DoorDefaultState.Open => true,
            DoorDefaultState.Closed => false,
            _ => ResolveConfiguredOpenState()
        };
    }

    /// <summary>
    /// Infers initial open state from the authored door-pivot rotation when no default-state tag is assigned.
    /// </summary>
    private bool ResolveConfiguredOpenState()
    {
        if (openingStyle == DoorOpeningStyle.Upwards && upwardsDoorVisual != null)
            return upwardsDoorVisual.color.a <= 0.5f;

        if (doorPivot == null)
            return false;

        float authoredAngle = NormalizeAngle(doorPivot.localEulerAngles.z);
        float closedAngleDelta = Mathf.Abs(Mathf.DeltaAngle(authoredAngle, NormalizeAngle(closedLocalAngle)));
        float openAngleDelta = Mathf.Abs(Mathf.DeltaAngle(authoredAngle, ResolveTargetAngle(open: true)));
        return openAngleDelta < closedAngleDelta;
    }

    /// <summary>
    /// Resolves the default open state from the authored awareness tag when one is assigned.
    /// </summary>
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

    /// <summary>
    /// Returns whether the supplied actor may currently operate this door based on its locked state.
    /// </summary>
    private bool CanActorOperateDoor(GameObject actorRoot)
    {
        return doorLockState == null || !doorLockState.IsLocked || doorLockState.CanActorBypassLockedState(actorRoot);
    }

    /// <summary>
    /// Classifies the actor that requested a door state change for room-awareness filtering.
    /// </summary>
    private static WorldStateChangeSource ResolveStateChangeSource(GameObject actorRoot)
    {
        if (actorRoot == null)
            return WorldStateChangeSource.System;

        return actorRoot.GetComponentInParent<EnemyMovementController>() != null
            ? WorldStateChangeSource.Enemy
            : WorldStateChangeSource.Player;
    }

    /// <summary>
    /// Applies the door's local A* graph tag update and optionally repaths active enemies afterward.
    /// </summary>
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

    /// <summary>
    /// Plays door SFX and emits matching world noise for the requested open or close action.
    /// </summary>
    private void PlayDoorFeedback(bool opening, GameObject interactorRoot)
    {
        DoorAudioDefinition audioDefinition = opening ? openAudio : closeAudio;
        if (audioDefinition == null)
            return;

        Vector3 feedbackPosition = audioOrigin != null ? audioOrigin.position : transform.position;
        GameObject noiseSource = interactorRoot != null ? interactorRoot : gameObject;

        if (audioDefinition.NoiseAmount > 0f)
            NoiseManager.EmitNoise(feedbackPosition, audioDefinition.NoiseAmount, audioDefinition.NoiseType, noiseSource, audioDefinition.ExtremeNoise);

        if (audioDefinition.Sfx == null || !audioDefinition.Sfx.HasAnyClip)
            return;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        worldSfxManager?.PlayClipSetAt(feedbackPosition, audioDefinition.Sfx, audioDefinition.NoiseType);
    }

    /// <summary>
    /// Normalizes an angle into the 0-to-360 degree range.
    /// </summary>
    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
            angle += 360f;

        return angle;
    }
}

}
