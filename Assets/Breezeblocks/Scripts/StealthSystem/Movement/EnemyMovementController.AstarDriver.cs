using System;
using Breezeblocks.Missions;
using Pathfinding;
using UnityEngine;

public partial class EnemyMovementController
{
    /// <summary>
    /// Refreshes cached A* configuration, door traversal preferences, and door detection filters.
    /// </summary>
    private void RefreshAstarDriverConfiguration()
    {
        ConfigureAstarDriver();
        CacheDoorTraversalPreferences();
        ApplyDoorTraversalPreferencesIfNeeded(force: true);
        RefreshDoorDetectionContactFilter();
    }

    /// <summary>
    /// Refreshes low-level runtime navigation after pathing or mission-start state changes.
    /// </summary>
    private void RefreshRuntimeNavigationState()
    {
        ApplyDoorTraversalPreferencesIfNeeded(force: true);
        RefreshRuntimeNavigationForCurrentState();
        SyncAstarTargets();
    }

    /// <summary>
    /// Synchronizes active high-level target intent into the A* destination driver.
    /// </summary>
    private void SyncAstarTargets()
    {
        if (aiPath == null)
            return;

        if (UsesCombatMovementOverridesForState(currentState) && hasDetectedMovementOverride)
        {
            ClearAstarTargetBinding();

            if (hasDestination)
                AssignAstarDestination(currentDestination);

            return;
        }

        EnemyDetectionBehavior behavior = ResolveDetectionBehavior();
        if (currentState == EnemyState.Detected &&
            behavior == EnemyDetectionBehavior.ChasePlayer &&
            detectedTarget != null)
        {
            AssignAstarFollowTarget(detectedTarget);
            return;
        }

        if (currentState == EnemyState.Alert &&
            alertChaseTarget &&
            detectedTarget != null)
        {
            AssignAstarFollowTarget(detectedTarget);
            return;
        }

        ClearAstarTargetBinding();

        if (hasDestination)
            AssignAstarDestination(currentDestination);
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

        AssignAstarFollowTarget(target);
        RequestAstarPathSearchIfNeeded(forceSearchPath);
    }

    /// <summary>
    /// Switches navigation into direct-destination mode.
    /// </summary>
    private void SetDirectDestination(Vector2 destination, bool forceSearchPath)
    {
        ClearManualFacingOverride();
        currentDestination = destination;
        hasDestination = true;

        ClearAstarTargetBinding();
        AssignAstarDestination(destination);
        RequestAstarPathSearchIfNeeded(forceSearchPath);
    }

    /// <summary>
    /// Updates direct destination only when it changed enough to matter.
    /// </summary>
    private void SetDestinationIfChanged(Vector2 destination, bool forceSearchPath)
    {
        if (hasDestination && (currentDestination - destination).sqrMagnitude <= DestinationRefreshSqrDistance)
            return;

        SetDirectDestination(destination, forceSearchPath);
    }

    /// <summary>
    /// Binds the A* destination setter to follow a target transform when available.
    /// </summary>
    private void AssignAstarFollowTarget(Transform target)
    {
        if (aiDestinationSetter != null)
            aiDestinationSetter.target = target;

        if (aiPath == null || target == null)
            return;

        ApplyDoorTraversalPreferencesIfNeeded();
        aiPath.destination = target.position;
    }

    /// <summary>
    /// Assigns a direct destination to the low-level A* mover.
    /// </summary>
    private void AssignAstarDestination(Vector2 destination)
    {
        if (aiPath == null)
            return;

        ApplyDoorTraversalPreferencesIfNeeded();
        aiPath.destination = destination;
    }

    /// <summary>
    /// Clears any A* follow-target binding so direct destinations can drive movement.
    /// </summary>
    private void ClearAstarTargetBinding()
    {
        if (aiDestinationSetter != null)
            aiDestinationSetter.target = null;
    }

    /// <summary>
    /// Issues a forced A* path search only when runtime state allows it.
    /// </summary>
    private void RequestAstarPathSearchIfNeeded(bool forceSearchPath)
    {
        if (!forceSearchPath || !CanIssueAstarSearchRequest())
            return;

        aiPath.SearchPath();
    }

