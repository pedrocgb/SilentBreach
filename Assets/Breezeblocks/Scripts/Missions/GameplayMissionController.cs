using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Gameplay Mission Controller")]
public class GameplayMissionController : MonoBehaviour
{
    [Serializable]
    private sealed class ObjectiveRuntimeState
    {
        public HideoutJobObjectiveDefinition Definition;
        public int CompletedCount;
        public readonly HashSet<int> CountedSourceIds = new();

        public int RequiredCount => Definition != null ? Definition.RequiredCount : 1;
        public bool IsComplete => CompletedCount >= RequiredCount;
        public string DisplayText => Definition != null ? Definition.DisplayText : string.Empty;
    }

    [Serializable]
    private sealed class FailureRuntimeState
    {
        public HideoutJobFailureDefinition Definition;
        public float TimeRemaining;
        public bool Triggered;
    }

    [FoldoutGroup("Player")]
    [SerializeField] private Transform playerRoot;

    [FoldoutGroup("Player")]
    [SerializeField] private Rigidbody2D playerBody;

    [FoldoutGroup("Player")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("Player")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("Player")]
    [SerializeField] private PlayerEquipmentController playerEquipmentController;

    [FoldoutGroup("Player")]
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("Player")]
    [SerializeField] private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("Player")]
    [SerializeField] private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("Player")]
    [SerializeField] private PlayerPickupInteractor playerPickupInteractor;

    [FoldoutGroup("Player")]
    [SerializeField] private ActorHealth playerHealth;

    [FoldoutGroup("Music")]
    [SerializeField] private MissionMusicController missionMusicController;

    [FoldoutGroup("Car Audio")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("Car Audio")]
    [SerializeField] private AudioMixerGroup carLoopMixerGroup;

    [FoldoutGroup("Car Audio/One Shots"), Title("Car Door Open SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet carDoorOpenSfx = new();

    [FoldoutGroup("Car Audio/One Shots"), Title("Car Door Close SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet carDoorCloseSfx = new();

    [FoldoutGroup("Car Audio/One Shots"), Title("Car Start SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet carStartSfx = new();

    [FoldoutGroup("Car Audio/Loops"), Title("Car Engine Loop SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet carEngineLoopSfx = new();

    [FoldoutGroup("Car Audio/Loops"), Title("Car Idle Loop SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet carIdleLoopSfx = new();

    [FoldoutGroup("Car Audio/Loops"), LabelText("Idle Loop Local Offset")]
    [SerializeField] private Vector3 carIdleLoopLocalOffset = new(-0.35f, 0f, 0f);

    [FoldoutGroup("Car Audio/Loops"), LabelText("Engine Loop Local Offset")]
    [SerializeField] private Vector3 carEngineLoopLocalOffset = new(0.35f, 0f, 0f);

    [FoldoutGroup("Car Audio")]
    [SerializeField] private NoiseType carSfxSoundType = NoiseType.Common;

    [FoldoutGroup("Car Audio"), Range(0f, 1f)]
    [SerializeField] private float carLoopSpatialBlend = 1f;

    [FoldoutGroup("Car Audio"), MinValue(0f)]
    [SerializeField] private float carLoopMinDistance = 1.5f;

    [FoldoutGroup("Car Audio"), MinValue(0f)]
    [SerializeField] private float carLoopMaxDistance = 22f;

    [FoldoutGroup("Car Audio")]
    [SerializeField] private AudioRolloffMode carLoopRolloffMode = AudioRolloffMode.Logarithmic;

    [FoldoutGroup("Car Audio"), MinValue(0f)]
    [SerializeField] private float carLoopDopplerLevel = 0f;

    [FoldoutGroup("Car Audio"), Range(0f, 360f)]
    [SerializeField] private float carLoopSpread;

    [FoldoutGroup("Car Audio"), Range(0, 256)]
    [SerializeField] private int carLoopPriority = 96;

    [FoldoutGroup("Job")]
    [SerializeField] private HideoutJobDefinition fallbackMission;

    [FoldoutGroup("Job UI")]
    [SerializeField] private TMP_Text jobNameText;

    [FoldoutGroup("Job UI")]
    [SerializeField] private TMP_Text jobObjectivesText;

    [FoldoutGroup("Job UI")]
    [SerializeField] private TMP_Text jobFailureText;

    [FoldoutGroup("Job UI")]
    [SerializeField] private TMP_Text escapeNowText;

    [FoldoutGroup("Job UI")]
    [SerializeField] private TMP_Text timeLimitText;

    [FoldoutGroup("Job UI"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float timeLimitWarningThresholdSeconds = 30f;

    [FoldoutGroup("Job UI"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float timeLimitMillisecondsThresholdSeconds = 10f;

    [FoldoutGroup("Job UI")]
    [SerializeField] private Color timeLimitWarningColor = new Color(1f, 0.3f, 0.3f, 1f);

    [FoldoutGroup("Job UI"), MinValue(1f)]
    [SerializeField] private float timeLimitWarningPulseScale = 1.08f;

    [FoldoutGroup("Job UI"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float timeLimitWarningPulseDuration = 0.35f;

    [FoldoutGroup("Fade and Screens")]
    [SerializeField] private UiImageFader fadeImageFader;

    [FoldoutGroup("Fade and Screens")]
    [SerializeField] private float screenFadeDuration = 0.6f;

    [FoldoutGroup("Fade and Screens")]
    [SerializeField] private GameObject questFailScreen;

    [FoldoutGroup("Fade and Screens")]
    [SerializeField] private GameObject playerKilledScreen;

    [FoldoutGroup("Fade and Screens")]
    [SerializeField] private GameObject gameWinScreen;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private bool playIntroCinematic = true;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private Transform introCarTransform;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private Animator introCarAnimator;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private Transform introCarSeatPoint;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private Transform introDriveTarget;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private Transform introPlayerExitPoint;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private Transform introPlayerFacingTarget;

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("u/s", true)]
    [SerializeField] private float introDriveSpeed = 8f;

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("u/s^2", true)]
    [SerializeField] private float introDriveAcceleration = 14f;

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("u/s^2", true)]
    [SerializeField] private float introDriveDeceleration = 18f;

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float introPlayerExitDuration = 0.45f;

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float introDoorOpenWait = 0.45f;

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float introDoorCloseWait = 0.45f;

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private string openDoorAnimationState = "OpenDoor";

    [FoldoutGroup("Intro Cinematic")]
    [SerializeField] private string closeDoorAnimationState = "CloseDoor";

    [FoldoutGroup("Intro Cinematic"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<Collider2D> collidersToEnableAfterGameplayStart = new();

    [FoldoutGroup("Intro Cinematic"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> gameObjectsToEnableAfterGameplayStart = new();

    [FoldoutGroup("Escape and Win")]
    [SerializeField] private MissionEscapeTrigger missionEscapeTrigger;

    [FoldoutGroup("Escape and Win")]
    [SerializeField] private Transform outroPlayerEntryPoint;

    [FoldoutGroup("Escape and Win")]
    [SerializeField] private Transform outroCarSeatPoint;

    [FoldoutGroup("Escape and Win")]
    [SerializeField] private Transform outroDriveTarget;

    [FoldoutGroup("Escape and Win"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<Collider2D> carCollidersToDisableWhileBoarding = new();

    [FoldoutGroup("Escape and Win"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float outroPlayerEntryDuration = 0.45f;

    [FoldoutGroup("Escape and Win"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float outroDoorOpenWait = 0.45f;

    [FoldoutGroup("Escape and Win"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float outroDoorCloseWait = 0.45f;

    [FoldoutGroup("Escape and Win"), MinValue(0f), SuffixLabel("u/s", true)]
    [SerializeField] private float outroDriveSpeed = 12f;

    [FoldoutGroup("Escape and Win"), MinValue(0f), SuffixLabel("u/s^2", true)]
    [SerializeField] private float outroDriveAcceleration = 18f;

    [FoldoutGroup("Escape and Win"), MinValue(0f), SuffixLabel("u/s^2", true)]
    [SerializeField] private float outroDriveDeceleration = 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public HideoutJobDefinition CurrentJob => currentJob;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool GameplayStarted => gameplayStarted;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool ObjectivesCompleted => objectivesCompleted;

    private readonly List<ObjectiveRuntimeState> objectiveStates = new();
    private readonly List<FailureRuntimeState> failureStates = new();
    private Sequence escapePromptSequence;
    private Sequence timeLimitWarningSequence;
    private Tween carEngineLoopTween;
    private HideoutJobDefinition currentJob;
    private Color timeLimitDefaultColor = Color.white;
    private bool gameplayStarted;
    private bool missionEnded;
    private bool objectivesCompleted;
    private AudioSource carIdleLoopSource;
    private AudioSource carEngineLoopSource;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        PrepareCarAudio();
        PrepareUiDefaults();
        InitializeJobRuntime();
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, false);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, false);

        if (missionEscapeTrigger != null)
        {
            missionEscapeTrigger.Bind(this);
            missionEscapeTrigger.SetEscapeEnabled(false);
        }
    }

    private void OnEnable()
    {
        MissionRuntimeEvents.ActorKilled += HandleActorKilled;
        MissionRuntimeEvents.ActorIncapacitated += HandleActorIncapacitated;
        MissionRuntimeEvents.ItemPickedUp += HandleItemPickedUp;
        MissionRuntimeEvents.EnemyStateChanged += HandleEnemyStateChanged;

        if (playerHealth != null)
        {
            playerHealth.Died += HandlePlayerDied;
            playerHealth.Incapacitated += HandlePlayerIncapacitated;
        }
    }

    private void Start()
    {
        if (playIntroCinematic && CanPlayIntroCinematic())
            StartCoroutine(PlayIntroRoutine());
        else
            StartGameplay();
    }

    private void Update()
    {
        if (!missionEnded && gameplayStarted)
            UpdateTimeLimitFailures(Time.deltaTime);

        EnsureCarIdleLoopRunning();
        RefreshTimeLimitUi();
    }

    private void OnDisable()
    {
        MissionRuntimeEvents.ActorKilled -= HandleActorKilled;
        MissionRuntimeEvents.ActorIncapacitated -= HandleActorIncapacitated;
        MissionRuntimeEvents.ItemPickedUp -= HandleItemPickedUp;
        MissionRuntimeEvents.EnemyStateChanged -= HandleEnemyStateChanged;

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
            playerHealth.Incapacitated -= HandlePlayerIncapacitated;
        }

        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse(resetScale: false);
        carEngineLoopTween?.Kill();
        carEngineLoopTween = null;
        if (carIdleLoopSource != null)
            carIdleLoopSource.Stop();
        if (carEngineLoopSource != null)
            carEngineLoopSource.Stop();
    }

    public void TryHandleEscapeTrigger(GameObject enteringRoot)
    {
        if (!objectivesCompleted || missionEnded || !IsPlayerRoot(enteringRoot))
            return;

        StartCoroutine(PlayWinRoutine());
    }

    public bool TryHandleMusicTrigger(GameObject enteringRoot, MissionMusicCue cue)
    {
        if (missionEnded || !gameplayStarted || !IsPlayerRoot(enteringRoot) || missionMusicController == null)
            return false;

        switch (cue)
        {
            case MissionMusicCue.Lurking:
                missionMusicController.PlayLurkingMusic();
                return true;

            case MissionMusicCue.Alerted:
                missionMusicController.PlayAlertedMusic();
                return true;

            case MissionMusicCue.GameOver:
                missionMusicController.PlayGameOverMusic();
                return true;

            default:
                return false;
        }
    }

    private void InitializeJobRuntime()
    {
        currentJob = ResolveCurrentJob();
        objectiveStates.Clear();
        failureStates.Clear();

        if (currentJob != null)
        {
            IReadOnlyList<HideoutJobObjectiveDefinition> objectives = currentJob.GameplayObjectives;
            for (int i = 0; i < objectives.Count; i++)
            {
                HideoutJobObjectiveDefinition definition = objectives[i];
                if (definition == null)
                    continue;

                objectiveStates.Add(new ObjectiveRuntimeState { Definition = definition });
            }

            IReadOnlyList<HideoutJobFailureDefinition> failures = currentJob.GameplayFailures;
            for (int i = 0; i < failures.Count; i++)
            {
                HideoutJobFailureDefinition definition = failures[i];
                if (definition == null)
                    continue;

                failureStates.Add(new FailureRuntimeState
                {
                    Definition = definition,
                    TimeRemaining = definition.FailureType == HideoutJobFailureType.TimeLimit ? definition.TimeLimitSeconds : 0f
                });
            }
        }

        objectivesCompleted = objectiveStates.Count == 0;
        RefreshMissionTexts();
        RefreshTimeLimitUi();

        if (objectivesCompleted)
            HandleAllObjectivesCompleted();
    }

    private HideoutJobDefinition ResolveCurrentJob()
    {
        HideoutJobDefinition runtimeJob = HideoutRuntimeSession.CurrentJob;
        HideoutJobDefinition resolvedJob = runtimeJob != null ? runtimeJob : fallbackMission;
        if (resolvedJob != null && resolvedJob != runtimeJob)
            HideoutRuntimeSession.SetCurrentJob(resolvedJob);

        return resolvedJob;
    }

    private void CacheReferences()
    {
        if (playerRoot == null && playerMotor != null)
            playerRoot = playerMotor.transform;

        if (playerRoot == null && playerHealth != null)
            playerRoot = playerHealth.transform;

        if (playerRoot == null && playerWeaponController != null)
            playerRoot = playerWeaponController.transform;

        if (playerRoot == null)
        {
            PlayerTopDownMotor2D foundMotor = FindFirstObjectByType<PlayerTopDownMotor2D>();
            if (foundMotor != null)
                playerRoot = foundMotor.transform;
        }

        if (playerRoot == null)
            return;

        if (playerBody == null)
            playerBody = playerRoot.GetComponent<Rigidbody2D>();

        if (playerMotor == null)
            playerMotor = playerRoot.GetComponent<PlayerTopDownMotor2D>();

        if (playerVisionLight == null)
            playerVisionLight = playerRoot.GetComponentInChildren<PlayerVisionLight>(true);

        if (playerEquipmentController == null)
            playerEquipmentController = playerRoot.GetComponent<PlayerEquipmentController>();

        if (playerWeaponController == null)
            playerWeaponController = playerRoot.GetComponent<PlayerWeaponController>();

        if (playerUtilityController == null)
            playerUtilityController = playerRoot.GetComponent<PlayerUtilityController>();

        if (playerMeleeController == null)
            playerMeleeController = playerRoot.GetComponent<PlayerMeleeController>();

        if (playerPickupInteractor == null)
            playerPickupInteractor = playerRoot.GetComponent<PlayerPickupInteractor>();

        if (playerHealth == null)
            playerHealth = playerRoot.GetComponent<ActorHealth>();

        if (missionMusicController == null)
            missionMusicController = GetComponent<MissionMusicController>();

        if (missionMusicController == null)
            missionMusicController = FindFirstObjectByType<MissionMusicController>();
    }

    private void PrepareUiDefaults()
    {
        if (fadeImageFader != null)
            fadeImageFader.SetAlphaImmediate(0f);

        if (questFailScreen != null)
            questFailScreen.SetActive(false);

        if (playerKilledScreen != null)
            playerKilledScreen.SetActive(false);

        if (gameWinScreen != null)
            gameWinScreen.SetActive(false);

        if (escapeNowText != null)
        {
            escapeNowText.gameObject.SetActive(false);
            SetTextAlpha(escapeNowText, 0f);
            escapeNowText.rectTransform.localScale = Vector3.one;
        }

        if (timeLimitText != null)
        {
            timeLimitDefaultColor = timeLimitText.color;
            timeLimitText.gameObject.SetActive(false);
            timeLimitText.text = string.Empty;
            timeLimitText.color = timeLimitDefaultColor;
            timeLimitText.rectTransform.localScale = Vector3.one;
        }
    }

    private void PrepareCarAudio()
    {
        carDoorOpenSfx ??= new AudioClipSet();
        carDoorOpenSfx.Validate();
        carDoorCloseSfx ??= new AudioClipSet();
        carDoorCloseSfx.Validate();
        carStartSfx ??= new AudioClipSet();
        carStartSfx.Validate();
        carEngineLoopSfx ??= new AudioClipSet();
        carEngineLoopSfx.Validate();
        carIdleLoopSfx ??= new AudioClipSet();
        carIdleLoopSfx.Validate();

        carLoopSpatialBlend = Mathf.Clamp01(carLoopSpatialBlend);
        carLoopMinDistance = Mathf.Max(0f, carLoopMinDistance);
        carLoopMaxDistance = Mathf.Max(carLoopMinDistance, carLoopMaxDistance);
        carLoopDopplerLevel = Mathf.Max(0f, carLoopDopplerLevel);
        carLoopSpread = Mathf.Clamp(carLoopSpread, 0f, 360f);
        carLoopPriority = Mathf.Clamp(carLoopPriority, 0, 256);

        if (introCarTransform == null)
            return;

        carIdleLoopSource = EnsureCarLoopSource(carIdleLoopSource, "Car Idle Loop Source", carIdleLoopLocalOffset);
        carEngineLoopSource = EnsureCarLoopSource(carEngineLoopSource, "Car Engine Loop Source", carEngineLoopLocalOffset);
        EnsureCarIdleLoopRunning();
    }

    private bool CanPlayIntroCinematic()
    {
        return introCarTransform != null &&
               introDriveTarget != null &&
               introPlayerExitPoint != null;
    }

    private IEnumerator PlayIntroRoutine()
    {
        BlockPlayerControls(true);
        AttachPlayerToPoint(introCarSeatPoint != null ? introCarSeatPoint : introCarTransform, parentToSeat: true);

        yield return DriveCarToPoint(introCarTransform, introDriveTarget, introDriveSpeed, introDriveAcceleration, introDriveDeceleration, startAtCruiseSpeed: true);

        PlayCarAnimation(openDoorAnimationState);
        PlayCarDoorOpenSfx();
        if (introDoorOpenWait > 0f)
            yield return new WaitForSecondsRealtime(introDoorOpenWait);

        if (playerRoot != null)
            playerRoot.SetParent(null, true);

        yield return MovePlayerToPoint(introPlayerExitPoint, introPlayerFacingTarget, introPlayerExitDuration);

        PlayCarAnimation(closeDoorAnimationState);
        PlayCarDoorCloseSfx();
        if (introDoorCloseWait > 0f)
            yield return new WaitForSecondsRealtime(introDoorCloseWait);

        StartGameplay();
    }

    private void StartGameplay()
    {
        gameplayStarted = true;
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, true);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, true);
        BlockPlayerControls(false);
        RefreshTimeLimitUi();

        if (objectivesCompleted)
            HandleAllObjectivesCompleted();
    }

    private IEnumerator PlayWinRoutine()
    {
        if (missionEnded)
            yield break;

        missionEnded = true;
        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse();
        BlockPlayerControls(true);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, false);

        if (introCarTransform == null)
        {
            yield return FadeAndShowScreen(gameWinScreen);
            yield break;
        }

        if (missionEscapeTrigger != null)
            missionEscapeTrigger.SetEscapeEnabled(false);

        PlayCarAnimation(openDoorAnimationState);
        PlayCarDoorOpenSfx();
        if (outroDoorOpenWait > 0f)
            yield return new WaitForSecondsRealtime(outroDoorOpenWait);

        SetCollidersEnabled(carCollidersToDisableWhileBoarding, false);
        Transform boardingSeatTarget = outroCarSeatPoint != null
            ? outroCarSeatPoint
            : outroPlayerEntryPoint != null ? outroPlayerEntryPoint : introCarTransform;
        yield return MovePlayerToPoint(boardingSeatTarget, boardingSeatTarget, outroPlayerEntryDuration);
        AttachPlayerToPoint(outroCarSeatPoint != null ? outroCarSeatPoint : introCarTransform, parentToSeat: true);
        float carStartDuration = PlayCarStartSfx();
        float carStartSfxEndTime = carStartDuration > 0f ? Time.unscaledTime + carStartDuration : float.NegativeInfinity;
        ClampPlayerRotationToZero();
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, false);

        PlayCarAnimation(closeDoorAnimationState);
        PlayCarDoorCloseSfx();
        if (outroDoorCloseWait > 0f)
            yield return new WaitForSecondsRealtime(outroDoorCloseWait);

        float remainingCarStartWait = carStartSfxEndTime - Time.unscaledTime;
        if (remainingCarStartWait > 0f)
            yield return new WaitForSecondsRealtime(remainingCarStartWait);

        if (outroDriveTarget != null)
            yield return DriveCarToPoint(introCarTransform, outroDriveTarget, outroDriveSpeed, outroDriveAcceleration, outroDriveDeceleration, startAtCruiseSpeed: true);

        Tween fadeTween = fadeImageFader != null ? fadeImageFader.FadeIn(screenFadeDuration) : null;
        if (fadeTween != null)
            yield return fadeTween.WaitForCompletion();

        if (gameWinScreen != null)
            gameWinScreen.SetActive(true);
    }

    private IEnumerator FadeAndShowScreen(GameObject screen)
    {
        Tween fadeTween = fadeImageFader != null ? fadeImageFader.FadeIn(screenFadeDuration) : null;
        if (fadeTween != null)
            yield return fadeTween.WaitForCompletion();

        if (screen != null)
            screen.SetActive(true);
    }

    private IEnumerator MovePlayerToPoint(Transform targetPoint, Transform facingTarget, float duration)
    {
        if (playerRoot == null || targetPoint == null)
            yield break;

        Tween moveTween = playerRoot.DOMove(targetPoint.position, Mathf.Max(0f, duration))
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnUpdate(() => ForcePlayerFacing(facingTarget != null ? facingTarget.position : targetPoint.position));

        yield return moveTween.WaitForCompletion();
        ForcePlayerFacing(facingTarget != null ? facingTarget.position : targetPoint.position);
    }

    private IEnumerator DriveCarToPoint(Transform carTransform, Transform targetPoint, float driveSpeed, float acceleration, float deceleration, bool startAtCruiseSpeed)
    {
        if (carTransform == null || targetPoint == null)
            yield break;

        Vector2 startPosition = carTransform.position;
        Vector2 targetPosition = targetPoint.position;
        Vector2 path = targetPosition - startPosition;
        float totalDistance = path.magnitude;
        if (totalDistance <= 0.0001f)
        {
            carTransform.position = targetPosition;
            yield break;
        }

        float maxSpeed = Mathf.Max(0f, driveSpeed);
        if (maxSpeed <= 0.0001f)
        {
            carTransform.position = targetPosition;
            yield break;
        }

        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);

        Vector2 direction = path / totalDistance;
        float currentSpeed = startAtCruiseSpeed ? maxSpeed : 0f;
        SetCarEngineLoopActive(true);

        while (true)
        {
            Vector2 currentPosition = carTransform.position;
            float traveledDistance = Vector2.Dot(currentPosition - startPosition, direction);
            float clampedTraveledDistance = Mathf.Max(0f, traveledDistance);
            float remainingDistance = totalDistance - clampedTraveledDistance;
            if (remainingDistance <= 0f)
                break;

            float desiredSpeed = maxSpeed;
            if (deceleration > 0f)
            {
                float brakingDistance = (currentSpeed * currentSpeed) / (2f * deceleration);
                if (remainingDistance <= brakingDistance)
                    desiredSpeed = Mathf.Sqrt(Mathf.Max(0f, 2f * deceleration * remainingDistance));
            }

            if (currentSpeed < desiredSpeed)
            {
                currentSpeed = acceleration > 0f
                    ? Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.unscaledDeltaTime)
                    : desiredSpeed;
            }
            else
            {
                currentSpeed = deceleration > 0f
                    ? Mathf.MoveTowards(currentSpeed, desiredSpeed, deceleration * Time.unscaledDeltaTime)
                    : desiredSpeed;
            }

            float frameDistance = currentSpeed * Time.unscaledDeltaTime;
            if (frameDistance <= 0f)
            {
                carTransform.position = targetPosition;
                break;
            }

            float nextDistance = clampedTraveledDistance + frameDistance;
            if (nextDistance >= totalDistance)
            {
                carTransform.position = targetPosition;
                break;
            }

            carTransform.position = startPosition + direction * nextDistance;
            yield return null;
        }

        carTransform.position = targetPosition;
        SetCarEngineLoopActive(false);
    }

    private void AttachPlayerToPoint(Transform targetPoint, bool parentToSeat)
    {
        if (playerRoot == null || targetPoint == null)
            return;

        if (parentToSeat)
            playerRoot.SetParent(targetPoint, false);
        else
            playerRoot.SetParent(null, true);

        playerRoot.position = targetPoint.position;
    }

    private void ClampPlayerRotationToZero()
    {
        if (playerRoot != null)
            playerRoot.rotation = Quaternion.identity;

        if (playerBody != null)
            playerBody.rotation = 0f;
    }

    private void ForcePlayerFacing(Vector3 worldTarget)
    {
        if (playerVisionLight == null || playerRoot == null)
            return;

        Vector2 direction = (Vector2)(worldTarget - playerRoot.position);
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        playerVisionLight.ApplyExternalDirection(direction.normalized, playerVisionLight.RotationSmoothing, Time.unscaledDeltaTime);
    }

    private void PlayCarAnimation(string stateName)
    {
        if (introCarAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        introCarAnimator.Play(stateName, 0, 0f);
    }

    private void PlayCarDoorOpenSfx()
    {
        PlayCarOneShot(carDoorOpenSfx);
    }

    private void PlayCarDoorCloseSfx()
    {
        PlayCarOneShot(carDoorCloseSfx);
    }

    private float PlayCarStartSfx()
    {
        return PlayCarOneShot(carStartSfx);
    }

    private float PlayCarOneShot(AudioClipSet clipSet)
    {
        if (introCarTransform == null || clipSet == null || !clipSet.HasAnyClip)
            return 0f;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return 0f;

        bool played = worldSfxManager.PlayClipSetAt(introCarTransform.position, clipSet, carSfxSoundType, out float playbackDuration);
        return played ? playbackDuration : 0f;
    }

    private void StartCarIdleLoopIfNeeded()
    {
        if (carIdleLoopSource == null || carIdleLoopSource.isPlaying || carIdleLoopSfx == null || !carIdleLoopSfx.HasAnyClip)
            return;

        PlayLoopClipSet(carIdleLoopSource, carIdleLoopSfx, startVolume: true);
    }

    private void EnsureCarIdleLoopRunning()
    {
        if (introCarTransform == null || carIdleLoopSfx == null || !carIdleLoopSfx.HasAnyClip)
            return;

        carIdleLoopSource = EnsureCarLoopSource(carIdleLoopSource, "Car Idle Loop Source", carIdleLoopLocalOffset);
        if (carIdleLoopSource == null)
            return;

        if (!carIdleLoopSource.isPlaying || carIdleLoopSource.clip == null)
        {
            StartCarIdleLoopIfNeeded();
            return;
        }

        carIdleLoopSource.volume = Mathf.Clamp01(carIdleLoopSfx.Volume);
    }

    private void SetCarEngineLoopActive(bool active)
    {
        if (introCarTransform == null)
            return;

        EnsureCarIdleLoopRunning();
        carEngineLoopSource = EnsureCarLoopSource(carEngineLoopSource, "Car Engine Loop Source", carEngineLoopLocalOffset);
        if (carEngineLoopSource == null)
            return;

        carEngineLoopTween?.Kill();
        carEngineLoopTween = null;

        if (active)
        {
            if (!carEngineLoopSource.isPlaying)
                PlayLoopClipSet(carEngineLoopSource, carEngineLoopSfx, startVolume: false);

            if (carEngineLoopSource.clip == null)
                return;

            float targetVolume = carEngineLoopSfx != null ? Mathf.Clamp01(carEngineLoopSfx.Volume) : 0f;
            carEngineLoopTween = DOTween.To(
                    () => carEngineLoopSource.volume,
                    value => carEngineLoopSource.volume = Mathf.Clamp01(value),
                    targetVolume,
                    0.2f)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
            return;
        }

        if (!carEngineLoopSource.isPlaying)
            return;

        carEngineLoopTween = DOTween.To(
                () => carEngineLoopSource.volume,
                value => carEngineLoopSource.volume = Mathf.Clamp01(value),
                0f,
                0.2f)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (carEngineLoopSource != null)
                {
                    carEngineLoopSource.Stop();
                    carEngineLoopSource.clip = null;
                }
            });
    }

    private AudioSource EnsureCarLoopSource(AudioSource existingSource, string objectName, Vector3 localOffset)
    {
        if (introCarTransform == null)
            return null;

        AudioSource resolvedSource = existingSource;
        if (resolvedSource == null)
        {
            Transform existingChild = introCarTransform.Find(objectName);
            if (existingChild != null)
                resolvedSource = existingChild.GetComponent<AudioSource>();
        }

        if (resolvedSource == null)
        {
            GameObject sourceObject = new(objectName);
            sourceObject.transform.SetParent(introCarTransform, false);
            resolvedSource = sourceObject.AddComponent<AudioSource>();
        }

        ConfigureCarLoopSource(resolvedSource, localOffset);
        return resolvedSource;
    }

    private void ConfigureCarLoopSource(AudioSource source, Vector3 localOffset)
    {
        if (source == null)
            return;

        source.transform.localPosition = localOffset;
        source.playOnAwake = false;
        source.loop = true;
        source.outputAudioMixerGroup = carLoopMixerGroup;
        source.spatialBlend = carLoopSpatialBlend;
        source.minDistance = carLoopMinDistance;
        source.maxDistance = carLoopMaxDistance;
        source.rolloffMode = carLoopRolloffMode;
        source.dopplerLevel = carLoopDopplerLevel;
        source.spread = carLoopSpread;
        source.priority = carLoopPriority;
    }

    private static void PlayLoopClipSet(AudioSource source, AudioClipSet clipSet, bool startVolume)
    {
        if (source == null || clipSet == null || !clipSet.HasAnyClip)
            return;

        AudioClip clip = clipSet.GetRandomClip();
        if (clip == null)
            return;

        source.clip = clip;
        source.pitch = clipSet.GetRandomPitch();
        source.volume = startVolume ? Mathf.Clamp01(clipSet.Volume) : 0f;
        source.loop = true;
        source.Play();
    }

    private void ResolveWorldSfxManager()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;
    }

    private void BlockPlayerControls(bool blocked)
    {
        if (playerEquipmentController != null)
        {
            playerEquipmentController.SetInputBlocked(blocked);
            if (blocked)
                playerEquipmentController.SetEquipmentPanelVisible(false);
        }

        playerMotor?.SetInputBlocked(blocked);
        playerVisionLight?.SetInputBlocked(blocked);
        playerWeaponController?.SetInputBlocked(blocked);
        playerUtilityController?.SetInputBlocked(blocked);
        playerMeleeController?.SetInputBlocked(blocked);
        playerPickupInteractor?.SetInputBlocked(blocked);

        if (blocked && playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        if (!blocked && playerVisionLight != null)
            playerVisionLight.DriveMouseLook(playerVisionLight.RotationSmoothing, 0f);
    }

    private void HandleActorKilled(MissionActorEvent actorEvent)
    {
        if (missionEnded)
            return;

        RegisterObjectiveProgress(actorEvent, HideoutJobObjectiveType.KillTarget);

        if (IsPlayerInstigator(actorEvent.InstigatorRoot))
            EvaluateFailureRulesForActorEvent(actorEvent, harmed: true, killed: true);
    }

    private void HandleActorIncapacitated(MissionActorEvent actorEvent)
    {
        if (missionEnded)
            return;

        RegisterObjectiveProgress(actorEvent, HideoutJobObjectiveType.IncapacitateTarget);

        if (IsPlayerInstigator(actorEvent.InstigatorRoot))
            EvaluateFailureRulesForActorEvent(actorEvent, harmed: true, killed: false);
    }

    private void HandleItemPickedUp(MissionPickupEvent pickupEvent)
    {
        if (missionEnded || !IsPlayerRoot(pickupEvent.PickerRoot))
            return;

        if (pickupEvent.PickableItem == null)
            return;

        int sourceId = pickupEvent.PickableItem.GetInstanceID();
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null ||
                state.Definition == null ||
                state.Definition.ObjectiveType != HideoutJobObjectiveType.RetrieveItem ||
                state.IsComplete ||
                state.CountedSourceIds.Contains(sourceId) ||
                !MatchesReferenceId(state.Definition.ReferenceId, pickupEvent.ItemId))
            {
                continue;
            }

            state.CountedSourceIds.Add(sourceId);
            state.CompletedCount = Mathf.Min(state.CompletedCount + 1, state.RequiredCount);
        }

        RefreshObjectivesAndEscapeState();
    }

    private void HandleEnemyStateChanged(EnemyStateChangedEvent stateEvent)
    {
        if (missionEnded || !gameplayStarted)
            return;

        if (stateEvent.NewState == EnemyState.Detected && stateEvent.PreviousState != EnemyState.Detected)
            missionMusicController?.PlayAlertedMusic();

        if (stateEvent.NewState != EnemyState.Alert || stateEvent.PreviousState == EnemyState.Alert)
            return;

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState failureState = failureStates[i];
            if (failureState == null || failureState.Triggered || failureState.Definition == null)
                continue;

            if (failureState.Definition.FailureType != HideoutJobFailureType.DontBeDetected)
                continue;

            failureState.Triggered = true;
            StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: false));
            return;
        }
    }

    private void HandlePlayerDied(ActorDamageContext context)
    {
        if (missionEnded)
            return;

        StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: true));
    }

    private void HandlePlayerIncapacitated(ActorDamageContext context)
    {
        if (missionEnded)
            return;

        StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: true));
    }

    private void RegisterObjectiveProgress(MissionActorEvent actorEvent, HideoutJobObjectiveType objectiveType)
    {
        int sourceId = actorEvent.ActorHealth != null ? actorEvent.ActorHealth.GetInstanceID() : 0;
        string actorId = actorEvent.Identity != null ? actorEvent.Identity.ActorId : string.Empty;

        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null ||
                state.Definition == null ||
                state.Definition.ObjectiveType != objectiveType ||
                state.IsComplete ||
                (sourceId != 0 && state.CountedSourceIds.Contains(sourceId)) ||
                !MatchesReferenceId(state.Definition.ReferenceId, actorId))
            {
                continue;
            }

            if (sourceId != 0)
                state.CountedSourceIds.Add(sourceId);

            state.CompletedCount = Mathf.Min(state.CompletedCount + 1, state.RequiredCount);
        }

        RefreshObjectivesAndEscapeState();
    }

    private void EvaluateFailureRulesForActorEvent(MissionActorEvent actorEvent, bool harmed, bool killed)
    {
        if (actorEvent.ActorHealth == null || actorEvent.ActorHealth == playerHealth)
            return;

        MissionActorIdentity identity = actorEvent.Identity;
        bool isInnocent = identity != null && identity.IsInnocent;

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState failureState = failureStates[i];
            if (failureState == null || failureState.Triggered || failureState.Definition == null)
                continue;

            bool shouldTrigger = failureState.Definition.FailureType switch
            {
                HideoutJobFailureType.DontHarmInnocent => harmed && isInnocent,
                HideoutJobFailureType.DontKillInnocent => killed && isInnocent,
                HideoutJobFailureType.DontHarmAnyone => harmed,
                HideoutJobFailureType.DontKillAnyone => killed,
                _ => false
            };

            if (!shouldTrigger)
                continue;

            failureState.Triggered = true;
            StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: false));
            return;
        }
    }

    private void RefreshObjectivesAndEscapeState()
    {
        if (missionEnded)
            return;

        bool wereObjectivesCompleted = objectivesCompleted;
        objectivesCompleted = AreAllObjectivesComplete();
        RefreshMissionTexts();

        if (!wereObjectivesCompleted && objectivesCompleted)
            HandleAllObjectivesCompleted();
    }

    private bool AreAllObjectivesComplete()
    {
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            if (!objectiveStates[i].IsComplete)
                return false;
        }

        return true;
    }

    private void HandleAllObjectivesCompleted()
    {
        if (!objectivesCompleted || !gameplayStarted)
            return;

        if (missionEscapeTrigger != null)
            missionEscapeTrigger.SetEscapeEnabled(true);

        if (escapeNowText == null)
            return;

        escapeNowText.gameObject.SetActive(true);
        escapePromptSequence?.Kill();
        SetTextAlpha(escapeNowText, 1f);
        escapeNowText.rectTransform.localScale = Vector3.one;
        escapePromptSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(escapeNowText.rectTransform.DOScale(1.06f, 0.55f).SetEase(Ease.InOutSine))
            .Join(DOTween.ToAlpha(() => escapeNowText.color, color => escapeNowText.color = color, 0.45f, 0.55f))
            .Append(escapeNowText.rectTransform.DOScale(1f, 0.55f).SetEase(Ease.InOutSine))
            .Join(DOTween.ToAlpha(() => escapeNowText.color, color => escapeNowText.color = color, 1f, 0.55f))
            .SetLoops(-1, LoopType.Restart);
    }

    private void RefreshMissionTexts()
    {
        if (jobNameText != null)
            jobNameText.text = currentJob != null ? currentJob.JobTitle : string.Empty;

        if (jobObjectivesText != null)
            jobObjectivesText.text = BuildObjectiveText();

        if (jobFailureText != null)
            jobFailureText.text = BuildFailureText();
    }

    private string BuildObjectiveText()
    {
        if (objectiveStates.Count == 0)
            return currentJob != null ? currentJob.ObjectivesText : string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null || state.Definition == null)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            string line = state.DisplayText;
            if (state.RequiredCount > 1)
                line = $"{line} ({Mathf.Min(state.CompletedCount, state.RequiredCount)}/{state.RequiredCount})";

            if (state.IsComplete)
                line = $"<s>{line}</s>";

            builder.Append("- ");
            builder.Append(line);
        }

        return builder.ToString();
    }

    private string BuildFailureText()
    {
        if (failureStates.Count == 0)
            return currentJob != null ? currentJob.TermsOfFailureText : string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Definition == null)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append("- ");
            builder.Append(state.Definition.DisplayText);
        }

        return builder.ToString();
    }

    private void UpdateTimeLimitFailures(float deltaTime)
    {
        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Triggered || state.Definition == null || state.Definition.FailureType != HideoutJobFailureType.TimeLimit)
                continue;

            state.TimeRemaining = Mathf.Max(0f, state.TimeRemaining - deltaTime);
            if (state.TimeRemaining > 0f)
                continue;

            state.Triggered = true;
            StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: false));
            return;
        }
    }

    private void RefreshTimeLimitUi()
    {
        if (timeLimitText == null)
            return;

        FailureRuntimeState activeTimeLimit = GetActiveTimeLimitFailure();
        if (activeTimeLimit == null)
        {
            timeLimitText.gameObject.SetActive(false);
            timeLimitText.text = string.Empty;
            timeLimitText.color = timeLimitDefaultColor;
            StopTimeLimitWarningPulse();
            return;
        }

        timeLimitText.gameObject.SetActive(true);
        float remainingTime = Mathf.Max(0f, activeTimeLimit.TimeRemaining);
        timeLimitText.text = FormatTimeLimitText(remainingTime);

        bool useWarningVisuals = remainingTime <= Mathf.Max(0f, timeLimitWarningThresholdSeconds);
        if (useWarningVisuals)
        {
            timeLimitText.color = timeLimitWarningColor;
            StartTimeLimitWarningPulse();
        }
        else
        {
            timeLimitText.color = timeLimitDefaultColor;
            StopTimeLimitWarningPulse();
        }
    }

    private FailureRuntimeState GetActiveTimeLimitFailure()
    {
        FailureRuntimeState activeState = null;
        float lowestRemainingTime = float.PositiveInfinity;

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Definition == null || state.Definition.FailureType != HideoutJobFailureType.TimeLimit || state.Triggered)
                continue;

            if (state.TimeRemaining >= lowestRemainingTime)
                continue;

            lowestRemainingTime = state.TimeRemaining;
            activeState = state;
        }

        return activeState;
    }

    private string FormatTimeLimitText(float remainingTime)
    {
        remainingTime = Mathf.Max(0f, remainingTime);
        bool showMilliseconds = remainingTime <= Mathf.Max(0f, timeLimitMillisecondsThresholdSeconds);
        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime) % 60;

        if (!showMilliseconds)
            return $"{minutes:00}:{seconds:00}";

        int milliseconds = Mathf.Clamp(Mathf.FloorToInt((remainingTime - Mathf.Floor(remainingTime)) * 1000f), 0, 999);

        return $"{minutes:00}:{seconds:00}:{milliseconds:000}";
    }

    private void StartTimeLimitWarningPulse()
    {
        if (timeLimitText == null || timeLimitWarningSequence != null)
            return;

        timeLimitText.rectTransform.localScale = Vector3.one;
        timeLimitWarningSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(timeLimitText.rectTransform.DOScale(timeLimitWarningPulseScale, timeLimitWarningPulseDuration).SetEase(Ease.InOutSine))
            .Append(timeLimitText.rectTransform.DOScale(1f, timeLimitWarningPulseDuration).SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Restart);
    }

    private void StopTimeLimitWarningPulse(bool resetScale = true)
    {
        timeLimitWarningSequence?.Kill();
        timeLimitWarningSequence = null;

        if (resetScale && timeLimitText != null)
            timeLimitText.rectTransform.localScale = Vector3.one;
    }

    private IEnumerator HandleMissionFailedRoutine(bool playerWasKilled)
    {
        if (missionEnded)
            yield break;

        missionEnded = true;
        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse();
        BlockPlayerControls(true);
        missionMusicController?.PlayGameOverMusic();

        yield return FadeAndShowScreen(playerWasKilled ? playerKilledScreen : questFailScreen);
    }

    private bool IsPlayerInstigator(GameObject instigatorRoot)
    {
        return IsPlayerRoot(instigatorRoot);
    }

    private bool IsPlayerRoot(GameObject candidateRoot)
    {
        return candidateRoot != null &&
               playerRoot != null &&
               candidateRoot.transform.root == playerRoot.root;
    }

    private static bool MatchesReferenceId(string expectedId, string actualId)
    {
        if (string.IsNullOrWhiteSpace(expectedId))
            return true;

        if (string.IsNullOrWhiteSpace(actualId))
            return false;

        return string.Equals(expectedId.Trim(), actualId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void SetCollidersEnabled(IReadOnlyList<Collider2D> colliders, bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    private static void SetGameObjectsActive(IReadOnlyList<GameObject> gameObjects, bool active)
    {
        if (gameObjects == null)
            return;

        for (int i = 0; i < gameObjects.Count; i++)
        {
            if (gameObjects[i] != null)
                gameObjects[i].SetActive(active);
        }
    }

    private static void SetTextAlpha(TMP_Text textField, float alpha)
    {
        if (textField == null)
            return;

        Color color = textField.color;
        color.a = Mathf.Clamp01(alpha);
        textField.color = color;
    }
}

}
