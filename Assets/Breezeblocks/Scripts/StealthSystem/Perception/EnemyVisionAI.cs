using System.Collections.Generic;
using Breezeblocks.Missions;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Vision AI")]
public class EnemyVisionAI : MonoBehaviour
{
    private const float MinimumVisionRange = 0.01f;
    private const float MinimumVisionCheckInterval = 0.02f;
    private const float MissingTargetResolveInterval = 1f;
    private const float MinimumDirectionSqr = 0.0001f;

    [FoldoutGroup("References")]
    [Tooltip("Optional origin point for the vision cone. If empty, this transform position is used.")]
    [SerializeField] private Transform visionOrigin;

    [FoldoutGroup("References")]
    [Tooltip("Target transform, usually the player.")]
    [SerializeField] private Transform targetTransform;

    [FoldoutGroup("References")]
    [Tooltip("Optional PlayerVisibility component on the target. If missing, the target is treated as fully visible.")]
    [SerializeField] private PlayerVisibility targetVisibility;

    [FoldoutGroup("References")]
    [Tooltip("Optional PlayerUtilityController component on the target. Used for flashlight suspicion checks.")]
    [SerializeField] private PlayerUtilityController targetUtilityController;

    private EnemyMovementController enemyMovementController;
    private EnemyCombatantAI enemyCombatantAI;

    private float visionRange = 8f;
    private float visionAngle = 90f;
    private bool useTransformUpAsForward = true;
    private Vector2 localForwardDirection = Vector2.up;
    private float forwardAngleOffset;
    private float visionCheckInterval = 0.1f;
    private bool requireLineOfSight = true;
    private LayerMask obstacleMask;
    private float visibilityThreshold = 0.35f;
    private float detectionSpeed = 1.25f;
    private float detectionDecaySpeed = 0.75f;
    private float fullDetectionRadius;
    private float fullDetectionSpeedMultiplier = 5f;
    private bool reactToFlashlight = true;
    private bool reactToBodies = true;
    private float flashlightSourceLostDuration = 2f;
    private float flashlightSourceUpdateDistance = 0.75f;
    private int flashlightVisibilitySampleCount = 5;
    private float flashlightVisibilitySurfaceOffset = 0.05f;
    private bool useDistanceDetectionMultiplier = true;
    private float closeRangeDistance = 1.5f;
    private float noBonusDistance = 6f;
    private float closeRangeDetectionMultiplier = 4f;
    private bool debugLogging;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemyState CurrentState => enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Disabled;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentDetectionValue => currentDetectionValue;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentTargetVisibility => currentTargetVisibility;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float VisibilityThreshold => visibilityThreshold;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastKnownTargetPosition => lastKnownTargetPosition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool TargetInRange => targetInRange;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool TargetInsideVisionCone => targetInsideVisionCone;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasLineOfSight => hasLineOfSight;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool MeetsVisibilityThreshold => meetsVisibilityThreshold;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool CanCurrentlyDetectTarget => canCurrentlyDetectTarget;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool TargetInsideFullDetectionRadius => targetInsideFullDetectionRadius;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool UsingFullDetectionRadius => usingFullDetectionRadius;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool CanCurrentlySeeFlashlight => canCurrentlySeeFlashlight;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasActiveFlashlightStimulus => hasActiveFlashlightStimulus;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool CanCurrentlySeeBody => canCurrentlySeeBody;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastSeenBodyPosition => lastSeenBodyPosition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastKnownFlashlightSourcePosition => lastKnownFlashlightSourcePosition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentTargetDistance => currentTargetDistance;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentDistanceDetectionMultiplier => currentDistanceDetectionMultiplier;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float FlashlightStimulusTimeRemaining => Mathf.Max(0f, flashlightStimulusHoldUntil - Time.time);

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public PlayerVisibility TargetVisibilityComponent => targetVisibility;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Transform TargetTransform => targetTransform;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public PlayerUtilityController TargetUtilityController => targetUtilityController;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool ShouldIgnoreNoise => enemyMovementController != null && !enemyMovementController.CanReactToNoise();

    public Vector2 GizmoVisionOrigin => VisionOriginPosition;
    public Vector2 GizmoForwardDirection => ForwardDirection;
    public float ConfiguredVisionRange => visionRange;
    public float ConfiguredVisionAngle => visionAngle;
    public float ConfiguredFullDetectionRadius => fullDetectionRadius;
    public float ConfiguredFullDetectionSpeedMultiplier => fullDetectionSpeedMultiplier;

