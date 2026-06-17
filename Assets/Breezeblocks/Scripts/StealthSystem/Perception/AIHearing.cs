using System;
using Breezeblocks.Missions;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[AddComponentMenu("Breezeblocks/Stealth/AI Hearing")]
public class AIHearing : MonoBehaviour
{
    public event Action<NoiseEvent> NoiseReactionTriggered;

    private const int MinimumObstructionChecks = 1;

    [FoldoutGroup("References")]
    [Tooltip("Optional origin point for hearing checks. If empty, this transform position is used.")]
    [SerializeField] private Transform hearingOrigin;

    private EnemyMovementController enemyMovementController;
    private EnemyCombatantAI enemyCombatantAI;
    private EnemySleepController enemySleepController;

    private bool enableHearing = true;
    private float loudHearingRange = 15f;
    private float commonHearingRange = 8f;
    private float silentHearingRange = 3f;
    private bool ignoreSilentSounds;
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

    public Vector2 GizmoHearingOrigin => HearingOriginPosition;
    public float ConfiguredLoudHearingRange => loudHearingRange;
    public float ConfiguredCommonHearingRange => commonHearingRange;
    public float ConfiguredSilentHearingRange => silentHearingRange;

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
    private float externalSensitivityMultiplier = 1f;

    /// <summary>
    /// Caches same-object references when the component is reset in the editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches runtime references and prepares reusable obstruction query data.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        EnsureObstructionBuffer();
        RefreshObstructionFilter();
    }

    /// <summary>
    /// Registers this listener with the shared noise manager.
    /// </summary>
    private void OnEnable()
    {
        NoiseManager.RegisterListener(this);
    }

    /// <summary>
    /// Unregisters this listener from the shared noise manager.
    /// </summary>
    private void OnDisable()
    {
        NoiseManager.UnregisterListener(this);
    }

    /// <summary>
    /// Clamps authoring values and refreshes cached reference/query state in the editor.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
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
    }

    /// <summary>
    /// Decays hearing accumulation when no recent stimulus is sustaining suspicion.
    /// </summary>
    private void Update()
    {
        if (ShouldSkipDecayUpdate())
            return;

        currentAccumulatedDetection = Mathf.MoveTowards(
            currentAccumulatedDetection,
            0f,
            detectionDecayPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// Processes an incoming noise event and routes the resulting reaction to movement and combat systems.
    /// </summary>
    public void ReceiveNoise(NoiseEvent noiseEvent)
    {
        if (GameplayMissionController.EnemyRuntimeBlockedAtMissionStart)
            return;

        if (noiseEvent.Source != null && noiseEvent.Source.transform.root == transform.root)
            return;

        bool isSleeping = enemySleepController != null && enemySleepController.IsSleeping;
        hearingIgnoredBecauseOfVisualDetection = !isSleeping && enemyCombatantAI != null
            ? enemyCombatantAI.ShouldIgnoreNoiseEvents
            : !isSleeping && enemyMovementController != null && !enemyMovementController.CanReactToNoise();

        if (!enableHearing || enemyMovementController == null)
            return;

        if (!isSleeping && enemySleepController != null && enemySleepController.IsWakeDelayActive)
            return;

        if (GameplayConsoleCheatState.Lightfooted || (!isSleeping && hearingIgnoredBecauseOfVisualDetection))
        {
            currentAccumulatedDetection = 0f;
            return;
        }

        if (isSleeping && !enemySleepController.CanProcessSleepingNoise(noiseEvent))
            return;

        if (ignoreSilentSounds && noiseEvent.NoiseType == NoiseType.Silent)
            return;

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

        if (isSleeping)
        {
            enemySleepController.TryWakeFromNoise(noiseEvent, heardValue, noiseEvent.IsExtremeNoise);
            return;
        }

        currentAccumulatedDetection = Mathf.Clamp(
            currentAccumulatedDetection + heardValue,
            0f,
            maximumAccumulatedDetection);

        if (noiseEvent.IsExtremeNoise)
        {
            RouteNoiseReaction(noiseEvent, heardValue, true);
            return;
        }

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

        RouteNoiseReaction(noiseEvent, heardValue, false);
    }

    /// <summary>
    /// Applies hearing settings loaded from an actor profile.
    /// </summary>
    public void ApplySettings(EnemyHearingSettings settings)
    {
        if (settings == null)
            return;

        enableHearing = settings.EnableHearing;
        loudHearingRange = Mathf.Max(0f, settings.LoudHearingRange);
        commonHearingRange = Mathf.Max(0f, settings.CommonHearingRange);
        silentHearingRange = Mathf.Max(0f, settings.SilentHearingRange);
        ignoreSilentSounds = settings.IgnoreSilentSounds;
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
        currentAccumulatedDetection = Mathf.Clamp(currentAccumulatedDetection, 0f, maximumAccumulatedDetection);

        EnsureObstructionBuffer();
        RefreshObstructionFilter();
    }

    /// <summary>
    /// Applies an external runtime multiplier to all hearing sensitivity calculations.
    /// </summary>
    public void SetExternalSensitivityMultiplier(float multiplier)
    {
        externalSensitivityMultiplier = Mathf.Clamp01(multiplier);
    }

    /// <summary>
    /// Clears hearing accumulation and optionally returns temporary investigation states to default behavior.
    /// </summary>
    public void ClearAccumulatedDetectionForConsoleCheat(bool resumeDefaultState = true)
    {
        currentAccumulatedDetection = 0f;
        lastHeardNoiseValue = 0f;
        lastHeardNoisePosition = Vector2.zero;
        lastNoiseWasObstructed = false;
        lastObstructionHitCount = 0;
        lastAccumulationTime = float.NegativeInfinity;

        if (!resumeDefaultState || enemyMovementController == null)
            return;

        EnemyState currentState = enemyMovementController.CurrentState;
        if (currentState == EnemyState.Suspicious || currentState == EnemyState.Searching)
            enemyMovementController.ReturnToStart();
    }

    /// <summary>
    /// Routes a confirmed noise reaction after a sleeping enemy finishes its wake-up delay.
    /// </summary>
    public void CompleteDelayedWakeNoiseReaction(NoiseEvent noiseEvent, float heardValue, bool extremeNoise)
    {
        if (!enableHearing || enemyMovementController == null)
            return;

        currentAccumulatedDetection = Mathf.Max(currentAccumulatedDetection, hearingThreshold);
        lastHeardNoiseType = noiseEvent.NoiseType;
        lastHeardNoiseValue = heardValue;
        lastHeardNoisePosition = noiseEvent.Position;
        lastAccumulationTime = Time.time;
        RouteNoiseReaction(noiseEvent, heardValue, extremeNoise);
    }

    /// <summary>
    /// Calculates the effective heard value after distance, falloff, and obstruction modifiers.
    /// </summary>
    private float CalculateHeardValue(NoiseEvent noiseEvent, float distance, float hearingRange, Vector2 origin)
    {
        float normalizedDistance = hearingRange <= Mathf.Epsilon ? 1f : Mathf.Clamp01(distance / hearingRange);
        float closeness = 1f - normalizedDistance;
        float distanceFactor = Mathf.Max(0f, distanceFalloffCurve.Evaluate(closeness));
        float closeBonus = Mathf.Lerp(1f, closeDistanceMultiplier, closeness);
        float obstructionMultiplier = EvaluateObstructionMultiplier(noiseEvent.Position, origin);
        return noiseEvent.Intensity * distanceFactor * closeBonus * obstructionMultiplier * externalSensitivityMultiplier;
    }

    /// <summary>
    /// Evaluates obstruction attenuation between the noise source and this listener.
    /// </summary>
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

    /// <summary>
    /// Resolves the configured hearing range for the provided noise type.
    /// </summary>
    private float ResolveHearingRange(NoiseType noiseType)
    {
        return noiseType switch
        {
            NoiseType.Loud => loudHearingRange * externalSensitivityMultiplier,
            NoiseType.Common => commonHearingRange * externalSensitivityMultiplier,
            NoiseType.Silent => silentHearingRange * externalSensitivityMultiplier,
            _ => 0f
        };
    }

    /// <summary>
    /// Returns whether the passive decay update should be skipped this frame.
    /// </summary>
    private bool ShouldSkipDecayUpdate()
    {
        if (GameplayMissionController.EnemyRuntimeBlockedAtMissionStart ||
            GameplayConsoleCheatState.Lightfooted)
        {
            currentAccumulatedDetection = 0f;
            return true;
        }

        return !enableHearing ||
               currentAccumulatedDetection <= 0f ||
               detectionDecayPerSecond <= 0f ||
               Time.time < lastAccumulationTime + detectionDecayDelay;
    }

    /// <summary>
    /// Routes a confirmed heard noise into the owning AI systems.
    /// </summary>
    private void RouteNoiseReaction(NoiseEvent noiseEvent, float heardValue, bool extremeNoise)
    {
        if (extremeNoise)
        {
            currentAccumulatedDetection = Mathf.Max(currentAccumulatedDetection, hearingThreshold);
            enemyMovementController.ReactToExtremeNoise(noiseEvent.Position);
        }
        else
        {
            enemyCombatantAI?.HandleInvestigativeNoiseHeard(noiseEvent);
            enemyMovementController.HandleHeardNoise(noiseEvent.Position);
        }

        NoiseReactionTriggered?.Invoke(noiseEvent);

        if (!debugHearing)
            return;

        string reactionPrefix = extremeNoise ? "EXTREME " : string.Empty;
        string thresholdSuffix = extremeNoise
            ? string.Empty
            : $" and reached {currentAccumulatedDetection:0.00}/{hearingThreshold:0.00}";
        Debug.Log(
            $"{name} heard {reactionPrefix}{noiseEvent.NoiseType} noise with value {heardValue:0.00} at {noiseEvent.Position}{thresholdSuffix}.",
            this);
    }

    /// <summary>
    /// Caches same-object component references used by hearing-driven behaviors.
    /// </summary>
    private void CacheReferences()
    {
        enemyMovementController ??= GetComponent<EnemyMovementController>();
        enemyCombatantAI ??= GetComponent<EnemyCombatantAI>();
        enemySleepController ??= GetComponent<EnemySleepController>();
    }

    /// <summary>
    /// Ensures the reusable obstruction hit buffer matches the configured query size.
    /// </summary>
    private void EnsureObstructionBuffer()
    {
        int requiredSize = Mathf.Max(MinimumObstructionChecks, maxObstructionChecks);
        if (obstructionHits == null || obstructionHits.Length != requiredSize)
            obstructionHits = new RaycastHit2D[requiredSize];
    }

    /// <summary>
    /// Refreshes the reusable contact filter for obstruction linecasts.
    /// </summary>
    private void RefreshObstructionFilter()
    {
        obstructionContactFilter = default;
        obstructionContactFilter.useLayerMask = true;
        obstructionContactFilter.layerMask = obstructionLayerMask;
        obstructionContactFilter.useTriggers = false;
    }

    private Vector2 HearingOriginPosition => hearingOrigin != null ? (Vector2)hearingOrigin.position : (Vector2)transform.position;
}
