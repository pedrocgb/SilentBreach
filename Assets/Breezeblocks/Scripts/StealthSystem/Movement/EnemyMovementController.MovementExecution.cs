using UnityEngine;

public partial class EnemyMovementController
{
    /// <summary>
    /// Immediately clears destinations and stops active movement driver.
    /// </summary>
    private void StopMovementImmediately()
    {
        ClearAutoDoorTraversal();
        hasDestination = false;
        ClearAstarTargetBinding();
        ApplyImmediateStopToDrivers();
    }

    /// <summary>
    /// Refreshes runtime movement state derived from active movement driver.
    /// </summary>
    private void SyncRuntimeMovementState()
    {
        currentMovementSpeed = ResolveActualMovementSpeed();
        isMoving = currentMovementSpeed > minimumMoveSpeed;
        hasReachedDestination = EvaluateHasReachedDestination();
    }

    /// <summary>
    /// Resolves current movement speed from A* or Rigidbody2D.
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
    /// Returns whether active destination has been reached.
    /// </summary>
    private bool EvaluateHasReachedDestination()
    {
        if (!hasDestination)
            return false;

        if (activeAutoDoor != null)
            return false;

        float activeStoppingDistance = ResolveCurrentStoppingDistance();
        if (aiPath != null)
            return aiPath.reachedDestination || aiPath.remainingDistance <= activeStoppingDistance;

        Vector2 delta = currentDestination - CurrentPosition;
        return delta.sqrMagnitude <= activeStoppingDistance * activeStoppingDistance;
    }

