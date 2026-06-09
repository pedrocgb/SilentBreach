using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Breezeblocks.WeaponSystem;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

public enum HideoutPerkTier
{
    TierI,
    TierII,
    TierIII
}

[CreateAssetMenu(fileName = "HideoutPerk", menuName = "Breezeblocks/Hideout/Perk")]
public sealed class HideoutPerkDefinition : ScriptableObject
{
    [FoldoutGroup("Perk")]
    [SerializeField] private string perkName;

    [FoldoutGroup("Perk")]
    [SerializeField] private string perkId;

    [FoldoutGroup("Perk"), MinValue(0)]
    [SerializeField] private int cost = 1;

    [FoldoutGroup("Perk"), TextArea(2, 6)]
    [SerializeField] private string description;

    [FoldoutGroup("Perk"), TextArea(2, 6)]
    [SerializeField] private string effect;

    [FoldoutGroup("Perk")]
    [SerializeField] private HideoutPerkTier tier = HideoutPerkTier.TierI;

    [FoldoutGroup("Runtime"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutPerkRuntimeEffectDefinition> runtimeEffects = new();

    [FoldoutGroup("Visuals"), PreviewField(96, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite icon;

    public string PerkName => string.IsNullOrWhiteSpace(perkName) ? name : perkName;
    public string PerkId => string.IsNullOrWhiteSpace(perkId) ? name : perkId;
    public int Cost => Mathf.Max(0, cost);
    public string Description => description ?? string.Empty;
    public string Effect => effect ?? string.Empty;
    public HideoutPerkTier Tier => tier;
    public IReadOnlyList<HideoutPerkRuntimeEffectDefinition> RuntimeEffects => runtimeEffects;
    public Sprite Icon => icon;

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Runtime")]
    private void ApplySuggestedRuntimeDefaults()
    {
        runtimeEffects = BuildDefaultRuntimeEffects();
    }

    private void OnValidate()
    {
        perkName = perkName != null ? perkName.Trim() : string.Empty;
        perkId = string.IsNullOrWhiteSpace(perkId) ? name : perkId.Trim();
        cost = Mathf.Max(0, cost);
        description = description != null ? description.Trim() : string.Empty;
        effect = effect != null ? effect.Trim() : string.Empty;
        runtimeEffects ??= new List<HideoutPerkRuntimeEffectDefinition>();

        if (runtimeEffects.Count == 0)
            runtimeEffects = BuildDefaultRuntimeEffects();

        for (int i = 0; i < runtimeEffects.Count; i++)
            runtimeEffects[i]?.OnValidate();
    }

    private List<HideoutPerkRuntimeEffectDefinition> BuildDefaultRuntimeEffects()
    {
        string normalizedKey = NormalizeKey(PerkId);
        if (string.IsNullOrWhiteSpace(normalizedKey))
            normalizedKey = NormalizeKey(PerkName);

        List<HideoutPerkRuntimeEffectDefinition> defaults = new();
        switch (normalizedKey)
        {
            case "ironlungs":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.MaxStaminaFlat, 40f));
                break;

            case "sharpinstinct":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.MaxFocusFlat, 20f));
                break;

            case "armoredreflex":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.ArmorRotationPenaltyMultiplier, 0f));
                break;

            case "efficientstrikes":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.MeleeStaminaCostMultiplier, 0.5f));
                break;

            case "steadyhands":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFirearmSpreadMultiplier(FirearmClass.Pistol, 0.2f));
                break;

            case "quickhands":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.ReloadTimeMultiplier, 0.5f));
                break;

            case "clearmind":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFocusRegenerationOverride(5f, true));
                break;

            case "sixthsense":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateBool(HideoutPerkEffectType.RevealArmedAgentsDuringFocus, true));
                break;

            case "ghoststep":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.SprintNoiseMultiplier, 0.6f));
                break;

            case "submachinetraining":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFirearmSpreadMultiplier(FirearmClass.SMG, 0.4f));
                break;

            case "eagleeye":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.AccurateAimTimeMultiplier, 0.2f));
                break;

            case "relentlesspursuit":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.SprintStaminaDrainMultiplier, 0f));
                break;

            case "armoredmobility":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFloat(HideoutPerkEffectType.ArmorMovementPenaltyMultiplier, 0f));
                break;

            case "catwalk":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateWalkNoiseSpeedLevelMultipliers(new[]
                {
                    0f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 0.95f, 1f, 1f
                }));
                break;

            case "assaulttactics":
                defaults.Add(HideoutPerkRuntimeEffectDefinition.CreateFirearmSpreadMultiplier(FirearmClass.AssaultRifle, 0.6f));
                break;
        }

        return defaults;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = value.Trim().ToLowerInvariant().ToCharArray();
        List<char> normalized = new(buffer.Length);
        for (int i = 0; i < buffer.Length; i++)
        {
            if (char.IsLetterOrDigit(buffer[i]))
                normalized.Add(buffer[i]);
        }

        return new string(normalized.ToArray());
    }
}

}
