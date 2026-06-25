using System;
using System.Collections.Generic;
using Breezeblocks.Missions;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;

public enum EnemyState
{
    Idle,
    Patrol,
    Suspicious,
    Searching,
    LookAround,
    ReturningToStart,
    Detected,
    Fleeing,
    Disabled,
    Alert,
    Sleeping
}

public enum EnemyPatrolMode
{
    Loop,
    PingPong,
    Random
}

public enum EnemyDetectionBehavior
{
    ChasePlayer,
    FleeToPoint,
    StandStill,
    CustomOnly
}

public enum EnemySpeedType
{
    Walk,
    Run,
    Sprint
}

public enum EnemyFacingMode
{
    None,
    UseTransformRotation,
    CustomAngle
}

public enum EnemyItineraryStepType
{
    Idle,
    Patrol
}

public enum EnemyItineraryPatrolCompletionMode
{
    FixedDuration,
    CompleteLoop
}

[Serializable]
public class EnemyFacingSettings
{
    [LabelText("Facing"), EnumToggleButtons]
    public EnemyFacingMode FacingMode = EnemyFacingMode.None;

    [ShowIf(nameof(UsesCustomAngle)), Range(0f, 360f)]
    public float CustomAngle;

    public bool HasFacingOverride => FacingMode != EnemyFacingMode.None;

    private bool UsesCustomAngle => FacingMode == EnemyFacingMode.CustomAngle;

    /// <summary>
    /// Resolves the configured facing into a world-space Z angle.
    /// </summary>
    public bool TryResolveAngle(Transform referenceTransform, float fallbackAngle, out float resolvedAngle)
    {
        resolvedAngle = Mathf.Repeat(fallbackAngle, 360f);

        switch (FacingMode)
        {
            case EnemyFacingMode.None:
                return false;

            case EnemyFacingMode.UseTransformRotation:
                resolvedAngle = referenceTransform != null ? referenceTransform.eulerAngles.z : Mathf.Repeat(fallbackAngle, 360f);
                return true;

            case EnemyFacingMode.CustomAngle:
                resolvedAngle = Mathf.Repeat(CustomAngle, 360f);
                return true;

            default:
                return false;
        }
    }
}

[Serializable]
public class PatrolPoint
{
    [Required]
    public Transform Point;

    [MinValue(0f), SuffixLabel("s", true)]
    public float WaitDuration;

    public bool LookAroundAtPoint;

    [ShowIf(nameof(LookAroundAtPoint)), MinValue(0f), SuffixLabel("s", true)]
    public float LookAroundDuration = 2f;

    [ShowIf(nameof(LookAroundAtPoint)), MinValue(0f), SuffixLabel("s", true)]
    public float LookAroundTurnInterval = 0.5f;

    [InlineProperty, LabelText("Arrival Facing")]
    public EnemyFacingSettings ArrivalFacing = new();
}

[Serializable]
public class EnemyItineraryStep
{
    [HorizontalGroup("Header"), HideLabel]
    public string StepName = "Itinerary Step";

    [HorizontalGroup("Header"), HideLabel, EnumToggleButtons]
    public EnemyItineraryStepType StepType = EnemyItineraryStepType.Idle;

    [FoldoutGroup("Idle"), ShowIf(nameof(IsIdleStep))]
    public Transform IdlePoint;

    [FoldoutGroup("Idle"), ShowIf(nameof(IsIdleStep)), MinValue(0f), SuffixLabel("s", true)]
    public float IdleDuration = 10f;

    [FoldoutGroup("Idle"), ShowIf(nameof(IsIdleStep)), InlineProperty, LabelText("Idle Facing")]
    public EnemyFacingSettings IdleFacing = new();

    [FoldoutGroup("Patrol"), ShowIf(nameof(IsPatrolStep))]
    public bool UseControllerPatrolRoute = true;

    [FoldoutGroup("Patrol"), ShowIf(nameof(IsPatrolStep)), EnumToggleButtons]
    public EnemyPatrolMode PatrolMode = EnemyPatrolMode.Loop;

    [FoldoutGroup("Patrol"), ShowIf(nameof(IsPatrolStep)), EnumToggleButtons]
    public EnemyItineraryPatrolCompletionMode PatrolCompletionMode = EnemyItineraryPatrolCompletionMode.FixedDuration;

    [FoldoutGroup("Patrol"), ShowIf("@IsPatrolStep && PatrolCompletionMode == EnemyItineraryPatrolCompletionMode.FixedDuration"), MinValue(0f), SuffixLabel("s", true)]
    public float PatrolDuration = 15f;

