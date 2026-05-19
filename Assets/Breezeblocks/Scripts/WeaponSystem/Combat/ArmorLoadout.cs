using Sirenix.OdinInspector;
using UnityEngine;
using System;

namespace Breezeblocks.WeaponSystem
{

public readonly struct ArmorImpactResult
{
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
    [FoldoutGroup("Armor"), AssetsOnly]
    [SerializeField] private ArmorData startingArmor;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public ArmorData EquippedArmor => startingArmor;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentArmorValue { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float MaxArmorValue => startingArmor != null ? startingArmor.ArmorValue : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasArmor => startingArmor != null && CurrentArmorValue > 0f;

    public float RotationPenaltyPercent => HasArmor ? startingArmor.RotationPenalty : 0f;

    public event Action ArmorChanged;

    private void Awake()
    {
        RestoreArmor();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RestoreArmor()
    {
        CurrentArmorValue = startingArmor != null ? startingArmor.ArmorValue : 0f;
        NotifyArmorChanged();
    }

    public void EquipArmor(ArmorData armorData)
    {
        startingArmor = armorData;
        RestoreArmor();
    }

    public ArmorImpactResult ResolveProjectileImpact(ProjectileData projectile)
    {
        if (projectile == null)
            return new ArmorImpactResult(false, false, 0f, 0f);

        return ResolveImpact(projectile.Damage, projectile.Penetration);
    }

    public ArmorImpactResult ResolveDirectImpact(float damage, int penetration)
    {
        if (damage <= 0f)
            return new ArmorImpactResult(false, false, 0f, 0f);

        return ResolveImpact(damage, penetration);
    }

    private ArmorImpactResult ResolveImpact(float damage, int penetration)
    {
        float clampedDamage = Mathf.Max(0f, damage);
        if (!HasArmor || startingArmor == null)
            return new ArmorImpactResult(false, true, 0f, clampedDamage);

        if (penetration > startingArmor.ArmorClass)
            return new ArmorImpactResult(true, true, 0f, clampedDamage);

        float armorDamage = clampedDamage;
        if (penetration < startingArmor.ArmorClass)
            armorDamage = startingArmor.ArmorClass > 0 ? clampedDamage / startingArmor.ArmorClass : clampedDamage;

        CurrentArmorValue = Mathf.Max(0f, CurrentArmorValue - armorDamage);
        NotifyArmorChanged();
        return new ArmorImpactResult(true, false, armorDamage, 0f);
    }

    private void NotifyArmorChanged()
    {
        ArmorChanged?.Invoke();
    }
}
}
