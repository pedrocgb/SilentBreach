using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.Missions
{

/// <summary>
/// Defines an independent scene target operated by the shared cut-wire minigame.
/// </summary>
public interface ICutWireSessionTarget
{
    CutWireMinigameDefinition Definition { get; }
    IReadOnlyList<bool> CutStates { get; }

    /// <summary>
    /// Persists a newly cut wire immediately so manual panel closure never resets progress.
    /// </summary>
    void NotifyWireCut(int wireIndex);

    /// <summary>
    /// Applies the successful minigame outcome to this target.
    /// </summary>
    void NotifySucceeded(GameObject interactorRoot);

    /// <summary>
    /// Applies the failed minigame outcome to this target.
    /// </summary>
    void NotifyFailed(GameObject interactorRoot);
}

}
