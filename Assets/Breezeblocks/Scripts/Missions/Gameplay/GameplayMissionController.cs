using System;
using System.Collections;
using System.Collections.Generic;
using Breezeblocks;
using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Rewired;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[DefaultExecutionOrder(-5000)]
[AddComponentMenu("Breezeblocks/Missions/Gameplay Mission Controller")]
public partial class GameplayMissionController : MonoBehaviour
{
    public static bool EnemyRuntimeBlockedAtMissionStart { get; private set; }

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
    [SerializeField] private float carLoopDopplerLevel;

    [FoldoutGroup("Car Audio"), Range(0f, 360f)]
    [SerializeField] private float carLoopSpread;

    [FoldoutGroup("Car Audio"), Range(0, 256)]
    [SerializeField] private int carLoopPriority = 96;

    [FoldoutGroup("Job")]
    [SerializeField] private HideoutJobDefinition fallbackMission;

    [FoldoutGroup("Job UI")]
    [SerializeField] private TMP_Text jobTitleText;

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
    [SerializeField] private GameObject timerContent;

    [FoldoutGroup("Job UI/List")]
    [SerializeField] private RectTransform missionStatusContentRoot;

    [FoldoutGroup("Job UI/List"), AssetsOnly]
    [SerializeField] private MissionStatusEntryUI objectiveStatusEntryPrefab;

    [FoldoutGroup("Job UI/List"), AssetsOnly]
    [SerializeField] private MissionStatusEntryUI failureStatusEntryPrefab;

    [FoldoutGroup("Job UI/List"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float missionStatusEntryFadeDuration = 0.2f;

    [FoldoutGroup("Job UI/List"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float missionStatusEntrySpawnInterval = 0.2f;

    [FoldoutGroup("Job UI/List")]
    [SerializeField] private GlobalObjectPooler globalObjectPooler;

    [FoldoutGroup("Job UI"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float timeLimitWarningThresholdSeconds = 30f;

    [FoldoutGroup("Job UI"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float timeLimitMillisecondsThresholdSeconds = 10f;

    [FoldoutGroup("Job UI")]
    [SerializeField] private Color timeLimitWarningColor = new(1f, 0.3f, 0.3f, 1f);

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
    [SerializeField] private int hideoutSceneBuildIndex;

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

    [FoldoutGroup("Intro Cinematic"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float introStartupBlackHoldDuration = 0.5f;

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
    [SerializeField] private float outroDriveDeceleration;

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
    private readonly List<MissionStatusEntryUI> activeMissionStatusEntries = new();
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
    private Coroutine startupSequenceRoutine;
    private Coroutine continuousCarDriveRoutine;
    private bool playerVisionLightDefaultEnabled = true;
    private bool playerFocusControllerDefaultEnabled = true;
    private bool playerComponentDefaultStatesCached;
    private bool sceneTransitionInProgress;
    private bool suppressCarAudioAutoRestart;

    private bool UseMissionStatusEntryList =>
        missionStatusContentRoot != null &&
        objectiveStatusEntryPrefab != null &&
        failureStatusEntryPrefab != null;
}

}
