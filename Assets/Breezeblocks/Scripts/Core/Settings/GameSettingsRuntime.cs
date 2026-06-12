using System;
using System.Collections.Generic;
using Breezeblocks.HideoutSystem;
using Rewired;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breezeblocks.Settings
{

/// <summary>
/// Owns persisted player preferences and applies them independently of scene lifetime.
/// </summary>
public static class GameSettingsRuntime
{
    public const int DefaultRewiredPlayerId = 1;
    public const string AimActionName = "Aim";

    private static GameSettingsSaveData current = GameSettingsSaveData.CreateDefaults();
    private static bool initialized;

    public static event Action SettingsChanged;

    public static GameSettingsSaveData Current
    {
        get
        {
            EnsureInitialized();
            return current;
        }
    }

    public static bool IsInitialized => initialized;
    public static bool ScreenshakeEnabled => Current.ScreenshakeEnabled;
    public static bool ToggleAimEnabled => Current.ToggleAim;
    public static float MouseSensitivity => Current.MouseSensitivity;
    public static float AimSensitivity => Current.AimSensitivity;

    /// <summary>
    /// Resets static state when entering play mode without a domain reload.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ReInput.InitializedEvent -= HandleRewiredInitialized;
        initialized = false;
        current = GameSettingsSaveData.CreateDefaults();
        GameSettingsDisplayService.Reset();
        GameSettingsBrightnessService.Reset();
        GameSettingsRewiredMapService.Reset();
        SettingsChanged = null;
    }

    /// <summary>
    /// Loads and applies settings before the first scene begins.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeBeforeFirstScene()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Ensures preferences are loaded exactly once and runtime integrations are registered.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;
        current = HideoutSaveSystem.TryLoad(out HideoutSaveSnapshot snapshot)
            ? snapshot.Settings?.Clone() ?? GameSettingsSaveData.CreateDefaults()
            : GameSettingsSaveData.CreateDefaults();

        current.Sanitize();
        GameSettingsDisplayService.Initialize(current);

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ReInput.InitializedEvent -= HandleRewiredInitialized;
        ReInput.InitializedEvent += HandleRewiredInitialized;

        GameSettingsDisplayService.ApplyDisplaySettings(current);
        GameSettingsDisplayService.ApplyFrameRateSettings(current);
        ApplyToGlobalSettings();
        GameSettingsRewiredMapService.TryApply(current, DefaultRewiredPlayerId);
    }

    /// <summary>
    /// Returns a detached settings copy for persistence by another system.
    /// </summary>
    public static GameSettingsSaveData ExportSaveData()
    {
        return Current.Clone();
    }

    /// <summary>
    /// Copies the current display layout into the supplied reusable list.
    /// </summary>
    public static void GetDisplayLayout(List<DisplayInfo> results)
    {
        if (results == null)
            return;

        GameSettingsDisplayService.GetDisplayLayout(results);
    }

    /// <summary>
    /// Copies supported full-screen resolutions for a monitor into the supplied reusable list.
    /// </summary>
    public static void GetResolutionsForMonitor(int monitorIndex, List<Resolution> results)
    {
        if (results == null)
            return;

        GameSettingsDisplayService.GetResolutionsForMonitor(monitorIndex, results);
    }

    /// <summary>
    /// Applies a selected monitor and saves the preference immediately.
    /// </summary>
    public static void SetMonitor(int monitorIndex)
    {
        int previousIndex = current.MonitorIndex;
        int previousWidth = current.ResolutionWidth;
        int previousHeight = current.ResolutionHeight;
        GameSettingsDisplayService.SetMonitorIdentity(current, monitorIndex);
        if (current.MonitorIndex == previousIndex &&
            current.ResolutionWidth == previousWidth &&
            current.ResolutionHeight == previousHeight)
        {
            return;
        }

        GameSettingsDisplayService.ApplyDisplaySettings(current);
        Commit();
    }

    /// <summary>
    /// Applies a selected full-screen resolution and saves it immediately.
    /// </summary>
    public static void SetResolution(int width, int height)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        if (current.ResolutionWidth == width && current.ResolutionHeight == height)
            return;

        current.ResolutionWidth = width;
        current.ResolutionHeight = height;
        GameSettingsDisplayService.ApplyResolution(current);
        Commit();
    }

    /// <summary>
    /// Applies vertical synchronization and saves it immediately.
    /// </summary>
    public static void SetVSync(bool enabled)
    {
        if (current.VSyncEnabled == enabled)
            return;

        current.VSyncEnabled = enabled;
        GameSettingsDisplayService.ApplyFrameRateSettings(current);
        Commit();
    }

    /// <summary>
    /// Applies post-processing brightness and saves it immediately.
    /// </summary>
    public static void SetBrightness(float value)
    {
        value = Mathf.Clamp(value, 0f, 2f);
        if (Mathf.Approximately(current.Brightness, value))
            return;

        current.Brightness = value;
        GameSettingsBrightnessService.Apply(current);
        Commit();
    }

    /// <summary>
    /// Enables or disables all registered screenshake playback and saves it immediately.
    /// </summary>
    public static void SetScreenshake(bool enabled)
    {
        if (current.ScreenshakeEnabled == enabled)
            return;

        current.ScreenshakeEnabled = enabled;
        Commit();
    }

    /// <summary>
    /// Applies a supported target frame rate and saves it immediately.
    /// </summary>
    public static void SetFrameRateLimit(GameFrameRateLimit frameRateLimit)
    {
        int value = (int)frameRateLimit;
        if (current.FrameRateLimit == value)
            return;

        current.FrameRateLimit = value;
        current.Sanitize();
        GameSettingsDisplayService.ApplyFrameRateSettings(current);
        Commit();
    }

    /// <summary>
    /// Updates the master mixer volume percentage and saves it immediately.
    /// </summary>
    public static void SetMasterVolume(float value)
    {
        SetVolume(ref current.MasterVolume, value);
    }

    /// <summary>
    /// Updates the music mixer volume percentage and saves it immediately.
    /// </summary>
    public static void SetMusicVolume(float value)
    {
        SetVolume(ref current.MusicVolume, value);
    }

    /// <summary>
    /// Updates the sound-effects mixer volume percentage and saves it immediately.
    /// </summary>
    public static void SetSfxVolume(float value)
    {
        SetVolume(ref current.SfxVolume, value);
    }

    /// <summary>
    /// Updates the UI mixer volume percentage and saves it immediately.
    /// </summary>
    public static void SetUiVolume(float value)
    {
        SetVolume(ref current.UiVolume, value);
    }

    /// <summary>
    /// Updates the ambient mixer volume percentage and saves it immediately.
    /// </summary>
    public static void SetAmbientVolume(float value)
    {
        SetVolume(ref current.AmbientVolume, value);
    }

    /// <summary>
    /// Updates the base mouse-look sensitivity multiplier and saves it immediately.
    /// </summary>
    public static void SetMouseSensitivity(float value)
    {
        SetSensitivity(ref current.MouseSensitivity, value);
    }

    /// <summary>
    /// Updates the aiming sensitivity multiplier and saves it immediately.
    /// </summary>
    public static void SetAimSensitivity(float value)
    {
        SetSensitivity(ref current.AimSensitivity, value);
    }

    /// <summary>
    /// Updates toggle-aim behavior and saves it immediately.
    /// </summary>
    public static void SetToggleAim(bool enabled)
    {
        if (current.ToggleAim == enabled)
            return;

        current.ToggleAim = enabled;
        Breezeblocks.Input.RewiredToggleActionState.Reset(AimActionName);
        Commit();
    }

    /// <summary>
    /// Updates toggle-sprint behavior and saves it immediately.
    /// </summary>
    public static void SetToggleSprint(bool enabled)
    {
        if (current.ToggleSprint == enabled)
            return;

        current.ToggleSprint = enabled;
        ApplyToGlobalSettings();
        Commit();
    }

    /// <summary>
    /// Updates toggle-drag behavior and saves it immediately.
    /// </summary>
    public static void SetToggleDragBody(bool enabled)
    {
        if (current.ToggleDragBody == enabled)
            return;

        current.ToggleDragBody = enabled;
        ApplyToGlobalSettings();
        Commit();
    }

    /// <summary>
    /// Updates the selected language identifier and saves it immediately.
    /// </summary>
    public static void SetLanguage(string languageCode)
    {
        string sanitizedCode = string.IsNullOrWhiteSpace(languageCode)
            ? GameSettingsSaveData.DefaultLanguageCode
            : languageCode.Trim();

        if (string.Equals(current.LanguageCode, sanitizedCode, StringComparison.Ordinal))
            return;

        current.LanguageCode = sanitizedCode;
        Commit();
    }

    /// <summary>
    /// Restores every preference and Rewired map to its configured default.
    /// </summary>
    public static void RestoreDefaults()
    {
        current = GameSettingsSaveData.CreateDefaults();
        GameSettingsDisplayService.ResolveInitialSelection(current);
        GameSettingsRewiredMapService.RestoreDefaults(current, DefaultRewiredPlayerId);
        ApplyAll();
        Commit(captureRewiredMaps: true);
    }

    /// <summary>
    /// Captures current Rewired keyboard and mouse maps and saves them immediately.
    /// </summary>
    public static void SaveCurrentRewiredMaps()
    {
        GameSettingsRewiredMapService.Capture(current, DefaultRewiredPlayerId);
        Commit();
    }

    /// <summary>
    /// Multiplies look smoothing by current base and optional aim sensitivity.
    /// </summary>
    public static float ResolveLookSmoothing(float baseSmoothing, bool isAiming)
    {
        float multiplier = Current.MouseSensitivity;
        if (isAiming)
            multiplier *= Current.AimSensitivity;

        return Mathf.Max(0f, baseSmoothing) * multiplier;
    }

    /// <summary>
    /// Reapplies settings that depend on the persistent GlobalSettings object.
    /// </summary>
    public static void ApplyToGlobalSettings()
    {
        if (!initialized || GlobalSettings.Instance == null)
            return;

        GlobalSettings.Instance.ApplyPlayerSettings(current);
    }

    /// <summary>
    /// Converts a zero-to-one-hundred slider value to the configured mixer decibel range.
    /// </summary>
    public static float VolumePercentToDecibels(float percentage)
    {
        return Mathf.Lerp(-80f, 0f, Mathf.Clamp01(percentage / 100f));
    }

    /// <summary>
    /// Reapplies scene-dependent settings after a scene finishes loading.
    /// </summary>
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        GameSettingsBrightnessService.Reset();
        GameSettingsBrightnessService.Apply(current);
        ApplyToGlobalSettings();
        GameSettingsRewiredMapService.TryApply(current, DefaultRewiredPlayerId);
    }

    /// <summary>
    /// Applies persisted maps as soon as Rewired finishes initializing.
    /// </summary>
    private static void HandleRewiredInitialized()
    {
        GameSettingsRewiredMapService.Reset();
        GameSettingsRewiredMapService.TryApply(current, DefaultRewiredPlayerId);
    }

    /// <summary>
    /// Applies all settings to their runtime systems.
    /// </summary>
    private static void ApplyAll()
    {
        GameSettingsDisplayService.ApplyDisplaySettings(current);
        GameSettingsDisplayService.ApplyFrameRateSettings(current);
        GameSettingsBrightnessService.Apply(current);
        ApplyToGlobalSettings();
        GameSettingsRewiredMapService.TryApply(current, DefaultRewiredPlayerId);
    }

    /// <summary>
    /// Updates one volume field and applies all audio settings through GlobalSettings.
    /// </summary>
    private static void SetVolume(ref float destination, float value)
    {
        value = Mathf.Clamp(value, 0f, 100f);
        if (Mathf.Approximately(destination, value))
            return;

        destination = value;
        ApplyToGlobalSettings();
        Commit();
    }

    /// <summary>
    /// Updates one sensitivity multiplier and saves it immediately.
    /// </summary>
    private static void SetSensitivity(ref float destination, float value)
    {
        value = Mathf.Clamp(value, 0f, 2f);
        if (Mathf.Approximately(destination, value))
            return;

        destination = value;
        Commit();
    }

    /// <summary>
    /// Persists current settings without discarding existing hideout progress.
    /// </summary>
    private static void Commit(bool captureRewiredMaps = false)
    {
        if (captureRewiredMaps)
            GameSettingsRewiredMapService.Capture(current, DefaultRewiredPlayerId);

        current.Sanitize();
        HideoutSaveSystem.TryLoad(out HideoutSaveSnapshot snapshot);
        snapshot ??= new HideoutSaveSnapshot();
        snapshot.Settings = current.Clone();
        HideoutSaveSystem.Save(snapshot);
        SettingsChanged?.Invoke();
    }
}

}
