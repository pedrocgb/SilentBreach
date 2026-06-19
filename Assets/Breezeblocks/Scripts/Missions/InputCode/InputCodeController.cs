using System;
using System.Collections.Generic;
using System.Text;
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
[AddComponentMenu("Breezeblocks/Missions/Input Code/Input Code Controller")]
public sealed class InputCodeController : MonoBehaviour
{
    private enum SessionOutcome
    {
        None,
        Success,
        Failure
    }

    private const float MinimumDuration = 0f;

    private static InputCodeController activeInstance;

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string closeAction = "Cancel Throw";

    [FoldoutGroup("References")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("References")]
    [SerializeField] private Button closeButton;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text inputText;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text attemptsText;

    [FoldoutGroup("References")]
    [SerializeField] private Volume targetVolume;

    [FoldoutGroup("References"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<InputCodeButtonView> buttonViews = new();

    [FoldoutGroup("Text")]
    [SerializeField] private string attemptsFormat = "Tentativas: {0}";

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
    [SerializeField] private AudioClip numberClickSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip eraseClickSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip confirmClickSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip successSfx;

    [FoldoutGroup("Audio"), AssetsOnly]
    [SerializeField] private AudioClip failureSfx;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float numberClickVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float eraseClickVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float confirmClickVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float successVolume = 1f;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float failureVolume = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSessionActive => activeTarget != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsOutcomePending => pendingOutcome != SessionOutcome.None;

    public static bool HasRegisteredInstance => activeInstance != null && activeInstance.CanStartSessions;

    public event Action<IInputCodeSessionTarget> Succeeded;
    public event Action<IInputCodeSessionTarget> Failed;
    public event Action<IInputCodeSessionTarget> SessionClosed;

    private readonly List<InputCodeButtonView> resolvedButtonViews = new();
    private readonly PlayerMinigameControlLock controlLock = new();
    private readonly MinigameBokehBlurController blurController = new();
    private readonly StringBuilder currentInput = new();

    private AudioSource uiAudioSource;
    private CanvasGroup panelCanvasGroup;
    private IPlayerInputReader inputReader;
    private Tween panelFadeTween;
    private Tween outcomeDelayTween;
    private IInputCodeSessionTarget activeTarget;
    private MonoBehaviour activeTargetBehaviour;
    private InputCodeMinigameDefinition activeDefinition;
    private GameObject activeInteractorRoot;
    private SessionOutcome pendingOutcome;
    private bool isClosing;
    private bool CanStartSessions => panelRoot != null && resolvedButtonViews.Count > 0;

    /// <summary>
    /// Caches same-object services, input reader, and reusable button references.
    /// </summary>
    private void Awake()
    {
        uiAudioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
        ResolvePanelCanvasGroup();
        RebuildResolvedButtonViews();
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
        HidePanelImmediate();
    }

    /// <summary>
    /// Registers this controller as the shared input-code minigame service.
    /// </summary>
    private void OnEnable()
    {
        ResolvePanelCanvasGroup();
        RebuildResolvedButtonViews();
        SubscribeButtonViews();
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseActiveSession);

        if (activeInstance != null && activeInstance != this)
            Debug.LogWarning("Multiple InputCodeController instances are active. Newest instance will be used.", this);

        activeInstance = this;
    }

    /// <summary>
    /// Restores player control and clears callbacks owned by this UI controller.
    /// </summary>
    private void OnDisable()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseActiveSession);

        UnsubscribeButtonViews();
        ForceCloseSessionImmediate();
        KillOwnedTweens();

