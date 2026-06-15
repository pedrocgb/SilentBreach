using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

public enum PlayerBadgeId
{
    Amador,
    Operador,
    Especialista,
    Chefao
}

public readonly struct PlayerProgressionAward
{
    /// <summary>
    /// Captures the sanitized progression state produced by an experience award.
    /// </summary>
    public PlayerProgressionAward(int experience, int level, int levelUps)
    {
        Experience = experience;
        Level = level;
        LevelUps = levelUps;
    }

    public int Experience { get; }
    public int Level { get; }
    public int LevelUps { get; }
}

public static class PlayerProgressionRules
{
    public const int MinimumLevel = 1;
    public const int MaximumLevel = 20;
    private const int BaseExperienceRequirement = 100;
    private const int ExperienceRequirementPerLevel = 35;

    /// <summary>
    /// Returns the experience required to advance from the supplied level.
    /// </summary>
    public static int GetExperienceNeededForNextLevel(int level)
    {
        int sanitizedLevel = SanitizeLevel(level);
        return sanitizedLevel >= MaximumLevel
            ? 0
            : BaseExperienceRequirement + ((sanitizedLevel - 1) * ExperienceRequirementPerLevel);
    }

    /// <summary>
    /// Applies experience to a level state and reports the sanitized result.
    /// </summary>
    public static PlayerProgressionAward ApplyExperience(int currentExperience, int currentLevel, int experienceAward)
    {
        int level = SanitizeLevel(currentLevel);
        int experience = SanitizeExperience(currentExperience, level);
        int remainingAward = Mathf.Max(0, experienceAward);
        int levelUps = 0;

        while (remainingAward > 0 && level < MaximumLevel)
        {
            int requiredExperience = GetExperienceNeededForNextLevel(level);
            int experienceUntilLevel = Mathf.Max(1, requiredExperience - experience);
            if (remainingAward < experienceUntilLevel)
            {
                experience += remainingAward;
                remainingAward = 0;
                continue;
            }

            remainingAward -= experienceUntilLevel;
            experience = 0;
            level++;
            levelUps++;
        }

        if (level >= MaximumLevel)
            experience = 0;

        return new PlayerProgressionAward(experience, level, levelUps);
    }

    /// <summary>
    /// Returns the experience reward contributed by a job difficulty.
    /// </summary>
    public static int GetDifficultyExperienceReward(HideoutJobLevel jobLevel)
    {
        return jobLevel switch
        {
            HideoutJobLevel.Easy => 40,
            HideoutJobLevel.Medium => 70,
            HideoutJobLevel.Hard => 120,
            HideoutJobLevel.Insane => 200,
            _ => 0
        };
    }

    /// <summary>
    /// Returns the badge represented by the supplied player level.
    /// </summary>
    public static PlayerBadgeId GetBadgeId(int level)
    {
        int sanitizedLevel = SanitizeLevel(level);
        if (sanitizedLevel <= 5)
            return PlayerBadgeId.Amador;

        if (sanitizedLevel <= 10)
            return PlayerBadgeId.Operador;

        if (sanitizedLevel <= 15)
            return PlayerBadgeId.Especialista;

        return PlayerBadgeId.Chefao;
    }

    /// <summary>
    /// Returns the one-to-five tier represented within the current badge.
    /// </summary>
    public static int GetBadgeTier(int level)
    {
        return ((SanitizeLevel(level) - 1) % 5) + 1;
    }

    /// <summary>
    /// Returns the Roman numeral used to display a badge tier.
    /// </summary>
    public static string GetRomanTier(int level)
    {
        return GetBadgeTier(level) switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => "I"
        };
    }

    /// <summary>
    /// Clamps a persisted or runtime level to the supported progression range.
    /// </summary>
    public static int SanitizeLevel(int level)
    {
        return Mathf.Clamp(level, MinimumLevel, MaximumLevel);
    }

    /// <summary>
    /// Clamps experience so it remains valid for the supplied level.
    /// </summary>
    public static int SanitizeExperience(int experience, int level)
    {
        int sanitizedLevel = SanitizeLevel(level);
        if (sanitizedLevel >= MaximumLevel)
            return 0;

        return Mathf.Clamp(experience, 0, GetExperienceNeededForNextLevel(sanitizedLevel) - 1);
    }
}

}
