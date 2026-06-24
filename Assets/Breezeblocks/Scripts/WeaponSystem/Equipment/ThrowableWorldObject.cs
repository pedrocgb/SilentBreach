using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[AddComponentMenu("Breezeblocks/Equipment/Throwable World Object")]
public class ThrowableWorldObject : MonoBehaviour
{
    private const float MinimumLaunchDirectionSqr = 0.0001f;
    private const float StopVelocityThreshold = 0.1f;

    [FoldoutGroup("References")]
    [SerializeField] private Rigidbody2D rigidbody2D;

    [FoldoutGroup("References")]
    [SerializeField] private Collider2D primaryCollider;

    [FoldoutGroup("References")]
    [SerializeField] private GlobalPooledObject pooledObject;

    [FoldoutGroup("References")]
    [SerializeField] private GlobalObjectPooler globalObjectPooler;

    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("Detonation"), MinValue(1)]
    [SerializeField] private int maxEffectOverlapHits = 32;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public ThrowableUtilityData ActiveData => activeData;

    private readonly List<Collider2D> ownerColliders = new();
    private readonly List<Collider2D> selfColliders = new();
    private readonly HashSet<int> affectedActorRoots = new();
    private ThrowableUtilityData activeData;
    private GameObject ownerRoot;
    private float detonateAtTime = float.NegativeInfinity;
    private float lastImpactNoiseTime = float.NegativeInfinity;
    private float resolvedTravelDistance;
    private float travelDistanceLimit;
    private bool launched;
    private bool hasResolvedPrimaryImpact;
    private Vector2 lastTrackedPosition;
    private Collider2D[] effectOverlapBuffer;
    private ContactFilter2D effectOverlapFilter;

    /// <summary>
    /// Caches same-object references and reusable buffers when component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        EnsureEffectOverlapBuffer();
        RefreshEffectOverlapFilter();
    }

    /// <summary>
    /// Initializes cached references and reusable physics data for pooled throws.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        EnsureEffectOverlapBuffer();
        RefreshEffectOverlapFilter();
    }

    /// <summary>
    /// Resets pooled runtime state every time throwable becomes active.
    /// </summary>
    private void OnEnable()
    {
        ResetRuntimeState();
    }

    /// <summary>
    /// Restores ignored collisions and clears transient physics state before pooling.
    /// </summary>
    private void OnDisable()
    {
        RestoreIgnoredOwnerCollisions();
        ResetPhysicsState();
        ResetRuntimeState();
    }

    /// <summary>
    /// Clamps editor settings and rebuilds reusable overlap buffer configuration.
    /// </summary>
    private void OnValidate()
    {
        maxEffectOverlapHits = Mathf.Max(1, maxEffectOverlapHits);
        EnsureEffectOverlapBuffer();
        RefreshEffectOverlapFilter();
    }

    /// <summary>
    /// Launches throwable with owner collision ignore setup and optional timed detonation.
    /// </summary>
    public void Launch(ThrowableUtilityData data, GameObject owner, Vector2 origin, Vector2 direction, float charge01)
    {
        CacheReferences();
        EnsureEffectOverlapBuffer();
        RefreshEffectOverlapFilter();
        RestoreIgnoredOwnerCollisions();
        ResetPhysicsState();
        ResetRuntimeState();

        activeData = data;
        ownerRoot = owner != null ? owner.transform.root.gameObject : null;
        transform.position = origin;
        transform.rotation = Quaternion.identity;
        lastTrackedPosition = origin;
        resolvedTravelDistance = 0f;
        travelDistanceLimit = activeData != null
            ? Mathf.Lerp(activeData.MinTravelDistance, activeData.MaxTravelDistance, Mathf.Clamp01(charge01))
            : 0f;

        CacheOwnerColliders();
        IgnoreOwnerCollisions(true);

        if (activeData == null || rigidbody2D == null)
            return;

        direction = direction.sqrMagnitude > MinimumLaunchDirectionSqr ? direction.normalized : Vector2.up;
        float throwForce = Mathf.Lerp(activeData.MinThrowForce, activeData.MaxThrowForce, Mathf.Clamp01(charge01));
        rigidbody2D.linearVelocity = direction * throwForce;
        rigidbody2D.angularVelocity = activeData.ThrowSpinSpeed;

        if (activeData.UsesTimerDetonation)
            detonateAtTime = Time.time + activeData.DetonationDelay;

        launched = true;
    }

    /// <summary>
    /// Tracks distance, handles timed detonation, and returns non-detonating throwables after rest.
    /// </summary>
    private void Update()
    {
        if (!launched || activeData == null)
            return;

        TrackTravelDistance();
        if (!hasResolvedPrimaryImpact &&
            travelDistanceLimit > 0f &&
            resolvedTravelDistance >= travelDistanceLimit)
        {
            HandleTravelLimitReached();
            return;
        }

        if (activeData.UsesTimerDetonation && Time.time >= detonateAtTime)
        {
            Detonate(transform.position);
            return;
        }

        if (!activeData.UsesDetonation &&
            hasResolvedPrimaryImpact &&
            rigidbody2D != null &&
            rigidbody2D.linearVelocity.sqrMagnitude <= StopVelocityThreshold * StopVelocityThreshold)
        {
            ReturnToPool();
        }
    }

    /// <summary>
    /// Resolves impact behavior for hit-detonating, direct-damage, and noise-maker throwables.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!launched || activeData == null || collision.collider == null)
            return;

        if (ownerRoot != null && collision.collider.transform.root == ownerRoot.transform)
            return;

        Vector2 impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;

        if (activeData.UsesHitDetonation)
        {
            Detonate(impactPoint);
            return;
        }

        EmitImpactFeedback(impactPoint);

        if (hasResolvedPrimaryImpact)
            return;

        hasResolvedPrimaryImpact = true;
        switch (activeData.Behavior)
        {
            case ThrowableUtilityBehavior.NoiseMaker:
                SpawnResolveEffect(impactPoint);
                ReturnToPool();
                break;

            case ThrowableUtilityBehavior.DirectDamage:
                CombatImpactUtility.TryApplyDirectImpact(
                    collision.collider,
                    activeData.DirectHitDamage,
                    activeData.DirectHitPenetration,
                    activeData.DirectHitStaggerDuration,
                    new Breezeblocks.Missions.ActorDamageContext(ownerRoot, activeData.DirectHitIsLethal));
                SpawnResolveEffect(impactPoint);
                ReturnToPool();
                break;

            default:
                StopAtImpact();
                break;
        }
    }

    /// <summary>
    /// Applies explosion or flashbang effects to unique targets inside reusable overlap buffer.
    /// </summary>
    private void Detonate(Vector2 detonationPoint)
    {
        if (activeData == null)
        {
            ReturnToPool();
            return;
        }

        hasResolvedPrimaryImpact = true;
        StopAtImpact();
        SpawnResolveEffect(detonationPoint);
        EmitDetonationNoise(detonationPoint);
        EmitDetonationSfx(detonationPoint);
        affectedActorRoots.Clear();

        if (activeData.EffectRadius > 0f)
        {
            int overlapCount = Physics2D.OverlapCircle(detonationPoint, activeData.EffectRadius, effectOverlapFilter, effectOverlapBuffer);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D hitCollider = effectOverlapBuffer[i];
                if (hitCollider == null)
                    continue;

                if (!TryRegisterAffectedActorRoot(hitCollider, out GameObject hitRootObject))
                    continue;

                if (!HasLineOfSightToEffectPoint(detonationPoint, hitCollider))
                    continue;

                ApplyDetonationEffectToTarget(hitCollider, hitRootObject, detonationPoint);
            }
        }

        ReturnToPool();
    }

    /// <summary>
    /// Resolves behavior when travel limit is reached before collision or timed detonation.
    /// </summary>
    private void HandleTravelLimitReached()
    {
        if (activeData == null)
        {
            ReturnToPool();
            return;
        }

        Vector2 impactPoint = transform.position;

        if (activeData.UsesHitDetonation)
        {
            Detonate(impactPoint);
            return;
        }

        EmitImpactFeedback(impactPoint, ignoreCooldown: true);

        hasResolvedPrimaryImpact = true;
        if (activeData.UsesTimerDetonation)
        {
            StopAtImpact();
            return;
        }

        SpawnResolveEffect(impactPoint);
        ReturnToPool();
    }

    /// <summary>
    /// Applies configured detonation behavior to one unique actor root.
    /// </summary>
    private void ApplyDetonationEffectToTarget(Collider2D hitCollider, GameObject hitRootObject, Vector2 detonationPoint)
    {
        switch (activeData.Behavior)
        {
            case ThrowableUtilityBehavior.Explosion:
                CombatImpactUtility.TryApplyUnarmoredExplosionDamage(
                    hitCollider,
                    activeData.ExplosionDamage,
                    new Breezeblocks.Missions.ActorDamageContext(ownerRoot, activeData.ExplosionIsLethal));
                if (activeData.ApplyExplosionKnockback)
                    CombatImpactUtility.TryApplyExplosionKnockback(hitCollider, detonationPoint, activeData.ExplosionKnockbackForce);
                break;

            case ThrowableUtilityBehavior.Flashbang:
                ApplyFlashbangEffect(hitRootObject);
                break;
        }
    }

    /// <summary>
    /// Tracks unique actor roots so one explosion does not process same actor multiple times.
    /// </summary>
    private bool TryRegisterAffectedActorRoot(Collider2D hitCollider, out GameObject hitRootObject)
    {
        hitRootObject = null;
        if (hitCollider == null)
            return false;

        Transform hitRoot = hitCollider.transform.root;
        if (hitRoot == null)
            return false;

        hitRootObject = hitRoot.gameObject;
        return affectedActorRoots.Add(hitRootObject.GetInstanceID());
    }

    /// <summary>
    /// Applies player or enemy flashbang status to affected root object.
    /// </summary>
    private void ApplyFlashbangEffect(GameObject hitRootObject)
    {
        if (hitRootObject == null || activeData == null)
            return;

        if (hitRootObject.TryGetComponent(out PlayerTopDownMotor2D _))
        {
            PlayerFlashbangEffect.EnsureOn(hitRootObject)
                ?.ApplyFlashbang(
                    activeData.FlashbangDuration,
                    activeData.FlashbangRecoveryThreshold,
                    activeData.PlayerRingingLoopClip,
                    activeData.OverridePlayerRingingSpatialBlend ? activeData.PlayerRingingSpatialBlend : 0f);
            return;
        }

        if (hitRootObject.TryGetComponent(out EnemyMovementController _))
        {
            EnemyFlashbangStatus.EnsureOn(hitRootObject)
                ?.ApplyFlashbang(activeData.FlashbangDuration, activeData.FlashbangRecoveryThreshold, activeData.FlashbangAimlessRotationSpeed);
        }
    }

    /// <summary>
    /// Checks whether effect point and target center have unobstructed line of sight.
    /// </summary>
    private bool HasLineOfSightToEffectPoint(Vector2 origin, Collider2D hitCollider)
    {
        if (hitCollider == null || activeData == null || activeData.EffectObstacleMask.value == 0)
            return true;

        Vector2 targetPoint = hitCollider.bounds.center;
        RaycastHit2D hit = Physics2D.Linecast(origin, targetPoint, activeData.EffectObstacleMask);
        return hit.collider == null;
    }

    /// <summary>
    /// Emits impact noise and SFX with cooldown to avoid spam on repeated bounces.
    /// </summary>
    private void EmitImpactFeedback(Vector2 impactPoint, bool ignoreCooldown = false)
    {
        if (activeData == null)
            return;

        if (!ignoreCooldown && Time.time < lastImpactNoiseTime + activeData.ImpactNoiseCooldown)
            return;

        lastImpactNoiseTime = Time.time;

        if (activeData.ImpactNoise > 0f)
            NoiseManager.EmitNoise(impactPoint, activeData.ImpactNoise, activeData.ImpactNoiseType, ResolveNoiseSource(), activeData.ImpactExtremeNoise);

        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);
        worldSfxManager?.PlayClipSetAt(impactPoint, activeData.ImpactSfx, activeData.ImpactNoiseType);
    }

    /// <summary>
    /// Emits configured detonation noise event for AI hearing systems.
    /// </summary>
    private void EmitDetonationNoise(Vector2 detonationPoint)
    {
        if (activeData == null || activeData.DetonationNoise <= 0f)
            return;

        NoiseManager.EmitNoise(detonationPoint, activeData.DetonationNoise, activeData.DetonationNoiseType, ResolveNoiseSource(), activeData.DetonationExtremeNoise);
    }

    /// <summary>
    /// Plays configured detonation world SFX at resolved detonation point.
    /// </summary>
    private void EmitDetonationSfx(Vector2 detonationPoint)
    {
        if (activeData == null)
            return;

        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);
        worldSfxManager?.PlayClipSetAt(detonationPoint, activeData.DetonationSfx, activeData.DetonationNoiseType);
    }

    /// <summary>
    /// Resolves the owning actor as the noise source so AI ignores its own thrown utility noise.
    /// </summary>
    private GameObject ResolveNoiseSource()
    {
        return ownerRoot != null ? ownerRoot : gameObject;
    }

    /// <summary>
    /// Caches local references and static self-collider list used for owner collision ignores.
    /// </summary>
    private void CacheReferences()
    {
        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();

        if (primaryCollider == null)
            primaryCollider = GetComponent<Collider2D>();

        if (pooledObject == null)
            pooledObject = GetComponent<GlobalPooledObject>();

        globalObjectPooler = WeaponRuntimeUtility.ResolveGlobalObjectPooler(globalObjectPooler);
        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);

        if (selfColliders.Count == 0)
            GetComponentsInChildren(true, selfColliders);
    }

    /// <summary>
    /// Collects owner colliders for temporary collision ignore after launch.
    /// </summary>
    private void CacheOwnerColliders()
    {
        ownerColliders.Clear();
        if (ownerRoot == null)
            return;

        ownerRoot.GetComponentsInChildren(true, ownerColliders);
    }

    /// <summary>
    /// Toggles collision ignore between throwable colliders and owner colliders.
    /// </summary>
    private void IgnoreOwnerCollisions(bool ignored)
    {
        if (ownerColliders.Count == 0 || selfColliders.Count == 0)
            return;

        for (int i = 0; i < selfColliders.Count; i++)
        {
            Collider2D selfCollider = selfColliders[i];
            if (selfCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Count; j++)
            {
                Collider2D ownerCollider = ownerColliders[j];
                if (ownerCollider == null)
                    continue;

                Physics2D.IgnoreCollision(selfCollider, ownerCollider, ignored);
            }
        }
    }

    /// <summary>
    /// Restores owner collisions after throwable resolves or returns to pool.
    /// </summary>
    private void RestoreIgnoredOwnerCollisions()
    {
        IgnoreOwnerCollisions(false);
        ownerColliders.Clear();
    }

    /// <summary>
    /// Clears current rigidbody movement state for pooled reset.
    /// </summary>
    private void ResetPhysicsState()
    {
        if (rigidbody2D == null)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.angularVelocity = 0f;
    }

    /// <summary>
    /// Stops throwable in place after impact when behavior should remain at collision point.
    /// </summary>
    private void StopAtImpact()
    {
        if (rigidbody2D == null)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.angularVelocity = 0f;
    }

    /// <summary>
    /// Clears transient state so pooled throwable starts from clean runtime data.
    /// </summary>
    private void ResetRuntimeState()
    {
        activeData = null;
        ownerRoot = null;
        detonateAtTime = float.NegativeInfinity;
        lastImpactNoiseTime = float.NegativeInfinity;
        resolvedTravelDistance = 0f;
        travelDistanceLimit = 0f;
        launched = false;
        hasResolvedPrimaryImpact = false;
        lastTrackedPosition = transform.position;
        affectedActorRoots.Clear();
    }

    /// <summary>
    /// Accumulates traveled distance for max-range throw resolution.
    /// </summary>
    private void TrackTravelDistance()
    {
        Vector2 currentPosition = transform.position;
        resolvedTravelDistance += Vector2.Distance(lastTrackedPosition, currentPosition);
        lastTrackedPosition = currentPosition;
    }

    /// <summary>
    /// Ensures non-alloc overlap buffer matches configured max detonation hits.
    /// </summary>
    private void EnsureEffectOverlapBuffer()
    {
        if (effectOverlapBuffer == null || effectOverlapBuffer.Length != maxEffectOverlapHits)
            effectOverlapBuffer = new Collider2D[Mathf.Max(1, maxEffectOverlapHits)];
    }

    /// <summary>
    /// Refreshes overlap filter so non-alloc radius queries keep expected trigger behavior.
    /// </summary>
    private void RefreshEffectOverlapFilter()
    {
        effectOverlapFilter.useLayerMask = false;
        effectOverlapFilter.useDepth = false;
        effectOverlapFilter.useNormalAngle = false;
        effectOverlapFilter.useTriggers = Physics2D.queriesHitTriggers;
    }

    /// <summary>
    /// Spawns pooled resolve effect aligned against current throwable travel direction.
    /// </summary>
    private void SpawnResolveEffect(Vector2 impactPoint)
    {
        if (activeData == null || activeData.ResolveEffectPrefab == null)
            return;

        globalObjectPooler = WeaponRuntimeUtility.ResolveGlobalObjectPooler(globalObjectPooler);
        if (globalObjectPooler == null)
            return;

        Vector2 effectDirection = rigidbody2D != null && rigidbody2D.linearVelocity.sqrMagnitude > MinimumLaunchDirectionSqr
            ? -rigidbody2D.linearVelocity.normalized
            : Vector2.up;
        float rotationAngle = Mathf.Atan2(effectDirection.y, effectDirection.x) * Mathf.Rad2Deg;

        GameObject effectInstance = globalObjectPooler.Spawn(
            activeData.ResolveEffectPrefab,
            impactPoint,
            Quaternion.Euler(0f, 0f, rotationAngle),
            null,
            activeData.ResolveEffectPoolPrewarm);
        if (effectInstance == null)
            return;

        if (!effectInstance.TryGetComponent(out BulletHitEffect hitEffect))
            hitEffect = effectInstance.AddComponent<BulletHitEffect>();

        hitEffect.Play();
    }

    /// <summary>
    /// Returns throwable to pool when available, otherwise disables object.
    /// </summary>
    private void ReturnToPool()
    {
        launched = false;
        hasResolvedPrimaryImpact = true;

        if (pooledObject != null)
        {
            pooledObject.ReturnToPool();
            return;
        }

        gameObject.SetActive(false);
    }
}

}
