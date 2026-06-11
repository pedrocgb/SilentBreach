using UnityEngine;

namespace Breezeblocks.Missions
{

/// <summary>
/// Defines a runtime target that can be solved by the shared lockpicking minigame.
/// </summary>
public interface ILockpickSessionTarget
{
    /// <summary>
    /// Gets the authored minigame definition that configures this lock.
    /// </summary>
    LockpickMinigameDefinition Definition { get; }

    /// <summary>
    /// Applies the successful unlock result to the target after the minigame is solved.
    /// </summary>
    void NotifyUnlocked(GameObject interactorRoot);
}

}
