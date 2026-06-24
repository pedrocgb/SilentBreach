using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
[AddComponentMenu("Breezeblocks/Missions/UI/Objective Hold Progress UI")]
public sealed class ObjectiveHoldProgressUI : MonoBehaviour
{
    private static ObjectiveHoldProgressUI activeInstance;

    [FoldoutGroup("References"), Required]
    [SerializeField] private TMP_Text timerText;

    [FoldoutGroup("Text")]
    [SerializeField] private string timerFormat = "{0:0.0}s";

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float fadeDuration = 0.15f;

    [FoldoutGroup("Animation"), MinValue(1f)]
    [SerializeField] private float pulseScale = 1.08f;

    [FoldoutGroup("Animation"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float pulseDuration = 0.35f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private Tween pulseTween;

    public static bool HasRegisteredInstance => activeInstance != null;

    /// <summary>
    /// Shows the active registered hold-progress UI for a new timed interaction.
    /// </summary>
    public static void ShowActive(float duration)
    {
        activeInstance?.Show(duration);
    }

    /// <summary>
    /// Updates the active registered hold-progress UI with remaining time.
    /// </summary>
    public static void UpdateActive(float elapsedTime, float duration)
    {
        activeInstance?.UpdateProgress(elapsedTime, duration);
    }

    /// <summary>
    /// Hides the active registered hold-progress UI.
    /// </summary>
    public static void HideActive()
    {
        activeInstance?.Hide();
    }

    /// <summary>
    /// Caches same-object UI dependencies before animation starts.
    /// </summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        SetVisibleImmediate(false);
    }

    /// <summary>
    /// Registers this UI as the scene active hold-progress presenter.
    /// </summary>
    private void OnEnable()
    {
        activeInstance = this;
    }

    /// <summary>
    /// Stops active tweens and unregisters this UI when disabled.
    /// </summary>
    private void OnDisable()
    {
        if (activeInstance == this)
            activeInstance = null;

        KillTweens();
    }

    /// <summary>
    /// Keeps authored animation values safe in edit mode.
    /// </summary>
    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0f, fadeDuration);
        pulseScale = Mathf.Max(1f, pulseScale);
        pulseDuration = Mathf.Max(0.01f, pulseDuration);
        timerFormat = string.IsNullOrWhiteSpace(timerFormat) ? "{0:0.0}s" : timerFormat;
    }

    /// <summary>
    /// Fades the timer in and starts the looping pulse feedback.
    /// </summary>
    private void Show(float duration)
    {
        EnsureCanvasGroup();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        UpdateProgress(0f, duration);

        fadeTween?.Kill();
        fadeTween = canvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);

        pulseTween?.Kill();
        transform.localScale = Vector3.one;
        pulseTween = DOTween.Sequence()
            .SetUpdate(true)
            .Append(transform.DOScale(pulseScale, pulseDuration).SetEase(Ease.InOutSine))
            .Append(transform.DOScale(1f, pulseDuration).SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Restart);
    }

    /// <summary>
    /// Updates the remaining hold timer text.
    /// </summary>
    private void UpdateProgress(float elapsedTime, float duration)
    {
        if (timerText == null)
            return;

        float remaining = Mathf.Max(0f, duration - elapsedTime);
        timerText.text = string.Format(timerFormat, remaining);
    }

    /// <summary>
    /// Fades the timer out and stops the looping pulse.
    /// </summary>
    private void Hide()
    {
        EnsureCanvasGroup();
        pulseTween?.Kill();
        pulseTween = null;
        transform.localScale = Vector3.one;

        fadeTween?.Kill();
        fadeTween = canvasGroup
            .DOFade(0f, fadeDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    /// <summary>
    /// Sets initial visibility without creating tweens.
    /// </summary>
    private void SetVisibleImmediate(bool visible)
    {
        EnsureCanvasGroup();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// Ensures same-object CanvasGroup cache exists before UI mutation.
    /// </summary>
    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Kills all active tweens owned by this UI.
    /// </summary>
    private void KillTweens()
    {
        fadeTween?.Kill();
        fadeTween = null;
        pulseTween?.Kill();
        pulseTween = null;
    }
}

}
