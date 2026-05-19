using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Breezeblocks/Stealth/Combat Cover 2D")]
public class CombatCover2D : MonoBehaviour
{
    private const float MinimumDistance = 0.01f;
    private const float MinimumDirectionSqr = 0.0001f;

    [FoldoutGroup("References")]
    [SerializeField] private Collider2D coverCollider;

    [FoldoutGroup("Cover"), MinValue(0f)]
    [SerializeField] private float slotOffset = 0.45f;

    [FoldoutGroup("Cover"), Range(0f, 1f)]
    [SerializeField] private float blockChance = 0.75f;

    [FoldoutGroup("Cover"), MinValue(0f)]
    [Tooltip("How close an actor sample point must be to the computed cover slot to count as using this cover.")]
    [SerializeField] private float slotUsageRadius = 0.8f;

    [FoldoutGroup("Cover"), MinValue(0f)]
    [Tooltip("Small tolerance used when deciding whether a point is still protected by a hit surface or allowed to fire out from it.")]
    [SerializeField] private float hitSurfaceTolerance = 0.05f;

    [FoldoutGroup("Cover"), Range(-1f, 1f)]
    [Tooltip("How directly a shot must come from the protected side before the cover can intercept it.")]
    [SerializeField] private float protectionDotThreshold = 0.15f;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    public Collider2D CoverCollider => coverCollider;
    public float BlockChance => blockChance;

    private void Reset()
    {
        coverCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        if (coverCollider == null)
            coverCollider = GetComponent<Collider2D>();
    }

    private void OnValidate()
    {
        slotOffset = Mathf.Max(0f, slotOffset);
        blockChance = Mathf.Clamp01(blockChance);
        slotUsageRadius = Mathf.Max(0f, slotUsageRadius);
        hitSurfaceTolerance = Mathf.Max(0f, hitSurfaceTolerance);
        protectionDotThreshold = Mathf.Clamp(protectionDotThreshold, -1f, 1f);

        if (coverCollider == null)
            coverCollider = GetComponent<Collider2D>();
    }

    public bool TryGetCoverSlot(Vector2 threatPosition, out Vector2 slotPosition, out Vector2 protectionDirection)
    {
        slotPosition = transform.position;
        protectionDirection = Vector2.right;

        if (coverCollider == null)
            return false;

        Vector2 center = coverCollider.bounds.center;
        Vector2 toThreat = threatPosition - center;
        if (toThreat.sqrMagnitude <= MinimumDirectionSqr)
            toThreat = Vector2.right;

        protectionDirection = toThreat.normalized;

        Bounds bounds = coverCollider.bounds;
        float probeDistance = Mathf.Max(bounds.extents.x, bounds.extents.y) + Mathf.Max(slotOffset, MinimumDistance) + 1f;
        Vector2 safeProbePoint = center - (protectionDirection * probeDistance);
        Vector2 safeSurfacePoint = coverCollider.ClosestPoint(safeProbePoint);
        slotPosition = safeSurfacePoint - (protectionDirection * Mathf.Max(slotOffset, MinimumDistance));
        return true;
    }

    public bool TryEvaluateUsageAgainstThreat(Vector2 userPoint, Vector2 threatPosition, out Vector2 slotPosition, out Vector2 protectionDirection)
    {
        if (!TryGetCoverSlot(threatPosition, out slotPosition, out protectionDirection))
            return false;

        return IsPointWithinUsageDistance(userPoint, slotPosition);
    }

    public bool IsPointWithinUsageDistance(Vector2 point, Vector2 slotPosition)
    {
        return (point - slotPosition).sqrMagnitude <= slotUsageRadius * slotUsageRadius;
    }

    public bool OwnsCollider(Collider2D collider)
    {
        if (collider == null)
            return false;

        CombatCover2D candidateCover = collider.GetComponentInParent<CombatCover2D>();
        return candidateCover == this;
    }

    public bool DoesHitShieldPoint(RaycastHit2D coverHit, Vector2 protectedPoint)
    {
        if (coverHit.collider == null || !OwnsCollider(coverHit.collider))
            return false;

        return GetSignedSurfaceSide(coverHit, protectedPoint) <= hitSurfaceTolerance;
    }

    public bool AllowsPointToFirePastHit(RaycastHit2D coverHit, Vector2 firingPoint)
    {
        if (coverHit.collider == null || !OwnsCollider(coverHit.collider))
            return false;

        return GetSignedSurfaceSide(coverHit, firingPoint) >= -hitSurfaceTolerance;
    }

    public bool DoesProtectFrom(Vector2 shotOrigin, Vector2 protectedPosition, Vector2 protectionDirection)
    {
        if (protectionDirection.sqrMagnitude <= MinimumDirectionSqr)
            return false;

        Vector2 attackDirection = shotOrigin - protectedPosition;
        if (attackDirection.sqrMagnitude <= MinimumDirectionSqr)
            return false;

        return Vector2.Dot(attackDirection.normalized, protectionDirection.normalized) >= protectionDotThreshold;
    }

    private float GetSignedSurfaceSide(RaycastHit2D coverHit, Vector2 point)
    {
        Vector2 surfaceNormal = ResolveSurfaceNormal(coverHit);
        if (surfaceNormal.sqrMagnitude <= MinimumDirectionSqr)
            return 0f;

        return Vector2.Dot(point - coverHit.point, surfaceNormal);
    }

    private Vector2 ResolveSurfaceNormal(RaycastHit2D coverHit)
    {
        if (coverHit.normal.sqrMagnitude > MinimumDirectionSqr)
            return coverHit.normal.normalized;

        if (coverCollider == null)
            return Vector2.zero;

        Vector2 fallbackNormal = coverHit.point - (Vector2)coverCollider.bounds.center;
        if (fallbackNormal.sqrMagnitude <= MinimumDirectionSqr)
            fallbackNormal = coverHit.point - (Vector2)transform.position;

        return fallbackNormal.sqrMagnitude > MinimumDirectionSqr
            ? fallbackNormal.normalized
            : Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos || coverCollider == null)
            return;

        Bounds bounds = coverCollider.bounds;
        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.85f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
