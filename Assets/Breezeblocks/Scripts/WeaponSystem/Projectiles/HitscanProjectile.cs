using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Hitscan Projectile")]
public class HitscanProjectile : MonoBehaviour
{
    [FoldoutGroup("Raycast"), Tooltip("Optional collision mask. If everything is needed, keep this at Everything.")]
    [SerializeField] private LayerMask hitMask = ~0;

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

    private Coroutine _returnRoutine;

    private void Reset()
    {
        tracerLineRenderer = GetComponent<LineRenderer>();
        ConfigureTracerDefaults();
    }

    private void Awake()
    {
        EnsureTracerReference();
        ConfigureTracerDefaults();
        ResolveGlobalObjectPooler();
        RegisterImpactPrefab();
    }

    private void OnValidate()
    {
        tracerWidth = Mathf.Max(0f, tracerWidth);
        tracerTravelSpeed = Mathf.Max(0.01f, tracerTravelSpeed);
        tracerFadeDuration = Mathf.Max(0f, tracerFadeDuration);
        tracerEndWidthMultiplier = Mathf.Max(0f, tracerEndWidthMultiplier);
        bulletHitPoolPrewarm = Mathf.Max(0, bulletHitPoolPrewarm);
        bulletHitSurfaceOffset = Mathf.Max(0f, bulletHitSurfaceOffset);

        EnsureTracerReference();
        ConfigureTracerDefaults();
    }

    private void OnDisable()
    {
        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
        }

        if (tracerLineRenderer != null)
            tracerLineRenderer.enabled = false;
    }

    public void Fire(GameObject shooter, Vector2 origin, Vector2 direction, ProjectileData projectileData, float debugDuration = -1f)
    {
        if (projectileData == null || direction.sqrMagnitude <= 0.0001f)
        {
            ReturnToPoolOrDisable();
            return;
        }

        direction.Normalize();

        float range = projectileData.Range;
        Vector2 endPoint = origin + (direction * range);
        Color debugColor = Color.yellow;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, range, hitMask);
        RaycastHit2D chosenHit = default;
        bool foundHit = false;
        CoverUser2D shooterCoverUser = shooter != null ? shooter.GetComponentInParent<CoverUser2D>() : null;
        Vector2 threatPoint = origin + (direction * range);
        RaycastHit2D pendingCoverHit = default;
        CombatCover2D pendingCover = null;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || IsShooterCollider(hit.collider, shooter))
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
                foundHit = true;
                break;
            }

            chosenHit = pendingCover != null ? pendingCoverHit : hit;
            foundHit = true;
            break;
        }

        if (!foundHit && pendingCover != null)
        {
            chosenHit = pendingCoverHit;
            foundHit = true;
        }

        if (foundHit)
        {
            endPoint = chosenHit.point;
            debugColor = ResolveImpact(chosenHit.collider, projectileData);

            if (ShouldSpawnBulletHit(chosenHit.collider))
                SpawnBulletHitEffect(endPoint, direction);
        }

        Debug.DrawLine(origin, endPoint, debugColor, debugDuration >= 0f ? debugDuration : defaultDebugDuration);

        if (_returnRoutine != null)
            StopCoroutine(_returnRoutine);

        _returnRoutine = ShouldPlayTracer()
            ? StartCoroutine(PlayTracerAndReturn(origin, endPoint))
            : StartCoroutine(ReturnNextFrame());
    }

    private static bool IsDamageableCollider(Collider2D hitCollider)
    {
        if (hitCollider == null)
            return false;

        return hitCollider.GetComponentInParent<ArmorLoadout>() != null ||
               hitCollider.GetComponentInParent<ActorHealth>() != null;
    }

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

    private static bool IsShooterCollider(Collider2D hitCollider, GameObject shooter)
    {
        if (hitCollider == null || shooter == null)
            return false;

        Transform shooterRoot = shooter.transform.root;
        return hitCollider.transform.root == shooterRoot;
    }

    private static Color ResolveImpact(Collider2D hitCollider, ProjectileData projectileData)
    {
        ArmorLoadout armor = hitCollider.GetComponentInParent<ArmorLoadout>();
        ActorHealth health = hitCollider.GetComponentInParent<ActorHealth>();
        ActorStaggerController staggerController = hitCollider.GetComponentInParent<ActorStaggerController>();

        if (armor != null)
        {
            ArmorImpactResult armorImpact = armor.ResolveProjectileImpact(projectileData);
            if (!armorImpact.Penetrated && armorImpact.DamageToArmor > 0f && projectileData != null)
                staggerController?.ApplyStagger(projectileData.StaggerDuration);

            if (armorImpact.DamageToHealth > 0f && health != null)
                health.ApplyDamage(armorImpact.DamageToHealth);

            return armorImpact.Penetrated ? Color.green : Color.red;
        }

        if (health != null)
        {
            health.ApplyDamage(projectileData.Damage);
            return Color.green;
        }

        return Color.yellow;
    }

    private IEnumerator ReturnNextFrame()
    {
        yield return null;
        ReturnToPoolOrDisable();
        _returnRoutine = null;
    }

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
        _returnRoutine = null;
    }

    private bool ShouldPlayTracer()
    {
        return enableTracer && tracerLineRenderer != null;
    }

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

    private void EnsureTracerReference()
    {
        if (tracerLineRenderer == null)
            tracerLineRenderer = GetComponent<LineRenderer>();
    }

    private void ConfigureTracerDefaults()
    {
        if (tracerLineRenderer == null)
            return;

        tracerLineRenderer.useWorldSpace = true;
        tracerLineRenderer.positionCount = 2;
        tracerLineRenderer.enabled = false;
    }

    private bool ShouldSpawnBulletHit(Collider2D hitCollider)
    {
        if (hitCollider == null || bulletHitPrefab == null)
            return false;

        return spawnBulletHitOnDamageableTargets || !IsDamageableCollider(hitCollider);
    }

    private void SpawnBulletHitEffect(Vector2 impactPoint, Vector2 shotDirection)
    {
        ResolveGlobalObjectPooler();
        if (globalObjectPooler == null || bulletHitPrefab == null || shotDirection.sqrMagnitude <= 0.0001f)
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

        BulletHitEffect hitEffect = impactInstance.GetComponent<BulletHitEffect>();
        if (hitEffect == null)
            hitEffect = impactInstance.AddComponent<BulletHitEffect>();

        hitEffect.Play();
    }

    private void ResolveGlobalObjectPooler()
    {
        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;
    }

    private void RegisterImpactPrefab()
    {
        if (globalObjectPooler == null || bulletHitPrefab == null)
            return;

        globalObjectPooler.RegisterPrefab(bulletHitPrefab, bulletHitPoolPrewarm);
    }

    private void ReturnToPoolOrDisable()
    {
        GlobalPooledObject pooledObject = GetComponent<GlobalPooledObject>();
        if (pooledObject != null)
            pooledObject.ReturnToPool();
        else
            gameObject.SetActive(false);
    }
}
}
