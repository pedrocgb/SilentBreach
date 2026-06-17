using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Hitscan Projectile")]
public class HitscanProjectile : MonoBehaviour
{
    private const float MinimumDirectionSqr = 0.0001f;

    [FoldoutGroup("Raycast"), Tooltip("Optional collision mask. If everything is needed, keep this at Everything.")]
    [SerializeField] private LayerMask hitMask = ~0;

    [FoldoutGroup("Raycast"), MinValue(1)]
    [SerializeField] private int maxRaycastHits = 16;

    [FoldoutGroup("Tracer"), Tooltip("Optional line renderer used to display the bullet tracer.")]
    [SerializeField] private LineRenderer tracerLineRenderer;

    [FoldoutGroup("Tracer")]
    [SerializeField] private bool enableTracer = true;

    [FoldoutGroup("Tracer"), MinValue(0f)]
    [SerializeField] private float tracerWidth = 0.06f;

    [FoldoutGroup("Tracer"), MinValue(0.01f)]
    [SerializeField] private float tracerTravelSpeed = 180f;

    [FoldoutGroup("Tracer"), MinValue(0f)]
    [SerializeField] private float tracerFadeDuration = 0.04f;

    [FoldoutGroup("Tracer"), MinValue(0f)]
    [SerializeField] private float tracerEndWidthMultiplier = 0.35f;

    [FoldoutGroup("Tracer")]
    [SerializeField] private Color tracerStartColor = new(1f, 0.92f, 0.7f, 0.95f);

    [FoldoutGroup("Tracer")]
    [SerializeField] private Color tracerEndColor = new(1f, 0.45f, 0.15f, 0.1f);

    [FoldoutGroup("Impact")]
    [SerializeField] private GlobalObjectPooler globalObjectPooler;

    [FoldoutGroup("Impact"), AssetsOnly]
    [SerializeField] private GameObject bulletHitPrefab;

    [FoldoutGroup("Impact"), MinValue(0)]
    [SerializeField] private int bulletHitPoolPrewarm = 8;

    [FoldoutGroup("Impact")]
    [SerializeField] private bool spawnBulletHitOnDamageableTargets;

    [FoldoutGroup("Impact")]
    [SerializeField] private float bulletHitRotationOffset;

    [FoldoutGroup("Impact"), MinValue(0f)]
    [SerializeField] private float bulletHitSurfaceOffset = 0.01f;

    [FoldoutGroup("Debug"), MinValue(0f)]
    [SerializeField] private float defaultDebugDuration = 0.1f;

    private Coroutine returnRoutine;
    private RaycastHit2D[] raycastHitBuffer;
    private ContactFilter2D hitContactFilter;
    private GlobalPooledObject pooledObject;

    /// <summary>
    /// Caches optional local references when component is first added.
    /// </summary>
    private void Reset()
    {
        tracerLineRenderer = GetComponent<LineRenderer>();
        pooledObject = GetComponent<GlobalPooledObject>();
        ConfigureTracerDefaults();
        EnsureRaycastBuffer();
        RefreshHitContactFilter();
    }

    /// <summary>
    /// Initializes cached references and reusable physics buffers.
    /// </summary>
    private void Awake()
    {
        EnsureTracerReference();
        pooledObject = GetComponent<GlobalPooledObject>();
        ConfigureTracerDefaults();
        EnsureRaycastBuffer();
        RefreshHitContactFilter();
        ResolveGlobalObjectPooler();
        RegisterImpactPrefab();
    }

    /// <summary>
    /// Clamps serialized settings and rebuilds reusable runtime configuration in editor.
    /// </summary>
    private void OnValidate()
    {
        maxRaycastHits = Mathf.Max(1, maxRaycastHits);
        tracerWidth = Mathf.Max(0f, tracerWidth);
        tracerTravelSpeed = Mathf.Max(0.01f, tracerTravelSpeed);
        tracerFadeDuration = Mathf.Max(0f, tracerFadeDuration);
        tracerEndWidthMultiplier = Mathf.Max(0f, tracerEndWidthMultiplier);
        bulletHitPoolPrewarm = Mathf.Max(0, bulletHitPoolPrewarm);
        bulletHitSurfaceOffset = Mathf.Max(0f, bulletHitSurfaceOffset);

        EnsureTracerReference();
        ConfigureTracerDefaults();
        EnsureRaycastBuffer();
        RefreshHitContactFilter();
    }

