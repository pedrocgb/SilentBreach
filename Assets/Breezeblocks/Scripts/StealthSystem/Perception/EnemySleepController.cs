using System.Collections;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

public enum EnemySleepType
{
    NormalSleep,
    DeepSleep,
    ForcedSleep
}

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[RequireComponent(typeof(ActorHealth))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Sleep Controller")]
public sealed class EnemySleepController : MonoBehaviour
{
    [FoldoutGroup("Thresholds"), Range(0f, 1f)]
    [SerializeField] private float normalSleepWakeThreshold = 0.4f;

    [FoldoutGroup("Thresholds"), Range(0f, 1f)]
    [SerializeField] private float deepSleepWakeThreshold = 0.8f;

    [FoldoutGroup("Duration"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float normalSleepAutoWakeDelay = 120f;

    [FoldoutGroup("Duration"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float deepSleepAutoWakeDelay = 240f;

    [FoldoutGroup("Duration"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float forcedSleepAutoWakeDelay = 60f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSleeping => isSleeping;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsWakeDelayActive => isWakeDelayActive;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemySleepType CurrentSleepType => currentSleepType;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentWakeThreshold => ResolveWakeThreshold(currentSleepType);

    private EnemyMovementController movementController;
    private EnemyVisionAI visionAI;
    private AIHearing hearing;
    private ActorHealth actorHealth;
    private Coroutine sleepRoutine;
    private EnemySleepType currentSleepType;
    private NoiseEvent pendingWakeNoise;
    private float pendingWakeHeardValue;
    private bool pendingWakeExtremeNoise;
    private bool isSleeping;
    private bool isWakeDelayActive;
    private bool configuredStartSleeping;
    private bool forcedSleepSuppressedHearing;
    private bool hearingWasEnabledBeforeForcedSleep;
    private EnemySleepType configuredStartSleepType = EnemySleepType.NormalSleep;

    /// <summary>
    /// Caches same-object dependencies before sleep state can be applied.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Applies optional start-sleep state after other enemy startup methods have run.
    /// </summary>
    private void Start()
    {
        if (configuredStartSleeping)
            sleepRoutine = StartCoroutine(StartSleepingAfterStartupRoutine(configuredStartSleepType));
    }

    /// <summary>
    /// Restores temporarily suppressed senses when this controller leaves play.
    /// </summary>
    private void OnDisable()
    {
        StopSleepRoutine();

        bool bodyUnavailable = actorHealth != null && (actorHealth.IsDead || actorHealth.IsIncapacitated);
        if (!bodyUnavailable)
        {
            RestoreForcedSleepHearing();
            ApplySleepingSenses(false);
        }

        isSleeping = false;
        isWakeDelayActive = false;
    }

    /// <summary>
    /// Keeps authoring values within safe ranges while editing.
    /// </summary>
    private void OnValidate()
    {
        normalSleepWakeThreshold = Mathf.Clamp01(normalSleepWakeThreshold);
        deepSleepWakeThreshold = Mathf.Clamp01(deepSleepWakeThreshold);
        normalSleepAutoWakeDelay = Mathf.Max(0f, normalSleepAutoWakeDelay);
        deepSleepAutoWakeDelay = Mathf.Max(0f, deepSleepAutoWakeDelay);
        forcedSleepAutoWakeDelay = Mathf.Max(0f, forcedSleepAutoWakeDelay);
        CacheReferences();
    }

    /// <summary>
    /// Starts sleeping with the configured startup sleep type.
    /// </summary>
    [Button(ButtonSizes.Small), FoldoutGroup("Debug")]
    public void StartConfiguredSleep()
    {
        StartSleeping(configuredStartSleepType);
    }

    /// <summary>
    /// Applies profile-authored startup sleep settings without exposing duplicate scene-level fields.
    /// </summary>
    public void ApplySettings(EnemySleepSettings settings)
    {
        if (settings == null)
            return;

        configuredStartSleeping = settings.StartSleeping;
        configuredStartSleepType = settings.StartSleepType;
    }

    /// <summary>
    /// Starts sleeping with an explicit sleep type and prepares the matching wake timer.
    /// </summary>
    public void StartSleeping(EnemySleepType sleepType)
    {
        CacheReferences();
        if (actorHealth == null || actorHealth.IsDead || actorHealth.IsIncapacitated)
            return;

        StopSleepRoutine();
        currentSleepType = sleepType;
        isSleeping = true;
        isWakeDelayActive = false;
        pendingWakeNoise = default;
        pendingWakeHeardValue = 0f;
        pendingWakeExtremeNoise = false;

        movementController?.EnterSleepingState();
        ApplySleepingSenses(true);

        float autoWakeDelay = ResolveAutoWakeDelay(sleepType);
        if (autoWakeDelay > 0f)
            sleepRoutine = StartCoroutine(AutoWakeRoutine(autoWakeDelay));
    }

    /// <summary>
    /// Wakes immediately and resumes default behavior after the global wake delay.
    /// </summary>
    public void WakeUpNaturally()
    {
        if (!isSleeping)
            return;

        BeginWake(noiseDriven: false);
    }

    /// <summary>
    /// Returns whether a sleeping enemy is allowed to process this noise type.
    /// </summary>
    public bool CanProcessSleepingNoise(NoiseEvent noiseEvent)
    {
        return isSleeping &&
               currentSleepType != EnemySleepType.ForcedSleep &&
               noiseEvent.NoiseType != NoiseType.Silent;
    }

    /// <summary>
    /// Attempts to wake the enemy from a qualifying heard noise value.
    /// </summary>
    public bool TryWakeFromNoise(NoiseEvent noiseEvent, float heardValue, bool extremeNoise)
    {
        if (!CanProcessSleepingNoise(noiseEvent))
            return false;

        if (heardValue < ResolveWakeThreshold(currentSleepType))
            return false;

        pendingWakeNoise = noiseEvent;
        pendingWakeHeardValue = heardValue;
        pendingWakeExtremeNoise = extremeNoise;
        BeginWake(noiseDriven: true);
        return true;
    }

    /// <summary>
    /// Delays start-sleep until all scene Start methods have had one frame to initialize.
    /// </summary>
    private IEnumerator StartSleepingAfterStartupRoutine(EnemySleepType sleepType)
    {
        yield return null;
        sleepRoutine = null;
        StartSleeping(sleepType);
    }

    /// <summary>
    /// Wakes the enemy after the configured sleep duration expires naturally.
    /// </summary>
    private IEnumerator AutoWakeRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        sleepRoutine = null;
        WakeUpNaturally();
    }

    /// <summary>
    /// Wakes senses immediately, then delays the requested action to simulate getting oriented.
    /// </summary>
    private IEnumerator WakeDelayRoutine(bool noiseDriven)
    {
        float delay = GlobalSettings.Instance != null ? GlobalSettings.Instance.SleepWakeActionDelay : 0f;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        isWakeDelayActive = false;
        sleepRoutine = null;

        if (actorHealth == null || actorHealth.IsDead || actorHealth.IsIncapacitated)
            yield break;

        if (movementController != null && movementController.CurrentState == EnemyState.Sleeping)
            movementController.ResumeAfterSleeping();

        if (noiseDriven)
        {
            hearing?.CompleteDelayedWakeNoiseReaction(pendingWakeNoise, pendingWakeHeardValue, pendingWakeExtremeNoise);
            yield break;
        }

    }

    /// <summary>
    /// Leaves sleep state and starts the post-wake action delay.
    /// </summary>
    private void BeginWake(bool noiseDriven)
    {
        StopSleepRoutine();
        isSleeping = false;
        isWakeDelayActive = true;
        RestoreForcedSleepHearing();
        ApplySleepingSenses(false);
        sleepRoutine = StartCoroutine(WakeDelayRoutine(noiseDriven));
    }

    /// <summary>
    /// Applies or clears the sense gates required while the enemy is sleeping.
    /// </summary>
    private void ApplySleepingSenses(bool sleeping)
    {
        actorHealth?.SetSleeping(sleeping);
        visionAI?.SetSleepSuppressed(sleeping);

        if (sleeping && currentSleepType == EnemySleepType.ForcedSleep)
            SuppressForcedSleepHearing();
        else if (!sleeping)
            RestoreForcedSleepHearing();
    }

    /// <summary>
    /// Temporarily disables hearing while forced sleep is active.
    /// </summary>
    private void SuppressForcedSleepHearing()
    {
        if (hearing == null || forcedSleepSuppressedHearing)
            return;

        hearingWasEnabledBeforeForcedSleep = hearing.enabled;
        hearing.enabled = false;
        forcedSleepSuppressedHearing = true;
    }

    /// <summary>
    /// Restores hearing to the enabled state it had before forced sleep.
    /// </summary>
    private void RestoreForcedSleepHearing()
    {
        if (hearing == null || !forcedSleepSuppressedHearing)
            return;

        hearing.enabled = hearingWasEnabledBeforeForcedSleep;
        forcedSleepSuppressedHearing = false;
    }

    /// <summary>
    /// Stops any active sleep or wake coroutine before changing state.
    /// </summary>
    private void StopSleepRoutine()
    {
        if (sleepRoutine == null)
            return;

        StopCoroutine(sleepRoutine);
        sleepRoutine = null;
    }

    /// <summary>
    /// Resolves the current noise wake threshold for the configured sleep type.
    /// </summary>
    private float ResolveWakeThreshold(EnemySleepType sleepType)
    {
        return sleepType switch
        {
            EnemySleepType.DeepSleep => deepSleepWakeThreshold,
            EnemySleepType.ForcedSleep => float.PositiveInfinity,
            _ => normalSleepWakeThreshold
        };
    }

    /// <summary>
    /// Resolves the natural auto-wake duration for the configured sleep type.
    /// </summary>
    private float ResolveAutoWakeDelay(EnemySleepType sleepType)
    {
        return sleepType switch
        {
            EnemySleepType.DeepSleep => deepSleepAutoWakeDelay,
            EnemySleepType.ForcedSleep => forcedSleepAutoWakeDelay,
            _ => normalSleepAutoWakeDelay
        };
    }

    /// <summary>
    /// Caches same-object AI and health dependencies used by sleep state.
    /// </summary>
    private void CacheReferences()
    {
        movementController ??= GetComponent<EnemyMovementController>();
        visionAI ??= GetComponent<EnemyVisionAI>();
        hearing ??= GetComponent<AIHearing>();
        actorHealth ??= GetComponent<ActorHealth>();
    }
}
