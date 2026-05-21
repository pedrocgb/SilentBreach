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

    [FoldoutGroup("References")]
    [SerializeField] private BoxCollider2D hitboxCollider;

    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public MeleeWeaponData EquippedWeapon => equippedWeapon;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDamageActive => isDamageActive;

    private readonly HashSet<ActorHealth> hitTargets = new();
    private readonly HashSet<int> hitColliderIds = new();
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
        hitColliderIds.Clear();
    }

    public void PlaySwingSfx()
    {
        if (equippedWeapon == null)
            return;

        ResolveWorldSfxManager();
        worldSfxManager?.PlayClipSetAt(transform.position, equippedWeapon.SwingSfx, equippedWeapon.AttackNoiseType);
    }

    public void SetDamageActive(bool active)
    {
        isDamageActive = active && equippedWeapon != null;
        hitTargets.Clear();
        hitColliderIds.Clear();

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

        int otherColliderId = other.GetInstanceID();
        if (hitColliderIds.Contains(otherColliderId))
            return;

        Transform otherRoot = other.transform.root;
        if (ownerRoot != null && otherRoot == ownerRoot.transform)
            return;

        ActorHealth health = other.GetComponentInParent<ActorHealth>();
        ArmorLoadout armorLoadout = other.GetComponentInParent<ArmorLoadout>();
        bool treatAsEnvironmentHit = !other.isTrigger && health == null && armorLoadout == null;
        if (health == null && armorLoadout == null && !treatAsEnvironmentHit)
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

        if (!registeredImpact && treatAsEnvironmentHit)
            registeredImpact = equippedWeapon.ResolveHitSfxForLayer(other.gameObject.layer) != null;

        if (!registeredImpact)
            return;

        hitColliderIds.Add(otherColliderId);

        if (health != null)
            hitTargets.Add(health);

        ActorStaggerController staggerController = other.GetComponentInParent<ActorStaggerController>();
        if (staggerController != null && equippedWeapon.StaggerDuration > 0f)
            staggerController.ApplyStagger(equippedWeapon.StaggerDuration);

        PlayHitSfx(other);
        ApplyPushForce(other);
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

    private void ApplyPushForce(Collider2D other)
    {
        if (equippedWeapon == null || !equippedWeapon.AppliesPushForce || equippedWeapon.PushForce <= 0f || other == null)
            return;

        Rigidbody2D targetBody = other.attachedRigidbody != null
            ? other.attachedRigidbody
            : other.GetComponentInParent<Rigidbody2D>();
        if (targetBody == null || !targetBody.simulated)
            return;

        Transform targetRoot = targetBody.transform.root;
        if (ownerRoot != null && targetRoot == ownerRoot.transform)
            return;

        Vector2 pushDirection = ResolvePushDirection(targetRoot);
        if (pushDirection.sqrMagnitude <= MinimumDirectionSqr)
            return;

        targetBody.AddForce(pushDirection * equippedWeapon.PushForce, ForceMode2D.Impulse);
    }

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

    private void ResolveWorldSfxManager()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;
    }

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
