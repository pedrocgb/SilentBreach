using System;
using System.Collections.Generic;
using Breezeblocks.Missions;
using Pathfinding;
using UnityEngine;

public partial class EnemyMovementController
{
    /// <summary>
    /// Synchronizes the active high-level target into the A* destination driver.
    /// </summary>
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

    /// <summary>
    /// Immediately clears destinations and stops the current movement driver.
    /// </summary>
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

    /// <summary>
    /// Switches navigation into follow-target mode.
    /// </summary>
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
            ApplyDoorTraversalPreferencesIfNeeded();
            aiPath.destination = currentDestination;
            if (forceSearchPath && CanIssueAstarSearchRequest())
                aiPath.SearchPath();
        }
    }

    /// <summary>
    /// Switches navigation into direct-destination mode.
    /// </summary>
    private void SetDirectDestination(Vector2 destination, bool forceSearchPath)
    {
        ClearManualFacingOverride();
        currentDestination = destination;
        hasDestination = true;

        if (aiDestinationSetter != null)
            aiDestinationSetter.target = null;

        if (aiPath != null)
        {
            ApplyDoorTraversalPreferencesIfNeeded();
            aiPath.destination = destination;
            if (forceSearchPath && CanIssueAstarSearchRequest())
                aiPath.SearchPath();
        }
    }

    /// <summary>
    /// Updates the direct destination only when it changed enough to matter.
    /// </summary>
    private void SetDestinationIfChanged(Vector2 destination, bool forceSearchPath)
    {
        if (hasDestination && (currentDestination - destination).sqrMagnitude <= DestinationRefreshSqrDistance)
            return;

        SetDirectDestination(destination, forceSearchPath);
    }

    /// <summary>
    /// Refreshes runtime movement state derived from the active movement driver.
    /// </summary>
    private void SyncRuntimeMovementState()
    {
        currentMovementSpeed = ResolveActualMovementSpeed();
        isMoving = currentMovementSpeed > minimumMoveSpeed;
        hasReachedDestination = EvaluateHasReachedDestination();
    }

    /// <summary>
    /// Resolves the current movement speed from A* or Rigidbody2D.
    /// </summary>
    private float ResolveActualMovementSpeed()
    {
        if (aiPath != null)
            return aiPath.velocity.magnitude;

        if (movementBody != null)
            return movementBody.linearVelocity.magnitude;

        return 0f;
    }

    /// <summary>
    /// Returns whether the active destination has been reached.
    /// </summary>
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

    /// <summary>
    /// Resolves the desired movement speed for the current high-level state.
    /// </summary>
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

    /// <summary>
    /// Resolves a speed type into its configured scalar value.
    /// </summary>
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

    /// <summary>
    /// Resolves the stopping distance for the active state.
    /// </summary>
    private float ResolveCurrentStoppingDistance()
    {
        return currentState == EnemyState.Fleeing ? Mathf.Max(stoppingDistance, fleeStoppingDistance) : stoppingDistance;
    }

    /// <summary>
    /// Returns whether the supplied state supports detected-state movement overrides.
    /// </summary>
    private bool UsesCombatMovementOverridesForState(EnemyState state)
    {
        return state == EnemyState.Detected || state == EnemyState.Alert;
    }

    /// <summary>
    /// Resolves the effective detection behavior after fallback rules.
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
    /// Resolves the most relevant current target position for debugging and UI.
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
    /// Resolves the desired facing direction based on state, movement, and overrides.
    /// </summary>
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

    /// <summary>
    /// Returns whether the enemy should actively face its tracked target.
    /// </summary>
    private bool ShouldFaceTrackedTarget()
    {
        if (!faceTargetWhenDetected || detectedTarget == null)
            return false;

        return currentState == EnemyState.Detected ||
               currentState == EnemyState.Alert;
    }

    /// <summary>
    /// Resolves the current movement vector from the active movement driver.
    /// </summary>
    private Vector2 ResolveMovementVector()
    {
        if (aiPath != null)
            return aiPath.velocity;

        if (movementBody != null)
            return movementBody.linearVelocity;

        return Vector2.zero;
    }

    /// <summary>
    /// Resolves a stable facing vector based on path steering, velocity, or destination.
    /// </summary>
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

    /// <summary>
    /// Picks the next randomized look-around direction.
    /// </summary>
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
    /// Starts stationary suspicious behavior toward a point of interest.
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
    /// Refreshes the timer and facing used by stationary suspicious behavior.
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

        lastKnownTargetPosition = position;
        fleeCompleted = false;
        patrolWaiting = false;
        itineraryPatrolCompletionPending = false;
        ClearAlertFocus();
    }

    /// <summary>
    /// Updates the active investigative destination.
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
    /// Remembers the latest alert stimulus location and refreshes its timeout.
    /// </summary>
    private void RememberAlertStimulus(Vector2 position)
    {
        lastKnownTargetPosition = position;
        alertStimulusUntil = Time.time + alertTargetLostDuration;
    }

    /// <summary>
    /// Clears the current alert noise focus.
    /// </summary>
    private void ClearAlertFocus()
    {
        alertHasNoiseFocus = false;
        alertNoiseFocusUntil = float.NegativeInfinity;
        alertNoiseFocusPoint = Vector2.zero;
    }

    /// <summary>
    /// Returns whether an alert stimulus is still active.
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
        aiPath = GetComponent<AIPath>();
        aiDestinationSetter = GetComponent<AIDestinationSetter>();
        seeker = GetComponent<Seeker>();
    }

    /// <summary>
    /// Captures the starting transform used for return behavior.
    /// </summary>
    private void CaptureStartingTransform()
    {
        startingPosition = CurrentPosition;
        startingRotation = CurrentRotation;
    }

    /// <summary>
    /// Applies recommended Rigidbody2D settings for top-down movement.
    /// </summary>
    private void ApplyRigidbodyRecommendations()
    {
        if (!applyRecommendedRigidbodySettings || movementBody == null)
            return;

        if (forceZeroGravity)
            movementBody.gravityScale = 0f;

        movementBody.interpolation = recommendedInterpolation;
        movementBody.collisionDetectionMode = recommendedCollisionDetection;
    }

    /// <summary>
    /// Configures AIPath to act as a low-level mover while this controller owns high-level state.
    /// </summary>
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

    /// <summary>
    /// Returns whether forced A* path searches may be issued right now.
    /// </summary>
    private bool CanIssueAstarSearchRequest()
    {
        return aiPath != null && (!Application.isPlaying || startupCompleted);
    }

    /// <summary>
    /// Rebuilds the contact filter used by automatic door opening.
    /// </summary>
    private void RefreshDoorDetectionContactFilter()
    {
        doorDetectionContactFilter = default;
        doorDetectionContactFilter.useLayerMask = true;
        doorDetectionContactFilter.layerMask = doorDetectionMask;
        doorDetectionContactFilter.useTriggers = true;
    }

    /// <summary>
    /// Caches the seeker's default traversable tags and penalties.
    /// </summary>
    private void CacheDoorTraversalPreferences()
    {
        if (seeker == null)
            return;

        defaultTraversableTags = seeker.traversableTags;
        if (defaultTagPenalties == null || defaultTagPenalties.Length != 32)
            defaultTagPenalties = new int[32];

        if (seeker.tagPenalties != null)
            Array.Copy(seeker.tagPenalties, defaultTagPenalties, Mathf.Min(defaultTagPenalties.Length, seeker.tagPenalties.Length));
        else
            Array.Clear(defaultTagPenalties, 0, defaultTagPenalties.Length);

        doorTraversalPreferencesInitialized = true;
        doorTraversalPreferenceDirty = true;
    }

    /// <summary>
    /// Applies door traversal tags and penalties that match the current state.
    /// </summary>
    private void ApplyDoorTraversalPreferencesIfNeeded(bool force = false)
    {
        if (seeker == null)
            return;

        if (!doorTraversalPreferencesInitialized || defaultTagPenalties == null || defaultTagPenalties.Length != 32)
            CacheDoorTraversalPreferences();

        if (!doorTraversalPreferencesInitialized)
            return;

        if (!force && !doorTraversalPreferenceDirty)
            return;

        int doorTagMask = 1 << closedDoorPathTag;
        bool allowClosedDoorTraversal = ShouldAllowClosedDoorTraversalForCurrentState();
        int desiredTraversableTags = allowClosedDoorTraversal
            ? defaultTraversableTags | doorTagMask
            : defaultTraversableTags & ~doorTagMask;

        seeker.traversableTags = desiredTraversableTags;

        if (seeker.tagPenalties == null || seeker.tagPenalties.Length != 32)
            seeker.tagPenalties = new int[32];

        for (int i = 0; i < seeker.tagPenalties.Length; i++)
        {
            int desiredPenalty = defaultTagPenalties[i];
            if (i == closedDoorPathTag && allowClosedDoorTraversal)
                desiredPenalty = ResolveClosedDoorTagPenaltyForCurrentState();

            if (seeker.tagPenalties[i] == desiredPenalty)
                continue;

            seeker.tagPenalties[i] = desiredPenalty;
        }

        doorTraversalPreferenceDirty = false;
    }

    /// <summary>
    /// Returns whether closed doors should be considered traversable in the current state.
    /// </summary>
    private bool ShouldAllowClosedDoorTraversalForCurrentState()
    {
        return currentState switch
        {
            EnemyState.Patrol => allowClosedDoorTraversalWhilePatrol,
            EnemyState.Alert => allowClosedDoorTraversalWhileAlert,
            EnemyState.Suspicious => allowClosedDoorTraversalWhileSuspicious,
            EnemyState.Searching => allowClosedDoorTraversalWhileSearching,
            EnemyState.Fleeing => allowClosedDoorTraversalWhileFleeing,
            EnemyState.Detected => allowClosedDoorTraversalWhileDetected,
            _ => false
        };
    }

    /// <summary>
    /// Resolves the closed-door path penalty for the current state.
    /// </summary>
    private int ResolveClosedDoorTagPenaltyForCurrentState()
    {
        return currentState == EnemyState.Patrol
            ? closedDoorPatrolTagPenalty
            : closedDoorTagPenalty;
    }

    /// <summary>
    /// Attempts to automatically open a door that blocks the current path.
    /// </summary>
    private bool TryAutoOpenDoorInPath()
    {
        if (!ShouldAllowClosedDoorTraversalForCurrentState() ||
            !hasDestination ||
            Time.time < nextDoorAutoOpenTime)
            return false;

        if (!TryResolveDoorAutoOpenDirection(out Vector2 direction))
            return false;

        int hitCount = Physics2D.CircleCast(
            CurrentPosition,
            doorAutoOpenRadius,
            direction,
            doorDetectionContactFilter,
            doorAutoOpenHits,
            doorAutoOpenRange);

        DoorInteractable nearestDoor = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = doorAutoOpenHits[i];
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null)
                continue;

            DoorInteractable candidateDoor = hitCollider.GetComponentInParent<DoorInteractable>();
            if (candidateDoor == null || !candidateDoor.CanBeAutoOpenedByEnemy(this))
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestDoor = candidateDoor;
        }

        if (nearestDoor == null || !nearestDoor.TryOpenForEnemy(this))
            return false;

        nextDoorAutoOpenTime = Time.time + doorAutoOpenCooldown;
        return true;
    }

    /// <summary>
    /// Resolves the direction used to probe for an automatically openable door.
    /// </summary>
    private bool TryResolveDoorAutoOpenDirection(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (aiPath != null)
        {
            Vector2 steeringDirection = (Vector2)aiPath.steeringTarget - CurrentPosition;
            if (steeringDirection.sqrMagnitude > MinimumDirectionSqr)
            {
                direction = steeringDirection.normalized;
                return true;
            }
        }

        if (hasDestination)
        {
            Vector2 toDestination = currentDestination - CurrentPosition;
            if (toDestination.sqrMagnitude > MinimumDirectionSqr)
            {
                direction = toDestination.normalized;
                return true;
            }
        }

        Vector2 movementVector = ResolveMovementVector();
        if (movementVector.sqrMagnitude <= MinimumDirectionSqr)
            return false;

        direction = movementVector.normalized;
        return true;
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
    /// Holds the enemy in a fully stopped state while mission startup is blocked.
    /// </summary>
    private void HoldForMissionStartup()
    {
        if (movementBody != null)
        {
            movementBody.linearVelocity = Vector2.zero;
            movementBody.angularVelocity = 0f;
        }

        if (aiPath == null)
            return;

        aiPath.canMove = false;
        aiPath.isStopped = true;
        aiPath.destination = transform.position;
    }

    /// <summary>
    /// Rebuilds navigation intent for the current state after a pathing change.
    /// </summary>
    private void RefreshRuntimeNavigationForCurrentState()
    {
        switch (currentState)
        {
            case EnemyState.Patrol:
                MoveToCurrentPatrolPoint();
                return;

            case EnemyState.Suspicious:
            case EnemyState.Searching:
            case EnemyState.ReturningToStart:
            case EnemyState.Fleeing:
                if (hasDestination)
                    SetDirectDestination(currentDestination, true);
                return;

            case EnemyState.Detected:
                if (ResolveDetectionBehavior() == EnemyDetectionBehavior.ChasePlayer && detectedTarget != null)
                    SetFollowTarget(detectedTarget, true);
                else if (hasDestination)
                    SetDirectDestination(currentDestination, true);
                return;

            case EnemyState.Alert:
                if (hasDetectedMovementOverride && hasDestination)
                {
                    SetDirectDestination(currentDestination, true);
                    return;
                }

                if (alertChaseTarget && detectedTarget != null)
                {
                    SetFollowTarget(detectedTarget, true);
                    return;
                }

                if (alertChaseTarget && HasActiveAlertStimulus())
                {
                    SetDirectDestination(lastKnownTargetPosition, true);
                    return;
                }

                if (hasDestination)
                    SetDirectDestination(currentDestination, true);
                return;

            default:
                if (hasDestination)
                    SetDirectDestination(currentDestination, true);
                return;
        }
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

        if (!useMovePosition && !useVelocityMovement)
            useMovePosition = true;

        if (useMovePosition && useVelocityMovement)
            useVelocityMovement = false;

        patrolPoints ??= new List<PatrolPoint>();
        itinerarySteps ??= new List<EnemyItineraryStep>();
    }

    /// <summary>
    /// Restricts the starting state to values supported by startup flow.
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
            _ => EnemyState.Idle
        };
    }

    /// <summary>
    /// Returns whether suspicious/searching behavior may begin in the current state.
    /// </summary>
    private bool CanEnterInvestigativeState()
    {
        return currentState != EnemyState.Fleeing &&
               currentState != EnemyState.Disabled &&
               currentState != EnemyState.Alert;
    }

    /// <summary>
    /// Returns whether flee settings should be shown in the inspector.
    /// </summary>
    private bool ShouldShowFleeSettings()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint;
    }

    /// <summary>
    /// Returns whether missing-flee fallback settings should be shown in the inspector.
    /// </summary>
    private bool ShouldShowMissingFleeFallback()
    {
        return detectionBehavior == EnemyDetectionBehavior.FleeToPoint;
    }

    /// <summary>
    /// Emits a warning the first time no low-level movement driver is available.
    /// </summary>
    private void WarnMissingMover()
    {
        if (warnedMissingMover || !debugMovement)
            return;

        warnedMissingMover = true;
        Debug.LogWarning($"{name} has no AIPath or Rigidbody2D movement driver. State changes will still work, but the enemy cannot move.", this);
    }

    /// <summary>
    /// Resolves the current itinerary step if one is active.
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
    /// Resolves the active patrol route for the current controller or itinerary step.
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
    /// Resolves the number of active patrol points.
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
    /// Resolves the patrol mode currently driving patrol logic.
    /// </summary>
    private EnemyPatrolMode GetActivePatrolMode()
    {
        if (TryGetCurrentItineraryStep(out EnemyItineraryStep step) && step.StepType == EnemyItineraryStepType.Patrol)
            return step.PatrolMode;

        return patrolMode;
    }

    /// <summary>
    /// Returns whether the current patrol itinerary step should complete on this arrival.
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
    /// Resolves the idle destination for an itinerary step.
    /// </summary>
    private Vector2 ResolveIdleStepPosition(EnemyItineraryStep step)
    {
        if (step != null && step.IdlePoint != null)
            return step.IdlePoint.position;

        return startingPosition;
    }

    /// <summary>
    /// Returns whether the supplied position is within the active stopping distance.
    /// </summary>
    private bool IsWithinStoppingDistance(Vector2 position)
    {
        Vector2 delta = position - CurrentPosition;
        float activeStoppingDistance = ResolveCurrentStoppingDistance();
        return delta.sqrMagnitude <= activeStoppingDistance * activeStoppingDistance;
    }

    /// <summary>
    /// Returns whether the enemy is already at its alert hold point.
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
    /// Caches the default facing angle used by alert hold behavior.
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
    /// Applies the cached default alert facing override.
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
    /// Applies the provided facing override settings when they resolve to a valid angle.
    /// </summary>
    private void ApplyFacingOverrideIfAvailable(EnemyFacingSettings facingSettings, Transform referenceTransform, float fallbackAngle)
    {
        if (facingSettings == null || !facingSettings.TryResolveAngle(referenceTransform, fallbackAngle, out float resolvedAngle))
            return;

        SetManualFacingOverride(resolvedAngle);
    }

    /// <summary>
    /// Converts a Z angle into a manual facing override direction.
    /// </summary>
    private void SetManualFacingOverride(float zAngle)
    {
        float radians = (zAngle - rotationAngleOffset) * Mathf.Deg2Rad;
        manualFacingDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        hasManualFacingOverride = manualFacingDirection.sqrMagnitude > MinimumDirectionSqr;
    }

    /// <summary>
    /// Resolves the current world-facing direction from the active transform rotation.
    /// </summary>
    private Vector2 ResolveCurrentFacingDirection()
    {
        float radians = (CurrentRotation - rotationAngleOffset) * Mathf.Deg2Rad;
        Vector2 facingDirection = new(Mathf.Cos(radians), Mathf.Sin(radians));
        return facingDirection.sqrMagnitude > MinimumDirectionSqr ? facingDirection.normalized : Vector2.down;
    }

    /// <summary>
    /// Clears the current manual facing override.
    /// </summary>
    private void ClearManualFacingOverride()
    {
        hasManualFacingOverride = false;
    }

    /// <summary>
    /// Updates the movement speed cap for the current frame.
    /// </summary>
    private void UpdateMovementSpeed(float deltaTime)
    {
        float desiredSpeed = ResolveDesiredSpeedForState();
        float changeRate = desiredSpeed > currentSpeedCap ? acceleration : deceleration;
        currentSpeedCap = Mathf.MoveTowards(currentSpeedCap, desiredSpeed, changeRate * deltaTime);
    }

    /// <summary>
    /// Applies the active low-level movement driver for the current frame.
    /// </summary>
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

    /// <summary>
    /// Applies custom rotation toward the currently desired facing direction.
    /// </summary>
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

    /// <summary>
    /// Returns whether an itinerary is currently configured.
    /// </summary>
    private bool ShouldUseItinerary => useItinerary && itinerarySteps != null && itinerarySteps.Count > 0;

    /// <summary>
    /// Returns the current movement position from Rigidbody2D when available.
    /// </summary>
    private Vector2 CurrentPosition => movementBody != null ? movementBody.position : (Vector2)transform.position;

    /// <summary>
    /// Returns the current rotation from Rigidbody2D when available.
    /// </summary>
    private float CurrentRotation => movementBody != null ? movementBody.rotation : transform.eulerAngles.z;

    /// <summary>
    /// Rotates a vector by the supplied angle in degrees.
    /// </summary>
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
