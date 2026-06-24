using System;
using System.Collections.Generic;
using Breezeblocks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using Breezeblocks.WeaponSystem;

namespace Breezeblocks.HideoutSystem
{

public enum HideoutJobObjectiveType
{
    KillTarget,
    RetrieveItem,
    IncapacitateTarget,
    Kidnapping,
    ActivateObject
}

public enum HideoutJobFailureType
{
    DontHarmInnocent,
    DontKillInnocent,
    DontHarmAnyone,
    DontKillAnyone,
    DontAlert,
    DontBeDetected,
    TimeLimit
}

public enum HideoutJobLevel
{
    Easy,
    Medium,
    Hard,
    Insane
}

[Serializable]
public sealed class HideoutJobObjectiveDefinition
{
    [SerializeField] private HideoutJobObjectiveType objectiveType;
    [SerializeField] private string referenceId;
    [SerializeField] private string displayText;
    [MinValue(1)]
    [SerializeField] private int requiredCount = 1;

    public HideoutJobObjectiveType ObjectiveType => objectiveType;
    public string ReferenceId => referenceId ?? string.Empty;
    public string DisplayText => ResolveDisplayText();
    public int RequiredCount => Mathf.Max(1, requiredCount);

    /// <summary>
    /// Resolves the objective label shown to the player for this definition.
    /// </summary>
    public string ResolveDisplayText()
    {
        return HideoutJobTextUtility.ResolveObjectiveDisplayText(objectiveType, referenceId, displayText);
    }

    /// <summary>
    /// Normalizes author-facing objective values inside the inspector.
    /// </summary>
    public void OnValidate()
    {
        referenceId = referenceId != null ? referenceId.Trim() : string.Empty;
        displayText = displayText != null ? displayText.Trim() : string.Empty;
        requiredCount = Mathf.Max(1, requiredCount);
    }
}

[Serializable]
public sealed class HideoutJobFailureDefinition
{
    [SerializeField] private HideoutJobFailureType failureType;
    [SerializeField] private string displayText;
    [TextArea(2, 4)]
    [SerializeField] private string failureScreenMessage;
    [ShowIf(nameof(UsesTimeLimit)), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float timeLimitSeconds = 300f;

    public HideoutJobFailureType FailureType => failureType;
    public string DisplayText => ResolveDisplayText();
    public string FailureScreenMessage => ResolveFailureScreenMessage();
    public float TimeLimitSeconds => Mathf.Max(0.01f, timeLimitSeconds);

    private bool UsesTimeLimit => failureType == HideoutJobFailureType.TimeLimit;

    /// <summary>
    /// Resolves the failure label shown to the player for this definition.
    /// </summary>
    public string ResolveDisplayText()
    {
        return HideoutJobTextUtility.ResolveFailureDisplayText(failureType, displayText, TimeLimitSeconds);
    }

    /// <summary>
    /// Resolves the failure screen message shown if this rule is broken.
    /// </summary>
    public string ResolveFailureScreenMessage()
    {
        return HideoutJobTextUtility.ResolveFailureScreenMessage(failureType, failureScreenMessage);
    }

    /// <summary>
    /// Normalizes author-facing failure values inside the inspector.
    /// </summary>
    public void OnValidate()
    {
        displayText = displayText != null ? displayText.Trim() : string.Empty;
        failureScreenMessage = failureScreenMessage != null ? failureScreenMessage.Trim() : string.Empty;
        timeLimitSeconds = Mathf.Max(0.01f, timeLimitSeconds);
    }
}

[Serializable]
public sealed class HideoutFenceOfferDefinition
{
    [AssetsOnly]
    [SerializeField] internal EquipmentItemData item;

    [ShowIf(nameof(UsesProjectile)), AssetsOnly]
    [SerializeField] internal ProjectileData firearmProjectile;

    [Range(0f, 1f)]
    [SerializeField] internal float availabilityProbability = 1f;

    [MinValue(1)]
    [SerializeField] internal int maxQuantity = 1;

    public EquipmentItemData Item => item;
    public ProjectileData FirearmProjectile => firearmProjectile;
    public float AvailabilityProbability => availabilityProbability;
    public int MaxQuantity => maxQuantity;

