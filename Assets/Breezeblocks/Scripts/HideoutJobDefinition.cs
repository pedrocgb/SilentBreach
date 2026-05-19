using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Breezeblocks.WeaponSystem;

namespace Breezeblocks.HideoutSystem
{

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
    public string ObjectivesText => objectivesText ?? string.Empty;
    public string TermsOfFailureText => termsOfFailureText ?? string.Empty;
    public string FixerName => fixerName ?? string.Empty;
    public Sprite JobImage => jobImage;
    public string QuestScenePath => questScenePath ?? string.Empty;
    public string ShopTitle => string.IsNullOrWhiteSpace(shopTitle) ? "The Fence" : shopTitle;
    public string ShopDescription => shopDescription ?? string.Empty;
    public Sprite ShopImage => shopImage;
    public IReadOnlyList<HideoutFenceOfferDefinition> FenceOffers => fenceOffers;

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
    }
}

}