    /// <summary>
    /// Configures AIPath to act as low-level mover while this controller owns high-level state.
    /// </summary>
    private void ConfigureAstarDriver()
    {
        if (aiPath == null)
            return;

        if (movementBody == null && debugMovement && !warnedAstarWithoutRigidbody)
        {
            warnedAstarWithoutRigidbody = true;
            Debug.LogWarning(
                $"{name} has AIPath but no Rigidbody2D. A* will still move enemy, but it will not use Rigidbody2D-based top-down movement until you add one.",
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
    /// Rebuilds contact filter used by automatic door opening.
    /// </summary>
    private void RefreshDoorDetectionContactFilter()
    {
        doorDetectionContactFilter = default;
        doorDetectionContactFilter.useLayerMask = true;
        doorDetectionContactFilter.layerMask = doorDetectionMask;
        doorDetectionContactFilter.useTriggers = true;
    }

    /// <summary>
    /// Caches seeker's default traversable tags and penalties.
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
    /// Applies door traversal tags and penalties that match current state.
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
    /// Returns whether closed doors should be considered traversable in current state.
    /// </summary>
    private bool ShouldAllowClosedDoorTraversalForCurrentState()
    {
        return currentState switch
        {
            EnemyState.Patrol => allowClosedDoorTraversalWhilePatrol,
            EnemyState.Alert => allowClosedDoorTraversalWhileAlert,
            EnemyState.Suspicious => allowClosedDoorTraversalWhileSuspicious,
            EnemyState.Searching => allowClosedDoorTraversalWhileSearching,
            EnemyState.ReturningToStart => allowClosedDoorTraversalWhileReturningToStart,
            EnemyState.Fleeing => allowClosedDoorTraversalWhileFleeing,
            EnemyState.Detected => allowClosedDoorTraversalWhileDetected,
            _ => false
        };
    }

    /// <summary>
    /// Resolves closed-door path penalty for current state.
    /// </summary>
    private int ResolveClosedDoorTagPenaltyForCurrentState()
    {
        return currentState == EnemyState.Patrol
            ? closedDoorPatrolTagPenalty
            : closedDoorTagPenalty;
    }

    /// <summary>
    /// Attempts to automatically open door that blocks current path.
    /// </summary>
    private bool TryAutoOpenDoorInPath()
    {
        UpdatePendingAutoDoorClosures();

        if (!ShouldAllowClosedDoorTraversalForCurrentState() ||
            !hasDestination ||
            Time.time < nextDoorAutoOpenTime)
            return false;

        if (!TryResolveDoorAutoOpenTarget(out Vector2 targetPosition))
            return false;

        Vector2 direction = (targetPosition - CurrentPosition).normalized;
        DoorInteractable nearestDoor = FindNearestPreferredRouteDoor(targetPosition, direction);
        if (nearestDoor != null && !IsDoorWithinAutoOpenRange(nearestDoor))
            nearestDoor = null;

        nearestDoor ??= FindNearestAutoOpenDoorByCircleCast(direction);
        nearestDoor ??= FindNearestAutoOpenDoorByOverlap(CurrentPosition + (direction * doorAutoOpenRange * 0.5f), direction);
        nearestDoor ??= TryResolveSteeringTargetOverlapCenter(out Vector2 steeringOverlapCenter)
            ? FindNearestAutoOpenDoorByOverlap(steeringOverlapCenter, direction)
            : null;

        if (nearestDoor == null || !IsDoorWithinAutoOpenRange(nearestDoor) || !TryOpenDoorForTraversal(nearestDoor, direction))
            return false;

        nextDoorAutoOpenTime = Time.time + doorAutoOpenCooldown;
        RequestAstarPathSearchIfNeeded(forceSearchPath: true);
        return true;
    }

    /// <summary>
    /// Returns whether the door is close enough to be operated by this enemy's auto-open range.
    /// </summary>
    private bool IsDoorWithinAutoOpenRange(DoorInteractable door)
    {
        if (door == null)
            return false;

        float allowedDistance = doorAutoOpenRange + doorAutoOpenRadius;
        Vector2 toDoor = (Vector2)door.InteractionPosition - CurrentPosition;
        return toDoor.sqrMagnitude <= allowedDistance * allowedDistance;
    }

    /// <summary>
    /// Opens a path-blocking door for this enemy and records whether it should be closed or relocked after traversal.
    /// </summary>
    private bool TryOpenDoorForTraversal(DoorInteractable door, Vector2 passDirection)
    {
        if (door == null)
            return false;

        DoorLockState lockState = door.LockState;
        bool unlockedIgnoredLock = lockState != null &&
                                   lockState.IsLocked &&
                                   lockState.CanEnemyIgnoreLockedState(this);

        if (unlockedIgnoredLock)
            lockState.SetLocked(false);

        if (!door.TryOpenForEnemy(this))
        {
            if (unlockedIgnoredLock)
                lockState.SetLocked(true);

            return false;
        }

        RegisterAutoOpenedDoorTraversal(door, lockState, unlockedIgnoredLock, passDirection);
        return true;
    }

    /// <summary>
    /// Adds or refreshes runtime tracking for a door this enemy opened through pathfinding.
    /// </summary>
    private void RegisterAutoOpenedDoorTraversal(DoorInteractable door, DoorLockState lockState, bool unlockedIgnoredLock, Vector2 passDirection)
    {
        if (!closeDoorsAfterPassing || door == null)
            return;

        Vector2 normalizedDirection = passDirection.sqrMagnitude > MinimumDirectionSqr ? passDirection.normalized : Vector2.up;
        for (int i = 0; i < pendingAutoDoorTraversals.Count; i++)
        {
            AutoDoorTraversalRecord existingRecord = pendingAutoDoorTraversals[i];
            if (existingRecord == null || existingRecord.Door != door)
                continue;

            existingRecord.LockState = lockState;
            existingRecord.ShouldRelock = unlockedIgnoredLock && relockIgnoredLockedDoorsAfterPassing;
            existingRecord.CloseRequested = false;
            existingRecord.DoorPosition = door.InteractionPosition;
            existingRecord.PassDirection = normalizedDirection;
            existingRecord.InitialSideSign = ResolveAutoDoorSideSign(existingRecord.DoorPosition, normalizedDirection);
            existingRecord.HasCrossedDoorPlane = false;
            existingRecord.CloseAllowedTime = Time.time + doorCloseAfterOpenDelay;
            return;
        }

        pendingAutoDoorTraversals.Add(new AutoDoorTraversalRecord
        {
            Door = door,
            LockState = lockState,
            ShouldRelock = unlockedIgnoredLock && relockIgnoredLockedDoorsAfterPassing,
            DoorPosition = door.InteractionPosition,
            PassDirection = normalizedDirection,
            InitialSideSign = ResolveAutoDoorSideSign(door.InteractionPosition, normalizedDirection),
            CloseAllowedTime = Time.time + doorCloseAfterOpenDelay
        });
    }

    /// <summary>
    /// Closes doors this enemy opened once it has moved safely away from their doorway on either side.
    /// </summary>
    private void UpdatePendingAutoDoorClosures()
    {
        for (int i = pendingAutoDoorTraversals.Count - 1; i >= 0; i--)
        {
            AutoDoorTraversalRecord record = pendingAutoDoorTraversals[i];
            if (record == null || record.Door == null)
            {
                pendingAutoDoorTraversals.RemoveAt(i);
                continue;
            }

            if (!record.Door.IsOpen && !record.Door.IsTransitioning)
            {
                CompleteAutoDoorTraversalRecord(record);
                pendingAutoDoorTraversals.RemoveAt(i);
                continue;
            }

            if (record.CloseRequested || Time.time < record.CloseAllowedTime || !HasPassedAutoOpenedDoor(record))
                continue;

            if (record.Door.TryCloseForEnemy(this))
                record.CloseRequested = true;
        }
    }

    /// <summary>
    /// Returns whether the enemy is far enough from a tracked door to close it without blocking itself.
    /// </summary>
    private bool HasPassedAutoOpenedDoor(AutoDoorTraversalRecord record)
    {
        Vector2 toEnemy = CurrentPosition - record.DoorPosition;
        float closeDistanceSqr = doorCloseAfterPassDistance * doorCloseAfterPassDistance;
        if (toEnemy.sqrMagnitude < closeDistanceSqr)
            return false;

        if (record.PassDirection.sqrMagnitude <= MinimumDirectionSqr)
            return true;

        float doorPlaneOffset = Vector2.Dot(toEnemy, record.PassDirection.normalized);
        int currentSideSign = ResolveSideSign(doorPlaneOffset);
        if (!record.HasCrossedDoorPlane)
        {
            if (record.InitialSideSign != 0 && (currentSideSign == 0 || currentSideSign == record.InitialSideSign))
                return false;

            record.HasCrossedDoorPlane = true;
        }

        return Mathf.Abs(doorPlaneOffset) >= doorCloseAfterPassDistance;
    }

    /// <summary>
    /// Resolves which side of an auto-opened door the enemy was on when the door tracking began.
    /// </summary>
    private int ResolveAutoDoorSideSign(Vector2 doorPosition, Vector2 passDirection)
    {
        if (passDirection.sqrMagnitude <= MinimumDirectionSqr)
            return 0;

        float doorPlaneOffset = Vector2.Dot(CurrentPosition - doorPosition, passDirection.normalized);
        return ResolveSideSign(doorPlaneOffset);
    }

    /// <summary>
    /// Converts a signed door-plane offset into a stable side marker with a small dead zone around the doorway.
    /// </summary>
    private static int ResolveSideSign(float value)
    {
        if (value > MinimumDistance)
            return 1;

        return value < -MinimumDistance ? -1 : 0;
    }

    /// <summary>
    /// Applies any final relock requested for a door after it has finished closing.
    /// </summary>
    private void CompleteAutoDoorTraversalRecord(AutoDoorTraversalRecord record)
    {
        if (record == null || !record.ShouldRelock || record.LockState == null)
            return;

        record.LockState.SetLocked(true);
    }

    /// <summary>
    /// Clears tracked auto-opened doors and safely relocks any already-closed door that still needs it.
    /// </summary>
    private void ClearPendingAutoDoorClosure()
    {
        for (int i = 0; i < pendingAutoDoorTraversals.Count; i++)
        {
            AutoDoorTraversalRecord record = pendingAutoDoorTraversals[i];
            if (record == null || record.Door == null || record.Door.IsOpen || record.Door.IsTransitioning)
                continue;

            CompleteAutoDoorTraversalRecord(record);
        }

        pendingAutoDoorTraversals.Clear();
    }

    /// <summary>
    /// Finds the nearest closed door that lies close to the preferred direct route toward the active target.
    /// </summary>
    private DoorInteractable FindNearestPreferredRouteDoor(Vector2 targetPosition, Vector2 direction)
    {
        var activeDoors = DoorInteractable.ActiveDoors;
        if (activeDoors == null || activeDoors.Count == 0)
            return null;

        float directDistanceToTarget = Vector2.Distance(CurrentPosition, targetPosition);
        float maxForwardDistance = Mathf.Min(directDistanceToTarget, doorPreferredRouteProbeDistance);
        if (maxForwardDistance <= MinimumDirectionSqr)
            return null;

        float routeProbeWidthSqr = doorPreferredRouteProbeWidth * doorPreferredRouteProbeWidth;
        DoorInteractable nearestDoor = null;
        float bestRouteDistance = float.PositiveInfinity;

        for (int i = 0; i < activeDoors.Count; i++)
        {
            DoorInteractable candidateDoor = activeDoors[i];
            if (candidateDoor == null || !candidateDoor.CanBeAutoOpenedByEnemy(this))
                continue;

            Vector2 doorPosition = candidateDoor.InteractionPosition;
            Vector2 toDoor = doorPosition - CurrentPosition;
            float forwardDistance = Vector2.Dot(direction, toDoor);
            if (forwardDistance <= 0f || forwardDistance > maxForwardDistance)
                continue;

            Vector2 closestPointOnRoute = CurrentPosition + (direction * forwardDistance);
            if ((doorPosition - closestPointOnRoute).sqrMagnitude > routeProbeWidthSqr)
                continue;

            float routeDistance = forwardDistance + Vector2.Distance(doorPosition, targetPosition);
            if (routeDistance >= bestRouteDistance)
                continue;

            bestRouteDistance = routeDistance;
            nearestDoor = candidateDoor;
        }

        return nearestDoor;
    }

    /// <summary>
    /// Finds the nearest automatically openable door through a forward circle-cast probe.
    /// </summary>
    private DoorInteractable FindNearestAutoOpenDoorByCircleCast(Vector2 direction)
    {
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
            DoorInteractable candidateDoor = ResolveCandidateDoor(hit.collider);
            if (candidateDoor == null || hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestDoor = candidateDoor;
        }

        return nearestDoor;
    }

    /// <summary>
    /// Finds the nearest automatically openable door through a local overlap probe around the path corridor.
    /// </summary>
    private DoorInteractable FindNearestAutoOpenDoorByOverlap(Vector2 center, Vector2 direction)
    {
        int hitCount = Physics2D.OverlapCircle(center, doorAutoOpenRadius, doorDetectionContactFilter, doorAutoOpenOverlapHits);
        DoorInteractable nearestDoor = null;
        float nearestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            DoorInteractable candidateDoor = ResolveCandidateDoor(doorAutoOpenOverlapHits[i]);
            if (candidateDoor == null)
                continue;

            Vector2 toDoor = (Vector2)candidateDoor.InteractionPosition - CurrentPosition;
            if (toDoor.sqrMagnitude <= MinimumDirectionSqr)
                continue;

            if (Vector2.Dot(direction, toDoor.normalized) <= 0f)
                continue;

            float distanceSqr = toDoor.sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr)
                continue;

            nearestDistanceSqr = distanceSqr;
            nearestDoor = candidateDoor;
        }

        return nearestDoor;
    }

    /// <summary>
    /// Resolves the overlap center near the current steering target when A* already has one.
    /// </summary>
    private bool TryResolveSteeringTargetOverlapCenter(out Vector2 center)
    {
        center = Vector2.zero;

        if (aiPath == null)
            return false;

        Vector2 steeringTarget = aiPath.steeringTarget;
        if ((steeringTarget - CurrentPosition).sqrMagnitude <= MinimumDirectionSqr)
            return false;

        center = steeringTarget;
        return true;
    }

    /// <summary>
    /// Resolves a valid door candidate from a detected collider.
    /// </summary>
    private DoorInteractable ResolveCandidateDoor(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return null;

        DoorInteractable candidateDoor = hitCollider.GetComponentInParent<DoorInteractable>();
        return candidateDoor != null && candidateDoor.CanBeAutoOpenedByEnemy(this)
            ? candidateDoor
            : null;
    }

    /// <summary>
    /// Resolves the most relevant target point used to evaluate door opening opportunities.
    /// </summary>
    private bool TryResolveDoorAutoOpenTarget(out Vector2 targetPosition)
    {
        targetPosition = Vector2.zero;

        if (hasDestination)
        {
            Vector2 toDestination = currentDestination - CurrentPosition;
            if (toDestination.sqrMagnitude > MinimumDirectionSqr)
            {
                targetPosition = currentDestination;
                return true;
            }
        }

        if (aiPath == null)
            return false;

        Vector2 steeringTarget = aiPath.steeringTarget;
        if ((steeringTarget - CurrentPosition).sqrMagnitude <= MinimumDirectionSqr)
            return false;

        targetPosition = steeringTarget;
        return true;
    }

    /// <summary>
    /// Holds enemy in fully stopped state while mission startup is blocked.
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

        ClearAstarTargetBinding();
        aiPath.canMove = false;
        aiPath.isStopped = true;
        aiPath.destination = transform.position;
    }

    /// <summary>
    /// Rebuilds navigation intent for current state after pathing change.
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
}
