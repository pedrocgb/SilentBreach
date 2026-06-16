using System;
using System.Collections.Generic;
using System.IO;
using Breezeblocks.Settings;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

[Serializable]
public sealed class HideoutSaveSnapshot
{
    public bool HasHideoutProgress;
    public int Cash;
    public int InfluencePoints;
    public int PerkPoints;
    public int CurrentExperience;
    public int CurrentLevel = PlayerProgressionRules.MinimumLevel;
    public int BadgeId;
    public int UnlockedTierOnePerkCount;
    public int UnlockedTierTwoPerkCount;
    public bool TierTwoUnlocked;
    public bool TierThreeUnlocked;
    public string ActiveMissionJobId = string.Empty;
    public List<string> EquippedPerkIds = new();
    public List<string> UnlockedPerkIds = new();
    public List<string> UnlockedJobIds = new();
    public List<string> CompletedJobIds = new();
    public List<string> FailedJobIds = new();
    public GameSettingsSaveData Settings = GameSettingsSaveData.CreateDefaults();
}

public static class HideoutSaveSystem
{
    private const int CurrentSchemaVersion = 3;
    private const string SaveFileName = "hideout_save.json";
    private const string BackupExtension = ".bak";
    private const string TempExtension = ".tmp";

    [Serializable]
    private sealed class HideoutSaveVersionProbe
    {
        public int schemaVersion;
    }

    [Serializable]
    private sealed class HideoutSaveEnvelopeV1
    {
        public int schemaVersion = 1;
        public long savedAtUtcTicks;
        public HideoutSavePayloadV1 payload = new();
    }

    [Serializable]
    private sealed class HideoutSaveEnvelopeV2
    {
        public int schemaVersion = 2;
        public long savedAtUtcTicks;
        public HideoutSavePayloadV2 payload = new();
    }

    [Serializable]
    private sealed class HideoutSaveEnvelopeV3
    {
        public int schemaVersion = CurrentSchemaVersion;
        public long savedAtUtcTicks;
        public HideoutSavePayloadV3 payload = new();
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
        public List<string> equippedPerkIds = new();
        public List<string> unlockedPerkIds = new();
        public List<string> unlockedJobIds = new();
        public List<string> completedJobIds = new();
        public List<string> failedJobIds = new();
    }

    [Serializable]
    private sealed class HideoutSavePayloadV2
    {
        public bool hasHideoutProgress;
        public HideoutSavePayloadV1 hideoutProgress = new();
        public GameSettingsSaveData settings = GameSettingsSaveData.CreateDefaults();
    }

    [Serializable]
    private sealed class PlayerProgressionSavePayloadV3
    {
        public int currentExperience;
        public int currentLevel = PlayerProgressionRules.MinimumLevel;
        public int badgeId;
    }

    [Serializable]
    private sealed class HideoutSavePayloadV3
    {
        public bool hasHideoutProgress;
        public HideoutSavePayloadV1 hideoutProgress = new();
        public PlayerProgressionSavePayloadV3 playerProgression = new();
        public GameSettingsSaveData settings = GameSettingsSaveData.CreateDefaults();
    }

    /// <summary>
    /// Loads the latest valid primary or backup save snapshot.
    /// </summary>
    public static bool TryLoad(out HideoutSaveSnapshot snapshot)
    {
        if (TryReadSnapshot(GetPrimaryPath(), out snapshot))
            return true;

        if (TryReadSnapshot(GetBackupPath(), out snapshot))
            return true;

        snapshot = new HideoutSaveSnapshot();
        return false;
    }

