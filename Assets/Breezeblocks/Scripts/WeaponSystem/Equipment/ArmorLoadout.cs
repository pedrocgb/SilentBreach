using Sirenix.OdinInspector;
using Breezeblocks.HideoutSystem;
using UnityEngine;
using System;

namespace Breezeblocks.WeaponSystem
{

public readonly struct ArmorImpactResult
{
    /// <summary>
    /// Creates the result of applying incoming damage against the current armor state.
    /// </summary>
    public ArmorImpactResult(bool hadArmor, bool penetrated, float damageToArmor, float damageToHealth)
    {
        HadArmor = hadArmor;
        Penetrated = penetrated;
        DamageToArmor = damageToArmor;
        DamageToHealth = damageToHealth;
    }

    public bool HadArmor { get; }
    public bool Penetrated { get; }
    public float DamageToArmor { get; }
    public float DamageToHealth { get; }
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Combat/Armor Loadout")]
public class ArmorLoadout : MonoBehaviour
{
    private ArmorData equippedArmor;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public ArmorData EquippedArmor => equippedArmor;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentArmorValue { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float MaxArmorValue => equippedArmor != null ? equippedArmor.ArmorValue : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasArmor => equippedArmor != null && CurrentArmorValue > 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasEquippedArmor => equippedArmor != null;

    public float RotationPenaltyPercent => HasEquippedArmor
        ? equippedArmor.RotationPenalty * perkRotationPenaltyMultiplier
        : 0f;
    public float RotationSpeedMultiplier => 1f - Mathf.Clamp01(RotationPenaltyPercent / 100f);
    public float MovementNoiseMultiplier => HasEquippedArmor
        ? 1f + ((equippedArmor.MovementNoiseModifierPercent * perkMovementPenaltyMultiplier) / 100f)
        : 1f;
    public float MovementSpeedPenaltyPercent => HasEquippedArmor
        ? equippedArmor.MovementSpeedPenaltyPercent * perkMovementPenaltyMultiplier
        : 0f;
    public float MovementSpeedMultiplier => 1f - Mathf.Clamp01(MovementSpeedPenaltyPercent / 100f);

    public event Action ArmorChanged;

    private float perkRotationPenaltyMultiplier = 1f;
    private float perkMovementPenaltyMultiplier = 1f;

    /// <summary>
    /// Initializes armor durability from the current runtime armor item.
    /// </summary>
    private void Awake()
    {
        RestoreArmor();
    }

    /// <summary>
    /// Restores durability to the currently equipped armor's maximum value.
    /// </summary>
    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RestoreArmor()
    {
        CurrentArmorValue = equippedArmor != null ? equippedArmor.ArmorValue : 0f;
        NotifyArmorChanged();
    }

    /// <summary>
    /// Equips a runtime armor item and restores its durability.
    /// </summary>
    public void EquipArmor(ArmorData armorData)
    {
        equippedArmor = armorData;
        RestoreArmor();
    }

    /// <summary>
    /// Applies perk modifiers that reduce armor movement and rotation penalties.
    /// </summary>
    public void ApplyPerkModifiers(PlayerPerkModifierSet modifiers)
    {
        perkRotationPenaltyMultiplier = modifiers != null ? Mathf.Max(0f, modifiers.ArmorRotationPenaltyMultiplier) : 1f;
        perkMovementPenaltyMultiplier = modifiers != null ? Mathf.Max(0f, modifiers.ArmorMovementPenaltyMultiplier) : 1f;
        NotifyArmorChanged();
    }

    /// <summary>
    /// Resolves a projectile hit against the currently equipped armor.
    /// </summary>
    public ArmorImpactResult ResolveProjectileImpact(ProjectileData projectile)
    {
        if (projectile == null)
            return new ArmorImpactResult(false, false, 0f, 0f);

        return ResolveImpact(projectile.Damage, projectile.Penetration);
    }

    /// <summary>
    /// Resolves direct damage against the currently equipped armor.
    /// </summary>
    public ArmorImpactResult ResolveDirectImpact(float damage, int penetration)
    {
        if (damage <= 0f)
            return new ArmorImpactResult(false, false, 0f, 0f);

        return ResolveImpact(damage, penetration);
    }

    /// <summary>
    /// Applies damage to armor or health based on penetration and current durability.
    /// </summary>
    private ArmorImpactResult ResolveImpact(float damage, int penetration)
    {
        float clampedDamage = Mathf.Max(0f, damage);
        if (!HasArmor || equippedArmor == null)
            return new ArmorImpactResult(false, true, 0f, clampedDamage);

        if (penetration > equippedArmor.ArmorClass)
            return new ArmorImpactResult(true, true, 0f, clampedDamage);

        float armorDamage = clampedDamage;
        if (penetration < equippedArmor.ArmorClass)
            armorDamage = equippedArmor.ArmorClass > 0 ? clampedDamage / equippedArmor.ArmorClass : clampedDamage;

        CurrentArmorValue = Mathf.Max(0f, CurrentArmorValue - armorDamage);
        NotifyArmorChanged();
        return new ArmorImpactResult(true, false, armorDamage, 0f);
    }

    /// <summary>
    /// Notifies listeners that equipped armor or durability changed.
    /// </summary>
    private void NotifyArmorChanged()
    {
        ArmorChanged?.Invoke();
    }
}
}
