using System;
using System.Collections;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using UnityEngine;

namespace Breezeblocks.Missions
{

public partial class GameplayMissionController
{
    /// <summary>
    /// Returns whether intro cinematic has all required scene references.
    /// </summary>
    private bool CanPlayIntroCinematic()
    {
        return introCarTransform != null &&
               introDriveTarget != null &&
               introPlayerExitPoint != null;
    }

    /// <summary>
    /// Delays startup fade, then either begins intro cinematic or gameplay immediately.
    /// </summary>
    private IEnumerator BeginMissionStartupRoutine()
    {
        if (introStartupBlackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(introStartupBlackHoldDuration);

        bool shouldPlayIntro = playIntroCinematic && CanPlayIntroCinematic();
        if (shouldPlayIntro)
        {
            SetIntroVisionLightActive(false);
            ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);
            introRoutine = StartCoroutine(PlayIntroRoutine());
            yield return FadeOverlayOutAndRestore();
        }
        else
        {
            yield return FadeOverlayOutAndRestore();
            StartGameplay();
        }

        startupSequenceRoutine = null;
    }

    /// <summary>
    /// Plays intro cinematic sequence from drive-in through player exit.
    /// </summary>
    private IEnumerator PlayIntroRoutine()
    {
        BlockPlayerControls(true);
        SetIntroVisionLightActive(false);
        ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);
        AttachPlayerToPoint(introCarSeatPoint != null ? introCarSeatPoint : introCarTransform, parentToSeat: true, facingDegrees: introInitialPlayerFacingDegrees);

        yield return DriveCarToPoint(introCarTransform, introDriveTarget, introDriveSpeed, introDriveAcceleration, introDriveDeceleration, startAtCruiseSpeed: true);

        PlayCarAnimation(openDoorAnimationState);
        PlayCarDoorOpenSfx();
        float introDoorOpenPhaseDuration = Mathf.Max(introDoorOpenWait, ResolveCarAnimationDuration(openDoorAnimationState));
        if (playerRoot != null)
            playerRoot.SetParent(null, true);

        Tween exitMoveTween = BeginMovePlayerToPoint(introPlayerExitPoint, introPlayerFacingTarget, introPlayerExitDuration);
        if (introDoorOpenPhaseDuration > 0f)
            yield return new WaitForSecondsRealtime(introDoorOpenPhaseDuration);

        if (exitMoveTween != null)
            yield return exitMoveTween.WaitForCompletion();

        ApplyCinematicPlayerFacing(introPlayerExitPoint, introPlayerFacingTarget, null);

        PlayCarAnimation(closeDoorAnimationState);
        PlayCarDoorCloseSfx();
        if (introDoorCloseWait > 0f)
            yield return new WaitForSecondsRealtime(introDoorCloseWait);

        introRoutine = null;
        StartGameplay();
    }