    [FoldoutGroup("Patrol"), ShowIf("@IsPatrolStep && !UseControllerPatrolRoute")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    public List<PatrolPoint> PatrolPoints = new();

    private bool IsIdleStep => StepType == EnemyItineraryStepType.Idle;
    private bool IsPatrolStep => StepType == EnemyItineraryStepType.Patrol;
}

internal enum EnemyLookAroundContext
{
    None,
    Patrol,
    Suspicious,
    Searching,
    LostTarget,
    Manual,
    DoorBell
}

internal enum EnemyReturnContext
{
    None,
    StartingState,
    ItineraryStep
}

internal enum AutoDoorTraversalPhase
{
    None,
    Approach,
    Exit
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Movement Controller")]
public partial class EnemyMovementController : MonoBehaviour
{
    private sealed class AutoDoorTraversalRecord
    {
        public DoorInteractable Door;
        public DoorLockState LockState;
        public bool ShouldRelock;
        public bool CloseRequested;
        public Vector2 DoorPosition;
        public Vector2 PassDirection;
        public int InitialSideSign;
        public bool HasCrossedDoorPlane;
        public float CloseAllowedTime;
    }

    private const float MinimumSpeed = 0f;
    private const float MinimumAcceleration = 0f;
    private const float MinimumDistance = 0.01f;
    private const float MinimumInterval = 0.02f;
    private const float MinimumDirectionSqr = 0.0001f;
    private const float DestinationRefreshSqrDistance = 0.0025f;
    private const float AstarAccelerationOverride = 9999f;
    private const float MinimumDoorAutoOpenRange = 0.05f;
    private const float MinimumDoorAutoOpenRadius = 0.01f;
    private const float MinimumDoorRouteProbeWidth = 0.05f;

    [FoldoutGroup("References"), ShowInInspector, ReadOnly]
    [Tooltip("Runtime-cached Rigidbody2D on this GameObject.")]
    private Rigidbody2D movementBody;

    [FoldoutGroup("A* Pathfinding"), ShowInInspector, ReadOnly]
    [Tooltip("Runtime-cached AIPath used as the low-level A* movement driver.")]
    private AIPath aiPath;

    [FoldoutGroup("A* Pathfinding"), ShowInInspector, ReadOnly]
    private AIDestinationSetter aiDestinationSetter;

    [FoldoutGroup("A* Pathfinding"), ShowInInspector, ReadOnly]
    private Seeker seeker;

    private bool allowClosedDoorTraversalWhilePatrol = true;
    private bool allowClosedDoorTraversalWhileAlert = true;
    private bool allowClosedDoorTraversalWhileSuspicious = true;
    private bool allowClosedDoorTraversalWhileSearching = true;
    private bool allowClosedDoorTraversalWhileReturningToStart = true;
    private bool allowClosedDoorTraversalWhileFleeing = true;
    private bool allowClosedDoorTraversalWhileDetected;
    private int closedDoorPathTag = 1;
    private int closedDoorTagPenalty;
    private int closedDoorPatrolTagPenalty;
    private LayerMask doorDetectionMask = Physics2D.AllLayers;
    private float doorAutoOpenRange = 0.9f;
    private float doorAutoOpenRadius = 0.18f;
    private float doorAutoOpenCooldown = 0.2f;
    private float doorPreferredRouteProbeDistance = 3f;
    private float doorPreferredRouteProbeWidth = 0.75f;
    private bool closeDoorsAfterPassing;
    private bool relockIgnoredLockedDoorsAfterPassing;
    private float doorCloseAfterPassDistance = 0.75f;
    private float doorCloseAfterOpenDelay = 0.25f;

    private EnemyState startingState = EnemyState.Idle;
    private EnemyFacingSettings startingPointFacing = new();
    private float walkSpeed = 1.5f;
    private float runSpeed = 3.25f;
    private float sprintSpeed = 5f;
    private float acceleration = 10f;
    private float deceleration = 14f;
    private float stoppingDistance = 0.2f;
    private float slowdownDistance = 0.8f;
    private float minimumMoveSpeed = 0.05f;
    private bool useCustomRotation = true;
    private float rotationSpeed = 360f;
    private float rotationAngleOffset = -90f;
    private bool faceMovementDirection = true;
    private bool faceTargetWhenDetected = true;
    private bool preferPathSteeringDirection = true;
    private bool lockRotationWhenIdle = true;
    private EnemyPatrolMode patrolMode = EnemyPatrolMode.Loop;