    [SerializeField, HideInInspector] private float currentDetectionValue;
    [SerializeField, HideInInspector] private float currentTargetVisibility;
    [SerializeField, HideInInspector] private Vector2 lastKnownTargetPosition;
    [SerializeField, HideInInspector] private bool targetInRange;
    [SerializeField, HideInInspector] private bool targetInsideVisionCone;
    [SerializeField, HideInInspector] private bool hasLineOfSight;
    [SerializeField, HideInInspector] private bool meetsVisibilityThreshold;
    [SerializeField, HideInInspector] private bool canCurrentlyDetectTarget;
    [SerializeField, HideInInspector] private bool targetInsideFullDetectionRadius;
    [SerializeField, HideInInspector] private bool usingFullDetectionRadius;
    [SerializeField, HideInInspector] private bool canCurrentlySeeFlashlight;
    [SerializeField, HideInInspector] private bool hasActiveFlashlightStimulus;
    [SerializeField, HideInInspector] private float currentTargetDistance;
    [SerializeField, HideInInspector] private float currentDistanceDetectionMultiplier = 1f;
    [SerializeField, HideInInspector] private Vector2 lastKnownFlashlightSourcePosition;
    [SerializeField, HideInInspector] private bool canCurrentlySeeBody;
    [SerializeField, HideInInspector] private Vector2 lastSeenBodyPosition;

    private float nextVisionCheckTime;
    private float nextTargetResolveTime;
    private float flashlightStimulusHoldUntil;
    private bool wasFullyDetectedLastFrame;
    private bool hadActiveFlashlightStimulusLastFrame;
    private bool hasLastKnownTargetPosition;
    private bool hasTrackedFlashlightSource;
    private bool hasIssuedFlashlightInvestigation;
    private Vector2 lastIssuedFlashlightInvestigationPosition;
    private EnemyState lastIssuedFlashlightInvestigationState = EnemyState.Suspicious;
    private float externalPerceptionMultiplier = 1f;

    /// <summary>
    /// Caches same-object references when the component is reset in the editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches runtime references, clamps settings, and performs an initial target resolve.
    /// </summary>
    private void Awake()
    {
        ClampSettings();
        CacheReferences();
        TryResolveTargetReferences(force: true);
    }

    /// <summary>
    /// Resets per-enable transient detection state and staggers the first vision check slightly.
    /// </summary>
    private void OnEnable()
    {
        float offset = Application.isPlaying ? Random.Range(0f, visionCheckInterval) : 0f;
        nextVisionCheckTime = Time.time + offset;
        nextTargetResolveTime = Time.time;
        flashlightStimulusHoldUntil = float.NegativeInfinity;
        hadActiveFlashlightStimulusLastFrame = false;
        hasTrackedFlashlightSource = false;
        hasIssuedFlashlightInvestigation = false;
        hasActiveFlashlightStimulus = false;
        wasFullyDetectedLastFrame = false;
    }

    /// <summary>
    /// Clamps authoring values and refreshes cached references while editing.
    /// </summary>
    private void OnValidate()
    {
        ClampSettings();
        CacheReferences();
    }

    /// <summary>
    /// Runs periodic vision sampling and forwards resulting stimuli into the movement controller.
    /// </summary>
    private void Update()
    {
        if (!CanRunVisionUpdate())
            return;

        if (Time.time >= nextVisionCheckTime)
        {
            PerformVisionCheck();
            nextVisionCheckTime = Time.time + visionCheckInterval;
        }

        UpdateDetection(Time.deltaTime);
        ForwardVisionStateToMovementController();
    }

    /// <summary>
    /// Applies vision settings loaded from an actor profile.
    /// </summary>
    public void ApplySettings(EnemyVisionSettings settings)
    {
        if (settings == null)
            return;

        visionRange = settings.VisionRange;
        visionAngle = settings.VisionAngle;
        useTransformUpAsForward = settings.UseTransformUpAsForward;
        localForwardDirection = settings.LocalForwardDirection;
        forwardAngleOffset = settings.ForwardAngleOffset;
        visionCheckInterval = settings.VisionCheckInterval;
        requireLineOfSight = settings.RequireLineOfSight;
        obstacleMask = settings.ObstacleMask;
        visibilityThreshold = settings.VisibilityThreshold;
        detectionSpeed = settings.DetectionSpeed;
        detectionDecaySpeed = settings.DetectionDecaySpeed;
        fullDetectionRadius = settings.FullDetectionRadius;
        fullDetectionSpeedMultiplier = settings.FullDetectionSpeedMultiplier;
        reactToFlashlight = settings.ReactToFlashlight;
        reactToBodies = settings.ReactToBodies;
        flashlightSourceLostDuration = settings.FlashlightSourceLostDuration;
        flashlightSourceUpdateDistance = settings.FlashlightSourceUpdateDistance;
        flashlightVisibilitySampleCount = settings.FlashlightVisibilitySampleCount;
        flashlightVisibilitySurfaceOffset = settings.FlashlightVisibilitySurfaceOffset;
        useDistanceDetectionMultiplier = settings.UseDistanceDetectionMultiplier;
        closeRangeDistance = settings.CloseRangeDistance;
        noBonusDistance = settings.NoBonusDistance;
        closeRangeDetectionMultiplier = settings.CloseRangeDetectionMultiplier;
        debugLogging = settings.DebugLogging;

        ClampSettings();
    }

