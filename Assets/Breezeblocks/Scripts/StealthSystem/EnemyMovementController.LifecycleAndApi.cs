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
        ConfigureAstarDriver();
        CacheDoorTraversalPreferences();
        ApplyDoorTraversalPreferencesIfNeeded(force: true);
        RefreshDoorDetectionContactFilter();
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

        ConfigureAstarDriver();
        RefreshDoorDetectionContactFilter();
        doorTraversalPreferenceDirty = true;
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
    /// Routes an external state request into the correct behavior entry point.
    /// </summary>
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

    /// <summary>
    /// Starts suspicious investigation behavior at the given world position.
    /// </summary>
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

    /// <summary>
    /// Starts searching behavior at the given world position.
    /// </summary>
    public void SearchAt(Vector2 position)
    {
        if (!CanEnterInvestigativeState())
            return;

        PrepareInvestigativeState(position);
        BeginDirectedState(EnemyState.Searching, position);
    }

    /// <summary>
    /// Starts an externally owned suspicious or searching investigation.
    /// </summary>
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

    /// <summary>
    /// Updates the active search destination or starts a search if needed.
    /// </summary>
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

    /// <summary>
    /// Updates the active investigative destination, respecting alert behavior.
    /// </summary>
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

    /// <summary>
    /// Refreshes suspicion toward a world position without restarting equivalent behavior unnecessarily.
    /// </summary>
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

    /// <summary>
    /// Routes heard noise into alert or investigation behavior.
    /// </summary>
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

    /// <summary>
    /// Cancels temporary investigation states and resumes default behavior.
    /// </summary>
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

    /// <summary>
    /// Clears an externally owned investigation and optionally resumes default behavior.
    /// </summary>
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

    /// <summary>
    /// Handles full player detection and transitions into the configured detected behavior.
    /// </summary>
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

    /// <summary>
    /// Handles loss of the actively detected target.
    /// </summary>
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

    /// <summary>
    /// Starts flee behavior or its configured fallback when no flee point is available.
    /// </summary>
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

    /// <summary>
    /// Returns the enemy to its starting point or active itinerary destination.
    /// </summary>
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

    /// <summary>
    /// Enters alert mode and optionally moves toward the configured alert hold point.
    /// </summary>
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

    /// <summary>
    /// Focuses alert behavior on a temporary point of interest.
    /// </summary>
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

    /// <summary>
    /// Promotes an extreme noise event into alert behavior.
    /// </summary>
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

    /// <summary>
    /// Registers a visual target while the enemy is already alert.
    /// </summary>
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

    /// <summary>
    /// Clears the visual target tracked by alert behavior while preserving last known stimulus.
    /// </summary>
    public void ClearAlertVisualTarget()
    {
        if (currentState != EnemyState.Alert)
            return;

        detectedTarget = null;
        hasDetectedMovementOverride = false;
        RememberAlertStimulus(lastKnownTargetPosition);
    }

    /// <summary>
    /// Resumes the configured default behavior after a temporary interruption.
    /// </summary>
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
        ApplyDoorTraversalPreferencesIfNeeded(force: true);
        RefreshRuntimeNavigationForCurrentState();
        SyncAstarTargets();
        SyncRuntimeMovementState();
    }

    /// <summary>
    /// Refreshes navigation state after dynamic environment pathing changes.
    /// </summary>
    public void HandleEnvironmentPathingChanged()
    {
        if (currentState == EnemyState.Disabled)
            return;

        ApplyDoorTraversalPreferencesIfNeeded(force: true);
        RefreshRuntimeNavigationForCurrentState();
        SyncAstarTargets();
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
        allowClosedDoorTraversalWhileFleeing = settings.AllowClosedDoorTraversalWhileFleeing;
        allowClosedDoorTraversalWhileDetected = settings.AllowClosedDoorTraversalWhileDetected;
        closedDoorPathTag = settings.ClosedDoorPathTag;
        closedDoorTagPenalty = settings.ClosedDoorTagPenalty;
        closedDoorPatrolTagPenalty = settings.ClosedDoorPatrolTagPenalty;
        doorDetectionMask = settings.DoorDetectionMask;
        doorAutoOpenRange = settings.DoorAutoOpenRange;
        doorAutoOpenRadius = settings.DoorAutoOpenRadius;
        doorAutoOpenCooldown = settings.DoorAutoOpenCooldown;

        ClampSettings();
        ApplyRigidbodyRecommendations();
        ConfigureAstarDriver();
        CacheDoorTraversalPreferences();
        ApplyDoorTraversalPreferencesIfNeeded(force: true);
        RefreshDoorDetectionContactFilter();
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