        if (activeInstance == this)
            activeInstance = null;
    }

    /// <summary>
    /// Clamps authored animation and audio values while editing.
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
        numberClickVolume = Mathf.Clamp01(numberClickVolume);
        eraseClickVolume = Mathf.Clamp01(eraseClickVolume);
        confirmClickVolume = Mathf.Clamp01(confirmClickVolume);
        successVolume = Mathf.Clamp01(successVolume);
        failureVolume = Mathf.Clamp01(failureVolume);
        RebuildResolvedButtonViews();
    }

    /// <summary>
    /// Polls the Rewired close action while this minigame owns gameplay input.
    /// </summary>
    private void Update()
    {
        if (!IsSessionActive || isClosing || IsOutcomePending)
            return;

        if (activeTargetBehaviour == null || !activeTargetBehaviour.isActiveAndEnabled)
        {
            CloseSessionInternal();
            return;
        }

        inputReader ??= new RewiredPlayerInputReader(rewiredPlayerId);
        if (inputReader.IsReady && inputReader.GetButtonDown(closeAction))
            CloseSessionInternal();
    }

    /// <summary>
    /// Starts a session through the currently active shared input-code controller.
    /// </summary>
    public static bool TryBeginActiveSession(GameObject interactorRoot, IInputCodeSessionTarget target)
    {
        return activeInstance != null && activeInstance.TryBeginSession(interactorRoot, target);
    }

    /// <summary>
    /// Opens this controller for one input-code target when setup and attempts are valid.
    /// </summary>
    public bool TryBeginSession(GameObject interactorRoot, IInputCodeSessionTarget target)
    {
        if (IsSessionActive ||
            interactorRoot == null ||
            target == null ||
            target.Definition == null ||
            target.RemainingAttempts <= 0 ||
            target is not MonoBehaviour targetBehaviour ||
            !targetBehaviour.isActiveAndEnabled)
        {
            return false;
        }

        ResolvePanelCanvasGroup();
        RebuildResolvedButtonViews();
        if (panelRoot == null || resolvedButtonViews.Count <= 0)
            return false;

        activeTarget = target;
        activeTargetBehaviour = targetBehaviour;
        activeDefinition = target.Definition;
        activeInteractorRoot = interactorRoot;
        pendingOutcome = SessionOutcome.None;
        isClosing = false;
        currentInput.Clear();
        controlLock.Bind(interactorRoot);
        controlLock.SetBlocked(true);
        SetCursorVisible(true);
        RefreshText();
        ShowPanel();
        return true;
    }

    /// <summary>
    /// Closes the active minigame without consuming attempts or applying outcomes.
    /// </summary>
    public void CloseActiveSession()
    {
        if (IsSessionActive && !IsOutcomePending && !isClosing)
            CloseSessionInternal();
    }

    /// <summary>
    /// Handles one number, delete, or confirm button click.
    /// </summary>
    private void HandleButtonClicked(InputCodeButtonView buttonView)
    {
        if (!IsSessionActive || IsOutcomePending || isClosing || buttonView == null)
            return;

        switch (buttonView.ButtonKind)
        {
            case InputCodeButtonKind.Delete:
                HandleDeleteClicked();
                break;

            case InputCodeButtonKind.Confirm:
                HandleConfirmClicked();
                break;

            default:
                HandleNumberClicked(buttonView.Digit);
                break;
        }
    }

    /// <summary>
    /// Appends one digit while respecting the active code length.
    /// </summary>
    private void HandleNumberClicked(int digit)
    {
        if (activeDefinition == null || currentInput.Length >= activeDefinition.RequiredDigitCount)
            return;

        currentInput.Append(Mathf.Clamp(digit, 0, 9));
        PlayOneShot(numberClickSfx, numberClickVolume);
        RefreshText();
    }

    /// <summary>
    /// Removes the most recently entered digit when one exists.
    /// </summary>
    private void HandleDeleteClicked()
    {
        PlayOneShot(eraseClickSfx, eraseClickVolume);
        if (currentInput.Length > 0)
            currentInput.Length -= 1;

        RefreshText();
    }

    /// <summary>
    /// Submits the current input and consumes an attempt when it is incomplete or incorrect.
    /// </summary>
    private void HandleConfirmClicked()
    {
        PlayOneShot(confirmClickSfx, confirmClickVolume);
        if (activeDefinition == null)
            return;

        string submittedCode = currentInput.ToString();
        if (submittedCode.Length == activeDefinition.RequiredDigitCount && activeDefinition.IsCorrect(submittedCode))
        {
            BeginOutcome(SessionOutcome.Success);
            return;
        }

        int remainingAttempts = activeTarget != null ? activeTarget.ConsumeFailedAttempt(activeInteractorRoot) : 0;
        currentInput.Clear();
        RefreshText();
        if (remainingAttempts <= 0)
            BeginOutcome(SessionOutcome.Failure);
    }

    /// <summary>
    /// Starts delayed success or final failure feedback.
    /// </summary>
    private void BeginOutcome(SessionOutcome outcome)
    {
        if (outcome == SessionOutcome.None || IsOutcomePending)
            return;

        pendingOutcome = outcome;
        SetButtonInteractionEnabled(false);
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
    /// Applies the pending result to the active target after feedback finishes.
    /// </summary>
    private void FinalizeOutcome()
    {
        IInputCodeSessionTarget resolvedTarget = activeTarget;
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
    /// Starts closing the panel and blocks further input until fade completion.
    /// </summary>
    private void CloseSessionInternal()
    {
        if (!IsSessionActive || isClosing)
            return;

        isClosing = true;
        outcomeDelayTween?.Kill();
        outcomeDelayTween = null;
        SetButtonInteractionEnabled(false);
        SetPanelInteractionEnabled(false);
        HidePanel(CompleteSessionClose);
    }

    /// <summary>
    /// Immediately restores player control and hides the panel during teardown.
    /// </summary>
    private void ForceCloseSessionImmediate()
    {
        if (!IsSessionActive)
            return;

        IInputCodeSessionTarget closedTarget = activeTarget;
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
    /// Restores gameplay control and clears session ownership after fade out.
    /// </summary>
    private void CompleteSessionClose()
    {
        if (!IsSessionActive)
            return;

        IInputCodeSessionTarget closedTarget = activeTarget;
        controlLock.SetBlocked(false);
        controlLock.Clear();
        SetCursorVisible(false);
        ClearSessionState();
        SessionClosed?.Invoke(closedTarget);
    }

    /// <summary>
    /// Clears target references and entered digits without restoring consumed attempts.
    /// </summary>
    private void ClearSessionState()
    {
        activeTarget = null;
        activeTargetBehaviour = null;
        activeDefinition = null;
        activeInteractorRoot = null;
        pendingOutcome = SessionOutcome.None;
        isClosing = false;
        currentInput.Clear();
        RefreshText();
    }

    /// <summary>
    /// Refreshes entered digits and attempts text.
    /// </summary>
    private void RefreshText()
    {
        if (inputText != null)
            inputText.text = currentInput.ToString();

        if (attemptsText != null)
            attemptsText.text = string.Format(attemptsFormat, activeTarget != null ? activeTarget.RemainingAttempts : 0);
    }

    /// <summary>
    /// Enables or disables every configured keypad button.
    /// </summary>
    private void SetButtonInteractionEnabled(bool enabled)
    {
        for (int i = 0; i < resolvedButtonViews.Count; i++)
            resolvedButtonViews[i]?.SetInteractionEnabled(enabled);
    }

    /// <summary>
    /// Rebuilds the non-null keypad button cache while preserving serialized ordering.
    /// </summary>
    private void RebuildResolvedButtonViews()
    {
        resolvedButtonViews.Clear();
        for (int i = 0; i < buttonViews.Count; i++)
        {
            if (buttonViews[i] != null)
                resolvedButtonViews.Add(buttonViews[i]);
        }
    }

    /// <summary>
    /// Subscribes to keypad button click events.
    /// </summary>
    private void SubscribeButtonViews()
    {
        for (int i = 0; i < resolvedButtonViews.Count; i++)
        {
            resolvedButtonViews[i].Clicked -= HandleButtonClicked;
            resolvedButtonViews[i].Clicked += HandleButtonClicked;
        }
    }

    /// <summary>
    /// Removes keypad button click subscriptions owned by this controller.
    /// </summary>
    private void UnsubscribeButtonViews()
    {
        for (int i = 0; i < resolvedButtonViews.Count; i++)
            resolvedButtonViews[i].Clicked -= HandleButtonClicked;
    }

    /// <summary>
    /// Resolves the panel CanvasGroup from the configured panel root.
    /// </summary>
    private void ResolvePanelCanvasGroup()
    {
        panelCanvasGroup = panelRoot != null ? panelRoot.GetComponent<CanvasGroup>() : null;
    }

    /// <summary>
    /// Shows the input-code panel and fades it in using unscaled time.
    /// </summary>
    private void ShowPanel()
    {
        if (panelRoot == null)
            return;

        panelFadeTween?.Kill();
        panelFadeTween = null;
        panelRoot.SetActive(true);
        SetButtonInteractionEnabled(true);

        if (panelCanvasGroup == null)
        {
            AnimateBlur(true, immediate: false);
            return;
        }

        panelCanvasGroup.alpha = panelFadeDuration > 0f ? 0f : 1f;
        SetPanelInteractionEnabled(true);
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
    /// Fades out the input-code panel before publishing close completion.
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
    /// Hides the input-code panel immediately without animation.
    /// </summary>
    private void HidePanelImmediate()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;
        AnimateBlur(false, immediate: true);
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
    /// Enables or disables panel raycasts without changing fade alpha.
    /// </summary>
    private void SetPanelInteractionEnabled(bool enabled)
    {
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.interactable = enabled;
        panelCanvasGroup.blocksRaycasts = enabled;
    }

    /// <summary>
    /// Applies expected cursor visibility while the mouse-driven keypad owns input.
    /// </summary>
    private static void SetCursorVisible(bool visible)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = visible;
    }

    /// <summary>
    /// Animates the shared Bokeh blur in or out using this panel's authored values.
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
    /// Plays one optional keypad UI clip.
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
