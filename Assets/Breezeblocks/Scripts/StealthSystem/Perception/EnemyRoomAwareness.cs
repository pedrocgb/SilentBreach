using System.Collections;
using Breezeblocks.Missions;
using Sirenix.OdinInspector;
using UnityEngine;

internal enum EnemyRoomAwarenessReactionType
{
    None,
    Light,
    ConfusedLight,
    DoorState
}

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Room Awareness")]
public class EnemyRoomAwareness : MonoBehaviour
{
    private const float MinimumInterval = 0.02f;
    private const float MinimumDirectionSqr = 0.0001f;

    private EnemyMovementController enemyMovementController;
    private AIHearing aiHearing;
    private EnemyVisionAI enemyVisionAI;
    private EnemyConfusedReactionIndicator confusedReactionIndicator;

    [FoldoutGroup("Room Awareness")]
    [SerializeField] private bool roomAwareness = true;

    [FoldoutGroup("Room Awareness"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    [SerializeField] private float roomCheckInterval = 0.15f;

    [FoldoutGroup("Room Awareness"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float waitBeforeSwitchDuration = 1f;

    [FoldoutGroup("Room Awareness"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    [SerializeField] private float confusedReactionDuration = 1.2f;

    [FoldoutGroup("Room Awareness"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float lookAroundDurationAfterTurningLightsOn = 2.5f;

    [FoldoutGroup("Room Awareness"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    [SerializeField] private float lookAroundTurnInterval = 0.45f;

    [FoldoutGroup("Room Awareness"), MinValue(0f), SuffixLabel("deg/s", true)]
    [SerializeField] private float lookAroundRotationSpeed = 420f;

    [FoldoutGroup("Door State Awareness")]
    [SerializeField] private bool doorStateAwareness = true;

    [FoldoutGroup("Door State Awareness"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float waitBeforeDoorStateFixDuration = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool RoomAwareness => roomAwareness;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemyRoomZone CurrentRoom => currentRoom;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool CurrentRoomLightsOn => currentRoom == null || currentRoomLightsOn;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsReactingToDarkRoom => reactionRoutine != null && currentReactionType == EnemyRoomAwarenessReactionType.Light;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsReactingToConfusedLight => reactionRoutine != null && currentReactionType == EnemyRoomAwarenessReactionType.ConfusedLight;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsReactingToDoorState => reactionRoutine != null && currentReactionType == EnemyRoomAwarenessReactionType.DoorState;

    private EnemyRoomZone currentRoom;
    private Coroutine reactionRoutine;
    private float nextRoomCheckTime;
    private bool currentRoomLightsOn = true;
    private bool cancelReactionRequested;
    private bool cancelReactionKeepCurrentBehavior;
    private bool pendingConfusedLightReaction;
    private EnemyRoomZone reactingRoom;
    private EnemyRoomAwarenessReactionType currentReactionType;
    private DoorInteractable reactingDoor;
    private readonly System.Collections.Generic.List<DoorInteractable> connectedDoorsBuffer = new();
    private readonly System.Collections.Generic.List<DoorInteractable> visibleIncorrectDoorsBuffer = new();
    private readonly System.Collections.Generic.List<DoorInteractable> pendingDoorFixes = new();

    /// <summary>
    /// Ensures the provided actor root has a room awareness component.
    /// </summary>
    public static EnemyRoomAwareness EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        if (actorRoot.TryGetComponent(out EnemyRoomAwareness existing))
            return existing;

        return actorRoot.AddComponent<EnemyRoomAwareness>();
    }

    /// <summary>
    /// Caches same-object references when the component is reset in the editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches required same-object references before runtime logic starts.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Subscribes to movement, hearing, and room events when the component becomes active.
    /// </summary>
    private void OnEnable()
    {
        CacheReferences();

        if (enemyMovementController != null)
            enemyMovementController.StateChanged += HandleMovementStateChanged;

        if (aiHearing != null)
            aiHearing.NoiseReactionTriggered += HandleNoiseReactionTriggered;

        RefreshCurrentRoom(allowImmediateReaction: false);
    }

    /// <summary>
    /// Unsubscribes from external events and clears any active reaction state.
    /// </summary>
    private void OnDisable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged -= HandleMovementStateChanged;

        if (aiHearing != null)
            aiHearing.NoiseReactionTriggered -= HandleNoiseReactionTriggered;

        SubscribeToCurrentRoom(null);
        currentRoom = null;
        currentRoomLightsOn = true;
        pendingConfusedLightReaction = false;
        ForceEndReaction(resumeDefaultBehavior: false);
    }

    /// <summary>
    /// Clamps authoring values and refreshes cached references while editing.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
        roomCheckInterval = Mathf.Max(MinimumInterval, roomCheckInterval);
        waitBeforeSwitchDuration = Mathf.Max(0f, waitBeforeSwitchDuration);
        lookAroundDurationAfterTurningLightsOn = Mathf.Max(0f, lookAroundDurationAfterTurningLightsOn);
        lookAroundTurnInterval = Mathf.Max(MinimumInterval, lookAroundTurnInterval);
        lookAroundRotationSpeed = Mathf.Max(0f, lookAroundRotationSpeed);
        waitBeforeDoorStateFixDuration = Mathf.Max(0f, waitBeforeDoorStateFixDuration);
        confusedReactionDuration = Mathf.Max(MinimumInterval, confusedReactionDuration);
    }

    /// <summary>
    /// Periodically refreshes room context and starts the highest-priority room reaction when allowed.
    /// </summary>
    private void Update()
    {
        if (!CanEvaluateRoomAwarenessThisFrame())
            return;

        nextRoomCheckTime = Time.time + roomCheckInterval;
        RefreshCurrentRoom(allowImmediateReaction: true);
    }

    /// <summary>
    /// Cancels the active room reaction when movement enters a higher-priority state.
    /// </summary>
    private void HandleMovementStateChanged(EnemyState previousState, EnemyState newState)
    {
        if (reactionRoutine == null)
            return;

        if (IsHigherPriorityState(newState))
            RequestCancelReaction(keepCurrentBehavior: true);
    }

    /// <summary>
    /// Cancels active room reactions when hearing promotes a stronger stimulus.
    /// </summary>
    private void HandleNoiseReactionTriggered(NoiseEvent noiseEvent)
    {
        if (GameplayMissionController.EnemyRuntimeBlockedAtMissionStart || reactionRoutine == null)
            return;

        RequestCancelReaction(keepCurrentBehavior: true);
    }

    /// <summary>
    /// Tracks light state changes for the current room and cancels invalid reactions.
    /// </summary>
    private void HandleRoomLightStateChanged(EnemyRoomZone room, bool lightsOn)
    {
        if (room == null || room != currentRoom)
            return;

        currentRoomLightsOn = lightsOn;
        if (lightsOn)
            pendingConfusedLightReaction = false;

        if (!roomAwareness)
            return;

        if (!lightsOn)
        {
            if (enemyMovementController != null &&
                enemyMovementController.ConfusedByLightsOff &&
                !IsHigherPriorityState(enemyMovementController.CurrentState))
            {
                pendingConfusedLightReaction = true;
            }

            RequestCancelReaction(keepCurrentBehavior: false);
        }
    }

    /// <summary>
    /// Resolves the current room and optionally evaluates whether a reaction should begin immediately.
    /// </summary>
    private void RefreshCurrentRoom(bool allowImmediateReaction)
    {
        EnemyRoomZone nextRoom = EnemyRoomZone.FindContainingPoint(transform.position);
        if (nextRoom != currentRoom)
        {
            SubscribeToCurrentRoom(nextRoom);
            currentRoom = nextRoom;
            currentRoomLightsOn = currentRoom == null || currentRoom.AreLightsOn;
            pendingConfusedLightReaction = false;

            if (reactionRoutine != null && reactingRoom != null && reactingRoom != currentRoom)
                RequestCancelReaction(keepCurrentBehavior: false);
        }
        else if (currentRoom != null)
        {
            currentRoomLightsOn = currentRoom.AreLightsOn;
        }

        if (!allowImmediateReaction || currentRoom == null)
            return;

        TryStartHighestPriorityRoomReaction();
    }

    /// <summary>
    /// Updates the current room light-state subscription.
    /// </summary>
    private void SubscribeToCurrentRoom(EnemyRoomZone nextRoom)
    {
        if (currentRoom != null)
            currentRoom.LightStateChanged -= HandleRoomLightStateChanged;

        if (nextRoom != null)
            nextRoom.LightStateChanged += HandleRoomLightStateChanged;
    }

    /// <summary>
    /// Starts the dark-room correction reaction when the current context allows it.
    /// </summary>
    private void TryStartDarkRoomReaction(EnemyRoomZone room)
    {
        if (room == null || reactionRoutine != null || !CanStartRoomReaction(room))
            return;

        currentReactionType = EnemyRoomAwarenessReactionType.Light;
        reactionRoutine = StartCoroutine(DarkRoomReactionRoutine(room));
    }

    /// <summary>
    /// Starts the confused-by-darkness reaction when the enemy witnessed the lights turning off.
    /// </summary>
    private void TryStartConfusedLightReaction(EnemyRoomZone room)
    {
        if (room == null ||
            reactionRoutine != null ||
            !pendingConfusedLightReaction ||
            !CanStartRoomReaction(room))
        {
            return;
        }

        pendingConfusedLightReaction = false;
        currentReactionType = EnemyRoomAwarenessReactionType.ConfusedLight;
        reactionRoutine = StartCoroutine(ConfusedLightReactionRoutine(room));
    }

    /// <summary>
    /// Starts the highest-priority room-driven reaction for the current room.
    /// </summary>
    private void TryStartHighestPriorityRoomReaction()
    {
        if (!CanReactToRoomStimuli())
            return;

        if (!currentRoomLightsOn)
        {
            if (enemyMovementController != null && enemyMovementController.ConfusedByLightsOff)
                TryStartConfusedLightReaction(currentRoom);
            else
                TryStartDarkRoomReaction(currentRoom);

            return;
        }

        TryStartDoorStateReaction(currentRoom);
    }

    /// <summary>
    /// Returns whether the enemy may begin a dark-room reaction in the provided room.
    /// </summary>
    private bool CanStartRoomReaction(EnemyRoomZone room)
    {
        return CanReactToRoomStimuli() && room != null && !room.AreLightsOn;
    }

    /// <summary>
    /// Starts the door-state correction reaction when visible incorrect doors exist.
    /// </summary>
    private void TryStartDoorStateReaction(EnemyRoomZone room)
    {
        if (!doorStateAwareness ||
            room == null ||
            reactionRoutine != null ||
            !CanStartDoorStateReaction(room))
        {
            return;
        }

        pendingDoorFixes.Clear();
        AppendVisibleIncorrectDoors(room);
        if (pendingDoorFixes.Count <= 0)
            return;

        currentReactionType = EnemyRoomAwarenessReactionType.DoorState;
        reactionRoutine = StartCoroutine(DoorStateReactionRoutine(room));
    }

    /// <summary>
    /// Returns whether the enemy may begin a door-state correction reaction in the provided room.
    /// </summary>
    private bool CanStartDoorStateReaction(EnemyRoomZone room)
    {
        return doorStateAwareness &&
               enemyVisionAI != null &&
               CanReactToRoomStimuli() &&
               room != null &&
               room.AreLightsOn;
    }

    /// <summary>
    /// Drives the full dark-room correction sequence.
    /// </summary>
    private IEnumerator DarkRoomReactionRoutine(EnemyRoomZone room)
    {
        reactingRoom = room;
        reactingDoor = null;
        cancelReactionRequested = false;
        cancelReactionKeepCurrentBehavior = false;
        bool completedNormally = false;

        Vector2 switchPosition = room.LightSwitch != null ? room.SwitchPosition : (Vector2)transform.position;

        enemyMovementController.SetExternalInvestigation(transform.position, EnemyState.Suspicious);
        yield return WaitWhileSuspicious(room, waitBeforeSwitchDuration, switchPosition, cancelIfLightsTurnOn: true);
        if (cancelReactionRequested)
        {
            FinishReaction(completedNormally);
            yield break;
        }

        if (room.LightSwitch != null && !room.AreLightsOn)
        {
            enemyMovementController.SetExternalInvestigation(switchPosition, EnemyState.Suspicious);
            while (!cancelReactionRequested && !enemyMovementController.HasReachedDestination)
            {
                if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn: true, cancelIfLightsTurnOff: false))
                    break;

                yield return null;
            }

            if (!cancelReactionRequested && !room.AreLightsOn)
                room.TryTurnLightsOn(gameObject, playSfx: true);
        }

        if (cancelReactionRequested)
        {
            FinishReaction(completedNormally);
            yield break;
        }

        enemyMovementController.SetExternalInvestigation(transform.position, EnemyState.Suspicious);
        yield return LookAroundAfterTurningLightsOn(room);
        if (!cancelReactionRequested)
            completedNormally = true;

        FinishReaction(completedNormally);
    }

    /// <summary>
    /// Drives the temporary confusion reaction used by enemies that freeze when lights go out.
    /// </summary>
    private IEnumerator ConfusedLightReactionRoutine(EnemyRoomZone room)
    {
        reactingRoom = room;
        reactingDoor = null;
        cancelReactionRequested = false;
        cancelReactionKeepCurrentBehavior = false;
        bool completedNormally = false;

        enemyMovementController.SetExternalInvestigation(transform.position, EnemyState.Suspicious);
        confusedReactionIndicator?.Play(confusedReactionDuration);

        float endTime = Time.time + confusedReactionDuration;
        while (!cancelReactionRequested && Time.time < endTime)
        {
            if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn: true, cancelIfLightsTurnOff: false))
                break;

            yield return null;
        }

        if (!cancelReactionRequested && Time.time >= endTime)
            completedNormally = true;

        FinishReaction(completedNormally);
    }

    /// <summary>
    /// Drives the sequence that restores incorrect door states visible from the current room.
    /// </summary>
    private IEnumerator DoorStateReactionRoutine(EnemyRoomZone room)
    {
        reactingRoom = room;
        reactingDoor = null;
        cancelReactionRequested = false;
        cancelReactionKeepCurrentBehavior = false;
        bool completedNormally = false;

        while (!cancelReactionRequested)
        {
            AppendVisibleIncorrectDoors(room);
            if (!TryPopNearestPendingDoor(out DoorInteractable nextDoor))
            {
                completedNormally = true;
                break;
            }

            reactingDoor = nextDoor;

            if (!reactingDoor.IsInDefaultState)
            {
                enemyMovementController.SetExternalInvestigation(transform.position, EnemyState.Suspicious);
                yield return WaitWhileSuspicious(
                    room,
                    waitBeforeDoorStateFixDuration,
                    reactingDoor.AwarenessSamplePosition,
                    cancelIfLightsTurnOn: false,
                    cancelIfLightsTurnOff: true);
            }

            if (cancelReactionRequested)
                break;

            if (reactingDoor == null || !reactingDoor.isActiveAndEnabled || reactingDoor.IsInDefaultState)
            {
                reactingDoor = null;
                continue;
            }

            enemyMovementController.SetExternalInvestigation(reactingDoor.InteractionPosition, EnemyState.Suspicious);
            while (!cancelReactionRequested && !enemyMovementController.HasReachedDestination)
            {
                if (!CanContinueDoorReaction(room, reactingDoor))
                    break;

                yield return null;
            }

            if (cancelReactionRequested)
                break;

            if (reactingDoor != null &&
                reactingDoor.isActiveAndEnabled &&
                !reactingDoor.IsInDefaultState)
            {
                reactingDoor.TryRestoreDefaultState(gameObject);
            }

            reactingDoor = null;
        }

        FinishReaction(completedNormally);
    }

    /// <summary>
    /// Holds the enemy in suspicious facing behavior for the requested duration while checking cancellation rules.
    /// </summary>
    private IEnumerator WaitWhileSuspicious(
        EnemyRoomZone room,
        float duration,
        Vector2 facePoint,
        bool cancelIfLightsTurnOn,
        bool cancelIfLightsTurnOff = false)
    {
        float endTime = Time.time + Mathf.Max(0f, duration);
        while (!cancelReactionRequested && Time.time < endTime)
        {
            if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn, cancelIfLightsTurnOff))
                yield break;

            enemyMovementController.SetFacingPoint(facePoint);
            yield return null;
        }
    }

    /// <summary>
    /// Rotates the enemy through a temporary look-around after lights are restored.
    /// </summary>
    private IEnumerator LookAroundAfterTurningLightsOn(EnemyRoomZone room)
    {
        Vector2 baseDirection = room != null
            ? room.ResolveLookAroundBaseDirection(transform.position)
            : enemyMovementController.CurrentFacingDirection;
        if (baseDirection.sqrMagnitude <= MinimumDirectionSqr)
            baseDirection = transform.up;

        enemyMovementController.SetExternalTurnSpeedOverride(true, lookAroundRotationSpeed);

        float endTime = Time.time + Mathf.Max(0f, lookAroundDurationAfterTurningLightsOn);
        float nextTurnTime = Time.time;
        while (!cancelReactionRequested && Time.time < endTime)
        {
            if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn: false, cancelIfLightsTurnOff: true))
                yield break;

            if (Time.time >= nextTurnTime)
            {
                float minAngle = room != null ? room.LookAroundMinAngle : -70f;
                float maxAngle = room != null ? room.LookAroundMaxAngle : 70f;
                if (maxAngle < minAngle)
                    maxAngle = minAngle;

                float angle = Random.Range(minAngle, maxAngle);
                enemyMovementController.SetExternalFacingDirection(Rotate(baseDirection.normalized, angle));
                nextTurnTime = Time.time + lookAroundTurnInterval;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Collects visible doors in the room that are not in their authored default state.
    /// </summary>
    private void CollectVisibleIncorrectDoors(EnemyRoomZone room, System.Collections.Generic.List<DoorInteractable> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (room == null || enemyVisionAI == null)
            return;

        room.GetConnectedDoors(connectedDoorsBuffer);
        for (int i = 0; i < connectedDoorsBuffer.Count; i++)
        {
            DoorInteractable door = connectedDoorsBuffer[i];
            if (door == null ||
                !door.isActiveAndEnabled ||
                !door.HasDefaultStateTag ||
                door.IsInDefaultState)
            {
                continue;
            }

            if (!enemyVisionAI.CanPerceiveWorldPoint(door.AwarenessSamplePosition, door.GetCurrentVisibility()))
                continue;

            results.Add(door);
        }
    }

    /// <summary>
    /// Appends newly visible incorrect doors to the pending fix queue.
    /// </summary>
    private void AppendVisibleIncorrectDoors(EnemyRoomZone room)
    {
        CollectVisibleIncorrectDoors(room, visibleIncorrectDoorsBuffer);
        for (int i = 0; i < visibleIncorrectDoorsBuffer.Count; i++)
        {
            DoorInteractable door = visibleIncorrectDoorsBuffer[i];
            if (door == null || pendingDoorFixes.Contains(door) || door == reactingDoor)
                continue;

            pendingDoorFixes.Add(door);
        }
    }

    /// <summary>
    /// Pops the nearest currently valid pending door fix target.
    /// </summary>
    private bool TryPopNearestPendingDoor(out DoorInteractable closestDoor)
    {
        closestDoor = null;
        int closestIndex = -1;
        float closestDistanceSqr = float.PositiveInfinity;
        Vector2 currentPosition = transform.position;

        for (int i = pendingDoorFixes.Count - 1; i >= 0; i--)
        {
            DoorInteractable candidateDoor = pendingDoorFixes[i];
            if (candidateDoor == null || !candidateDoor.isActiveAndEnabled || candidateDoor.IsInDefaultState)
            {
                pendingDoorFixes.RemoveAt(i);
                continue;
            }

            float distanceSqr = ((Vector2)candidateDoor.InteractionPosition - currentPosition).sqrMagnitude;
            if (distanceSqr >= closestDistanceSqr)
                continue;

            closestDistanceSqr = distanceSqr;
            closestIndex = i;
            closestDoor = candidateDoor;
        }

        if (closestIndex < 0)
            return false;

        pendingDoorFixes.RemoveAt(closestIndex);
        return true;
    }

    /// <summary>
    /// Returns whether the current reaction may continue under the present room and state conditions.
    /// </summary>
    private bool CanContinueCurrentReaction(EnemyRoomZone room, bool cancelIfLightsTurnOn, bool cancelIfLightsTurnOff)
    {
        if (cancelReactionRequested)
            return false;

        if (!roomAwareness || room == null || enemyMovementController == null)
        {
            RequestCancelReaction(keepCurrentBehavior: false);
            return false;
        }

        if (IsHigherPriorityState(enemyMovementController.CurrentState))
        {
            RequestCancelReaction(keepCurrentBehavior: true);
            return false;
        }

        if (currentRoom != room)
        {
            RequestCancelReaction(keepCurrentBehavior: false);
            return false;
        }

        if (cancelIfLightsTurnOn && room.AreLightsOn)
        {
            RequestCancelReaction(keepCurrentBehavior: false);
            return false;
        }

        if (cancelIfLightsTurnOff && !room.AreLightsOn)
        {
            RequestCancelReaction(keepCurrentBehavior: false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns whether the current door-fix reaction may continue for the provided door.
    /// </summary>
    private bool CanContinueDoorReaction(EnemyRoomZone room, DoorInteractable door)
    {
        if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn: false, cancelIfLightsTurnOff: true))
            return false;

        return door != null && door.isActiveAndEnabled && !door.IsInDefaultState;
    }

    /// <summary>
    /// Returns whether room awareness should evaluate on this frame.
    /// </summary>
    private bool CanEvaluateRoomAwarenessThisFrame()
    {
        return !GameplayMissionController.EnemyRuntimeBlockedAtMissionStart &&
               Time.time >= nextRoomCheckTime;
    }

    /// <summary>
    /// Returns whether the enemy may currently react to room-level stimuli.
    /// </summary>
    private bool CanReactToRoomStimuli()
    {
        return reactionRoutine == null &&
               roomAwareness &&
               currentRoom != null &&
               enemyMovementController != null &&
               !IsHigherPriorityState(enemyMovementController.CurrentState);
    }

    /// <summary>
    /// Requests cancellation of the active reaction and records whether current behavior should be preserved.
    /// </summary>
    private void RequestCancelReaction(bool keepCurrentBehavior)
    {
        cancelReactionRequested = true;
        cancelReactionKeepCurrentBehavior |= keepCurrentBehavior;
    }

    /// <summary>
    /// Finishes the active reaction and restores the movement controller to its appropriate follow-up behavior.
    /// </summary>
    private void FinishReaction(bool completedNormally)
    {
        confusedReactionIndicator?.HideImmediate();
        enemyMovementController?.ClearExternalFacingOverride();
        enemyMovementController?.SetExternalTurnSpeedOverride(false, 0f);
        enemyMovementController?.ClearFacingOverride();

        bool resumeDefaultBehavior = completedNormally || !cancelReactionKeepCurrentBehavior;
        enemyMovementController?.ClearExternalInvestigation(resumeDefaultBehavior);

        reactionRoutine = null;
        reactingRoom = null;
        reactingDoor = null;
        currentReactionType = EnemyRoomAwarenessReactionType.None;
        pendingDoorFixes.Clear();
        connectedDoorsBuffer.Clear();
        visibleIncorrectDoorsBuffer.Clear();
        cancelReactionRequested = false;
        cancelReactionKeepCurrentBehavior = false;
    }

    /// <summary>
    /// Forcefully ends the current reaction without waiting for its coroutine to complete.
    /// </summary>
    private void ForceEndReaction(bool resumeDefaultBehavior)
    {
        bool hadActiveReaction = reactionRoutine != null || reactingRoom != null;
        if (reactionRoutine != null)
            StopCoroutine(reactionRoutine);

        if (hadActiveReaction)
        {
            confusedReactionIndicator?.HideImmediate();
            enemyMovementController?.ClearExternalFacingOverride();
            enemyMovementController?.SetExternalTurnSpeedOverride(false, 0f);
            enemyMovementController?.ClearFacingOverride();
            enemyMovementController?.ClearExternalInvestigation(resumeDefaultBehavior);
        }

        reactionRoutine = null;
        reactingRoom = null;
        reactingDoor = null;
        currentReactionType = EnemyRoomAwarenessReactionType.None;
        pendingDoorFixes.Clear();
        connectedDoorsBuffer.Clear();
        visibleIncorrectDoorsBuffer.Clear();
        cancelReactionRequested = false;
        cancelReactionKeepCurrentBehavior = false;
    }

    /// <summary>
    /// Returns whether the provided state outranks room-awareness reactions.
    /// </summary>
    private bool IsHigherPriorityState(EnemyState state)
    {
        return state == EnemyState.Detected ||
               state == EnemyState.Alert ||
               state == EnemyState.Fleeing ||
               state == EnemyState.Disabled;
    }

    /// <summary>
    /// Caches same-object references used by room-awareness reactions.
    /// </summary>
    private void CacheReferences()
    {
        enemyMovementController ??= GetComponent<EnemyMovementController>();
        aiHearing ??= GetComponent<AIHearing>();
        enemyVisionAI ??= GetComponent<EnemyVisionAI>();
        confusedReactionIndicator ??= GetComponent<EnemyConfusedReactionIndicator>();
    }

    /// <summary>
    /// Rotates a direction vector by the provided angle in degrees.
    /// </summary>
    private static Vector2 Rotate(Vector2 direction, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }
}
