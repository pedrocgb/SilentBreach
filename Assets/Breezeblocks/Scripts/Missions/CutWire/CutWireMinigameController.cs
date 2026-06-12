using System;
using System.Collections.Generic;
using Breezeblocks.Input;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Cut Wire/Minigame Controller")]
public sealed class CutWireMinigameController : MonoBehaviour
{
    private enum SessionOutcome
    {
        None,
        Success,
        Failure
    }

    private const float MinimumDuration = 0f;

    private static CutWireMinigameController activeInstance;

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

    [FoldoutGroup("References"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<CutWireWireView> wireViews = new();

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float panelFadeDuration = 0.16f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease panelFadeEase = Ease.InOutSine;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float outcomeCloseDelay = 0.6f;

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
    private bool CanStartSessions => panelRoot != null && resolvedWireViews.Count > 0;

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
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseActiveSession);

        if (activeInstance != null && activeInstance != this)
            Debug.LogWarning("Multiple CutWireMinigameController instances are active. Newest instance will be used.", this);

        activeInstance = this;
    }

    /// <summary>
    /// Restores player control and clears all owned callbacks and tweens during teardown.
    /// </summary>
    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseActiveSession);

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
        controlLock.Bind(interactorRoot);
        controlLock.SetBlocked(true);
        RefreshHeader();
        RefreshWireViews();
        ShowPanel();
        return true;
    }

    /// <summary>
    /// Closes an active non-resolved session while preserving every already-cut target wire.
    /// </summary>
    public void CloseActiveSession()
    {
        if (IsSessionActive && !IsOutcomePending)
            CloseSessionInternal();
    }

    /// <summary>
    /// Handles one intact wire click and resolves success or failure when appropriate.
    /// </summary>
    private void HandleWireCutRequested(CutWireWireView wireView)
    {
        if (!IsSessionActive || IsOutcomePending || wireView == null || wireView.IsCut)
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
    /// Restores player control, fades the panel out, and clears transient session ownership.
    /// </summary>
    private void CloseSessionInternal()
    {
        if (!IsSessionActive)
            return;

        ICutWireSessionTarget closedTarget = activeTarget;
        outcomeDelayTween?.Kill();
        outcomeDelayTween = null;
        controlLock.SetBlocked(false);
        controlLock.Clear();
        HidePanel();
        ClearSessionState();
        SessionClosed?.Invoke(closedTarget);
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
            wireView.SetInteractionEnabled(!IsOutcomePending);
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
            return;

        panelCanvasGroup.alpha = panelFadeDuration > 0f ? 0f : 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
        if (panelFadeDuration <= 0f)
            return;

        panelFadeTween = panelCanvasGroup
            .DOFade(1f, panelFadeDuration)
            .SetEase(panelFadeEase)
            .SetUpdate(true)
            .OnComplete(() => panelFadeTween = null);
    }

    /// <summary>
    /// Fades out the minigame panel before deactivating its external root.
    /// </summary>
    private void HidePanel()
    {
        if (panelRoot == null)
            return;

        panelFadeTween?.Kill();
        panelFadeTween = null;
        if (panelCanvasGroup == null || panelFadeDuration <= 0f)
        {
            HidePanelImmediate();
            return;
        }

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        panelFadeTween = panelCanvasGroup
            .DOFade(0f, panelFadeDuration)
            .SetEase(panelFadeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panelFadeTween = null;
                if (panelRoot != null && panelRoot != gameObject)
                    panelRoot.SetActive(false);
            });
    }

    /// <summary>
    /// Applies a hidden non-interactable panel state without animation.
    /// </summary>
    private void HidePanelImmediate()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (panelRoot != null && panelRoot != gameObject)
            panelRoot.SetActive(false);
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
    }
}

}
