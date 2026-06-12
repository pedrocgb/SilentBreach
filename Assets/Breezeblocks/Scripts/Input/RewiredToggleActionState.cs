using System;
using System.Collections.Generic;
using Breezeblocks.Settings;
using Rewired;
using UnityEngine;

namespace Breezeblocks.Input
{

/// <summary>
/// Evaluates configured toggle actions once per frame so multiple gameplay consumers share one state.
/// </summary>
public static class RewiredToggleActionState
{
    private sealed class PlayerToggleState
    {
        public int LastEvaluatedFrame = -1;
        public bool IsActive;
    }

    private static readonly Dictionary<int, PlayerToggleState> AimStatesByPlayer = new();

    /// <summary>
    /// Resolves a held action using its configured hold or toggle behavior.
    /// </summary>
    public static bool GetButton(Player player, string actionName)
    {
        if (player == null || string.IsNullOrWhiteSpace(actionName))
            return false;

        if (!GameSettingsRuntime.ToggleAimEnabled ||
            !string.Equals(actionName, GameSettingsRuntime.AimActionName, StringComparison.Ordinal))
        {
            return player.GetButton(actionName);
        }

        if (!AimStatesByPlayer.TryGetValue(player.id, out PlayerToggleState state))
        {
            state = new PlayerToggleState();
            AimStatesByPlayer.Add(player.id, state);
        }

        if (state.LastEvaluatedFrame != Time.frameCount)
        {
            state.LastEvaluatedFrame = Time.frameCount;
            if (player.GetButtonDown(actionName))
                state.IsActive = !state.IsActive;
        }

        return state.IsActive;
    }

    /// <summary>
    /// Clears shared toggle state for an action after its mode changes.
    /// </summary>
    public static void Reset(string actionName)
    {
        if (string.Equals(actionName, GameSettingsRuntime.AimActionName, StringComparison.Ordinal))
            AimStatesByPlayer.Clear();
    }
}

}
