using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

public static class HideoutRuntimeSession
{
    private static bool initialized;
    private static int cash;
    private static int influencePoints;
    private static HideoutJobDefinition currentJob;
    private static string pendingHideoutMessage;
    private static readonly HashSet<string> completedJobIds = new();
    private static readonly HashSet<string> unlockedJobIds = new();

    public static bool IsInitialized => initialized;
    public static int Cash => cash;
    public static int InfluencePoints => influencePoints;
    public static HideoutJobDefinition CurrentJob => currentJob;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        initialized = false;
        cash = 0;
        influencePoints = 0;
        currentJob = null;
        pendingHideoutMessage = string.Empty;
        completedJobIds.Clear();
        unlockedJobIds.Clear();
    }

    public static void EnsureInitialized(int startingCash, int startingInfluencePoints)
    {
        if (initialized)
            return;

        initialized = true;
        bool hasExistingProgress = cash > 0 ||
                                   influencePoints > 0 ||
                                   completedJobIds.Count > 0 ||
                                   unlockedJobIds.Count > 0 ||
                                   !string.IsNullOrWhiteSpace(pendingHideoutMessage);

        cash = hasExistingProgress ? Mathf.Max(0, cash) : Mathf.Max(0, startingCash);
        influencePoints = hasExistingProgress ? Mathf.Max(0, influencePoints) : Mathf.Max(0, startingInfluencePoints);
        currentJob = null;
    }

    public static bool TrySpendCash(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (cash < amount)
            return false;

        cash -= amount;
        return true;
    }

    public static void AddCash(int amount)
    {
        cash += Mathf.Max(0, amount);
    }

    public static void AddInfluencePoints(int amount)
    {
        influencePoints += Mathf.Max(0, amount);
    }

    public static void SetCurrentJob(HideoutJobDefinition jobDefinition)
    {
        currentJob = jobDefinition;
    }

    public static void ClearCurrentJob()
    {
        currentJob = null;
    }

    public static bool IsJobCompleted(HideoutJobDefinition jobDefinition)
    {
        string jobId = ResolveJobId(jobDefinition);
        return !string.IsNullOrWhiteSpace(jobId) && completedJobIds.Contains(jobId);
    }

    public static bool IsJobUnlocked(HideoutJobDefinition jobDefinition)
    {
        string jobId = ResolveJobId(jobDefinition);
        return !string.IsNullOrWhiteSpace(jobId) && unlockedJobIds.Contains(jobId);
    }

    public static bool TryConsumePendingHideoutMessage(out string message)
    {
        message = pendingHideoutMessage ?? string.Empty;
        pendingHideoutMessage = string.Empty;
        return !string.IsNullOrWhiteSpace(message);
    }

    public static bool CompleteCurrentJob()
    {
        return CompleteJob(currentJob);
    }

    public static bool CompleteJob(HideoutJobDefinition jobDefinition)
    {
        if (jobDefinition == null)
        {
            currentJob = null;
            return false;
        }

        string jobId = ResolveJobId(jobDefinition);
        if (string.IsNullOrWhiteSpace(jobId))
        {
            currentJob = null;
            return false;
        }

        currentJob = null;
        if (!completedJobIds.Add(jobId))
            return false;

        AddCash(jobDefinition.RewardCash);
        AddInfluencePoints(jobDefinition.RewardInfluencePoints);

        int unlockedCount = 0;
        IReadOnlyList<HideoutJobDefinition> unlockJobs = jobDefinition.UnlockJobs;
        for (int i = 0; i < unlockJobs.Count; i++)
        {
            string unlockJobId = ResolveJobId(unlockJobs[i]);
            if (string.IsNullOrWhiteSpace(unlockJobId) || completedJobIds.Contains(unlockJobId))
                continue;

            if (unlockedJobIds.Add(unlockJobId))
                unlockedCount++;
        }

        pendingHideoutMessage = BuildCompletionMessage(jobDefinition, unlockedCount);
        return true;
    }

    private static string ResolveJobId(HideoutJobDefinition jobDefinition)
    {
        return jobDefinition != null ? jobDefinition.JobId : string.Empty;
    }

    private static string BuildCompletionMessage(HideoutJobDefinition jobDefinition, int unlockedCount)
    {
        if (jobDefinition == null)
            return string.Empty;

        StringBuilder builder = new();
        builder.Append($"Completed {jobDefinition.JobTitle}.");

        bool hasRewardText = false;
        if (jobDefinition.RewardCash > 0)
        {
            builder.Append($" Earned ${jobDefinition.RewardCash}");
            hasRewardText = true;
        }

        if (jobDefinition.RewardInfluencePoints > 0)
        {
            builder.Append(hasRewardText ? " and " : " Earned ");
            builder.Append($"+{jobDefinition.RewardInfluencePoints} influence");
            hasRewardText = true;
        }

        if (hasRewardText)
            builder.Append('.');

        if (unlockedCount > 0)
            builder.Append(unlockedCount == 1 ? " 1 new job is now available." : $" {unlockedCount} new jobs are now available.");

        return builder.ToString();
    }
}

}
