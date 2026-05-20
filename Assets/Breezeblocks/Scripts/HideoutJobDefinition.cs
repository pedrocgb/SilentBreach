using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using Breezeblocks.WeaponSystem;

namespace Breezeblocks.HideoutSystem
{

public enum HideoutJobObjectiveType
{
    KillTarget,
    RetrieveItem,
    IncapacitateTarget
}

public enum HideoutJobFailureType
{
    DontHarmInnocent,
    DontKillInnocent,
    DontHarmAnyone,
    DontKillAnyone,
    DontBeDetected,
    TimeLimit
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

    public string ResolveDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(displayText))
            return displayText.Trim();

        string readableId = string.IsNullOrWhiteSpace(referenceId) ? "target" : referenceId.Trim();
        return objectiveType switch
        {
            HideoutJobObjectiveType.KillTarget => $"Kill {readableId}",
            HideoutJobObjectiveType.RetrieveItem => $"Retrieve {readableId}",
            HideoutJobObjectiveType.IncapacitateTarget => $"Incapacitate {readableId}",
            _ => readableId
        };
    }

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
    [ShowIf(nameof(UsesTimeLimit)), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float timeLimitSeconds = 300f;

    public HideoutJobFailureType FailureType => failureType;
    public string DisplayText => ResolveDisplayText();
    public float TimeLimitSeconds => Mathf.Max(0.01f, timeLimitSeconds);

    private bool UsesTimeLimit => failureType == HideoutJobFailureType.TimeLimit;

    public string ResolveDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(displayText))
            return displayText.Trim();

        return failureType switch
        {
            HideoutJobFailureType.DontHarmInnocent => "Do not harm innocents",
            HideoutJobFailureType.DontKillInnocent => "Do not kill innocents",
            HideoutJobFailureType.DontHarmAnyone => "Do not harm anyone",
            HideoutJobFailureType.DontKillAnyone => "Do not kill anyone",
            HideoutJobFailureType.DontBeDetected => "Do not be detected",
            HideoutJobFailureType.TimeLimit => $"Finish within {TimeLimitSeconds:0.#} seconds",
            _ => "Unknown failure condition"
        };
    }

    public void OnValidate()
    {
        displayText = displayText != null ? displayText.Trim() : string.Empty;
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

    [MinValue(0)]
    [SerializeField] internal int price = 100;

    public EquipmentItemData Item => item;
    public ProjectileData FirearmProjectile => firearmProjectile;
    public float AvailabilityProbability => availabilityProbability;
    public int MaxQuantity => maxQuantity;
    public int Price => price;

    private bool UsesProjectile => item is FirearmData;
}

[CreateAssetMenu(fileName = "HideoutJob", menuName = "Breezeblocks/Hideout/Job")]
public sealed class HideoutJobDefinition : ScriptableObject
{
    [FoldoutGroup("Job")]
    [SerializeField] private string jobTitle;

    [FoldoutGroup("Job"), TextArea(3, 8)]
    [SerializeField] private string jobDescription;

    [FoldoutGroup("Job")]
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

    [FoldoutGroup("Job")]
    [SerializeField] private string questScenePath = "Assets/Breezeblocks/Scenes/[2] Poker Scene.unity";

    [FoldoutGroup("Fence")]
    [SerializeField] private string shopTitle = "The Fence";

    [FoldoutGroup("Fence"), TextArea(2, 6)]
    [SerializeField] private string shopDescription;

    [FoldoutGroup("Fence"), PreviewField(96, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite shopImage;

    [FoldoutGroup("Fence"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutFenceOfferDefinition> fenceOffers = new();

    public string JobTitle => string.IsNullOrWhiteSpace(jobTitle) ? name : jobTitle;
    public string JobDescription => jobDescription ?? string.Empty;
    public string RewardText => rewardText ?? string.Empty;
    public string ObjectivesText => string.IsNullOrWhiteSpace(objectivesText)
        ? BuildFormattedList(gameplayObjectives, objective => objective?.DisplayText)
        : objectivesText ?? string.Empty;
    public string TermsOfFailureText => string.IsNullOrWhiteSpace(termsOfFailureText)
        ? BuildFormattedList(gameplayFailures, failure => failure?.DisplayText)
        : termsOfFailureText ?? string.Empty;
    public string FixerName => fixerName ?? string.Empty;
    public Sprite JobImage => jobImage;
    public string QuestScenePath => questScenePath ?? string.Empty;
    public string ShopTitle => string.IsNullOrWhiteSpace(shopTitle) ? "The Fence" : shopTitle;
    public string ShopDescription => shopDescription ?? string.Empty;
    public Sprite ShopImage => shopImage;
    public IReadOnlyList<HideoutFenceOfferDefinition> FenceOffers => fenceOffers;
    public IReadOnlyList<HideoutJobObjectiveDefinition> GameplayObjectives => gameplayObjectives;
    public IReadOnlyList<HideoutJobFailureDefinition> GameplayFailures => gameplayFailures;

    private void OnValidate()
    {
        jobTitle = jobTitle != null ? jobTitle.Trim() : string.Empty;
        jobDescription ??= string.Empty;
        rewardText ??= string.Empty;
        objectivesText ??= string.Empty;
        termsOfFailureText ??= string.Empty;
        fixerName = fixerName != null ? fixerName.Trim() : string.Empty;
        questScenePath = questScenePath != null ? questScenePath.Trim() : string.Empty;
        shopTitle = string.IsNullOrWhiteSpace(shopTitle) ? "The Fence" : shopTitle.Trim();
        shopDescription ??= string.Empty;
        fenceOffers ??= new List<HideoutFenceOfferDefinition>();
        gameplayObjectives ??= new List<HideoutJobObjectiveDefinition>();
        gameplayFailures ??= new List<HideoutJobFailureDefinition>();

        for (int i = 0; i < fenceOffers.Count; i++)
        {
            HideoutFenceOfferDefinition offer = fenceOffers[i];
            if (offer == null)
                continue;

            offer.availabilityProbability = Mathf.Clamp01(offer.availabilityProbability);
            offer.maxQuantity = Mathf.Max(1, offer.maxQuantity);
            offer.price = Mathf.Max(0, offer.price);

            if (offer.item is not FirearmData)
                offer.firearmProjectile = null;
        }

        for (int i = 0; i < gameplayObjectives.Count; i++)
            gameplayObjectives[i]?.OnValidate();

        for (int i = 0; i < gameplayFailures.Count; i++)
            gameplayFailures[i]?.OnValidate();
    }

    private static string BuildFormattedList<T>(IReadOnlyList<T> entries, Func<T, string> resolver)
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
