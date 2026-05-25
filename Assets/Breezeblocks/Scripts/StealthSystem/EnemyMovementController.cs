using System;
using System.Collections.Generic;
using Breezeblocks.Missions;
using Breezeblocks.WeaponSystem;
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
    Alert
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
    Manual
}

internal enum EnemyReturnContext
{
    None,
    StartingState,
    ItineraryStep
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Movement Controller")]
public class EnemyMovementController : MonoBehaviour
{
    private const float MinimumSpeed = 0f;
    private const float MinimumAcceleration = 0f;
    private const float MinimumDistance = 0.01f;
    private const float MinimumInterval = 0.02f;
    private const float MinimumDirectionSqr = 0.0001f;
    private const float DestinationRefreshSqrDistance = 0.0025f;
    private const float AstarAccelerationOverride = 9999f;

    [FoldoutGroup("References")]
    [Tooltip("Optional override. If empty, uses Rigidbody2D on this GameObject.")]
    [SerializeField] private Rigidbody2D movementBody;

    [FoldoutGroup("A* Pathfinding")]
    [Tooltip("Optional AIPath component used as the low-level A* movement driver.")]
    [SerializeField] private AIPath aiPath;

    [FoldoutGroup("A* Pathfinding")]
    [SerializeField] private AIDestinationSetter aiDestinationSetter;

    [FoldoutGroup("A* Pathfinding")]
    [SerializeField] private Seeker seeker;

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

    [FoldoutGroup("A* Pathfinding"), InfoBox("When AIPath is assigned, it remains the low-level path steering and Rigidbody2D mover. This controller owns the high-level state, destination, speed caps, and custom rotation.")]
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
    private float stationarySuspicionUntil = float.NegativeInfinity;
    private bool alertHasNoiseFocus;
    private float alertNoiseFocusUntil = float.NegativeInfinity;
    private float alertDefaultFacingAngle;
    private Vector2 alertNoiseFocusPoint;
    private float alertStimulusUntil = float.NegativeInfinity;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        CaptureStartingTransform();
        lastStableFacingDirection = ResolveCurrentFacingDirection();
        ApplyRigidbodyRecommendations();
        ConfigureAstarDriver();
        CharacterOrbitHandsAnimator.EnsureOn(gameObject);
    }

    private void Start()
    {
        startupCompleted = true;

        if (ShouldUseItinerary)
        {
            BeginItinerary();
            return;
        }

        ResumeStartingStateWithoutItinerary();
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheReferences();

        if (!Application.isPlaying)
            CharacterOrbitHandsAnimator.EnsureOn(gameObject);

        if (!Application.isPlaying)
            ApplyRigidbodyRecommendations();

        ConfigureAstarDriver();
    }

    private void Update()
    {
        SyncRuntimeMovementState();
        UpdateStateMachine();
        UpdateItinerary(Time.deltaTime);
        SyncAstarTargets();
    }