    /// <summary>
    /// Stops active visual routines and hides tracer when pooled object is disabled.
    /// </summary>
    private void OnDisable()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        if (tracerLineRenderer != null)
            tracerLineRenderer.enabled = false;
    }

    /// <summary>
    /// Resolves first valid impact, applies damage, and plays optional tracer before returning to pool.
    /// </summary>
    public void Fire(GameObject shooter, Vector2 origin, Vector2 direction, ProjectileData projectileData, float debugDuration = -1f)
    {
        if (projectileData == null || direction.sqrMagnitude <= MinimumDirectionSqr)
        {
            ReturnToPoolOrDisable();
            return;
        }

        EnsureRaycastBuffer();
        RefreshHitContactFilter();

        direction.Normalize();

        float range = projectileData.Range;
        Vector2 endPoint = origin + (direction * range);
        Color debugColor = Color.yellow;
        bool chosenHitWasDamageable = false;
        RaycastHit2D chosenHit = default;
        bool foundHit = TryResolveImpactHit(
            shooter,
            origin,
            direction,
            range,
            out chosenHit,
            out chosenHitWasDamageable);

        if (foundHit)
        {
            endPoint = chosenHit.point;
            debugColor = ResolveImpact(chosenHit.collider, projectileData, shooter, direction);

            if (ShouldSpawnBulletHit(chosenHitWasDamageable))
                SpawnBulletHitEffect(endPoint, direction);
        }

        Debug.DrawLine(origin, endPoint, debugColor, debugDuration >= 0f ? debugDuration : defaultDebugDuration);

        if (returnRoutine != null)
            StopCoroutine(returnRoutine);

        returnRoutine = ShouldPlayTracer()
            ? StartCoroutine(PlayTracerAndReturn(origin, endPoint))
            : StartCoroutine(ReturnNextFrame());
    }

    /// <summary>
    /// Finds best hit from reusable raycast buffer while respecting cover logic.
    /// </summary>
    private bool TryResolveImpactHit(
        GameObject shooter,
        Vector2 origin,
        Vector2 direction,
        float range,
        out RaycastHit2D chosenHit,
        out bool chosenHitWasDamageable)
    {
        chosenHit = default;
        chosenHitWasDamageable = false;

        int hitCount = Physics2D.Raycast(origin, direction, hitContactFilter, raycastHitBuffer, range);
        Transform shooterRoot = shooter != null ? shooter.transform.root : null;
        CoverUser2D shooterCoverUser = shooterRoot != null ? shooterRoot.GetComponent<CoverUser2D>() : null;
        Vector2 threatPoint = origin + (direction * range);
        RaycastHit2D pendingCoverHit = default;
        CombatCover2D pendingCover = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = raycastHitBuffer[i];
            if (hit.collider == null || IsShooterCollider(hit.collider, shooterRoot))
                continue;

            CombatCover2D hitCover = hit.collider.GetComponentInParent<CombatCover2D>();
            bool isDamageableTarget = IsDamageableCollider(hit.collider);

            if (hitCover != null)
            {
                if (shooterCoverUser != null && shooterCoverUser.ShouldIgnoreOutgoingCoverHit(hitCover, hit, threatPoint))
                    continue;

                pendingCoverHit = hit;
                pendingCover = hitCover;
                continue;
            }

            if (isDamageableTarget)
            {
                chosenHit = ResolveTargetImpactHit(origin, hit, pendingCoverHit, pendingCover);
                chosenHitWasDamageable = chosenHit.collider == hit.collider;
                return true;
            }

            chosenHit = pendingCover != null ? pendingCoverHit : hit;
            chosenHitWasDamageable = false;
            return true;
        }

        if (pendingCover != null && pendingCoverHit.collider != null)
        {
            chosenHit = pendingCoverHit;
            chosenHitWasDamageable = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether collider belongs to damageable actor hierarchy.
    /// </summary>
    private static bool IsDamageableCollider(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        return hitCollider.GetComponentInParent<ArmorLoadout>() != null ||
               hitCollider.GetComponentInParent<ActorHealth>() != null;
    }

    /// <summary>
    /// Resolves whether cover or actor should receive final projectile impact.
    /// </summary>
    private static RaycastHit2D ResolveTargetImpactHit(Vector2 shotOrigin, RaycastHit2D actorHit, RaycastHit2D pendingCoverHit, CombatCover2D pendingCover)
    {
        if (pendingCover == null || pendingCoverHit.collider == null)
            return actorHit;

        CoverUser2D targetCoverUser = actorHit.collider != null ? actorHit.collider.GetComponentInParent<CoverUser2D>() : null;
        if (targetCoverUser != null &&
            targetCoverUser.TryResolveCoverBlock(shotOrigin, pendingCoverHit, pendingCover, out CombatCover2D activeCover, out float blockChance) &&
            activeCover == pendingCover)
        {
            return Random.value < blockChance ? pendingCoverHit : actorHit;
        }

        return pendingCoverHit;
    }

    /// <summary>
    /// Checks whether hit collider belongs to shooter root and should be ignored.
    /// </summary>
    private static bool IsShooterCollider(Collider2D hitCollider, Transform shooterRoot)
    {
        if (hitCollider == null || shooterRoot == null)
            return false;

        return hitCollider.transform.root == shooterRoot;
    }

    /// <summary>
    /// Applies projectile combat impact and returns debug color for result.
    /// </summary>
    private static Color ResolveImpact(Collider2D hitCollider, ProjectileData projectileData, GameObject shooter, Vector2 projectileDirection)
    {
        if (projectileData == null || hitCollider == null)
            return Color.yellow;

        ArmorLoadout armor = hitCollider.GetComponentInParent<ArmorLoadout>();
        bool impactApplied = CombatImpactUtility.TryApplyProjectileImpact(hitCollider, projectileData, shooter, projectileDirection);
        if (!impactApplied)
            return Color.yellow;

        return armor != null && armor.HasArmor ? Color.red : Color.green;
    }

    /// <summary>
    /// Returns pooled projectile on next frame when no tracer needs playback.
    /// </summary>
    private IEnumerator ReturnNextFrame()
    {
        yield return null;
        ReturnToPoolOrDisable();
        returnRoutine = null;
    }

    /// <summary>
    /// Animates tracer travel and fade before returning projectile to pool.
    /// </summary>
    private IEnumerator PlayTracerAndReturn(Vector2 origin, Vector2 endPoint)
    {
        tracerLineRenderer.enabled = true;

        float distance = Vector2.Distance(origin, endPoint);
        float travelDuration = tracerTravelSpeed > 0f ? distance / tracerTravelSpeed : 0f;
        float elapsed = 0f;

        while (elapsed < travelDuration)
        {
            float t = travelDuration > 0f ? elapsed / travelDuration : 1f;
            Vector2 currentEndPoint = Vector2.Lerp(origin, endPoint, t);
            UpdateTracerVisual(origin, currentEndPoint, 1f, 1f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < tracerFadeDuration)
        {
            float t = tracerFadeDuration > 0f ? elapsed / tracerFadeDuration : 1f;
            float fadeMultiplier = 1f - t;
            UpdateTracerVisual(origin, endPoint, fadeMultiplier, fadeMultiplier);

            elapsed += Time.deltaTime;
            yield return null;
        }

        UpdateTracerVisual(origin, endPoint, 0f, 0f);
        tracerLineRenderer.enabled = false;
        ReturnToPoolOrDisable();
        returnRoutine = null;
    }

    /// <summary>
    /// Reports whether tracer renderer should animate for this shot instance.
    /// </summary>
    private bool ShouldPlayTracer()
    {
        return enableTracer && tracerLineRenderer != null;
    }

    /// <summary>
    /// Updates tracer width, alpha, and endpoints for current frame.
    /// </summary>
    private void UpdateTracerVisual(Vector2 startPoint, Vector2 endPoint, float alphaMultiplier, float widthMultiplier)
    {
        if (tracerLineRenderer == null)
            return;

        tracerLineRenderer.SetPosition(0, startPoint);
        tracerLineRenderer.SetPosition(1, endPoint);

        float currentWidth = tracerWidth * Mathf.Max(0f, widthMultiplier);
        tracerLineRenderer.startWidth = currentWidth;
        tracerLineRenderer.endWidth = currentWidth * tracerEndWidthMultiplier;

        Color startColor = tracerStartColor;
        Color endColor = tracerEndColor;
        startColor.a *= Mathf.Clamp01(alphaMultiplier);
        endColor.a *= Mathf.Clamp01(alphaMultiplier);

        tracerLineRenderer.startColor = startColor;
        tracerLineRenderer.endColor = endColor;
    }

    /// <summary>
    /// Ensures optional tracer renderer reference exists when component has one locally.
    /// </summary>
    private void EnsureTracerReference()
    {
        if (tracerLineRenderer == null)
            tracerLineRenderer = GetComponent<LineRenderer>();
    }

    /// <summary>
    /// Applies stable runtime defaults to tracer renderer.
    /// </summary>
    private void ConfigureTracerDefaults()
    {
        if (tracerLineRenderer == null)
            return;

        tracerLineRenderer.useWorldSpace = true;
        tracerLineRenderer.positionCount = 2;
        tracerLineRenderer.enabled = false;
    }

    /// <summary>
    /// Keeps reusable raycast buffer sized for configured hit cap.
    /// </summary>
    private void EnsureRaycastBuffer()
    {
        if (raycastHitBuffer == null || raycastHitBuffer.Length != maxRaycastHits)
            raycastHitBuffer = new RaycastHit2D[Mathf.Max(1, maxRaycastHits)];
    }

    /// <summary>
    /// Refreshes contact filter used by non-alloc projectile raycasts.
    /// </summary>
    private void RefreshHitContactFilter()
    {
        hitContactFilter.useLayerMask = true;
        hitContactFilter.layerMask = hitMask;
        hitContactFilter.useTriggers = Physics2D.queriesHitTriggers;
        hitContactFilter.useDepth = false;
        hitContactFilter.useNormalAngle = false;
    }

    /// <summary>
    /// Decides whether impact effect should spawn for resolved hit type.
    /// </summary>
    private bool ShouldSpawnBulletHit(bool hitDamageableTarget)
    {
        if (bulletHitPrefab == null)
            return false;

        return spawnBulletHitOnDamageableTargets || !hitDamageableTarget;
    }

    /// <summary>
    /// Spawns and plays pooled bullet impact effect at resolved surface point.
    /// </summary>
    private void SpawnBulletHitEffect(Vector2 impactPoint, Vector2 shotDirection)
    {
        ResolveGlobalObjectPooler();
        if (globalObjectPooler == null || bulletHitPrefab == null || shotDirection.sqrMagnitude <= MinimumDirectionSqr)
            return;

        Vector2 oppositeDirection = (-shotDirection).normalized;
        float rotationAngle = Mathf.Atan2(oppositeDirection.y, oppositeDirection.x) * Mathf.Rad2Deg + bulletHitRotationOffset;
        Vector3 spawnPosition = impactPoint + (oppositeDirection * bulletHitSurfaceOffset);

        GameObject impactInstance = globalObjectPooler.Spawn(
            bulletHitPrefab,
            spawnPosition,
            Quaternion.Euler(0f, 0f, rotationAngle),
            null,
            bulletHitPoolPrewarm);

        if (impactInstance == null)
            return;

        if (!impactInstance.TryGetComponent(out BulletHitEffect hitEffect))
            hitEffect = impactInstance.AddComponent<BulletHitEffect>();

        hitEffect.Play();
    }

    /// <summary>
    /// Resolves shared pooler dependency once before pooled impact usage.
    /// </summary>
    private void ResolveGlobalObjectPooler()
    {
        globalObjectPooler = WeaponRuntimeUtility.ResolveGlobalObjectPooler(globalObjectPooler);
    }

    /// <summary>
    /// Registers optional impact prefab with shared pooler for warm startup.
    /// </summary>
    private void RegisterImpactPrefab()
    {
        if (bulletHitPrefab == null)
            return;

        ResolveGlobalObjectPooler();
        globalObjectPooler?.RegisterPrefab(bulletHitPrefab, bulletHitPoolPrewarm);
    }

    /// <summary>
    /// Returns projectile to pool when available, otherwise disables object.
    /// </summary>
    private void ReturnToPoolOrDisable()
    {
        pooledObject ??= GetComponent<GlobalPooledObject>();
        if (pooledObject != null)
        {
            pooledObject.ReturnToPool();
            return;
        }

        gameObject.SetActive(false);
    }
}

}
