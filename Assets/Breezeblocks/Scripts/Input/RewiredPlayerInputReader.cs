using Rewired;
using UnityEngine;

namespace Breezeblocks.Input
{

/// <summary>
/// Provides action-based player input reads without exposing Rewired directly to gameplay consumers.
/// </summary>
public interface IPlayerInputReader
{
    /// <summary>
    /// Gets whether the configured Rewired player is currently available for reads.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Reads a named digital action as a held button state.
    /// </summary>
    bool GetButton(string actionName);

    /// <summary>
    /// Reads a named digital action as a button-down edge for the current frame.
    /// </summary>
    bool GetButtonDown(string actionName);

    /// <summary>
    /// Reads a named digital action as a button-up edge for the current frame.
    /// </summary>
    bool GetButtonUp(string actionName);

    /// <summary>
    /// Reads a named analog action as a float axis value.
    /// </summary>
    float GetAxis(string actionName);

    /// <summary>
    /// Reads two named analog actions as a combined 2D vector.
    /// </summary>
    Vector2 GetAxis2D(string horizontalActionName, string verticalActionName);
}

/// <summary>
/// Provides pointer-related input reads through Rewired instead of Unity's direct input APIs.
/// </summary>
public interface IPointerInputReader
{
    /// <summary>
    /// Attempts to read the current pointer screen position.
    /// </summary>
    bool TryGetScreenPosition(out Vector2 screenPosition);

    /// <summary>
    /// Gets the current pointer screen position or the screen center when the pointer is unavailable.
    /// </summary>
    Vector2 GetScreenPositionOrDefault();
}

/// <summary>
/// Wraps Rewired player and mouse access behind a small gameplay-facing input abstraction.
/// </summary>
public sealed class RewiredPlayerInputReader : IPlayerInputReader, IPointerInputReader
{
    private readonly int rewiredPlayerId;
    private Player rewiredPlayer;

    /// <summary>
    /// Creates a new reader for the specified Rewired player id.
    /// </summary>
    public RewiredPlayerInputReader(int rewiredPlayerId = -1)
    {
        this.rewiredPlayerId = rewiredPlayerId;
    }

    /// <summary>
    /// Gets whether the configured Rewired player can currently be resolved.
    /// </summary>
    public bool IsReady => TryResolvePlayer();

    /// <summary>
    /// Reads a held button state from the configured Rewired player.
    /// </summary>
    public bool GetButton(string actionName)
    {
        return TryResolvePlayer() &&
               !string.IsNullOrWhiteSpace(actionName) &&
               RewiredToggleActionState.GetButton(rewiredPlayer, actionName);
    }

    /// <summary>
    /// Reads a button-down edge from the configured Rewired player.
    /// </summary>
    public bool GetButtonDown(string actionName)
    {
        return TryResolvePlayer() && !string.IsNullOrWhiteSpace(actionName) && rewiredPlayer.GetButtonDown(actionName);
    }

    /// <summary>
    /// Reads a button-up edge from the configured Rewired player.
    /// </summary>
    public bool GetButtonUp(string actionName)
    {
        return TryResolvePlayer() && !string.IsNullOrWhiteSpace(actionName) && rewiredPlayer.GetButtonUp(actionName);
    }

    /// <summary>
    /// Reads an analog axis value from the configured Rewired player.
    /// </summary>
    public float GetAxis(string actionName)
    {
        return TryResolvePlayer() && !string.IsNullOrWhiteSpace(actionName) ? rewiredPlayer.GetAxis(actionName) : 0f;
    }

    /// <summary>
    /// Reads a combined 2D axis value from the configured Rewired player.
    /// </summary>
    public Vector2 GetAxis2D(string horizontalActionName, string verticalActionName)
    {
        if (!TryResolvePlayer() ||
            string.IsNullOrWhiteSpace(horizontalActionName) ||
            string.IsNullOrWhiteSpace(verticalActionName))
        {
            return Vector2.zero;
        }

        return rewiredPlayer.GetAxis2D(horizontalActionName, verticalActionName);
    }

    /// <summary>
    /// Attempts to read the current hardware mouse screen position through Rewired.
    /// </summary>
    public bool TryGetScreenPosition(out Vector2 screenPosition)
    {
        if (!ReInput.isReady || ReInput.controllers == null || ReInput.controllers.Mouse == null || !ReInput.controllers.Mouse.enabled)
        {
            screenPosition = ResolveDefaultScreenPosition();
            return false;
        }

        screenPosition = ReInput.controllers.Mouse.screenPosition;
        return true;
    }

    /// <summary>
    /// Returns the current pointer position or falls back to the center of the screen.
    /// </summary>
    public Vector2 GetScreenPositionOrDefault()
    {
        return TryGetScreenPosition(out Vector2 screenPosition)
            ? screenPosition
            : ResolveDefaultScreenPosition();
    }

    /// <summary>
    /// Resolves and caches the Rewired player before gameplay reads are attempted.
    /// </summary>
    private bool TryResolvePlayer()
    {
        if (rewiredPlayerId < 0)
            return false;

        if (!ReInput.isReady)
        {
            rewiredPlayer = null;
            return false;
        }

        rewiredPlayer ??= ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }

    /// <summary>
    /// Calculates a stable fallback pointer position when no Rewired mouse is available.
    /// </summary>
    private static Vector2 ResolveDefaultScreenPosition()
    {
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }
}

}