    private void FixedUpdate()
    {
        SyncRuntimeMovementState();
        UpdateMovementSpeed(Time.fixedDeltaTime);
        ApplyMovementDriver();
        UpdateRotation(Time.fixedDeltaTime);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceIdle()
    {
        SetState(EnemyState.Idle);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForcePatrol()
    {
        SetState(EnemyState.Patrol);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceSearchAtCurrentPlayerPosition()
    {
        PlayerVisibility playerVisibility = FindFirstObjectByType<PlayerVisibility>();
        if (playerVisibility != null)
            SearchAt(playerVisibility.SamplePosition);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceLookAround()
    {
        BeginLookAround(defaultLookAroundDuration, lookAroundTurnInterval, EnemyLookAroundContext.Manual);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceReturnToStart()
    {
        ReturnToStart();
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceFlee()
    {
        Flee();
    }

    public void SetState(EnemyState newState)
    {
        switch (newState)
        {
            case EnemyState.Idle:
                EnterIdleState();
                break;

            case EnemyState.Patrol:
                EnterPatrolState(resetPatrolProgress: true);
                break;

            case EnemyState.Suspicious:
                SetSuspicious(CurrentPosition);
                break;

            case EnemyState.Searching:
                SearchAt(CurrentPosition);
                break;

            case EnemyState.LookAround:
                BeginLookAround(defaultLookAroundDuration, lookAroundTurnInterval, EnemyLookAroundContext.Manual);
                break;

            case EnemyState.ReturningToStart:
                ReturnToStart();
                break;

            case EnemyState.Alert:
                EnterAlertState();
                break;

            case EnemyState.Detected:
                if (detectedTarget != null)
                    SetDetected(detectedTarget);
                else
                    EnterDetectedState();
                break;

            case EnemyState.Fleeing:
                Flee();
                break;

            case EnemyState.Disabled:
                EnterDisabledState();
                break;
        }
    }

    public void SetSuspicious(Vector2 position)
    {
        if (!CanEnterInvestigativeState())
            return;

        PrepareInvestigativeState(position);

        if (!investigate)
        {
            BeginSuspiciousFocusState(position);
            return;
        }

        BeginDirectedState(EnemyState.Suspicious, position);
    }

    public void SearchAt(Vector2 position)
    {
        if (!CanEnterInvestigativeState())
            return;

        PrepareInvestigativeState(position);
        BeginDirectedState(EnemyState.Searching, position);
    }

    public void SetExternalInvestigation(Vector2 position, EnemyState state)
    {
        if (!CanEnterInvestigativeState())
            return;

        state = state == EnemyState.Searching ? EnemyState.Searching : EnemyState.Suspicious;

        hasExternalInvestigation = true;
        externalInvestigationState = state;
        PrepareInvestigativeState(position, resetExternalInvestigation: false);

        if (state == EnemyState.Suspicious && !investigate)
        {
            BeginSuspiciousFocusState(position);
            return;
        }

        if (currentState != state)
        {
            BeginDirectedState(state, position);
            return;
        }

        SetInvestigativeDestination(position, true);
    }

    public void UpdateSearchDestination(Vector2 position)
    {
        if (!CanReactToNoise())
            return;

        if (currentState != EnemyState.Searching)
        {
            SearchAt(position);
            return;
        }

        SetInvestigativeDestination(position, true);
    }

    public void UpdateInvestigativeDestination(Vector2 position)
    {
        if (currentState == EnemyState.Alert)
        {
            FocusAlertOnPoint(position);
            return;
        }

        if (!CanEnterInvestigativeState())
            return;

        if (currentState == EnemyState.Suspicious && !investigate)
        {
            RefreshStationarySuspicion(position);
            return;
        }

        if (currentState != EnemyState.Suspicious && currentState != EnemyState.Searching)
        {
            SearchAt(position);
            return;
        }

        SetInvestigativeDestination(position, true);
    }

    public void RefreshSuspicion(Vector2 position)
    {
        if (currentState == EnemyState.Alert)
        {
            FocusAlertOnPoint(position);
            return;
        }

        if (!CanEnterInvestigativeState())
            return;

        if (currentState == EnemyState.Suspicious && !investigate)
        {
            RefreshStationarySuspicion(position);
            return;
        }

        if (currentState == EnemyState.Suspicious || currentState == EnemyState.Searching)
        {
            SetInvestigativeDestination(position, false);
            return;
        }

        SetSuspicious(position);
    }

    public void HandleHeardNoise(Vector2 position)
    {
        if (currentState == EnemyState.Alert)
        {
            FocusAlertOnPoint(position);
            return;
        }

        if (currentState == EnemyState.Searching || currentState == EnemyState.Suspicious)
        {
            UpdateInvestigativeDestination(position);
            return;
        }

        SetSuspicious(position);
    }

    public void CancelSearch()
    {
        if (currentState != EnemyState.Searching && currentState != EnemyState.Suspicious)
            return;

        ResetExternalInvestigationState();
        if (returnToStartAfterTemporaryStates)
            ReturnToStart();
        else
            ResumeStartingState();
    }

    public void ClearExternalInvestigation(bool resumeDefaultBehavior = true)
    {
        if (!hasExternalInvestigation)
            return;

        bool shouldResumeDefaultBehavior =
            resumeDefaultBehavior &&
            (currentState == EnemyState.Suspicious || currentState == EnemyState.Searching);

        ResetExternalInvestigationState();
        if (!shouldResumeDefaultBehavior)
            return;

        if (returnToStartAfterTemporaryStates)
            ReturnToStart();
        else
            ResumeStartingState();
    }

    public void SetDetected(Transform target)
    {
        if (target == null || currentState == EnemyState.Disabled)
            return;

        if (currentState == EnemyState.Alert)
        {
            if (ResolveDetectionBehavior() == EnemyDetectionBehavior.FleeToPoint)
            {
                detectedTarget = target;
                lastKnownTargetPosition = target.position;
                Flee();
                return;
            }

            UpdateAlertVisualTarget(target, target.position);
            return;
        }

        ResetExternalInvestigationState();
        detectedTarget = target;
        lastKnownTargetPosition = target.position;
        fleeCompleted = false;
        itineraryPatrolCompletionPending = false;
        ClearManualFacingOverride();

        bool combatOwnsDetectedBehavior =
            TryGetComponent(out EnemyCombatantAI combatantAI) &&
            combatantAI != null &&
            combatantAI.IsDrafted;

        EnemyDetectionBehavior behavior = ResolveDetectionBehavior();
        if (behavior == EnemyDetectionBehavior.FleeToPoint)
        {
            Flee();
            return;
        }

        bool wasAlreadyDetected = currentState == EnemyState.Detected;
        ChangeState(EnemyState.Detected);
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        patrolWaiting = false;

        if (wasAlreadyDetected && (hasDetectedMovementOverride || combatOwnsDetectedBehavior))
            return;

        switch (behavior)
        {
            case EnemyDetectionBehavior.ChasePlayer:
                hasDestination = true;
                SetFollowTarget(target, true);
                break;

            case EnemyDetectionBehavior.StandStill:
            case EnemyDetectionBehavior.CustomOnly:
                StopMovementImmediately();
                break;
        }
    }

    public void LoseTarget()
    {
        if (currentState == EnemyState.Alert)
        {
            ClearAlertVisualTarget();
            return;
        }

        ResetExternalInvestigationState();
        detectedTarget = null;

        if (currentState != EnemyState.Detected)
            return;

        if (TryGetComponent(out EnemyCombatantAI combatantAI) && combatantAI.HandleDetectedTargetLost(lastKnownTargetPosition))
            return;
        
        EnterAlertState(force: true);
    }

    public void Flee()
    {
        if (currentState == EnemyState.Disabled || currentState == EnemyState.Fleeing && fleeCompleted)
            return;

        ResetExternalInvestigationState();
        if (fleePoint == null)
        {
            if (debugMovement)
            {
                Debug.LogWarning(
                    $"{name} cannot flee because no flee point is assigned. Falling back to {missingFleePointFallbackBehavior}.",
                    this);
            }

            EnemyDetectionBehavior fallback = missingFleePointFallbackBehavior == EnemyDetectionBehavior.FleeToPoint
                ? EnemyDetectionBehavior.StandStill
                : missingFleePointFallbackBehavior;

            if (fallback == EnemyDetectionBehavior.ChasePlayer && detectedTarget != null)
            {
                ChangeState(EnemyState.Detected);
                hasDestination = true;
                SetFollowTarget(detectedTarget, true);
            }
            else
            {
                EnterDetectedState();
            }

            return;
        }

        fleeCompleted = false;
        itineraryPatrolCompletionPending = false;
        ClearManualFacingOverride();
        ChangeState(EnemyState.Fleeing);
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        patrolWaiting = false;
        hasDestination = true;
        currentDestination = fleePoint.position;
        SetDirectDestination(currentDestination, true);
    }

    public void ReturnToStart()
    {
        if (currentState == EnemyState.Disabled)
            return;

        ResetExternalInvestigationState();
        detectedTarget = null;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;

        Vector2 returnDestination = startingPosition;
        EnemyReturnContext returnContext = EnemyReturnContext.StartingState;

        if (ShouldUseItinerary && TryResolveItineraryResumeDestination(out Vector2 itineraryDestination))
        {
            returnDestination = itineraryDestination;
            returnContext = EnemyReturnContext.ItineraryStep;
        }

        BeginReturnState(returnDestination, returnContext);
    }

    public void EnterAlertState(bool force = false)
    {
        if (currentState == EnemyState.Disabled)
            return;

        if (currentState == EnemyState.Alert)
        {
            CacheAlertDefaultFacingAngle();
            return;
        }

        if (!force && !enterAlertStateWhenTargetLost)
        {
            if (returnToStartAfterTemporaryStates)
                ReturnToStart();
            else
                ResumeStartingState();

            return;
        }

        bool transitioningFromDetected = currentState == EnemyState.Detected;
        Vector2 rememberedTargetPosition = lastKnownTargetPosition;

        ResetExternalInvestigationState();
        detectedTarget = null;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        hasDetectedMovementOverride = false;
        fleeCompleted = false;
        ClearAlertFocus();
        CacheAlertDefaultFacingAngle();

        Vector2 holdPosition = alertHoldPoint != null ? (Vector2)alertHoldPoint.position : CurrentPosition;
        bool shouldMoveToHoldPosition = alertHoldPoint != null && !IsWithinStoppingDistance(holdPosition);

        ChangeState(EnemyState.Alert);
        if (transitioningFromDetected)
            RememberAlertStimulus(rememberedTargetPosition);
        else
            alertStimulusUntil = float.NegativeInfinity;

        if (shouldMoveToHoldPosition)
        {
            ClearManualFacingOverride();
            hasDestination = true;
            currentDestination = holdPosition;
            SetDirectDestination(holdPosition, true);
            return;
        }

        StopMovementImmediately();
        ApplyAlertDefaultFacing();
    }

    public void FocusAlertOnPoint(Vector2 worldPoint)
    {
        if (currentState != EnemyState.Alert)
            return;

        Vector2 toPoint = worldPoint - CurrentPosition;
        if (toPoint.sqrMagnitude <= MinimumDirectionSqr)
            return;

        RememberAlertStimulus(worldPoint);
        alertHasNoiseFocus = true;
        alertNoiseFocusPoint = worldPoint;
        alertNoiseFocusUntil = Time.time + alertNoiseFocusDuration;

        if (!alertChaseTarget)
        {
            if (IsAtAlertHoldPoint())
            {
                StopMovementImmediately();
                SetFacingPoint(worldPoint);
            }

            return;
        }

        if (detectedTarget != null)
            return;

        if (IsWithinStoppingDistance(worldPoint))
        {
            StopMovementImmediately();
            SetFacingPoint(worldPoint);
            return;
        }

        SetDestinationIfChanged(worldPoint, true);
    }

    public void ReactToExtremeNoise(Vector2 worldPoint)
    {
        if (currentState == EnemyState.Disabled)
            return;

        if (ResolveDetectionBehavior() == EnemyDetectionBehavior.FleeToPoint)
        {
            Flee();
            return;
        }

        EnterAlertState(force: true);
        FocusAlertOnPoint(worldPoint);
    }

    public void UpdateAlertVisualTarget(Transform target, Vector2 targetPosition)
    {
        if (currentState != EnemyState.Alert || target == null)
            return;

        ResetExternalInvestigationState();
        detectedTarget = target;
        RememberAlertStimulus(targetPosition);
        ClearManualFacingOverride();
        ClearAlertFocus();
    }

    public void ClearAlertVisualTarget()
    {
        if (currentState != EnemyState.Alert)
            return;

        detectedTarget = null;
        hasDetectedMovementOverride = false;
        RememberAlertStimulus(lastKnownTargetPosition);
    }

    public void ResumeStartingState()
    {
        fleeCompleted = false;

        if (ShouldUseItinerary)
        {
            ResumeCurrentItineraryStep();
            return;
        }

        ResumeStartingStateWithoutItinerary();
    }

    public bool CanReactToNoise()
    {
        return currentState != EnemyState.Detected &&
               currentState != EnemyState.Fleeing &&
               currentState != EnemyState.Disabled;
    }

    public bool IsPlayerFullyDetectedState()
    {
        return currentState == EnemyState.Detected ||
               currentState == EnemyState.Fleeing ||
               (currentState == EnemyState.Alert && detectedTarget != null);
    }

    public bool IsTemporaryState()
    {
        return currentState == EnemyState.Suspicious ||
               currentState == EnemyState.Searching ||
               currentState == EnemyState.LookAround ||
               currentState == EnemyState.ReturningToStart;
    }

    public void SetDetectedDestination(Vector2 destination, EnemySpeedType speedType = EnemySpeedType.Sprint)
    {
        if (!UsesCombatMovementOverridesForState(currentState) && currentState != EnemyState.Fleeing)
            return;

        hasDetectedMovementOverride = true;
        detectedMovementOverrideSpeedType = speedType;
        currentDestination = destination;
        hasDestination = true;
        SetDirectDestination(destination, true);
    }

    public void SetStaggerOverride(bool active, float moveSpeedOverride, float turnSpeedMultiplier)
    {
        staggerOverrideActive = active;
        staggeredMoveSpeedOverride = Mathf.Max(0f, moveSpeedOverride);
        staggerTurnSpeedMultiplier = active ? Mathf.Clamp01(turnSpeedMultiplier) : 1f;
    }

    public void ApplySettings(EnemyMovementSettings settings)
    {
        if (settings == null)
            return;

        CacheReferences();

        startingState = settings.StartingState;
        startingPointFacing = ActorProfileDataUtility.CloneFacing(settings.StartingPointFacing);
        walkSpeed = settings.WalkSpeed;
        runSpeed = settings.RunSpeed;
        sprintSpeed = settings.SprintSpeed;
        acceleration = settings.Acceleration;
        deceleration = settings.Deceleration;
        stoppingDistance = settings.StoppingDistance;
        slowdownDistance = settings.SlowdownDistance;
        minimumMoveSpeed = settings.MinimumMoveSpeed;
        useCustomRotation = settings.UseCustomRotation;
        rotationSpeed = settings.RotationSpeed;
        rotationAngleOffset = settings.RotationAngleOffset;
        faceMovementDirection = settings.FaceMovementDirection;
        faceTargetWhenDetected = settings.FaceTargetWhenDetected;
        preferPathSteeringDirection = settings.PreferPathSteeringDirection;
        lockRotationWhenIdle = settings.LockRotationWhenIdle;
        patrolMode = settings.PatrolMode;
        returnToStartAfterTemporaryStates = settings.ReturnToStartAfterTemporaryStates;
        returnToStartSpeedType = settings.ReturnToStartSpeedType;
        enterAlertStateWhenTargetLost = settings.EnterAlertStateWhenTargetLost;
        alertChaseTarget = settings.ChaseTarget;
        alertNoiseFocusDuration = settings.AlertNoiseFocusDuration;
        alertTargetLostDuration = settings.AlertTargetLostDuration;
        defaultLookAroundDuration = settings.DefaultLookAroundDuration;
        lookAroundTurnInterval = settings.LookAroundTurnInterval;
        lookAroundRotationSpeed = settings.LookAroundRotationSpeed;
        randomLookAngleRange = settings.RandomLookAngleRange;
        useItinerary = settings.UseItinerary;
        loopItinerary = settings.LoopItinerary;
        investigate = settings.Investigate;
        detectionBehavior = settings.DetectionBehavior;
        searchLastKnownTargetPositionWhenTargetLost = settings.SearchLastKnownTargetPositionWhenTargetLost;
        missingFleePointFallbackBehavior = settings.MissingFleePointFallbackBehavior;
        canFlee = settings.CanFlee;
        stayAtFleePointForever = settings.StayAtFleePointForever;
        fleeStoppingDistance = settings.FleeStoppingDistance;
        disableHearingAfterFlee = settings.DisableHearingAfterFlee;
        disableVisionAfterFlee = settings.DisableVisionAfterFlee;
        useMovePosition = settings.UseMovePosition;
        useVelocityMovement = settings.UseVelocityMovement;
        applyRecommendedRigidbodySettings = settings.ApplyRecommendedRigidbodySettings;
        forceZeroGravity = settings.ForceZeroGravity;
        recommendedInterpolation = settings.RecommendedInterpolation;
        recommendedCollisionDetection = settings.RecommendedCollisionDetection;

        ClampSettings();
        ApplyRigidbodyRecommendations();
        ConfigureAstarDriver();
    }

    public void HoldDetectedPosition()
    {
        if (!UsesCombatMovementOverridesForState(currentState))
            return;

        hasDetectedMovementOverride = true;
        StopMovementImmediately();
    }

    public void ClearDetectedMovementOverride(bool resumeDefaultDetectedBehavior = true)
    {
        hasDetectedMovementOverride = false;

        if (!resumeDefaultDetectedBehavior || !UsesCombatMovementOverridesForState(currentState))
            return;

        if (currentState == EnemyState.Alert)
        {
            if (alertChaseTarget && detectedTarget != null)
                SetFollowTarget(detectedTarget, true);
            else if (alertChaseTarget && HasActiveAlertStimulus())
                SetDirectDestination(lastKnownTargetPosition, true);
            else
                StopMovementImmediately();

            return;
        }

        if (ResolveDetectionBehavior() == EnemyDetectionBehavior.ChasePlayer && detectedTarget != null)
            SetFollowTarget(detectedTarget, true);
        else
            StopMovementImmediately();
    }

    public void SetFacingPoint(Vector2 worldPoint)
    {
        Vector2 toPoint = worldPoint - CurrentPosition;
        if (toPoint.sqrMagnitude <= MinimumDirectionSqr)
            return;

        manualFacingDirection = toPoint.normalized;
        hasManualFacingOverride = true;
    }

    public void SetExternalFacingDirection(Vector2 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= MinimumDirectionSqr)
            return;

        externalFacingDirection = worldDirection.normalized;
        hasExternalFacingOverride = true;
    }

    public void ClearExternalFacingOverride()
    {
        hasExternalFacingOverride = false;
    }

    public void SetExternalTurnSpeedOverride(bool active, float turnSpeed)
    {
        hasExternalTurnSpeedOverride = active && turnSpeed > 0f;
        externalTurnSpeedOverride = hasExternalTurnSpeedOverride ? Mathf.Max(0f, turnSpeed) : -1f;
    }

    public void ClearFacingOverride()
    {
        ClearManualFacingOverride();
    }

    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case EnemyState.Patrol:
                UpdatePatrolState();
                break;

            case EnemyState.Suspicious:
                UpdateInvestigativeState(EnemyLookAroundContext.Suspicious);
                break;

            case EnemyState.Searching:
                UpdateInvestigativeState(EnemyLookAroundContext.Searching);
                break;

            case EnemyState.LookAround:
                UpdateLookAroundState();
                break;

            case EnemyState.ReturningToStart:
                UpdateReturnToStartState();
                break;

            case EnemyState.Alert:
                UpdateAlertState();
                break;

            case EnemyState.Detected:
                UpdateDetectedState();
                break;

            case EnemyState.Fleeing:
                UpdateFleeingState();
                break;
        }
    }

    private void UpdatePatrolState()
    {
        if (!TryGetActivePatrolPointCount(out int patrolPointCount) || patrolPointCount <= 0)
        {
            if (debugMovement)
                Debug.LogWarning($"{name} is set to Patrol but has no patrol points. Falling back to Idle.", this);

            EnterIdleState();
            return;
        }

        if (patrolWaiting)
        {
            if (Time.time >= patrolWaitUntil)
            {
                patrolWaiting = false;

                if (itineraryPatrolCompletionPending)
                {
                    itineraryPatrolCompletionPending = false;
                    AdvanceItineraryStep();
                }
                else
                {
                    AdvanceToNextPatrolPoint();
                }
            }

            return;
        }

        if (!hasDestination)
        {
            MoveToCurrentPatrolPoint();
            return;
        }

        if (hasReachedDestination)
            HandlePatrolPointArrival();
    }

    private void UpdateInvestigativeState(EnemyLookAroundContext context)
    {
        if (currentState == EnemyState.Suspicious && !investigate)
        {
            UpdateStationarySuspicion();
            return;
        }

        if (hasExternalInvestigation && currentState == externalInvestigationState)
        {
            if (hasDestination && hasReachedDestination)
                StopMovementImmediately();

            return;
        }

        if (hasReachedDestination)
            BeginLookAround(defaultLookAroundDuration, lookAroundTurnInterval, context);
    }

    private void UpdateLookAroundState()
    {
        if (Time.time >= nextLookAroundTurnTime)
            PickNextLookAroundDirection();

        if (Time.time < lookAroundEndTime)
            return;

        switch (currentLookAroundContext)
        {
            case EnemyLookAroundContext.Patrol:
                ChangeState(EnemyState.Patrol);

                if (itineraryPatrolCompletionPending)
                {
                    itineraryPatrolCompletionPending = false;
                    AdvanceItineraryStep();
                    break;
                }

                if (patrolWaiting && Time.time >= patrolWaitUntil)
                {
                    patrolWaiting = false;
                    AdvanceToNextPatrolPoint();
                }
                break;

            case EnemyLookAroundContext.Searching:
            case EnemyLookAroundContext.Suspicious:
                if (returnToStartAfterTemporaryStates)
                    ReturnToStart();
                else
                    ResumeStartingState();
                break;

            case EnemyLookAroundContext.LostTarget:
                if (enterAlertStateWhenTargetLost)
                    EnterAlertState();
                else if (returnToStartAfterTemporaryStates)
                    ReturnToStart();
                else
                    ResumeStartingState();
                break;

            default:
                ResumeStartingState();
                break;
        }
    }

    private void UpdateReturnToStartState()
    {
        if (!hasReachedDestination)
            return;

        CompleteReturnState();
    }

    private void UpdateAlertState()
    {
        if (hasDetectedMovementOverride)
        {
            if (!hasReachedDestination)
                return;

            StopMovementImmediately();
        }

        if (alertChaseTarget && detectedTarget != null)
        {
            RememberAlertStimulus(detectedTarget.position);

            if (!hasDetectedMovementOverride)
                SetFollowTarget(detectedTarget, false);

            return;
        }

        if (alertChaseTarget && HasActiveAlertStimulus())
        {
            if (IsWithinStoppingDistance(lastKnownTargetPosition))
            {
                StopMovementImmediately();
                SetFacingPoint(lastKnownTargetPosition);
                return;
            }

            SetDestinationIfChanged(lastKnownTargetPosition, false);
            return;
        }

        if (alertHasNoiseFocus && Time.time < alertNoiseFocusUntil)
        {
            SetFacingPoint(alertNoiseFocusPoint);
            return;
        }

        if (alertHasNoiseFocus)
            ClearAlertFocus();

        Vector2 holdPosition = alertHoldPoint != null ? (Vector2)alertHoldPoint.position : CurrentPosition;
        if (alertHoldPoint != null && !IsWithinStoppingDistance(holdPosition))
        {
            SetDestinationIfChanged(holdPosition, true);
            return;
        }

        ApplyAlertDefaultFacing();
    }

    private void UpdateDetectedState()
    {
        if (detectedTarget == null)
            return;

        lastKnownTargetPosition = detectedTarget.position;
        if (ResolveDetectionBehavior() == EnemyDetectionBehavior.ChasePlayer)
            hasDestination = true;
    }

    private void UpdateFleeingState()
    {
        if (!hasReachedDestination)
            return;

        fleeCompleted = true;
        StopMovementImmediately();
        ApplyFleePointFacingOverrideIfAvailable();

        if (disableHearingAfterFlee && TryGetComponent(out AIHearing hearing))
            hearing.enabled = false;

        if (disableVisionAfterFlee && TryGetComponent(out EnemyVisionAI vision))
            vision.enabled = false;

        if (!stayAtFleePointForever)
            EnterIdleState();
    }

    private void UpdateItinerary(float deltaTime)
    {
        if (!ShouldUseItinerary || itineraryFinished || !TryGetCurrentItineraryStep(out EnemyItineraryStep step))
            return;

        if (step.StepType == EnemyItineraryStepType.Idle)
        {
            if (currentState != EnemyState.Idle || hasDestination)
                return;

            itineraryStepRemainingTime = Mathf.Max(0f, itineraryStepRemainingTime - deltaTime);
            if (itineraryStepRemainingTime <= 0f)
                AdvanceItineraryStep();

            return;
        }

        if (step.PatrolCompletionMode != EnemyItineraryPatrolCompletionMode.FixedDuration)
            return;

        if (currentState != EnemyState.Patrol && !(currentState == EnemyState.LookAround && currentLookAroundContext == EnemyLookAroundContext.Patrol))
            return;

        itineraryStepRemainingTime = Mathf.Max(0f, itineraryStepRemainingTime - deltaTime);
        if (itineraryStepRemainingTime > 0f)
            return;

        itineraryPatrolCompletionPending = false;
        AdvanceItineraryStep();
    }

    private void UpdateMovementSpeed(float deltaTime)
    {
        float desiredSpeed = ResolveDesiredSpeedForState();
        float changeRate = desiredSpeed > currentSpeedCap ? acceleration : deceleration;
        currentSpeedCap = Mathf.MoveTowards(currentSpeedCap, desiredSpeed, changeRate * deltaTime);
    }

    private void ApplyMovementDriver()
    {
        float desiredSpeed = ResolveDesiredSpeedForState();
        float appliedSpeed = desiredSpeed > 0f
            ? Mathf.Max(currentSpeedCap, minimumMoveSpeed)
            : 0f;

        if (aiPath != null)
        {
            aiPath.canMove = currentState != EnemyState.Disabled;
            aiPath.isStopped = appliedSpeed <= Mathf.Epsilon || !hasDestination;
            aiPath.maxSpeed = appliedSpeed;
            aiPath.maxAcceleration = AstarAccelerationOverride;
            aiPath.slowdownDistance = slowdownDistance;
            aiPath.endReachedDistance = ResolveCurrentStoppingDistance();
            return;
        }

        if (movementBody == null)
        {
            WarnMissingMover();
            return;
        }

        if (!hasDestination || appliedSpeed <= Mathf.Epsilon)
        {
            if (useVelocityMovement)
                movementBody.linearVelocity = Vector2.zero;

            return;
        }

        Vector2 currentPosition = movementBody.position;
        Vector2 toDestination = currentDestination - currentPosition;
        float stoppingDistanceForState = ResolveCurrentStoppingDistance();
        if (toDestination.sqrMagnitude <= stoppingDistanceForState * stoppingDistanceForState)
        {
            if (useVelocityMovement)
                movementBody.linearVelocity = Vector2.zero;

            return;
        }

        Vector2 desiredVelocity = toDestination.normalized * appliedSpeed;
        if (useVelocityMovement)
        {
            movementBody.linearVelocity = Vector2.MoveTowards(
                movementBody.linearVelocity,
                desiredVelocity,
                acceleration * Time.fixedDeltaTime);
            return;
        }

        Vector2 nextPosition = currentPosition + (desiredVelocity * Time.fixedDeltaTime);
        movementBody.MovePosition(nextPosition);
    }

    private void UpdateRotation(float deltaTime)
    {
        if (!useCustomRotation)
            return;

        Vector2 desiredDirection = ResolveDesiredFacingDirection();
        if (desiredDirection.sqrMagnitude <= MinimumDirectionSqr)
            return;

        lastStableFacingDirection = desiredDirection.normalized;

        float activeRotationSpeed = hasExternalTurnSpeedOverride
            ? externalTurnSpeedOverride
            : currentState == EnemyState.LookAround ? lookAroundRotationSpeed : rotationSpeed;
        activeRotationSpeed *= staggerTurnSpeedMultiplier;
        float targetAngle = Mathf.Atan2(desiredDirection.y, desiredDirection.x) * Mathf.Rad2Deg + rotationAngleOffset;
        float nextAngle = Mathf.MoveTowardsAngle(CurrentRotation, targetAngle, activeRotationSpeed * deltaTime);

        if (movementBody != null)
            movementBody.MoveRotation(nextAngle);
        else
            transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
    }

    private void BeginDirectedState(EnemyState state, Vector2 position)
    {
        detectedTarget = null;
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        stationarySuspicionUntil = float.NegativeInfinity;
        ClearManualFacingOverride();
        ChangeState(state);
        hasDestination = true;
        currentDestination = position;
        SetDirectDestination(position, true);
    }

    private void BeginLookAround(float duration, float turnInterval, EnemyLookAroundContext context)
    {
        detectedTarget = null;
        hasDestination = false;
        currentLookAroundContext = context;
        activeLookAroundTurnInterval = Mathf.Max(MinimumInterval, turnInterval);
        lookAroundEndTime = Time.time + Mathf.Max(0f, duration);
        nextLookAroundTurnTime = Time.time;
        StopMovementImmediately();
        ChangeState(EnemyState.LookAround);
        PickNextLookAroundDirection();
    }

    private void BeginReturnState(Vector2 destination, EnemyReturnContext context)
    {
        ResetExternalInvestigationState();
        currentReturnContext = context;
        currentLookAroundContext = EnemyLookAroundContext.None;
        stationarySuspicionUntil = float.NegativeInfinity;
        ClearManualFacingOverride();
        ChangeState(EnemyState.ReturningToStart);
        hasDestination = true;
        currentDestination = destination;
        SetDirectDestination(destination, true);
    }

    private void CompleteReturnState()
    {
        EnemyReturnContext completedContext = currentReturnContext;
        currentReturnContext = EnemyReturnContext.None;

        switch (completedContext)
        {
            case EnemyReturnContext.ItineraryStep:
                ResumeCurrentItineraryStep();
                break;

            case EnemyReturnContext.StartingState:
            default:
                ResumeStartingStateWithoutItinerary();
                break;
        }
    }

    private void EnterIdleState()
    {
        ResetExternalInvestigationState();
        detectedTarget = null;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        hasDetectedMovementOverride = false;
        ChangeState(EnemyState.Idle);
        StopMovementImmediately();
    }

    private void EnterPatrolState(bool resetPatrolProgress)
    {
        if (!TryGetActivePatrolPointCount(out int patrolPointCount) || patrolPointCount <= 0)
        {
            EnterIdleState();
            return;
        }

        ResetExternalInvestigationState();
        if (resetPatrolProgress)
        {
            currentPatrolIndex = 0;
            patrolDirection = 1;
            itineraryRandomPatrolVisitCount = 0;
        }

        detectedTarget = null;
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;
        hasDetectedMovementOverride = false;
        ClearManualFacingOverride();
        currentPatrolIndex = Mathf.Clamp(currentPatrolIndex, 0, patrolPointCount - 1);
        ChangeState(EnemyState.Patrol);
        MoveToCurrentPatrolPoint();
    }

    private void EnterDetectedState()
    {
        ResetExternalInvestigationState();
        ClearManualFacingOverride();
        ChangeState(EnemyState.Detected);
        StopMovementImmediately();
    }

    private void EnterDisabledState()
    {
        ResetExternalInvestigationState();
        detectedTarget = null;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        hasDetectedMovementOverride = false;
        ClearManualFacingOverride();
        ChangeState(EnemyState.Disabled);
        StopMovementImmediately();
    }

    private void MoveToCurrentPatrolPoint()
    {
        if (!TryGetCurrentPatrolPoint(out PatrolPoint patrolPoint))
            return;

        hasDestination = true;
        currentDestination = patrolPoint.Point.position;
        SetDirectDestination(currentDestination, true);
    }

    private void HandlePatrolPointArrival()
    {
        if (!TryGetCurrentPatrolPoint(out PatrolPoint patrolPoint))
            return;

        if (debugMovement)
            Debug.Log($"{name} reached patrol point {currentPatrolIndex}.", this);

        itineraryPatrolCompletionPending = ShouldCompleteCurrentPatrolStepOnArrival();

        float waitDuration = Mathf.Max(0f, patrolPoint.WaitDuration);
        patrolWaiting = waitDuration > 0f || patrolPoint.LookAroundAtPoint;
        patrolWaitUntil = Time.time + waitDuration;
        StopMovementImmediately();
        ApplyPatrolPointFacingOverrideIfAvailable(patrolPoint);

        if (patrolPoint.LookAroundAtPoint)
        {
            float duration = patrolPoint.LookAroundDuration > 0f ? patrolPoint.LookAroundDuration : defaultLookAroundDuration;
            float turnInterval = patrolPoint.LookAroundTurnInterval > 0f ? patrolPoint.LookAroundTurnInterval : lookAroundTurnInterval;
            BeginLookAround(duration, turnInterval, EnemyLookAroundContext.Patrol);
            return;
        }

        if (!patrolWaiting)
        {
            if (itineraryPatrolCompletionPending)
            {
                itineraryPatrolCompletionPending = false;
                AdvanceItineraryStep();
            }
            else
            {
                AdvanceToNextPatrolPoint();
            }
        }
    }

    private void AdvanceToNextPatrolPoint()
    {
        if (!TryGetActivePatrolPointCount(out int patrolPointCount) || patrolPointCount <= 0)
            return;

        switch (GetActivePatrolMode())
        {
            case EnemyPatrolMode.Loop:
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPointCount;
                break;

            case EnemyPatrolMode.PingPong:
                if (patrolPointCount == 1)
                {
                    currentPatrolIndex = 0;
                    break;
                }

                if (currentPatrolIndex <= 0)
                    patrolDirection = 1;
                else if (currentPatrolIndex >= patrolPointCount - 1)
                    patrolDirection = -1;

                currentPatrolIndex = Mathf.Clamp(currentPatrolIndex + patrolDirection, 0, patrolPointCount - 1);
                break;

            case EnemyPatrolMode.Random:
                if (patrolPointCount == 1)
                {
                    currentPatrolIndex = 0;
                    break;
                }

                int previousIndex = currentPatrolIndex;
                do
                {
                    currentPatrolIndex = UnityEngine.Random.Range(0, patrolPointCount);
                }
                while (currentPatrolIndex == previousIndex);
                break;
        }

        if (debugMovement)
            Debug.Log($"{name} advancing to patrol point {currentPatrolIndex}.", this);

        MoveToCurrentPatrolPoint();
    }

    private bool TryGetCurrentPatrolPoint(out PatrolPoint patrolPoint)
    {
        patrolPoint = null;
        if (!TryGetActivePatrolPoints(out List<PatrolPoint> activePatrolPoints) || activePatrolPoints.Count <= 0)
            return false;

        currentPatrolIndex = Mathf.Clamp(currentPatrolIndex, 0, activePatrolPoints.Count - 1);
        patrolPoint = activePatrolPoints[currentPatrolIndex];
        if (patrolPoint != null && patrolPoint.Point != null)
            return true;

        if (debugMovement)
            Debug.LogWarning($"{name} has a patrol point entry without a transform assigned.", this);

        return false;
    }

    private void BeginItinerary()
    {
        itineraryFinished = false;
        currentItineraryIndex = itinerarySteps.Count > 0 ? 0 : -1;
        ConfigureCurrentItineraryStep(resetPatrolProgress: true, resetStepTimer: true);
        ResumeCurrentItineraryStep();
    }

    private void AdvanceItineraryStep()
    {
        if (!ShouldUseItinerary || itineraryFinished)
            return;

        int nextIndex = currentItineraryIndex + 1;
        if (nextIndex >= itinerarySteps.Count)
        {
            if (!loopItinerary)
            {
                itineraryFinished = true;
                EnterIdleState();
                return;
            }

            nextIndex = 0;
        }

        currentItineraryIndex = nextIndex;
        ConfigureCurrentItineraryStep(resetPatrolProgress: true, resetStepTimer: true);
        ResumeCurrentItineraryStep();
    }

    private void ResumeCurrentItineraryStep()
    {
        if (!TryGetCurrentItineraryStep(out EnemyItineraryStep step))
        {
            ResumeStartingStateWithoutItinerary();
            return;
        }

        itineraryFinished = false;

        switch (step.StepType)
        {
            case EnemyItineraryStepType.Idle:
                ResumeIdleItineraryStep(step);
                break;

            case EnemyItineraryStepType.Patrol:
                ResumePatrolItineraryStep();
                break;
        }
    }

    private void ResumeIdleItineraryStep(EnemyItineraryStep step)
    {
        Vector2 idleDestination = ResolveIdleStepPosition(step);
        if (!IsWithinStoppingDistance(idleDestination))
        {
            BeginReturnState(idleDestination, EnemyReturnContext.ItineraryStep);
            return;
        }

        EnterIdleState();
        ApplyIdleStepFacingOverrideIfAvailable(step);
    }

    private void ResumePatrolItineraryStep()
    {
        EnterPatrolState(resetPatrolProgress: false);
    }

    private void ConfigureCurrentItineraryStep(bool resetPatrolProgress, bool resetStepTimer)
    {
        if (!TryGetCurrentItineraryStep(out EnemyItineraryStep step))
            return;

        itineraryPatrolCompletionPending = false;

        if (resetStepTimer)
            itineraryStepRemainingTime = ResolveItineraryStepDuration(step);

        if (step.StepType != EnemyItineraryStepType.Patrol || !resetPatrolProgress)
            return;

        currentPatrolIndex = 0;
        patrolDirection = 1;
        itineraryRandomPatrolVisitCount = 0;
    }

    private float ResolveItineraryStepDuration(EnemyItineraryStep step)
    {
        if (step == null)
            return 0f;

        return step.StepType switch
        {
            EnemyItineraryStepType.Idle => Mathf.Max(0f, step.IdleDuration),
            EnemyItineraryStepType.Patrol when step.PatrolCompletionMode == EnemyItineraryPatrolCompletionMode.FixedDuration => Mathf.Max(0f, step.PatrolDuration),
            _ => 0f
        };
    }

    private bool TryResolveItineraryResumeDestination(out Vector2 destination)
    {
        destination = startingPosition;
        if (!TryGetCurrentItineraryStep(out EnemyItineraryStep step))
            return false;

        if (step.StepType == EnemyItineraryStepType.Idle)
        {
            destination = ResolveIdleStepPosition(step);
            return true;
        }

        if (TryGetCurrentPatrolPoint(out PatrolPoint patrolPoint) && patrolPoint != null && patrolPoint.Point != null)
        {
            destination = patrolPoint.Point.position;
            return true;
        }

        return false;
    }

    private bool DetermineIsItineraryPaused()
    {
        if (!ShouldUseItinerary || itineraryFinished || !TryGetCurrentItineraryStep(out _))
            return false;

        return currentState == EnemyState.Suspicious ||
               currentState == EnemyState.Searching ||
               currentState == EnemyState.ReturningToStart ||
               currentState == EnemyState.Detected ||
               currentState == EnemyState.Alert ||
               currentState == EnemyState.Fleeing ||
               currentState == EnemyState.Disabled;
    }

    private void ResumeStartingStateWithoutItinerary()
    {
        switch (SanitizeStartingState(startingState))
        {
            case EnemyState.Patrol:
                EnterPatrolState(resetPatrolProgress: true);
                break;

            case EnemyState.Suspicious:
                ChangeState(EnemyState.Suspicious);
                StopMovementImmediately();
                break;

            case EnemyState.Alert:
                if (enterAlertStateWhenTargetLost)
                    EnterAlertState();
                else
                    EnterIdleState();
                break;

            case EnemyState.Disabled:
                EnterDisabledState();
                break;

            default:
                EnterIdleState();
                ApplyStartingPointFacingOverrideIfAvailable();
                break;
        }
    }

    private void SyncAstarTargets()
    {
        if (aiPath == null)
            return;

        if (UsesCombatMovementOverridesForState(currentState) && hasDetectedMovementOverride)
        {
            if (aiDestinationSetter != null)
                aiDestinationSetter.target = null;

            if (hasDestination)
                aiPath.destination = currentDestination;

            return;
        }

        EnemyDetectionBehavior behavior = ResolveDetectionBehavior();
        if (currentState == EnemyState.Detected &&
            behavior == EnemyDetectionBehavior.ChasePlayer &&
            detectedTarget != null)
        {
            if (aiDestinationSetter != null)
                aiDestinationSetter.target = detectedTarget;
            else
                aiPath.destination = detectedTarget.position;

            return;
        }

        if (currentState == EnemyState.Alert &&
            alertChaseTarget &&
            detectedTarget != null)
        {
            if (aiDestinationSetter != null)
                aiDestinationSetter.target = detectedTarget;
            else
                aiPath.destination = detectedTarget.position;

            return;
        }

        if (aiDestinationSetter != null)
            aiDestinationSetter.target = null;

        if (hasDestination)
            aiPath.destination = currentDestination;
    }

    private void StopMovementImmediately()
    {
        hasDestination = false;

        if (aiDestinationSetter != null)
            aiDestinationSetter.target = null;

        if (aiPath != null)
            aiPath.isStopped = true;

        if (movementBody != null && useVelocityMovement)
            movementBody.linearVelocity = Vector2.zero;
    }

    private void SetFollowTarget(Transform target, bool forceSearchPath)
    {
        if (target == null)
            return;

        ClearManualFacingOverride();
        currentDestination = target.position;
        hasDestination = true;

        if (aiDestinationSetter != null)
            aiDestinationSetter.target = target;

        if (aiPath != null)
        {
            aiPath.destination = currentDestination;
            if (forceSearchPath && CanIssueAstarSearchRequest())
                aiPath.SearchPath();
        }
    }

    private void SetDirectDestination(Vector2 destination, bool forceSearchPath)
    {
        ClearManualFacingOverride();
        currentDestination = destination;
        hasDestination = true;

        if (aiDestinationSetter != null)
            aiDestinationSetter.target = null;

        if (aiPath != null)
        {
            aiPath.destination = destination;
            if (forceSearchPath && CanIssueAstarSearchRequest())
                aiPath.SearchPath();
        }
    }

    private void SetDestinationIfChanged(Vector2 destination, bool forceSearchPath)
    {
        if (hasDestination && (currentDestination - destination).sqrMagnitude <= DestinationRefreshSqrDistance)
            return;

        SetDirectDestination(destination, forceSearchPath);
    }

    private void SyncRuntimeMovementState()
    {
        currentMovementSpeed = ResolveActualMovementSpeed();
        isMoving = currentMovementSpeed > minimumMoveSpeed;
        hasReachedDestination = EvaluateHasReachedDestination();
    }

    private float ResolveActualMovementSpeed()
    {
        if (aiPath != null)
            return aiPath.velocity.magnitude;

        if (movementBody != null)
            return movementBody.linearVelocity.magnitude;

        return 0f;
    }

    private bool EvaluateHasReachedDestination()
    {
        if (!hasDestination)
            return false;

        float activeStoppingDistance = ResolveCurrentStoppingDistance();
        if (aiPath != null)
            return aiPath.reachedDestination || aiPath.remainingDistance <= activeStoppingDistance;

        Vector2 delta = currentDestination - CurrentPosition;
        return delta.sqrMagnitude <= activeStoppingDistance * activeStoppingDistance;
    }

    private float ResolveDesiredSpeedForState()
    {
        float desiredSpeed = currentState switch
        {
            EnemyState.Patrol when patrolWaiting => 0f,
            EnemyState.Patrol => walkSpeed,
            EnemyState.Suspicious when hasDestination => runSpeed,
            EnemyState.Searching when hasDestination => runSpeed,
            EnemyState.ReturningToStart when hasDestination => ResolveSpeed(returnToStartSpeedType),
            EnemyState.Alert when hasDetectedMovementOverride && hasDestination => ResolveSpeed(detectedMovementOverrideSpeedType),
            EnemyState.Alert when hasDestination => sprintSpeed,
            EnemyState.Detected when hasDetectedMovementOverride && hasDestination => ResolveSpeed(detectedMovementOverrideSpeedType),
            EnemyState.Detected when ResolveDetectionBehavior() == EnemyDetectionBehavior.ChasePlayer && detectedTarget != null => sprintSpeed,
            EnemyState.Fleeing when !fleeCompleted && hasDestination => sprintSpeed,
            _ => 0f
        };

        if (staggerOverrideActive && desiredSpeed > 0f)
            return staggeredMoveSpeedOverride;

        return desiredSpeed;
    }

    private float ResolveSpeed(EnemySpeedType speedType)
    {
        return speedType switch
        {
            EnemySpeedType.Walk => walkSpeed,
            EnemySpeedType.Run => runSpeed,
            EnemySpeedType.Sprint => sprintSpeed,
            _ => walkSpeed
        };
    }

    private float ResolveCurrentStoppingDistance()
    {
        return currentState == EnemyState.Fleeing ? Mathf.Max(stoppingDistance, fleeStoppingDistance) : stoppingDistance;
    }

    private bool UsesCombatMovementOverridesForState(EnemyState state)
    {
        return state == EnemyState.Detected || state == EnemyState.Alert;
    }

    private EnemyDetectionBehavior ResolveDetectionBehavior()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint && !canFlee
            ? missingFleePointFallbackBehavior == EnemyDetectionBehavior.FleeToPoint
                ? EnemyDetectionBehavior.StandStill
                : missingFleePointFallbackBehavior
            : detectionBehavior;
    }

    private Vector2 ResolveCurrentTargetPosition()
    {
        if (UsesCombatMovementOverridesForState(currentState) && hasDetectedMovementOverride && hasDestination)
            return currentDestination;

        if (detectedTarget != null && currentState == EnemyState.Detected && ResolveDetectionBehavior() == EnemyDetectionBehavior.ChasePlayer)
            return detectedTarget.position;

        if (detectedTarget != null && currentState == EnemyState.Alert && alertChaseTarget)
            return detectedTarget.position;

        return hasDestination ? currentDestination : CurrentPosition;
    }

    private Vector2 ResolveDesiredFacingDirection()
    {
        if (currentState == EnemyState.LookAround)
            return currentLookDirection;

        if (ShouldFaceTrackedTarget())
        {
            Vector2 toTarget = (Vector2)detectedTarget.position - CurrentPosition;
            if (toTarget.sqrMagnitude > MinimumDirectionSqr)
                return toTarget.normalized;
        }

        if (hasExternalFacingOverride && externalFacingDirection.sqrMagnitude > MinimumDirectionSqr)
            return externalFacingDirection.normalized;

        if (hasManualFacingOverride && manualFacingDirection.sqrMagnitude > MinimumDirectionSqr)
            return manualFacingDirection.normalized;

        if (currentState == EnemyState.Idle && lockRotationWhenIdle)
            return Vector2.zero;

        if (faceMovementDirection)
        {
            Vector2 pathDirection = ResolveStablePathFacingDirection();
            if (pathDirection.sqrMagnitude > MinimumDirectionSqr)
                return pathDirection.normalized;
        }

        if (hasDestination)
        {
            Vector2 toDestination = currentDestination - CurrentPosition;
            if (toDestination.sqrMagnitude > MinimumDirectionSqr)
                return toDestination.normalized;
        }

        if (!lockRotationWhenIdle && lastStableFacingDirection.sqrMagnitude > MinimumDirectionSqr)
            return lastStableFacingDirection;

        return Vector2.zero;
    }

    private bool ShouldFaceTrackedTarget()
    {
        if (!faceTargetWhenDetected || detectedTarget == null)
            return false;

        return currentState == EnemyState.Detected ||
               currentState == EnemyState.Alert;
    }

    private Vector2 ResolveMovementVector()
    {
        if (aiPath != null)
            return aiPath.velocity;

        if (movementBody != null)
            return movementBody.linearVelocity;

        return Vector2.zero;
    }

    private Vector2 ResolveStablePathFacingDirection()
    {
        if (aiPath != null)
        {
            if (preferPathSteeringDirection)
            {
                Vector2 toSteeringTarget = (Vector2)aiPath.steeringTarget - CurrentPosition;
                if (toSteeringTarget.sqrMagnitude > MinimumDirectionSqr)
                    return toSteeringTarget.normalized;
            }

            Vector2 velocity = aiPath.velocity;
            if (velocity.sqrMagnitude > MinimumDirectionSqr)
                return velocity.normalized;
        }
        else if (movementBody != null)
        {
            Vector2 velocity = movementBody.linearVelocity;
            if (velocity.sqrMagnitude > MinimumDirectionSqr)
                return velocity.normalized;
        }

        if (hasDestination)
        {
            Vector2 toDestination = currentDestination - CurrentPosition;
            if (toDestination.sqrMagnitude > MinimumDirectionSqr)
                return toDestination.normalized;
        }

        return Vector2.zero;
    }

    private void OnDisable()
    {
        if (debugMovement && Application.isPlaying)
            Debug.LogWarning($"{name} EnemyMovementController component was disabled externally.", this);
    }

    private void PickNextLookAroundDirection()
    {
        Vector2 basis = hasDestination
            ? currentDestination - CurrentPosition
            : ResolveMovementVector();

        if (basis.sqrMagnitude <= MinimumDirectionSqr)
            basis = transform.up;

        float angleOffset = UnityEngine.Random.Range(-randomLookAngleRange * 0.5f, randomLookAngleRange * 0.5f);
        currentLookDirection = Rotate(basis.normalized, angleOffset);
        nextLookAroundTurnTime = Time.time + activeLookAroundTurnInterval;
    }

    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
            return;

        EnemyState oldState = currentState;
        previousState = currentState;
        currentState = newState;

        if (!UsesCombatMovementOverridesForState(newState))
            hasDetectedMovementOverride = false;

        if (debugMovement)
            Debug.Log($"{name} state changed from {previousState} to {currentState}.", this);

        MissionRuntimeEvents.RaiseEnemyStateChanged(this, oldState, newState);
        StateChanged?.Invoke(oldState, newState);
    }

    private void BeginSuspiciousFocusState(Vector2 position)
    {
        detectedTarget = null;
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        ChangeState(EnemyState.Suspicious);
        RefreshStationarySuspicion(position);
    }

    private void RefreshStationarySuspicion(Vector2 position)
    {
        lastKnownTargetPosition = position;
        stationarySuspicionUntil = Time.time + Mathf.Max(0f, defaultLookAroundDuration);
        hasDestination = false;
        StopMovementImmediately();
        SetFacingPoint(position);
    }

    private void UpdateStationarySuspicion()
    {
        hasDestination = false;
        StopMovementImmediately();
        SetFacingPoint(lastKnownTargetPosition);

        if (Time.time < stationarySuspicionUntil)
            return;

        if (returnToStartAfterTemporaryStates)
            ReturnToStart();
        else
            ResumeStartingState();
    }

    private void PrepareInvestigativeState(Vector2 position, bool resetExternalInvestigation = true)
    {
        if (resetExternalInvestigation)
            ResetExternalInvestigationState();

        lastKnownTargetPosition = position;
        fleeCompleted = false;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;
        ClearAlertFocus();
    }

    private void SetInvestigativeDestination(Vector2 position, bool forceSearchPath)
    {
        lastKnownTargetPosition = position;
        if (hasDestination && (currentDestination - position).sqrMagnitude <= DestinationRefreshSqrDistance)
            return;

        currentDestination = position;
        hasDestination = true;
        SetDirectDestination(position, forceSearchPath);
    }

    private void RememberAlertStimulus(Vector2 position)
    {
        lastKnownTargetPosition = position;
        alertStimulusUntil = Time.time + alertTargetLostDuration;
    }

    private void ClearAlertFocus()
    {
        alertHasNoiseFocus = false;
        alertNoiseFocusUntil = float.NegativeInfinity;
        alertNoiseFocusPoint = Vector2.zero;
    }

    private bool HasActiveAlertStimulus()
    {
        return Time.time < alertStimulusUntil;
    }

    private void CacheReferences()
    {
        if (movementBody == null)
            movementBody = GetComponent<Rigidbody2D>();

        if (aiPath == null)
            aiPath = GetComponent<AIPath>();

        if (aiDestinationSetter == null)
            aiDestinationSetter = GetComponent<AIDestinationSetter>();

        if (seeker == null)
            seeker = GetComponent<Seeker>();
    }

    private void CaptureStartingTransform()
    {
        startingPosition = CurrentPosition;
        startingRotation = CurrentRotation;
    }

    private void ApplyRigidbodyRecommendations()
    {
        if (!applyRecommendedRigidbodySettings || movementBody == null)
            return;

        if (forceZeroGravity)
            movementBody.gravityScale = 0f;

        movementBody.interpolation = recommendedInterpolation;
        movementBody.collisionDetectionMode = recommendedCollisionDetection;
    }

    private void ConfigureAstarDriver()
    {
        if (aiPath == null)
            return;

        if (movementBody == null && debugMovement && !warnedAstarWithoutRigidbody)
        {
            warnedAstarWithoutRigidbody = true;
            Debug.LogWarning(
                $"{name} has AIPath but no Rigidbody2D. A* will still move the enemy, but it will not use Rigidbody2D-based top-down movement until you add one.",
                this);
        }

        aiPath.orientation = OrientationMode.YAxisForward;
        aiPath.enableRotation = !useCustomRotation;
        aiPath.updateRotation = !useCustomRotation;
        aiPath.maxAcceleration = AstarAccelerationOverride;
        aiPath.slowdownDistance = slowdownDistance;
        aiPath.endReachedDistance = stoppingDistance;
    }

    private bool CanIssueAstarSearchRequest()
    {
        return aiPath != null && (!Application.isPlaying || startupCompleted);
    }

    private void ClampSettings()
    {
        startingState = SanitizeStartingState(startingState);
        walkSpeed = Mathf.Max(MinimumSpeed, walkSpeed);
        runSpeed = Mathf.Max(walkSpeed, runSpeed);
        sprintSpeed = Mathf.Max(runSpeed, sprintSpeed);
        acceleration = Mathf.Max(MinimumAcceleration, acceleration);
        deceleration = Mathf.Max(MinimumAcceleration, deceleration);
        stoppingDistance = Mathf.Max(MinimumDistance, stoppingDistance);
        slowdownDistance = Mathf.Max(stoppingDistance, slowdownDistance);
        minimumMoveSpeed = Mathf.Clamp(minimumMoveSpeed, 0f, sprintSpeed);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        alertNoiseFocusDuration = Mathf.Max(0f, alertNoiseFocusDuration);
        alertTargetLostDuration = Mathf.Max(0f, alertTargetLostDuration);
        defaultLookAroundDuration = Mathf.Max(0f, defaultLookAroundDuration);
        lookAroundTurnInterval = Mathf.Max(MinimumInterval, lookAroundTurnInterval);
        lookAroundRotationSpeed = Mathf.Max(0f, lookAroundRotationSpeed);
        randomLookAngleRange = Mathf.Clamp(randomLookAngleRange, 0f, 360f);
        fleeStoppingDistance = Mathf.Max(MinimumDistance, fleeStoppingDistance);

        if (!useMovePosition && !useVelocityMovement)
            useMovePosition = true;

        if (useMovePosition && useVelocityMovement)
            useVelocityMovement = false;

        patrolPoints ??= new List<PatrolPoint>();
        itinerarySteps ??= new List<EnemyItineraryStep>();
    }

    private EnemyState SanitizeStartingState(EnemyState candidate)
    {
        return candidate switch
        {
            EnemyState.Idle => candidate,
            EnemyState.Patrol => candidate,
            EnemyState.Suspicious => candidate,
            EnemyState.Alert => candidate,
            EnemyState.Disabled => candidate,
            _ => EnemyState.Idle
        };
    }

    private bool CanEnterInvestigativeState()
    {
        return currentState != EnemyState.Fleeing &&
               currentState != EnemyState.Disabled &&
               currentState != EnemyState.Alert;
    }

    private bool ShouldShowFleeSettings()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint;
    }

