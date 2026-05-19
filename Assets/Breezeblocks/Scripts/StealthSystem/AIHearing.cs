using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[AddComponentMenu("Breezeblocks/Stealth/AI Hearing")]
public class AIHearing : MonoBehaviour
{
    private const int MinimumObstructionChecks = 1;

    [FoldoutGroup("References")]
    [Tooltip("Optional origin point for hearing checks. If empty, this transform position is used.")]
    [SerializeField] private Transform hearingOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyCombatantAI enemyCombatantAI;

    private bool enableHearing = true;

    private float loudHearingRange = 15f;

    private float commonHearingRange = 8f;

    private float silentHearingRange = 3f;

    private float hearingThreshold = 0.2f;

    private float maximumAccumulatedDetection = 1f;

    private float detectionDecayDelay = 1f;

    private float detectionDecayPerSecond = 0.2f;

    private float closeDistanceMultiplier = 2f;

    private AnimationCurve distanceFalloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool useObstructionCheck = true;

    private LayerMask obstructionLayerMask;

    private float wallObstructionMultiplier = 0.2f;

    private int maxObstructionChecks = 4;

    private bool stackObstructionMultipliers;

    private bool debugHearing;

    private bool drawHearingRanges = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HearingIgnoredBecauseOfVisualDetection => hearingIgnoredBecauseOfVisualDetection;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public NoiseType LastHeardNoiseType => lastHeardNoiseType;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 2f)]
    public float LastHeardNoiseValue => lastHeardNoiseValue;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentAccumulatedDetection => currentAccumulatedDetection;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastHeardNoisePosition => lastHeardNoisePosition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool LastNoiseWasObstructed => lastNoiseWasObstructed;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int LastObstructionHitCount => lastObstructionHitCount;

    [SerializeField, HideInInspector] private bool hearingIgnoredBecauseOfVisualDetection;
    [SerializeField, HideInInspector] private NoiseType lastHeardNoiseType;
    [SerializeField, HideInInspector] private float lastHeardNoiseValue;
    [SerializeField, HideInInspector] private float currentAccumulatedDetection;
    [SerializeField, HideInInspector] private Vector2 lastHeardNoisePosition;
    [SerializeField, HideInInspector] private bool lastNoiseWasObstructed;
    [SerializeField, HideInInspector] private int lastObstructionHitCount;

    private RaycastHit2D[] obstructionHits;
    private ContactFilter2D obstructionContactFilter;
    private float lastAccumulationTime = float.NegativeInfinity;

    private void Reset()
    {
        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();
    }

    private void Awake()
    {
        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();

        EnsureObstructionBuffer();
        RefreshObstructionFilter();
    }

    private void OnEnable()
    {
        NoiseManager.RegisterListener(this);
    }

    private void OnDisable()
    {
        NoiseManager.UnregisterListener(this);
    }

    private void OnValidate()
    {
        loudHearingRange = Mathf.Max(0f, loudHearingRange);
        commonHearingRange = Mathf.Max(0f, commonHearingRange);
        silentHearingRange = Mathf.Max(0f, silentHearingRange);
        hearingThreshold = Mathf.Max(0f, hearingThreshold);
        maximumAccumulatedDetection = Mathf.Max(1f, hearingThreshold, maximumAccumulatedDetection);
        detectionDecayDelay = Mathf.Max(0f, detectionDecayDelay);
        detectionDecayPerSecond = Mathf.Max(0f, detectionDecayPerSecond);
        closeDistanceMultiplier = Mathf.Max(1f, closeDistanceMultiplier);
        wallObstructionMultiplier = Mathf.Clamp01(wallObstructionMultiplier);
        maxObstructionChecks = Mathf.Max(MinimumObstructionChecks, maxObstructionChecks);
        currentAccumulatedDetection = Mathf.Clamp(currentAccumulatedDetection, 0f, maximumAccumulatedDetection);
        EnsureObstructionBuffer();
        RefreshObstructionFilter();

        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();
    }

    private void Update()
    {
        if (!enableHearing ||
            currentAccumulatedDetection <= 0f ||
            detectionDecayPerSecond <= 0f ||
            Time.time < lastAccumulationTime + detectionDecayDelay)
        {
            return;
        }

        currentAccumulatedDetection = Mathf.MoveTowards(
            currentAccumulatedDetection,
            0f,
            detectionDecayPerSecond * Time.deltaTime);
    }

    public void ReceiveNoise(NoiseEvent noiseEvent)
    {
        hearingIgnoredBecauseOfVisualDetection = enemyCombatantAI != null
            ? enemyCombatantAI.ShouldIgnoreNoiseEvents
            : enemyMovementController != null && !enemyMovementController.CanReactToNoise();

        if (!enableHearing || enemyMovementController == null)
            return;

        if (hearingIgnoredBecauseOfVisualDetection)
        {
            currentAccumulatedDetection = 0f;
            return;
        }

        float hearingRange = ResolveHearingRange(noiseEvent.NoiseType);
        if (hearingRange <= 0f)
            return;

        Vector2 origin = HearingOriginPosition;
        Vector2 toNoise = noiseEvent.Position - origin;
        float distance = toNoise.magnitude;
        if (distance > hearingRange)
            return;

        float heardValue = CalculateHeardValue(noiseEvent, distance, hearingRange, origin);
        if (heardValue <= 0f)
            return;

        lastHeardNoiseType = noiseEvent.NoiseType;
        lastHeardNoiseValue = heardValue;
        lastHeardNoisePosition = noiseEvent.Position;
        lastAccumulationTime = Time.time;
        currentAccumulatedDetection = Mathf.Clamp(
            currentAccumulatedDetection + heardValue,
            0f,
            maximumAccumulatedDetection);

        if (currentAccumulatedDetection < hearingThreshold)
        {
            if (debugHearing)
            {
                Debug.Log(
                    $"{name} accumulated {currentAccumulatedDetection:0.00}/{hearingThreshold:0.00} hearing from {noiseEvent.NoiseType} noise at {noiseEvent.Position}.",
                    this);
            }

            return;
        }

        enemyCombatantAI?.HandleInvestigativeNoiseHeard(noiseEvent);
        if (enemyMovementController.CurrentState == EnemyState.Alert)
        {
            enemyMovementController.FocusAlertOnPoint(noiseEvent.Position);
        }
        else if (enemyMovementController.CurrentState == EnemyState.Searching ||
                 enemyMovementController.CurrentState == EnemyState.Suspicious)
        {
            enemyMovementController.UpdateInvestigativeDestination(noiseEvent.Position);
        }
        else
        {
            enemyMovementController.SetSuspicious(noiseEvent.Position);
        }

        if (debugHearing)
        {
            Debug.Log(
                $"{name} heard {noiseEvent.NoiseType} noise with value {heardValue:0.00} at {noiseEvent.Position} and reached {currentAccumulatedDetection:0.00}/{hearingThreshold:0.00}.",
                this);
        }
    }

    public void ApplySettings(EnemyHearingSettings settings)
    {
        if (settings == null)
            return;

        enableHearing = settings.EnableHearing;
        loudHearingRange = Mathf.Max(0f, settings.LoudHearingRange);
        commonHearingRange = Mathf.Max(0f, settings.CommonHearingRange);
        silentHearingRange = Mathf.Max(0f, settings.SilentHearingRange);
        hearingThreshold = Mathf.Max(0f, settings.HearingThreshold);
        maximumAccumulatedDetection = Mathf.Max(1f, hearingThreshold, settings.MaximumAccumulatedDetection);
        detectionDecayDelay = Mathf.Max(0f, settings.DetectionDecayDelay);
        detectionDecayPerSecond = Mathf.Max(0f, settings.DetectionDecayPerSecond);
        closeDistanceMultiplier = Mathf.Max(1f, settings.CloseDistanceMultiplier);
        distanceFalloffCurve = ActorProfileDataUtility.CloneCurve(settings.DistanceFalloffCurve);
        useObstructionCheck = settings.UseObstructionCheck;
        obstructionLayerMask = settings.ObstructionLayerMask;
        wallObstructionMultiplier = Mathf.Clamp01(settings.WallObstructionMultiplier);
        maxObstructionChecks = Mathf.Max(MinimumObstructionChecks, settings.MaxObstructionChecks);
        stackObstructionMultipliers = settings.StackObstructionMultipliers;
        debugHearing = settings.DebugHearing;
        drawHearingRanges = settings.DrawHearingRanges;
        currentAccumulatedDetection = Mathf.Clamp(currentAccumulatedDetection, 0f, maximumAccumulatedDetection);

        EnsureObstructionBuffer();
        RefreshObstructionFilter();
    }

    private float CalculateHeardValue(NoiseEvent noiseEvent, float distance, float hearingRange, Vector2 origin)
    {
        float normalizedDistance = hearingRange <= Mathf.Epsilon ? 1f : Mathf.Clamp01(distance / hearingRange);
        float closeness = 1f - normalizedDistance;
        float distanceFactor = Mathf.Max(0f, distanceFalloffCurve.Evaluate(closeness));
        float closeBonus = Mathf.Lerp(1f, closeDistanceMultiplier, closeness);
        float obstructionMultiplier = EvaluateObstructionMultiplier(noiseEvent.Position, origin);
        return noiseEvent.Intensity * distanceFactor * closeBonus * obstructionMultiplier;
    }

    private float EvaluateObstructionMultiplier(Vector2 start, Vector2 end)
    {
        lastNoiseWasObstructed = false;
        lastObstructionHitCount = 0;

        if (!useObstructionCheck || obstructionLayerMask.value == 0)
            return 1f;

        EnsureObstructionBuffer();
        int hitCount = Physics2D.Linecast(start, end, obstructionContactFilter, obstructionHits);
        if (hitCount <= 0)
            return 1f;

        lastNoiseWasObstructed = true;
        lastObstructionHitCount = Mathf.Min(hitCount, obstructionHits.Length);

        if (!stackObstructionMultipliers)
            return wallObstructionMultiplier;

        float multiplier = 1f;
        for (int i = 0; i < lastObstructionHitCount; i++)
            multiplier *= wallObstructionMultiplier;

        return multiplier;
    }

    private float ResolveHearingRange(NoiseType noiseType)
    {
        return noiseType switch
        {
            NoiseType.Loud => loudHearingRange,
            NoiseType.Common => commonHearingRange,
            NoiseType.Silent => silentHearingRange,
            _ => 0f
        };
    }

    private void EnsureObstructionBuffer()
    {
        int requiredSize = Mathf.Max(MinimumObstructionChecks, maxObstructionChecks);
        if (obstructionHits == null || obstructionHits.Length != requiredSize)
            obstructionHits = new RaycastHit2D[requiredSize];
    }

    private void RefreshObstructionFilter()
    {
        obstructionContactFilter = default;
        obstructionContactFilter.useLayerMask = true;
        obstructionContactFilter.layerMask = obstructionLayerMask;
        obstructionContactFilter.useTriggers = false;
    }

    private Vector2 HearingOriginPosition => hearingOrigin != null ? (Vector2)hearingOrigin.position : (Vector2)transform.position;

    private void OnDrawGizmosSelected()
    {
        if (!drawHearingRanges)
            return;

        Vector3 origin = HearingOriginPosition;
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.6f);
        Gizmos.DrawWireSphere(origin, loudHearingRange);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(origin, commonHearingRange);

        Gizmos.color = new Color(0.45f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireSphere(origin, silentHearingRange);

        if (!Application.isPlaying || lastHeardNoiseValue <= 0f)
            return;

        Gizmos.color = lastNoiseWasObstructed
            ? new Color(1f, 0.35f, 0.35f, 0.95f)
            : new Color(0.2f, 1f, 0.55f, 0.95f);

        Gizmos.DrawLine(origin, lastHeardNoisePosition);
        Gizmos.DrawSphere(lastHeardNoisePosition, 0.14f);
    }
}
