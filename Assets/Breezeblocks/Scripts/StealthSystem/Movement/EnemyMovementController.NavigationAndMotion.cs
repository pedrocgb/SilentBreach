using System.Collections.Generic;
using Breezeblocks.Missions;
using UnityEngine;

public partial class EnemyMovementController
{
    /// <summary>
    /// Returns whether supplied state supports detected-state movement overrides.
    /// </summary>
    private bool UsesCombatMovementOverridesForState(EnemyState state)
    {
        return state == EnemyState.Detected || state == EnemyState.Alert;
    }

    /// <summary>
    /// Resolves effective detection behavior after fallback rules.
    /// </summary>
    private EnemyDetectionBehavior ResolveDetectionBehavior()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint && !canFlee
            ? missingFleePointFallbackBehavior == EnemyDetectionBehavior.FleeToPoint
                ? EnemyDetectionBehavior.StandStill
                : missingFleePointFallbackBehavior
            : detectionBehavior;
    }

    /// <summary>
    /// Resolves most relevant current target position for debugging and UI.
    /// </summary>
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

    /// <summary>
    /// Changes high-level state and updates dependent runtime navigation settings.
    /// </summary>
    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState)
            return;

        EnemyState oldState = currentState;
        previousState = currentState;
        currentState = newState;
        doorTraversalPreferenceDirty = true;

        if (!UsesCombatMovementOverridesForState(newState))
            hasDetectedMovementOverride = false;

        ApplyDoorTraversalPreferencesIfNeeded();

        if (debugMovement)
            Debug.Log($"{name} state changed from {previousState} to {currentState}.", this);

        MissionRuntimeEvents.RaiseEnemyStateChanged(this, oldState, newState);
        StateChanged?.Invoke(oldState, newState);
    }

    /// <summary>
    /// Starts stationary suspicious behavior toward point of interest.
    /// </summary>
    private void BeginSuspiciousFocusState(Vector2 position)
    {
        detectedTarget = null;
        currentLookAroundContext = EnemyLookAroundContext.None;
        currentReturnContext = EnemyReturnContext.None;
        ChangeState(EnemyState.Suspicious);
        RefreshStationarySuspicion(position);
    }

    /// <summary>
    /// Refreshes timer and facing used by stationary suspicious behavior.
    /// </summary>
    private void RefreshStationarySuspicion(Vector2 position)
    {
        lastKnownTargetPosition = position;
        stationarySuspicionUntil = Time.time + Mathf.Max(0f, defaultLookAroundDuration);
        hasDestination = false;
        StopMovementImmediately();
        SetFacingPoint(position);
    }

    /// <summary>
    /// Updates stationary suspicious behavior until it times out.
    /// </summary>
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

    /// <summary>
    /// Clears transient movement state before entering suspicious or searching behavior.
    /// </summary>
    private void PrepareInvestigativeState(Vector2 position, bool resetExternalInvestigation = true)
    {
        if (resetExternalInvestigation)
            ResetExternalInvestigationState();

        ResetDoorBellReactionState();
        lastKnownTargetPosition = position;
        fleeCompleted = false;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;
        ClearAlertFocus();
    }

    /// <summary>
    /// Updates active investigative destination.
    /// </summary>
    private void SetInvestigativeDestination(Vector2 position, bool forceSearchPath)
    {
        lastKnownTargetPosition = position;
        if (hasDestination && (currentDestination - position).sqrMagnitude <= DestinationRefreshSqrDistance)
            return;

        currentDestination = position;
        hasDestination = true;
        SetDirectDestination(position, forceSearchPath);
    }

    /// <summary>
    /// Remembers latest alert stimulus location and refreshes its timeout.
    /// </summary>
    private void RememberAlertStimulus(Vector2 position)
    {
        lastKnownTargetPosition = position;
        alertStimulusUntil = Time.time + alertTargetLostDuration;
    }

    /// <summary>
    /// Clears current alert noise focus.
    /// </summary>
    private void ClearAlertFocus()
    {
        alertHasNoiseFocus = false;
        alertNoiseFocusUntil = float.NegativeInfinity;
        alertNoiseFocusPoint = Vector2.zero;
    }

    /// <summary>
    /// Returns whether alert stimulus is still active.
    /// </summary>
    private bool HasActiveAlertStimulus()
    {
        return Time.time < alertStimulusUntil;
    }

    /// <summary>
    /// Caches optional same-object movement and A* components.
    /// </summary>
    private void CacheReferences()
    {
        movementBody = GetComponent<Rigidbody2D>();
        aiPath = GetComponent<Pathfinding.AIPath>();
        aiDestinationSetter = GetComponent<Pathfinding.AIDestinationSetter>();
        seeker = GetComponent<Pathfinding.Seeker>();
    }

    /// <summary>
    /// Returns whether global mission startup currently blocks enemy runtime.
    /// </summary>
    private static bool IsMissionStartupBlockingEnemyRuntime()
    {
        return GameplayMissionController.EnemyRuntimeBlockedAtMissionStart;
    }

    /// <summary>
    /// Completes one-time startup and enters default behavior.
    /// </summary>
    private void CompleteStartup()
    {
        if (startupCompleted)
            return;

        startupCompleted = true;

        if (ShouldUseItinerary)
        {
            BeginItinerary();
            return;
        }

        ResumeStartingStateWithoutItinerary();
    }

    /// <summary>
    /// Clamps editable settings into safe runtime ranges.
    /// </summary>
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
        closedDoorPathTag = Mathf.Clamp(closedDoorPathTag, 0, 31);
        closedDoorTagPenalty = Mathf.Max(0, closedDoorTagPenalty);
        closedDoorPatrolTagPenalty = Mathf.Max(0, closedDoorPatrolTagPenalty);
        doorAutoOpenRange = Mathf.Max(MinimumDoorAutoOpenRange, doorAutoOpenRange);
        doorAutoOpenRadius = Mathf.Max(MinimumDoorAutoOpenRadius, doorAutoOpenRadius);
        doorAutoOpenCooldown = Mathf.Max(0f, doorAutoOpenCooldown);
        doorPreferredRouteProbeDistance = Mathf.Max(MinimumDoorAutoOpenRange, doorPreferredRouteProbeDistance);
        doorPreferredRouteProbeWidth = Mathf.Max(MinimumDoorRouteProbeWidth, doorPreferredRouteProbeWidth);
        doorCloseAfterPassDistance = Mathf.Max(MinimumDistance, doorCloseAfterPassDistance);
        doorCloseAfterOpenDelay = Mathf.Max(0f, doorCloseAfterOpenDelay);
        doorBellReactionsBeforeAlert = Mathf.Max(0, doorBellReactionsBeforeAlert);
        doorBellRepeatIgnoreDuration = Mathf.Max(0f, doorBellRepeatIgnoreDuration);
        doorBellStandDuration = Mathf.Max(0f, doorBellStandDuration);
        doorBellLookAroundDuration = Mathf.Max(0f, doorBellLookAroundDuration);
        doorBellLookAroundTurnInterval = Mathf.Max(MinimumInterval, doorBellLookAroundTurnInterval);

        if (!useMovePosition && !useVelocityMovement)
            useMovePosition = true;

        if (useMovePosition && useVelocityMovement)
            useVelocityMovement = false;

        patrolPoints ??= new List<PatrolPoint>();
        itinerarySteps ??= new List<EnemyItineraryStep>();
    }

    /// <summary>
    /// Restricts starting state to values supported by startup flow.
    /// </summary>
    private EnemyState SanitizeStartingState(EnemyState candidate)
    {
        return candidate switch
        {
            EnemyState.Idle => candidate,
            EnemyState.Patrol => candidate,
            EnemyState.Suspicious => candidate,
            EnemyState.Alert => candidate,
            EnemyState.Disabled => candidate,
            EnemyState.Sleeping => candidate,
            _ => EnemyState.Idle
        };
    }

    /// <summary>
    /// Returns whether suspicious/searching behavior may begin in current state.
    /// </summary>
    private bool CanEnterInvestigativeState()
    {
        return currentState != EnemyState.Fleeing &&
               currentState != EnemyState.Disabled &&
               currentState != EnemyState.Sleeping &&
               currentState != EnemyState.Alert;
    }

    /// <summary>
    /// Returns whether a doorbell reaction may interrupt the current low-priority state.
    /// </summary>
    private bool CanEnterDoorBellReactionState()
    {
        return reactToDoorBell &&
               !doorBellReactionActive &&
               (currentState == EnemyState.Idle || currentState == EnemyState.Patrol);
    }

    /// <summary>
    /// Returns whether flee settings should be shown in inspector.
    /// </summary>
    private bool ShouldShowFleeSettings()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint;
    }

    /// <summary>
    /// Returns whether missing-flee fallback settings should be shown in inspector.
    /// </summary>
    private bool ShouldShowMissingFleeFallback()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint;
    }

    /// <summary>
    /// Resolves current itinerary step if one is active.
    /// </summary>
    private bool TryGetCurrentItineraryStep(out EnemyItineraryStep step)
    {
        step = null;
        if (!ShouldUseItinerary || currentItineraryIndex < 0 || currentItineraryIndex >= itinerarySteps.Count)
            return false;

        step = itinerarySteps[currentItineraryIndex];
        return step != null;
    }

    /// <summary>
    /// Resolves active patrol route for current controller or itinerary step.
    /// </summary>
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

    /// <summary>
    /// Resolves number of active patrol points.
    /// </summary>
    private bool TryGetActivePatrolPointCount(out int patrolPointCount)
    {
        patrolPointCount = 0;
        if (!TryGetActivePatrolPoints(out List<PatrolPoint> activePatrolPoints))
            return false;

        patrolPointCount = activePatrolPoints.Count;
        return patrolPointCount > 0;
    }

    /// <summary>
    /// Resolves patrol mode currently driving patrol logic.
    /// </summary>
    private EnemyPatrolMode GetActivePatrolMode()
    {
        if (TryGetCurrentItineraryStep(out EnemyItineraryStep step) && step.StepType == EnemyItineraryStepType.Patrol)
            return step.PatrolMode;

        return patrolMode;
    }

    /// <summary>
    /// Returns whether current patrol itinerary step should complete on this arrival.
    /// </summary>
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

    /// <summary>
    /// Resolves idle destination for itinerary step.
    /// </summary>
    private Vector2 ResolveIdleStepPosition(EnemyItineraryStep step)
    {
        if (step != null && step.IdlePoint != null)
            return step.IdlePoint.position;

        return startingPosition;
    }

    /// <summary>
    /// Returns whether supplied position is within active stopping distance.
    /// </summary>
    private bool IsWithinStoppingDistance(Vector2 position)
    {
        Vector2 delta = position - CurrentPosition;
        float activeStoppingDistance = ResolveCurrentStoppingDistance();
        return delta.sqrMagnitude <= activeStoppingDistance * activeStoppingDistance;
    }

    /// <summary>
    /// Returns whether enemy is already at its alert hold point.
    /// </summary>
    private bool IsAtAlertHoldPoint()
    {
        return alertHoldPoint == null || IsWithinStoppingDistance(alertHoldPoint.position);
    }

    /// <summary>
    /// Clears state used by externally owned investigations.
    /// </summary>
    private void ResetExternalInvestigationState()
    {
        hasExternalInvestigation = false;
        externalInvestigationState = EnemyState.Suspicious;
    }

    /// <summary>
    /// Clears the active doorbell reaction without changing remembered reaction counts.
    /// </summary>
    private void ResetDoorBellReactionState()
    {
        activeDoorBell = null;
        doorBellReactionActive = false;
        doorBellWaitingAtTarget = false;
        doorBellStandUntil = float.NegativeInfinity;
    }

    /// <summary>
    /// Caches default facing angle used by alert hold behavior.
    /// </summary>
    private void CacheAlertDefaultFacingAngle()
    {
        alertDefaultFacingAngle = CurrentRotation;

        if (alertFacing != null &&
            alertFacing.TryResolveAngle(alertHoldPoint, alertDefaultFacingAngle, out float resolvedAngle))
        {
            alertDefaultFacingAngle = resolvedAngle;
        }
    }

    /// <summary>
    /// Applies cached default alert facing override.
    /// </summary>
    private void ApplyAlertDefaultFacing()
    {
        if (currentState != EnemyState.Alert)
            return;

        SetManualFacingOverride(alertDefaultFacingAngle);
    }

    /// <summary>
    /// Applies any starting-point facing override.
    /// </summary>
    private void ApplyStartingPointFacingOverrideIfAvailable()
    {
        ApplyFacingOverrideIfAvailable(startingPointFacing, null, startingRotation);
    }

    /// <summary>
    /// Applies any idle-step facing override.
    /// </summary>
    private void ApplyIdleStepFacingOverrideIfAvailable(EnemyItineraryStep step)
    {
        if (step == null)
            return;

        ApplyFacingOverrideIfAvailable(step.IdleFacing, step.IdlePoint, startingRotation);
    }

    /// <summary>
    /// Applies any patrol-point arrival facing override.
    /// </summary>
    private void ApplyPatrolPointFacingOverrideIfAvailable(PatrolPoint patrolPoint)
    {
        if (patrolPoint == null)
            return;

        ApplyFacingOverrideIfAvailable(patrolPoint.ArrivalFacing, patrolPoint.Point, CurrentRotation);
    }

    /// <summary>
    /// Applies any flee-point facing override.
    /// </summary>
    private void ApplyFleePointFacingOverrideIfAvailable()
    {
        ApplyFacingOverrideIfAvailable(fleePointFacing, fleePoint, CurrentRotation);
    }

    /// <summary>
    /// Applies provided facing override settings when they resolve to valid angle.
    /// </summary>
    private void ApplyFacingOverrideIfAvailable(EnemyFacingSettings facingSettings, Transform referenceTransform, float fallbackAngle)
    {
        if (facingSettings == null || !facingSettings.TryResolveAngle(referenceTransform, fallbackAngle, out float resolvedAngle))
            return;

        SetManualFacingOverride(resolvedAngle);
    }
}
