using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.Settings
{

public enum GameFrameRateLimit
{
    Unlimited = -1,
    Fps30 = 30,
    Fps60 = 60,
    Fps120 = 120,
    Fps144 = 144
}

[Serializable]
public sealed class RewiredControllerMapSaveData
{
    public int ControllerType;
    public int ControllerId;
    public string MapJson = string.Empty;

    /// <summary>
    /// Creates a detached copy suitable for save snapshots.
    /// </summary>
    public RewiredControllerMapSaveData Clone()
    {
        return new RewiredControllerMapSaveData
        {
            ControllerType = ControllerType,
            ControllerId = ControllerId,
            MapJson = MapJson ?? string.Empty
        };
    }
}

[Serializable]
public sealed class GameSettingsSaveData
{
    public const string DefaultLanguageCode = "pt-BR";

    public int MonitorIndex = -1;
    public string MonitorName = string.Empty;
    public int ResolutionWidth;
    public int ResolutionHeight;
    public bool VSyncEnabled;
    public float Brightness = 1f;
    public bool ScreenshakeEnabled = true;
    public int FrameRateLimit = (int)GameFrameRateLimit.Unlimited;
    public float MasterVolume = 100f;
    public float MusicVolume = 80f;
    public float SfxVolume = 100f;
    public float UiVolume = 100f;
    public float AmbientVolume = 80f;
    public float MouseSensitivity = 1f;
    public float AimSensitivity = 1f;
    public bool ToggleAim;
    public bool ToggleSprint;
    public bool ToggleDragBody;
    public string LanguageCode = DefaultLanguageCode;
    public List<RewiredControllerMapSaveData> RewiredControllerMaps = new();

    /// <summary>
    /// Creates a new settings object populated with game defaults.
    /// </summary>
    public static GameSettingsSaveData CreateDefaults()
    {
        return new GameSettingsSaveData();
    }

    /// <summary>
    /// Creates a detached copy so save and runtime state cannot mutate each other.
    /// </summary>
    public GameSettingsSaveData Clone()
    {
        GameSettingsSaveData clone = new()
        {
            MonitorIndex = MonitorIndex,
            MonitorName = MonitorName ?? string.Empty,
            ResolutionWidth = ResolutionWidth,
            ResolutionHeight = ResolutionHeight,
            VSyncEnabled = VSyncEnabled,
            Brightness = Brightness,
            ScreenshakeEnabled = ScreenshakeEnabled,
            FrameRateLimit = FrameRateLimit,
            MasterVolume = MasterVolume,
            MusicVolume = MusicVolume,
            SfxVolume = SfxVolume,
            UiVolume = UiVolume,
            AmbientVolume = AmbientVolume,
            MouseSensitivity = MouseSensitivity,
            AimSensitivity = AimSensitivity,
            ToggleAim = ToggleAim,
            ToggleSprint = ToggleSprint,
            ToggleDragBody = ToggleDragBody,
            LanguageCode = LanguageCode ?? DefaultLanguageCode,
            RewiredControllerMaps = new List<RewiredControllerMapSaveData>()
        };

        if (RewiredControllerMaps == null)
            return clone;

        for (int i = 0; i < RewiredControllerMaps.Count; i++)
        {
            RewiredControllerMapSaveData map = RewiredControllerMaps[i];
            if (map != null && !string.IsNullOrWhiteSpace(map.MapJson))
                clone.RewiredControllerMaps.Add(map.Clone());
        }

        return clone;
    }

    /// <summary>
    /// Clamps persisted values and repairs missing collections after loading.
    /// </summary>
    public void Sanitize()
    {
        MonitorIndex = Mathf.Max(-1, MonitorIndex);
        MonitorName = MonitorName != null ? MonitorName.Trim() : string.Empty;
        ResolutionWidth = Mathf.Max(0, ResolutionWidth);
        ResolutionHeight = Mathf.Max(0, ResolutionHeight);
        Brightness = Mathf.Clamp(Brightness, 0f, 2f);
        FrameRateLimit = SanitizeFrameRate(FrameRateLimit);
        MasterVolume = Mathf.Clamp(MasterVolume, 0f, 100f);
        MusicVolume = Mathf.Clamp(MusicVolume, 0f, 100f);
        SfxVolume = Mathf.Clamp(SfxVolume, 0f, 100f);
        UiVolume = Mathf.Clamp(UiVolume, 0f, 100f);
        AmbientVolume = Mathf.Clamp(AmbientVolume, 0f, 100f);
        MouseSensitivity = Mathf.Clamp(MouseSensitivity, 0f, 2f);
        AimSensitivity = Mathf.Clamp(AimSensitivity, 0f, 2f);
        LanguageCode = string.IsNullOrWhiteSpace(LanguageCode) ? DefaultLanguageCode : LanguageCode.Trim();
        RewiredControllerMaps ??= new List<RewiredControllerMapSaveData>();
    }

    /// <summary>
    /// Restricts frame-rate values to options exposed by the settings menu.
    /// </summary>
    private static int SanitizeFrameRate(int frameRate)
    {
        return frameRate switch
        {
            (int)GameFrameRateLimit.Fps30 => frameRate,
            (int)GameFrameRateLimit.Fps60 => frameRate,
            (int)GameFrameRateLimit.Fps120 => frameRate,
            (int)GameFrameRateLimit.Fps144 => frameRate,
            _ => (int)GameFrameRateLimit.Unlimited
        };
    }
}

}