    /// <summary>
    /// Applies an external runtime multiplier to perception calculations.
    /// </summary>
    public void SetExternalPerceptionMultiplier(float multiplier)
    {
        externalPerceptionMultiplier = Mathf.Clamp01(multiplier);
    }

    /// <summary>
    /// Returns whether this vision setup can perceive the provided world point right now.
    /// </summary>
    public bool CanPerceiveWorldPoint(Vector2 targetPosition, float targetVisibility = 1f)
    {
        float effectiveVisionRange = ResolveEffectiveVisionRange();
        if (effectiveVisionRange <= 0f)
            return false;

        Vector2 origin = VisionOriginPosition;
        Vector2 toTarget = targetPosition - origin;
        float distance = toTarget.magnitude;
        if (distance > effectiveVisionRange || toTarget.sqrMagnitude <= MinimumDirectionSqr)
            return false;

        if (!IsInsideVisionCone(toTarget))
            return false;

        if (requireLineOfSight && obstacleMask.value != 0)
        {
            if (Physics2D.Linecast(origin, targetPosition, obstacleMask).collider != null)
                return false;
        }

        float effectiveTargetVisibility = Mathf.Max(0f, targetVisibility) * externalPerceptionMultiplier;
        return effectiveTargetVisibility > visibilityThreshold;
    }

    /// <summary>
    /// Clears visual detection state and optionally returns temporary movement states to their default behavior.
    /// </summary>
    public void ClearVisualDetectionForConsoleCheat(bool resumeDefaultState = true)
    {
        currentDetectionValue = 0f;
        currentTargetVisibility = 0f;
        targetInRange = false;
        targetInsideVisionCone = false;
        hasLineOfSight = !requireLineOfSight || obstacleMask.value == 0;
        meetsVisibilityThreshold = false;
        canCurrentlyDetectTarget = false;
        targetInsideFullDetectionRadius = false;
        usingFullDetectionRadius = false;
        canCurrentlySeeFlashlight = false;
        hasActiveFlashlightStimulus = false;
        canCurrentlySeeBody = false;
        currentTargetDistance = 0f;
        currentDistanceDetectionMultiplier = 1f;
        flashlightStimulusHoldUntil = float.NegativeInfinity;
        hadActiveFlashlightStimulusLastFrame = false;
        hasLastKnownTargetPosition = false;
        hasTrackedFlashlightSource = false;
        hasIssuedFlashlightInvestigation = false;
        wasFullyDetectedLastFrame = false;
        lastKnownTargetPosition = Vector2.zero;
        lastKnownFlashlightSourcePosition = Vector2.zero;
        lastSeenBodyPosition = Vector2.zero;
        ResetFlashlightInvestigationRequestState();

        if (!resumeDefaultState || enemyMovementController == null)
            return;

        EnemyState currentState = enemyMovementController.CurrentState;
        if (currentState == EnemyState.Detected ||
            currentState == EnemyState.Suspicious ||
            currentState == EnemyState.Searching)
        {
            enemyMovementController.ClearExternalInvestigation(resumeDefaultBehavior: false);
            enemyMovementController.ReturnToStart();
        }
    }

