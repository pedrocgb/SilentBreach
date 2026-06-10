using UnityEngine;

namespace Breezeblocks.Missions
{

/// <summary>
/// Defines an interactable that stays active across multiple frames until the player ends it.
/// </summary>
public interface IPlayerHoldInteractable
{
    /// <summary>
    /// Attempts to start a held interaction for the supplied interactor.
    /// </summary>
    bool TryBeginHold(GameObject interactorRoot);

    /// <summary>
    /// Returns whether the interaction is still active for the supplied interactor.
    /// </summary>
    bool IsHoldActive(GameObject interactorRoot);

    /// <summary>
    /// Ticks an active sustained interaction while it remains engaged.
    /// </summary>
    void TickHold(GameObject interactorRoot, float deltaTime);

    /// <summary>
    /// Ends the active sustained interaction for the supplied interactor.
    /// </summary>
    void EndHold(GameObject interactorRoot);
}

}
