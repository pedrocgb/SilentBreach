using UnityEngine;

namespace Breezeblocks.Missions
{

public interface IInputCodeSessionTarget
{
    InputCodeMinigameDefinition Definition { get; }
    int RemainingAttempts { get; }

    /// <summary>
    /// Consumes one failed attempt and returns the remaining attempt count.
    /// </summary>
    int ConsumeFailedAttempt(GameObject interactorRoot);

    /// <summary>
    /// Notifies the target that the correct code was submitted.
    /// </summary>
    void NotifySucceeded(GameObject interactorRoot);

    /// <summary>
    /// Notifies the target that no attempts remain.
    /// </summary>
    void NotifyFailed(GameObject interactorRoot);
}

}
