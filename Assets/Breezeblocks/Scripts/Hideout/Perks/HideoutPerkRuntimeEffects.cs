using System;
using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

public enum HideoutPerkEffectType
{
    MaxStaminaFlat,
    MaxFocusFlat,
    ArmorRotationPenaltyMultiplier,
    ArmorMovementPenaltyMultiplier,
    MeleeStaminaCostMultiplier,
    FirearmClassSpreadMultiplier,
    ReloadTimeMultiplier,
    AccurateAimTimeMultiplier,
    SprintNoiseMultiplier,
    SprintStaminaDrainMultiplier,
    FocusRegenerationOverride,
    RevealArmedAgentsDuringFocus,
    WalkNoiseSpeedLevelMultipliers
}

[Serializable]
public sealed class HideoutPerkRuntimeEffectDefinition
{
    private const int WalkSpeedLevelsCount = 10;

    [FoldoutGroup("Effect")]
    [SerializeField] private HideoutPerkEffectType effectType;

    [FoldoutGroup("Effect"), ShowIf(nameof(UsesFloatValue))]
    [SerializeField] private float floatValue = 1f;

    [FoldoutGroup("Effect"), ShowIf(nameof(UsesBoolValue))]
    [SerializeField] private bool boolValue = true;

    [FoldoutGroup("Effect"), ShowIf(nameof(UsesFirearmClass))]
    [SerializeField] private FirearmClass firearmClass = FirearmClass.Pistol;

    [FoldoutGroup("Effect"), ShowIf(nameof(UsesSpeedLevelValues))]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, NumberOfItemsPerPage = WalkSpeedLevelsCount)]
    [SerializeField] private float[] speedLevelValues = DefaultWalkNoiseMultipliers();

    public HideoutPerkEffectType EffectType => effectType;
    public float FloatValue => floatValue;
    public bool BoolValue => boolValue;
    public FirearmClass FirearmClass => firearmClass;
    public IReadOnlyList<float> SpeedLevelValues => speedLevelValues;

    private bool UsesFloatValue =>
        effectType != HideoutPerkEffectType.RevealArmedAgentsDuringFocus &&
        effectType != HideoutPerkEffectType.WalkNoiseSpeedLevelMultipliers;

    private bool UsesBoolValue =>
        effectType == HideoutPerkEffectType.FocusRegenerationOverride ||
        effectType == HideoutPerkEffectType.RevealArmedAgentsDuringFocus;

    private bool UsesFirearmClass => effectType == HideoutPerkEffectType.FirearmClassSpreadMultiplier;
    private bool UsesSpeedLevelValues => effectType == HideoutPerkEffectType.WalkNoiseSpeedLevelMultipliers;

    public void OnValidate()
    {
        speedLevelValues = EnsureArraySize(speedLevelValues, WalkSpeedLevelsCount, 1f);
        for (int i = 0; i < speedLevelValues.Length; i++)
            speedLevelValues[i] = Mathf.Max(0f, speedLevelValues[i]);
    }

    public void ApplyTo(PlayerPerkModifierSet modifiers)
    {
        if (modifiers == null)
            return;

        switch (effectType)
        {
            case HideoutPerkEffectType.MaxStaminaFlat:
                modifiers.MaxStaminaFlatBonus += Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.MaxFocusFlat:
                modifiers.MaxFocusFlatBonus += Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.ArmorRotationPenaltyMultiplier:
                modifiers.ArmorRotationPenaltyMultiplier *= Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.ArmorMovementPenaltyMultiplier:
                modifiers.ArmorMovementPenaltyMultiplier *= Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.MeleeStaminaCostMultiplier:
                modifiers.MeleeStaminaCostMultiplier *= Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.FirearmClassSpreadMultiplier:
                modifiers.SetFirearmSpreadMultiplier(
                    firearmClass,
                    modifiers.GetFirearmSpreadMultiplier(firearmClass) * Mathf.Max(0f, floatValue));
                break;

            case HideoutPerkEffectType.ReloadTimeMultiplier:
                modifiers.ReloadTimeMultiplier *= Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.AccurateAimTimeMultiplier:
                modifiers.AccurateAimTimeMultiplier *= Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.SprintNoiseMultiplier:
                modifiers.SprintNoiseMultiplier *= Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.SprintStaminaDrainMultiplier:
                modifiers.SprintStaminaDrainMultiplier *= Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.FocusRegenerationOverride:
                modifiers.HasFocusRegenerationOverride = true;
                modifiers.FocusRegenerationEnabled = boolValue;
                modifiers.FocusRegenerationPerSecond = Mathf.Max(0f, floatValue);
                break;

            case HideoutPerkEffectType.RevealArmedAgentsDuringFocus:
                modifiers.RevealArmedAgentsDuringFocus |= boolValue;
                break;

            case HideoutPerkEffectType.WalkNoiseSpeedLevelMultipliers:
                modifiers.ApplyWalkNoiseSpeedLevelMultipliers(speedLevelValues);
                break;
        }
    }

    internal static HideoutPerkRuntimeEffectDefinition CreateFloat(HideoutPerkEffectType effectType, float value)
    {
        return new HideoutPerkRuntimeEffectDefinition
        {
            effectType = effectType,
            floatValue = value
        };
    }

    internal static HideoutPerkRuntimeEffectDefinition CreateFirearmSpreadMultiplier(FirearmClass firearmClassValue, float value)
    {
        return new HideoutPerkRuntimeEffectDefinition
        {
            effectType = HideoutPerkEffectType.FirearmClassSpreadMultiplier,
            firearmClass = firearmClassValue,
            floatValue = value
        };
    }

    internal static HideoutPerkRuntimeEffectDefinition CreateFocusRegenerationOverride(float regenerationPerSecond, bool enabled)
    {
        return new HideoutPerkRuntimeEffectDefinition
        {
            effectType = HideoutPerkEffectType.FocusRegenerationOverride,
            floatValue = regenerationPerSecond,
            boolValue = enabled
        };
    }

    internal static HideoutPerkRuntimeEffectDefinition CreateBool(HideoutPerkEffectType effectType, bool value)
    {
        return new HideoutPerkRuntimeEffectDefinition
        {
            effectType = effectType,
            boolValue = value
        };
    }

    internal static HideoutPerkRuntimeEffectDefinition CreateWalkNoiseSpeedLevelMultipliers(float[] values)
    {
        return new HideoutPerkRuntimeEffectDefinition
        {
            effectType = HideoutPerkEffectType.WalkNoiseSpeedLevelMultipliers,
            speedLevelValues = EnsureArraySize(values, WalkSpeedLevelsCount, 1f)
        };
    }

    private static float[] EnsureArraySize(float[] source, int size, float defaultValue)
    {
        if (source != null && source.Length == size)
            return source;

        float[] resized = new float[size];
        for (int i = 0; i < size; i++)
            resized[i] = source != null && i < source.Length ? source[i] : defaultValue;

        return resized;
    }

    private static float[] DefaultWalkNoiseMultipliers()
    {
        float[] values = new float[WalkSpeedLevelsCount];
        for (int i = 0; i < values.Length; i++)
            values[i] = 1f;

        return values;
    }
}

