using Breezeblocks.Missions;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

/// <summary>
/// Provides shared impact resolution helpers for projectile, melee, and explosion damage flows.
/// </summary>
public static class CombatImpactUtility
{
    /// <summary>
    /// Caches combat-related component lookups for one impacted collider hierarchy.
    /// </summary>
    public readonly struct ImpactTargetContext
    {
        /// <summary>
        /// Initializes resolved impact context for one collider hierarchy.
        /// </summary>
        public ImpactTargetContext(
            Transform rootTransform,
            ArmorLoadout armorLoadout,
            ActorHealth actorHealth,
            ActorStaggerController actorStaggerController,
            Rigidbody2D rigidbody2D)
        {
            RootTransform = rootTransform;
            ArmorLoadout = armorLoadout;
            ActorHealth = actorHealth;
            ActorStaggerController = actorStaggerController;
            Rigidbody2D = rigidbody2D;
        }

        /// <summary>
        /// Gets resolved root transform for impacted collider hierarchy.
        /// </summary>
        public Transform RootTransform { get; }

        /// <summary>
        /// Gets resolved armor loadout on impacted hierarchy when present.
        /// </summary>
        public ArmorLoadout ArmorLoadout { get; }

        /// <summary>
        /// Gets resolved health component on impacted hierarchy when present.
        /// </summary>
        public ActorHealth ActorHealth { get; }

        /// <summary>
        /// Gets resolved stagger controller on impacted hierarchy when present.
        /// </summary>
        public ActorStaggerController ActorStaggerController { get; }

        /// <summary>
        /// Gets resolved rigidbody on impacted hierarchy when present.
        /// </summary>
        public Rigidbody2D Rigidbody2D { get; }

        /// <summary>
        /// Gets whether impacted hierarchy has armor.
        /// </summary>
        public bool HasArmor => ArmorLoadout != null;

        /// <summary>
        /// Gets whether impacted hierarchy has health.
        /// </summary>
        public bool HasHealth => ActorHealth != null;
    }

    /// <summary>
    /// Applies projectile impact using projectile data with no explicit instigator root.
    /// </summary>
    public static bool TryApplyProjectileImpact(Collider2D hitCollider, ProjectileData projectileData)
    {
        return TryApplyProjectileImpact(hitCollider, projectileData, null);
    }

    /// <summary>
    /// Applies projectile impact using projectile data and explicit instigator root.
    /// </summary>
    public static bool TryApplyProjectileImpact(Collider2D hitCollider, ProjectileData projectileData, GameObject instigatorRoot)
    {
        if (hitCollider == null || projectileData == null)
            return false;

        return TryApplyDirectImpact(
            hitCollider,
            projectileData.Damage,
            projectileData.Penetration,
            projectileData.StaggerDuration,
            new ActorDamageContext(instigatorRoot, projectileData.IsLethal));
    }

    /// <summary>
    /// Applies direct impact with default lethal damage context.
    /// </summary>
    public static bool TryApplyDirectImpact(Collider2D hitCollider, float damage, int penetration, float staggerDuration = 0f)
    {
        return TryApplyDirectImpact(hitCollider, damage, penetration, staggerDuration, new ActorDamageContext(null, isLethal: true));
    }

    /// <summary>
    /// Applies direct impact to resolved armor or health components on impacted hierarchy.
    /// </summary>
    public static bool TryApplyDirectImpact(Collider2D hitCollider, float damage, int penetration, float staggerDuration, ActorDamageContext damageContext)
    {
        if (hitCollider == null || damage <= 0f)
            return false;

        if (!TryResolveImpactTarget(hitCollider, out ImpactTargetContext targetContext))
            return false;

        if (targetContext.HasArmor)
            return TryApplyArmoredImpact(targetContext, damage, penetration, staggerDuration, damageContext);

        if (!targetContext.HasHealth)
            return false;

        targetContext.ActorHealth.ApplyDamage(damage, damageContext);
        if (staggerDuration > 0f)
            targetContext.ActorStaggerController?.ApplyStagger(staggerDuration);

        return true;
    }

