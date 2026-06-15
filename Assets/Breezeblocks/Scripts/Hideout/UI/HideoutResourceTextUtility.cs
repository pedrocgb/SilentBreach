using System.Collections.Generic;
using TMPro;

namespace Breezeblocks.HideoutSystem
{

public static class HideoutResourceTextUtility
{
    /// <summary>
    /// Formats the player's current money for hideout resource displays.
    /// </summary>
    public static string FormatMoney(int value)
    {
        return $"Dinheiro: R${value}";
    }

    /// <summary>
    /// Formats the player's current influence points for hideout resource displays.
    /// </summary>
    public static string FormatInfluencePoints(int value)
    {
        return $"Influência: {value}";
    }

    /// <summary>
    /// Formats the player's current perk points using the configured global label.
    /// </summary>
    public static string FormatPerkPoints(int value)
    {
        string label = GlobalSettings.Instance != null
            ? GlobalSettings.Instance.PerksText
            : "Pontos de Talento";
        return $"{label}: {value}";
    }

    /// <summary>
    /// Formats the player's current experience using the configured global label.
    /// </summary>
    public static string FormatExperience(int currentExperience, int requiredExperience, bool isMaximumLevel)
    {
        string value = isMaximumLevel ? "MAX" : $"{currentExperience} / {requiredExperience}";
        return FormatExperienceValue(value);
    }

    /// <summary>
    /// Formats a job's experience reward using the configured global label.
    /// </summary>
    public static string FormatExperienceReward(int experienceReward)
    {
        return FormatExperienceValue(experienceReward.ToString());
    }

    /// <summary>
    /// Adds the configured experience label to an already formatted value.
    /// </summary>
    private static string FormatExperienceValue(string value)
    {
        string label = GlobalSettings.Instance != null
            ? GlobalSettings.Instance.ExperienceText
            : "Experiência";
        return $"{label}: {value}";
    }

    /// <summary>
    /// Updates a legacy primary reference and every additional configured text without duplicate assignments.
    /// </summary>
    public static void SetTexts(TMP_Text primaryText, IReadOnlyList<TMP_Text> additionalTexts, string value)
    {
        SetText(primaryText, value);
        if (additionalTexts == null)
            return;

        for (int i = 0; i < additionalTexts.Count; i++)
        {
            TMP_Text target = additionalTexts[i];
            if (target != primaryText)
                SetText(target, value);
        }
    }

    /// <summary>
    /// Safely updates one optional text reference.
    /// </summary>
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value ?? string.Empty;
    }
}

}
