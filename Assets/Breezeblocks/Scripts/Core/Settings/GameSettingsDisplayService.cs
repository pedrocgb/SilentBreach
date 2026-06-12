using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.Settings
{

/// <summary>
/// Resolves connected displays and applies monitor, resolution, VSync, and frame-rate preferences.
/// </summary>
public static class GameSettingsDisplayService
{
    private static readonly List<DisplayInfo> Displays = new();
    private static int startupMonitorIndex = -1;
    private static int startupResolutionWidth;
    private static int startupResolutionHeight;
    private static GameSettingsSaveData pendingSettings;

    /// <summary>
    /// Captures the monitor and resolution used when the game opened.
    /// </summary>
    public static void Initialize(GameSettingsSaveData settings)
    {
        RefreshDisplayLayout();
        startupMonitorIndex = FindCurrentMonitorIndex();
        startupResolutionWidth = Mathf.Max(1, Screen.width);
        startupResolutionHeight = Mathf.Max(1, Screen.height);
        ResolveInitialSelection(settings);
    }

    /// <summary>
    /// Clears cached startup and asynchronous display state.
    /// </summary>
    public static void Reset()
    {
        Displays.Clear();
        startupMonitorIndex = -1;
        startupResolutionWidth = 0;
        startupResolutionHeight = 0;
        pendingSettings = null;
    }

    /// <summary>
    /// Copies the current connected-display layout into a reusable caller list.
    /// </summary>
    public static void GetDisplayLayout(List<DisplayInfo> results)
    {
        if (results == null)
            return;

        results.Clear();
        Screen.GetDisplayLayout(results);
    }

    /// <summary>
    /// Copies supported full-screen resolutions for one monitor into a reusable caller list.
    /// </summary>
    public static void GetResolutionsForMonitor(int monitorIndex, List<Resolution> results)
    {
        if (results == null)
            return;

        results.Clear();
        RefreshDisplayLayout();
        if (monitorIndex >= 0 &&
            monitorIndex < Displays.Count &&
            TryGetDisplayResolutions(Displays[monitorIndex], out Resolution[] monitorResolutions))
        {
            results.AddRange(monitorResolutions);
            return;
        }

        Resolution[] fallbackResolutions = Screen.resolutions;
        if (fallbackResolutions != null && fallbackResolutions.Length > 0)
            results.AddRange(fallbackResolutions);

        if (results.Count <= 0)
            results.Add(Screen.currentResolution);
    }

    /// <summary>
    /// Clamps a selected monitor index and records its stable display name.
    /// </summary>
    public static void SetMonitorIdentity(GameSettingsSaveData settings, int monitorIndex)
    {
        if (settings == null)
            return;

        RefreshDisplayLayout();
        settings.MonitorIndex = Displays.Count > 0 ? Mathf.Clamp(monitorIndex, 0, Displays.Count - 1) : -1;
        settings.MonitorName = settings.MonitorIndex >= 0 && settings.MonitorIndex < Displays.Count
            ? Displays[settings.MonitorIndex].name ?? string.Empty
            : string.Empty;

        if (settings.MonitorIndex >= 0 && settings.MonitorIndex < Displays.Count &&
            !SupportsResolution(Displays[settings.MonitorIndex], settings.ResolutionWidth, settings.ResolutionHeight))
        {
            settings.ResolutionWidth = Mathf.Max(1, Displays[settings.MonitorIndex].width);
            settings.ResolutionHeight = Mathf.Max(1, Displays[settings.MonitorIndex].height);
        }
    }

    /// <summary>
    /// Applies monitor movement and then applies the selected resolution.
    /// </summary>
    public static void ApplyDisplaySettings(GameSettingsSaveData settings)
    {
        if (settings == null)
            return;

        pendingSettings = settings;
        RefreshDisplayLayout();
        if (settings.MonitorIndex >= 0 && settings.MonitorIndex < Displays.Count)
        {
            DisplayInfo targetDisplay = Displays[settings.MonitorIndex];
            AsyncOperation moveOperation = Screen.MoveMainWindowTo(targetDisplay, Vector2Int.zero);
            if (moveOperation != null)
            {
                moveOperation.completed += HandleWindowMoveCompleted;
                return;
            }
        }

        ApplyResolution(settings);
    }

    /// <summary>
    /// Applies the selected resolution without changing monitors.
    /// </summary>
    public static void ApplyResolution(GameSettingsSaveData settings)
    {
        if (settings != null && settings.ResolutionWidth > 0 && settings.ResolutionHeight > 0)
            Screen.SetResolution(settings.ResolutionWidth, settings.ResolutionHeight, Screen.fullScreenMode);
    }

    /// <summary>
    /// Applies VSync and target frame rate together.
    /// </summary>
    public static void ApplyFrameRateSettings(GameSettingsSaveData settings)
    {
        if (settings == null)
            return;

        QualitySettings.vSyncCount = settings.VSyncEnabled ? 1 : 0;
        Application.targetFrameRate = settings.FrameRateLimit;
    }

    /// <summary>
    /// Resolves saved display identity against currently connected monitors.
    /// </summary>
    public static void ResolveInitialSelection(GameSettingsSaveData settings)
    {
        if (settings == null)
            return;

        RefreshDisplayLayout();
        if (!string.IsNullOrWhiteSpace(settings.MonitorName))
        {
            for (int i = 0; i < Displays.Count; i++)
            {
                if (string.Equals(Displays[i].name, settings.MonitorName, StringComparison.Ordinal))
                {
                    settings.MonitorIndex = i;
                    break;
                }
            }
        }

        if (settings.MonitorIndex < 0 || settings.MonitorIndex >= Displays.Count)
            settings.MonitorIndex = startupMonitorIndex >= 0 ? startupMonitorIndex : FindCurrentMonitorIndex();

        settings.MonitorName = settings.MonitorIndex >= 0 && settings.MonitorIndex < Displays.Count
            ? Displays[settings.MonitorIndex].name ?? string.Empty
            : string.Empty;

        if (settings.ResolutionWidth <= 0 || settings.ResolutionHeight <= 0)
        {
            settings.ResolutionWidth = startupResolutionWidth > 0 ? startupResolutionWidth : Mathf.Max(1, Screen.width);
            settings.ResolutionHeight = startupResolutionHeight > 0 ? startupResolutionHeight : Mathf.Max(1, Screen.height);
        }
    }

    /// <summary>
    /// Refreshes the cached connected-display list.
    /// </summary>
    private static void RefreshDisplayLayout()
    {
        Displays.Clear();
        Screen.GetDisplayLayout(Displays);
    }

    /// <summary>
    /// Finds the connected-display index currently containing the main window.
    /// </summary>
    private static int FindCurrentMonitorIndex()
    {
        DisplayInfo currentDisplay = Screen.mainWindowDisplayInfo;
        for (int i = 0; i < Displays.Count; i++)
        {
            DisplayInfo display = Displays[i];
            if (display.name == currentDisplay.name &&
                display.width == currentDisplay.width &&
                display.height == currentDisplay.height)
            {
                return i;
            }
        }

        return Displays.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Checks whether a display supports a selected full-screen resolution.
    /// </summary>
    private static bool SupportsResolution(DisplayInfo display, int width, int height)
    {
        if (!TryGetDisplayResolutions(display, out Resolution[] supportedResolutions))
            return display.width == width && display.height == height;

        for (int i = 0; i < supportedResolutions.Length; i++)
        {
            if (supportedResolutions[i].width == width && supportedResolutions[i].height == height)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Safely reads per-monitor resolutions on platforms that implement the optional API.
    /// </summary>
    private static bool TryGetDisplayResolutions(DisplayInfo display, out Resolution[] resolutions)
    {
        try
        {
            resolutions = display.resolutions;
            return resolutions != null && resolutions.Length > 0;
        }
        catch (NotSupportedException)
        {
            resolutions = Array.Empty<Resolution>();
            return false;
        }
    }

    /// <summary>
    /// Applies the pending resolution after an asynchronous monitor move completes.
    /// </summary>
    private static void HandleWindowMoveCompleted(AsyncOperation operation)
    {
        ApplyResolution(pendingSettings);
    }
}

}
