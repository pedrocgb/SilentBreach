using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.HideoutSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Hideout/Hideout Progression Display UI")]
public sealed class HideoutProgressionDisplayUI : MonoBehaviour
{
    [FoldoutGroup("Progression"), AssetsOnly]
    [SerializeField] private PlayerProgressionDefinition progressionDefinition;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text currentExperienceText;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text currentLevelText;

    [FoldoutGroup("References")]
    [SerializeField] private Image badgeImage;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text badgeText;

    [FoldoutGroup("References")]
    [SerializeField] private Image experienceFillImage;

    /// <summary>
    /// Refreshes progression values whenever the hideout display becomes visible.
    /// </summary>
    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// Displays the saved player level, experience, badge, and badge tier.
    /// </summary>
    public void Refresh()
    {
        int level = PlayerProgressionRules.SanitizeLevel(HideoutRuntimeSession.CurrentLevel);
        int experience = PlayerProgressionRules.SanitizeExperience(HideoutRuntimeSession.CurrentExperience, level);
        int requiredExperience = PlayerProgressionRules.GetExperienceNeededForNextLevel(level);
        bool isMaximumLevel = level >= PlayerProgressionRules.MaximumLevel;

        SetText(currentLevelText, level.ToString());
        SetText(
            currentExperienceText,
            HideoutResourceTextUtility.FormatExperience(experience, requiredExperience, isMaximumLevel));

        if (experienceFillImage != null)
        {
            experienceFillImage.fillAmount = isMaximumLevel || requiredExperience <= 0
                ? 1f
                : Mathf.Clamp01((float)experience / requiredExperience);
        }

        PlayerBadgeVisualDefinition badgeDefinition = progressionDefinition != null
            ? progressionDefinition.GetBadgeForLevel(level)
            : null;
        string badgeName = badgeDefinition != null && !string.IsNullOrWhiteSpace(badgeDefinition.DisplayName)
            ? badgeDefinition.DisplayName
            : PlayerProgressionRules.GetBadgeId(level).ToString();

        SetText(badgeText, $"{badgeName} {PlayerProgressionRules.GetRomanTier(level)}");
        if (badgeImage != null)
        {
            badgeImage.sprite = badgeDefinition?.Sprite;
            badgeImage.enabled = badgeImage.sprite != null;
        }
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
