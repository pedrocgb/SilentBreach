using System;
using System.Collections.Generic;
using Breezeblocks.Input;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace Breezeblocks.Missions
{

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Lockpick Minigame Controller")]
public sealed class LockpickMinigameController : MonoBehaviour
{
    private sealed class TumblerRuntimeState
    {
        public float HotspotDepth;
        public float HotspotShakeRampStartDepth;
        public float CurrentDepth;
        public bool IsLocked;
        public bool HotspotActive;
        public float HotspotExpireTime;
    }

    private const float MinimumAxisThreshold = 0.01f;
    private const float MinimumDuration = 0f;
    private const float MinimumShakeCycleDuration = 0.02f;
    private const float MinimumHotspotWindowDuration = 0.02f;

    private static LockpickMinigameController activeInstance;
    private static readonly LockedInteractionFeedbackSettings DefaultNoLockpickFeedback = new();

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string horizontalAction = "Move Horizontal";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string pushAction = "Interact";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string closeAction = "Cancel Throw";

    [FoldoutGroup("References")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("References")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [FoldoutGroup("References")]
    [SerializeField] private Canvas targetCanvas;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform selectorRect;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform selectorReferenceSpace;

    [FoldoutGroup("References"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<LockpickTumblerView> tumblerViews = new();

    [FoldoutGroup("References")]
    [SerializeField] private Volume targetVolume;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text lockpickUsesText;

    [FoldoutGroup("References/Lockpick Break Visual")]
    [SerializeField] private GameObject lockpickPoint;

    [FoldoutGroup("References/Lockpick Break Visual")]
    [SerializeField] private RectTransform brokenLockpickPoint;

    [FoldoutGroup("References/Lockpick Break Visual")]
    [SerializeField] private RectTransform brokenLockpickPieceOne;

    [FoldoutGroup("References/Lockpick Break Visual")]
    [SerializeField] private RectTransform brokenLockpickPieceTwo;

    [FoldoutGroup("Navigation"), Range(MinimumAxisThreshold, 1f)]
    [SerializeField] private float navigationAxisThreshold = 0.5f;

    [FoldoutGroup("Navigation"), Range(MinimumAxisThreshold, 1f)]
    [SerializeField] private float navigationAxisReleaseThreshold = 0.25f;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float panelFadeDuration = 0.16f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease panelFadeEase = Ease.InOutSine;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float selectorMoveDuration = 0.12f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease selectorMoveEase = Ease.OutSine;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float earlyReleaseResetDuration = 0.18f;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float failureResetDuration = 0.26f;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float successCloseDelayDuration = 0.4f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease tumblerResetEase = Ease.OutSine;

    [FoldoutGroup("Animation"), MinValue(MinimumShakeCycleDuration), SuffixLabel("s", true)]
    [SerializeField] private float hotspotShakeCycleDuration = 0.14f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Vector2 hotspotShakeStrength = new(7f, 0f);

    [FoldoutGroup("Animation"), MinValue(1)]
    [SerializeField] private int hotspotShakeVibrato = 24;

    [FoldoutGroup("Animation"), Range(0f, 180f)]
    [SerializeField] private float hotspotShakeRandomness = 20f;

    [FoldoutGroup("Blur"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float blurTransitionDuration = 0.16f;

    [FoldoutGroup("Blur"), MinValue(0f)]
    [SerializeField] private float blurBokehFocusDistance = 2.5f;

    [FoldoutGroup("Blur"), MinValue(0.1f)]
    [SerializeField] private float blurBokehAperture = 0.8f;

    [FoldoutGroup("Blur"), MinValue(1f)]
    [SerializeField] private float blurBokehFocalLength = 75f;

    [FoldoutGroup("Blur"), Range(3, 9)]
    [SerializeField] private int blurBokehBladeCount = 7;

    [FoldoutGroup("Blur"), Range(0f, 1f)]
    [SerializeField] private float blurBokehBladeCurvature = 0.9f;

    [FoldoutGroup("Blur"), Range(-180f, 180f)]
    [SerializeField] private float blurBokehBladeRotation;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip tumblerFailSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip tumblerLockSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip tumblerShakingLoopSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip lockpickCompleteSfx;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float tumblerFailVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float tumblerLockVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float tumblerShakingLoopVolume = 0.65f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float lockpickCompleteVolume = 1f;

    [FoldoutGroup("Lockpick Break")]
    [SerializeField] private AudioClipSet lockpickBreakSfx = new();

    [FoldoutGroup("Lockpick Break"), Range(0f, 1f)]
    [SerializeField] private float lockpickBreakSfxVolume = 1f;

    [FoldoutGroup("Lockpick Break"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float lockpickBreakRetryDelay = 0.35f;

    [FoldoutGroup("Lockpick Break"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float outOfLockpicksCloseDelayDuration = 0.75f;

    [FoldoutGroup("Lockpick Break/Visual"), SuffixLabel("deg", true)]
    [SerializeField] private float brokenLockpickPieceOneRotationZ = -34.5f;

    [FoldoutGroup("Lockpick Break/Visual"), SuffixLabel("deg", true)]
    [SerializeField] private float brokenLockpickPieceTwoRotationZ = -45f;

    [FoldoutGroup("Lockpick Break/Visual"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float brokenLockpickRotationDuration = 0.12f;

    [FoldoutGroup("Lockpick Break/Visual")]
    [SerializeField] private Ease brokenLockpickRotationEase = Ease.OutBack;

    [FoldoutGroup("Lockpick Break/Visual"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float brokenLockpickDropDelay = 0.06f;

    [FoldoutGroup("Lockpick Break/Visual"), SuffixLabel("anchored Y", true)]
    [SerializeField] private float brokenLockpickDropTargetY = -520f;

    [FoldoutGroup("Lockpick Break/Visual"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float brokenLockpickDropDuration = 0.32f;

    [FoldoutGroup("Lockpick Break/Visual")]
    [SerializeField] private Ease brokenLockpickDropEase = Ease.InBack;

    [FoldoutGroup("Lockpick Break"), MinValue(0f)]
    [SerializeField] private float lockpickBreakNoise = 0.25f;

    [FoldoutGroup("Lockpick Break")]
    [SerializeField] private NoiseType lockpickBreakNoiseType = NoiseType.Common;

    [FoldoutGroup("Lockpick Break")]
    [SerializeField] private bool lockpickBreakExtremeNoise;

    [FoldoutGroup("No Lockpick Feedback"), InlineProperty, HideLabel]
    [SerializeField] private LockedInteractionFeedbackSettings noLockpickFeedback = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSessionActive => activeLockpickTarget != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int SelectedTumblerIndex => selectedIndex;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public LockpickableInteractable ActiveLockpickable => activeLockpickTarget as LockpickableInteractable;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public ILockpickSessionTarget ActiveLockpickTarget => activeLockpickTarget;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public LockpickDifficulty ActiveDifficulty => activeDefinition != null ? activeDefinition.Difficulty : LockpickDifficulty.Medium;

    public static bool HasRegisteredInstance => activeInstance != null && activeInstance.CanStartSessions;

    public event Action<ILockpickSessionTarget> Unlocked;
    public event Action<ILockpickSessionTarget> SessionClosed;

    private readonly List<LockpickTumblerView> resolvedTumblerViews = new();
    private readonly List<TumblerRuntimeState> tumblerStates = new();
    private readonly PlayerMinigameControlLock controlLock = new();
    private readonly MinigameBokehBlurController blurController = new();

    private AudioSource uiAudioSource;
    private IPlayerInputReader inputReader;
    private Tween panelFadeTween;
    private Tween selectorTween;
    private Tween successDelayTween;
    private Tween outOfLockpicksCloseTween;
    private Sequence lockpickBreakVisualTween;
    private ILockpickSessionTarget activeLockpickTarget;
    private MonoBehaviour activeLockpickTargetBehaviour;
    private LockpickMinigameDefinition activeDefinition;
    private GameObject activeInteractorRoot;
    private Vector2 brokenLockpickInitialAnchoredPosition;
    private Quaternion brokenLockpickInitialRotation = Quaternion.identity;
    private Quaternion brokenLockpickPieceOneInitialRotation = Quaternion.identity;
    private Quaternion brokenLockpickPieceTwoInitialRotation = Quaternion.identity;
    private int activeTumblerCount;
    private int selectedIndex;
    private bool navigationAxisEngaged;
    private bool waitForPushRelease;
    private bool successClosePending;
    private bool outOfLockpicksClosePending;
    private bool lockpickBreakVisualActive;
    private bool hasBrokenLockpickInitialState;
    private bool lockpickBreakDelayActive;
    private float lockpickBreakDelayEndsAt;
    private bool CanStartSessions => panelRoot != null && selectorRect != null && resolvedTumblerViews.Count > 0;

    /// <summary>
    /// Registers the controller singleton and applies a hidden default panel state.
    /// </summary>
    private void Awake()
    {
        CacheAudioSource();
        ResolveReferences();
        RebuildResolvedTumblerViews();
        ValidateNoLockpickFeedback();
        CaptureBrokenLockpickVisualInitialState();
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
        HidePanelImmediate();
    }

    /// <summary>
    /// Registers this controller as the active shared lockpicking service.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        RebuildResolvedTumblerViews();

        if (activeInstance != null && activeInstance != this)
            Debug.LogWarning("Multiple LockpickMinigameController instances are active. The newest instance will be used.", this);

        activeInstance = this;
    }

    /// <summary>
    /// Cancels any active session and unregisters the shared controller when disabled.
    /// </summary>
    private void OnDisable()
    {
        ForceCloseSessionImmediate();

        panelFadeTween?.Kill();
        panelFadeTween = null;
        selectorTween?.Kill();
        selectorTween = null;
        blurController.KillTween();
        successDelayTween?.Kill();
        successDelayTween = null;
        outOfLockpicksCloseTween?.Kill();
        outOfLockpicksCloseTween = null;
        lockpickBreakVisualTween?.Kill();
        lockpickBreakVisualTween = null;
        StopShakeLoopSfx();

        if (activeInstance == this)
            activeInstance = null;
    }

    /// <summary>
    /// Clamps authored animation values and refreshes cached references while editing.
    /// </summary>
    private void OnValidate()
    {
        navigationAxisThreshold = Mathf.Clamp(navigationAxisThreshold, MinimumAxisThreshold, 1f);
        navigationAxisReleaseThreshold = Mathf.Clamp(navigationAxisReleaseThreshold, MinimumAxisThreshold, navigationAxisThreshold);
        panelFadeDuration = Mathf.Max(MinimumDuration, panelFadeDuration);
        selectorMoveDuration = Mathf.Max(MinimumDuration, selectorMoveDuration);
        earlyReleaseResetDuration = Mathf.Max(MinimumDuration, earlyReleaseResetDuration);
        failureResetDuration = Mathf.Max(MinimumDuration, failureResetDuration);
        successCloseDelayDuration = Mathf.Max(MinimumDuration, successCloseDelayDuration);
        hotspotShakeCycleDuration = Mathf.Max(MinimumShakeCycleDuration, hotspotShakeCycleDuration);
        hotspotShakeVibrato = Mathf.Max(1, hotspotShakeVibrato);
        hotspotShakeRandomness = Mathf.Clamp(hotspotShakeRandomness, 0f, 180f);
        blurTransitionDuration = Mathf.Max(MinimumDuration, blurTransitionDuration);
        blurBokehFocusDistance = Mathf.Max(0f, blurBokehFocusDistance);
        blurBokehAperture = Mathf.Max(0.1f, blurBokehAperture);
        blurBokehFocalLength = Mathf.Max(1f, blurBokehFocalLength);
        blurBokehBladeCount = Mathf.Clamp(blurBokehBladeCount, 3, 9);
        blurBokehBladeCurvature = Mathf.Clamp01(blurBokehBladeCurvature);
        blurBokehBladeRotation = Mathf.Clamp(blurBokehBladeRotation, -180f, 180f);
        tumblerFailVolume = Mathf.Clamp01(tumblerFailVolume);
        tumblerLockVolume = Mathf.Clamp01(tumblerLockVolume);
        tumblerShakingLoopVolume = Mathf.Clamp01(tumblerShakingLoopVolume);
        lockpickCompleteVolume = Mathf.Clamp01(lockpickCompleteVolume);
        lockpickBreakSfx ??= new AudioClipSet();
        lockpickBreakSfx.Validate();
        lockpickBreakSfxVolume = Mathf.Clamp01(lockpickBreakSfxVolume);
        lockpickBreakRetryDelay = Mathf.Max(0f, lockpickBreakRetryDelay);
        outOfLockpicksCloseDelayDuration = Mathf.Max(0f, outOfLockpicksCloseDelayDuration);
        brokenLockpickRotationDuration = Mathf.Max(MinimumDuration, brokenLockpickRotationDuration);
        brokenLockpickDropDelay = Mathf.Max(MinimumDuration, brokenLockpickDropDelay);
        brokenLockpickDropDuration = Mathf.Max(MinimumDuration, brokenLockpickDropDuration);
        lockpickBreakNoise = Mathf.Max(0f, lockpickBreakNoise);
        ValidateNoLockpickFeedback();
        ResolveReferences();
        RebuildResolvedTumblerViews();
    }

    /// <summary>
    /// Advances the active lockpicking session by polling Rewired input and updating tumbler state.
    /// </summary>
    private void Update()
    {
        if (!IsSessionActive)
            return;

        if (activeLockpickTargetBehaviour == null || !activeLockpickTargetBehaviour.isActiveAndEnabled)
        {
            CancelActiveSession();
            return;
        }

        if (successClosePending || outOfLockpicksClosePending || lockpickBreakVisualActive)
        {
            TrackSelectorToAnimatedTumbler();
            return;
        }

        if (lockpickBreakDelayActive)
        {
            if (Time.unscaledTime < lockpickBreakDelayEndsAt)
            {
                TrackSelectorToAnimatedTumbler();
                return;
            }

            lockpickBreakDelayActive = false;
        }

        inputReader ??= new RewiredPlayerInputReader(rewiredPlayerId);
        if (!inputReader.IsReady)
            return;

        bool closePressed = inputReader.GetButtonDown(closeAction);
        bool pushHeld = inputReader.GetButton(pushAction);
        if (waitForPushRelease)
        {
            if (!pushHeld)
                waitForPushRelease = false;

            return;
        }

        if (closePressed)
        {
            CancelAndResetActiveSession();
            return;
        }

        if (!PlayerLockpickInventoryUtility.HasAnyLockpickUses(activeInteractorRoot))
        {
            TrackSelectorToAnimatedTumbler();
            return;
        }

        HandleHorizontalNavigation(pushHeld);
        HandlePushInteraction(pushHeld);
        TrackSelectorToAnimatedTumbler();
    }

    /// <summary>
    /// Starts a new session through the shared active controller when one is available.
    /// </summary>
    public static bool TryBeginActiveSession(GameObject interactorRoot, LockpickableInteractable lockpickable)
    {
        return activeInstance != null && activeInstance.TryBeginSession(interactorRoot, lockpickable);
    }

    /// <summary>
    /// Starts a new session through the shared active controller for any supported lockpick target.
    /// </summary>
    public static bool TryBeginActiveSession(GameObject interactorRoot, ILockpickSessionTarget lockpickTarget)
    {
        return activeInstance != null && activeInstance.TryBeginSession(interactorRoot, lockpickTarget);
    }

    /// <summary>
    /// Resolves the shared denied-lock prompt label for any system that uses lockpicking.
    /// </summary>
    public static string ResolveNoLockpickDisplayName(bool blockedAttemptRevealed)
    {
        LockedInteractionFeedbackSettings settings = ResolveNoLockpickFeedbackSettings();
        return blockedAttemptRevealed ? settings.LockedLabel : settings.AttemptLabel;
    }

    /// <summary>
    /// Creates the shared prompt feedback payload used when the player tries a lock without lockpicks.
    /// </summary>
    public static InteractionPromptFeedback CreateNoLockpickPromptFeedback()
    {
        return ResolveNoLockpickFeedbackSettings().CreatePromptFeedback();
    }

    /// <summary>
    /// Plays the shared denied-lock world feedback from the supplied target position.
    /// </summary>
    public static void PlayNoLockpickWorldFeedback(Vector3 position, GameObject source)
    {
        ResolveNoLockpickFeedbackSettings().PlayWorldFeedback(position, source);
    }

    /// <summary>
    /// Attempts to start a lockpicking session for the supplied interactable and interactor.
    /// </summary>
    public bool TryBeginSession(GameObject interactorRoot, LockpickableInteractable lockpickable)
    {
        return TryBeginSession(interactorRoot, lockpickable as ILockpickSessionTarget);
    }

    /// <summary>
    /// Attempts to start a lockpicking session for the supplied target and interactor.
    /// </summary>
    public bool TryBeginSession(GameObject interactorRoot, ILockpickSessionTarget lockpickTarget)
    {
        if (IsSessionActive ||
            interactorRoot == null ||
            lockpickTarget == null ||
            lockpickTarget.Definition == null ||
            lockpickTarget is not MonoBehaviour targetBehaviour ||
            !targetBehaviour.isActiveAndEnabled)
        {
            return false;
        }

        if (GameplayConsoleCheatState.InstantLockpicking)
        {
            lockpickTarget.NotifyUnlocked(interactorRoot);
            Unlocked?.Invoke(lockpickTarget);
            return true;
        }

        ResolveReferences();
        RebuildResolvedTumblerViews();

        int usableTumblerCount = Mathf.Min(lockpickTarget.Definition.TumblerCount, resolvedTumblerViews.Count);
        if (panelRoot == null ||
            selectorRect == null ||
            usableTumblerCount <= 0 ||
            !PlayerLockpickInventoryUtility.HasAnyLockpickUses(interactorRoot))
        {
            return false;
        }

        activeLockpickTarget = lockpickTarget;
        activeLockpickTargetBehaviour = targetBehaviour;
        activeDefinition = lockpickTarget.Definition;
        activeInteractorRoot = interactorRoot;
        CacheInteractorReferences(interactorRoot);
        PrepareSessionState(usableTumblerCount);
        ApplyInteractorInputBlocked(true);
        ShowPanel();
        waitForPushRelease = inputReader != null && inputReader.GetButton(pushAction);
        return true;
    }

    /// <summary>
    /// Cancels the current session without unlocking the target.
    /// </summary>
    public void CancelActiveSession()
    {
        if (!IsSessionActive)
            return;

        CloseSessionInternal();
    }

    /// <summary>
    /// Cancels the current session as a player-initiated close and resets all progress for the next entry.
    /// </summary>
    public void CancelAndResetActiveSession()
    {
        if (!IsSessionActive || successClosePending)
            return;

        CloseSessionInternal();
    }

    /// <summary>
    /// Resolves local and external references used by the shared panel controller.
    /// </summary>
    private void ResolveReferences()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (panelCanvasGroup == null && panelRoot != null)
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();

        if (selectorReferenceSpace == null && selectorRect != null)
            selectorReferenceSpace = selectorRect.parent as RectTransform;

        if (targetCanvas == null)
        {
            RectTransform panelRect = panelRoot != null ? panelRoot.transform as RectTransform : null;
            if (panelRect != null)
                targetCanvas = panelRect.GetComponentInParent<Canvas>(true);
        }

        if (targetVolume == null)
            targetVolume = PlayerSceneReferenceUtility.FindPlayerVolume(activeInteractorRoot != null ? activeInteractorRoot : gameObject);
    }

    /// <summary>
    /// Rebuilds the non-null tumbler view cache while preserving serialized ordering.
    /// </summary>
    private void RebuildResolvedTumblerViews()
    {
        resolvedTumblerViews.Clear();
        for (int i = 0; i < tumblerViews.Count; i++)
        {
            LockpickTumblerView tumblerView = tumblerViews[i];
            if (tumblerView != null)
                resolvedTumblerViews.Add(tumblerView);
        }
    }

    /// <summary>
    /// Caches player control components so they can be blocked while the minigame is open.
    /// </summary>
    private void CacheInteractorReferences(GameObject interactorRoot)
    {
        controlLock.Bind(interactorRoot);
    }

    /// <summary>
    /// Returns the configured no-lockpick feedback settings, falling back to safe defaults when no controller exists.
    /// </summary>
    private static LockedInteractionFeedbackSettings ResolveNoLockpickFeedbackSettings()
    {
        if (activeInstance != null)
        {
            activeInstance.ValidateNoLockpickFeedback();
            return activeInstance.noLockpickFeedback;
        }

        DefaultNoLockpickFeedback.Validate();
        return DefaultNoLockpickFeedback;
    }

    /// <summary>
    /// Ensures the shared no-lockpick feedback settings remain valid before consumers use them.
    /// </summary>
    private void ValidateNoLockpickFeedback()
    {
        noLockpickFeedback ??= new LockedInteractionFeedbackSettings();
        noLockpickFeedback.Validate();
    }

    /// <summary>
    /// Blocks or restores the player's normal gameplay input while the minigame is active.
    /// </summary>
    private void ApplyInteractorInputBlocked(bool blocked)
    {
        controlLock.SetBlocked(blocked);
        if (!blocked)
            controlLock.Clear();
    }

    /// <summary>
    /// Builds randomized tumbler state for the current lock and refreshes all tumbler visuals.
    /// </summary>
    private void PrepareSessionState(int usableTumblerCount)
    {
        activeTumblerCount = Mathf.Max(0, usableTumblerCount);
        selectedIndex = 0;
        navigationAxisEngaged = false;
        RefreshLockpickUsesText();

        EnsureStateCapacity(activeTumblerCount);
        ResetLockpickBreakVisual(showLockpickPoint: true);
        for (int i = 0; i < resolvedTumblerViews.Count; i++)
        {
            LockpickTumblerView tumblerView = resolvedTumblerViews[i];
            if (tumblerView == null)
                continue;

            bool isUsed = i < activeTumblerCount;
            tumblerView.SetRuntimeVisible(isUsed);
            if (!isUsed)
                continue;

            TumblerRuntimeState tumblerState = tumblerStates[i];
            tumblerState.CurrentDepth = 0f;
            tumblerState.HotspotDepth = UnityEngine.Random.Range(activeDefinition.HotspotMinDepth, activeDefinition.HotspotMaxDepth);
            tumblerState.HotspotShakeRampStartDepth = tumblerState.HotspotDepth * activeDefinition.HotspotShakeRampStartNormalizedDepth;
            tumblerState.HotspotActive = false;
            tumblerState.HotspotExpireTime = float.NegativeInfinity;
            tumblerState.IsLocked = false;

            tumblerView.ResetVisualState(0f, activeDefinition.MaxPushDepth, i == selectedIndex, false);
        }

        MoveSelectorToSelectedTumbler(animate: false);
    }

    /// <summary>
    /// Ensures tumbler runtime storage exists for the number of authored tumbler views.
    /// </summary>
    private void EnsureStateCapacity(int usableTumblerCount)
    {
        int requiredCapacity = Mathf.Max(usableTumblerCount, resolvedTumblerViews.Count);
        while (tumblerStates.Count < requiredCapacity)
            tumblerStates.Add(new TumblerRuntimeState());
    }

    /// <summary>
    /// Shows the panel root and fades it in when a canvas group is available.
    /// </summary>
    private void ShowPanel()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (panelCanvasGroup == null)
        {
            AnimateBlur(true, immediate: false);
            return;
        }

        panelCanvasGroup.alpha = panelFadeDuration > 0f ? 0f : 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
        AnimateBlur(true, immediate: panelFadeDuration <= 0f);

        if (panelFadeDuration <= 0f)
            return;

        panelFadeTween = panelCanvasGroup
            .DOFade(1f, panelFadeDuration)
            .SetEase(panelFadeEase)
            .SetUpdate(true)
            .OnComplete(() => panelFadeTween = null);
    }

    /// <summary>
    /// Hides the panel instantly without waiting for tween completion.
    /// </summary>
    private void HidePanelImmediate()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;
        ResetLockpickBreakVisual(showLockpickPoint: true);
        AnimateBlur(false, immediate: true);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (CanDeactivatePanelRoot())
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Starts fading the panel out and disables interaction immediately.
    /// </summary>
    private void HidePanel()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;
        AnimateBlur(false, immediate: panelFadeDuration <= 0f);

        if (panelCanvasGroup == null)
        {
            if (CanDeactivatePanelRoot())
                panelRoot.SetActive(false);

            return;
        }

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        if (panelFadeDuration <= 0f)
        {
            panelCanvasGroup.alpha = 0f;
            if (CanDeactivatePanelRoot())
                panelRoot.SetActive(false);

            return;
        }

        panelFadeTween = panelCanvasGroup
            .DOFade(0f, panelFadeDuration)
            .SetEase(panelFadeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panelFadeTween = null;
                if (CanDeactivatePanelRoot())
                    panelRoot.SetActive(false);
            });
    }

    /// <summary>
    /// Handles left and right tumbler selection changes from the configured horizontal Rewired axis.
    /// </summary>
    private void HandleHorizontalNavigation(bool pushHeld)
    {
        if (pushHeld && TryGetState(selectedIndex, out TumblerRuntimeState selectedState) && !selectedState.IsLocked)
            return;

        float axis = inputReader != null ? inputReader.GetAxis(horizontalAction) : 0f;
        if (Mathf.Abs(axis) < navigationAxisReleaseThreshold)
        {
            navigationAxisEngaged = false;
            return;
        }

        if (navigationAxisEngaged || Mathf.Abs(axis) < navigationAxisThreshold)
            return;

        navigationAxisEngaged = true;
        MoveSelection(axis > 0f ? 1 : -1);
    }

    /// <summary>
    /// Applies hold and release behavior for the currently selected tumbler.
    /// </summary>
    private void HandlePushInteraction(bool pushHeld)
    {
        if (!TryGetState(selectedIndex, out TumblerRuntimeState selectedState))
            return;

        if (selectedState.IsLocked)
            return;

        if (!PlayerLockpickInventoryUtility.HasAnyLockpickUses(activeInteractorRoot))
            return;

        LockpickTumblerView selectedView = resolvedTumblerViews[selectedIndex];
        if (inputReader != null && inputReader.GetButtonUp(pushAction))
        {
            AttemptReleaseSelectedTumbler(selectedState, selectedView);
            return;
        }

        if (!pushHeld)
            return;

        PushSelectedTumbler(selectedState, selectedView, Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Moves the currently selected tumbler upward and ramps warning feedback before the true hotspot window is reached.
    /// </summary>
    private void PushSelectedTumbler(TumblerRuntimeState tumblerState, LockpickTumblerView tumblerView, float deltaTime)
    {
        if (tumblerState == null || tumblerView == null || activeDefinition == null)
            return;

        tumblerState.CurrentDepth = Mathf.Min(activeDefinition.MaxPushDepth, tumblerState.CurrentDepth + (activeDefinition.PushSpeed * Mathf.Max(0f, deltaTime)));
        tumblerView.SetDepthImmediate(tumblerState.CurrentDepth);
        UpdateTumblerShakeFeedback(tumblerState, tumblerView);

        if (!tumblerState.HotspotActive && tumblerState.CurrentDepth >= tumblerState.HotspotDepth)
        {
            tumblerState.HotspotActive = true;
            tumblerState.HotspotExpireTime = Time.unscaledTime + Mathf.Max(MinimumHotspotWindowDuration, activeDefinition.HotspotWindowDuration);
        }

        if (tumblerState.HotspotActive && Time.unscaledTime >= tumblerState.HotspotExpireTime)
            ResetSelectedTumbler(tumblerState, tumblerView, failureResetDuration);
    }

    /// <summary>
    /// Updates pre-hotspot shake intensity so the tumbler communicates when the ideal release window is approaching.
    /// </summary>
    private void UpdateTumblerShakeFeedback(TumblerRuntimeState tumblerState, LockpickTumblerView tumblerView)
    {
        if (tumblerState == null || tumblerView == null)
            return;

        float shakeStartDepth = Mathf.Min(tumblerState.HotspotShakeRampStartDepth, tumblerState.HotspotDepth);
        float shakeIntensity = shakeStartDepth >= tumblerState.HotspotDepth
            ? (tumblerState.CurrentDepth >= tumblerState.HotspotDepth ? 1f : 0f)
            : Mathf.InverseLerp(shakeStartDepth, tumblerState.HotspotDepth, tumblerState.CurrentDepth);

        tumblerView.SetHotspotShakeIntensity(
            shakeIntensity,
            hotspotShakeCycleDuration,
            hotspotShakeStrength,
            hotspotShakeVibrato,
            hotspotShakeRandomness);

        UpdateShakeLoopSfx(shakeIntensity > 0f);
    }

    /// <summary>
    /// Resolves whether a button release should lock the tumbler or reset it back to the top.
    /// </summary>
    private void AttemptReleaseSelectedTumbler(TumblerRuntimeState tumblerState, LockpickTumblerView tumblerView)
    {
        if (tumblerState == null || tumblerView == null)
            return;

        if (tumblerState.HotspotActive && Time.unscaledTime <= tumblerState.HotspotExpireTime)
        {
            LockSelectedTumbler(tumblerState, tumblerView);
            return;
        }

        ResetSelectedTumbler(tumblerState, tumblerView, earlyReleaseResetDuration);
    }

    /// <summary>
    /// Locks the selected tumbler in place and completes the session once all tumblers are secured.
    /// </summary>
    private void LockSelectedTumbler(TumblerRuntimeState tumblerState, LockpickTumblerView tumblerView)
    {
        if (tumblerState == null || tumblerView == null)
            return;

        tumblerState.IsLocked = true;
        tumblerState.HotspotActive = false;
        tumblerState.HotspotExpireTime = float.NegativeInfinity;
        tumblerView.StopHotspotShake();
        StopShakeLoopSfx();
        tumblerView.SetLocked(true);
        PlayOneShot(tumblerLockSfx, tumblerLockVolume);

        if (AreAllTumblersLocked())
        {
            BeginUnlockedSessionComplete();
            return;
        }

        SelectNextUnlockedTumbler();
    }

    /// <summary>
    /// Resets the selected tumbler only, preserving all already locked tumbler states.
    /// </summary>
    private void ResetSelectedTumbler(TumblerRuntimeState tumblerState, LockpickTumblerView tumblerView, float duration)
    {
        if (tumblerState == null || tumblerView == null)
            return;

        tumblerState.CurrentDepth = 0f;
        tumblerState.HotspotActive = false;
        tumblerState.HotspotExpireTime = float.NegativeInfinity;
        tumblerView.StopHotspotShake();
        StopShakeLoopSfx();
        tumblerView.TweenToDepth(0f, duration, tumblerResetEase);
        PlayOneShot(tumblerFailSfx, tumblerFailVolume);
        HandleLockpickBroken();
    }

    /// <summary>
    /// Begins the final success flow, keeping the panel open briefly before the solved target is notified.
    /// </summary>
    private void BeginUnlockedSessionComplete()
    {
        if (successClosePending)
            return;

        successClosePending = true;
        StopShakeLoopSfx();
        PlayOneShot(lockpickCompleteSfx, lockpickCompleteVolume);

        successDelayTween?.Kill();
        successDelayTween = null;

        if (successCloseDelayDuration <= 0f)
        {
            FinalizeUnlockedSession();
            return;
        }

        successDelayTween = DOVirtual
            .DelayedCall(successCloseDelayDuration, FinalizeUnlockedSession)
            .SetUpdate(true)
            .OnComplete(() => successDelayTween = null);
    }

    /// <summary>
    /// Finalizes the delayed success flow, closes the panel, and unlocks the target.
    /// </summary>
    private void FinalizeUnlockedSession()
    {
        ILockpickSessionTarget unlockedTarget = activeLockpickTarget;
        GameObject interactorRoot = activeInteractorRoot;

        successDelayTween?.Kill();
        successDelayTween = null;
        successClosePending = false;

        unlockedTarget?.NotifyUnlocked(interactorRoot);
        CloseSessionInternal();
        Unlocked?.Invoke(unlockedTarget);
    }

    /// <summary>
    /// Closes the current session, hides the panel, restores player control, and clears runtime state.
    /// </summary>
    private void CloseSessionInternal()
    {
        ILockpickSessionTarget closedTarget = activeLockpickTarget;

        successDelayTween?.Kill();
        successDelayTween = null;
        successClosePending = false;
        outOfLockpicksCloseTween?.Kill();
        outOfLockpicksCloseTween = null;
        outOfLockpicksClosePending = false;
        lockpickBreakVisualTween?.Kill();
        lockpickBreakVisualTween = null;
        lockpickBreakVisualActive = false;
        ResetLockpickBreakVisual(showLockpickPoint: true);
        StopShakeLoopSfx();
        ApplyInteractorInputBlocked(false);
        HidePanel();
        ResetRuntimeState();
        SessionClosed?.Invoke(closedTarget);
    }

    /// <summary>
    /// Immediately closes the active session without leaving fade tweens running during teardown.
    /// </summary>
    private void ForceCloseSessionImmediate()
    {
        if (!IsSessionActive)
            return;

        ILockpickSessionTarget closedTarget = activeLockpickTarget;
        successDelayTween?.Kill();
        successDelayTween = null;
        successClosePending = false;
        outOfLockpicksCloseTween?.Kill();
        outOfLockpicksCloseTween = null;
        outOfLockpicksClosePending = false;
        lockpickBreakVisualTween?.Kill();
        lockpickBreakVisualTween = null;
        lockpickBreakVisualActive = false;
        ResetLockpickBreakVisual(showLockpickPoint: true);
        StopShakeLoopSfx();
        ApplyInteractorInputBlocked(false);
        HidePanelImmediate();
        ResetRuntimeState();
        SessionClosed?.Invoke(closedTarget);
    }

    /// <summary>
    /// Clears active-session references and stops all tumbler feedback owned by the current lock.
    /// </summary>
    private void ResetRuntimeState()
    {
        selectorTween?.Kill();
        selectorTween = null;

        for (int i = 0; i < resolvedTumblerViews.Count; i++)
            resolvedTumblerViews[i]?.StopHotspotShake();

        activeLockpickTarget = null;
        activeLockpickTargetBehaviour = null;
        activeDefinition = null;
        activeInteractorRoot = null;
        activeTumblerCount = 0;
        selectedIndex = 0;
        navigationAxisEngaged = false;
        waitForPushRelease = false;
        outOfLockpicksClosePending = false;
        lockpickBreakVisualActive = false;
        lockpickBreakDelayActive = false;
        lockpickBreakDelayEndsAt = 0f;
        RefreshLockpickUsesText();
    }

    /// <summary>
    /// Caches and configures the dedicated UI audio source used for lockpicking feedback.
    /// </summary>
    private void CacheAudioSource()
    {
        uiAudioSource = GetComponent<AudioSource>();
        if (uiAudioSource == null)
            return;

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
    }

    /// <summary>
    /// Plays a single UI clip through the cached lockpicking audio source when both are available.
    /// </summary>
    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (uiAudioSource == null || clip == null || volume <= 0f)
            return;

        uiAudioSource.volume = 1f;
        uiAudioSource.loop = false;
        uiAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    /// <summary>
    /// Consumes one lockpick use, refreshes the counter, and briefly blocks another push attempt.
    /// </summary>
    private void HandleLockpickBroken()
    {
        if (GameplayConsoleCheatState.InfiniteLockpicks || GameplayConsoleCheatState.InstantLockpicking)
        {
            RefreshLockpickUsesText();
            return;
        }

        if (!PlayerLockpickInventoryUtility.TryConsumeLockpickUse(activeInteractorRoot))
        {
            RefreshLockpickUsesText();
            return;
        }

        RefreshLockpickUsesText();
        PlayLockpickBreakFeedback();
        PlayerLockpickInventoryUtility.TryGetTotalLockpickUses(activeInteractorRoot, out int remainingUses);
        BeginLockpickBreakVisual(remainingUses);
    }

    /// <summary>
    /// Starts the authored broken-lockpick UI animation before the minigame resumes or closes.
    /// </summary>
    private void BeginLockpickBreakVisual(int remainingUses)
    {
        lockpickBreakVisualTween?.Kill();
        lockpickBreakVisualTween = null;

        if (!CanPlayLockpickBreakVisual())
        {
            CompleteLockpickBreakVisual(remainingUses);
            return;
        }

        lockpickBreakVisualActive = true;
        lockpickBreakDelayActive = false;
        ResetLockpickBreakVisual(showLockpickPoint: false);

        lockpickBreakVisualTween = DOTween.Sequence()
            .SetUpdate(true)
            .Join(brokenLockpickPieceOne
                .DOLocalRotate(new Vector3(0f, 0f, brokenLockpickPieceOneRotationZ), brokenLockpickRotationDuration)
                .SetEase(brokenLockpickRotationEase))
            .Join(brokenLockpickPieceTwo
                .DOLocalRotate(new Vector3(0f, 0f, brokenLockpickPieceTwoRotationZ), brokenLockpickRotationDuration)
                .SetEase(brokenLockpickRotationEase))
            .AppendInterval(brokenLockpickDropDelay)
            .Append(brokenLockpickPoint
                .DOAnchorPosY(brokenLockpickDropTargetY, brokenLockpickDropDuration)
                .SetEase(brokenLockpickDropEase))
            .OnComplete(() =>
            {
                lockpickBreakVisualTween = null;
                CompleteLockpickBreakVisual(remainingUses);
            });
    }

    /// <summary>
    /// Finishes the broken-lockpick feedback by either restoring the pick or closing the session.
    /// </summary>
    private void CompleteLockpickBreakVisual(int remainingUses)
    {
        lockpickBreakVisualActive = false;
        lockpickBreakVisualTween?.Kill();
        lockpickBreakVisualTween = null;

        if (remainingUses <= 0)
        {
            BeginOutOfLockpicksClose();
            return;
        }

        ResetLockpickBreakVisual(showLockpickPoint: true);
        if (lockpickBreakRetryDelay <= 0f)
            return;

        lockpickBreakDelayActive = true;
        lockpickBreakDelayEndsAt = Time.unscaledTime + lockpickBreakRetryDelay;
    }

    /// <summary>
    /// Returns whether all broken-lockpick UI references are assigned and ready to animate.
    /// </summary>
    private bool CanPlayLockpickBreakVisual()
    {
        return brokenLockpickPoint != null &&
               brokenLockpickPieceOne != null &&
               brokenLockpickPieceTwo != null;
    }

    /// <summary>
    /// Captures the authored starting transform values so repeated break animations reset cleanly.
    /// </summary>
    private void CaptureBrokenLockpickVisualInitialState()
    {
        if (brokenLockpickPoint == null)
            return;

        brokenLockpickInitialAnchoredPosition = brokenLockpickPoint.anchoredPosition;
        brokenLockpickInitialRotation = brokenLockpickPoint.localRotation;
        brokenLockpickPieceOneInitialRotation = brokenLockpickPieceOne != null
            ? brokenLockpickPieceOne.localRotation
            : Quaternion.identity;
        brokenLockpickPieceTwoInitialRotation = brokenLockpickPieceTwo != null
            ? brokenLockpickPieceTwo.localRotation
            : Quaternion.identity;
        hasBrokenLockpickInitialState = true;
    }

    /// <summary>
    /// Restores the visible and broken pick UI states before a session starts or after feedback completes.
    /// </summary>
    private void ResetLockpickBreakVisual(bool showLockpickPoint)
    {
        if (!hasBrokenLockpickInitialState)
            CaptureBrokenLockpickVisualInitialState();

        if (brokenLockpickPoint == null)
        {
            if (lockpickPoint != null)
                lockpickPoint.SetActive(showLockpickPoint);

            return;
        }

        if (showLockpickPoint)
        {
            if (hasBrokenLockpickInitialState)
            {
                brokenLockpickPoint.anchoredPosition = brokenLockpickInitialAnchoredPosition;
                brokenLockpickPoint.localRotation = brokenLockpickInitialRotation;
            }
        }
        else
        {
            SyncBrokenLockpickPointToLockpickPoint();
        }

        if (brokenLockpickPieceOne != null)
            brokenLockpickPieceOne.localRotation = hasBrokenLockpickInitialState
                ? brokenLockpickPieceOneInitialRotation
                : Quaternion.identity;

        if (brokenLockpickPieceTwo != null)
            brokenLockpickPieceTwo.localRotation = hasBrokenLockpickInitialState
                ? brokenLockpickPieceTwoInitialRotation
                : Quaternion.identity;

        if (lockpickPoint != null)
            lockpickPoint.SetActive(showLockpickPoint);

        brokenLockpickPoint.gameObject.SetActive(!showLockpickPoint);
    }

    /// <summary>
    /// Aligns the broken lockpick parent with the current visible lockpick so break feedback starts from the same UI point.
    /// </summary>
    private void SyncBrokenLockpickPointToLockpickPoint()
    {
        if (lockpickPoint == null || brokenLockpickPoint == null)
            return;

        Transform lockpickTransform = lockpickPoint.transform;
        brokenLockpickPoint.position = lockpickTransform.position;
        brokenLockpickPoint.rotation = lockpickTransform.rotation;
        brokenLockpickPoint.localScale = lockpickTransform.localScale;
    }

    /// <summary>
    /// Starts a delayed close after the player breaks the last available lockpick.
    /// </summary>
    private void BeginOutOfLockpicksClose()
    {
        if (outOfLockpicksClosePending)
            return;

        outOfLockpicksClosePending = true;
        lockpickBreakDelayActive = false;
        StopShakeLoopSfx();
        outOfLockpicksCloseTween?.Kill();
        outOfLockpicksCloseTween = null;

        if (outOfLockpicksCloseDelayDuration <= 0f)
        {
            FinalizeOutOfLockpicksClose();
            return;
        }

        outOfLockpicksCloseTween = DOVirtual
            .DelayedCall(outOfLockpicksCloseDelayDuration, FinalizeOutOfLockpicksClose)
            .SetUpdate(true)
            .OnComplete(() => outOfLockpicksCloseTween = null);
    }

    /// <summary>
    /// Closes the lockpicking session after the player's lockpick inventory reaches zero.
    /// </summary>
    private void FinalizeOutOfLockpicksClose()
    {
        outOfLockpicksCloseTween?.Kill();
        outOfLockpicksCloseTween = null;
        outOfLockpicksClosePending = false;
        CloseSessionInternal();
    }

    /// <summary>
    /// Plays world-space break SFX and emits a noise event from the lock target position.
    /// </summary>
    private void PlayLockpickBreakFeedback()
    {
        Vector3 feedbackPosition = activeLockpickTargetBehaviour != null
            ? activeLockpickTargetBehaviour.transform.position
            : transform.position;

        GameObject noiseSource = activeLockpickTargetBehaviour != null
            ? activeLockpickTargetBehaviour.gameObject
            : gameObject;

        if (lockpickBreakNoise > 0f)
            NoiseManager.EmitNoise(feedbackPosition, lockpickBreakNoise, lockpickBreakNoiseType, noiseSource, lockpickBreakExtremeNoise);

        WorldSfxManager.Instance?.PlayClipSetAt(feedbackPosition, lockpickBreakSfx, lockpickBreakNoiseType, lockpickBreakSfxVolume);
    }

    /// <summary>
    /// Updates the lockpick uses text with the active player's total remaining lockpick charges.
    /// </summary>
    private void RefreshLockpickUsesText()
    {
        if (lockpickUsesText == null)
            return;

        if (GameplayConsoleCheatState.InfiniteLockpicks || GameplayConsoleCheatState.InstantLockpicking)
        {
            lockpickUsesText.text = "INF";
            return;
        }

        int totalUses = 0;
        PlayerLockpickInventoryUtility.TryGetTotalLockpickUses(activeInteractorRoot, out totalUses);
        lockpickUsesText.text = totalUses.ToString();
    }

    /// <summary>
    /// Starts or stops the looping shake warning clip based on whether the selected tumbler is currently in its warning range.
    /// </summary>
    private void UpdateShakeLoopSfx(bool shouldPlay)
    {
        if (uiAudioSource == null || tumblerShakingLoopSfx == null || successClosePending || outOfLockpicksClosePending)
            return;

        if (!shouldPlay)
        {
            StopShakeLoopSfx();
            return;
        }

        if (uiAudioSource.isPlaying && uiAudioSource.loop && uiAudioSource.clip == tumblerShakingLoopSfx)
            return;

        uiAudioSource.clip = tumblerShakingLoopSfx;
        uiAudioSource.loop = true;
        uiAudioSource.volume = Mathf.Clamp01(tumblerShakingLoopVolume);
        uiAudioSource.Play();
    }

    /// <summary>
    /// Stops the looping shake warning clip without interrupting one-shot feedback that is not using the loop channel.
    /// </summary>
    private void StopShakeLoopSfx()
    {
        if (uiAudioSource == null || !uiAudioSource.loop)
            return;

        uiAudioSource.Stop();
        uiAudioSource.loop = false;
        uiAudioSource.clip = null;
        uiAudioSource.volume = 1f;
    }

    /// <summary>
    /// Animates the shared Bokeh blur in or out using this lockpick panel's authored values.
    /// </summary>
    private void AnimateBlur(bool blurred, bool immediate)
    {
        blurController.Animate(
            blurred,
            immediate,
            targetVolume,
            activeInteractorRoot != null ? activeInteractorRoot : gameObject,
            blurTransitionDuration,
            blurBokehFocusDistance,
            blurBokehAperture,
            blurBokehFocalLength,
            blurBokehBladeCount,
            blurBokehBladeCurvature,
            blurBokehBladeRotation);
    }

    /// <summary>
    /// Returns whether the panel root may safely be deactivated without disabling this controller.
    /// </summary>
    private bool CanDeactivatePanelRoot()
    {
        return panelRoot != null && panelRoot != gameObject;
    }

    /// <summary>
    /// Moves the selector one fixed tumbler step to the left or right while clamping to valid bounds.
    /// </summary>
    private void MoveSelection(int direction)
    {
        if (activeTumblerCount <= 0)
            return;

        int nextIndex = Mathf.Clamp(selectedIndex + Math.Sign(direction), 0, activeTumblerCount - 1);
        if (nextIndex == selectedIndex)
            return;

        SetSelectedIndex(nextIndex, animateSelector: true);
    }

    /// <summary>
    /// Selects the next available unlocked tumbler after a successful lock.
    /// </summary>
    private void SelectNextUnlockedTumbler()
    {
        if (activeTumblerCount <= 0)
            return;

        for (int offset = 1; offset < activeTumblerCount; offset++)
        {
            int candidateIndex = (selectedIndex + offset) % activeTumblerCount;
            if (TryGetState(candidateIndex, out TumblerRuntimeState candidateState) && candidateState != null && !candidateState.IsLocked)
            {
                SetSelectedIndex(candidateIndex, animateSelector: true);
                return;
            }
        }
    }

    /// <summary>
    /// Applies a new selected tumbler index and refreshes both highlight and selector position.
    /// </summary>
    private void SetSelectedIndex(int newIndex, bool animateSelector)
    {
        selectedIndex = Mathf.Clamp(newIndex, 0, Mathf.Max(0, activeTumblerCount - 1));
        RefreshSelectedVisuals();
        MoveSelectorToSelectedTumbler(animateSelector);
    }

    /// <summary>
    /// Refreshes the selected-state highlight across all active tumbler views.
    /// </summary>
    private void RefreshSelectedVisuals()
    {
        for (int i = 0; i < activeTumblerCount && i < resolvedTumblerViews.Count; i++)
            resolvedTumblerViews[i]?.SetSelected(i == selectedIndex);
    }

    /// <summary>
    /// Moves the selector rect to the currently selected tumbler's configured selector anchor.
    /// </summary>
    private void MoveSelectorToSelectedTumbler(bool animate)
    {
        if (selectorRect == null ||
            selectorReferenceSpace == null ||
            selectedIndex < 0 ||
            selectedIndex >= activeTumblerCount ||
            selectedIndex >= resolvedTumblerViews.Count)
        {
            return;
        }

        LockpickTumblerView selectedView = resolvedTumblerViews[selectedIndex];
        if (selectedView == null)
            return;

        Vector2 targetPosition = selectedView.GetSelectorLocalPosition(selectorReferenceSpace, ResolveCanvasCamera());
        selectorTween?.Kill();
        selectorTween = null;

        if (!animate || selectorMoveDuration <= 0f)
        {
            selectorRect.anchoredPosition = targetPosition;
            return;
        }

        selectorTween = selectorRect
            .DOAnchorPos(targetPosition, selectorMoveDuration)
            .SetEase(selectorMoveEase)
            .SetUpdate(true)
            .OnComplete(() => selectorTween = null);
    }

    /// <summary>
    /// Keeps the selector aligned with the selected tumbler while that tumbler is moving or shaking.
    /// </summary>
    private void TrackSelectorToAnimatedTumbler()
    {
        if (selectorTween != null ||
            selectorRect == null ||
            selectorReferenceSpace == null ||
            selectedIndex < 0 ||
            selectedIndex >= activeTumblerCount ||
            selectedIndex >= resolvedTumblerViews.Count)
        {
            return;
        }

        LockpickTumblerView selectedView = resolvedTumblerViews[selectedIndex];
        if (selectedView == null || !selectedView.NeedsSelectorTracking)
            return;

        selectorRect.anchoredPosition = selectedView.GetSelectorLocalPosition(selectorReferenceSpace, ResolveCanvasCamera());
    }

    /// <summary>
    /// Returns the camera used by the target canvas when converting between UI coordinate spaces.
    /// </summary>
    private Camera ResolveCanvasCamera()
    {
        return targetCanvas != null ? targetCanvas.worldCamera : null;
    }

    /// <summary>
    /// Returns whether every active tumbler has been successfully locked.
    /// </summary>
    private bool AreAllTumblersLocked()
    {
        for (int i = 0; i < activeTumblerCount; i++)
        {
            if (!TryGetState(i, out TumblerRuntimeState tumblerState) || tumblerState == null || !tumblerState.IsLocked)
                return false;
        }

        return activeTumblerCount > 0;
    }

    /// <summary>
    /// Resolves a tumbler runtime state by index only when it falls inside the current active tumbler range.
    /// </summary>
    private bool TryGetState(int tumblerIndex, out TumblerRuntimeState tumblerState)
    {
        tumblerState = null;
        if (tumblerIndex < 0 || tumblerIndex >= activeTumblerCount || tumblerIndex >= tumblerStates.Count)
            return false;

        tumblerState = tumblerStates[tumblerIndex];
        return tumblerState != null;
    }
}

}
