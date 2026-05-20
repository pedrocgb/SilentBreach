using System.Collections.Generic;
using Breezeblocks.Missions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Breezeblocks/Combat/Melee Damage Source")]
public class MeleeDamageSource : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private BoxCollider2D hitboxCollider;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public MeleeWeaponData EquippedWeapon => equippedWeapon;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDamageActive => isDamageActive;

    private readonly HashSet<ActorHealth> hitTargets = new();
    private GameObject ownerRoot;
    private MeleeWeaponData equippedWeapon;
    private bool isDamageActive;

    public static MeleeDamageSource EnsureOn(GameObject host)
    {
        if (host == null)
            return null;

        MeleeDamageSource damageSource = host.GetComponent<MeleeDamageSource>();
        if (damageSource == null)
            damageSource = host.AddComponent<MeleeDamageSource>();

        damageSource.CacheReferences();
        if (damageSource.hitboxCollider != null)
            damageSource.hitboxCollider.isTrigger = true;

        return damageSource;
    }

    private void Reset()
    {
        CacheReferences();
        if (hitboxCollider != null)
            hitboxCollider.isTrigger = true;
    }

    private void Awake()
    {
        CacheReferences();
        if (hitboxCollider != null)
            hitboxCollider.isTrigger = true;

        SetDamageActive(false);
    }

    private void OnDisable()
    {
        SetDamageActive(false);
    }

    public void Configure(GameObject owner, MeleeWeaponData weapon)
    {
        ownerRoot = owner != null ? owner.transform.root.gameObject : null;
        equippedWeapon = weapon;
        CacheReferences();
        RefreshHitboxShape();
        SetDamageActive(false);
    }

    public void BeginSwing()
    {
        hitTargets.Clear();
    }

    public void SetDamageActive(bool active)
    {
        isDamageActive = active && equippedWeapon != null;
        hitTargets.Clear();

        if (hitboxCollider != null)
            hitboxCollider.enabled = isDamageActive;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyHit(other);
    }

    private void TryApplyHit(Collider2D other)
    {
        if (!isDamageActive || equippedWeapon == null || other == null)
            return;

        Transform otherRoot = other.transform.root;
        if (ownerRoot != null && otherRoot == ownerRoot.transform)
            return;

        ActorHealth health = other.GetComponentInParent<ActorHealth>();
        ArmorLoadout armorLoadout = other.GetComponentInParent<ArmorLoadout>();
        if (health == null && armorLoadout == null)
            return;

        if (health != null && hitTargets.Contains(health))
            return;

        bool registeredImpact = false;

        if (armorLoadout != null)
        {
            ArmorImpactResult impact = armorLoadout.ResolveDirectImpact(equippedWeapon.Damage, equippedWeapon.ArmorPenetration);
            if (impact.DamageToHealth > 0f && health != null)
            {
                health.ApplyDamage(impact.DamageToHealth, new ActorDamageContext(ownerRoot, equippedWeapon.IsLethal));
                registeredImpact = true;
            }

            if (impact.HadArmor)
                registeredImpact = true;
        }
        else if (health != null)
        {
            health.ApplyDamage(equippedWeapon.Damage, new ActorDamageContext(ownerRoot, equippedWeapon.IsLethal));
            registeredImpact = equippedWeapon.Damage > 0f;
        }

        if (!registeredImpact)
            return;

        if (health != null)
            hitTargets.Add(health);

        ActorStaggerController staggerController = other.GetComponentInParent<ActorStaggerController>();
        if (staggerController != null && equippedWeapon.StaggerDuration > 0f)
            staggerController.ApplyStagger(equippedWeapon.StaggerDuration);
    }

    private void CacheReferences()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<BoxCollider2D>();
    }

    private void RefreshHitboxShape()
    {
        if (hitboxCollider == null)
            return;

        hitboxCollider.isTrigger = true;
        hitboxCollider.offset = equippedWeapon != null ? equippedWeapon.HitboxOffset : Vector2.zero;
        hitboxCollider.size = equippedWeapon != null ? equippedWeapon.HitboxSize : new Vector2(0.1f, 0.1f);
        hitboxCollider.enabled = false;
    }
}
}
