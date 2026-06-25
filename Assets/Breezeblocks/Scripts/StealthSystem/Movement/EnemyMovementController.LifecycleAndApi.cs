using Breezeblocks.Missions;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

public partial class EnemyMovementController
{
    /// <summary>
    /// Refreshes cached same-object references when the component is reset in the editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches runtime references and configures movement systems before play begins.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        CaptureStartingTransform();
        lastStableFacingDirection = ResolveCurrentFacingDirection();
        ApplyRigidbodyRecommendations();
        RefreshAstarDriverConfiguration();
        CharacterOrbitHandsAnimator.EnsureOn(gameObject);

        if (IsMissionStartupBlockingEnemyRuntime())
            HoldForMissionStartup();
    }

    /// <summary>
    /// Completes startup immediately when the mission does not block enemy runtime.
    /// </summary>
    private void Start()
    {
        if (IsMissionStartupBlockingEnemyRuntime())
        {
            HoldForMissionStartup();
            return;
        }

        CompleteStartup();
    }

    /// <summary>
    /// Keeps inspector-facing settings clamped and runtime references current in edit mode.
    /// </summary>
    private void OnValidate()
    {
        ClampSettings();
        CacheReferences();
        CacheDoorTraversalPreferences();

        if (!Application.isPlaying)
            CharacterOrbitHandsAnimator.EnsureOn(gameObject);

        if (!Application.isPlaying)
            ApplyRigidbodyRecommendations();

        RefreshAstarDriverConfiguration();
    }

    /// <summary>
    /// Advances high-level AI state and keeps A* destinations synchronized each frame.
    /// </summary>
    private void Update()
    {
        if (IsMissionStartupBlockingEnemyRuntime())
        {
            HoldForMissionStartup();
            return;
        }

        CompleteStartup();
        SyncRuntimeMovementState();
        UpdateStateMachine();
        UpdateItinerary(Time.deltaTime);
        SyncAstarTargets();
        TryAutoOpenDoorInPath();
    }

    /// <summary>
    /// Applies low-level movement and rotation during the physics step.
    /// </summary>
    private void FixedUpdate()
    {
        if (IsMissionStartupBlockingEnemyRuntime())
        {
            HoldForMissionStartup();
            return;
        }

        CompleteStartup();
        SyncRuntimeMovementState();
        UpdateMovementSpeed(Time.fixedDeltaTime);
        ApplyMovementDriver();
        UpdateRotation(Time.fixedDeltaTime);
    }

    /// <summary>
    /// Warns when the controller is disabled unexpectedly during play.
    /// </summary>
    private void OnDisable()
    {
        ClearAutoDoorTraversal();
        ClearPendingAutoDoorClosure();

        if (debugMovement && Application.isPlaying)
            Debug.LogWarning($"{name} EnemyMovementController component was disabled externally.", this);
    }

    /// <summary>
    /// Forces the enemy into the idle state from the inspector.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceIdle()
    {
        SetState(EnemyState.Idle);
    }

    /// <summary>
    /// Forces the enemy into the patrol state from the inspector.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForcePatrol()
    {
        SetState(EnemyState.Patrol);
    }

    /// <summary>
    /// Forces a search at the current player position when a player visibility target exists.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceSearchAtCurrentPlayerPosition()
    {
        EnemyVisionAI visionAI = GetComponent<EnemyVisionAI>();
        if (visionAI?.TargetVisibilityComponent != null)
        {
            SearchAt(visionAI.TargetVisibilityComponent.SamplePosition);
            return;
        }

        if (visionAI?.TargetTransform != null)
            SearchAt(visionAI.TargetTransform.position);
    }

    /// <summary>
    /// Forces the enemy to start a manual look-around sequence from the inspector.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceLookAround()
    {
        BeginLookAround(defaultLookAroundDuration, lookAroundTurnInterval, EnemyLookAroundContext.Manual);
    }

    /// <summary>
    /// Forces the enemy to return to its starting or itinerary destination.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceReturnToStart()
    {
        ReturnToStart();
    }

    /// <summary>
    /// Forces the enemy into flee behavior from the inspector.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void ForceFlee()
    {
        Flee();
    }

    /// <summary>
    /// Restores movement/pathing runtime state after mission intro gating ends.
    /// </summary>
    public void HandleMissionGameplayStarted()
    {
        if (IsMissionStartupBlockingEnemyRuntime())
            return;

        if (movementBody != null)
        {
            movementBody.linearVelocity = Vector2.zero;
            movementBody.angularVelocity = 0f;
        }

        if (aiPath != null)
        {
            aiPath.canMove = true;
            aiPath.isStopped = false;
            aiPath.Teleport(CurrentPosition, false);
        }

        CompleteStartup();
        RefreshRuntimeNavigationState();
        SyncRuntimeMovementState();
    }

    /// <summary>
    /// Refreshes navigation state after dynamic environment pathing changes.
    /// </summary>
    public void HandleEnvironmentPathingChanged()
    {
        if (currentState == EnemyState.Disabled)
            return;

        RefreshRuntimeNavigationState();
    }

    /// <summary>
    /// Returns whether the enemy may currently react to noise.
    /// </summary>
    public bool CanReactToNoise()
    {
        return currentState != EnemyState.Detected &&
               currentState != EnemyState.Fleeing &&
               currentState != EnemyState.Disabled;
    }

    /// <summary>
    /// Returns whether the player should be treated as fully detected right now.
    /// </summary>
    public bool IsPlayerFullyDetectedState()
    {
        return currentState == EnemyState.Detected ||
               currentState == EnemyState.Fleeing ||
               (currentState == EnemyState.Alert && detectedTarget != null);
    }

    /// <summary>
    /// Returns whether the current state is a temporary interruption of default behavior.
    /// </summary>
    public bool IsTemporaryState()
    {
        return currentState == EnemyState.Suspicious ||
               currentState == EnemyState.Searching ||
               currentState == EnemyState.LookAround ||
               currentState == EnemyState.ReturningToStart;
    }

    /// <summary>
    /// Overrides detected-state movement with a direct destination and speed type.
    /// </summary>
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

    /// <summary>
    /// Applies or clears stagger movement overrides.
    /// </summary>
    public void SetStaggerOverride(bool active, float moveSpeedOverride, float turnSpeedMultiplier)
    {
        staggerOverrideActive = active;
        staggeredMoveSpeedOverride = Mathf.Max(0f, moveSpeedOverride);
        staggerTurnSpeedMultiplier = active ? Mathf.Clamp01(turnSpeedMultiplier) : 1f;
    }

    /// <summary>
    /// Applies movement settings loaded from an actor profile.
    /// </summary>
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
        allowClosedDoorTraversalWhilePatrol = settings.AllowClosedDoorTraversalWhilePatrol;
        allowClosedDoorTraversalWhileAlert = settings.AllowClosedDoorTraversalWhileAlert;
        allowClosedDoorTraversalWhileSuspicious = settings.AllowClosedDoorTraversalWhileSuspicious;
        allowClosedDoorTraversalWhileSearching = settings.AllowClosedDoorTraversalWhileSearching;
        allowClosedDoorTraversalWhileReturningToStart = settings.AllowClosedDoorTraversalWhileReturningToStart;
        allowClosedDoorTraversalWhileFleeing = settings.AllowClosedDoorTraversalWhileFleeing;
        allowClosedDoorTraversalWhileDetected = settings.AllowClosedDoorTraversalWhileDetected;
        closedDoorPathTag = settings.ClosedDoorPathTag;
        closedDoorTagPenalty = settings.ClosedDoorTagPenalty;
        closedDoorPatrolTagPenalty = settings.ClosedDoorPatrolTagPenalty;
        doorDetectionMask = settings.DoorDetectionMask;
        doorAutoOpenRange = settings.DoorAutoOpenRange;
        doorAutoOpenRadius = settings.DoorAutoOpenRadius;
        doorAutoOpenCooldown = settings.DoorAutoOpenCooldown;
        doorPreferredRouteProbeDistance = settings.DoorPreferredRouteProbeDistance;
        doorPreferredRouteProbeWidth = settings.DoorPreferredRouteProbeWidth;
        closeDoorsAfterPassing = settings.CloseDoorsAfterPassing;
        relockIgnoredLockedDoorsAfterPassing = settings.RelockIgnoredLockedDoorsAfterPassing;
        doorCloseAfterPassDistance = settings.DoorCloseAfterPassDistance;
        doorCloseAfterOpenDelay = settings.DoorCloseAfterOpenDelay;

        ClampSettings();
        ApplyRigidbodyRecommendations();
        RefreshAstarDriverConfiguration();
    }

    /// <summary>
    /// Applies doorbell reaction settings loaded from the enemy actor profile.
    /// </summary>
    public void ApplyDoorBellReactionSettings(EnemyDoorBellReactionSettings settings)
    {
        if (settings == null)
            return;

        reactToDoorBell = settings.ReactToDoorBell;
        doorBellReactionsBeforeAlert = Mathf.Max(0, settings.ReactionsBeforeAlert);
        doorBellRepeatIgnoreDuration = Mathf.Max(0f, settings.RepeatIgnoreDuration);
        doorBellReactionSpeed = settings.MoveSpeed;
        doorBellStandDuration = Mathf.Max(0f, settings.StandDuration);
        doorBellLookAroundDuration = Mathf.Max(0f, settings.LookAroundDuration);
        doorBellLookAroundTurnInterval = Mathf.Max(0.02f, settings.LookAroundTurnInterval);
    }

    /// <summary>
    /// Stops detected-state movement while keeping the override active.
    /// </summary>
    public void HoldDetectedPosition()
    {
        if (!UsesCombatMovementOverridesForState(currentState))
            return;

        hasDetectedMovementOverride = true;
        StopMovementImmediately();
    }

    /// <summary>
    /// Clears detected-state movement overrides and resumes default detected behavior when requested.
    /// </summary>
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

    /// <summary>
    /// Sets a manual facing direction toward a world-space point.
    /// </summary>
    public void SetFacingPoint(Vector2 worldPoint)
    {
        Vector2 toPoint = worldPoint - CurrentPosition;
        if (toPoint.sqrMagnitude <= MinimumDirectionSqr)
            return;

        manualFacingDirection = toPoint.normalized;
        hasManualFacingOverride = true;
    }

    /// <summary>
    /// Applies an externally provided facing direction override.
    /// </summary>
    public void SetExternalFacingDirection(Vector2 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= MinimumDirectionSqr)
            return;

        externalFacingDirection = worldDirection.normalized;
        hasExternalFacingOverride = true;
    }

    /// <summary>
    /// Clears the external facing override.
    /// </summary>
    public void ClearExternalFacingOverride()
    {
        hasExternalFacingOverride = false;
    }

    /// <summary>
    /// Applies an external turn speed override.
    /// </summary>
    public void SetExternalTurnSpeedOverride(bool active, float turnSpeed)
    {
        hasExternalTurnSpeedOverride = active && turnSpeed > 0f;
        externalTurnSpeedOverride = hasExternalTurnSpeedOverride ? Mathf.Max(0f, turnSpeed) : -1f;
    }

    /// <summary>
    /// Clears the current manual facing override.
    /// </summary>
    public void ClearFacingOverride()
    {
        ClearManualFacingOverride();
    }
}