    /// <summary>
    /// Samples target visibility, line of sight, and flashlight stimuli for the current frame.
    /// </summary>
    private void PerformVisionCheck()
    {
        canCurrentlyDetectTarget = false;
        canCurrentlySeeFlashlight = false;
        hasActiveFlashlightStimulus = false;
        canCurrentlySeeBody = false;
        targetInRange = false;
        targetInsideVisionCone = false;
        targetInsideFullDetectionRadius = false;
        usingFullDetectionRadius = false;
        hasLineOfSight = !requireLineOfSight || obstacleMask.value == 0;
        meetsVisibilityThreshold = false;
        currentTargetVisibility = 0f;
        currentTargetDistance = 0f;
        currentDistanceDetectionMultiplier = 1f;

        if (GameplayConsoleCheatState.Invisible)
            return;

        if (!TryResolveTargetReferences())
            return;

        Vector2 origin = VisionOriginPosition;
        EvaluateFlashlightVisibility(origin);
        EvaluateBodyVisibility(origin);
        hasActiveFlashlightStimulus = reactToFlashlight &&
                                      hasTrackedFlashlightSource &&
                                      (canCurrentlySeeFlashlight || Time.time < flashlightStimulusHoldUntil);

        Vector2 targetPosition = TargetSamplePosition;
        if (TryApplyFullDetectionRadius(origin, targetPosition))
            return;

        float effectiveVisionRange = ResolveEffectiveVisionRange();
        if (effectiveVisionRange <= 0f)
            return;

        Vector2 toTarget = targetPosition - origin;
        float distance = toTarget.magnitude;
        currentTargetDistance = distance;
        if (distance > effectiveVisionRange)
            return;

        targetInRange = true;
        if (!IsInsideVisionCone(toTarget))
            return;

        targetInsideVisionCone = true;
        if (requireLineOfSight && obstacleMask.value != 0)
        {
            hasLineOfSight = Physics2D.Linecast(origin, targetPosition, obstacleMask).collider == null;
            if (!hasLineOfSight)
                return;
        }

        currentTargetVisibility = (targetVisibility != null ? targetVisibility.CurrentVisibility : 1f) * externalPerceptionMultiplier;
        meetsVisibilityThreshold = currentTargetVisibility > visibilityThreshold;
        if (!meetsVisibilityThreshold)
            return;

        currentDistanceDetectionMultiplier = CalculateDistanceDetectionMultiplier(distance);
        canCurrentlyDetectTarget = true;
        lastKnownTargetPosition = targetPosition;
        hasLastKnownTargetPosition = true;
    }

    /// <summary>
    /// Advances or decays the continuous visual detection meter.
    /// </summary>
    private void UpdateDetection(float deltaTime)
    {
        if (canCurrentlyDetectTarget)
        {
            float detectionFactor = usingFullDetectionRadius
                ? Mathf.Clamp01(externalPerceptionMultiplier)
                : CalculateDetectionFactor();
            currentDetectionValue = Mathf.MoveTowards(
                currentDetectionValue,
                1f,
                detectionSpeed * detectionFactor * currentDistanceDetectionMultiplier * deltaTime);
        }
        else
        {
            currentDetectionValue = Mathf.MoveTowards(currentDetectionValue, 0f, detectionDecaySpeed * deltaTime);
        }

        currentDetectionValue = Mathf.Clamp01(currentDetectionValue);
    }

    /// <summary>
    /// Routes visual and flashlight stimulus state into the movement controller.
    /// </summary>
    private void ForwardVisionStateToMovementController()
    {
        if (enemyMovementController == null)
            return;

        bool combatOwnsTemporaryStates = enemyCombatantAI != null && enemyCombatantAI.IsDrafted;
        bool alertState = enemyMovementController.CurrentState == EnemyState.Alert;
        bool fullyDetected = IsFullyVisuallyDetected;

        if (fullyDetected)
        {
            HandleFullDetectionState(alertState);
        }
        else if (wasFullyDetectedLastFrame)
        {
            HandleDetectionLossState(alertState);
        }
        else if (alertState)
        {
            HandleAlertVisionState();
        }
        else
        {
            HandleNonAlertVisionState(combatOwnsTemporaryStates);
        }

        hadActiveFlashlightStimulusLastFrame = hasActiveFlashlightStimulus && !combatOwnsTemporaryStates && !fullyDetected;
        wasFullyDetectedLastFrame = fullyDetected;
    }

    /// <summary>
    /// Calculates the normalized detection factor from the current visibility value.
    /// </summary>
    private float CalculateDetectionFactor()
    {
        if (currentTargetVisibility <= visibilityThreshold)
            return 0f;

        if (visibilityThreshold >= 1f)
            return 1f;

        return Mathf.InverseLerp(visibilityThreshold, 1f, currentTargetVisibility);
    }

    /// <summary>
    /// Calculates the distance-based detection multiplier for the current target distance.
    /// </summary>
    private float CalculateDistanceDetectionMultiplier(float distance)
    {
        if (!useDistanceDetectionMultiplier)
            return 1f;

        if (distance <= closeRangeDistance)
            return closeRangeDetectionMultiplier;

        if (distance >= noBonusDistance)
            return 1f;

        if (Mathf.Approximately(closeRangeDistance, noBonusDistance))
            return closeRangeDetectionMultiplier;

        float t = Mathf.InverseLerp(closeRangeDistance, noBonusDistance, distance);
        return Mathf.Lerp(closeRangeDetectionMultiplier, 1f, t);
    }

