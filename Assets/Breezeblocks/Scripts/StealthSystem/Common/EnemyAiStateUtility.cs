/// <summary>
/// Provides shared enemy awareness-state classification helpers.
/// </summary>
internal static class EnemyAiStateUtility
{
    /// <summary>
    /// Returns whether the state represents active combat awareness.
    /// </summary>
    public static bool IsCombatAwarenessState(EnemyState state)
    {
        return state == EnemyState.Detected || state == EnemyState.Alert;
    }

    /// <summary>
    /// Returns whether the state should keep a weapon readied.
    /// </summary>
    public static bool RequiresReadiedWeapon(EnemyState state)
    {
        return state == EnemyState.Suspicious ||
               state == EnemyState.Searching ||
               state == EnemyState.Alert ||
               state == EnemyState.Detected;
    }

    /// <summary>
    /// Returns whether the state is part of the calm traversal set.
    /// </summary>
    public static bool IsCalmState(EnemyState state)
    {
        return state == EnemyState.Idle ||
               state == EnemyState.Patrol ||
               state == EnemyState.ReturningToStart;
    }
}
