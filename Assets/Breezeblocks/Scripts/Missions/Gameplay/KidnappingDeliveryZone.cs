using Sirenix.OdinInspector;
using Breezeblocks.WeaponSystem;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Breezeblocks/Missions/Kidnapping Delivery Zone")]
public sealed class KidnappingDeliveryZone : MonoBehaviour
{
    [FoldoutGroup("Kidnapping")]
    [SerializeField] private string targetActorId;

    [FoldoutGroup("Kidnapping")]
    [SerializeField] private bool acceptAnyIncapacitatedTarget;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool Delivered => delivered;

    private Collider2D zoneCollider;
    private bool delivered;

    /// <summary>
    /// Caches trigger collider when component is reset in editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    /// <summary>
    /// Caches required trigger collider before gameplay.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Keeps target id normalized and collider configured as trigger.
    /// </summary>
    private void OnValidate()
    {
        targetActorId = targetActorId != null ? targetActorId.Trim() : string.Empty;
        CacheReferences();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    /// <summary>
    /// Attempts delivery when an incapacitated target enters the extraction trigger.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDeliver(other);
    }

    /// <summary>
    /// Attempts delivery while dragged bodies remain inside the extraction trigger.
    /// </summary>
    private void OnTriggerStay2D(Collider2D other)
    {
        TryDeliver(other);
    }

    /// <summary>
    /// Clears runtime delivery state for testing.
    /// </summary>
    [Button(ButtonSizes.Small)]
    public void ResetRuntimeState()
    {
        delivered = false;
    }

    /// <summary>
    /// Validates and completes a kidnapping delivery for the supplied collider.
    /// </summary>
    private void TryDeliver(Collider2D other)
    {
        if (delivered || other == null)
            return;

        ActorHealth actorHealth = other.GetComponentInParent<ActorHealth>();
        if (actorHealth == null || !actorHealth.IsIncapacitated || actorHealth.IsDead)
            return;

        MissionActorIdentity identity = actorHealth.GetComponent<MissionActorIdentity>() ?? actorHealth.GetComponentInParent<MissionActorIdentity>();
        if (!MatchesTarget(identity))
            return;

        delivered = true;
        MissionRuntimeEvents.RaiseKidnappingDelivered(actorHealth, identity, gameObject);
        actorHealth.gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns whether supplied identity matches this delivery zone's target filter.
    /// </summary>
    private bool MatchesTarget(MissionActorIdentity identity)
    {
        if (acceptAnyIncapacitatedTarget || string.IsNullOrWhiteSpace(targetActorId))
            return true;

        return identity != null &&
               string.Equals(identity.ActorId, targetActorId, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Caches same-object collider reference.
    /// </summary>
    private void CacheReferences()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();
    }
}

}
