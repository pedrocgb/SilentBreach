using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public static class CombatImpactUtility
{
    public static bool TryApplyProjectileImpact(Collider2D hitCollider, ProjectileData projectileData)
    {
        if (hitCollider == null || projectileData == null)
            return false;

        return TryApplyDirectImpact(hitCollider, projectileData.Damage, projectileData.Penetration, projectileData.StaggerDuration);
    }

    public static bool TryApplyDirectImpact(Collider2D hitCollider, float damage, int penetration, float staggerDuration = 0f)
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
                health.ApplyDamage(impact.DamageToHealth);
                registeredImpact = true;
            }

            if (impact.HadArmor)
                registeredImpact = true;

            return registeredImpact;
        }

        if (health == null)
            return false;

        health.ApplyDamage(damage);
        if (staggerDuration > 0f)
            staggerController?.ApplyStagger(staggerDuration);

        return true;
    }

    public static bool TryApplyUnarmoredExplosionDamage(Collider2D hitCollider, float damage)
    {
        if (hitCollider == null || damage <= 0f)
            return false;

        ActorHealth health = hitCollider.GetComponentInParent<ActorHealth>();
        if (health == null)
            return false;

        health.ApplyDamage(damage);
        return true;
    }
}

}
