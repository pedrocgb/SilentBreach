using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Breezeblocks.HideoutSystem
{

/// <summary>
/// Provides shared formatting helpers for hideout job text and scene references.
/// </summary>
public static class HideoutJobTextUtility
{
    /// <summary>
    /// Resolves the display text shown for a gameplay objective definition.
    /// </summary>
    public static string ResolveObjectiveDisplayText(HideoutJobObjectiveType objectiveType, string referenceId, string customDisplayText)
    {
        if (!string.IsNullOrWhiteSpace(customDisplayText))
            return customDisplayText.Trim();

        string readableId = string.IsNullOrWhiteSpace(referenceId) ? "target" : referenceId.Trim();
        return objectiveType switch
        {
            HideoutJobObjectiveType.KillTarget => $"Kill {readableId}",
            HideoutJobObjectiveType.RetrieveItem => $"Retrieve {readableId}",
            HideoutJobObjectiveType.IncapacitateTarget => $"Incapacitate {readableId}",
            _ => readableId
        };
    }

    /// <summary>
    /// Resolves the display text shown for a gameplay failure definition.
    /// </summary>
    public static string ResolveFailureDisplayText(HideoutJobFailureType failureType, string customDisplayText, float timeLimitSeconds)
    {
        if (!string.IsNullOrWhiteSpace(customDisplayText))
            return customDisplayText.Trim();

        return failureType switch
        {
            HideoutJobFailureType.DontHarmInnocent => "Do not harm innocents",
            HideoutJobFailureType.DontKillInnocent => "Do not kill innocents",
            HideoutJobFailureType.DontHarmAnyone => "Do not harm anyone",
            HideoutJobFailureType.DontKillAnyone => "Do not kill anyone",
            HideoutJobFailureType.DontAlert => "Do not alert anyone",
            HideoutJobFailureType.DontBeDetected => "Do not be detected",
            HideoutJobFailureType.TimeLimit => $"Finish within {Math.Max(0.01f, timeLimitSeconds):0.#} seconds",
            _ => "Unknown failure condition"
        };
    }

    /// <summary>
    /// Resolves the failure screen message shown when a failure definition triggers.
    /// </summary>
    public static string ResolveFailureScreenMessage(HideoutJobFailureType failureType, string customMessage)
    {
        if (!string.IsNullOrWhiteSpace(customMessage))
            return customMessage.Trim();

        return failureType switch
        {
            HideoutJobFailureType.DontHarmInnocent => "Mission Failed. You were not supposed to harm innocents!",
            HideoutJobFailureType.DontKillInnocent => "Mission Failed. You were not supposed to kill innocents!",
            HideoutJobFailureType.DontHarmAnyone => "Mission Failed. You were not supposed to harm anyone!",
            HideoutJobFailureType.DontKillAnyone => "Mission Failed. You were not supposed to kill anyone!",
            HideoutJobFailureType.DontAlert => "Mission Failed. You alerted someone!",
            HideoutJobFailureType.DontBeDetected => "Mission Failed. You were detected!",
            HideoutJobFailureType.TimeLimit => "Mission Failed. You ran out of time!",
            _ => "Mission Failed."
        };
    }

    /// <summary>
    /// Builds the compact reward summary used by hideout mission selection UI.
    /// </summary>
    public static string BuildRewardSummaryText(int rewardCash, int rewardInfluencePoints, string fallbackRewardText)
    {
        List<string> parts = new();
        if (Math.Max(0, rewardCash) > 0)
            parts.Add($"${Math.Max(0, rewardCash)}");

        if (Math.Max(0, rewardInfluencePoints) > 0)
            parts.Add($"Influence +{Math.Max(0, rewardInfluencePoints)}");

        if (parts.Count > 0)
            return string.Join(" | ", parts);

        return fallbackRewardText != null ? fallbackRewardText.Trim() : string.Empty;
    }

    /// <summary>
    /// Normalizes a scene name or path down to the scene name used by scene loading helpers.
    /// </summary>
    public static string NormalizeSceneName(string rawSceneReference)
    {
        if (string.IsNullOrWhiteSpace(rawSceneReference))
            return string.Empty;

        string trimmed = rawSceneReference.Trim();
        if (trimmed.Contains("/") || trimmed.Contains("\\") || trimmed.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileNameWithoutExtension(trimmed);

        return trimmed;
    }

    /// <summary>
    /// Builds a newline-separated bullet list from the provided entry resolver.
    /// </summary>
    public static string BuildFormattedList<T>(IReadOnlyList<T> entries, Func<T, string> resolver)
    {
        if (entries == null || entries.Count == 0 || resolver == null)
            return string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < entries.Count; i++)
        {
            string value = resolver(entries[i]);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append("- ");
            builder.Append(value.Trim());
        }

        return builder.ToString();
    }
}

}
