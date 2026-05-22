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

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        ResetRuntimeState();
    }

    private void OnDisable()
    {
        RestoreIgnoredOwnerCollisions();
        ResetPhysicsState();
        ResetRuntimeState();
    }

    public void Launch(ThrowableUtilityData data, GameObject owner, Vector2 origin, Vector2 direction, float charge01)
    {
        CacheReferences();
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
                    activeData.DirectHitStaggerDuration);
                SpawnResolveEffect(impactPoint);
                ReturnToPool();
                break;

            default:
                StopAtImpact();
                break;
        }
    }

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
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(detonationPoint, activeData.EffectRadius);
            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider2D hitCollider = overlaps[i];
                if (hitCollider == null)
                    continue;

                Transform hitRoot = hitCollider.transform.root;
                if (hitRoot == null)
                    continue;

                int rootId = hitRoot.gameObject.GetInstanceID();
                if (!affectedActorRoots.Add(rootId))
                    continue;

                if (!HasLineOfSightToEffectPoint(detonationPoint, hitCollider))
                    continue;

                switch (activeData.Behavior)
                {
                    case ThrowableUtilityBehavior.Explosion:
                        CombatImpactUtility.TryApplyUnarmoredExplosionDamage(hitCollider, activeData.ExplosionDamage);
                        break;

                    case ThrowableUtilityBehavior.Flashbang:
                        ApplyFlashbangEffect(hitCollider);
                        break;
                }
            }
        }

        ReturnToPool();
    }

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

    private void ApplyFlashbangEffect(Collider2D hitCollider)
    {
        if (hitCollider == null || activeData == null)
            return;

        if (hitCollider.GetComponentInParent<PlayerTopDownMotor2D>() != null)
        {
            PlayerFlashbangEffect.EnsureOn(hitCollider.transform.root.gameObject)
                ?.ApplyFlashbang(
                    activeData.FlashbangDuration,
                    activeData.FlashbangRecoveryThreshold,
                    activeData.PlayerRingingLoopClip,
                    activeData.OverridePlayerRingingSpatialBlend ? activeData.PlayerRingingSpatialBlend : 0f);
            return;
        }

        if (hitCollider.GetComponentInParent<EnemyMovementController>() != null)
        {
            EnemyFlashbangStatus.EnsureOn(hitCollider.transform.root.gameObject)
                ?.ApplyFlashbang(activeData.FlashbangDuration, activeData.FlashbangRecoveryThreshold, activeData.FlashbangAimlessRotationSpeed);
        }
    }

    private bool HasLineOfSightToEffectPoint(Vector2 origin, Collider2D hitCollider)
    {
        if (hitCollider == null || activeData == null || activeData.EffectObstacleMask.value == 0)
            return true;

        Vector2 targetPoint = hitCollider.bounds.center;
        RaycastHit2D hit = Physics2D.Linecast(origin, targetPoint, activeData.EffectObstacleMask);
        return hit.collider == null;
    }

    private void EmitImpactFeedback(Vector2 impactPoint, bool ignoreCooldown = false)
    {
        if (activeData == null)
            return;

        if (!ignoreCooldown && Time.time < lastImpactNoiseTime + activeData.ImpactNoiseCooldown)
            return;

        lastImpactNoiseTime = Time.time;

        if (activeData.ImpactNoise > 0f)
            NoiseManager.EmitNoise(impactPoint, activeData.ImpactNoise, activeData.ImpactNoiseType, gameObject, activeData.ImpactExtremeNoise);

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        worldSfxManager?.PlayClipSetAt(impactPoint, activeData.ImpactSfx, activeData.ImpactNoiseType);
    }

    private void EmitDetonationNoise(Vector2 detonationPoint)
    {
        if (activeData == null || activeData.DetonationNoise <= 0f)
            return;

        NoiseManager.EmitNoise(detonationPoint, activeData.DetonationNoise, activeData.DetonationNoiseType, gameObject, activeData.DetonationExtremeNoise);
    }

    private void EmitDetonationSfx(Vector2 detonationPoint)
    {
        if (activeData == null)
            return;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        worldSfxManager?.PlayClipSetAt(detonationPoint, activeData.DetonationSfx, activeData.DetonationNoiseType);
    }

    private void CacheReferences()
    {
        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();

        if (primaryCollider == null)
            primaryCollider = GetComponent<Collider2D>();

        if (pooledObject == null)
            pooledObject = GetComponent<GlobalPooledObject>();

        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        selfColliders.Clear();
        GetComponentsInChildren(true, selfColliders);
    }

    private void CacheOwnerColliders()
    {
        ownerColliders.Clear();
        if (ownerRoot == null)
            return;

        ownerRoot.GetComponentsInChildren(true, ownerColliders);
    }

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

    private void RestoreIgnoredOwnerCollisions()
    {
        IgnoreOwnerCollisions(false);
        ownerColliders.Clear();
    }

    private void ResetPhysicsState()
    {
        if (rigidbody2D == null)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.angularVelocity = 0f;
    }

    private void StopAtImpact()
    {
        if (rigidbody2D == null)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.angularVelocity = 0f;
    }

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

    private void TrackTravelDistance()
    {
        Vector2 currentPosition = transform.position;
        resolvedTravelDistance += Vector2.Distance(lastTrackedPosition, currentPosition);
        lastTrackedPosition = currentPosition;
    }

    private void SpawnResolveEffect(Vector2 impactPoint)
    {
        if (activeData == null || activeData.ResolveEffectPrefab == null)
            return;

        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        if (globalObjectPooler == null)
            return;

        Vector2 effectDirection = rigidbody2D != null && rigidbody2D.linearVelocity.sqrMagnitude > 0.0001f
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

        BulletHitEffect hitEffect = effectInstance.GetComponent<BulletHitEffect>();
        if (hitEffect == null)
            hitEffect = effectInstance.AddComponent<BulletHitEffect>();

        hitEffect.Play();
    }

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
