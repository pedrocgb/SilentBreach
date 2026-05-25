using UnityEngine;
using Breezeblocks.Missions;

namespace Breezeblocks.WeaponSystem
{

public static class CombatImpactUtility
{
    public static bool TryApplyProjectileImpact(Collider2D hitCollider, ProjectileData projectileData)
    {
        if (hitCollider == null || projectileData == null)
            return false;

        return TryApplyProjectileImpact(hitCollider, projectileData, null);
    }

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

    public static bool TryApplyDirectImpact(Collider2D hitCollider, float damage, int penetration, float staggerDuration = 0f)
    {
        return TryApplyDirectImpact(hitCollider, damage, penetration, staggerDuration, new ActorDamageContext(null, isLethal: true));
    }

    public static bool TryApplyDirectImpact(Collider2D hitCollider, float damage, int penetration, float staggerDuration, ActorDamageContext damageContext)
    {
        if (hitCollider == null || damage <= 0f)
            return false;

        ArmorLoadout armor = hitCollider.GetComponentInParent<ArmorLoadout>();
        ActorHealth health = hitCollider.GetComponentInParent<ActorHealth>();
        ActorStaggerController staggerController = hitCollider.GetComponentInParent<ActorStaggerController>();

        bool registeredImpact = false;
        if (armor != null)
        {
            ArmorImpactResult impact = armor.ResolveDirectImpact(damage, penetration);
            if (!impact.Penetrated && impact.DamageToArmor > 0f && staggerDuration > 0f)
                staggerController?.ApplyStagger(staggerDuration);

            if (impact.DamageToHealth > 0f && health != null)
            {
                health.ApplyDamage(impact.DamageToHealth, damageContext);
                registeredImpact = true;
            }

            if (impact.HadArmor)
                registeredImpact = true;

            return registeredImpact;
        }

        if (health == null)
            return false;

        health.ApplyDamage(damage, damageContext);
        if (staggerDuration > 0f)
            staggerController?.ApplyStagger(staggerDuration);

        return true;
    }

    public static bool TryApplyUnarmoredExplosionDamage(Collider2D hitCollider, float damage)
    {
        return TryApplyUnarmoredExplosionDamage(hitCollider, damage, null);
    }

    public static bool TryApplyUnarmoredExplosionDamage(Collider2D hitCollider, float damage, GameObject instigatorRoot)
    {
        if (hitCollider == null || damage <= 0f)
            return false;

        ActorHealth health = hitCollider.GetComponentInParent<ActorHealth>();
        if (health == null)
            return false;

        health.ApplyDamage(damage, new ActorDamageContext(instigatorRoot, isLethal: true));
        return true;
    }

    public static bool TryApplyExplosionKnockback(Collider2D hitCollider, Vector2 explosionCenter, float force)
    {
        if (hitCollider == null || force <= 0f)
            return false;

        Rigidbody2D body = hitCollider.attachedRigidbody;
        if (body == null)
            body = hitCollider.GetComponentInParent<Rigidbody2D>();

        if (body == null)
            return false;

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
}

}
