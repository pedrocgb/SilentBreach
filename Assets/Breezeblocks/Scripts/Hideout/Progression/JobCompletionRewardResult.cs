namespace Breezeblocks.HideoutSystem
{

public readonly struct JobCompletionRewardResult
{
    /// <summary>
    /// Captures the before and after values committed by one successful job completion.
    /// </summary>
    public JobCompletionRewardResult(
        bool wasAwarded,
        int experienceAwarded,
        int experienceBefore,
        int experienceAfter,
        int levelBefore,
        int levelAfter,
        int levelUps,
        int perkPointsBefore,
        int perkPointsAfter,
        int cashBefore,
        int cashAfter)
    {
        WasAwarded = wasAwarded;
        ExperienceAwarded = experienceAwarded;
        ExperienceBefore = experienceBefore;
        ExperienceAfter = experienceAfter;
        LevelBefore = levelBefore;
        LevelAfter = levelAfter;
        LevelUps = levelUps;
        PerkPointsBefore = perkPointsBefore;
        PerkPointsAfter = perkPointsAfter;
        CashBefore = cashBefore;
        CashAfter = cashAfter;
    }

    public bool WasAwarded { get; }
    public int ExperienceAwarded { get; }
    public int ExperienceBefore { get; }
    public int ExperienceAfter { get; }
    public int LevelBefore { get; }
    public int LevelAfter { get; }
    public int LevelUps { get; }
    public int PerkPointsBefore { get; }
    public int PerkPointsAfter { get; }
    public int CashBefore { get; }
    public int CashAfter { get; }
    public int CashAwarded => CashAfter - CashBefore;
}

}
