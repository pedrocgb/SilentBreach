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

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyCombatantAI enemyCombatantAI;

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

    private bool reactToFlashlight = true;

    private float flashlightSourceLostDuration = 2f;

    private float flashlightSourceUpdateDistance = 0.75f;

    private int flashlightVisibilitySampleCount = 5;

    private float flashlightVisibilitySurfaceOffset = 0.05f;

    private bool useDistanceDetectionMultiplier = true;

    private float closeRangeDistance = 1.5f;

    private float noBonusDistance = 6f;

    private float closeRangeDetectionMultiplier = 4f;

    private bool debugDraw = true;

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
    public bool CanCurrentlySeeFlashlight => canCurrentlySeeFlashlight;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasActiveFlashlightStimulus => hasActiveFlashlightStimulus;

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

    [SerializeField, HideInInspector] private float currentDetectionValue;
    [SerializeField, HideInInspector] private float currentTargetVisibility;
    [SerializeField, HideInInspector] private Vector2 lastKnownTargetPosition;
    [SerializeField, HideInInspector] private bool targetInRange;
    [SerializeField, HideInInspector] private bool targetInsideVisionCone;
    [SerializeField, HideInInspector] private bool hasLineOfSight;
    [SerializeField, HideInInspector] private bool meetsVisibilityThreshold;
    [SerializeField, HideInInspector] private bool canCurrentlyDetectTarget;
    [SerializeField, HideInInspector] private bool canCurrentlySeeFlashlight;
    [SerializeField, HideInInspector] private bool hasActiveFlashlightStimulus;
    [SerializeField, HideInInspector] private float currentTargetDistance;
    [SerializeField, HideInInspector] private float currentDistanceDetectionMultiplier = 1f;
    [SerializeField, HideInInspector] private Vector2 lastKnownFlashlightSourcePosition;

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

    private void Reset()
    {
        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();
    }

    private void Awake()
    {
        ClampSettings();

        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();

        TryResolveTargetReferences(force: true);
    }

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

    private void OnValidate()
    {
        ClampSettings();

        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();
    }

    private void Update()
    {
        if (Time.time >= nextVisionCheckTime)
        {
            PerformVisionCheck();
            nextVisionCheckTime = Time.time + visionCheckInterval;
        }

        UpdateDetection(Time.deltaTime);
        ForwardVisionStateToMovementController();
    }

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
        reactToFlashlight = settings.ReactToFlashlight;
        flashlightSourceLostDuration = settings.FlashlightSourceLostDuration;
        flashlightSourceUpdateDistance = settings.FlashlightSourceUpdateDistance;
        flashlightVisibilitySampleCount = settings.FlashlightVisibilitySampleCount;
        flashlightVisibilitySurfaceOffset = settings.FlashlightVisibilitySurfaceOffset;
        useDistanceDetectionMultiplier = settings.UseDistanceDetectionMultiplier;
        closeRangeDistance = settings.CloseRangeDistance;
        noBonusDistance = settings.NoBonusDistance;
        closeRangeDetectionMultiplier = settings.CloseRangeDetectionMultiplier;
        debugDraw = settings.DebugDraw;
        debugLogging = settings.DebugLogging;

        ClampSettings();
    }

    public void SetExternalPerceptionMultiplier(float multiplier)
    {
        externalPerceptionMultiplier = Mathf.Clamp01(multiplier);
    }

    private void PerformVisionCheck()
    {
        canCurrentlyDetectTarget = false;
        canCurrentlySeeFlashlight = false;
        hasActiveFlashlightStimulus = false;
        targetInRange = false;
        targetInsideVisionCone = false;
        hasLineOfSight = !requireLineOfSight || obstacleMask.value == 0;
        meetsVisibilityThreshold = false;
        currentTargetVisibility = 0f;
        currentTargetDistance = 0f;
        currentDistanceDetectionMultiplier = 1f;

        if (!TryResolveTargetReferences())
            return;

        Vector2 origin = VisionOriginPosition;
        EvaluateFlashlightVisibility(origin);
        hasActiveFlashlightStimulus = reactToFlashlight &&
                                      hasTrackedFlashlightSource &&
                                      (canCurrentlySeeFlashlight || Time.time < flashlightStimulusHoldUntil);
        float effectiveVisionRange = ResolveEffectiveVisionRange();
        if (effectiveVisionRange <= 0f)
            return;

        Vector2 targetPosition = TargetSamplePosition;
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

    private void UpdateDetection(float deltaTime)
    {
        if (canCurrentlyDetectTarget)
        {
            float detectionFactor = CalculateDetectionFactor();
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

    private void ForwardVisionStateToMovementController()
    {
        if (enemyMovementController == null)
            return;

        bool combatOwnsTemporaryStates = enemyCombatantAI != null && enemyCombatantAI.IsDrafted;
        bool alertState = enemyMovementController.CurrentState == EnemyState.Alert;
        bool fullyDetected = IsFullyVisuallyDetected;
        if (fullyDetected)
        {
            enemyMovementController.ClearExternalInvestigation(resumeDefaultBehavior: false);
            enemyMovementController.SetDetected(targetTransform);
        }
        else if (wasFullyDetectedLastFrame)
        {
            enemyMovementController.ClearExternalInvestigation(resumeDefaultBehavior: false);
            enemyMovementController.LoseTarget();
        }
        else if (alertState)
        {
            if (hadActiveFlashlightStimulusLastFrame && !hasActiveFlashlightStimulus)
            {
                enemyMovementController.ClearExternalInvestigation(resumeDefaultBehavior: false);
                ResetFlashlightInvestigationRequestState();
                hasTrackedFlashlightSource = false;
            }

            if (hasActiveFlashlightStimulus && hasTrackedFlashlightSource)
            {
                enemyMovementController.FocusAlertOnPoint(lastKnownFlashlightSourcePosition);
            }
            else if (canCurrentlyDetectTarget && hasLastKnownTargetPosition)
            {
                enemyMovementController.SetFacingPoint(lastKnownTargetPosition);
            }
        }
        else if (hasActiveFlashlightStimulus && !combatOwnsTemporaryStates)
        {
            MaintainFlashlightInvestigation();
        }
        else
        {
            if (hadActiveFlashlightStimulusLastFrame)
            {
                enemyMovementController.ClearExternalInvestigation();
                ResetFlashlightInvestigationRequestState();
                hasTrackedFlashlightSource = false;
            }

            if (currentDetectionValue > 0f &&
                hasLastKnownTargetPosition &&
                !combatOwnsTemporaryStates)
            {
                enemyMovementController.SetSuspicious(lastKnownTargetPosition);
            }
        }

        hadActiveFlashlightStimulusLastFrame = hasActiveFlashlightStimulus && !combatOwnsTemporaryStates && !fullyDetected;
        wasFullyDetectedLastFrame = fullyDetected;
    }

    private float CalculateDetectionFactor()
    {
        if (currentTargetVisibility <= visibilityThreshold)
            return 0f;

        if (visibilityThreshold >= 1f)
            return 1f;

        return Mathf.InverseLerp(visibilityThreshold, 1f, currentTargetVisibility);
    }

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

    private bool TryResolveTargetReferences(bool force = false)
    {
        if (!force && targetTransform != null)
        {
            if (targetVisibility == null)
                targetVisibility = ResolveTargetComponent<PlayerVisibility>(targetTransform);

            if (targetUtilityController == null)
                targetUtilityController = ResolveTargetComponent<PlayerUtilityController>(targetTransform);

            return true;
        }

        if (!force && Time.time < nextTargetResolveTime)
            return targetTransform != null;

        nextTargetResolveTime = Time.time + MissingTargetResolveInterval;

        if (targetVisibility == null)
            targetVisibility = FindFirstObjectByType<PlayerVisibility>();

        if (targetUtilityController == null)
            targetUtilityController = FindFirstObjectByType<PlayerUtilityController>();

        if (targetVisibility != null && targetTransform == null)
            targetTransform = targetVisibility.transform;

        if (targetUtilityController != null && targetTransform == null)
            targetTransform = targetUtilityController.transform;

        if (targetTransform == null)
            return false;

        if (targetVisibility == null)
            targetVisibility = ResolveTargetComponent<PlayerVisibility>(targetTransform);

        if (targetUtilityController == null)
            targetUtilityController = ResolveTargetComponent<PlayerUtilityController>(targetTransform);

        return true;
    }

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

    private bool IsInsideVisionCone(Vector2 toTarget)
    {
        float effectiveVisionAngle = ResolveEffectiveVisionAngle();
        if (effectiveVisionAngle >= 360f)
            return true;

        Vector2 forward = ForwardDirection;
        float angleToTarget = Vector2.Angle(forward, toTarget.normalized);
        return angleToTarget <= effectiveVisionAngle * 0.5f;
    }

    private float ResolveEffectiveVisionRange()
    {
        return visionRange * externalPerceptionMultiplier;
    }

    private float ResolveEffectiveVisionAngle()
    {
        return visionAngle * externalPerceptionMultiplier;
    }

    private static bool IsInsideAngle(Vector2 forward, Vector2 toTarget, float angle)
    {
        if (angle >= 360f)
            return true;

        if (forward.sqrMagnitude <= MinimumDirectionSqr || toTarget.sqrMagnitude <= MinimumDirectionSqr)
            return false;

        float angleToTarget = Vector2.Angle(forward.normalized, toTarget.normalized);
        return angleToTarget <= angle * 0.5f;
    }

    private void ClampSettings()
    {
        visionRange = Mathf.Max(MinimumVisionRange, visionRange);
        visionAngle = Mathf.Clamp(visionAngle, 0f, 360f);
        visionCheckInterval = Mathf.Max(MinimumVisionCheckInterval, visionCheckInterval);
        visibilityThreshold = Mathf.Clamp01(visibilityThreshold);
        detectionSpeed = Mathf.Max(0f, detectionSpeed);
        detectionDecaySpeed = Mathf.Max(0f, detectionDecaySpeed);
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

    private bool ShouldShowLocalForwardDirection()
    {
        return !useTransformUpAsForward;
    }

    private void ResetFlashlightInvestigationRequestState()
    {
        hasIssuedFlashlightInvestigation = false;
        lastIssuedFlashlightInvestigationPosition = Vector2.zero;
        lastIssuedFlashlightInvestigationState = EnemyState.Suspicious;
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

    private void OnDrawGizmosSelected()
    {
        if (!debugDraw)
            return;

        Vector2 origin = VisionOriginPosition;
        Color stateColor = CurrentState switch
        {
            EnemyState.Idle => new Color(0.4f, 1f, 0.4f, 0.85f),
            EnemyState.Patrol => new Color(0.2f, 0.85f, 1f, 0.85f),
            EnemyState.Suspicious => new Color(1f, 0.85f, 0.2f, 0.9f),
            EnemyState.Searching => new Color(1f, 0.65f, 0.2f, 0.9f),
            _ => new Color(1f, 0.3f, 0.3f, 0.9f)
        };

        Gizmos.color = stateColor;
        Gizmos.DrawWireSphere(origin, visionRange);

        Vector2 forward = ForwardDirection;
        if (visionAngle >= 360f)
        {
            Gizmos.DrawLine(origin, origin + (forward * visionRange));
        }
        else
        {
            float halfAngle = visionAngle * 0.5f;
            Vector2 left = Rotate(forward, -halfAngle) * visionRange;
            Vector2 right = Rotate(forward, halfAngle) * visionRange;
            Gizmos.DrawLine(origin, origin + left);
            Gizmos.DrawLine(origin, origin + right);
            DrawArc(origin, forward, visionRange, visionAngle);
        }

        if (targetTransform == null)
            return;

        Gizmos.color = canCurrentlyDetectTarget
            ? new Color(1f, 0.15f, 0.15f, 0.95f)
            : new Color(0.6f, 0.7f, 1f, 0.55f);

        Gizmos.DrawLine(origin, TargetSamplePosition);
    }

    private void DrawArc(Vector2 origin, Vector2 forward, float radius, float angle)
    {
        const int Segments = 24;
        float halfAngle = angle * 0.5f;
        Vector2 previousPoint = origin + (Rotate(forward, -halfAngle) * radius);

        for (int i = 1; i <= Segments; i++)
        {
            float t = i / (float)Segments;
            float stepAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector2 nextPoint = origin + (Rotate(forward, stepAngle) * radius);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }

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
