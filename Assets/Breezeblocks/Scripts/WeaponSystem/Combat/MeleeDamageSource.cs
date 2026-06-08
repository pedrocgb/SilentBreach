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
    private const float MinimumDirectionSqr = 0.0001f;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private BoxCollider2D hitboxCollider;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public MeleeWeaponData EquippedWeapon => equippedWeapon;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDamageActive => isDamageActive;

    private readonly HashSet<ActorHealth> hitTargets = new();
    private readonly HashSet<int> hitColliderIds = new();
    private WorldSfxManager worldSfxManager;
    private GameObject ownerRoot;
    private MeleeWeaponData equippedWeapon;
    private bool isDamageActive;

    /// <summary>
    /// Ensures host object has melee damage source so held melee visuals can drive hit detection.
    /// </summary>
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

    /// <summary>
    /// Caches required collider reference when component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        if (hitboxCollider != null)
            hitboxCollider.isTrigger = true;
    }

    /// <summary>
    /// Initializes required collider reference and disables damage hitbox on startup.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        if (hitboxCollider != null)
            hitboxCollider.isTrigger = true;

        SetDamageActive(false);
    }

    /// <summary>
    /// Disables active damage state whenever melee source leaves play.
    /// </summary>
    private void OnDisable()
    {
        SetDamageActive(false);
    }

    /// <summary>
    /// Applies owner and weapon data to source before a swing sequence begins.
    /// </summary>
    public void Configure(GameObject owner, MeleeWeaponData weapon)
    {
        ownerRoot = owner != null ? owner.transform.root.gameObject : null;
        equippedWeapon = weapon;
        CacheReferences();
        RefreshHitboxShape();
        SetDamageActive(false);
    }

    /// <summary>
    /// Clears per-swing hit tracking so next attack can affect fresh targets.
    /// </summary>
    public void BeginSwing()
    {
        hitTargets.Clear();
        hitColliderIds.Clear();
    }

    /// <summary>
    /// Plays configured swing SFX from current held weapon position.
    /// </summary>
    public void PlaySwingSfx()
    {
        if (equippedWeapon == null)
            return;

        ResolveWorldSfxManager();
        worldSfxManager?.PlayClipSetAt(transform.position, equippedWeapon.SwingSfx, equippedWeapon.AttackNoiseType);
    }

    /// <summary>
    /// Enables or disables active damage window and clears previous hit bookkeeping.
    /// </summary>
    public void SetDamageActive(bool active)
    {
        isDamageActive = active && equippedWeapon != null;
        hitTargets.Clear();
        hitColliderIds.Clear();

        if (hitboxCollider != null)
            hitboxCollider.enabled = isDamageActive;
    }

    /// <summary>
    /// Processes first trigger contact during active melee damage window.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyHit(other);
    }

    /// <summary>
    /// Processes sustained trigger contact during active melee damage window.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyHit(other);
    }

    /// <summary>
    /// Applies melee impact once per collider while honoring armor, environment, stagger, and push rules.
    /// </summary>
    private void TryApplyHit(Collider2D other)
    {
        if (!isDamageActive || equippedWeapon == null || other == null)
            return;

        int otherColliderId = other.GetInstanceID();
        if (hitColliderIds.Contains(otherColliderId))
            return;

        if (!CombatImpactUtility.TryResolveImpactTarget(other, out CombatImpactUtility.ImpactTargetContext targetContext))
            return;

        if (ownerRoot != null && targetContext.RootTransform == ownerRoot.transform)
            return;

        bool treatAsEnvironmentHit = !other.isTrigger && !targetContext.HasHealth && !targetContext.HasArmor;
        if (!targetContext.HasHealth && !targetContext.HasArmor && !treatAsEnvironmentHit)
            return;

        if (targetContext.HasHealth && hitTargets.Contains(targetContext.ActorHealth))
            return;

        bool registeredImpact = TryApplyRegisteredImpact(other, targetContext, treatAsEnvironmentHit);
        if (!registeredImpact)
            return;

        hitColliderIds.Add(otherColliderId);
        if (targetContext.HasHealth)
            hitTargets.Add(targetContext.ActorHealth);

        ApplyStagger(targetContext);
        PlayHitSfx(other);
        ApplyPushForce(targetContext);
    }

    /// <summary>
    /// Resolves whether melee hit produced valid gameplay or environment impact.
    /// </summary>
    private bool TryApplyRegisteredImpact(
        Collider2D other,
        CombatImpactUtility.ImpactTargetContext targetContext,
        bool treatAsEnvironmentHit)
    {
        bool registeredImpact = false;

        if (targetContext.HasArmor)
        {
            ArmorImpactResult impact = targetContext.ArmorLoadout.ResolveDirectImpact(equippedWeapon.Damage, equippedWeapon.ArmorPenetration);
            if (impact.DamageToHealth > 0f && targetContext.HasHealth)
            {
                targetContext.ActorHealth.ApplyDamage(impact.DamageToHealth, new ActorDamageContext(ownerRoot, equippedWeapon.IsLethal));
                registeredImpact = true;
            }

            if (impact.HadArmor)
                registeredImpact = true;
        }
        else if (targetContext.HasHealth)
        {
            targetContext.ActorHealth.ApplyDamage(equippedWeapon.Damage, new ActorDamageContext(ownerRoot, equippedWeapon.IsLethal));
            registeredImpact = equippedWeapon.Damage > 0f;
        }

        if (!registeredImpact && treatAsEnvironmentHit)
            registeredImpact = equippedWeapon.ResolveHitSfxForLayer(other.gameObject.layer) != null;

        return registeredImpact;
    }

    /// <summary>
    /// Applies melee stagger to impacted actor when weapon configuration requires it.
    /// </summary>
    private void ApplyStagger(CombatImpactUtility.ImpactTargetContext targetContext)
    {
        if (equippedWeapon == null || equippedWeapon.StaggerDuration <= 0f)
            return;

        targetContext.ActorStaggerController?.ApplyStagger(equippedWeapon.StaggerDuration);
    }

    /// <summary>
    /// Caches same-object collider reference used for melee trigger shape.
    /// </summary>
    private void CacheReferences()
    {
        if (hitboxCollider == null)
            hitboxCollider = GetComponent<BoxCollider2D>();
    }

    /// <summary>
    /// Applies weapon-defined hitbox shape to cached collider.
    /// </summary>
    private void RefreshHitboxShape()
    {
        if (hitboxCollider == null)
            return;

        hitboxCollider.isTrigger = true;
        hitboxCollider.offset = equippedWeapon != null ? equippedWeapon.HitboxOffset : Vector2.zero;
        hitboxCollider.size = equippedWeapon != null ? equippedWeapon.HitboxSize : new Vector2(0.1f, 0.1f);
        hitboxCollider.enabled = false;
    }

    /// <summary>
    /// Applies configured push force to impacted rigidbody when melee weapon supports knockback.
    /// </summary>
    private void ApplyPushForce(CombatImpactUtility.ImpactTargetContext targetContext)
    {
        if (equippedWeapon == null || !equippedWeapon.AppliesPushForce || equippedWeapon.PushForce <= 0f)
            return;

        Rigidbody2D targetBody = targetContext.Rigidbody2D;
        if (targetBody == null || !targetBody.simulated)
            return;

        Transform targetRoot = targetContext.RootTransform;
        if (ownerRoot != null && targetRoot == ownerRoot.transform)
            return;

        Vector2 pushDirection = ResolvePushDirection(targetRoot);
        if (pushDirection.sqrMagnitude <= MinimumDirectionSqr)
            return;

        targetBody.AddForce(pushDirection * equippedWeapon.PushForce, ForceMode2D.Impulse);
    }

    /// <summary>
    /// Plays impact SFX selected from target layer at closest contact point.
    /// </summary>
    private void PlayHitSfx(Collider2D other)
    {
        if (equippedWeapon == null || other == null)
            return;

        AudioClipSet hitSfx = equippedWeapon.ResolveHitSfxForLayer(other.gameObject.layer);
        if (hitSfx == null || !hitSfx.HasAnyClip)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        Vector2 impactPoint = other.ClosestPoint(transform.position);
        if (impactPoint == Vector2.zero)
            impactPoint = transform.position;

        worldSfxManager.PlayClipSetAt(impactPoint, hitSfx, equippedWeapon.AttackNoiseType);
    }

    /// <summary>
    /// Resolves shared world SFX manager only when audio playback is needed.
    /// </summary>
    private void ResolveWorldSfxManager()
    {
        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);
    }

    /// <summary>
    /// Resolves knockback direction from attacker toward target, falling back to weapon or owner facing.
    /// </summary>
    private Vector2 ResolvePushDirection(Transform targetRoot)
    {
        Vector2 ownerPosition = ownerRoot != null ? ownerRoot.transform.position : transform.position;
        Vector2 targetPosition = targetRoot != null ? targetRoot.position : transform.position;
        Vector2 directionFromAttacker = targetPosition - ownerPosition;
        if (directionFromAttacker.sqrMagnitude > MinimumDirectionSqr)
            return directionFromAttacker.normalized;

        Vector2 weaponFacing = transform.up;
        if (weaponFacing.sqrMagnitude > MinimumDirectionSqr)
            return weaponFacing.normalized;

        if (ownerRoot != null)
        {
            Vector2 ownerFacing = ownerRoot.transform.up;
            if (ownerFacing.sqrMagnitude > MinimumDirectionSqr)
                return ownerFacing.normalized;
        }

        return Vector2.up;
    }
}

}
