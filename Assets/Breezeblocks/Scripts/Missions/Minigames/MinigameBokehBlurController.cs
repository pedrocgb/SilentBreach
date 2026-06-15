using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Breezeblocks.Missions
{

/// <summary>
/// Owns a temporary Bokeh depth-of-field override shared by UI minigames.
/// </summary>
public sealed class MinigameBokehBlurController
{
    private Volume targetVolume;
    private DepthOfField depthOfField;
    private Tween blurTween;
    private bool baseActive;
    private bool baseModeOverrideState;
    private bool baseFocusDistanceOverrideState;
    private bool baseApertureOverrideState;
    private bool baseFocalLengthOverrideState;
    private bool baseBladeCountOverrideState;
    private bool baseBladeCurvatureOverrideState;
    private bool baseBladeRotationOverrideState;
    private DepthOfFieldMode baseMode = DepthOfFieldMode.Off;
    private float baseFocusDistance = 10f;
    private float baseAperture = 5.6f;
    private float baseFocalLength = 50f;
    private int baseBladeCount = 5;
    private float baseBladeCurvature = 1f;
    private float baseBladeRotation;
    private float appliedStrength;
    private bool hasCachedBaseline;

    /// <summary>
    /// Animates the shared Bokeh blur toward its visible or restored state using unscaled time.
    /// </summary>
    public void Animate(
        bool blurred,
        bool immediate,
        Volume configuredVolume,
        GameObject context,
        float transitionDuration,
        float focusDistance,
        float aperture,
        float focalLength,
        int bladeCount,
        float bladeCurvature,
        float bladeRotation)
    {
        CacheBaseline(configuredVolume, context);
        if (depthOfField == null)
            return;

        KillTween();

        float targetStrength = blurred ? 1f : 0f;
        if (immediate || transitionDuration <= 0f)
        {
            ApplyStrength(targetStrength, focusDistance, aperture, focalLength, bladeCount, bladeCurvature, bladeRotation);
            return;
        }

        blurTween = DOVirtual
            .Float(
                Mathf.Clamp01(appliedStrength),
                targetStrength,
                transitionDuration,
                strength => ApplyStrength(strength, focusDistance, aperture, focalLength, bladeCount, bladeCurvature, bladeRotation))
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnComplete(() => blurTween = null);
    }

    /// <summary>
    /// Stops the active blur transition without modifying its currently applied value.
    /// </summary>
    public void KillTween()
    {
        blurTween?.Kill();
        blurTween = null;
    }

    /// <summary>
    /// Caches the target volume's original depth-of-field state before applying minigame blur.
    /// </summary>
    private void CacheBaseline(Volume configuredVolume, GameObject context)
    {
        if (hasCachedBaseline)
            return;

        targetVolume = configuredVolume != null
            ? configuredVolume
            : PlayerSceneReferenceUtility.FindPlayerVolume(context);
        if (targetVolume == null || targetVolume.profile == null)
            return;

        VolumeProfile runtimeVolumeProfile = targetVolume.profile;
        if (!runtimeVolumeProfile.TryGet(out depthOfField))
            depthOfField = runtimeVolumeProfile.Add<DepthOfField>(true);

        if (depthOfField == null)
            return;

        baseActive = depthOfField.active;
        baseMode = depthOfField.mode.value;
        baseFocusDistance = depthOfField.focusDistance.value;
        baseAperture = depthOfField.aperture.value;
        baseFocalLength = depthOfField.focalLength.value;
        baseBladeCount = depthOfField.bladeCount.value;
        baseBladeCurvature = depthOfField.bladeCurvature.value;
        baseBladeRotation = depthOfField.bladeRotation.value;
        baseModeOverrideState = depthOfField.mode.overrideState;
        baseFocusDistanceOverrideState = depthOfField.focusDistance.overrideState;
        baseApertureOverrideState = depthOfField.aperture.overrideState;
        baseFocalLengthOverrideState = depthOfField.focalLength.overrideState;
        baseBladeCountOverrideState = depthOfField.bladeCount.overrideState;
        baseBladeCurvatureOverrideState = depthOfField.bladeCurvature.overrideState;
        baseBladeRotationOverrideState = depthOfField.bladeRotation.overrideState;
        hasCachedBaseline = true;
    }

    /// <summary>
    /// Applies one blur strength while preserving and restoring the original volume override state.
    /// </summary>
    private void ApplyStrength(
        float strength,
        float focusDistance,
        float aperture,
        float focalLength,
        int bladeCount,
        float bladeCurvature,
        float bladeRotation)
    {
        if (depthOfField == null)
            return;

        float clampedStrength = Mathf.Clamp01(strength);
        if (clampedStrength <= 0f)
        {
            RestoreBaseline();
            return;
        }

        appliedStrength = clampedStrength;
        depthOfField.active = true;
        depthOfField.mode.overrideState = true;
        depthOfField.focusDistance.overrideState = true;
        depthOfField.aperture.overrideState = true;
        depthOfField.focalLength.overrideState = true;
        depthOfField.bladeCount.overrideState = true;
        depthOfField.bladeCurvature.overrideState = true;
        depthOfField.bladeRotation.overrideState = true;
        depthOfField.mode.value = DepthOfFieldMode.Bokeh;
        depthOfField.focusDistance.value = Mathf.Lerp(baseFocusDistance, focusDistance, clampedStrength);
        depthOfField.aperture.value = Mathf.Lerp(baseAperture, aperture, clampedStrength);
        depthOfField.focalLength.value = Mathf.Lerp(baseFocalLength, focalLength, clampedStrength);
        depthOfField.bladeCount.value = Mathf.RoundToInt(Mathf.Lerp(baseBladeCount, bladeCount, clampedStrength));
        depthOfField.bladeCurvature.value = Mathf.Lerp(baseBladeCurvature, bladeCurvature, clampedStrength);
        depthOfField.bladeRotation.value = Mathf.Lerp(baseBladeRotation, bladeRotation, clampedStrength);
    }

    /// <summary>
    /// Restores the exact depth-of-field state that existed before the minigame opened.
    /// </summary>
    private void RestoreBaseline()
    {
        depthOfField.active = baseActive;
        depthOfField.mode.overrideState = baseModeOverrideState;
        depthOfField.focusDistance.overrideState = baseFocusDistanceOverrideState;
        depthOfField.aperture.overrideState = baseApertureOverrideState;
        depthOfField.focalLength.overrideState = baseFocalLengthOverrideState;
        depthOfField.bladeCount.overrideState = baseBladeCountOverrideState;
        depthOfField.bladeCurvature.overrideState = baseBladeCurvatureOverrideState;
        depthOfField.bladeRotation.overrideState = baseBladeRotationOverrideState;
        depthOfField.mode.value = baseMode;
        depthOfField.focusDistance.value = baseFocusDistance;
        depthOfField.aperture.value = baseAperture;
        depthOfField.focalLength.value = baseFocalLength;
        depthOfField.bladeCount.value = baseBladeCount;
        depthOfField.bladeCurvature.value = baseBladeCurvature;
        depthOfField.bladeRotation.value = baseBladeRotation;
        appliedStrength = 0f;
        hasCachedBaseline = false;
        depthOfField = null;
        targetVolume = null;
    }
}

}
