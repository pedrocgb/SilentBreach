using System;
using System.Collections.Generic;
using System.Globalization;
using Breezeblocks.HideoutSystem;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/UI/Gameplay Job Success Rewards UI")]
public sealed class GameplayJobSuccessRewardsUI : MonoBehaviour
{
    [FoldoutGroup("Progression"), AssetsOnly]
    [SerializeField] private PlayerProgressionDefinition progressionDefinition;

    [FoldoutGroup("Progression")]
    [SerializeField] private Image experienceFillImage;

    [FoldoutGroup("Progression")]
    [SerializeField] private TMP_Text currentExperienceText;

    [FoldoutGroup("Progression")]
    [SerializeField] private TMP_Text nextLevelExperienceText;

    [FoldoutGroup("Progression")]
    [SerializeField] private TMP_Text levelText;

    [FoldoutGroup("Badge")]
    [SerializeField] private Image badgeImage;

    [FoldoutGroup("Badge")]
    [SerializeField] private TMP_Text badgeText;

    [FoldoutGroup("Badge")]
    [SerializeField] private TMP_Text perkPointsGainedText;

    [FoldoutGroup("Money")]
    [SerializeField] private TMP_Text currentMoneyText;

    [FoldoutGroup("Money")]
    [SerializeField] private TMP_Text moneyEarnedText;