    [FoldoutGroup("Patrol")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<PatrolPoint> patrolPoints = new();

    private bool returnToStartAfterTemporaryStates = true;
    private bool investigate = true;
    private EnemySpeedType returnToStartSpeedType = EnemySpeedType.Walk;
    private bool enterAlertStateWhenTargetLost = true;
    private bool alertChaseTarget = true;
    private float alertNoiseFocusDuration = 2f;
    private float alertTargetLostDuration = 3f;
    private float defaultLookAroundDuration = 2.5f;
    private float lookAroundTurnInterval = 0.5f;
    private float lookAroundRotationSpeed = 360f;
    private float randomLookAngleRange = 180f;
    private bool useItinerary;
    private bool loopItinerary = true;

    [FoldoutGroup("Itinerary"), ShowIf(nameof(useItinerary))]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<EnemyItineraryStep> itinerarySteps = new();

    private EnemyDetectionBehavior detectionBehavior = EnemyDetectionBehavior.ChasePlayer;
    private bool searchLastKnownTargetPositionWhenTargetLost = true;
    private EnemyDetectionBehavior missingFleePointFallbackBehavior = EnemyDetectionBehavior.StandStill;
    private bool canFlee = true;

    [FoldoutGroup("Fleeing"), ShowIf(nameof(ShouldShowFleeSettings))]
    [SerializeField] private Transform fleePoint;

    [FoldoutGroup("Fleeing"), ShowIf(nameof(ShouldShowFleeSettings))]
    [InlineProperty, LabelText("Flee Facing")]
    [SerializeField] private EnemyFacingSettings fleePointFacing = new();

    [FoldoutGroup("Alert")]
    [Tooltip("Optional point this enemy will hold while alert. If empty, the enemy holds its current position.")]
    [SerializeField] private Transform alertHoldPoint;

    [FoldoutGroup("Alert")]
    [InlineProperty, LabelText("Alert Facing")]
    [SerializeField] private EnemyFacingSettings alertFacing = new();

    private bool stayAtFleePointForever = true;
    private float fleeStoppingDistance = 0.2f;
    private bool disableHearingAfterFlee = true;
    private bool disableVisionAfterFlee;
    private bool useMovePosition = true;
    private bool useVelocityMovement;
    private bool applyRecommendedRigidbodySettings = true;
    private bool forceZeroGravity = true;
    private RigidbodyInterpolation2D recommendedInterpolation = RigidbodyInterpolation2D.Interpolate;
    private CollisionDetectionMode2D recommendedCollisionDetection = CollisionDetectionMode2D.Continuous;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool debugMovement;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemyState CurrentState => currentState;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemyState PreviousState => previousState;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemyState StartingState => startingState;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentTargetPosition => ResolveCurrentTargetPosition();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentMovementSpeed => currentMovementSpeed;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentSpeedCap => currentSpeedCap;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsMoving => isMoving;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasReachedDestination => hasReachedDestination;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentPatrolIndex => currentPatrolIndex;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float PatrolWaitTimer => patrolWaiting ? Mathf.Max(0f, patrolWaitUntil - Time.time) : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float CurrentLookAroundTimer => currentState == EnemyState.LookAround ? Mathf.Max(0f, lookAroundEndTime - Time.time) : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentLookDirection => currentLookDirection;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentFacingDirection => ResolveCurrentFacingDirection();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentMovementVector => ResolveMovementVector();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 StartingPosition => Application.isPlaying ? startingPosition : CurrentPosition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float StartingRotation => Application.isPlaying ? startingRotation : CurrentRotation;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Transform DetectedTarget => detectedTarget;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool UsingItinerary => ShouldUseItinerary;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentItineraryIndex => currentItineraryIndex;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string CurrentItineraryStepName => TryGetCurrentItineraryStep(out EnemyItineraryStep step) ? step.StepName : string.Empty;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float CurrentItineraryStepRemainingTime => itineraryStepRemainingTime;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsItineraryPaused => DetermineIsItineraryPaused();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasExternalInvestigation => hasExternalInvestigation;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAlertState => currentState == EnemyState.Alert;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float AlertNoiseFocusTimeRemaining => currentState == EnemyState.Alert && alertHasNoiseFocus
        ? Mathf.Max(0f, alertNoiseFocusUntil - Time.time)
        : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float AlertStimulusTimeRemaining => currentState == EnemyState.Alert
        ? Mathf.Max(0f, alertStimulusUntil - Time.time)
        : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool AlertChaseTargetEnabled => alertChaseTarget;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasAlertTrackedTarget => currentState == EnemyState.Alert && detectedTarget != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDoorBellReactionActive => doorBellReactionActive || currentLookAroundContext == EnemyLookAroundContext.DoorBell;

    [FoldoutGroup("A* Pathfinding")]
    [InfoBox("When AIPath is assigned, it remains the low-level path steering and Rigidbody2D mover. This controller owns the high-level state, destination, speed caps, and custom rotation.")]
    [ShowInInspector, ReadOnly]
    private bool UsingAstarDriver => aiPath != null;

    [SerializeField, HideInInspector] private EnemyState currentState;
    [SerializeField, HideInInspector] private EnemyState previousState;
    [SerializeField, HideInInspector] private float currentMovementSpeed;
    [SerializeField, HideInInspector] private float currentSpeedCap;
    [SerializeField, HideInInspector] private bool isMoving;
    [SerializeField, HideInInspector] private bool hasReachedDestination;
    [SerializeField, HideInInspector] private Vector2 startingPosition;
    [SerializeField, HideInInspector] private float startingRotation;

    public event Action<EnemyState, EnemyState> StateChanged;

    private EnemyLookAroundContext currentLookAroundContext;
    private EnemyReturnContext currentReturnContext;
    private Transform detectedTarget;
    private Vector2 currentDestination;
    private Vector2 lastKnownTargetPosition;
    private Vector2 currentLookDirection = Vector2.up;
    private Vector2 manualFacingDirection = Vector2.up;
    private Vector2 externalFacingDirection = Vector2.up;
    private float patrolWaitUntil;
    private float lookAroundEndTime;
    private float nextLookAroundTurnTime;
    private float activeLookAroundTurnInterval;
    private float itineraryStepRemainingTime;
    private int currentPatrolIndex;
    private int patrolDirection = 1;
    private int currentItineraryIndex = -1;
    private int itineraryRandomPatrolVisitCount;
    private bool hasDestination;
    private bool patrolWaiting;
    private bool fleeCompleted;
    private bool warnedMissingMover;
    private bool warnedAstarWithoutRigidbody;
    private bool startupCompleted;
    private bool hasManualFacingOverride;
    private bool hasExternalFacingOverride;
    private bool hasDetectedMovementOverride;
    private bool itineraryPatrolCompletionPending;
    private bool itineraryFinished;
    private Vector2 lastStableFacingDirection = Vector2.up;
    private EnemySpeedType detectedMovementOverrideSpeedType = EnemySpeedType.Sprint;
    private float externalTurnSpeedOverride = -1f;
    private bool hasExternalTurnSpeedOverride;
    private bool staggerOverrideActive;
    private float staggeredMoveSpeedOverride;
    private float staggerTurnSpeedMultiplier = 1f;
    private bool hasExternalInvestigation;
    private EnemyState externalInvestigationState = EnemyState.Suspicious;
    private bool reactToDoorBell = true;
    private int doorBellReactionsBeforeAlert = 2;
    private float doorBellRepeatIgnoreDuration = 1.5f;
    private EnemySpeedType doorBellReactionSpeed = EnemySpeedType.Walk;
    private float doorBellStandDuration = 1f;
    private float doorBellLookAroundDuration = 2.5f;
    private float doorBellLookAroundTurnInterval = 0.5f;
    private Breezeblocks.Missions.DoorBellInteractable activeDoorBell;
    private bool doorBellReactionActive;
    private bool doorBellWaitingAtTarget;
    private float doorBellStandUntil = float.NegativeInfinity;
    private readonly Dictionary<Breezeblocks.Missions.DoorBellInteractable, int> doorBellReactionCounts = new();
    private readonly Dictionary<Breezeblocks.Missions.DoorBellInteractable, float> doorBellNextAllowedTimes = new();
    private float stationarySuspicionUntil = float.NegativeInfinity;
    private bool alertHasNoiseFocus;
    private float alertNoiseFocusUntil = float.NegativeInfinity;
    private float alertDefaultFacingAngle;
    private Vector2 alertNoiseFocusPoint;
    private float alertStimulusUntil = float.NegativeInfinity;
    private int defaultTraversableTags = -1;
    private int[] defaultTagPenalties;
    private bool doorTraversalPreferencesInitialized;
    private bool doorTraversalPreferenceDirty = true;
    private float nextDoorAutoOpenTime;
    private DoorInteractable activeAutoDoor;
    private AutoDoorTraversalPhase activeAutoDoorPhase = AutoDoorTraversalPhase.None;
    private Vector2 activeAutoDoorApproachPosition;
    private Vector2 activeAutoDoorExitPosition;
    private readonly List<AutoDoorTraversalRecord> pendingAutoDoorTraversals = new();
    private readonly Dictionary<DoorInteractable, float> recentlyClosedAutoDoorCooldowns = new();
    private readonly RaycastHit2D[] doorAutoOpenHits = new RaycastHit2D[8];
    private readonly Collider2D[] doorAutoOpenOverlapHits = new Collider2D[8];
    private ContactFilter2D doorDetectionContactFilter;
}
