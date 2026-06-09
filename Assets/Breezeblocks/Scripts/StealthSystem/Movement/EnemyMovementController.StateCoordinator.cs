using System.Collections.Generic;
using UnityEngine;

public partial class EnemyMovementController
{
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
    /// Advances the active high-level state update.
    /// </summary>
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

    /// <summary>
    /// Updates patrol movement, waiting, and point arrival flow.
    /// </summary>
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

    /// <summary>
    /// Updates suspicious and searching movement until the destination is reached.
    /// </summary>
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

    /// <summary>
    /// Updates timed look-around behavior and resolves the follow-up state.
    /// </summary>
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

    /// <summary>
    /// Completes return-to-start behavior once the destination is reached.
    /// </summary>
    private void UpdateReturnToStartState()
    {
        if (!hasReachedDestination)
            return;

        CompleteReturnState();
    }

    /// <summary>
    /// Updates alert movement, remembered stimulus chasing, and idle facing behavior.
    /// </summary>
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

    /// <summary>
    /// Keeps detected-state tracking data current.
    /// </summary>
    private void UpdateDetectedState()
    {
        if (detectedTarget == null)
            return;

        lastKnownTargetPosition = detectedTarget.position;
        if (ResolveDetectionBehavior() == EnemyDetectionBehavior.ChasePlayer)
            hasDestination = true;
    }

    /// <summary>
    /// Finalizes flee behavior after the flee destination is reached.
    /// </summary>
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

    /// <summary>
    /// Updates itinerary timers and step progression.
    /// </summary>
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

        if (!ShouldCountPatrolItineraryTime())
            return;

        itineraryStepRemainingTime = Mathf.Max(0f, itineraryStepRemainingTime - deltaTime);
        if (itineraryStepRemainingTime > 0f)
            return;

        itineraryPatrolCompletionPending = false;
        AdvanceItineraryStep();
    }

    /// <summary>
    /// Returns whether patrol itinerary time should tick during the current patrol sub-state.
    /// </summary>
    private bool ShouldCountPatrolItineraryTime()
    {
        if (currentState == EnemyState.LookAround)
            return currentLookAroundContext == EnemyLookAroundContext.Patrol;

        return patrolWaiting || hasReachedDestination;
    }

    /// <summary>
    /// Starts a directed temporary state toward a world-space position.
    /// </summary>
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

    /// <summary>
    /// Starts a timed look-around sequence for the provided context.
    /// </summary>
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

    /// <summary>
    /// Starts navigation back toward a default or itinerary destination.
    /// </summary>
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

    /// <summary>
    /// Completes return-state flow and resumes the appropriate default behavior.
    /// </summary>
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

    /// <summary>
    /// Enters idle state and stops movement immediately.
    /// </summary>
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

    /// <summary>
    /// Enters patrol state and optionally resets patrol progress.
    /// </summary>
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

    /// <summary>
    /// Enters detected state without applying a chase destination.
    /// </summary>
    private void EnterDetectedState()
    {
        ResetExternalInvestigationState();
        ClearManualFacingOverride();
        ChangeState(EnemyState.Detected);
        StopMovementImmediately();
    }

    /// <summary>
    /// Enters disabled state and halts all movement.
    /// </summary>
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

    /// <summary>
    /// Sets the active patrol point as the current destination.
    /// </summary>
    private void MoveToCurrentPatrolPoint()
    {
        if (!TryGetCurrentPatrolPoint(out PatrolPoint patrolPoint))
            return;

        hasDestination = true;
        currentDestination = patrolPoint.Point.position;
        SetDirectDestination(currentDestination, true);
    }

    /// <summary>
    /// Handles patrol point arrival, including waiting, facing, and itinerary completion.
    /// </summary>
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

    /// <summary>
    /// Advances the patrol route according to the configured patrol mode.
    /// </summary>
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
                    currentPatrolIndex = Random.Range(0, patrolPointCount);
                }
                while (currentPatrolIndex == previousIndex);
                break;
        }

        if (debugMovement)
            Debug.Log($"{name} advancing to patrol point {currentPatrolIndex}.", this);

        MoveToCurrentPatrolPoint();
    }

    /// <summary>
    /// Resolves the active patrol point entry.
    /// </summary>
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

    /// <summary>
    /// Starts itinerary execution from the first step.
    /// </summary>
    private void BeginItinerary()
    {
        itineraryFinished = false;
        currentItineraryIndex = itinerarySteps.Count > 0 ? 0 : -1;
        ConfigureCurrentItineraryStep(resetPatrolProgress: true, resetStepTimer: true);
        ResumeCurrentItineraryStep();
    }

    /// <summary>
    /// Advances to the next itinerary step or completes the itinerary.
    /// </summary>
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

    /// <summary>
    /// Resumes behavior for the current itinerary step.
    /// </summary>
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

    /// <summary>
    /// Resumes an idle itinerary step, optionally navigating back to its idle point first.
    /// </summary>
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

    /// <summary>
    /// Resumes a patrol itinerary step without resetting the patrol sequence.
    /// </summary>
    private void ResumePatrolItineraryStep()
    {
        EnterPatrolState(resetPatrolProgress: false);
    }

    /// <summary>
    /// Configures timers and patrol state for the current itinerary step.
    /// </summary>
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

    /// <summary>
    /// Resolves the duration that should be used for the provided itinerary step.
    /// </summary>
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

    /// <summary>
    /// Tries to resolve the destination that should be used when resuming an itinerary.
    /// </summary>
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

    /// <summary>
    /// Returns whether itinerary progression is currently paused by a temporary state.
    /// </summary>
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

    /// <summary>
    /// Resumes the configured default starting state when no itinerary is active.
    /// </summary>
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
}