public sealed class PlayerPerkModifierSet
{
    private const int WalkSpeedLevelsCount = 10;
    private static readonly float[] DefaultWalkNoiseMultipliers = BuildDefaultWalkNoiseMultipliers();

    private readonly Dictionary<FirearmClass, float> firearmSpreadMultipliers = new();
    private readonly float[] walkNoiseSpeedLevelMultipliers = new float[WalkSpeedLevelsCount];

    public float MaxStaminaFlatBonus { get; set; }
    public float MaxFocusFlatBonus { get; set; }
    public float ArmorRotationPenaltyMultiplier { get; set; } = 1f;
    public float ArmorMovementPenaltyMultiplier { get; set; } = 1f;
    public float MeleeStaminaCostMultiplier { get; set; } = 1f;
    public float ReloadTimeMultiplier { get; set; } = 1f;
    public float AccurateAimTimeMultiplier { get; set; } = 1f;
    public float SprintNoiseMultiplier { get; set; } = 1f;
    public float SprintStaminaDrainMultiplier { get; set; } = 1f;
    public bool HasFocusRegenerationOverride { get; set; }
    public bool FocusRegenerationEnabled { get; set; }
    public float FocusRegenerationPerSecond { get; set; }
    public bool RevealArmedAgentsDuringFocus { get; set; }

    public PlayerPerkModifierSet()
    {
        Reset();
    }

