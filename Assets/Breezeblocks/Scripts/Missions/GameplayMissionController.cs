using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Breezeblocks;
using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Rewired;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
        public MissionStatusEntryUI EntryView;
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
        public MissionStatusEntryUI EntryView;
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
    [SerializeField] private PlayerFocusController playerFocusController;

    [FoldoutGroup("Player")]
    [SerializeField] private ActorHealth playerHealth;

    [FoldoutGroup("Music")]
    [SerializeField] private MissionMusicController missionMusicController;

    [FoldoutGroup("UI")]
    [SerializeField] private GameplayHudController gameplayHudController;

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

    [FoldoutGroup("Job UI")]
    [SerializeField] GameObject timerContent;

        [FoldoutGroup("Job UI/List"), SerializeField]
    private RectTransform missionStatusContentRoot;

    [FoldoutGroup("Job UI/List"), AssetsOnly]
    [SerializeField] private MissionStatusEntryUI objectiveStatusEntryPrefab;

    [FoldoutGroup("Job UI/List"), AssetsOnly]
    [SerializeField] private MissionStatusEntryUI failureStatusEntryPrefab;

    [FoldoutGroup("Job UI/List"), SerializeField, MinValue(0f), SuffixLabel("s", true)]
    private float missionStatusEntryFadeDuration = 0.2f;

    [FoldoutGroup("Job UI/List"), SerializeField, MinValue(0f), SuffixLabel("s", true)]
    private float missionStatusEntrySpawnInterval = 0.2f;

    [FoldoutGroup("Job UI/List")]
    [SerializeField] private GlobalObjectPooler globalObjectPooler;

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

    [FoldoutGroup("Fade and Screens/Failure")]
    [SerializeField] private TMP_Text questFailMessageText;

    [FoldoutGroup("Fade and Screens/Failure")]
    [SerializeField] private Button questFailRetryButton;

    [FoldoutGroup("Fade and Screens/Failure")]
    [SerializeField] private Button questFailQuitButton;

    [FoldoutGroup("Fade and Screens")]
    [SerializeField] private GameObject playerKilledScreen;

    [FoldoutGroup("Fade and Screens/Death")]
    [SerializeField] private TMP_Text playerKilledMessageText;

    [FoldoutGroup("Fade and Screens/Death"), TextArea(2, 4)]
    [SerializeField] private string playerKilledMessage = "Game Over.";

    [FoldoutGroup("Fade and Screens/Death")]
    [SerializeField] private Button playerKilledRetryButton;

    [FoldoutGroup("Fade and Screens/Death")]
    [SerializeField] private Button playerKilledQuitButton;

    [FoldoutGroup("Fade and Screens")]
    [SerializeField] private GameObject gameWinScreen;

    [FoldoutGroup("Fade and Screens/Win")]
    [SerializeField] private TMP_Text gameWinMessageText;

    [FoldoutGroup("Fade and Screens/Win"), TextArea(2, 4)]
    [SerializeField] private string missionCompletedMessage = "Mission Complete.";

    [FoldoutGroup("Fade and Screens/Win")]
    [SerializeField] private Button gameWinContinueButton;

    [FoldoutGroup("Scene Loading"), LabelText("Hideout Scene Build Index"), MinValue(-1)]
    [SerializeField] private int hideoutSceneBuildIndex = 0;

    [FoldoutGroup("Scene Loading"), LabelText("Hideout Scene Fallback Name")]
    [FormerlySerializedAs("hideoutScenePath")]
    [SerializeField] private string hideoutSceneName = "Hideout";

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

    [FoldoutGroup("Intro Cinematic/Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Intro Cinematic/Rewired")]
    [SerializeField] private string skipIntroAction = "SkipIntro";

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float introSkipBlackHoldDuration = 0.5f;

    [FoldoutGroup("Intro Cinematic"), LabelText("Initial Player Facing"), SuffixLabel("deg", true)]
    [SerializeField] private float introInitialPlayerFacingDegrees;

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

    [FoldoutGroup("Escape and Win"), LabelText("Win Cinematic Facing"), SuffixLabel("deg", true)]
    [SerializeField] private float winCinematicFacingDegrees;

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
    private Tween activeCinematicPlayerMoveTween;
    private Coroutine missionStatusEntryBuildRoutine;
    private HideoutJobDefinition currentJob;
    private Color timeLimitDefaultColor = Color.white;
    private bool gameplayStarted;
    private bool missionEnded;
    private bool objectivesCompleted;
    private AudioSource carIdleLoopSource;
    private AudioSource carEngineLoopSource;
    private float carIdleLoopBaseVolume;
    private float carEngineLoopBaseVolume;
    private float carAudioExternalVolumeMultiplier = 1f;
    private Player rewiredPlayer;
    private Coroutine introRoutine;
    private Coroutine introSkipRoutine;
    private Coroutine continuousCarDriveRoutine;
    private bool playerVisionLightDefaultEnabled = true;
    private bool playerFocusControllerDefaultEnabled = true;
    private bool playerComponentDefaultStatesCached;
    private bool sceneTransitionInProgress;
    private bool suppressCarAudioAutoRestart;
    private readonly List<MissionStatusEntryUI> activeMissionStatusEntries = new();

    private bool UseMissionStatusEntryList =>
        missionStatusContentRoot != null &&
        objectiveStatusEntryPrefab != null &&
        failureStatusEntryPrefab != null;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ResetSceneScopedRuntimeState();
        GameplayConsoleController.EnsureOn(gameObject);
        CachePlayerComponentDefaultStates();
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

        if (playIntroCinematic && CanPlayIntroCinematic())
        {
            BlockPlayerControls(true);
            SetIntroVisionLightActive(false);
            ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);
        }
    }

    private void OnEnable()
    {
        MissionRuntimeEvents.ActorKilled += HandleActorKilled;
        MissionRuntimeEvents.ActorIncapacitated += HandleActorIncapacitated;
        MissionRuntimeEvents.ItemPickedUp += HandleItemPickedUp;
        MissionRuntimeEvents.EnemyStateChanged += HandleEnemyStateChanged;
        MissionRuntimeEvents.EnemyPlayerFullyDetected += HandleEnemyPlayerFullyDetected;
        RegisterScreenButtonCallbacks();

        if (playerHealth != null)
        {
            playerHealth.Died += HandlePlayerDied;
            playerHealth.Incapacitated += HandlePlayerIncapacitated;
        }
    }

    private void Start()
    {
        if (playIntroCinematic && CanPlayIntroCinematic())
        {
            SetIntroVisionLightActive(false);
            ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);
            introRoutine = StartCoroutine(PlayIntroRoutine());
        }
        else
            StartGameplay();

        RestartMissionStatusEntryBuild();
    }

    private void Update()
    {
        if (introRoutine != null && introSkipRoutine == null)
            TryHandleIntroSkipInput();

        if (!missionEnded && gameplayStarted)
            UpdateTimeLimitFailures(Time.deltaTime);

        EnsureCarIdleLoopRunning();
        ApplyCarLoopVolumes();
        RefreshTimeLimitUi();
    }

    private void OnDisable()
    {
        MissionRuntimeEvents.ActorKilled -= HandleActorKilled;
        MissionRuntimeEvents.ActorIncapacitated -= HandleActorIncapacitated;
        MissionRuntimeEvents.ItemPickedUp -= HandleItemPickedUp;
        MissionRuntimeEvents.EnemyStateChanged -= HandleEnemyStateChanged;
        MissionRuntimeEvents.EnemyPlayerFullyDetected -= HandleEnemyPlayerFullyDetected;
        UnregisterScreenButtonCallbacks();

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
        activeCinematicPlayerMoveTween?.Kill();
        activeCinematicPlayerMoveTween = null;
        if (missionStatusEntryBuildRoutine != null)
        {
            StopCoroutine(missionStatusEntryBuildRoutine);
            missionStatusEntryBuildRoutine = null;
        }
        StopContinuousCarDrive();
        StopAllCarAudio(suppressAutoRestart: false);
        ClearMissionStatusEntries();

        if (playerVisionLight != null)
            playerVisionLight.enabled = playerVisionLightDefaultEnabled;

        if (playerFocusController != null)
            playerFocusController.enabled = playerFocusControllerDefaultEnabled;
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

        if (playerFocusController == null)
            playerFocusController = playerRoot.GetComponent<PlayerFocusController>();

        if (playerHealth == null)
            playerHealth = playerRoot.GetComponent<ActorHealth>();

        if (missionMusicController == null)
            missionMusicController = GetComponent<MissionMusicController>();

        if (missionMusicController == null)
            missionMusicController = FindFirstObjectByType<MissionMusicController>();

        if (gameplayHudController == null)
            gameplayHudController = FindFirstObjectByType<GameplayHudController>();

        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        ResolveRewiredPlayer();
    }

    private void CachePlayerComponentDefaultStates()
    {
        if (playerComponentDefaultStatesCached)
            return;

        playerVisionLightDefaultEnabled = playerVisionLight != null && playerVisionLight.enabled;
        playerFocusControllerDefaultEnabled = playerFocusController != null && playerFocusController.enabled;
        playerComponentDefaultStatesCached = true;
    }

    private void PrepareUiDefaults()
    {
        if (fadeImageFader != null)
            fadeImageFader.SetAlphaImmediate(0f);

        RegisterMissionStatusEntryPrefabs();
        ClearMissionStatusEntries();

        if (questFailScreen != null)
            questFailScreen.SetActive(false);

        if (playerKilledScreen != null)
            playerKilledScreen.SetActive(false);

        if (gameWinScreen != null)
            gameWinScreen.SetActive(false);

        if (questFailMessageText != null)
            questFailMessageText.text = string.Empty;

        if (playerKilledMessageText != null)
            playerKilledMessageText.text = ResolvePlayerKilledMessage();

        if (gameWinMessageText != null)
            gameWinMessageText.text = ResolveMissionCompletedMessage();

        if (escapeNowText != null)
        {
            escapeNowText.gameObject.SetActive(false);
            SetTextAlpha(escapeNowText, 0f);
            escapeNowText.rectTransform.localScale = Vector3.one;
        }

        if (timerContent != null)
        {
            timerContent.SetActive(false);
            timeLimitDefaultColor = timeLimitText.color;
            timeLimitText.text = string.Empty;
            timeLimitText.color = timeLimitDefaultColor;
            timeLimitText.rectTransform.localScale = Vector3.one;
        }

        if (UseMissionStatusEntryList)
        {
            if (jobObjectivesText != null)
                jobObjectivesText.text = string.Empty;

            if (jobFailureText != null)
                jobFailureText.text = string.Empty;
        }
    }

    private void PrepareCarAudio()
    {
        suppressCarAudioAutoRestart = false;
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
        SetIntroVisionLightActive(false);
        ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);
        AttachPlayerToPoint(introCarSeatPoint != null ? introCarSeatPoint : introCarTransform, parentToSeat: true, facingDegrees: introInitialPlayerFacingDegrees);

        yield return DriveCarToPoint(introCarTransform, introDriveTarget, introDriveSpeed, introDriveAcceleration, introDriveDeceleration, startAtCruiseSpeed: true);

        PlayCarAnimation(openDoorAnimationState);
        PlayCarDoorOpenSfx();
        float introDoorOpenPhaseDuration = Mathf.Max(introDoorOpenWait, ResolveCarAnimationDuration(openDoorAnimationState));
        if (playerRoot != null)
            playerRoot.SetParent(null, true);

        Tween exitMoveTween = BeginMovePlayerToPoint(introPlayerExitPoint, introPlayerFacingTarget, introPlayerExitDuration);
        if (introDoorOpenPhaseDuration > 0f)
            yield return new WaitForSecondsRealtime(introDoorOpenPhaseDuration);

        if (exitMoveTween != null)
            yield return exitMoveTween.WaitForCompletion();

        ApplyCinematicPlayerFacing(introPlayerExitPoint, introPlayerFacingTarget, null);

        PlayCarAnimation(closeDoorAnimationState);
        PlayCarDoorCloseSfx();
        if (introDoorCloseWait > 0f)
            yield return new WaitForSecondsRealtime(introDoorCloseWait);

        introRoutine = null;
        StartGameplay();
    }

    private void StartGameplay()
    {
        gameplayStarted = true;
        SetEndScreenPointerVisible(false);
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, true);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, true);
        SetIntroVisionLightActive(true);
        BlockPlayerControls(false);
        gameplayHudController?.HandleGameplayStarted();
        RefreshTimeLimitUi();

        if (objectivesCompleted)
            HandleAllObjectivesCompleted();
    }

    private IEnumerator PlayWinRoutine()
    {
        if (missionEnded)
            yield break;

        missionEnded = true;
        Time.timeScale = 1f;
        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse();
        BlockPlayerControls(true);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, false);

        if (introCarTransform == null)
        {
            if (gameWinMessageText != null)
                gameWinMessageText.text = ResolveMissionCompletedMessage();

            yield return FadeAndShowScreen(gameWinScreen);
            yield break;
        }

        if (missionEscapeTrigger != null)
            missionEscapeTrigger.SetEscapeEnabled(false);

        PlayCarAnimation(openDoorAnimationState);
        PlayCarDoorOpenSfx();
        float outroDoorOpenPhaseDuration = Mathf.Max(outroDoorOpenWait, ResolveCarAnimationDuration(openDoorAnimationState));
        SetCollidersEnabled(carCollidersToDisableWhileBoarding, false);
        Transform boardingSeatTarget = outroCarSeatPoint != null
            ? outroCarSeatPoint
            : outroPlayerEntryPoint != null ? outroPlayerEntryPoint : introCarTransform;
        Tween boardingMoveTween = BeginMovePlayerToPoint(boardingSeatTarget, null, outroPlayerEntryDuration, winCinematicFacingDegrees);
        if (outroDoorOpenPhaseDuration > 0f)
            yield return new WaitForSecondsRealtime(outroDoorOpenPhaseDuration);

        if (boardingMoveTween != null)
            yield return boardingMoveTween.WaitForCompletion();

        ApplyCinematicPlayerFacing(boardingSeatTarget, null, winCinematicFacingDegrees);
        yield return RotatePlayerToFacingDegrees(winCinematicFacingDegrees);
        AttachPlayerToPoint(outroCarSeatPoint != null ? outroCarSeatPoint : introCarTransform, parentToSeat: true, facingDegrees: winCinematicFacingDegrees);
        float carStartDuration = PlayCarStartSfx();
        float carStartSfxEndTime = carStartDuration > 0f ? Time.unscaledTime + carStartDuration : float.NegativeInfinity;
        ApplyPlayerFacingDegrees(winCinematicFacingDegrees);
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, false);

        PlayCarAnimation(closeDoorAnimationState);
        PlayCarDoorCloseSfx();
        if (outroDoorCloseWait > 0f)
            yield return new WaitForSecondsRealtime(outroDoorCloseWait);

        float remainingCarStartWait = carStartSfxEndTime - Time.unscaledTime;
        if (remainingCarStartWait > 0f)
            yield return new WaitForSecondsRealtime(remainingCarStartWait);

        if (outroDriveTarget != null)
            yield return DriveCarToPoint(introCarTransform, outroDriveTarget, outroDriveSpeed, outroDriveAcceleration, outroDriveDeceleration, startAtCruiseSpeed: true, continuePastTarget: true);

        yield return FadeOverlayToBlackForScreen();
        StopAllCarAudio(suppressAutoRestart: true);
        SetEndScreenPointerVisible(true);

        if (gameWinMessageText != null)
            gameWinMessageText.text = ResolveMissionCompletedMessage();

        if (gameWinScreen != null)
            gameWinScreen.SetActive(true);

        fadeImageFader?.SetAlphaImmediate(0f);
    }

    private IEnumerator FadeAndShowScreen(GameObject screen)
    {
        yield return FadeOverlayToBlackForScreen();

        if (screen != null)
            screen.SetActive(true);

        fadeImageFader?.SetAlphaImmediate(0f);
    }

    private void TryHandleIntroSkipInput()
    {
        if (!CanPlayIntroCinematic() || gameplayStarted || missionEnded)
            return;

        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        if (!rewiredPlayer.GetButtonDown(skipIntroAction))
            return;

        introSkipRoutine = StartCoroutine(SkipIntroRoutine());
    }

    private IEnumerator SkipIntroRoutine()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        activeCinematicPlayerMoveTween?.Kill();
        activeCinematicPlayerMoveTween = null;

        BlockPlayerControls(true);
        SetIntroVisionLightActive(false);

        Tween fadeInTween = fadeImageFader != null ? fadeImageFader.FadeIn(screenFadeDuration) : null;
        if (fadeInTween != null)
            yield return fadeInTween.WaitForCompletion();

        CompleteIntroInstantly();
        StartGameplay();

        if (introSkipBlackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(introSkipBlackHoldDuration);

        Tween fadeOutTween = fadeImageFader != null ? fadeImageFader.FadeOut(screenFadeDuration) : null;
        if (fadeOutTween != null)
            yield return fadeOutTween.WaitForCompletion();

        introSkipRoutine = null;
    }

    private IEnumerator MovePlayerToPoint(Transform targetPoint, Transform facingTarget, float duration, float? facingDegrees = null)
    {
        Tween moveTween = BeginMovePlayerToPoint(targetPoint, facingTarget, duration, facingDegrees);
        if (moveTween == null)
            yield break;

        yield return moveTween.WaitForCompletion();
        ApplyCinematicPlayerFacing(targetPoint, facingTarget, facingDegrees);
    }

    private Tween BeginMovePlayerToPoint(Transform targetPoint, Transform facingTarget, float duration, float? facingDegrees = null)
    {
        if (playerRoot == null || targetPoint == null)
            return null;

        activeCinematicPlayerMoveTween?.Kill();
        activeCinematicPlayerMoveTween = playerRoot.DOMove(targetPoint.position, Mathf.Max(0f, duration))
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnUpdate(() => ApplyCinematicPlayerFacing(targetPoint, facingTarget, facingDegrees))
            .OnComplete(() => activeCinematicPlayerMoveTween = null);

        return activeCinematicPlayerMoveTween;
    }

    private void ApplyCinematicPlayerFacing(Transform targetPoint, Transform facingTarget, float? facingDegrees)
    {
        if (facingTarget != null)
        {
            ForcePlayerFacing(facingTarget.position);
            return;
        }

        if (facingDegrees.HasValue)
        {
            SmoothPlayerFacingTowardsDegrees(facingDegrees.Value, Time.unscaledDeltaTime);
            return;
        }

        if (targetPoint != null)
            ForcePlayerFacing(targetPoint.position);
    }

    private IEnumerator DriveCarToPoint(Transform carTransform, Transform targetPoint, float driveSpeed, float acceleration, float deceleration, bool startAtCruiseSpeed, bool continuePastTarget = false)
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

        StopContinuousCarDrive();

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
        if (continuePastTarget)
        {
            continuousCarDriveRoutine = StartCoroutine(ContinueDrivingCarForever(carTransform, direction, Mathf.Max(currentSpeed, maxSpeed > 0f ? maxSpeed : currentSpeed)));
            yield break;
        }

        SetCarEngineLoopActive(false);
    }

    private IEnumerator ContinueDrivingCarForever(Transform carTransform, Vector2 direction, float speed)
    {
        if (carTransform == null || direction.sqrMagnitude <= 0.0001f || speed <= 0.0001f)
        {
            continuousCarDriveRoutine = null;
            yield break;
        }

        Vector2 normalizedDirection = direction.normalized;
        while (carTransform != null)
        {
            carTransform.position += (Vector3)(normalizedDirection * (speed * Time.unscaledDeltaTime));
            yield return null;
        }

        continuousCarDriveRoutine = null;
    }

    private void StopContinuousCarDrive()
    {
        if (continuousCarDriveRoutine == null)
            return;

        StopCoroutine(continuousCarDriveRoutine);
        continuousCarDriveRoutine = null;
    }

    private void CompleteIntroInstantly()
    {
        SetCarEngineLoopActive(false);

        if (introCarTransform != null && introDriveTarget != null)
            introCarTransform.position = introDriveTarget.position;

        PlayCarAnimation(closeDoorAnimationState, 1f);

        if (playerRoot != null)
            playerRoot.SetParent(null, true);

        if (playerRoot != null && introPlayerExitPoint != null)
            playerRoot.position = introPlayerExitPoint.position;

        if (introPlayerFacingTarget != null)
            ForcePlayerFacing(introPlayerFacingTarget.position);
        else
            ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);

        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
    }

    private void AttachPlayerToPoint(Transform targetPoint, bool parentToSeat, float? facingDegrees = null)
    {
        if (playerRoot == null || targetPoint == null)
            return;

        if (parentToSeat)
            playerRoot.SetParent(targetPoint, false);
        else
            playerRoot.SetParent(null, true);

        playerRoot.position = targetPoint.position;
        if (facingDegrees.HasValue)
            ApplyPlayerFacingDegrees(facingDegrees.Value);
    }

    private void ApplyPlayerFacingDegrees(float facingDegrees)
    {
        if (playerRoot != null)
            playerRoot.rotation = Quaternion.Euler(0f, 0f, facingDegrees);

        if (playerBody != null)
            playerBody.rotation = facingDegrees;

        if (playerVisionLight != null)
            playerVisionLight.ApplyExternalDirection(RotateUpByDegrees(facingDegrees), 0f, 0f);
    }

    private void SmoothPlayerFacingTowardsDegrees(float facingDegrees, float deltaTime)
    {
        if (playerVisionLight == null)
        {
            ApplyPlayerFacingDegrees(facingDegrees);
            return;
        }

        playerVisionLight.ApplyExternalDirection(RotateUpByDegrees(facingDegrees), playerVisionLight.RotationSmoothing, deltaTime);
    }

    private IEnumerator RotatePlayerToFacingDegrees(float facingDegrees)
    {
        if (playerVisionLight == null)
        {
            ApplyPlayerFacingDegrees(facingDegrees);
            yield break;
        }

        float timeout = 1.5f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            SmoothPlayerFacingTowardsDegrees(facingDegrees, Time.unscaledDeltaTime);
            float currentAngle = playerRoot != null ? playerRoot.eulerAngles.z : playerBody != null ? playerBody.rotation : 0f;
            float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, facingDegrees));
            if (delta <= 0.5f)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyPlayerFacingDegrees(facingDegrees);
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

    private void PlayCarAnimation(string stateName, float normalizedTime = 0f)
    {
        if (introCarAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        introCarAnimator.Play(stateName, 0, Mathf.Clamp01(normalizedTime));
        introCarAnimator.Update(0f);
    }

    private float ResolveCarAnimationDuration(string stateName)
    {
        if (introCarAnimator == null ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return 0f;
        }

        AnimatorStateInfo stateInfo = introCarAnimator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.length > 0f)
            return stateInfo.length;

        if (introCarAnimator.runtimeAnimatorController == null)
            return 0f;

        AnimationClip[] clips = introCarAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || !string.Equals(clip.name, stateName, StringComparison.Ordinal))
                continue;

            return Mathf.Max(0f, clip.length);
        }

        return 0f;
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
        if (suppressCarAudioAutoRestart ||
            carIdleLoopSource == null ||
            carIdleLoopSource.isPlaying ||
            carIdleLoopSfx == null ||
            !carIdleLoopSfx.HasAnyClip)
        {
            return;
        }

        carIdleLoopBaseVolume = Mathf.Clamp01(carIdleLoopSfx.Volume);
        PlayLoopClipSet(carIdleLoopSource, carIdleLoopSfx, initialVolume: 0f);
        ApplyCarLoopVolumes();
    }

    private void EnsureCarIdleLoopRunning()
    {
        if (suppressCarAudioAutoRestart ||
            introCarTransform == null ||
            carIdleLoopSfx == null ||
            !carIdleLoopSfx.HasAnyClip)
        {
            return;
        }

        carIdleLoopSource = EnsureCarLoopSource(carIdleLoopSource, "Car Idle Loop Source", carIdleLoopLocalOffset);
        if (carIdleLoopSource == null)
            return;

        if (!carIdleLoopSource.isPlaying || carIdleLoopSource.clip == null)
        {
            StartCarIdleLoopIfNeeded();
            return;
        }

        carIdleLoopBaseVolume = Mathf.Clamp01(carIdleLoopSfx.Volume);
    }

    private void SetCarEngineLoopActive(bool active)
    {
        if (introCarTransform == null)
            return;

        if (suppressCarAudioAutoRestart)
        {
            if (!active)
                StopAllCarAudio(suppressAutoRestart: true);
            return;
        }

        EnsureCarIdleLoopRunning();
        carEngineLoopSource = EnsureCarLoopSource(carEngineLoopSource, "Car Engine Loop Source", carEngineLoopLocalOffset);
        if (carEngineLoopSource == null)
            return;

        carEngineLoopTween?.Kill();
        carEngineLoopTween = null;

        if (active)
        {
            if (!carEngineLoopSource.isPlaying)
            {
                carEngineLoopBaseVolume = 0f;
                PlayLoopClipSet(carEngineLoopSource, carEngineLoopSfx, initialVolume: 0f);
            }

            if (carEngineLoopSource.clip == null)
                return;

            float targetVolume = carEngineLoopSfx != null ? Mathf.Clamp01(carEngineLoopSfx.Volume) : 0f;
            carEngineLoopTween = DOTween.To(
                    () => carEngineLoopBaseVolume,
                    value =>
                    {
                        carEngineLoopBaseVolume = Mathf.Clamp01(value);
                        ApplyCarLoopVolumes();
                    },
                    targetVolume,
                    0.2f)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
            return;
        }

        if (!carEngineLoopSource.isPlaying)
            return;

        carEngineLoopTween = DOTween.To(
                () => carEngineLoopBaseVolume,
                value =>
                {
                    carEngineLoopBaseVolume = Mathf.Clamp01(value);
                    ApplyCarLoopVolumes();
                },
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

                carEngineLoopBaseVolume = 0f;
            });
    }

    public void SetExternalCarAudioVolumeMultiplier(float multiplier)
    {
        carAudioExternalVolumeMultiplier = Mathf.Clamp01(multiplier);
        ApplyCarLoopVolumes();
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

    private static void PlayLoopClipSet(AudioSource source, AudioClipSet clipSet, float initialVolume)
    {
        if (source == null || clipSet == null || !clipSet.HasAnyClip)
            return;

        AudioClip clip = clipSet.GetRandomClip();
        if (clip == null)
            return;

        source.clip = clip;
        source.pitch = clipSet.GetRandomPitch();
        source.spatialBlend = clipSet.ResolveSpatialBlend(source.spatialBlend);
        source.minDistance = clipSet.ResolveMinDistance(source.minDistance);
        source.maxDistance = clipSet.ResolveMaxDistance(source.minDistance, source.maxDistance);
        source.volume = Mathf.Clamp01(initialVolume);
        source.loop = true;
        source.Play();
    }

    private void ApplyCarLoopVolumes()
    {
        float multiplier = Mathf.Clamp01(carAudioExternalVolumeMultiplier);

        if (carIdleLoopSource != null)
            carIdleLoopSource.volume = Mathf.Clamp01(carIdleLoopBaseVolume * multiplier);

        if (carEngineLoopSource != null)
            carEngineLoopSource.volume = Mathf.Clamp01(carEngineLoopBaseVolume * multiplier);
    }

    private void StopAllCarAudio(bool suppressAutoRestart)
    {
        suppressCarAudioAutoRestart = suppressAutoRestart;

        carEngineLoopTween?.Kill();
        carEngineLoopTween = null;

        if (carIdleLoopSource != null)
        {
            carIdleLoopSource.Stop();
            carIdleLoopSource.clip = null;
        }

        if (carEngineLoopSource != null)
        {
            carEngineLoopSource.Stop();
            carEngineLoopSource.clip = null;
        }

        carIdleLoopBaseVolume = 0f;
        carEngineLoopBaseVolume = 0f;
    }

    private void ResolveWorldSfxManager()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
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
        if (playerFocusController != null)
        {
            if (!blocked)
                playerFocusController.enabled = playerFocusControllerDefaultEnabled;

            playerFocusController.SetInputBlocked(blocked);

            if (blocked)
                playerFocusController.enabled = false;
        }

        if (blocked && playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        if (!blocked && playerVisionLight != null)
            playerVisionLight.DriveMouseLook(playerVisionLight.RotationSmoothing, 0f);
    }

    private void SetIntroVisionLightActive(bool active)
    {
        if (playerVisionLight == null)
            return;

        playerVisionLight.enabled = active && playerVisionLightDefaultEnabled;
    }

    private static Vector2 RotateUpByDegrees(float degrees)
    {
        return (Quaternion.Euler(0f, 0f, degrees) * Vector2.up).normalized;
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

        bool enteredAlertState = stateEvent.NewState == EnemyState.Alert && stateEvent.PreviousState != EnemyState.Alert;
        if (enteredAlertState)
        {
            for (int i = 0; i < failureStates.Count; i++)
            {
                FailureRuntimeState failureState = failureStates[i];
                if (failureState == null || failureState.Triggered || failureState.Definition == null)
                    continue;

                if (failureState.Definition.FailureType != HideoutJobFailureType.DontAlert)
                    continue;

                TriggerMissionFailure(failureState);
                return;
            }
        }

    }

    private void HandleEnemyPlayerFullyDetected(EnemyVisualDetectionEvent detectionEvent)
    {
        if (missionEnded || !gameplayStarted)
            return;

        if (TryResolveMissionMusicController())
            missionMusicController.PlayAlertedMusic();

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState failureState = failureStates[i];
            if (failureState == null || failureState.Triggered || failureState.Definition == null)
                continue;

            if (failureState.Definition.FailureType != HideoutJobFailureType.DontBeDetected)
                continue;

            TriggerMissionFailure(failureState);
            return;
        }
    }

    private void HandlePlayerDied(ActorDamageContext context)
    {
        if (missionEnded)
            return;

        StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: true, screenMessage: ResolvePlayerKilledMessage()));
    }

    private void HandlePlayerIncapacitated(ActorDamageContext context)
    {
        if (missionEnded)
            return;

        StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: true, screenMessage: ResolvePlayerKilledMessage()));
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

            TriggerMissionFailure(failureState);
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

        if (UseMissionStatusEntryList)
        {
            if (jobObjectivesText != null)
                jobObjectivesText.text = string.Empty;

            if (jobFailureText != null)
                jobFailureText.text = string.Empty;

            RefreshMissionStatusEntriesFromStates();
            return;
        }

        if (jobObjectivesText != null)
            jobObjectivesText.text = BuildObjectiveText();

        if (jobFailureText != null)
            jobFailureText.text = BuildFailureText();
    }

    private void RegisterMissionStatusEntryPrefabs()
    {
        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        if (globalObjectPooler == null)
            return;

        if (objectiveStatusEntryPrefab != null)
            globalObjectPooler.RegisterPrefab(objectiveStatusEntryPrefab.gameObject);

        if (failureStatusEntryPrefab != null)
            globalObjectPooler.RegisterPrefab(failureStatusEntryPrefab.gameObject);
    }

    private void RestartMissionStatusEntryBuild()
    {
        if (missionStatusEntryBuildRoutine != null)
        {
            StopCoroutine(missionStatusEntryBuildRoutine);
            missionStatusEntryBuildRoutine = null;
        }

        ClearMissionStatusEntries();

        if (!UseMissionStatusEntryList || !isActiveAndEnabled)
            return;

        missionStatusEntryBuildRoutine = StartCoroutine(BuildMissionStatusEntriesRoutine());
    }

    private IEnumerator BuildMissionStatusEntriesRoutine()
    {
        bool spawnedAnyObjectives = false;
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null || state.Definition == null)
                continue;

            spawnedAnyObjectives = true;
            state.EntryView = SpawnMissionStatusEntry(
                objectiveStatusEntryPrefab,
                BuildObjectiveLine(state, applyStrikethrough: false),
                state.IsComplete);

            yield return WaitForMissionStatusEntry(state.EntryView);
        }

        if (!spawnedAnyObjectives && currentJob != null && !string.IsNullOrWhiteSpace(currentJob.ObjectivesText))
            yield return WaitForMissionStatusEntry(SpawnMissionStatusEntry(objectiveStatusEntryPrefab, currentJob.ObjectivesText, useStrikethrough: false));

        bool spawnedAnyFailures = false;
        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Definition == null)
                continue;

            spawnedAnyFailures = true;
            state.EntryView = SpawnMissionStatusEntry(
                failureStatusEntryPrefab,
                state.Definition.DisplayText,
                useStrikethrough: false);

            yield return WaitForMissionStatusEntry(state.EntryView);
        }

        if (!spawnedAnyFailures && currentJob != null && !string.IsNullOrWhiteSpace(currentJob.TermsOfFailureText))
            yield return WaitForMissionStatusEntry(SpawnMissionStatusEntry(failureStatusEntryPrefab, currentJob.TermsOfFailureText, useStrikethrough: false));

        missionStatusEntryBuildRoutine = null;
    }

    private IEnumerator WaitForMissionStatusEntry(MissionStatusEntryUI entryView)
    {
        Tween fadeTween = entryView != null
            ? entryView.PlayFadeIn(missionStatusEntryFadeDuration)
            : null;

        if (fadeTween != null)
            yield return fadeTween.WaitForCompletion();

        if (missionStatusEntrySpawnInterval > 0f)
            yield return new WaitForSecondsRealtime(missionStatusEntrySpawnInterval);
    }

    private MissionStatusEntryUI SpawnMissionStatusEntry(MissionStatusEntryUI prefab, string text, bool useStrikethrough)
    {
        if (prefab == null || missionStatusContentRoot == null)
            return null;

        MissionStatusEntryUI entryView = null;
        if (globalObjectPooler != null)
            entryView = globalObjectPooler.Spawn(prefab, Vector3.zero, Quaternion.identity, missionStatusContentRoot);

        if (entryView == null)
            entryView = Instantiate(prefab, missionStatusContentRoot);

        if (entryView == null)
            return null;

        entryView.transform.SetParent(missionStatusContentRoot, false);
        entryView.transform.SetAsLastSibling();
        entryView.PrepareForDisplay();
        entryView.SetText(text, useStrikethrough);
        activeMissionStatusEntries.Add(entryView);
        return entryView;
    }

    private void ClearMissionStatusEntries()
    {
        for (int i = 0; i < activeMissionStatusEntries.Count; i++)
        {
            MissionStatusEntryUI entryView = activeMissionStatusEntries[i];
            if (entryView == null)
                continue;

            GlobalPooledObject pooledObject = entryView.GetComponent<GlobalPooledObject>();
            if (pooledObject != null)
            {
                pooledObject.ReturnToPool();
                continue;
            }

            Destroy(entryView.gameObject);
        }

        activeMissionStatusEntries.Clear();

        for (int i = 0; i < objectiveStates.Count; i++)
        {
            if (objectiveStates[i] != null)
                objectiveStates[i].EntryView = null;
        }

        for (int i = 0; i < failureStates.Count; i++)
        {
            if (failureStates[i] != null)
                failureStates[i].EntryView = null;
        }
    }

    private void RefreshMissionStatusEntriesFromStates()
    {
        if (!UseMissionStatusEntryList)
            return;

        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state?.EntryView == null)
                continue;

            state.EntryView.SetText(BuildObjectiveLine(state, applyStrikethrough: false), state.IsComplete);
        }

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state?.EntryView == null || state.Definition == null)
                continue;

            state.EntryView.SetText(state.Definition.DisplayText, useStrikethrough: false);
        }
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

            string line = BuildObjectiveLine(state, applyStrikethrough: true);

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

    private string BuildObjectiveLine(ObjectiveRuntimeState state, bool applyStrikethrough)
    {
        if (state == null || state.Definition == null)
            return string.Empty;

        string line = state.DisplayText;
        if (state.RequiredCount > 1)
            line = $"{line} ({Mathf.Min(state.CompletedCount, state.RequiredCount)}/{state.RequiredCount})";

        if (applyStrikethrough && state.IsComplete)
            line = $"<s>{line}</s>";

        return line;
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

            TriggerMissionFailure(state);
            return;
        }
    }

    private void RefreshTimeLimitUi()
    {
        if (timerContent == null)
            return;

        FailureRuntimeState activeTimeLimit = GetActiveTimeLimitFailure();
        if (activeTimeLimit == null)
        {
            timerContent.gameObject.SetActive(false);
            timeLimitText.text = string.Empty;
            timeLimitText.color = timeLimitDefaultColor;
            StopTimeLimitWarningPulse();
            return;
        }

        timerContent.gameObject.SetActive(true);
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
        if (timerContent == null || timeLimitWarningSequence != null)
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

    private IEnumerator HandleMissionFailedRoutine(bool playerWasKilled, string screenMessage)
    {
        if (missionEnded)
            yield break;

        missionEnded = true;
        Time.timeScale = 1f;
        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse();
        BlockPlayerControls(true);
        SetEndScreenPointerVisible(true);
        if (TryResolveMissionMusicController())
            missionMusicController.PlayGameOverMusic();

        if (playerWasKilled)
        {
            if (playerKilledMessageText != null)
                playerKilledMessageText.text = string.IsNullOrWhiteSpace(screenMessage) ? ResolvePlayerKilledMessage() : screenMessage;
        }
        else if (questFailMessageText != null)
            questFailMessageText.text = string.IsNullOrWhiteSpace(screenMessage) ? "Mission Failed." : screenMessage;

        yield return FadeAndShowScreen(playerWasKilled ? playerKilledScreen : questFailScreen);
    }

    private void TriggerMissionFailure(FailureRuntimeState failureState)
    {
        if (failureState == null)
            return;

        failureState.Triggered = true;
        StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: false, screenMessage: ResolveFailureScreenMessage(failureState.Definition)));
    }

    private void RegisterScreenButtonCallbacks()
    {
        if (questFailRetryButton != null)
            questFailRetryButton.onClick.AddListener(RetryCurrentMission);

        if (questFailQuitButton != null)
            questFailQuitButton.onClick.AddListener(QuitToHideout);

        if (playerKilledRetryButton != null)
            playerKilledRetryButton.onClick.AddListener(RetryCurrentMission);

        if (playerKilledQuitButton != null)
            playerKilledQuitButton.onClick.AddListener(QuitToHideout);

        if (gameWinContinueButton != null)
            gameWinContinueButton.onClick.AddListener(ContinueToHideoutAfterWin);
    }

    private void UnregisterScreenButtonCallbacks()
    {
        if (questFailRetryButton != null)
            questFailRetryButton.onClick.RemoveListener(RetryCurrentMission);

        if (questFailQuitButton != null)
            questFailQuitButton.onClick.RemoveListener(QuitToHideout);

        if (playerKilledRetryButton != null)
            playerKilledRetryButton.onClick.RemoveListener(RetryCurrentMission);

        if (playerKilledQuitButton != null)
            playerKilledQuitButton.onClick.RemoveListener(QuitToHideout);

        if (gameWinContinueButton != null)
            gameWinContinueButton.onClick.RemoveListener(ContinueToHideoutAfterWin);
    }

    private IEnumerator FadeOverlayToBlackForScreen()
    {
        if (fadeImageFader == null)
            yield break;

        if (fadeImageFader.CurrentAlpha < 0.999f)
        {
            Tween fadeTween = fadeImageFader.FadeIn(screenFadeDuration);
            if (fadeTween != null)
                yield return fadeTween.WaitForCompletion();
        }
        else
            fadeImageFader.SetAlphaImmediate(1f);
    }

    private IEnumerator FadeOverlayOutAndRestore()
    {
        if (fadeImageFader == null)
            yield break;

        if (fadeImageFader.CurrentAlpha > 0.001f)
        {
            Tween fadeTween = fadeImageFader.FadeOut(screenFadeDuration);
            if (fadeTween != null)
                yield return fadeTween.WaitForCompletion();
        }
        else
            fadeImageFader.SetAlphaImmediate(0f);
    }

    public void RetryCurrentMission()
    {
        if (sceneTransitionInProgress)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!SceneLoadUtility.CanLoadScene(activeScene.buildIndex, activeScene.name))
            return;

        sceneTransitionInProgress = true;
        StartCoroutine(LoadSceneRoutine(activeScene.buildIndex, activeScene.name, clearCurrentJob: false, completeCurrentJob: false));
    }

    public void QuitToHideout()
    {
        if (sceneTransitionInProgress || !SceneLoadUtility.IsBuildSceneAvailable(hideoutSceneBuildIndex))
            return;

        sceneTransitionInProgress = true;
        StartCoroutine(LoadSceneRoutine(hideoutSceneBuildIndex, string.Empty, clearCurrentJob: false, completeCurrentJob: false));
    }

    public void ContinueToHideoutAfterWin()
    {
        if (sceneTransitionInProgress || !SceneLoadUtility.IsBuildSceneAvailable(hideoutSceneBuildIndex))
            return;

        sceneTransitionInProgress = true;
        StartCoroutine(LoadSceneRoutine(hideoutSceneBuildIndex, string.Empty, clearCurrentJob: false, completeCurrentJob: true));
    }

    private IEnumerator LoadSceneRoutine(int sceneBuildIndex, string fallbackSceneName, bool clearCurrentJob, bool completeCurrentJob)
    {
        Time.timeScale = 1f;

        if (!SceneLoadUtility.CanLoadScene(sceneBuildIndex, fallbackSceneName))
        {
            sceneTransitionInProgress = false;
            yield break;
        }

        if ((questFailScreen != null && questFailScreen.activeInHierarchy) ||
            (playerKilledScreen != null && playerKilledScreen.activeInHierarchy) ||
            (gameWinScreen != null && gameWinScreen.activeInHierarchy))
        {
            fadeImageFader?.SetAlphaImmediate(0f);
        }

        yield return FadeOverlayToBlackForScreen();

        if (completeCurrentJob)
            HideoutRuntimeSession.CompleteJob(currentJob);
        else if (clearCurrentJob)
            HideoutRuntimeSession.ClearCurrentJob();

        if (SceneLoadUtility.TryLoadScene(sceneBuildIndex, fallbackSceneName))
            yield break;

        sceneTransitionInProgress = false;
        Debug.LogWarning($"Could not load scene. Build Index: {sceneBuildIndex}, Fallback Name: {fallbackSceneName}", this);

        yield return FadeOverlayOutAndRestore();
    }

    private string ResolveFailureScreenMessage(HideoutJobFailureDefinition definition)
    {
        return definition != null ? definition.FailureScreenMessage : "Mission Failed.";
    }

    private string ResolvePlayerKilledMessage()
    {
        return string.IsNullOrWhiteSpace(playerKilledMessage) ? "Game Over." : playerKilledMessage.Trim();
    }

    private string ResolveMissionCompletedMessage()
    {
        return string.IsNullOrWhiteSpace(missionCompletedMessage) ? "Mission Complete." : missionCompletedMessage.Trim();
    }

    private void ResetSceneScopedRuntimeState()
    {
        Time.timeScale = 1f;
        MissionRuntimeEvents.ResetRuntimeState();
        GameplayConsoleCheatState.ResetRuntimeState();
        FocusRevealTarget.ResetRuntimeState();
        SetEndScreenPointerVisible(false);
    }

    private void SetEndScreenPointerVisible(bool visible)
    {
        DynamicCrosshairUI dynamicCrosshairUi = FindFirstObjectByType<DynamicCrosshairUI>();
        if (dynamicCrosshairUi != null)
            dynamicCrosshairUi.SetUiSuppressed(visible);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = visible;
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

    private bool TryResolveMissionMusicController()
    {
        if (missionMusicController == null)
            missionMusicController = GetComponent<MissionMusicController>();

        if (missionMusicController == null)
            missionMusicController = FindFirstObjectByType<MissionMusicController>();

        return missionMusicController != null;
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