    /// <summary>
    /// Applies full-detection-radius rules before the normal vision-cone path.
    /// </summary>
    private bool TryApplyFullDetectionRadius(Vector2 observerPosition, Vector2 targetPosition)
    {
        if (fullDetectionRadius <= 0f)
            return false;

        float distanceFromCenter = Vector2.Distance(transform.position, targetPosition);
        currentTargetDistance = distanceFromCenter;
        if (distanceFromCenter > fullDetectionRadius)
            return false;

        targetInsideFullDetectionRadius = true;
        targetInRange = true;

        if (requireLineOfSight && obstacleMask.value != 0)
        {
            hasLineOfSight = Physics2D.Linecast(observerPosition, targetPosition, obstacleMask).collider == null;
            if (!hasLineOfSight)
                return false;
        }

        currentTargetVisibility = targetVisibility != null
            ? targetVisibility.CurrentVisibility * externalPerceptionMultiplier
            : externalPerceptionMultiplier;
        canCurrentlyDetectTarget = externalPerceptionMultiplier > 0f;
        meetsVisibilityThreshold = canCurrentlyDetectTarget;
        usingFullDetectionRadius = canCurrentlyDetectTarget;
        currentDistanceDetectionMultiplier = fullDetectionSpeedMultiplier;

        if (!canCurrentlyDetectTarget)
            return false;

        lastKnownTargetPosition = targetPosition;
        hasLastKnownTargetPosition = true;
        return true;
    }

    /// <summary>
    /// Samples whether the enemy can currently see the player's flashlight cone.
    /// </summary>
    private void EvaluateFlashlightVisibility(Vector2 observerPosition)
    {
        canCurrentlySeeFlashlight = false;

        if (!reactToFlashlight || targetUtilityController == null)
            return;

        if (!targetUtilityController.TryGetActiveFlashlightCone(
                out Vector2 flashlightSource,
                out Vector2 flashlightDirection,
                out float flashlightRange,
                out float flashlightAngle))
        {
            return;
        }

        if (!TryFindVisibleFlashlightStimulusPoint(
                observerPosition,
                flashlightSource,
                flashlightDirection,
                flashlightRange,
                flashlightAngle,
                out Vector2 visibleStimulusPoint))
        {
            return;
        }

        canCurrentlySeeFlashlight = true;
        hasTrackedFlashlightSource = true;
        lastKnownFlashlightSourcePosition = flashlightSource;
        flashlightStimulusHoldUntil = Time.time + flashlightSourceLostDuration;
    }

    /// <summary>
    /// Maintains or refreshes the movement controller's flashlight investigation request.
    /// </summary>
    private void MaintainFlashlightInvestigation()
    {
        if (enemyMovementController == null || !hasTrackedFlashlightSource)
            return;

        EnemyState desiredState = canCurrentlySeeFlashlight ? EnemyState.Suspicious : EnemyState.Searching;
        bool shouldRefreshInvestigation =
            !hasIssuedFlashlightInvestigation ||
            lastIssuedFlashlightInvestigationState != desiredState ||
            enemyMovementController.CurrentState != desiredState ||
            !enemyMovementController.HasExternalInvestigation;

        if (!shouldRefreshInvestigation)
        {
            float refreshDistanceSqr = flashlightSourceUpdateDistance * flashlightSourceUpdateDistance;
            shouldRefreshInvestigation =
                (lastKnownFlashlightSourcePosition - lastIssuedFlashlightInvestigationPosition).sqrMagnitude >=
                refreshDistanceSqr;
        }

        if (!shouldRefreshInvestigation)
            return;

        enemyMovementController.SetExternalInvestigation(lastKnownFlashlightSourcePosition, desiredState);
        lastIssuedFlashlightInvestigationPosition = lastKnownFlashlightSourcePosition;
        lastIssuedFlashlightInvestigationState = desiredState;
        hasIssuedFlashlightInvestigation = true;
    }

    /// <summary>
    /// Searches the flashlight cone for a stimulus point that is visible to this enemy.
    /// </summary>
    private bool TryFindVisibleFlashlightStimulusPoint(
        Vector2 observerPosition,
        Vector2 flashlightSource,
        Vector2 flashlightDirection,
        float flashlightRange,
        float flashlightAngle,
        out Vector2 visibleStimulusPoint)
    {
        visibleStimulusPoint = Vector2.zero;

        if (flashlightDirection.sqrMagnitude <= MinimumDirectionSqr || flashlightRange <= 0f)
            return false;

        Vector2 normalizedDirection = flashlightDirection.normalized;
        float halfAngle = flashlightAngle * 0.5f;
        int sampleCount = Mathf.Max(1, flashlightVisibilitySampleCount);
        float bestScore = float.PositiveInfinity;
        bool foundVisiblePoint = false;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount == 1 ? 0.5f : i / (float)(sampleCount - 1);
            float angleOffset = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 sampleDirection = Rotate(normalizedDirection, angleOffset).normalized;
            Vector2 samplePoint = ResolveFlashlightStimulusPoint(flashlightSource, sampleDirection, flashlightRange);

            if (!CanSeeFlashlightStimulusPoint(observerPosition, samplePoint))
                continue;

            float score = Mathf.Abs(angleOffset);
            if (!foundVisiblePoint || score < bestScore)
            {
                bestScore = score;
                visibleStimulusPoint = samplePoint;
                foundVisiblePoint = true;
            }
        }