    /// <summary>
    /// Writes a versioned snapshot atomically while preserving a backup.
    /// </summary>
    public static void Save(HideoutSaveSnapshot snapshot)
    {
        snapshot ??= new HideoutSaveSnapshot();
        if (GameSettingsRuntime.IsInitialized)
            snapshot.Settings = GameSettingsRuntime.ExportSaveData();

        try
        {
            string primaryPath = GetPrimaryPath();
            string backupPath = GetBackupPath();
            string tempPath = GetTempPath();
            string directoryPath = Path.GetDirectoryName(primaryPath);
            if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            HideoutSaveEnvelopeV3 envelope = new()
            {
                schemaVersion = CurrentSchemaVersion,
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                payload = BuildPayloadV3(snapshot)
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

    /// <summary>
    /// Deletes every primary, backup, and temporary file owned by the hideout save system.
    /// </summary>
    public static int DeleteAllSaveFiles()
    {
        int deletedFileCount = 0;
        deletedFileCount += TryDeleteSaveFile(GetPrimaryPath());
        deletedFileCount += TryDeleteSaveFile(GetBackupPath());
        deletedFileCount += TryDeleteSaveFile(GetTempPath());
        return deletedFileCount;
    }

    /// <summary>
    /// Deletes one save-system-owned file and reports whether a file was removed.
    /// </summary>
    private static int TryDeleteSaveFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return 0;

        try
        {
            File.Delete(filePath);
            return 1;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not delete save file at {filePath}: {exception.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Attempts to deserialize a supported save schema from the supplied file.
    /// </summary>
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
                HideoutSaveVersionProbe versionProbe = JsonUtility.FromJson<HideoutSaveVersionProbe>(json);
                if (versionProbe == null)
                    return false;

                if (versionProbe.schemaVersion >= 3)
                {
                    HideoutSaveEnvelopeV3 envelope = JsonUtility.FromJson<HideoutSaveEnvelopeV3>(json);
                    if (envelope?.payload == null)
                        return false;

                    snapshot = BuildSnapshot(envelope.payload);
                    return true;
                }

                if (versionProbe.schemaVersion == 2)
                {
                    HideoutSaveEnvelopeV2 envelope = JsonUtility.FromJson<HideoutSaveEnvelopeV2>(json);
                    if (envelope?.payload == null)
                        return false;

                    snapshot = BuildSnapshot(envelope.payload);
                    return true;
                }

                HideoutSaveEnvelopeV1 legacyEnvelope = JsonUtility.FromJson<HideoutSaveEnvelopeV1>(json);
                if (legacyEnvelope?.payload == null)
                    return false;

                snapshot = BuildSnapshot(legacyEnvelope.payload);
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

    /// <summary>
    /// Migrates a version-one hideout payload into the current runtime snapshot.
    /// </summary>
    private static HideoutSaveSnapshot BuildSnapshot(HideoutSavePayloadV1 payload)
    {
        payload ??= new HideoutSavePayloadV1();
        return new HideoutSaveSnapshot
        {
            HasHideoutProgress = true,
            Cash = Mathf.Max(0, payload.cash),
            InfluencePoints = Mathf.Max(0, payload.influencePoints),
            PerkPoints = Mathf.Max(0, payload.perkPoints),
            UnlockedTierOnePerkCount = Mathf.Max(0, payload.unlockedTierOnePerkCount),
            UnlockedTierTwoPerkCount = Mathf.Max(0, payload.unlockedTierTwoPerkCount),
            TierTwoUnlocked = payload.tierTwoUnlocked,
            TierThreeUnlocked = payload.tierThreeUnlocked,
            ActiveMissionJobId = SanitizeId(payload.activeMissionJobId),
            EquippedPerkIds = SanitizeIds(payload.equippedPerkIds),
            UnlockedPerkIds = SanitizeIds(payload.unlockedPerkIds),
            UnlockedJobIds = SanitizeIds(payload.unlockedJobIds),
            CompletedJobIds = SanitizeIds(payload.completedJobIds),
            FailedJobIds = SanitizeIds(payload.failedJobIds),
            Settings = GameSettingsSaveData.CreateDefaults()
        };
    }

    /// <summary>
    /// Converts the current version-two payload into a sanitized runtime snapshot.
    /// </summary>
    private static HideoutSaveSnapshot BuildSnapshot(HideoutSavePayloadV2 payload)
    {
        payload ??= new HideoutSavePayloadV2();
        HideoutSaveSnapshot snapshot = BuildSnapshot(payload.hideoutProgress);
        snapshot.HasHideoutProgress = payload.hasHideoutProgress;
        snapshot.Settings = payload.settings?.Clone() ?? GameSettingsSaveData.CreateDefaults();
        snapshot.Settings.Sanitize();
        return snapshot;
    }

    /// <summary>
    /// Converts the current version-three payload into a sanitized runtime snapshot.
    /// </summary>
    private static HideoutSaveSnapshot BuildSnapshot(HideoutSavePayloadV3 payload)
    {
        payload ??= new HideoutSavePayloadV3();
        HideoutSaveSnapshot snapshot = BuildSnapshot(payload.hideoutProgress);
        PlayerProgressionSavePayloadV3 progression = payload.playerProgression ?? new PlayerProgressionSavePayloadV3();
        int sanitizedLevel = PlayerProgressionRules.SanitizeLevel(progression.currentLevel);

        snapshot.HasHideoutProgress = payload.hasHideoutProgress;
        snapshot.CurrentLevel = sanitizedLevel;
        snapshot.CurrentExperience = PlayerProgressionRules.SanitizeExperience(progression.currentExperience, sanitizedLevel);
        snapshot.BadgeId = (int)PlayerProgressionRules.GetBadgeId(sanitizedLevel);
        snapshot.Settings = payload.settings?.Clone() ?? GameSettingsSaveData.CreateDefaults();
        snapshot.Settings.Sanitize();
        return snapshot;
    }

    /// <summary>
    /// Builds the current version-three payload from a runtime snapshot.
    /// </summary>
    private static HideoutSavePayloadV3 BuildPayloadV3(HideoutSaveSnapshot snapshot)
    {
        GameSettingsSaveData settings = snapshot.Settings?.Clone() ?? GameSettingsSaveData.CreateDefaults();
        settings.Sanitize();
        int sanitizedLevel = PlayerProgressionRules.SanitizeLevel(snapshot.CurrentLevel);

        return new HideoutSavePayloadV3
        {
            hasHideoutProgress = snapshot.HasHideoutProgress,
            hideoutProgress = BuildPayloadV1(snapshot),
            playerProgression = new PlayerProgressionSavePayloadV3
            {
                currentExperience = PlayerProgressionRules.SanitizeExperience(snapshot.CurrentExperience, sanitizedLevel),
                currentLevel = sanitizedLevel,
                badgeId = (int)PlayerProgressionRules.GetBadgeId(sanitizedLevel)
            },
            settings = settings
        };
    }

    /// <summary>
    /// Builds the stable hideout-progress portion shared by current and legacy saves.
    /// </summary>
    private static HideoutSavePayloadV1 BuildPayloadV1(HideoutSaveSnapshot snapshot)
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
            equippedPerkIds = SanitizeIds(snapshot.EquippedPerkIds),
            unlockedPerkIds = SanitizeIds(snapshot.UnlockedPerkIds),
            unlockedJobIds = SanitizeIds(snapshot.UnlockedJobIds),
            completedJobIds = SanitizeIds(snapshot.CompletedJobIds),
            failedJobIds = SanitizeIds(snapshot.FailedJobIds)
        };
    }

    /// <summary>
    /// Removes empty and duplicate identifiers before persistence.
    /// </summary>
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

    /// <summary>
    /// Trims a persisted stable identifier.
    /// </summary>
    private static string SanitizeId(string value)
    {
        return value != null ? value.Trim() : string.Empty;
    }

    /// <summary>
    /// Returns the primary save file path.
    /// </summary>
    private static string GetPrimaryPath()
    {
        return Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    /// <summary>
    /// Returns the backup save file path.
    /// </summary>
    private static string GetBackupPath()
    {
        return GetPrimaryPath() + BackupExtension;
    }

    /// <summary>
    /// Returns the temporary atomic-write file path.
    /// </summary>
    private static string GetTempPath()
    {
        return GetPrimaryPath() + TempExtension;
    }
}

}