    private bool ShouldShowMissingFleeFallback()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint;
    }

    private void WarnMissingMover()
    {
        if (warnedMissingMover || !debugMovement)
            return;

        warnedMissingMover = true;
        Debug.LogWarning($"{name} has no AIPath or Rigidbody2D movement driver. State changes will still work, but the enemy cannot move.", this);
    }

    private bool TryGetCurrentItineraryStep(out EnemyItineraryStep step)
    {
        step = null;
        if (!ShouldUseItinerary || currentItineraryIndex < 0 || currentItineraryIndex >= itinerarySteps.Count)
            return false;

        step = itinerarySteps[currentItineraryIndex];
        return step != null;
    }

    private bool TryGetActivePatrolPoints(out List<PatrolPoint> activePatrolPoints)
    {
        activePatrolPoints = patrolPoints;

        if (TryGetCurrentItineraryStep(out EnemyItineraryStep step) &&
            step.StepType == EnemyItineraryStepType.Patrol &&
            !step.UseControllerPatrolRoute)
        {
            activePatrolPoints = step.PatrolPoints;
        }

        return activePatrolPoints != null && activePatrolPoints.Count > 0;
    }

    private bool TryGetActivePatrolPointCount(out int patrolPointCount)
    {
        patrolPointCount = 0;
        if (!TryGetActivePatrolPoints(out List<PatrolPoint> activePatrolPoints))
            return false;

        patrolPointCount = activePatrolPoints.Count;
        return patrolPointCount > 0;
    }

    private EnemyPatrolMode GetActivePatrolMode()
    {
        if (TryGetCurrentItineraryStep(out EnemyItineraryStep step) && step.StepType == EnemyItineraryStepType.Patrol)
            return step.PatrolMode;

        return patrolMode;
    }

    private bool ShouldCompleteCurrentPatrolStepOnArrival()
    {
        if (!TryGetCurrentItineraryStep(out EnemyItineraryStep step) ||
            step.StepType != EnemyItineraryStepType.Patrol ||
            step.PatrolCompletionMode != EnemyItineraryPatrolCompletionMode.CompleteLoop)
        {
            return false;
        }

        if (!TryGetActivePatrolPointCount(out int patrolPointCount) || patrolPointCount <= 0)
            return false;

        EnemyPatrolMode activeMode = GetActivePatrolMode();
        if (activeMode == EnemyPatrolMode.Random)
        {
            itineraryRandomPatrolVisitCount++;
            return itineraryRandomPatrolVisitCount >= patrolPointCount;
        }

        return currentPatrolIndex >= patrolPointCount - 1;
    }

    private Vector2 ResolveIdleStepPosition(EnemyItineraryStep step)
    {
        if (step != null && step.IdlePoint != null)
            return step.IdlePoint.position;

        return startingPosition;
    }

    private bool IsWithinStoppingDistance(Vector2 position)
    {
        Vector2 delta = position - CurrentPosition;
        float activeStoppingDistance = ResolveCurrentStoppingDistance();
        return delta.sqrMagnitude <= activeStoppingDistance * activeStoppingDistance;
    }

    private bool IsAtAlertHoldPoint()
    {
        return alertHoldPoint == null || IsWithinStoppingDistance(alertHoldPoint.position);
    }

    private void ResetExternalInvestigationState()
    {
        hasExternalInvestigation = false;
        externalInvestigationState = EnemyState.Suspicious;
    }

    private void CacheAlertDefaultFacingAngle()
    {
        alertDefaultFacingAngle = CurrentRotation;

        if (alertFacing != null &&
            alertFacing.TryResolveAngle(alertHoldPoint, alertDefaultFacingAngle, out float resolvedAngle))
        {
            alertDefaultFacingAngle = resolvedAngle;
        }
    }

    private void ApplyAlertDefaultFacing()
    {
        if (currentState != EnemyState.Alert)
            return;

        SetManualFacingOverride(alertDefaultFacingAngle);
    }

    private void ApplyStartingPointFacingOverrideIfAvailable()
    {
        ApplyFacingOverrideIfAvailable(startingPointFacing, null, startingRotation);
    }

    private void ApplyIdleStepFacingOverrideIfAvailable(EnemyItineraryStep step)
    {
        if (step == null)
            return;

        ApplyFacingOverrideIfAvailable(step.IdleFacing, step.IdlePoint, startingRotation);
    }

    private void ApplyPatrolPointFacingOverrideIfAvailable(PatrolPoint patrolPoint)
    {
        if (patrolPoint == null)
            return;

        ApplyFacingOverrideIfAvailable(patrolPoint.ArrivalFacing, patrolPoint.Point, CurrentRotation);
    }

    private void ApplyFleePointFacingOverrideIfAvailable()
    {
        ApplyFacingOverrideIfAvailable(fleePointFacing, fleePoint, CurrentRotation);
    }

    private void ApplyFacingOverrideIfAvailable(EnemyFacingSettings facingSettings, Transform referenceTransform, float fallbackAngle)
    {
        if (facingSettings == null || !facingSettings.TryResolveAngle(referenceTransform, fallbackAngle, out float resolvedAngle))
            return;

        SetManualFacingOverride(resolvedAngle);
    }

    private void SetManualFacingOverride(float zAngle)
    {
        float radians = (zAngle - rotationAngleOffset) * Mathf.Deg2Rad;
        manualFacingDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        hasManualFacingOverride = manualFacingDirection.sqrMagnitude > MinimumDirectionSqr;
    }

    private Vector2 ResolveCurrentFacingDirection()
    {
        float radians = (CurrentRotation - rotationAngleOffset) * Mathf.Deg2Rad;
        Vector2 facingDirection = new(Mathf.Cos(radians), Mathf.Sin(radians));
        return facingDirection.sqrMagnitude > MinimumDirectionSqr ? facingDirection.normalized : Vector2.down;
    }

    private void ClearManualFacingOverride()
    {
        hasManualFacingOverride = false;
    }

    private bool ShouldUseItinerary => useItinerary && itinerarySteps != null && itinerarySteps.Count > 0;

    private Vector2 CurrentPosition => movementBody != null ? movementBody.position : (Vector2)transform.position;

    private float CurrentRotation => movementBody != null ? movementBody.rotation : transform.eulerAngles.z;

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            (vector.x * cos) - (vector.y * sin),
            (vector.x * sin) + (vector.y * cos));
    }

}
