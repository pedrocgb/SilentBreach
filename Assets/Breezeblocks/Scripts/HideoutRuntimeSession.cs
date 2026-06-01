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
    private static int perkPoints;
    private static int unlockedTierOnePerkCount;
    private static int unlockedTierTwoPerkCount;
    private static bool tierTwoUnlocked;
    private static bool tierThreeUnlocked;
    private static HideoutJobDefinition currentJob;
    private static string activeMissionJobId;
    private static string pendingHideoutMessage;
    private static readonly HashSet<string> completedJobIds = new();
    private static readonly HashSet<string> unlockedJobIds = new();
    private static readonly HashSet<string> failedJobIds = new();
    private static readonly HashSet<string> unlockedPerkIds = new();

    public static bool IsInitialized => initialized;
    public static int Cash => cash;
    public static int InfluencePoints => influencePoints;
    public static int PerkPoints => perkPoints;
    public static HideoutJobDefinition CurrentJob => currentJob;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        initialized = false;
        cash = 0;
        influencePoints = 0;
        perkPoints = 0;
        unlockedTierOnePerkCount = 0;
        unlockedTierTwoPerkCount = 0;
        tierTwoUnlocked = false;
        tierThreeUnlocked = false;
        currentJob = null;
        activeMissionJobId = string.Empty;
        pendingHideoutMessage = string.Empty;
        completedJobIds.Clear();
        unlockedJobIds.Clear();
        failedJobIds.Clear();
        unlockedPerkIds.Clear();
    }

    public static void EnsureInitialized(int startingCash, int startingInfluencePoints)
    {
        EnsureInitialized(startingCash, startingInfluencePoints, 0);
    }

    public static void EnsureInitialized(int startingCash, int startingInfluencePoints, int startingPerkPoints)
    {
        if (initialized)
            return;

        initialized = true;
        if (HideoutSaveSystem.TryLoad(out HideoutSaveSnapshot snapshot))
            ApplyLoadedSnapshot(snapshot);
        else
            ApplyDefaultState(startingCash, startingInfluencePoints, startingPerkPoints);

        currentJob = null;
        ResolvePendingMissionFailureFromSave();
        PersistState();
    }

    public static bool TrySpendCash(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (cash < amount)
            return false;

        cash -= amount;
        PersistState();
        return true;
    }

    public static void AddCash(int amount)
    {
        int clampedAmount = Mathf.Max(0, amount);
        if (clampedAmount <= 0)
            return;

        cash += clampedAmount;
        PersistState();
    }

    public static void AddInfluencePoints(int amount)
    {
        int clampedAmount = Mathf.Max(0, amount);
        if (clampedAmount <= 0)
            return;

        influencePoints += clampedAmount;
        PersistState();
    }

    public static bool TrySpendPerkPoints(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (perkPoints < amount)
            return false;

        perkPoints -= amount;
        PersistState();
        return true;
    }

    public static void AddPerkPoints(int amount)
    {
        int clampedAmount = Mathf.Max(0, amount);
        if (clampedAmount <= 0)
            return;

        perkPoints += clampedAmount;
        PersistState();
    }

    public static bool IsPerkUnlocked(HideoutPerkDefinition perkDefinition)
    {
        string perkId = ResolvePerkId(perkDefinition);
        return !string.IsNullOrWhiteSpace(perkId) && unlockedPerkIds.Contains(perkId);
    }

    public static bool UnlockPerk(HideoutPerkDefinition perkDefinition)
    {
        string perkId = ResolvePerkId(perkDefinition);
        if (string.IsNullOrWhiteSpace(perkId) || !unlockedPerkIds.Add(perkId))
            return false;

        switch (perkDefinition != null ? perkDefinition.Tier : HideoutPerkTier.TierI)
        {
            case HideoutPerkTier.TierI:
                unlockedTierOnePerkCount++;
                if (unlockedTierOnePerkCount >= 3)
                    tierTwoUnlocked = true;
                break;

            case HideoutPerkTier.TierII:
                unlockedTierTwoPerkCount++;
                if (unlockedTierTwoPerkCount >= 2)
                    tierThreeUnlocked = true;
                break;
        }

        PersistState();
        return true;
    }

    public static void SyncPerkTierUnlocks(IEnumerable<HideoutPerkDefinition> perkDefinitions)
    {
        int tierOneCount = 0;
        int tierTwoCount = 0;

        if (perkDefinitions != null)
        {
            foreach (HideoutPerkDefinition perkDefinition in perkDefinitions)
            {
                if (perkDefinition == null || !IsPerkUnlocked(perkDefinition))
                    continue;

                if (perkDefinition.Tier == HideoutPerkTier.TierI)
                    tierOneCount++;
                else if (perkDefinition.Tier == HideoutPerkTier.TierII)
                    tierTwoCount++;
            }
        }

        int previousTierOneCount = unlockedTierOnePerkCount;
        int previousTierTwoCount = unlockedTierTwoPerkCount;
        bool previousTierTwoUnlocked = tierTwoUnlocked;
        bool previousTierThreeUnlocked = tierThreeUnlocked;

        unlockedTierOnePerkCount = Mathf.Max(unlockedTierOnePerkCount, tierOneCount);
        unlockedTierTwoPerkCount = Mathf.Max(unlockedTierTwoPerkCount, tierTwoCount);
        tierTwoUnlocked |= unlockedTierOnePerkCount >= 3;
        tierThreeUnlocked |= unlockedTierTwoPerkCount >= 2;

        if (previousTierOneCount != unlockedTierOnePerkCount ||
            previousTierTwoCount != unlockedTierTwoPerkCount ||
            previousTierTwoUnlocked != tierTwoUnlocked ||
            previousTierThreeUnlocked != tierThreeUnlocked)
        {
            PersistState();
        }
    }

    public static bool IsPerkTierUnlocked(HideoutPerkTier perkTier)
    {
        return perkTier switch
        {
            HideoutPerkTier.TierI => true,
            HideoutPerkTier.TierII => tierTwoUnlocked || unlockedTierOnePerkCount >= 3,
            HideoutPerkTier.TierIII => tierThreeUnlocked || unlockedTierTwoPerkCount >= 2,
            _ => false
        };
    }

    public static void SetCurrentJob(HideoutJobDefinition jobDefinition)
    {
        currentJob = jobDefinition;
    }

    public static void ClearCurrentJob()
    {
        currentJob = null;
    }

    public static void SetActiveMissionJob(HideoutJobDefinition jobDefinition)
    {
        activeMissionJobId = ResolveJobId(jobDefinition);
        PersistState();
    }

    public static void ClearActiveMissionJob()
    {
        if (string.IsNullOrWhiteSpace(activeMissionJobId))
            return;

        activeMissionJobId = string.Empty;
        PersistState();
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

    public static bool IsJobFailed(HideoutJobDefinition jobDefinition)
    {
        string jobId = ResolveJobId(jobDefinition);
        return !string.IsNullOrWhiteSpace(jobId) && failedJobIds.Contains(jobId);
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
            ClearActiveMissionJob();
            return false;
        }

        string jobId = ResolveJobId(jobDefinition);
        if (string.IsNullOrWhiteSpace(jobId))
        {
            currentJob = null;
            ClearActiveMissionJob();
            return false;
        }

        currentJob = null;
        activeMissionJobId = string.Empty;
        if (!completedJobIds.Add(jobId))
        {
            PersistState();
            return false;
        }

        cash += Mathf.Max(0, jobDefinition.RewardCash);
        influencePoints += Mathf.Max(0, jobDefinition.RewardInfluencePoints);

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
        PersistState();
        return true;
    }

    private static void ApplyLoadedSnapshot(HideoutSaveSnapshot snapshot)
    {
        snapshot ??= new HideoutSaveSnapshot();
        cash = Mathf.Max(0, snapshot.Cash);
        influencePoints = Mathf.Max(0, snapshot.InfluencePoints);
        perkPoints = Mathf.Max(0, snapshot.PerkPoints);
        unlockedTierOnePerkCount = Mathf.Max(0, snapshot.UnlockedTierOnePerkCount);
        unlockedTierTwoPerkCount = Mathf.Max(0, snapshot.UnlockedTierTwoPerkCount);
        tierTwoUnlocked = snapshot.TierTwoUnlocked;
        tierThreeUnlocked = snapshot.TierThreeUnlocked;
        activeMissionJobId = snapshot.ActiveMissionJobId != null ? snapshot.ActiveMissionJobId.Trim() : string.Empty;
        pendingHideoutMessage = string.Empty;

        CopyIds(snapshot.CompletedJobIds, completedJobIds);
        CopyIds(snapshot.UnlockedJobIds, unlockedJobIds);
        CopyIds(snapshot.FailedJobIds, failedJobIds);
        CopyIds(snapshot.UnlockedPerkIds, unlockedPerkIds);
    }

    private static void ApplyDefaultState(int startingCash, int startingInfluencePoints, int startingPerkPoints)
    {
        cash = Mathf.Max(0, startingCash);
        influencePoints = Mathf.Max(0, startingInfluencePoints);
        perkPoints = Mathf.Max(0, startingPerkPoints);
        unlockedTierOnePerkCount = 0;
        unlockedTierTwoPerkCount = 0;
        tierTwoUnlocked = false;
        tierThreeUnlocked = false;
        activeMissionJobId = string.Empty;
        pendingHideoutMessage = string.Empty;
        completedJobIds.Clear();
        unlockedJobIds.Clear();
        failedJobIds.Clear();
        unlockedPerkIds.Clear();
    }

    private static void ResolvePendingMissionFailureFromSave()
    {
        if (string.IsNullOrWhiteSpace(activeMissionJobId))
            return;

        bool markedFailed = failedJobIds.Add(activeMissionJobId);
        activeMissionJobId = string.Empty;

        if (markedFailed)
            pendingHideoutMessage = "An in-progress job was marked as failed because the game was closed before completion.";

        PersistState();
    }

    private static void PersistState()
    {
        if (!initialized)
            return;

        HideoutSaveSnapshot snapshot = new()
        {
            Cash = Mathf.Max(0, cash),
            InfluencePoints = Mathf.Max(0, influencePoints),
            PerkPoints = Mathf.Max(0, perkPoints),
            UnlockedTierOnePerkCount = Mathf.Max(0, unlockedTierOnePerkCount),
            UnlockedTierTwoPerkCount = Mathf.Max(0, unlockedTierTwoPerkCount),
            TierTwoUnlocked = tierTwoUnlocked || unlockedTierOnePerkCount >= 3,
            TierThreeUnlocked = tierThreeUnlocked || unlockedTierTwoPerkCount >= 2,
            ActiveMissionJobId = activeMissionJobId ?? string.Empty,
            UnlockedPerkIds = BuildSortedIds(unlockedPerkIds),
            UnlockedJobIds = BuildSortedIds(unlockedJobIds),
            CompletedJobIds = BuildSortedIds(completedJobIds),
            FailedJobIds = BuildSortedIds(failedJobIds)
        };

        HideoutSaveSystem.Save(snapshot);
    }

    private static List<string> BuildSortedIds(HashSet<string> source)
    {
        List<string> ids = new();
        if (source == null || source.Count <= 0)
            return ids;

        foreach (string id in source)
        {
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id.Trim());
        }

        ids.Sort((left, right) => string.Compare(left, right, System.StringComparison.OrdinalIgnoreCase));
        return ids;
    }

    private static void CopyIds(List<string> source, HashSet<string> destination)
    {
        destination.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            string id = source[i] != null ? source[i].Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(id))
                destination.Add(id);
        }
    }

    private static string ResolveJobId(HideoutJobDefinition jobDefinition)
    {
        return jobDefinition != null ? jobDefinition.JobId : string.Empty;
    }

    private static string ResolvePerkId(HideoutPerkDefinition perkDefinition)
    {
        return perkDefinition != null ? perkDefinition.PerkId : string.Empty;
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