    [FoldoutGroup("Completion"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<Button> buttonsEnabledAfterAnimation = new();

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float experienceBarDuration = 1.2f;

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float levelPulseDuration = 0.18f;

    [FoldoutGroup("Animation"), MinValue(1f)]
    [SerializeField] private float levelPulseScale = 1.15f;

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float badgeFadeDuration = 0.2f;

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float perkMessageDuration = 1.2f;

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float moneyMoveDuration = 0.65f;

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float moneyCountDuration = 0.8f;

    [FoldoutGroup("Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float moneyFadeDuration = 0.2f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease experienceEase = Ease.OutCubic;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease moneyEase = Ease.InOutQuad;

    private static readonly CultureInfo BrazilianCurrencyCulture = CultureInfo.GetCultureInfo("pt-BR");
    private Sequence activeSequence;
    private Action completionCallback;
    private Vector3 badgeBaseScale = Vector3.one;
    private Vector3 badgeTextBaseScale = Vector3.one;
    private Vector3 levelTextBaseScale = Vector3.one;
    private Vector3 moneyTextBaseScale = Vector3.one;
    private Vector3 moneyEarnedStartPosition;

    /// <summary>
    /// Captures the authored UI transforms before reward animations alter them.
    /// </summary>
    private void Awake()
    {
        badgeBaseScale = badgeImage != null ? badgeImage.rectTransform.localScale : Vector3.one;
        badgeTextBaseScale = badgeText != null ? badgeText.rectTransform.localScale : Vector3.one;
        levelTextBaseScale = levelText != null ? levelText.rectTransform.localScale : Vector3.one;
        moneyTextBaseScale = currentMoneyText != null ? currentMoneyText.rectTransform.localScale : Vector3.one;
        moneyEarnedStartPosition = moneyEarnedText != null ? moneyEarnedText.rectTransform.position : Vector3.zero;
    }

    /// <summary>
    /// Stops the active sequence so disabled UI objects are never targeted by stale tweens.
    /// </summary>
    private void OnDisable()
    {
        activeSequence?.Kill();
        activeSequence = null;
        completionCallback = null;
    }

    /// <summary>
    /// Plays the saved job reward transition and invokes completion after every animation.
    /// </summary>
    public void Play(JobCompletionRewardResult rewardResult, Action onCompleted = null)
    {
        activeSequence?.Kill();
        completionCallback = onCompleted;

        if (!rewardResult.WasAwarded)
        {
            CompletePresentation();
            return;
        }

        SetCompletionButtonsInteractable(false);
        PrepareInitialVisualState(rewardResult);

        activeSequence = DOTween.Sequence().SetUpdate(true);
        AppendExperienceAnimation(activeSequence, rewardResult);
        AppendPerkPointAnimation(activeSequence, rewardResult.LevelUps);
        AppendMoneyAnimation(activeSequence, rewardResult);
        activeSequence.OnComplete(CompletePresentation);
    }

    /// <summary>
    /// Prepares all reward views with the values held before the completed job was awarded.
    /// </summary>
    private void PrepareInitialVisualState(JobCompletionRewardResult rewardResult)
    {
        ApplyProgressionState(rewardResult.LevelBefore, rewardResult.ExperienceBefore, updateBadgeSprite: true);
        SetText(currentMoneyText, FormatMoney(rewardResult.CashBefore));
        SetText(moneyEarnedText, $"+ {FormatMoney(rewardResult.CashAwarded)}");
        SetText(perkPointsGainedText, string.Empty);

        if (moneyEarnedText != null)
        {
            moneyEarnedText.alpha = rewardResult.CashAwarded > 0 ? 1f : 0f;
            moneyEarnedText.rectTransform.position = moneyEarnedStartPosition;
        }

        if (badgeImage != null)
        {
            badgeImage.color = new Color(
                badgeImage.color.r,
                badgeImage.color.g,
                badgeImage.color.b,
                1f);
            badgeImage.rectTransform.localScale = badgeBaseScale;
        }

        if (badgeText != null)
            badgeText.rectTransform.localScale = badgeTextBaseScale;

        if (levelText != null)
            levelText.rectTransform.localScale = levelTextBaseScale;

        if (currentMoneyText != null)
            currentMoneyText.rectTransform.localScale = moneyTextBaseScale;
    }

    /// <summary>
    /// Builds sequential bar segments so experience can visibly wrap across multiple level-ups.
    /// </summary>
    private void AppendExperienceAnimation(Sequence sequence, JobCompletionRewardResult rewardResult)
    {
        int level = PlayerProgressionRules.SanitizeLevel(rewardResult.LevelBefore);
        int experience = PlayerProgressionRules.SanitizeExperience(rewardResult.ExperienceBefore, level);
        int remainingExperience = Mathf.Max(0, rewardResult.ExperienceAwarded);

        while (remainingExperience > 0 && level < PlayerProgressionRules.MaximumLevel)
        {
            int requiredExperience = PlayerProgressionRules.GetExperienceNeededForNextLevel(level);
            int segmentAmount = Mathf.Min(remainingExperience, requiredExperience - experience);
            int targetExperience = experience + segmentAmount;
            float duration = ResolveExperienceSegmentDuration(segmentAmount, requiredExperience);

            sequence.Append(CreateExperienceTween(experience, targetExperience, requiredExperience, level, duration));
            remainingExperience -= segmentAmount;
            experience = targetExperience;

            if (experience < requiredExperience)
                continue;

            int previousLevel = level;
            level++;
            experience = 0;
            AppendLevelUpAnimation(sequence, previousLevel, level);
        }

        sequence.AppendCallback(() => ApplyProgressionState(
            rewardResult.LevelAfter,
            rewardResult.ExperienceAfter,
            updateBadgeSprite: true));
    }

    /// <summary>
    /// Creates one experience-bar tween and keeps its numeric text synchronized.
    /// </summary>
    private Tween CreateExperienceTween(int startExperience, int targetExperience, int requiredExperience, int level, float duration)
    {
        int displayedExperience = startExperience;
        return DOTween.To(
                () => displayedExperience,
                value =>
                {
                    displayedExperience = value;
                    SetExperienceValues(level, value, requiredExperience);
                },
                targetExperience,
                duration)
            .SetEase(experienceEase);
    }

    /// <summary>
    /// Adds the level and badge feedback shown each time an experience segment reaches full.
    /// </summary>
    private void AppendLevelUpAnimation(Sequence sequence, int previousLevel, int newLevel)
    {
        bool badgeChanged = PlayerProgressionRules.GetBadgeId(previousLevel) != PlayerProgressionRules.GetBadgeId(newLevel);

        if (badgeChanged && badgeImage != null)
        {
            sequence.Append(badgeImage.DOFade(0f, badgeFadeDuration).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() => ApplyProgressionState(newLevel, 0, updateBadgeSprite: true));
            sequence.Append(badgeImage.DOFade(1f, badgeFadeDuration).SetEase(Ease.InQuad));
            sequence.Join(badgeImage.rectTransform.DOScale(badgeBaseScale * levelPulseScale, levelPulseDuration));
            sequence.Append(badgeImage.rectTransform.DOScale(badgeBaseScale, levelPulseDuration));
        }
        else
        {
            sequence.AppendCallback(() => ApplyProgressionState(newLevel, 0, updateBadgeSprite: true));
        }

        if (levelText != null)
        {
            sequence.Append(levelText.rectTransform.DOScale(levelTextBaseScale * levelPulseScale, levelPulseDuration));
            sequence.Append(levelText.rectTransform.DOScale(levelTextBaseScale, levelPulseDuration));
        }

        if (badgeText != null)
        {
            sequence.Join(badgeText.rectTransform.DOScale(badgeTextBaseScale * levelPulseScale, levelPulseDuration));
            sequence.Append(badgeText.rectTransform.DOScale(badgeTextBaseScale, levelPulseDuration));
        }
    }

    /// <summary>
    /// Adds the perk-point notification when one or more levels were earned.
    /// </summary>
    private void AppendPerkPointAnimation(Sequence sequence, int levelUps)
    {
        if (perkPointsGainedText == null || levelUps <= 0)
            return;

        string pointLabel = levelUps == 1 ? "ponto" : "pontos";
        sequence.AppendCallback(() =>
        {
            SetText(perkPointsGainedText, $"Você ganhou {levelUps} {pointLabel} de talentos.");
            perkPointsGainedText.alpha = 0f;
        });
        sequence.Append(perkPointsGainedText.DOFade(1f, badgeFadeDuration));
        sequence.AppendInterval(perkMessageDuration);
    }

    /// <summary>
    /// Adds the earned-money movement, fade, count, and emphasis animations.
    /// </summary>
    private void AppendMoneyAnimation(Sequence sequence, JobCompletionRewardResult rewardResult)
    {
        if (currentMoneyText == null)
            return;

        if (rewardResult.CashAwarded > 0 && moneyEarnedText != null)
        {
            sequence.Append(moneyEarnedText.rectTransform
                .DOMove(currentMoneyText.rectTransform.position, moneyMoveDuration)
                .SetEase(moneyEase));
            sequence.Append(moneyEarnedText.DOFade(0f, moneyFadeDuration));
        }

        int displayedCash = rewardResult.CashBefore;
        sequence.Append(DOTween.To(
                () => displayedCash,
                value =>
                {
                    displayedCash = value;
                    SetText(currentMoneyText, FormatMoney(value));
                },
                rewardResult.CashAfter,
                moneyCountDuration)
            .SetEase(moneyEase));
        sequence.Append(currentMoneyText.rectTransform.DOScale(moneyTextBaseScale * levelPulseScale, levelPulseDuration));
        sequence.Append(currentMoneyText.rectTransform.DOScale(moneyTextBaseScale, levelPulseDuration));
    }

    /// <summary>
    /// Applies level, experience, badge name, tier, and badge sprite to the reward UI.
    /// </summary>
    private void ApplyProgressionState(int level, int experience, bool updateBadgeSprite)
    {
        int sanitizedLevel = PlayerProgressionRules.SanitizeLevel(level);
        int requiredExperience = PlayerProgressionRules.GetExperienceNeededForNextLevel(sanitizedLevel);
        SetExperienceValues(sanitizedLevel, experience, requiredExperience);

        PlayerBadgeVisualDefinition badgeDefinition = progressionDefinition != null
            ? progressionDefinition.GetBadgeForLevel(sanitizedLevel)
            : null;
        string badgeName = badgeDefinition != null && !string.IsNullOrWhiteSpace(badgeDefinition.DisplayName)
            ? badgeDefinition.DisplayName
            : PlayerProgressionRules.GetBadgeId(sanitizedLevel).ToString();

        SetText(levelText, sanitizedLevel.ToString());
        SetText(badgeText, $"{badgeName} {PlayerProgressionRules.GetRomanTier(sanitizedLevel)}");

        if (updateBadgeSprite && badgeImage != null)
        {
            badgeImage.sprite = badgeDefinition?.Sprite;
            badgeImage.enabled = badgeImage.sprite != null;
        }
    }

    /// <summary>
    /// Synchronizes the experience fill and both numeric experience labels.
    /// </summary>
    private void SetExperienceValues(int level, int experience, int requiredExperience)
    {
        bool isMaximumLevel = level >= PlayerProgressionRules.MaximumLevel;
        int sanitizedExperience = isMaximumLevel
            ? 0
            : Mathf.Clamp(experience, 0, Mathf.Max(0, requiredExperience));

        if (experienceFillImage != null)
        {
            experienceFillImage.fillAmount = isMaximumLevel || requiredExperience <= 0
                ? 1f
                : Mathf.Clamp01((float)sanitizedExperience / requiredExperience);
        }

        SetText(currentExperienceText, isMaximumLevel ? "MAX" : "Experiência: " + sanitizedExperience.ToString());
        SetText(nextLevelExperienceText, isMaximumLevel ? "MAX" : requiredExperience.ToString());
    }

    /// <summary>
    /// Returns a proportional duration for one experience-bar segment.
    /// </summary>
    private float ResolveExperienceSegmentDuration(int segmentAmount, int requiredExperience)
    {
        if (experienceBarDuration <= 0f || requiredExperience <= 0)
            return 0f;

        return Mathf.Max(0.05f, experienceBarDuration * ((float)segmentAmount / requiredExperience));
    }

    /// <summary>
    /// Restores completion controls and notifies the mission controller after presentation.
    /// </summary>
    private void CompletePresentation()
    {
        activeSequence = null;
        SetCompletionButtonsInteractable(true);
        Action callback = completionCallback;
        completionCallback = null;
        callback?.Invoke();
    }

    /// <summary>
    /// Enables or disables every configured action that may leave the success screen.
    /// </summary>
    private void SetCompletionButtonsInteractable(bool interactable)
    {
        if (buttonsEnabledAfterAnimation == null)
            return;

        for (int i = 0; i < buttonsEnabledAfterAnimation.Count; i++)
        {
            if (buttonsEnabledAfterAnimation[i] != null)
                buttonsEnabledAfterAnimation[i].interactable = interactable;
        }
    }

    /// <summary>
    /// Formats integer cash values using the game's Brazilian Real presentation.
    /// </summary>
    private static string FormatMoney(int amount)
    {
        return $"R${Mathf.Max(0, amount).ToString("N2", BrazilianCurrencyCulture)}";
    }

    /// <summary>
    /// Safely updates an optional text reference.
    /// </summary>
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}

}
