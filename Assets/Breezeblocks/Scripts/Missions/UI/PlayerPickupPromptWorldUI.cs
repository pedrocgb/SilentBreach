using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Player Interact Prompt World UI")]
public class PlayerPickupPromptWorldUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private PlayerPickupInteractor pickupInteractor;

    [FoldoutGroup("UI")]
    [SerializeField] private GameObject pickUpPromptRoot;

    [FoldoutGroup("UI")]
    [SerializeField] private Image pickUpPromptImage;

    [FoldoutGroup("UI")]
    [SerializeField] private TMP_Text interactionLabelText;

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s/char", true)]
    [SerializeField] private float typewriterCharacterDuration = 0.014f;

    private Tween typewriterTween;
    private Tween feedbackMoveTween;
    private Tween feedbackColorTween;
    private PlayerWorldInteractable observedInteractable;
    private string currentShownLabel = string.Empty;
    private Color defaultLabelColor = Color.white;
    private Vector2 defaultLabelAnchoredPosition;

    /// <summary>
    /// Resolves the pickup interactor reference and applies the initial prompt visibility.
    /// </summary>
    private void Awake()
    {
        if (pickupInteractor == null)
            pickupInteractor = GetComponentInParent<PlayerPickupInteractor>();

        CacheLabelDefaults();
        Refresh();
    }

    /// <summary>
    /// Subscribes to interactor changes so the prompt tracks the nearest interactable.
    /// </summary>
    private void OnEnable()
    {
        if (pickupInteractor == null)
            pickupInteractor = GetComponentInParent<PlayerPickupInteractor>();

        if (pickupInteractor != null)
            pickupInteractor.CurrentInteractableChanged += HandleInteractableChanged;

        BindPresentationEvents(pickupInteractor != null ? pickupInteractor.CurrentInteractable : null);
        Refresh();
    }

    /// <summary>
    /// Unsubscribes from interactor events when the prompt goes inactive.
    /// </summary>
    private void OnDisable()
    {
        if (pickupInteractor != null)
            pickupInteractor.CurrentInteractableChanged -= HandleInteractableChanged;

        BindPresentationEvents(null);
        StopTypewriterTween();
        StopFeedbackTweens(restoreVisuals: true);
    }

    /// <summary>
    /// Refreshes prompt visibility whenever the current interactable changes.
    /// </summary>
    private void HandleInteractableChanged(PlayerWorldInteractable interactable)
    {
        BindPresentationEvents(interactable);
        Refresh();
    }

    /// <summary>
    /// Refreshes prompt presentation whenever the current interactable changes its own label or state.
    /// </summary>
    private void HandleInteractionPresentationChanged(PlayerWorldInteractable interactable)
    {
        Refresh();
    }

    /// <summary>
    /// Plays temporary prompt feedback requested by the current interactable.
    /// </summary>
    private void HandleInteractionFeedbackRequested(PlayerWorldInteractable interactable, InteractionPromptFeedback feedback)
    {
        if (interactable != observedInteractable)
            return;

        UpdateLabel(feedback.Label);
        PlayFeedbackAnimation(feedback);
    }

    /// <summary>
    /// Shows the prompt only while interaction is possible and input is not blocked.
    /// </summary>
    private void Refresh()
    {
        bool visible = pickupInteractor != null && !pickupInteractor.IsInputBlocked && pickupInteractor.CurrentInteractable != null;
        if (pickUpPromptRoot != null)
            pickUpPromptRoot.SetActive(visible);

        if (pickUpPromptImage != null)
            pickUpPromptImage.enabled = visible;

        if (!visible)
        {
            SetLabelHidden();
            return;
        }

        UpdateLabel(pickupInteractor.CurrentInteractable.GetInteractionDisplayName(pickupInteractor.gameObject));
    }

    /// <summary>
    /// Subscribes to the current interactable label-change event and unsubscribes from the previous one.
    /// </summary>
    private void BindPresentationEvents(PlayerWorldInteractable interactable)
    {
        if (observedInteractable == interactable)
            return;

        if (observedInteractable != null)
        {
            observedInteractable.InteractionPresentationChanged -= HandleInteractionPresentationChanged;
            observedInteractable.InteractionFeedbackRequested -= HandleInteractionFeedbackRequested;
        }

        observedInteractable = interactable;
        if (observedInteractable != null)
        {
            observedInteractable.InteractionPresentationChanged += HandleInteractionPresentationChanged;
            observedInteractable.InteractionFeedbackRequested += HandleInteractionFeedbackRequested;
        }
    }

    /// <summary>
    /// Stores the label's authored color and anchored position for feedback reset.
    /// </summary>
    private void CacheLabelDefaults()
    {
        if (interactionLabelText == null)
            return;

        defaultLabelColor = interactionLabelText.color;
        defaultLabelAnchoredPosition = interactionLabelText.rectTransform.anchoredPosition;
    }

    /// <summary>
    /// Updates the interaction label and replays the typewriter animation when the text changes.
    /// </summary>
    private void UpdateLabel(string interactionLabel)
    {
        if (interactionLabelText == null)
            return;

        string resolvedLabel = string.IsNullOrWhiteSpace(interactionLabel) ? string.Empty : interactionLabel;
        if (string.Equals(currentShownLabel, resolvedLabel, System.StringComparison.Ordinal))
            return;

        currentShownLabel = resolvedLabel;
        StopTypewriterTween();
        interactionLabelText.text = resolvedLabel;
        interactionLabelText.maxVisibleCharacters = 0;
        interactionLabelText.enabled = !string.IsNullOrEmpty(resolvedLabel);

        if (string.IsNullOrEmpty(resolvedLabel))
            return;

        float duration = Mathf.Max(0f, resolvedLabel.Length * Mathf.Max(0f, typewriterCharacterDuration));
        if (duration <= 0f)
        {
            interactionLabelText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        typewriterTween = DOVirtual
            .Int(0, resolvedLabel.Length, duration, visibleCharacters => interactionLabelText.maxVisibleCharacters = visibleCharacters)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (interactionLabelText != null)
                    interactionLabelText.maxVisibleCharacters = int.MaxValue;

                typewriterTween = null;
            });
    }

    /// <summary>
    /// Clears and hides the interaction label while the prompt is inactive.
    /// </summary>
    private void SetLabelHidden()
    {
        currentShownLabel = string.Empty;
        StopTypewriterTween();
        if (interactionLabelText == null)
            return;

        interactionLabelText.text = string.Empty;
        interactionLabelText.maxVisibleCharacters = 0;
        interactionLabelText.enabled = false;
    }

    /// <summary>
    /// Plays a short red flash and fixed left-right movement on the label to communicate a denied interaction.
    /// </summary>
    private void PlayFeedbackAnimation(InteractionPromptFeedback feedback)
    {
        if (interactionLabelText == null)
            return;

        StopFeedbackTweens(restoreVisuals: false);

        RectTransform labelRect = interactionLabelText.rectTransform;
        labelRect.anchoredPosition = defaultLabelAnchoredPosition;
        interactionLabelText.color = defaultLabelColor;

        float duration = Mathf.Max(0f, feedback.Duration);
        if (duration <= 0f)
        {
            interactionLabelText.color = feedback.FlashColor;
            return;
        }

        int sideSteps = Mathf.Max(1, feedback.Vibrato);
        float stepDuration = duration / ((sideSteps * 2f) + 1f);
        Sequence movementSequence = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < sideSteps; i++)
        {
            movementSequence.Append(labelRect.DOAnchorPosX(defaultLabelAnchoredPosition.x + feedback.Strength, stepDuration).SetEase(Ease.Linear));
            movementSequence.Append(labelRect.DOAnchorPosX(defaultLabelAnchoredPosition.x - feedback.Strength, stepDuration).SetEase(Ease.Linear));
        }

        movementSequence
            .Append(labelRect.DOAnchorPos(defaultLabelAnchoredPosition, stepDuration).SetEase(Ease.Linear))
            .OnComplete(() =>
            {
                if (labelRect != null)
                    labelRect.anchoredPosition = defaultLabelAnchoredPosition;

                feedbackMoveTween = null;
            });
        feedbackMoveTween = movementSequence;

        feedbackColorTween = interactionLabelText
            .DOColor(feedback.FlashColor, duration * 0.5f)
            .SetLoops(2, LoopType.Yoyo)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (interactionLabelText != null)
                    interactionLabelText.color = defaultLabelColor;

                feedbackColorTween = null;
            });
    }

    /// <summary>
    /// Stops the active typewriter tween before the label changes or the prompt is disabled.
    /// </summary>
    private void StopTypewriterTween()
    {
        typewriterTween?.Kill();
        typewriterTween = null;
    }

    /// <summary>
    /// Stops active feedback tweens and optionally restores the authored label visual state.
    /// </summary>
    private void StopFeedbackTweens(bool restoreVisuals)
    {
        feedbackMoveTween?.Kill();
        feedbackColorTween?.Kill();
        feedbackMoveTween = null;
        feedbackColorTween = null;

        if (!restoreVisuals || interactionLabelText == null)
            return;

        interactionLabelText.color = defaultLabelColor;
        interactionLabelText.rectTransform.anchoredPosition = defaultLabelAnchoredPosition;
    }
}

}
