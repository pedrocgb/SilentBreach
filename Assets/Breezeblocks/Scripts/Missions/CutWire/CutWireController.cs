using System;
using System.Collections.Generic;
using Breezeblocks.Input;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Cut Wire/Cut Wire Controller")]
public sealed class CutWireController : MonoBehaviour
{
    private enum SessionOutcome
    {
        None,
        Success,
        Failure
    }

    private const float MinimumDuration = 0f;

    private static CutWireController activeInstance;

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string closeAction = "Cancel Throw";

    [FoldoutGroup("References")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("References")]
    [SerializeField] private Button closeButton;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text fuseBoxNameText;

    [FoldoutGroup("References")]
    [SerializeField] private Image companyLogoImage;

    [FoldoutGroup("References")]
    [SerializeField] private CutWireFuseBoxDoorView fuseBoxDoorView;

    [FoldoutGroup("References")]
    [SerializeField] private Volume targetVolume;

    [FoldoutGroup("References"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<CutWireWireView> wireViews = new();

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float panelFadeDuration = 0.16f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease panelFadeEase = Ease.InOutSine;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float outcomeCloseDelay = 0.6f;

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
    [SerializeField] private AudioClip cutWireSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip successSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip failureSfx;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float cutWireVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float successVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float failureVolume = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSessionActive => activeTarget != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsOutcomePending => pendingOutcome != SessionOutcome.None;

    public static bool HasRegisteredInstance => activeInstance != null && activeInstance.CanStartSessions;

    public event Action<ICutWireSessionTarget> Succeeded;
    public event Action<ICutWireSessionTarget> Failed;
    public event Action<ICutWireSessionTarget> SessionClosed;

    private readonly List<CutWireWireView> resolvedWireViews = new();
    private readonly PlayerMinigameControlLock controlLock = new();
    private readonly MinigameBokehBlurController blurController = new();

    private AudioSource uiAudioSource;
    private CanvasGroup panelCanvasGroup;
    private IPlayerInputReader inputReader;
    private Tween panelFadeTween;
    private Tween outcomeDelayTween;
    private ICutWireSessionTarget activeTarget;
    private MonoBehaviour activeTargetBehaviour;
    private CutWireMinigameDefinition activeDefinition;
    private GameObject activeInteractorRoot;
    private SessionOutcome pendingOutcome;
    private bool isClosing;
    private bool CanStartSessions => panelRoot != null && resolvedWireViews.Count > 0;
    private bool CanCutWires => IsSessionActive &&
                                !IsOutcomePending &&
                                !isClosing &&
                                (fuseBoxDoorView == null || fuseBoxDoorView.IsOpen);

    /// <summary>
    /// Caches same-object services, resolves wired views, and applies a hidden initial panel state.
    /// </summary>
    private void Awake()
    {
        uiAudioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
        ResolvePanelCanvasGroup();
        RebuildResolvedWireViews();
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
        HidePanelImmediate();
    }

    /// <summary>
    /// Registers this shared UI controller and subscribes to all configured wire views.
    /// </summary>
    private void OnEnable()
    {
        ResolvePanelCanvasGroup();
        RebuildResolvedWireViews();
        SubscribeWireViews();
        SubscribeFuseBoxDoorView();
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseActiveSession);

        if (activeInstance != null && activeInstance != this)
            Debug.LogWarning("Multiple CutWireController instances are active. Newest instance will be used.", this);

        activeInstance = this;
    }

    /// <summary>
    /// Restores player control and clears all owned callbacks and tweens during teardown.
    /// </summary>
    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseActiveSession);

        UnsubscribeFuseBoxDoorView();
        UnsubscribeWireViews();
        ForceCloseSessionImmediate();
        KillOwnedTweens();

