using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

[Serializable]
public sealed class HideoutSaveSnapshot
{
    public int Cash;
    public int InfluencePoints;
    public int PerkPoints;
    public int UnlockedTierOnePerkCount;
    public int UnlockedTierTwoPerkCount;
    public bool TierTwoUnlocked;
    public bool TierThreeUnlocked;
    public string ActiveMissionJobId = string.Empty;
    public List<string> UnlockedPerkIds = new();
    public List<string> UnlockedJobIds = new();
    public List<string> CompletedJobIds = new();
    public List<string> FailedJobIds = new();
}

public static class HideoutSaveSystem
{
    private const int CurrentSchemaVersion = 1;
    private const string SaveFileName = "hideout_save.json";
    private const string BackupExtension = ".bak";
    private const string TempExtension = ".tmp";

    [Serializable]
    private sealed class HideoutSaveEnvelope
    {
        public int schemaVersion = CurrentSchemaVersion;
        public long savedAtUtcTicks;
        public HideoutSavePayloadV1 payload = new();
    }

    [Serializable]
    private sealed class HideoutSavePayloadV1
    {
        public int cash;
        public int influencePoints;
        public int perkPoints;
        public int unlockedTierOnePerkCount;
        public int unlockedTierTwoPerkCount;
        public bool tierTwoUnlocked;
        public bool tierThreeUnlocked;
        public string activeMissionJobId = string.Empty;
        public List<string> unlockedPerkIds = new();
        public List<string> unlockedJobIds = new();
        public List<string> completedJobIds = new();
        public List<string> failedJobIds = new();
    }

    public static bool TryLoad(out HideoutSaveSnapshot snapshot)
    {
        if (TryReadSnapshot(GetPrimaryPath(), out snapshot))
            return true;

        if (TryReadSnapshot(GetBackupPath(), out snapshot))
            return true;

        snapshot = new HideoutSaveSnapshot();
        return false;
    }

    public static void Save(HideoutSaveSnapshot snapshot)
    {
        snapshot ??= new HideoutSaveSnapshot();

        try
        {
            string primaryPath = GetPrimaryPath();
            string backupPath = GetBackupPath();
            string tempPath = GetTempPath();
            string directoryPath = Path.GetDirectoryName(primaryPath);
            if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            HideoutSaveEnvelope envelope = new()
            {
                schemaVersion = CurrentSchemaVersion,
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                payload = BuildPayload(snapshot)
            };

            string json = JsonUtility.ToJson(envelope, true);
            File.WriteAllText(tempPath, json);

            if (File.Exists(primaryPath))
                File.Copy(primaryPath, backupPath, overwrite: true);

            if (File.Exists(primaryPath))
                File.Delete(primaryPath);

            File.Move(tempPath, primaryPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not save hideout progress: {exception.Message}");
        }
    }

    private static bool TryReadSnapshot(string filePath, out HideoutSaveSnapshot snapshot)
    {
        snapshot = new HideoutSaveSnapshot();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        string json = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            if (json.IndexOf("\"schemaVersion\"", StringComparison.Ordinal) >= 0)
            {
                HideoutSaveEnvelope envelope = JsonUtility.FromJson<HideoutSaveEnvelope>(json);
                if (envelope?.payload == null)
                    return false;

                snapshot = BuildSnapshot(envelope.payload);
                return true;
            }

            if (json.IndexOf("\"cash\"", StringComparison.Ordinal) >= 0)
            {
                HideoutSavePayloadV1 legacyPayload = JsonUtility.FromJson<HideoutSavePayloadV1>(json);
                if (legacyPayload == null)
                    return false;

                snapshot = BuildSnapshot(legacyPayload);
                return true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load hideout save file at {filePath}: {exception.Message}");
        }

        return false;
    }

    private static HideoutSaveSnapshot BuildSnapshot(HideoutSavePayloadV1 payload)
    {
        payload ??= new HideoutSavePayloadV1();
        return new HideoutSaveSnapshot
        {
            Cash = Mathf.Max(0, payload.cash),
            InfluencePoints = Mathf.Max(0, payload.influencePoints),
            PerkPoints = Mathf.Max(0, payload.perkPoints),
            UnlockedTierOnePerkCount = Mathf.Max(0, payload.unlockedTierOnePerkCount),
            UnlockedTierTwoPerkCount = Mathf.Max(0, payload.unlockedTierTwoPerkCount),
            TierTwoUnlocked = payload.tierTwoUnlocked,
            TierThreeUnlocked = payload.tierThreeUnlocked,
            ActiveMissionJobId = SanitizeId(payload.activeMissionJobId),
            UnlockedPerkIds = SanitizeIds(payload.unlockedPerkIds),
            UnlockedJobIds = SanitizeIds(payload.unlockedJobIds),
            CompletedJobIds = SanitizeIds(payload.completedJobIds),
            FailedJobIds = SanitizeIds(payload.failedJobIds)
        };
    }

    private static HideoutSavePayloadV1 BuildPayload(HideoutSaveSnapshot snapshot)
    {
        return new HideoutSavePayloadV1
        {
            cash = Mathf.Max(0, snapshot.Cash),
            influencePoints = Mathf.Max(0, snapshot.InfluencePoints),
            perkPoints = Mathf.Max(0, snapshot.PerkPoints),
            unlockedTierOnePerkCount = Mathf.Max(0, snapshot.UnlockedTierOnePerkCount),
            unlockedTierTwoPerkCount = Mathf.Max(0, snapshot.UnlockedTierTwoPerkCount),
            tierTwoUnlocked = snapshot.TierTwoUnlocked,
            tierThreeUnlocked = snapshot.TierThreeUnlocked,
            activeMissionJobId = SanitizeId(snapshot.ActiveMissionJobId),
            unlockedPerkIds = SanitizeIds(snapshot.UnlockedPerkIds),
            unlockedJobIds = SanitizeIds(snapshot.UnlockedJobIds),
            completedJobIds = SanitizeIds(snapshot.CompletedJobIds),
            failedJobIds = SanitizeIds(snapshot.FailedJobIds)
        };
    }

    private static List<string> SanitizeIds(List<string> source)
    {
        List<string> sanitized = new();
        if (source == null || source.Count <= 0)
            return sanitized;

        HashSet<string> uniqueIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < source.Count; i++)
        {
            string sanitizedId = SanitizeId(source[i]);
            if (string.IsNullOrWhiteSpace(sanitizedId) || !uniqueIds.Add(sanitizedId))
                continue;

            sanitized.Add(sanitizedId);
        }

        return sanitized;
    }

    private static string SanitizeId(string value)
    {
        return value != null ? value.Trim() : string.Empty;
    }

    private static string GetPrimaryPath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    private static string GetBackupPath()
    {
        return GetPrimaryPath() + BackupExtension;
    }

    private static string GetTempPath()
    {
        return GetPrimaryPath() + TempExtension;
    }
}

}