    private bool UsesProjectile => item is FirearmData;
}

[CreateAssetMenu(fileName = "HideoutJob", menuName = "Breezeblocks/Hideout/Job")]
public sealed class HideoutJobDefinition : ScriptableObject
{
    [FoldoutGroup("Job")]
    [SerializeField] private string jobTitle;

    [FoldoutGroup("Job")]
    [SerializeField] private string jobId;

    [FoldoutGroup("Job"), TextArea(3, 8)]
    [SerializeField] private string jobDescription;

    [FoldoutGroup("Job Briefing")]
    [SerializeField] private string briefingTitle;

    [FoldoutGroup("Job Briefing"), TextArea(6, 14)]
    [SerializeField] private string jobBriefing;

    [FoldoutGroup("Job")]
    [SerializeField] private HideoutJobLevel jobLevel = HideoutJobLevel.Easy;

    [FoldoutGroup("Job")]
    [SerializeField] private HideoutJobType jobType = HideoutJobType.Furto;

    [FoldoutGroup("Rewards"), MinValue(0)]
    [SerializeField] private int rewardCash;

    [FoldoutGroup("Rewards"), MinValue(0)]
    [SerializeField] private int rewardInfluencePoints;

    [FoldoutGroup("Rewards"), TextArea(1, 4)]
    [SerializeField] private string rewardText;

    [FoldoutGroup("Job"), TextArea(2, 6)]
    [SerializeField] private string objectivesText;

    [FoldoutGroup("Job"), TextArea(2, 6)]
    [SerializeField] private string termsOfFailureText;

