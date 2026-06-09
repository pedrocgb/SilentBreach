using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Mission Actor Identity")]
public class MissionActorIdentity : MonoBehaviour
{
    [FoldoutGroup("Identity")]
    [SerializeField] private string actorId;

    [FoldoutGroup("Identity")]
    [SerializeField] private string actorDisplayName;

    [FoldoutGroup("Identity")]
    [SerializeField] private bool isInnocent;

    public string ActorId => string.IsNullOrWhiteSpace(actorId) ? name : actorId;
    public string ActorDisplayName => string.IsNullOrWhiteSpace(actorDisplayName) ? name : actorDisplayName;
    public bool IsInnocent => isInnocent;

    public static MissionActorIdentity EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        MissionActorIdentity identity = actorRoot.GetComponent<MissionActorIdentity>();
        if (identity == null)
            identity = actorRoot.AddComponent<MissionActorIdentity>();

        return identity;
    }

    private void OnValidate()
    {
        actorId = actorId != null ? actorId.Trim() : string.Empty;
        actorDisplayName = actorDisplayName != null ? actorDisplayName.Trim() : string.Empty;
    }
}

}