    /// <summary>
    /// Enables gameplay systems after startup sequence finishes.
    /// </summary>
    private void StartGameplay()
    {
        gameplayStarted = true;
        EnemyRuntimeBlockedAtMissionStart = false;
        NotifyEnemiesGameplayStarted();
        SetEndScreenPointerVisible(false);
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, true);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, true);
        SetIntroVisionLightActive(true);
        BlockPlayerControls(false);
        gameplayHudController?.HandleGameplayStarted();
        RefreshTimeLimitUi();

        if (objectivesCompleted)
            HandleAllObjectivesCompleted();
    }

    /// <summary>
    /// Signals all enemy movement controllers that mission gameplay has begun.
    /// </summary>
    private void NotifyEnemiesGameplayStarted()
    {
        EnemyMovementController[] enemyControllers = FindObjectsByType<EnemyMovementController>(FindObjectsSortMode.None);
        for (int i = 0; i < enemyControllers.Length; i++)
            enemyControllers[i]?.HandleMissionGameplayStarted();
    }

    /// <summary>
    /// Polls configured Rewired action to skip intro cinematic.
    /// </summary>
    private void TryHandleIntroSkipInput()
    {
        if (!CanPlayIntroCinematic() || gameplayStarted || missionEnded)
            return;

        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        if (!rewiredPlayer.GetButtonDown(skipIntroAction))
            return;

        introSkipRoutine = StartCoroutine(SkipIntroRoutine());
    }

    /// <summary>
    /// Skips intro cinematic by fading to black, snapping state, then fading back out.
    /// </summary>
    private IEnumerator SkipIntroRoutine()
    {
        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        activeCinematicPlayerMoveTween?.Kill();
        activeCinematicPlayerMoveTween = null;

        BlockPlayerControls(true);
        SetIntroVisionLightActive(false);

        Tween fadeInTween = fadeImageFader != null ? fadeImageFader.FadeIn(screenFadeDuration) : null;
        if (fadeInTween != null)
            yield return fadeInTween.WaitForCompletion();

        CompleteIntroInstantly();
        StartGameplay();

        if (introSkipBlackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(introSkipBlackHoldDuration);

        Tween fadeOutTween = fadeImageFader != null ? fadeImageFader.FadeOut(screenFadeDuration) : null;
        if (fadeOutTween != null)
            yield return fadeOutTween.WaitForCompletion();

        introSkipRoutine = null;
    }

    /// <summary>
    /// Moves player to cinematic target and waits until movement completes.
    /// </summary>
    private IEnumerator MovePlayerToPoint(Transform targetPoint, Transform facingTarget, float duration, float? facingDegrees = null)
    {
        Tween moveTween = BeginMovePlayerToPoint(targetPoint, facingTarget, duration, facingDegrees);
        if (moveTween == null)
            yield break;

        yield return moveTween.WaitForCompletion();
        ApplyCinematicPlayerFacing(targetPoint, facingTarget, facingDegrees);
    }

    /// <summary>
    /// Starts cinematic player move tween toward target point.
    /// </summary>
    private Tween BeginMovePlayerToPoint(Transform targetPoint, Transform facingTarget, float duration, float? facingDegrees = null)
    {
        if (playerRoot == null || targetPoint == null)
            return null;

        activeCinematicPlayerMoveTween?.Kill();
        activeCinematicPlayerMoveTween = playerRoot.DOMove(targetPoint.position, Mathf.Max(0f, duration))
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnUpdate(() => ApplyCinematicPlayerFacing(targetPoint, facingTarget, facingDegrees))
            .OnComplete(() => activeCinematicPlayerMoveTween = null);

        return activeCinematicPlayerMoveTween;
    }

    /// <summary>
    /// Applies cinematic facing from explicit target, explicit degrees, or motion target.
    /// </summary>
    private void ApplyCinematicPlayerFacing(Transform targetPoint, Transform facingTarget, float? facingDegrees)
    {
        if (facingTarget != null)
        {
            ForcePlayerFacing(facingTarget.position);
            return;
        }

        if (facingDegrees.HasValue)
        {
            SmoothPlayerFacingTowardsDegrees(facingDegrees.Value, Time.unscaledDeltaTime);
            return;
        }

        if (targetPoint != null)
            ForcePlayerFacing(targetPoint.position);
    }

    /// <summary>
    /// Drives cinematic car toward target using acceleration and deceleration model.
    /// </summary>
    private IEnumerator DriveCarToPoint(Transform carTransform, Transform targetPoint, float driveSpeed, float acceleration, float deceleration, bool startAtCruiseSpeed, bool continuePastTarget = false)
    {
        if (carTransform == null || targetPoint == null)
            yield break;

        Vector2 startPosition = carTransform.position;
        Vector2 targetPosition = targetPoint.position;
        Vector2 path = targetPosition - startPosition;
        float totalDistance = path.magnitude;
        if (totalDistance <= 0.0001f)
        {
            carTransform.position = targetPosition;
            yield break;
        }

        float maxSpeed = Mathf.Max(0f, driveSpeed);
        if (maxSpeed <= 0.0001f)
        {
            carTransform.position = targetPosition;
            yield break;
        }

        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);

        StopContinuousCarDrive();

        Vector2 direction = path / totalDistance;
        float currentSpeed = startAtCruiseSpeed ? maxSpeed : 0f;
        SetCarEngineLoopActive(true);

        while (true)
        {
            Vector2 currentPosition = carTransform.position;
            float traveledDistance = Vector2.Dot(currentPosition - startPosition, direction);
            float clampedTraveledDistance = Mathf.Max(0f, traveledDistance);
            float remainingDistance = totalDistance - clampedTraveledDistance;
            if (remainingDistance <= 0f)
                break;

            float desiredSpeed = maxSpeed;
            if (deceleration > 0f)
            {
                float brakingDistance = (currentSpeed * currentSpeed) / (2f * deceleration);
                if (remainingDistance <= brakingDistance)
                    desiredSpeed = Mathf.Sqrt(Mathf.Max(0f, 2f * deceleration * remainingDistance));
            }

            if (currentSpeed < desiredSpeed)
            {
                currentSpeed = acceleration > 0f
                    ? Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.unscaledDeltaTime)
                    : desiredSpeed;
            }
            else
            {
                currentSpeed = deceleration > 0f
                    ? Mathf.MoveTowards(currentSpeed, desiredSpeed, deceleration * Time.unscaledDeltaTime)
                    : desiredSpeed;
            }

            float frameDistance = currentSpeed * Time.unscaledDeltaTime;
            if (frameDistance <= 0f)
            {
                carTransform.position = targetPosition;
                break;
            }

            float nextDistance = clampedTraveledDistance + frameDistance;
            if (nextDistance >= totalDistance)
            {
                carTransform.position = targetPosition;
                break;
            }

            carTransform.position = startPosition + direction * nextDistance;
            yield return null;
        }

        carTransform.position = targetPosition;
        if (continuePastTarget)
        {
            continuousCarDriveRoutine = StartCoroutine(ContinueDrivingCarForever(carTransform, direction, Mathf.Max(currentSpeed, maxSpeed > 0f ? maxSpeed : currentSpeed)));
            yield break;
        }

        SetCarEngineLoopActive(false);
    }

    /// <summary>
    /// Continues moving cinematic car forever in same direction after outro departure.
    /// </summary>
    private IEnumerator ContinueDrivingCarForever(Transform carTransform, Vector2 direction, float speed)
    {
        if (carTransform == null || direction.sqrMagnitude <= 0.0001f || speed <= 0.0001f)
        {
            continuousCarDriveRoutine = null;
            yield break;
        }

        Vector2 normalizedDirection = direction.normalized;
        while (carTransform != null)
        {
            carTransform.position += (Vector3)(normalizedDirection * (speed * Time.unscaledDeltaTime));
            yield return null;
        }

        continuousCarDriveRoutine = null;
    }

    /// <summary>
    /// Stops continuous cinematic car drive routine if active.
    /// </summary>
    private void StopContinuousCarDrive()
    {
        if (continuousCarDriveRoutine == null)
            return;

        StopCoroutine(continuousCarDriveRoutine);
        continuousCarDriveRoutine = null;
    }

    /// <summary>
    /// Snaps intro cinematic to completed state for intro skip flow.
    /// </summary>
    private void CompleteIntroInstantly()
    {
        SetCarEngineLoopActive(false);

        if (introCarTransform != null && introDriveTarget != null)
            introCarTransform.position = introDriveTarget.position;

        PlayCarAnimation(closeDoorAnimationState, 1f);

        if (playerRoot != null)
            playerRoot.SetParent(null, true);

        if (playerRoot != null && introPlayerExitPoint != null)
            playerRoot.position = introPlayerExitPoint.position;

        if (introPlayerFacingTarget != null)
            ForcePlayerFacing(introPlayerFacingTarget.position);
        else
            ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);

        if (playerBody != null)
            playerBody.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// Snaps player to cinematic anchor point and optional facing.
    /// </summary>
    private void AttachPlayerToPoint(Transform targetPoint, bool parentToSeat, float? facingDegrees = null)
    {
        if (playerRoot == null || targetPoint == null)
            return;

        if (parentToSeat)
            playerRoot.SetParent(targetPoint, false);
        else
            playerRoot.SetParent(null, true);

        playerRoot.position = targetPoint.position;
        if (facingDegrees.HasValue)
            ApplyPlayerFacingDegrees(facingDegrees.Value);
    }

    /// <summary>
    /// Applies exact player facing angle to transform, body, and vision light.
    /// </summary>
    private void ApplyPlayerFacingDegrees(float facingDegrees)
    {
        if (playerRoot != null)
            playerRoot.rotation = Quaternion.Euler(0f, 0f, facingDegrees);

        if (playerBody != null)
            playerBody.rotation = facingDegrees;

        if (playerVisionLight != null)
            playerVisionLight.ApplyExternalDirection(RotateUpByDegrees(facingDegrees), 0f, 0f);
    }

    /// <summary>
    /// Smoothly rotates player vision toward desired facing degrees.
    /// </summary>
    private void SmoothPlayerFacingTowardsDegrees(float facingDegrees, float deltaTime)
    {
        if (playerVisionLight == null)
        {
            ApplyPlayerFacingDegrees(facingDegrees);
            return;
        }

        playerVisionLight.ApplyExternalDirection(RotateUpByDegrees(facingDegrees), playerVisionLight.RotationSmoothing, deltaTime);
    }

    /// <summary>
    /// Waits until player facing settles at desired cinematic angle.
    /// </summary>
    private IEnumerator RotatePlayerToFacingDegrees(float facingDegrees)
    {
        if (playerVisionLight == null)
        {
            ApplyPlayerFacingDegrees(facingDegrees);
            yield break;
        }

        float timeout = 1.5f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            SmoothPlayerFacingTowardsDegrees(facingDegrees, Time.unscaledDeltaTime);
            float currentAngle = playerRoot != null ? playerRoot.eulerAngles.z : playerBody != null ? playerBody.rotation : 0f;
            float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, facingDegrees));
            if (delta <= 0.5f)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyPlayerFacingDegrees(facingDegrees);
    }

    /// <summary>
    /// Forces player look direction toward world target point.
    /// </summary>
    private void ForcePlayerFacing(Vector3 worldTarget)
    {
        if (playerVisionLight == null || playerRoot == null)
            return;

        Vector2 direction = (Vector2)(worldTarget - playerRoot.position);
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        playerVisionLight.ApplyExternalDirection(direction.normalized, playerVisionLight.RotationSmoothing, Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Plays requested intro car animation state immediately.
    /// </summary>
    private void PlayCarAnimation(string stateName, float normalizedTime = 0f)
    {
        if (introCarAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        introCarAnimator.Play(stateName, 0, Mathf.Clamp01(normalizedTime));
        introCarAnimator.Update(0f);
    }

    /// <summary>
    /// Resolves active car animation duration from animator state or clip table.
    /// </summary>
    private float ResolveCarAnimationDuration(string stateName)
    {
        if (introCarAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return 0f;

        AnimatorStateInfo stateInfo = introCarAnimator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.length > 0f)
            return stateInfo.length;

        if (introCarAnimator.runtimeAnimatorController == null)
            return 0f;

        AnimationClip[] clips = introCarAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || !string.Equals(clip.name, stateName, StringComparison.Ordinal))
                continue;

            return Mathf.Max(0f, clip.length);
        }

        return 0f;
    }

    /// <summary>
    /// Plays car door open one-shot.
    /// </summary>
    private void PlayCarDoorOpenSfx()
    {
        PlayCarOneShot(carDoorOpenSfx);
    }

    /// <summary>
    /// Plays car door close one-shot.
    /// </summary>
    private void PlayCarDoorCloseSfx()
    {
        PlayCarOneShot(carDoorCloseSfx);
    }

    /// <summary>
    /// Plays car engine start one-shot and returns clip duration.
    /// </summary>
    private float PlayCarStartSfx()
    {
        return PlayCarOneShot(carStartSfx);
    }

    /// <summary>
    /// Plays given car audio clip set at car position.
    /// </summary>
    private float PlayCarOneShot(AudioClipSet clipSet)
    {
        if (introCarTransform == null || clipSet == null || !clipSet.HasAnyClip)
            return 0f;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return 0f;

        bool played = worldSfxManager.PlayClipSetAt(introCarTransform.position, clipSet, carSfxSoundType, out float playbackDuration);
        return played ? playbackDuration : 0f;
    }

    /// <summary>
    /// Starts idle loop if clip exists and no auto-restart suppression is active.
    /// </summary>
    private void StartCarIdleLoopIfNeeded()
    {
        if (suppressCarAudioAutoRestart ||
            carIdleLoopSource == null ||
            carIdleLoopSource.isPlaying ||
            carIdleLoopSfx == null ||
            !carIdleLoopSfx.HasAnyClip)
        {
            return;
        }

        carIdleLoopBaseVolume = Mathf.Clamp01(carIdleLoopSfx.Volume);
        PlayLoopClipSet(carIdleLoopSource, carIdleLoopSfx, initialVolume: 0f);
        ApplyCarLoopVolumes();
    }

    /// <summary>
    /// Ensures idle loop source exists and is playing while car is present.
    /// </summary>
    private void EnsureCarIdleLoopRunning()
    {
        if (suppressCarAudioAutoRestart ||
            introCarTransform == null ||
            carIdleLoopSfx == null ||
            !carIdleLoopSfx.HasAnyClip)
        {
            return;
        }

        carIdleLoopSource = EnsureCarLoopSource(carIdleLoopSource, "Car Idle Loop Source", carIdleLoopLocalOffset);
        if (carIdleLoopSource == null)
            return;

        if (!carIdleLoopSource.isPlaying || carIdleLoopSource.clip == null)
        {
            StartCarIdleLoopIfNeeded();
            return;
        }

        carIdleLoopBaseVolume = Mathf.Clamp01(carIdleLoopSfx.Volume);
    }

    /// <summary>
    /// Starts or stops car engine loop with volume tween.
    /// </summary>
    private void SetCarEngineLoopActive(bool active)
    {
        if (introCarTransform == null)
            return;

        if (suppressCarAudioAutoRestart)
        {
            if (!active)
                StopAllCarAudio(suppressAutoRestart: true);
            return;
        }

        EnsureCarIdleLoopRunning();
        carEngineLoopSource = EnsureCarLoopSource(carEngineLoopSource, "Car Engine Loop Source", carEngineLoopLocalOffset);
        if (carEngineLoopSource == null)
            return;

        carEngineLoopTween?.Kill();
        carEngineLoopTween = null;

        if (active)
        {
            if (!carEngineLoopSource.isPlaying)
            {
                carEngineLoopBaseVolume = 0f;
                PlayLoopClipSet(carEngineLoopSource, carEngineLoopSfx, initialVolume: 0f);
            }

            if (carEngineLoopSource.clip == null)
                return;

            float targetVolume = carEngineLoopSfx != null ? Mathf.Clamp01(carEngineLoopSfx.Volume) : 0f;
            carEngineLoopTween = DOTween.To(
                    () => carEngineLoopBaseVolume,
                    value =>
                    {
                        carEngineLoopBaseVolume = Mathf.Clamp01(value);
                        ApplyCarLoopVolumes();
                    },
                    targetVolume,
                    0.2f)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
            return;
        }

        if (!carEngineLoopSource.isPlaying)
            return;

        carEngineLoopTween = DOTween.To(
                () => carEngineLoopBaseVolume,
                value =>
                {
                    carEngineLoopBaseVolume = Mathf.Clamp01(value);
                    ApplyCarLoopVolumes();
                },
                0f,
                0.2f)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (carEngineLoopSource != null)
                {
                    carEngineLoopSource.Stop();
                    carEngineLoopSource.clip = null;
                }

                carEngineLoopBaseVolume = 0f;
            });
    }

    /// <summary>
    /// Applies external volume multiplier to all active car loops.
    /// </summary>
    public void SetExternalCarAudioVolumeMultiplier(float multiplier)
    {
        carAudioExternalVolumeMultiplier = Mathf.Clamp01(multiplier);
        ApplyCarLoopVolumes();
    }

    /// <summary>
    /// Resolves or creates named audio source under intro car transform.
    /// </summary>
    private AudioSource EnsureCarLoopSource(AudioSource existingSource, string objectName, Vector3 localOffset)
    {
        if (introCarTransform == null)
            return null;

        AudioSource resolvedSource = existingSource;
        if (resolvedSource == null)
        {
            Transform existingChild = introCarTransform.Find(objectName);
            if (existingChild != null)
                resolvedSource = existingChild.GetComponent<AudioSource>();
        }

        if (resolvedSource == null)
        {
            GameObject sourceObject = new(objectName);
            sourceObject.transform.SetParent(introCarTransform, false);
            resolvedSource = sourceObject.AddComponent<AudioSource>();
        }

        ConfigureCarLoopSource(resolvedSource, localOffset);
        return resolvedSource;
    }

    /// <summary>
    /// Applies shared audio source settings to cinematic car loop source.
    /// </summary>
    private void ConfigureCarLoopSource(AudioSource source, Vector3 localOffset)
    {
        if (source == null)
            return;

        source.transform.localPosition = localOffset;
        source.playOnAwake = false;
        source.loop = true;
        source.outputAudioMixerGroup = carLoopMixerGroup;
        source.spatialBlend = carLoopSpatialBlend;
        source.minDistance = carLoopMinDistance;
        source.maxDistance = carLoopMaxDistance;
        source.rolloffMode = carLoopRolloffMode;
        source.dopplerLevel = carLoopDopplerLevel;
        source.spread = carLoopSpread;
        source.priority = carLoopPriority;
    }

    /// <summary>
    /// Starts loop playback from clip set on provided source.
    /// </summary>
    private static void PlayLoopClipSet(AudioSource source, AudioClipSet clipSet, float initialVolume)
    {
        if (source == null || clipSet == null || !clipSet.HasAnyClip)
            return;

        AudioClip clip = clipSet.GetRandomClip();
        if (clip == null)
            return;

        source.clip = clip;
        source.pitch = clipSet.GetRandomPitch();
        source.spatialBlend = clipSet.ResolveSpatialBlend(source.spatialBlend);
        source.minDistance = clipSet.ResolveMinDistance(source.minDistance);
        source.maxDistance = clipSet.ResolveMaxDistance(source.minDistance, source.maxDistance);
        source.volume = Mathf.Clamp01(initialVolume);
        source.loop = true;
        source.Play();
    }

    /// <summary>
    /// Recomputes effective car loop volumes from base levels and external multiplier.
    /// </summary>
    private void ApplyCarLoopVolumes()
    {
        float multiplier = Mathf.Clamp01(carAudioExternalVolumeMultiplier);

        if (carIdleLoopSource != null)
            carIdleLoopSource.volume = Mathf.Clamp01(carIdleLoopBaseVolume * multiplier);

        if (carEngineLoopSource != null)
            carEngineLoopSource.volume = Mathf.Clamp01(carEngineLoopBaseVolume * multiplier);
    }

    /// <summary>
    /// Stops all car audio and optionally suppresses loop auto-restart.
    /// </summary>
    private void StopAllCarAudio(bool suppressAutoRestart)
    {
        suppressCarAudioAutoRestart = suppressAutoRestart;

        carEngineLoopTween?.Kill();
        carEngineLoopTween = null;

        if (carIdleLoopSource != null)
        {
            carIdleLoopSource.Stop();
            carIdleLoopSource.clip = null;
        }

        if (carEngineLoopSource != null)
        {
            carEngineLoopSource.Stop();
            carEngineLoopSource.clip = null;
        }

        carIdleLoopBaseVolume = 0f;
        carEngineLoopBaseVolume = 0f;
    }
}

}
