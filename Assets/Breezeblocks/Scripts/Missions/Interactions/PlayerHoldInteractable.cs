using UnityEngine;

namespace Breezeblocks.Missions
{

/// <summary>
/// Defines an interactable that remains active while the interact button is held.
/// </summary>
public interface IPlayerHoldInteractable
{
    /// <summary>
    /// Attempts to start a held interaction for the supplied interactor.
    /// </summary>
    bool TryBeginHold(GameObject interactorRoot);

    /// <summary>
    /// Ticks an active held interaction while the interact button remains pressed.
    /// </summary>
    void TickHold(GameObject interactorRoot, float deltaTime);

    /// <summary>
    /// Ends the active held interaction for the supplied interactor.
    /// </summary>
    void EndHold(GameObject interactorRoot);
}

}
