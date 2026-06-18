using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Mission Actor Identity")]
public class MissionActorIdentity : MonoBehaviour
{
    private string actorId;
    private string actorDisplayName;
    private bool isInnocent;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string ActorId => string.IsNullOrWhiteSpace(actorId) ? name : actorId;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string ActorDisplayName => string.IsNullOrWhiteSpace(actorDisplayName) ? name : actorDisplayName;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInnocent => isInnocent;

    /// <summary>
    /// Ensures an actor root has an identity component for mission objective and failure checks.
    /// </summary>
    public static MissionActorIdentity EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        MissionActorIdentity identity = actorRoot.GetComponent<MissionActorIdentity>();
        if (identity == null)
            identity = actorRoot.AddComponent<MissionActorIdentity>();

        return identity;
    }

    /// <summary>
    /// Applies profile-authored identity values to this runtime actor.
    /// </summary>
    public void ApplySettings(MissionActorIdentitySettings settings)
    {
        if (settings == null)
            return;

        actorId = settings.ActorId != null ? settings.ActorId.Trim() : string.Empty;
        actorDisplayName = settings.ActorDisplayName != null ? settings.ActorDisplayName.Trim() : string.Empty;
        isInnocent = settings.IsInnocent;
    }

    /// <summary>
    /// Keeps runtime identity strings normalized when edited through debug tooling.
    /// </summary>
    private void OnValidate()
    {
        actorId = actorId != null ? actorId.Trim() : string.Empty;
        actorDisplayName = actorDisplayName != null ? actorDisplayName.Trim() : string.Empty;
    }
}

}