    public static PlayerPerkModifierSet BuildFrom(IEnumerable<HideoutPerkDefinition> equippedPerks)
    {
        PlayerPerkModifierSet modifiers = new();
        if (equippedPerks == null)
            return modifiers;

        foreach (HideoutPerkDefinition perkDefinition in equippedPerks)
        {
            if (perkDefinition == null)
                continue;

            IReadOnlyList<HideoutPerkRuntimeEffectDefinition> runtimeEffects = perkDefinition.RuntimeEffects;
            for (int i = 0; i < runtimeEffects.Count; i++)
                runtimeEffects[i]?.ApplyTo(modifiers);
        }

        return modifiers;
    }

    public void Reset()
    {
        MaxStaminaFlatBonus = 0f;
        MaxFocusFlatBonus = 0f;
        ArmorRotationPenaltyMultiplier = 1f;
        ArmorMovementPenaltyMultiplier = 1f;
        MeleeStaminaCostMultiplier = 1f;
        ReloadTimeMultiplier = 1f;
        AccurateAimTimeMultiplier = 1f;
        SprintNoiseMultiplier = 1f;
        SprintStaminaDrainMultiplier = 1f;
        HasFocusRegenerationOverride = false;
        FocusRegenerationEnabled = false;
        FocusRegenerationPerSecond = 0f;
        RevealArmedAgentsDuringFocus = false;
        firearmSpreadMultipliers.Clear();

        for (int i = 0; i < walkNoiseSpeedLevelMultipliers.Length; i++)
            walkNoiseSpeedLevelMultipliers[i] = DefaultWalkNoiseMultipliers[i];
    }

    public void SetFirearmSpreadMultiplier(FirearmClass firearmClass, float multiplier)
    {
        firearmSpreadMultipliers[firearmClass] = Mathf.Max(0f, multiplier);
    }

    public float GetFirearmSpreadMultiplier(FirearmClass firearmClass)
    {
        return firearmSpreadMultipliers.TryGetValue(firearmClass, out float multiplier)
            ? Mathf.Max(0f, multiplier)
            : 1f;
    }

    public void ApplyWalkNoiseSpeedLevelMultipliers(IReadOnlyList<float> multipliers)
    {
        if (multipliers == null)
            return;

        for (int i = 0; i < walkNoiseSpeedLevelMultipliers.Length; i++)
        {
            float multiplier = i < multipliers.Count ? multipliers[i] : 1f;
            walkNoiseSpeedLevelMultipliers[i] *= Mathf.Max(0f, multiplier);
        }
    }

    public float GetWalkNoiseMultiplierForSpeedLevel(int speedLevel)
    {
        int clampedIndex = Mathf.Clamp(speedLevel - 1, 0, walkNoiseSpeedLevelMultipliers.Length - 1);
        return Mathf.Max(0f, walkNoiseSpeedLevelMultipliers[clampedIndex]);
    }

    public PlayerPerkModifierSet Clone()
    {
        PlayerPerkModifierSet clone = new()
        {
            MaxStaminaFlatBonus = MaxStaminaFlatBonus,
            MaxFocusFlatBonus = MaxFocusFlatBonus,
            ArmorRotationPenaltyMultiplier = ArmorRotationPenaltyMultiplier,
            ArmorMovementPenaltyMultiplier = ArmorMovementPenaltyMultiplier,
            MeleeStaminaCostMultiplier = MeleeStaminaCostMultiplier,
            ReloadTimeMultiplier = ReloadTimeMultiplier,
            AccurateAimTimeMultiplier = AccurateAimTimeMultiplier,
            SprintNoiseMultiplier = SprintNoiseMultiplier,
            SprintStaminaDrainMultiplier = SprintStaminaDrainMultiplier,
            HasFocusRegenerationOverride = HasFocusRegenerationOverride,
            FocusRegenerationEnabled = FocusRegenerationEnabled,
            FocusRegenerationPerSecond = FocusRegenerationPerSecond,
            RevealArmedAgentsDuringFocus = RevealArmedAgentsDuringFocus
        };

        foreach (KeyValuePair<FirearmClass, float> pair in firearmSpreadMultipliers)
            clone.firearmSpreadMultipliers[pair.Key] = pair.Value;

        for (int i = 0; i < walkNoiseSpeedLevelMultipliers.Length; i++)
            clone.walkNoiseSpeedLevelMultipliers[i] = walkNoiseSpeedLevelMultipliers[i];

        return clone;
    }

    private static float[] BuildDefaultWalkNoiseMultipliers()
    {
        float[] values = new float[WalkSpeedLevelsCount];
        for (int i = 0; i < values.Length; i++)
            values[i] = 1f;

        return values;
    }
}

}