        if (activeInstance == this)
            activeInstance = null;
    }

    /// <summary>
    /// Clamps designer-authored animation and audio values.
    /// </summary>
    private void OnValidate()
    {
        panelFadeDuration = Mathf.Max(MinimumDuration, panelFadeDuration);
        outcomeCloseDelay = Mathf.Max(MinimumDuration, outcomeCloseDelay);
        blurTransitionDuration = Mathf.Max(MinimumDuration, blurTransitionDuration);
        blurBokehFocusDistance = Mathf.Max(0f, blurBokehFocusDistance);
        blurBokehAperture = Mathf.Max(0.1f, blurBokehAperture);
        blurBokehFocalLength = Mathf.Max(1f, blurBokehFocalLength);
        blurBokehBladeCount = Mathf.Clamp(blurBokehBladeCount, 3, 9);
        blurBokehBladeCurvature = Mathf.Clamp01(blurBokehBladeCurvature);
        blurBokehBladeRotation = Mathf.Clamp(blurBokehBladeRotation, -180f, 180f);
        cutWireVolume = Mathf.Clamp01(cutWireVolume);
        successVolume = Mathf.Clamp01(successVolume);
        failureVolume = Mathf.Clamp01(failureVolume);
        ResolvePanelCanvasGroup();
        RebuildResolvedWireViews();
    }

    /// <summary>
    /// Polls only the Rewired close action while a cut-wire session owns gameplay input.
    /// </summary>
    private void Update()
    {
        if (!IsSessionActive)
            return;

        if (isClosing)
            return;

        if (activeTargetBehaviour == null || !activeTargetBehaviour.isActiveAndEnabled)
        {
            CloseSessionInternal();
            return;
        }

        if (IsOutcomePending)
            return;

        inputReader ??= new RewiredPlayerInputReader(rewiredPlayerId);
        if (inputReader.IsReady && inputReader.GetButtonDown(closeAction))
            CloseSessionInternal();
    }

    /// <summary>
    /// Starts a session through the currently active shared cut-wire controller.
    /// </summary>
    public static bool TryBeginActiveSession(GameObject interactorRoot, ICutWireSessionTarget target)
    {
        return activeInstance != null && activeInstance.TryBeginSession(interactorRoot, target);
    }

    /// <summary>
    /// Opens this controller for one independent scene target when its setup is valid.
    /// </summary>
    public bool TryBeginSession(GameObject interactorRoot, ICutWireSessionTarget target)
    {
        if (IsSessionActive ||
            interactorRoot == null ||
            target == null ||
            target.Definition == null ||
            target is not MonoBehaviour targetBehaviour ||
            !targetBehaviour.isActiveAndEnabled)
        {
            return false;
        }

        ResolvePanelCanvasGroup();
        RebuildResolvedWireViews();
        int activeWireCount = Mathf.Min(target.Definition.WireCount, resolvedWireViews.Count);
        if (panelRoot == null || activeWireCount < target.Definition.WireCount)
            return false;

        activeTarget = target;
        activeTargetBehaviour = targetBehaviour;
        activeDefinition = target.Definition;
        activeInteractorRoot = interactorRoot;
        pendingOutcome = SessionOutcome.None;
        isClosing = false;
        controlLock.Bind(interactorRoot);
        controlLock.SetBlocked(true);
        SetCursorVisible(true);
        SetHeaderVisible(true);
        RefreshHeader();
        fuseBoxDoorView?.ResetClosedImmediate();
        RefreshWireViews();
        fuseBoxDoorView?.SetInteractionEnabled(true);
        ShowPanel();
        return true;
    }

    /// <summary>
    /// Closes an active non-resolved session while preserving every already-cut target wire.
    /// </summary>
    public void CloseActiveSession()
    {
        if (IsSessionActive && !IsOutcomePending && !isClosing)
            CloseSessionInternal();
    }

    /// <summary>
    /// Handles one intact wire click and resolves success or failure when appropriate.
    /// </summary>
    private void HandleWireCutRequested(CutWireWireView wireView)
    {
        if (!CanCutWires || wireView == null || wireView.IsCut)
            return;

        int wireIndex = wireView.WireIndex;
        if (!activeDefinition.TryGetWire(wireIndex, out CutWireSlotDefinition wireDefinition))
            return;

        activeTarget.NotifyWireCut(wireIndex);
        wireView.SetCut(true);
        PlayOneShot(cutWireSfx, cutWireVolume);

        if (!wireDefinition.MustBeCut)
        {
            BeginOutcome(SessionOutcome.Failure);
            return;
        }

        if (activeDefinition.AreAllRequiredWiresCut(activeTarget.CutStates))
            BeginOutcome(SessionOutcome.Success);
    }

    /// <summary>
    /// Starts delayed outcome feedback and prevents further wire clicks or manual closure.
    /// </summary>
    private void BeginOutcome(SessionOutcome outcome)
    {
        if (outcome == SessionOutcome.None || IsOutcomePending)
            return;

        pendingOutcome = outcome;
        SetWireInteractionEnabled(false);
        PlayOneShot(outcome == SessionOutcome.Success ? successSfx : failureSfx, outcome == SessionOutcome.Success ? successVolume : failureVolume);

        outcomeDelayTween?.Kill();
        if (outcomeCloseDelay <= 0f)
        {
            FinalizeOutcome();
            return;
        }

        outcomeDelayTween = DOVirtual
            .DelayedCall(outcomeCloseDelay, FinalizeOutcome)
            .SetUpdate(true)
            .OnComplete(() => outcomeDelayTween = null);
    }

    /// <summary>
    /// Applies the pending target result after feedback finishes, then closes the session.
    /// </summary>
    private void FinalizeOutcome()
    {
        ICutWireSessionTarget resolvedTarget = activeTarget;
        GameObject interactorRoot = activeInteractorRoot;
        SessionOutcome outcome = pendingOutcome;

        outcomeDelayTween?.Kill();
        outcomeDelayTween = null;

        if (outcome == SessionOutcome.Success)
        {
            resolvedTarget?.NotifySucceeded(interactorRoot);
            Succeeded?.Invoke(resolvedTarget);
        }
        else if (outcome == SessionOutcome.Failure)
        {
            resolvedTarget?.NotifyFailed(interactorRoot);
            Failed?.Invoke(resolvedTarget);
        }

        CloseSessionInternal();
    }

    /// <summary>
    /// Starts the close sequence while keeping player control blocked until all UI animation finishes.
    /// </summary>
    private void CloseSessionInternal()
    {
        if (!IsSessionActive || isClosing)
            return;

        isClosing = true;
        outcomeDelayTween?.Kill();
        outcomeDelayTween = null;
        SetWireInteractionEnabled(false);
        fuseBoxDoorView?.SetInteractionEnabled(false);
        SetPanelInteractionEnabled(false);

        if (fuseBoxDoorView != null)
        {
            fuseBoxDoorView.Close(HidePanelAndCompleteClose);
            return;
        }

        HidePanelAndCompleteClose();
    }

    /// <summary>
    /// Restores player control immediately without leaving UI tweens alive during teardown.
    /// </summary>
    private void ForceCloseSessionImmediate()
    {
        if (!IsSessionActive)
            return;

        ICutWireSessionTarget closedTarget = activeTarget;
        outcomeDelayTween?.Kill();
        outcomeDelayTween = null;
        controlLock.SetBlocked(false);
        controlLock.Clear();
        HidePanelImmediate();
        SetCursorVisible(false);
        ClearSessionState();
        SessionClosed?.Invoke(closedTarget);
    }

    /// <summary>
    /// Fades the panel only after the fuse-box door has fully returned to its closed position.
    /// </summary>
    private void HidePanelAndCompleteClose()
    {
        HidePanel(CompleteSessionClose);
    }

    /// <summary>
    /// Restores gameplay control and clears session ownership after every close animation finishes.
    /// </summary>
    private void CompleteSessionClose()
    {
        if (!IsSessionActive)
            return;

        ICutWireSessionTarget closedTarget = activeTarget;
        controlLock.SetBlocked(false);
        controlLock.Clear();
        SetCursorVisible(false);
        ClearSessionState();
        SessionClosed?.Invoke(closedTarget);
    }

    /// <summary>
    /// Clears runtime session references without modifying target-owned cut progress.
    /// </summary>
    private void ClearSessionState()
    {
        activeTarget = null;
        activeTargetBehaviour = null;
        activeDefinition = null;
        activeInteractorRoot = null;
        pendingOutcome = SessionOutcome.None;
        isClosing = false;
    }

    /// <summary>
    /// Updates fuse-box identity fields from the active definition.
    /// </summary>
    private void RefreshHeader()
    {
        if (fuseBoxNameText != null)
            fuseBoxNameText.text = activeDefinition != null ? activeDefinition.FuseBoxName : string.Empty;

        if (companyLogoImage != null)
        {
            companyLogoImage.sprite = activeDefinition != null ? activeDefinition.CompanyLogo : null;
            companyLogoImage.enabled = companyLogoImage.sprite != null;
        }
    }

    /// <summary>
    /// Rebuilds all active wire slots from target-owned persistent cut state.
    /// </summary>
    private void RefreshWireViews()
    {
        int activeWireCount = activeDefinition != null ? activeDefinition.WireCount : 0;
        for (int i = 0; i < resolvedWireViews.Count; i++)
        {
            CutWireWireView wireView = resolvedWireViews[i];
            if (wireView == null)
                continue;

            if (i >= activeWireCount || activeDefinition == null)
            {
                wireView.SetRuntimeVisible(false);
                continue;
            }

            CutWireSlotDefinition wireDefinition = null;
            if (!activeDefinition.TryGetWire(i, out wireDefinition) || wireDefinition == null)
            {
                wireView.SetRuntimeVisible(false);
                continue;
            }

            wireView.SetRuntimeVisible(true);
            bool isCut = activeTarget.CutStates != null && i < activeTarget.CutStates.Count && activeTarget.CutStates[i];
            wireView.Configure(i, wireDefinition.Color, isCut);
            wireView.SetInteractionEnabled(CanCutWires);
        }
    }

    /// <summary>
    /// Enables or disables every visible wire button for outcome locking.
    /// </summary>
    private void SetWireInteractionEnabled(bool enabled)
    {
        for (int i = 0; i < resolvedWireViews.Count; i++)
            resolvedWireViews[i]?.SetInteractionEnabled(enabled);
    }

    /// <summary>
    /// Rebuilds the non-null wire view cache while preserving designer-authored ordering.
    /// </summary>
    private void RebuildResolvedWireViews()
    {
        resolvedWireViews.Clear();
        for (int i = 0; i < wireViews.Count; i++)
        {
            if (wireViews[i] != null)
                resolvedWireViews.Add(wireViews[i]);
        }
    }

    /// <summary>
    /// Subscribes this controller to configured wire click requests.
    /// </summary>
    private void SubscribeWireViews()
    {
        for (int i = 0; i < resolvedWireViews.Count; i++)
        {
            resolvedWireViews[i].CutRequested -= HandleWireCutRequested;
            resolvedWireViews[i].CutRequested += HandleWireCutRequested;
        }
    }

    /// <summary>
    /// Removes wire click subscriptions owned by this controller.
    /// </summary>
    private void UnsubscribeWireViews()
    {
        for (int i = 0; i < resolvedWireViews.Count; i++)
            resolvedWireViews[i].CutRequested -= HandleWireCutRequested;
    }

    /// <summary>
    /// Subscribes to the optional fuse-box door so it can reveal wires and control header visibility.
    /// </summary>
    private void SubscribeFuseBoxDoorView()
    {
        if (fuseBoxDoorView == null)
            return;

        fuseBoxDoorView.Opened -= HandleFuseBoxDoorOpened;
        fuseBoxDoorView.Opened += HandleFuseBoxDoorOpened;
        fuseBoxDoorView.HeaderVisibilityChanged -= SetHeaderVisible;
        fuseBoxDoorView.HeaderVisibilityChanged += SetHeaderVisible;
    }

    /// <summary>
    /// Removes callbacks owned by this controller from the optional fuse-box door view.
    /// </summary>
    private void UnsubscribeFuseBoxDoorView()
    {
        if (fuseBoxDoorView == null)
            return;

        fuseBoxDoorView.Opened -= HandleFuseBoxDoorOpened;
        fuseBoxDoorView.HeaderVisibilityChanged -= SetHeaderVisible;
    }

    /// <summary>
    /// Enables cutting only after the player has fully opened the fuse-box door.
    /// </summary>
    private void HandleFuseBoxDoorOpened()
    {
        if (CanCutWires)
            SetWireInteractionEnabled(true);
    }

    /// <summary>
    /// Shows or hides fuse-box identity objects when the door crosses its configured pivot threshold.
    /// </summary>
    private void SetHeaderVisible(bool visible)
    {
        if (fuseBoxNameText != null)
            fuseBoxNameText.gameObject.SetActive(visible);

        if (companyLogoImage != null)
            companyLogoImage.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Resolves the panel CanvasGroup from the externally assigned panel root.
    /// </summary>
    private void ResolvePanelCanvasGroup()
    {
        panelCanvasGroup = panelRoot != null ? panelRoot.GetComponent<CanvasGroup>() : null;
    }

    /// <summary>
    /// Activates and fades in the minigame panel using unscaled time.
    /// </summary>
    private void ShowPanel()
    {
        if (panelRoot == null)
            return;

        panelFadeTween?.Kill();
        panelFadeTween = null;
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
    /// Fades out the minigame panel before deactivating its external root and publishing completion.
    /// </summary>
    private void HidePanel(Action completed)
    {
        if (panelRoot == null)
        {
            completed?.Invoke();
            return;
        }

        panelFadeTween?.Kill();
        panelFadeTween = null;
        AnimateBlur(false, immediate: panelFadeDuration <= 0f);
        if (panelCanvasGroup == null || panelFadeDuration <= 0f)
        {
            HidePanelImmediate();
            completed?.Invoke();
            return;
        }

        SetPanelInteractionEnabled(false);
        panelFadeTween = panelCanvasGroup
            .DOFade(0f, panelFadeDuration)
            .SetEase(panelFadeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panelFadeTween = null;
                if (panelRoot != null && panelRoot != gameObject)
                    panelRoot.SetActive(false);

                completed?.Invoke();
            });
    }

    /// <summary>
    /// Applies a hidden non-interactable panel state without animation.
    /// </summary>
    private void HidePanelImmediate()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;
        AnimateBlur(false, immediate: true);
        fuseBoxDoorView?.ResetClosedImmediate();
        SetCursorVisible(false);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            SetPanelInteractionEnabled(false);
        }

        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Enables or disables all panel raycasts without changing its current fade value.
    /// </summary>
    private void SetPanelInteractionEnabled(bool enabled)
    {
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.interactable = enabled;
        panelCanvasGroup.blocksRaycasts = enabled;
    }

    /// <summary>
    /// Applies the expected system cursor state while the mouse-driven cut-wire panel owns input.
    /// </summary>
    private static void SetCursorVisible(bool visible)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = visible;
    }

    /// <summary>
    /// Animates the shared Bokeh blur in or out using this cut-wire panel's authored values.
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
    /// Configures the same-object AudioSource for non-spatial UI feedback.
    /// </summary>
    private void ConfigureAudioSource()
    {
        if (uiAudioSource == null)
            return;

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
    }

    /// <summary>
    /// Plays one optional minigame UI clip through the cached AudioSource.
    /// </summary>
    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (uiAudioSource != null && clip != null && volume > 0f)
            uiAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    /// <summary>
    /// Stops and clears every tween owned by this UI controller.
    /// </summary>
    private void KillOwnedTweens()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;
        outcomeDelayTween?.Kill();
        outcomeDelayTween = null;
        blurController.KillTween();
    }
}

}
