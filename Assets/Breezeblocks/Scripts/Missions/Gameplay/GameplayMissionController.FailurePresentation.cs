using Breezeblocks.WeaponSystem;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Breezeblocks.Missions
{

public partial class GameplayMissionController
{
    private const float DefaultFailureTargetTimeScale = 0.3f;
    private const float DefaultFailureSlowMotionDuration = 0.6f;
    private const float DefaultFailureScreenDelay = 1.5f;
    private const float DefaultEnemyFocusOrthographicSize = 3f;
    private const float DefaultEnemyFocusZoomDuration = 0.6f;
    private const float DefaultPlayerKilledTintDuration = 0.8f;

    private static readonly Color DefaultPlayerKilledTintColor = new(1f, 0.18f, 0.18f, 1f);

    private PlayerAimCamera2D failureAimCamera;
    private Volume failureVolume;
    private ColorAdjustments failureColorAdjustments;
    private Tween failureTimeScaleTween;
    private Tween failureZoomTween;
    private Tween failureTintTween;
    private float failureBaseFixedDeltaTime;
    private float failureBaseOrthographicSize;
    private bool failureFixedDeltaTimeCached;
    private bool failureZoomBaselineCached;
    private bool failureTintBaselineCached;
    private bool failureColorAdjustmentsBaselineActive;
    private bool failureTintBaselineOverrideState;
    private Color failureTintBaselineColor = Color.white;

    /// <summary>
    /// Resolves and caches presentation dependencies before a failure can occur.
    /// </summary>
    private void PrepareFailurePresentation()
    {
        if (!failureFixedDeltaTimeCached)
        {
            failureBaseFixedDeltaTime = Time.fixedDeltaTime / Mathf.Max(Time.timeScale, 0.01f);
            failureFixedDeltaTimeCached = true;
        }

        if (failureAimCamera == null)
            failureAimCamera = PlayerSceneReferenceUtility.FindPlayerAimCamera(gameObject);

        CacheFailureZoomBaseline();
        CacheFailureTintBaseline();
    }

    /// <summary>
    /// Starts camera focus, slow motion, and optional player-death tint presentation.
    /// </summary>
    private void BeginFailurePresentation(Transform focusTarget, bool applyPlayerKilledTint)
    {
        PrepareFailurePresentation();

        if (focusTarget != null && failureAimCamera != null)
        {
            failureAimCamera.SetAimState(false, 0f);
            failureAimCamera.SetFollowTarget(focusTarget);
        }

        if (focusTarget != null)
            StartEnemyFocusZoom();

        StartFailureSlowMotion();

        if (applyPlayerKilledTint)
            StartPlayerKilledTint();
    }

    /// <summary>
    /// Smoothly lowers the global time scale using unscaled time.
    /// </summary>
    private void StartFailureSlowMotion()
    {
        failureTimeScaleTween?.Kill();
        failureTimeScaleTween = null;

        float targetTimeScale = ResolveFailureTargetTimeScale();
        float duration = ResolveFailureSlowMotionDuration();
        if (duration <= 0f)
        {
            ApplyFailureTimeScale(targetTimeScale);
            return;
        }

        failureTimeScaleTween = DOVirtual.Float(Time.timeScale, targetTimeScale, duration, ApplyFailureTimeScale)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() => failureTimeScaleTween = null);
    }

    /// <summary>
    /// Applies the current failure time scale and keeps fixed-step simulation proportional.
    /// </summary>
    private void ApplyFailureTimeScale(float value)
    {
        float clampedTimeScale = Mathf.Clamp(value, 0.01f, 1f);
        Time.timeScale = clampedTimeScale;

        if (failureFixedDeltaTimeCached)
            Time.fixedDeltaTime = failureBaseFixedDeltaTime * clampedTimeScale;
    }

    /// <summary>
    /// Restores normal simulation speed when the game-over panel becomes visible.
    /// </summary>
    private void RestoreFailureTimeScale()
    {
        failureTimeScaleTween?.Kill();
        failureTimeScaleTween = null;
        Time.timeScale = 1f;

        if (failureFixedDeltaTimeCached)
            Time.fixedDeltaTime = failureBaseFixedDeltaTime;
    }

    /// <summary>
    /// Smoothly zooms toward the enemy responsible for an alert or detection failure.
    /// </summary>
    private void StartEnemyFocusZoom()
    {
        CacheFailureZoomBaseline();
        if (failureAimCamera == null)
            return;

        failureZoomTween?.Kill();
        failureZoomTween = null;

        float targetOrthographicSize = ResolveEnemyFocusOrthographicSize();
        float duration = ResolveEnemyFocusZoomDuration();
        if (duration <= 0f)
        {
            ApplyFailureOrthographicSize(targetOrthographicSize);
            return;
        }

        failureZoomTween = DOVirtual.Float(
                ResolveCurrentFailureOrthographicSize(),
                targetOrthographicSize,
                duration,
                ApplyFailureOrthographicSize)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() => failureZoomTween = null);
    }

    /// <summary>
    /// Applies an orthographic size to the active Cinemachine camera lens.
    /// </summary>
    private void ApplyFailureOrthographicSize(float orthographicSize)
    {
        failureAimCamera?.SetOrthographicSize(orthographicSize);
    }

    /// <summary>
    /// Caches the original orthographic size from the active player camera.
    /// </summary>
    private void CacheFailureZoomBaseline()
    {
        if (failureZoomBaselineCached)
            return;

        if (failureAimCamera == null || !failureAimCamera.TryGetOrthographicSize(out failureBaseOrthographicSize))
            return;

        failureZoomBaselineCached = true;
    }

    /// <summary>
    /// Resolves the current orthographic size while safely falling back to the cached baseline.
    /// </summary>
    private float ResolveCurrentFailureOrthographicSize()
    {
        return failureAimCamera != null && failureAimCamera.TryGetOrthographicSize(out float orthographicSize)
            ? orthographicSize
            : failureBaseOrthographicSize;
    }

    /// <summary>
    /// Smoothly tints the player camera red after the player is killed.
    /// </summary>
    private void StartPlayerKilledTint()
    {
        CacheFailureTintBaseline();
        if (failureColorAdjustments == null)
            return;

        failureTintTween?.Kill();
        failureTintTween = null;
        failureColorAdjustments.active = true;
        failureColorAdjustments.colorFilter.overrideState = true;

        Color targetColor = ResolvePlayerKilledTintColor();
        float duration = ResolvePlayerKilledTintDuration();
        if (duration <= 0f)
        {
            failureColorAdjustments.colorFilter.value = targetColor;
            return;
        }

        failureTintTween = DOTween.To(
                () => failureColorAdjustments.colorFilter.value,
                value => failureColorAdjustments.colorFilter.value = value,
                targetColor,
                duration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .OnComplete(() => failureTintTween = null);
    }

    /// <summary>
    /// Caches the runtime volume's color-filter state so it can be restored after failure presentation.
    /// </summary>
    private void CacheFailureTintBaseline()
    {
        if (failureTintBaselineCached)
            return;

        if (failureVolume == null)
            failureVolume = PlayerSceneReferenceUtility.FindPlayerVolume(playerRoot != null ? playerRoot.gameObject : gameObject);

        VolumeProfile runtimeProfile = failureVolume != null ? failureVolume.profile : null;
        if (runtimeProfile == null)
            return;

        if (!runtimeProfile.TryGet(out failureColorAdjustments))
        {
            failureColorAdjustments = runtimeProfile.Add<ColorAdjustments>(true);
            failureColorAdjustments.active = false;
            failureColorAdjustments.colorFilter.overrideState = false;
            failureColorAdjustments.colorFilter.value = Color.white;
        }

        if (failureColorAdjustments == null)
            return;

        failureColorAdjustmentsBaselineActive = failureColorAdjustments.active;
        failureTintBaselineOverrideState = failureColorAdjustments.colorFilter.overrideState;
        failureTintBaselineColor = failureColorAdjustments.colorFilter.value;
        failureTintBaselineCached = true;
    }

    /// <summary>
    /// Restores time scale, camera follow, and post-processing modified by failure presentation.
    /// </summary>
    private void ResetFailurePresentation()
    {
        RestoreFailureTimeScale();
        failureZoomTween?.Kill();
        failureZoomTween = null;
        failureTintTween?.Kill();
        failureTintTween = null;

        if (failureAimCamera != null && playerRoot != null)
        {
            failureAimCamera.SetAimState(false, 0f);
            failureAimCamera.SetFollowTarget(playerRoot);
        }

        if (failureZoomBaselineCached)
            ApplyFailureOrthographicSize(failureBaseOrthographicSize);

        if (!failureTintBaselineCached || failureColorAdjustments == null)
            return;

        failureColorAdjustments.colorFilter.value = failureTintBaselineColor;
        failureColorAdjustments.colorFilter.overrideState = failureTintBaselineOverrideState;
        failureColorAdjustments.active = failureColorAdjustmentsBaselineActive;
    }

    /// <summary>
    /// Resolves the real-time delay before a mission failure screen appears.
    /// </summary>
    private float ResolveFailureScreenDelay()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.MissionFailurePresentation.ScreenDelay
            : DefaultFailureScreenDelay;
    }

    /// <summary>
    /// Resolves the target global time scale used during mission failure presentation.
    /// </summary>
    private float ResolveFailureTargetTimeScale()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.MissionFailurePresentation.TargetTimeScale
            : DefaultFailureTargetTimeScale;
    }

    /// <summary>
    /// Resolves how quickly mission failure slow motion reaches its target value.
    /// </summary>
    private float ResolveFailureSlowMotionDuration()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.MissionFailurePresentation.SlowMotionDuration
            : DefaultFailureSlowMotionDuration;
    }

    /// <summary>
    /// Resolves the orthographic size used to zoom toward the responsible enemy.
    /// </summary>
    private float ResolveEnemyFocusOrthographicSize()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.MissionFailurePresentation.EnemyFocusOrthographicSize
            : DefaultEnemyFocusOrthographicSize;
    }

    /// <summary>
    /// Resolves the unscaled duration used to zoom toward the responsible enemy.
    /// </summary>
    private float ResolveEnemyFocusZoomDuration()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.MissionFailurePresentation.EnemyFocusZoomDuration
            : DefaultEnemyFocusZoomDuration;
    }

    /// <summary>
    /// Resolves the post-processing color used when the player is killed.
    /// </summary>
    private Color ResolvePlayerKilledTintColor()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.MissionFailurePresentation.PlayerKilledTintColor
            : DefaultPlayerKilledTintColor;
    }

    /// <summary>
    /// Resolves how quickly the player-killed post-processing tint is applied.
    /// </summary>
    private float ResolvePlayerKilledTintDuration()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.MissionFailurePresentation.PlayerKilledTintDuration
            : DefaultPlayerKilledTintDuration;
    }
}

}
