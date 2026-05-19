using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Cover User 2D")]
public class CoverUser2D : MonoBehaviour
{
    private const float MinimumDirectionSqr = 0.0001f;

    [FoldoutGroup("References")]
    [Tooltip("Optional point used for cover and shot protection checks. If empty, this transform position is used.")]
    [SerializeField] private Transform samplePoint;

    [FoldoutGroup("Resolution")]
    [Tooltip("When enabled, this actor can still use cover protection even if no explicit active cover was assigned by AI logic.")]
    [SerializeField] private bool allowThreatBasedCoverResolution = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public CombatCover2D ActiveCover { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInCover { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 ActiveCoverPoint { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 ProtectionDirection { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastThreatPosition { get; private set; }

    public Vector2 SamplePosition => samplePoint != null ? (Vector2)samplePoint.position : (Vector2)transform.position;

    public void SetActiveCover(CombatCover2D cover, Vector2 coverPoint, Vector2 protectionDirection, Vector2 threatPosition)
    {
        ActiveCover = cover;
        ActiveCoverPoint = coverPoint;
        ProtectionDirection = protectionDirection.sqrMagnitude > MinimumDirectionSqr
            ? protectionDirection.normalized
            : Vector2.zero;
        LastThreatPosition = threatPosition;
        IsInCover = ActiveCover != null;
    }

    public void ClearActiveCover()
    {
        ActiveCover = null;
        ActiveCoverPoint = Vector2.zero;
        ProtectionDirection = Vector2.zero;
        LastThreatPosition = Vector2.zero;
        IsInCover = false;
    }

    public bool OwnsCoverCollider(Collider2D collider)
    {
        return ActiveCover != null && ActiveCover.OwnsCollider(collider);
    }

    public bool ShouldIgnoreOutgoingCoverHit(CombatCover2D candidateCover, RaycastHit2D coverHit, Vector2 threatPosition)
    {
        if (!TryResolveCoverUsage(candidateCover, threatPosition, out CombatCover2D resolvedCover))
            return false;

        return resolvedCover.AllowsPointToFirePastHit(coverHit, SamplePosition);
    }

    public bool TryResolveCoverBlock(Vector2 shotOrigin, RaycastHit2D coverHit, CombatCover2D candidateCover, out CombatCover2D blockingCover, out float blockChance)
    {
        blockingCover = null;
        blockChance = 0f;

        if (!TryResolveCoverUsage(candidateCover, shotOrigin, out CombatCover2D resolvedCover))
            return false;

        if (!resolvedCover.DoesHitShieldPoint(coverHit, SamplePosition))
            return false;

        blockingCover = resolvedCover;
        blockChance = resolvedCover.BlockChance;
        return blockChance > 0f;
    }

    private bool TryResolveCoverUsage(CombatCover2D candidateCover, Vector2 threatPosition, out CombatCover2D resolvedCover)
    {
        resolvedCover = null;
        if (candidateCover == null)
            return false;

        if (TryResolveActiveCoverUsage(candidateCover, threatPosition))
        {
            resolvedCover = candidateCover;
            return true;
        }

        if (!allowThreatBasedCoverResolution)
            return false;

        if (!candidateCover.TryEvaluateUsageAgainstThreat(SamplePosition, threatPosition, out _, out _))
            return false;

        resolvedCover = candidateCover;
        return true;
    }

    private bool TryResolveActiveCoverUsage(CombatCover2D candidateCover, Vector2 threatPosition)
    {
        if (!IsInCover || ActiveCover == null || ActiveCover != candidateCover)
            return false;

        if (candidateCover.TryEvaluateUsageAgainstThreat(SamplePosition, threatPosition, out _, out _))
            return true;

        return candidateCover.IsPointWithinUsageDistance(SamplePosition, ActiveCoverPoint);
    }
}
