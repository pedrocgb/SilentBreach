using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class GameplayConsoleCheatState
{
    public static event Action StateChanged;

    private static readonly Dictionary<string, int> CommandUseCounts = new(StringComparer.OrdinalIgnoreCase);

    public static bool GodMode { get; private set; }
    public static bool Invisible { get; private set; }
    public static bool Lightfooted { get; private set; }
    public static bool InfiniteReserveAmmo { get; private set; }
    public static bool FocusMode { get; private set; }
    public static bool AthleteMode { get; private set; }
    public static bool GhostMode { get; private set; }
    public static bool MedusaMode { get; private set; }
    public static bool LetThereBeLight { get; private set; }
    public static bool LetThereBeLightOverrideInitialized { get; private set; }
    public static bool NoFailures { get; private set; }
    public static bool InstantLockpicking { get; private set; }
    public static bool InfiniteLockpicks { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        GodMode = false;
        Invisible = false;
        Lightfooted = false;
        InfiniteReserveAmmo = false;
        FocusMode = false;
        AthleteMode = false;
        GhostMode = false;
        MedusaMode = false;
        LetThereBeLight = false;
        LetThereBeLightOverrideInitialized = false;
        NoFailures = false;
        InstantLockpicking = false;
        InfiniteLockpicks = false;
        CommandUseCounts.Clear();
        StateChanged = null;
    }

    public static void ResetRuntimeState()
    {
        ResetState();
    }

    public static void SetGodMode(bool enabled)
    {
        if (GodMode == enabled)
            return;

        GodMode = enabled;
        NotifyStateChanged();
    }

    public static void SetInvisible(bool enabled)
    {
        if (Invisible == enabled)
            return;

        Invisible = enabled;
        NotifyStateChanged();
    }

    public static void SetLightfooted(bool enabled)
    {
        if (Lightfooted == enabled)
            return;

        Lightfooted = enabled;
        NotifyStateChanged();
    }

    public static void SetInfiniteReserveAmmo(bool enabled)
    {
        if (InfiniteReserveAmmo == enabled)
            return;

        InfiniteReserveAmmo = enabled;
        NotifyStateChanged();
    }

    public static void SetFocusMode(bool enabled)
    {
        if (FocusMode == enabled)
            return;

        FocusMode = enabled;
        NotifyStateChanged();
    }

    public static void SetAthleteMode(bool enabled)
    {
        if (AthleteMode == enabled)
            return;

        AthleteMode = enabled;
        NotifyStateChanged();
    }

    public static void SetGhostMode(bool enabled)
    {
        if (GhostMode == enabled)
            return;

        GhostMode = enabled;
        NotifyStateChanged();
    }

    public static void SetMedusaMode(bool enabled)
    {
        if (MedusaMode == enabled)
            return;

        MedusaMode = enabled;
        NotifyStateChanged();
    }

    public static void SetLetThereBeLight(bool enabled)
    {
        if (LetThereBeLightOverrideInitialized && LetThereBeLight == enabled)
            return;

        LetThereBeLight = enabled;
        LetThereBeLightOverrideInitialized = true;
        NotifyStateChanged();
    }

    public static void SetNoFailures(bool enabled)
    {
        if (NoFailures == enabled)
            return;

        NoFailures = enabled;
        NotifyStateChanged();
    }

    public static void SetInstantLockpicking(bool enabled)
    {
        if (InstantLockpicking == enabled)
            return;

        InstantLockpicking = enabled;
        NotifyStateChanged();
    }

    public static void SetInfiniteLockpicks(bool enabled)
    {
        if (InfiniteLockpicks == enabled)
            return;

        InfiniteLockpicks = enabled;
        NotifyStateChanged();
    }

    public static void RegisterCommandUse(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            return;

        string normalizedCommandName = commandName.Trim().ToLowerInvariant();
        CommandUseCounts.TryGetValue(normalizedCommandName, out int currentCount);
        CommandUseCounts[normalizedCommandName] = currentCount + 1;
    }

    public static string BuildActiveCheatsReport()
    {
        StringBuilder builder = new();
        AppendActiveCheat(builder, "god_mode", GodMode);
        AppendActiveCheat(builder, "invisible", Invisible);
        AppendActiveCheat(builder, "lightfooted", Lightfooted);
        AppendActiveCheat(builder, "noclip", InfiniteReserveAmmo);
        AppendActiveCheat(builder, "focus_mode", FocusMode);
        AppendActiveCheat(builder, "athlete_mode", AthleteMode);
        AppendActiveCheat(builder, "ghost_mode", GhostMode);
        AppendActiveCheat(builder, "medusa_mode", MedusaMode);
        AppendActiveCheat(builder, "let_there_be_light", LetThereBeLightOverrideInitialized && LetThereBeLight);
        AppendActiveCheat(builder, "no_failures", NoFailures);
        AppendActiveCheat(builder, "instant_lockpicking", InstantLockpicking);
        AppendActiveCheat(builder, "infinite_lockpicks", InfiniteLockpicks);
        return builder.Length > 0 ? builder.ToString() : "none";
    }

    public static string BuildUsedCommandsReport()
    {
        if (CommandUseCounts.Count == 0)
            return "none";

        StringBuilder builder = new();
        foreach (KeyValuePair<string, int> pair in CommandUseCounts)
        {
            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(pair.Key);
            builder.Append(" x");
            builder.Append(pair.Value);
        }

        return builder.ToString();
    }

    private static void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private static void AppendActiveCheat(StringBuilder builder, string commandName, bool active)
    {
        if (!active)
            return;

        if (builder.Length > 0)
            builder.Append(", ");

        builder.Append(commandName);
    }
}