    /// <summary>
    /// Resolves desired movement speed for current high-level state.
    /// </summary>
    private float ResolveDesiredSpeedForState()
    {
        float desiredSpeed = currentState switch
        {
            EnemyState.Patrol when patrolWaiting => 0f,
            EnemyState.Patrol => walkSpeed,
            EnemyState.Suspicious when doorBellReactionActive && hasDestination => ResolveSpeed(doorBellReactionSpeed),
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
    /// Resolves speed type into configured scalar value.
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
    /// Resolves stopping distance for active state.
    /// </summary>
    private float ResolveCurrentStoppingDistance()
    {
        return currentState == EnemyState.Fleeing ? Mathf.Max(stoppingDistance, fleeStoppingDistance) : stoppingDistance;
    }

    /// <summary>
    /// Resolves desired facing direction based on state, movement, and overrides.
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
    /// Returns whether enemy should actively face tracked target.
    /// </summary>
    private bool ShouldFaceTrackedTarget()
    {
        if (!faceTargetWhenDetected || detectedTarget == null)
            return false;

        return currentState == EnemyState.Detected ||
               currentState == EnemyState.Alert;
    }

    /// <summary>
    /// Resolves current movement vector from active movement driver.
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
    /// Resolves stable facing vector based on path steering, velocity, or destination.
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
    /// Picks next randomized look-around direction.
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
    /// Captures starting transform used for return behavior.
    /// </summary>
    private void CaptureStartingTransform()
    {
        startingPosition = CurrentPosition;
        startingRotation = CurrentRotation;
    }

    /// <summary>
    /// Emits warning first time no low-level movement driver is available.
    /// </summary>
    private void WarnMissingMover()
    {
        if (warnedMissingMover || !debugMovement)
            return;

        warnedMissingMover = true;
        Debug.LogWarning($"{name} has no AIPath or Rigidbody2D movement driver. State changes will still work, but enemy cannot move.", this);
    }

    /// <summary>
    /// Converts Z angle into manual facing override direction.
    /// </summary>
    private void SetManualFacingOverride(float zAngle)
    {
        float radians = (zAngle - rotationAngleOffset) * Mathf.Deg2Rad;
        manualFacingDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        hasManualFacingOverride = manualFacingDirection.sqrMagnitude > MinimumDirectionSqr;
    }

    /// <summary>
    /// Resolves current world-facing direction from active transform rotation.
    /// </summary>
    private Vector2 ResolveCurrentFacingDirection()
    {
        float radians = (CurrentRotation - rotationAngleOffset) * Mathf.Deg2Rad;
        Vector2 facingDirection = new(Mathf.Cos(radians), Mathf.Sin(radians));
        return facingDirection.sqrMagnitude > MinimumDirectionSqr ? facingDirection.normalized : Vector2.down;
    }

    /// <summary>
    /// Clears current manual facing override.
    /// </summary>
    private void ClearManualFacingOverride()
    {
        hasManualFacingOverride = false;
    }

    /// <summary>
    /// Updates movement speed cap for current frame.
    /// </summary>
    private void UpdateMovementSpeed(float deltaTime)
    {
        float desiredSpeed = ResolveDesiredSpeedForState();
        float changeRate = desiredSpeed > currentSpeedCap ? acceleration : deceleration;
        currentSpeedCap = Mathf.MoveTowards(currentSpeedCap, desiredSpeed, changeRate * deltaTime);
    }

    /// <summary>
    /// Applies active low-level movement driver for current frame.
    /// </summary>
    private void ApplyMovementDriver()
    {
        float desiredSpeed = ResolveDesiredSpeedForState();
        float appliedSpeed = desiredSpeed > 0f
            ? Mathf.Max(currentSpeedCap, minimumMoveSpeed)
            : 0f;

        if (ApplyAstarMovementDriver(appliedSpeed))
            return;

        ApplyRigidbodyMovementDriver(appliedSpeed);
    }

    /// <summary>
    /// Applies custom rotation toward currently desired facing direction.
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

        ApplyRotation(nextAngle);
    }

    /// <summary>
    /// Applies immediate stop flags to both A* and Rigidbody2D movement drivers.
    /// </summary>
    private void ApplyImmediateStopToDrivers()
    {
        if (aiPath != null)
            aiPath.isStopped = true;

        if (movementBody != null && useVelocityMovement)
            movementBody.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// Applies AIPath movement state and returns whether A* handled movement this frame.
    /// </summary>
    private bool ApplyAstarMovementDriver(float appliedSpeed)
    {
        if (aiPath == null)
            return false;

        aiPath.canMove = currentState != EnemyState.Disabled;
        aiPath.isStopped = appliedSpeed <= Mathf.Epsilon || !hasDestination;
        aiPath.maxSpeed = appliedSpeed;
        aiPath.maxAcceleration = AstarAccelerationOverride;
        aiPath.slowdownDistance = slowdownDistance;
        aiPath.endReachedDistance = ResolveCurrentStoppingDistance();
        return true;
    }

    /// <summary>
    /// Applies Rigidbody2D movement fallback when no A* mover is present.
    /// </summary>
    private void ApplyRigidbodyMovementDriver(float appliedSpeed)
    {
        if (movementBody == null)
        {
            WarnMissingMover();
            return;
        }

        if (!hasDestination || appliedSpeed <= Mathf.Epsilon)
        {
            StopRigidbodyVelocityIfNeeded();
            return;
        }

        Vector2 currentPosition = movementBody.position;
        Vector2 toDestination = currentDestination - currentPosition;
        float stoppingDistanceForState = ResolveCurrentStoppingDistance();
        if (toDestination.sqrMagnitude <= stoppingDistanceForState * stoppingDistanceForState)
        {
            StopRigidbodyVelocityIfNeeded();
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
    /// Stops Rigidbody2D velocity only when velocity-based movement is enabled.
    /// </summary>
    private void StopRigidbodyVelocityIfNeeded()
    {
        if (movementBody != null && useVelocityMovement)
            movementBody.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// Applies rotation through Rigidbody2D when available, otherwise through transform.
    /// </summary>
    private void ApplyRotation(float nextAngle)
    {
        if (movementBody != null)
            movementBody.MoveRotation(nextAngle);
        else
            transform.rotation = Quaternion.Euler(0f, 0f, nextAngle);
    }

    /// <summary>
    /// Returns whether itinerary is currently configured.
    /// </summary>
    private bool ShouldUseItinerary => useItinerary && itinerarySteps != null && itinerarySteps.Count > 0;

    /// <summary>
    /// Returns current movement position from Rigidbody2D when available.
    /// </summary>
    private Vector2 CurrentPosition => movementBody != null ? movementBody.position : (Vector2)transform.position;

    /// <summary>
    /// Returns current rotation from Rigidbody2D when available.
    /// </summary>
    private float CurrentRotation => movementBody != null ? movementBody.rotation : transform.eulerAngles.z;

    /// <summary>
    /// Rotates vector by supplied angle in degrees.
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