    /// <summary>
    /// Applies explosion damage only to health component when armor should be ignored.
    /// </summary>
    public static bool TryApplyUnarmoredExplosionDamage(Collider2D hitCollider, float damage)
    {
        return TryApplyUnarmoredExplosionDamage(hitCollider, damage, new ActorDamageContext(null, isLethal: true));
    }

    /// <summary>
    /// Applies unarmored explosion damage with optional instigator root.
    /// </summary>
    public static bool TryApplyUnarmoredExplosionDamage(Collider2D hitCollider, float damage, GameObject instigatorRoot)
    {
        return TryApplyUnarmoredExplosionDamage(hitCollider, damage, new ActorDamageContext(instigatorRoot, isLethal: true));
    }

    /// <summary>
    /// Applies unarmored explosion damage using explicit instigator and lethality context.
    /// </summary>
    public static bool TryApplyUnarmoredExplosionDamage(Collider2D hitCollider, float damage, ActorDamageContext damageContext)
    {
        if (hitCollider == null || damage <= 0f)
            return false;

        if (!TryResolveImpactTarget(hitCollider, out ImpactTargetContext targetContext) || !targetContext.HasHealth)
            return false;

        targetContext.ActorHealth.ApplyDamage(damage, damageContext);
        return true;
    }

    /// <summary>
    /// Applies explosion knockback to resolved rigidbody on impacted hierarchy.
    /// </summary>
    public static bool TryApplyExplosionKnockback(Collider2D hitCollider, Vector2 explosionCenter, float force)
    {
        if (hitCollider == null || force <= 0f)
            return false;

        if (!TryResolveImpactTarget(hitCollider, out ImpactTargetContext targetContext) || targetContext.Rigidbody2D == null)
            return false;

        Rigidbody2D body = targetContext.Rigidbody2D;
        Vector2 origin = body.worldCenterOfMass;
        Vector2 direction = origin - explosionCenter;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            direction = (Vector2)hitCollider.bounds.center - explosionCenter;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
                return false;
        }

        body.AddForce(direction.normalized * force, ForceMode2D.Impulse);
        return true;
    }

    /// <summary>
    /// Resolves combat-related components from impacted collider hierarchy once.
    /// </summary>
    public static bool TryResolveImpactTarget(Collider2D hitCollider, out ImpactTargetContext targetContext)
    {
        targetContext = default;
        if (hitCollider == null)
            return false;

        Transform rootTransform = hitCollider.transform.root;
        ArmorLoadout armorLoadout = hitCollider.GetComponentInParent<ArmorLoadout>();
        ActorHealth actorHealth = hitCollider.GetComponentInParent<ActorHealth>();
        ActorStaggerController actorStaggerController = hitCollider.GetComponentInParent<ActorStaggerController>();
        Rigidbody2D rigidbody2D = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody
            : hitCollider.GetComponentInParent<Rigidbody2D>();

        targetContext = new ImpactTargetContext(rootTransform, armorLoadout, actorHealth, actorStaggerController, rigidbody2D);
        return true;
    }

    /// <summary>
    /// Applies direct impact against armor-bearing targets and forwards penetrated health damage when needed.
    /// </summary>
    private static bool TryApplyArmoredImpact(ImpactTargetContext targetContext, float damage, int penetration, float staggerDuration, ActorDamageContext damageContext)
    {
        ArmorImpactResult impact = targetContext.ArmorLoadout.ResolveDirectImpact(damage, penetration);
        if (!impact.Penetrated && impact.DamageToArmor > 0f && staggerDuration > 0f)
            targetContext.ActorStaggerController?.ApplyStagger(staggerDuration);

        bool registeredImpact = false;
        if (impact.DamageToHealth > 0f && targetContext.HasHealth)
        {
            targetContext.ActorHealth.ApplyDamage(impact.DamageToHealth, damageContext);
            registeredImpact = true;
        }

        if (impact.HadArmor)
            registeredImpact = true;

        return registeredImpact;
    }
}

}
