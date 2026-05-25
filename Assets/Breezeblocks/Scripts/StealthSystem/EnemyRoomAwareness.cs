using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Room Awareness")]
public class EnemyRoomAwareness : MonoBehaviour
{
    private const float MinimumInterval = 0.02f;
    private const float MinimumDirectionSqr = 0.0001f;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private AIHearing aiHearing;

    [FoldoutGroup("Room Awareness")]
    [SerializeField] private bool roomAwareness = true;

    [FoldoutGroup("Room Awareness"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    [SerializeField] private float roomCheckInterval = 0.15f;

    [FoldoutGroup("Room Awareness"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float waitBeforeSwitchDuration = 1f;

    [FoldoutGroup("Room Awareness"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float lookAroundDurationAfterTurningLightsOn = 2.5f;

    [FoldoutGroup("Room Awareness"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    [SerializeField] private float lookAroundTurnInterval = 0.45f;

    [FoldoutGroup("Room Awareness"), MinValue(0f), SuffixLabel("deg/s", true)]
    [SerializeField] private float lookAroundRotationSpeed = 420f;

    [FoldoutGroup("Room Awareness"), SuffixLabel("deg", true)]
    [SerializeField] private float lookAroundMinAngle = -70f;

    [FoldoutGroup("Room Awareness"), SuffixLabel("deg", true)]
    [SerializeField] private float lookAroundMaxAngle = 70f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool RoomAwareness => roomAwareness;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemyRoomZone CurrentRoom => currentRoom;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool CurrentRoomLightsOn => currentRoom == null || currentRoomLightsOn;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsReactingToDarkRoom => reactionRoutine != null;

    private EnemyRoomZone currentRoom;
    private Coroutine reactionRoutine;
    private float nextRoomCheckTime;
    private bool currentRoomLightsOn = true;
    private bool cancelReactionRequested;
    private bool cancelReactionKeepCurrentBehavior;
    private EnemyRoomZone reactingRoom;

    public static EnemyRoomAwareness EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        if (actorRoot.TryGetComponent(out EnemyRoomAwareness existing))
            return existing;

        return actorRoot.AddComponent<EnemyRoomAwareness>();
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();

        if (enemyMovementController != null)
            enemyMovementController.StateChanged += HandleMovementStateChanged;

        if (aiHearing != null)
            aiHearing.NoiseReactionTriggered += HandleNoiseReactionTriggered;

        RefreshCurrentRoom(allowImmediateReaction: false);
    }

    private void OnDisable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged -= HandleMovementStateChanged;

        if (aiHearing != null)
            aiHearing.NoiseReactionTriggered -= HandleNoiseReactionTriggered;

        SubscribeToCurrentRoom(null);
        currentRoom = null;
        currentRoomLightsOn = true;
        ForceEndReaction(resumeDefaultBehavior: false);
    }

    private void OnValidate()
    {
        CacheReferences();
        roomCheckInterval = Mathf.Max(MinimumInterval, roomCheckInterval);
        waitBeforeSwitchDuration = Mathf.Max(0f, waitBeforeSwitchDuration);
        lookAroundDurationAfterTurningLightsOn = Mathf.Max(0f, lookAroundDurationAfterTurningLightsOn);
        lookAroundTurnInterval = Mathf.Max(MinimumInterval, lookAroundTurnInterval);
        lookAroundRotationSpeed = Mathf.Max(0f, lookAroundRotationSpeed);

        if (lookAroundMaxAngle < lookAroundMinAngle)
            lookAroundMaxAngle = lookAroundMinAngle;
    }

    private void Update()
    {
        if (Time.time < nextRoomCheckTime)
            return;

        nextRoomCheckTime = Time.time + roomCheckInterval;
        RefreshCurrentRoom(allowImmediateReaction: true);
    }

    private void HandleMovementStateChanged(EnemyState previousState, EnemyState newState)
    {
        if (reactionRoutine == null)
            return;

        if (IsHigherPriorityState(newState))
            RequestCancelReaction(keepCurrentBehavior: true);
    }

    private void HandleNoiseReactionTriggered(NoiseEvent noiseEvent)
    {
        if (reactionRoutine == null)
            return;

        RequestCancelReaction(keepCurrentBehavior: true);
    }

    private void HandleRoomLightStateChanged(EnemyRoomZone room, bool lightsOn)
    {
        if (room == null || room != currentRoom)
            return;

        currentRoomLightsOn = lightsOn;
        if (!roomAwareness)
            return;

        if (!lightsOn)
        {
            TryStartDarkRoomReaction(room);
        }
    }

    private void RefreshCurrentRoom(bool allowImmediateReaction)
    {
        EnemyRoomZone nextRoom = EnemyRoomZone.FindContainingPoint(transform.position);
        if (nextRoom != currentRoom)
        {
            SubscribeToCurrentRoom(nextRoom);
            currentRoom = nextRoom;
            currentRoomLightsOn = currentRoom == null || currentRoom.AreLightsOn;

            if (reactionRoutine != null && reactingRoom != null && reactingRoom != currentRoom)
                RequestCancelReaction(keepCurrentBehavior: false);
        }
        else if (currentRoom != null)
        {
            currentRoomLightsOn = currentRoom.AreLightsOn;
        }

        if (allowImmediateReaction && currentRoom != null && !currentRoomLightsOn)
            TryStartDarkRoomReaction(currentRoom);
    }

    private void SubscribeToCurrentRoom(EnemyRoomZone nextRoom)
    {
        if (currentRoom != null)
            currentRoom.LightStateChanged -= HandleRoomLightStateChanged;

        if (nextRoom != null)
            nextRoom.LightStateChanged += HandleRoomLightStateChanged;
    }

    private void TryStartDarkRoomReaction(EnemyRoomZone room)
    {
        if (room == null || reactionRoutine != null || !CanStartRoomReaction(room))
            return;

        reactionRoutine = StartCoroutine(DarkRoomReactionRoutine(room));
    }

    private bool CanStartRoomReaction(EnemyRoomZone room)
    {
        if (!roomAwareness || enemyMovementController == null || room == null || room.AreLightsOn)
            return false;

        return enemyMovementController.CurrentState switch
        {
            EnemyState.Idle => true,
            EnemyState.Patrol => true,
            EnemyState.ReturningToStart => true,
            _ => false
        };
    }

    private IEnumerator DarkRoomReactionRoutine(EnemyRoomZone room)
    {
        reactingRoom = room;
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
                if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn: true))
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

    private IEnumerator WaitWhileSuspicious(EnemyRoomZone room, float duration, Vector2 facePoint, bool cancelIfLightsTurnOn)
    {
        float endTime = Time.time + Mathf.Max(0f, duration);
        while (!cancelReactionRequested && Time.time < endTime)
        {
            if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn))
                yield break;

            enemyMovementController.SetFacingPoint(facePoint);
            yield return null;
        }
    }

    private IEnumerator LookAroundAfterTurningLightsOn(EnemyRoomZone room)
    {
        Vector2 baseDirection = enemyMovementController.CurrentFacingDirection;
        if (baseDirection.sqrMagnitude <= MinimumDirectionSqr)
            baseDirection = transform.up;

        enemyMovementController.SetExternalTurnSpeedOverride(true, lookAroundRotationSpeed);

        float endTime = Time.time + Mathf.Max(0f, lookAroundDurationAfterTurningLightsOn);
        float nextTurnTime = Time.time;
        while (!cancelReactionRequested && Time.time < endTime)
        {
            if (!CanContinueCurrentReaction(room, cancelIfLightsTurnOn: false))
                yield break;

            if (Time.time >= nextTurnTime)
            {
                float angle = Random.Range(lookAroundMinAngle, lookAroundMaxAngle);
                enemyMovementController.SetExternalFacingDirection(Rotate(baseDirection.normalized, angle));
                nextTurnTime = Time.time + lookAroundTurnInterval;
            }

            yield return null;
        }
    }

    private bool CanContinueCurrentReaction(EnemyRoomZone room, bool cancelIfLightsTurnOn)
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

        return true;
    }

    private void RequestCancelReaction(bool keepCurrentBehavior)
    {
        cancelReactionRequested = true;
        cancelReactionKeepCurrentBehavior |= keepCurrentBehavior;
    }

    private void FinishReaction(bool completedNormally)
    {
        enemyMovementController?.ClearExternalFacingOverride();
        enemyMovementController?.SetExternalTurnSpeedOverride(false, 0f);
        enemyMovementController?.ClearFacingOverride();

        bool resumeDefaultBehavior = completedNormally || !cancelReactionKeepCurrentBehavior;
        enemyMovementController?.ClearExternalInvestigation(resumeDefaultBehavior);

        reactionRoutine = null;
        reactingRoom = null;
        cancelReactionRequested = false;
        cancelReactionKeepCurrentBehavior = false;
    }

    private void ForceEndReaction(bool resumeDefaultBehavior)
    {
        bool hadActiveReaction = reactionRoutine != null || reactingRoom != null;
        if (reactionRoutine != null)
            StopCoroutine(reactionRoutine);

        if (hadActiveReaction)
        {
            enemyMovementController?.ClearExternalFacingOverride();
            enemyMovementController?.SetExternalTurnSpeedOverride(false, 0f);
            enemyMovementController?.ClearFacingOverride();
            enemyMovementController?.ClearExternalInvestigation(resumeDefaultBehavior);
        }

        reactionRoutine = null;
        reactingRoom = null;
        cancelReactionRequested = false;
        cancelReactionKeepCurrentBehavior = false;
    }

    private bool IsHigherPriorityState(EnemyState state)
    {
        return state == EnemyState.Detected ||
               state == EnemyState.Alert ||
               state == EnemyState.Fleeing ||
               state == EnemyState.Disabled;
    }

    private void CacheReferences()
    {
        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (aiHearing == null)
            aiHearing = GetComponent<AIHearing>();
    }

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