    [FoldoutGroup("Gameplay"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutJobObjectiveDefinition> gameplayObjectives = new();

    [FoldoutGroup("Gameplay"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutJobFailureDefinition> gameplayFailures = new();

    [FoldoutGroup("Job")]
    [SerializeField] private string fixerName;

    [FoldoutGroup("Job"), PreviewField(96, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite jobImage;

    [FoldoutGroup("Gameplay"), LabelText("Mission Scene Build Index"), MinValue(-1)]
    [SerializeField] private int missionSceneBuildIndex = 1;

    [FoldoutGroup("Gameplay"), LabelText("Mission Scene Fallback Name")]
    [FormerlySerializedAs("missionScenePath")]
    [FormerlySerializedAs("questScenePath")]
    [SerializeField] private string missionSceneName = "Poker Scene";

    [FoldoutGroup("Gameplay"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutJobDefinition> unlockJobs = new();

    [FoldoutGroup("Fence")]
    [SerializeField] private string shopTitle = "The Fence";

    [FoldoutGroup("Fence"), TextArea(2, 6)]
    [SerializeField] private string shopDescription;

    [FoldoutGroup("Fence"), PreviewField(96, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite shopImage;

    [FoldoutGroup("Fence"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutFenceOfferDefinition> fenceOffers = new();

    public string JobTitle => string.IsNullOrWhiteSpace(jobTitle) ? name : jobTitle;
    public string JobId => string.IsNullOrWhiteSpace(jobId) ? name : jobId;
    public string JobDescription => jobDescription ?? string.Empty;
    public string BriefingTitle => string.IsNullOrWhiteSpace(briefingTitle) ? JobTitle : briefingTitle;
    public string JobBriefing => jobBriefing ?? string.Empty;
    public HideoutJobLevel JobLevel => jobLevel;
    public HideoutJobType JobType => jobType;
    public string JobTypeDisplayName => HideoutJobTypeUtility.GetDisplayName(jobType);
    public string JobTypeDescription => HideoutJobTypeUtility.GetDescription(jobType);
    public int JobTypeExperienceReward => HideoutJobTypeUtility.GetExperienceReward(jobType);
    public int DifficultyExperienceReward => PlayerProgressionRules.GetDifficultyExperienceReward(jobLevel);
    public int TotalExperienceReward => JobTypeExperienceReward + DifficultyExperienceReward;
    public int RewardCash => Mathf.Max(0, rewardCash);
    public int RewardInfluencePoints => Mathf.Max(0, rewardInfluencePoints);
    public string RewardText => rewardText != null ? rewardText.Trim() : string.Empty;
    public string RewardSummaryText => BuildRewardSummaryText();
    public string ObjectivesText => string.IsNullOrWhiteSpace(objectivesText)
        ? HideoutJobTextUtility.BuildFormattedList(gameplayObjectives, objective => objective?.DisplayText)
        : objectivesText ?? string.Empty;
    public string TermsOfFailureText => string.IsNullOrWhiteSpace(termsOfFailureText)
        ? HideoutJobTextUtility.BuildFormattedList(gameplayFailures, failure => failure?.DisplayText)
        : termsOfFailureText ?? string.Empty;
    public string FixerName => fixerName ?? string.Empty;
    public Sprite JobImage => jobImage;
    public int MissionSceneBuildIndex => missionSceneBuildIndex;
    public string MissionSceneName => HideoutJobTextUtility.NormalizeSceneName(missionSceneName);
    public bool HasMissionSceneReference => SceneLoadUtility.HasSceneReference(missionSceneBuildIndex, missionSceneName);
    public string MissionScenePath => MissionSceneName;
    public string QuestScenePath => MissionSceneName;
    public string ShopTitle => string.IsNullOrWhiteSpace(shopTitle) ? "The Fence" : shopTitle;
    public string ShopDescription => shopDescription ?? string.Empty;
    public Sprite ShopImage => shopImage;
    public IReadOnlyList<HideoutFenceOfferDefinition> FenceOffers => fenceOffers;
    public IReadOnlyList<HideoutJobObjectiveDefinition> GameplayObjectives => gameplayObjectives;
    public IReadOnlyList<HideoutJobFailureDefinition> GameplayFailures => gameplayFailures;
    public IReadOnlyList<HideoutJobDefinition> UnlockJobs => unlockJobs;

    /// <summary>
    /// Normalizes job authoring values and child definitions inside the inspector.
    /// </summary>
    private void OnValidate()
    {
        jobTitle = jobTitle != null ? jobTitle.Trim() : string.Empty;
        jobId = string.IsNullOrWhiteSpace(jobId) ? name : jobId.Trim();
        jobDescription ??= string.Empty;
        briefingTitle = briefingTitle != null ? briefingTitle.Trim() : string.Empty;
        jobBriefing ??= string.Empty;
        rewardCash = Mathf.Max(0, rewardCash);
        rewardInfluencePoints = Mathf.Max(0, rewardInfluencePoints);
        rewardText ??= string.Empty;
        objectivesText ??= string.Empty;
        termsOfFailureText ??= string.Empty;
        fixerName = fixerName != null ? fixerName.Trim() : string.Empty;
        missionSceneBuildIndex = Mathf.Max(-1, missionSceneBuildIndex);
        missionSceneName = HideoutJobTextUtility.NormalizeSceneName(missionSceneName);
        shopTitle = string.IsNullOrWhiteSpace(shopTitle) ? "The Fence" : shopTitle.Trim();
        shopDescription ??= string.Empty;
        fenceOffers ??= new List<HideoutFenceOfferDefinition>();
        gameplayObjectives ??= new List<HideoutJobObjectiveDefinition>();
        gameplayFailures ??= new List<HideoutJobFailureDefinition>();
        unlockJobs ??= new List<HideoutJobDefinition>();

        for (int i = 0; i < fenceOffers.Count; i++)
        {
            HideoutFenceOfferDefinition offer = fenceOffers[i];
            if (offer == null)
                continue;

            offer.availabilityProbability = Mathf.Clamp01(offer.availabilityProbability);
            offer.maxQuantity = Mathf.Max(1, offer.maxQuantity);

            if (offer.item is not FirearmData)
                offer.firearmProjectile = null;
        }

        for (int i = 0; i < gameplayObjectives.Count; i++)
            gameplayObjectives[i]?.OnValidate();

        for (int i = 0; i < gameplayFailures.Count; i++)
            gameplayFailures[i]?.OnValidate();
    }

    /// <summary>
    /// Builds the compact reward summary displayed in hideout job summaries.
    /// </summary>
    private string BuildRewardSummaryText()
    {
        return HideoutJobTextUtility.BuildRewardSummaryText(RewardCash, RewardInfluencePoints, rewardText);
    }
}

}