        return foundVisiblePoint;
    }

    /// <summary>
    /// Resolves the flashlight sample point by tracing against world obstacles when needed.
    /// </summary>
    private Vector2 ResolveFlashlightStimulusPoint(Vector2 flashlightSource, Vector2 sampleDirection, float flashlightRange)
    {
        if (obstacleMask.value != 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(flashlightSource, sampleDirection, flashlightRange, obstacleMask);
            if (hit.collider != null)
            {
                float offset = Mathf.Max(0f, flashlightVisibilitySurfaceOffset);
                Vector2 pointBeforeSurface = hit.point - (sampleDirection * offset);
                return offset > 0f ? pointBeforeSurface : hit.point;
            }
        }

        return flashlightSource + (sampleDirection * flashlightRange);
    }

    /// <summary>
    /// Returns whether a flashlight stimulus point is visible to this enemy right now.
    /// </summary>
    private bool CanSeeFlashlightStimulusPoint(Vector2 observerPosition, Vector2 stimulusPoint)
    {
        Vector2 toStimulus = stimulusPoint - observerPosition;
        float distance = toStimulus.magnitude;
        if (distance > visionRange || toStimulus.sqrMagnitude <= MinimumDirectionSqr)
            return false;

        if (!IsInsideVisionCone(toStimulus))
            return false;

        if (!requireLineOfSight || obstacleMask.value == 0)
            return true;

        return Physics2D.Linecast(observerPosition, stimulusPoint, obstacleMask).collider == null;
    }

    /// <summary>
    /// Resolves target references from assigned values or loaded-scene player objects.
    /// </summary>
    private bool TryResolveTargetReferences(bool force = false)
    {
        if (!force && targetTransform != null)
        {
            ResolveTargetSiblingReferences();
            return true;
        }

        if (!force && Time.time < nextTargetResolveTime)
            return targetTransform != null;

        nextTargetResolveTime = Time.time + MissingTargetResolveInterval;
        TryResolveUnassignedTargetReferences();

        if (targetVisibility != null && targetTransform == null)
            targetTransform = targetVisibility.transform;

        if (targetUtilityController != null && targetTransform == null)
            targetTransform = targetUtilityController.transform;

        if (targetTransform == null)
            return false;

        ResolveTargetSiblingReferences();
        return true;
    }

    /// <summary>
    /// Resolves a component relative to the provided target transform hierarchy.
    /// </summary>
    private static T ResolveTargetComponent<T>(Transform target) where T : Component
    {
        if (target == null)
            return null;

        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        component = target.GetComponentInParent<T>();
        if (component != null)
            return component;

        return target.GetComponentInChildren<T>(true);
    }

    /// <summary>
    /// Returns whether the provided target vector falls inside the current vision cone.
    /// </summary>
    private bool IsInsideVisionCone(Vector2 toTarget)
    {
        float effectiveVisionAngle = ResolveEffectiveVisionAngle();
        if (effectiveVisionAngle >= 360f)
            return true;

        Vector2 forward = ForwardDirection;
        float angleToTarget = Vector2.Angle(forward, toTarget.normalized);
        return angleToTarget <= effectiveVisionAngle * 0.5f;
    }

    /// <summary>
    /// Resolves the currently effective vision range after runtime multipliers.
    /// </summary>
    private float ResolveEffectiveVisionRange()
    {
        return visionRange * externalPerceptionMultiplier;
    }

    /// <summary>
    /// Resolves the currently effective vision angle after runtime multipliers.
    /// </summary>
    private float ResolveEffectiveVisionAngle()
    {
        return visionAngle * externalPerceptionMultiplier;
    }

    /// <summary>
    /// Clamps authoring settings into safe runtime ranges.
    /// </summary>
    private void ClampSettings()
    {
        visionRange = Mathf.Max(MinimumVisionRange, visionRange);
        visionAngle = Mathf.Clamp(visionAngle, 0f, 360f);
        visionCheckInterval = Mathf.Max(MinimumVisionCheckInterval, visionCheckInterval);
        visibilityThreshold = Mathf.Clamp01(visibilityThreshold);
        detectionSpeed = Mathf.Max(0f, detectionSpeed);
        detectionDecaySpeed = Mathf.Max(0f, detectionDecaySpeed);
        fullDetectionRadius = Mathf.Max(0f, fullDetectionRadius);
        fullDetectionSpeedMultiplier = Mathf.Max(0f, fullDetectionSpeedMultiplier);
        flashlightSourceLostDuration = Mathf.Max(0f, flashlightSourceLostDuration);
        flashlightSourceUpdateDistance = Mathf.Max(0f, flashlightSourceUpdateDistance);
        flashlightVisibilitySampleCount = Mathf.Clamp(flashlightVisibilitySampleCount, 1, 9);
        flashlightVisibilitySurfaceOffset = Mathf.Max(0f, flashlightVisibilitySurfaceOffset);
        closeRangeDistance = Mathf.Max(0f, closeRangeDistance);
        noBonusDistance = Mathf.Max(closeRangeDistance, noBonusDistance);
        closeRangeDetectionMultiplier = Mathf.Max(1f, closeRangeDetectionMultiplier);

        if (localForwardDirection.sqrMagnitude <= MinimumDirectionSqr)
            localForwardDirection = Vector2.up;
    }

    /// <summary>
    /// Clears cached flashlight investigation request state.
    /// </summary>
    private void ResetFlashlightInvestigationRequestState()
    {
        hasIssuedFlashlightInvestigation = false;
        lastIssuedFlashlightInvestigationPosition = Vector2.zero;
        lastIssuedFlashlightInvestigationState = EnemyState.Suspicious;
    }

    /// <summary>
    /// Returns whether this frame may execute vision logic.
    /// </summary>
    private bool CanRunVisionUpdate()
    {
        return !GameplayMissionController.EnemyRuntimeBlockedAtMissionStart;
    }

    /// <summary>
    /// Handles the movement-controller routing for a fully detected visual target.
    /// </summary>
    private void HandleFullDetectionState(bool alertState)
    {
        if (!wasFullyDetectedLastFrame)
        {
            MissionRuntimeEvents.RaiseEnemyPlayerFullyDetected(this, enemyMovementController);
            LogVisionEvent($"fully detected {targetTransform?.name ?? "target"}.");
        }

        enemyMovementController.ClearExternalInvestigation(resumeDefaultBehavior: false);
        if (alertState)
            enemyMovementController.UpdateAlertVisualTarget(targetTransform, lastKnownTargetPosition);
        else
            enemyMovementController.SetDetected(targetTransform);
    }

    /// <summary>
    /// Handles the movement-controller routing when a previously detected target is lost.
    /// </summary>
    private void HandleDetectionLossState(bool alertState)
    {
        enemyMovementController.ClearExternalInvestigation(resumeDefaultBehavior: false);
        if (alertState)
            enemyMovementController.ClearAlertVisualTarget();
        else
            enemyMovementController.LoseTarget();

        LogVisionEvent("lost full visual detection.");
    }

    /// <summary>
    /// Handles alert-state visual routing, including flashlight focus cleanup.
    /// </summary>
    private void HandleAlertVisionState()
    {
        if (hadActiveFlashlightStimulusLastFrame && !hasActiveFlashlightStimulus)
        {
            enemyMovementController.ClearExternalInvestigation(resumeDefaultBehavior: false);
            ClearFlashlightInvestigationTracking();
        }

        if (hasActiveFlashlightStimulus && hasTrackedFlashlightSource)
        {
            enemyMovementController.FocusAlertOnPoint(lastKnownFlashlightSourcePosition);
            return;
        }

        if (canCurrentlySeeBody)
        {
            enemyMovementController.FocusAlertOnPoint(lastSeenBodyPosition);
            return;
        }

        if (canCurrentlyDetectTarget && hasLastKnownTargetPosition)
            enemyMovementController.SetFacingPoint(lastKnownTargetPosition);
    }

    /// <summary>
    /// Handles non-alert visual routing for flashlight and partial visual suspicion stimuli.
    /// </summary>
    private void HandleNonAlertVisionState(bool combatOwnsTemporaryStates)
    {
        if (canCurrentlySeeBody)
        {
            enemyMovementController.EnterAlertState(force: true);
            enemyMovementController.FocusAlertOnPoint(lastSeenBodyPosition);
            return;
        }

        if (hasActiveFlashlightStimulus && !combatOwnsTemporaryStates)
        {
            MaintainFlashlightInvestigation();
            return;
        }

        if (hadActiveFlashlightStimulusLastFrame)
        {
            enemyMovementController.ClearExternalInvestigation();
            ClearFlashlightInvestigationTracking();
        }

        if (currentDetectionValue > 0f &&
            hasLastKnownTargetPosition &&
            !combatOwnsTemporaryStates)
        {
            enemyMovementController.RefreshSuspicion(lastKnownTargetPosition);
        }
    }

    /// <summary>
    /// Clears cached flashlight investigation state when the stimulus expires.
    /// </summary>
    private void ClearFlashlightInvestigationTracking()
    {
        ResetFlashlightInvestigationRequestState();
        hasTrackedFlashlightSource = false;
    }

    /// <summary>
    /// Resolves components adjacent to the currently assigned target transform.
    /// </summary>
    private void ResolveTargetSiblingReferences()
    {
        if (targetVisibility == null)
            targetVisibility = ResolveTargetComponent<PlayerVisibility>(targetTransform);

        if (targetUtilityController == null)
            targetUtilityController = ResolveTargetComponent<PlayerUtilityController>(targetTransform);
    }

    /// <summary>
    /// Resolves missing target references by scanning loaded scenes with the project-safe utility.
    /// </summary>
    private void TryResolveUnassignedTargetReferences()
    {
        if (targetVisibility == null)
            targetVisibility = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerVisibility>(gameObject);

        if (targetUtilityController == null)
            targetUtilityController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerUtilityController>(gameObject);
    }

    /// <summary>
    /// Caches same-object component references required by the vision system.
    /// </summary>
    private void CacheReferences()
    {
        enemyMovementController ??= GetComponent<EnemyMovementController>();
        enemyCombatantAI ??= GetComponent<EnemyCombatantAI>();
    }

    /// <summary>
    /// Samples dead or incapacitated enemy bodies visible to this enemy.
    /// </summary>
    private void EvaluateBodyVisibility(Vector2 observerPosition)
    {
        canCurrentlySeeBody = false;

        if (!reactToBodies || enemyMovementController == null)
            return;

        IReadOnlyList<ActorIncapacitationController> bodies = ActorIncapacitationController.ActiveControllers;
        if (bodies == null)
            return;

        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < bodies.Count; i++)
        {
            ActorIncapacitationController body = bodies[i];
            if (!IsValidBodyStimulus(body))
                continue;

            Vector2 bodyPosition = body.transform.position;
            float visibility = VisibilityLight2D.EvaluateTotalVisibilityAt(bodyPosition, Time.time);
            if (!CanPerceiveWorldPoint(bodyPosition, visibility))
                continue;

            float distanceSqr = (bodyPosition - observerPosition).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            canCurrentlySeeBody = true;
            lastSeenBodyPosition = bodyPosition;
        }
    }

    /// <summary>
    /// Returns whether the supplied incapacitated actor should count as an alerting body stimulus.
    /// </summary>
    private bool IsValidBodyStimulus(ActorIncapacitationController body)
    {
        if (body == null || body.gameObject == gameObject || !body.isActiveAndEnabled)
            return false;

        if (!body.IsDead && !body.IsIncapacitated)
            return false;

        MissionActorIdentity identity = body.GetComponent<MissionActorIdentity>();
        if (identity != null && identity.IsInnocent)
            return false;

        return body.GetComponent<EnemyMovementController>() != null;
    }

    /// <summary>
    /// Emits a debug log only when vision debugging is enabled.
    /// </summary>
    private void LogVisionEvent(string message)
    {
        if (!debugLogging)
            return;

        Debug.Log($"{name} {message}", this);
    }

    private bool IsFullyVisuallyDetected => currentDetectionValue >= 0.999f;

    private Vector2 VisionOriginPosition => visionOrigin != null ? (Vector2)visionOrigin.position : (Vector2)transform.position;

    private Vector2 TargetSamplePosition
    {
        get
        {
            if (targetVisibility != null)
                return targetVisibility.SamplePosition;

            return targetTransform != null ? (Vector2)targetTransform.position : (Vector2)transform.position;
        }
    }

    private Vector2 ForwardDirection
    {
        get
        {
            if (useTransformUpAsForward)
            {
                if (enemyMovementController != null && enemyMovementController.CurrentFacingDirection.sqrMagnitude > MinimumDirectionSqr)
                    return Rotate(enemyMovementController.CurrentFacingDirection.normalized, forwardAngleOffset);

                return Rotate(transform.up, forwardAngleOffset);
            }

            return Rotate(transform.TransformDirection(localForwardDirection.normalized), forwardAngleOffset);
        }
    }

    /// <summary>
    /// Rotates the provided vector by the given angle in degrees.
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
