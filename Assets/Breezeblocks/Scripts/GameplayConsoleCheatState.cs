using System;
using UnityEngine;

public static class GameplayConsoleCheatState
{
    public static event Action StateChanged;

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
        StateChanged = null;
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

    private static void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
