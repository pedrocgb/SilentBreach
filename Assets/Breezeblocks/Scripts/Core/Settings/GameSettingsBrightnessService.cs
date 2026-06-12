using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Breezeblocks.Settings
{

/// <summary>
/// Applies saved brightness through the existing scene post-processing volume.
/// </summary>
public static class GameSettingsBrightnessService
{
    private const float MinimumExposure = -2f;
    private const float MaximumExposure = 2f;

    private static Volume brightnessVolume;
    private static ColorAdjustments brightnessColorAdjustments;

    /// <summary>
    /// Clears scene-specific cached volume references.
    /// </summary>
    public static void Reset()
    {
        brightnessVolume = null;
        brightnessColorAdjustments = null;
    }

    /// <summary>
    /// Applies the saved brightness through a cached ColorAdjustments override.
    /// </summary>
    public static void Apply(GameSettingsSaveData settings)
    {
        if (!Application.isPlaying || settings == null)
            return;

        if (brightnessVolume == null)
            brightnessVolume = PlayerSceneReferenceUtility.FindPlayerVolume(null);

        VolumeProfile profile = brightnessVolume != null ? brightnessVolume.profile : null;
        if (profile == null)
            return;

        if (brightnessColorAdjustments == null && !profile.TryGet(out brightnessColorAdjustments))
            brightnessColorAdjustments = profile.Add<ColorAdjustments>(true);

        brightnessColorAdjustments.active = true;
        brightnessColorAdjustments.postExposure.overrideState = true;
        brightnessColorAdjustments.postExposure.value = Mathf.Lerp(
            MinimumExposure,
            MaximumExposure,
            Mathf.Clamp01(settings.Brightness * 0.5f));
    }
}

}
