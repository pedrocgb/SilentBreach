using System;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

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

    private void Reset()
    {
        ResolveReferences();

        if (doorPivot != null)
            closedLocalAngle = NormalizeAngle(doorPivot.localEulerAngles.z);
    }

    private void Awake()
    {
        ResolveReferences();
        ValidateState();
        CacheClosedPathBounds();
        ApplyDoorVisualStateImmediate(startOpen);
        isOpen = startOpen;
        isTransitioning = false;
        pendingTargetOpenState = startOpen;
    }

    private void Start()
    {
        ApplyDoorPathTags(flushGraphUpdates: true, repathEnemies: false);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
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
            ApplyDoorVisualStateImmediate(startOpen);
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
