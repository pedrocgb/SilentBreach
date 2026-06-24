using System.Collections;
using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breezeblocks.Missions
{

public partial class GameplayMissionController
{
    /// <summary>
    /// Plays the mission-complete outro and reveals the win screen.
    /// </summary>
    private IEnumerator PlayWinRoutine()
    {
        if (missionEnded)
            yield break;

        missionEnded = true;
        Time.timeScale = 1f;
        PrepareJobSuccessRewards();
        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse();
        BlockPlayerControls(true);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, false);

        if (introCarTransform == null)
        {
            if (gameWinMessageText != null)
                gameWinMessageText.text = ResolveMissionCompletedMessage();

            yield return FadeAndShowScreen(gameWinScreen);
            PlayJobSuccessRewardsPresentation();
            yield break;
        }

        if (missionEscapeTrigger != null)
            missionEscapeTrigger.SetEscapeEnabled(false);

        PlayCarAnimation(openDoorAnimationState);
        PlayCarDoorOpenSfx();
        float outroDoorOpenPhaseDuration = Mathf.Max(outroDoorOpenWait, ResolveCarAnimationDuration(openDoorAnimationState));
        SetCollidersEnabled(carCollidersToDisableWhileBoarding, false);
        Transform boardingSeatTarget = outroCarSeatPoint != null
            ? outroCarSeatPoint
            : outroPlayerEntryPoint != null ? outroPlayerEntryPoint : introCarTransform;
        Tween boardingMoveTween = BeginMovePlayerToPoint(boardingSeatTarget, null, outroPlayerEntryDuration, winCinematicFacingDegrees);
        if (outroDoorOpenPhaseDuration > 0f)
            yield return new WaitForSecondsRealtime(outroDoorOpenPhaseDuration);

        if (boardingMoveTween != null)
            yield return boardingMoveTween.WaitForCompletion();

        ApplyCinematicPlayerFacing(boardingSeatTarget, null, winCinematicFacingDegrees);
        yield return RotatePlayerToFacingDegrees(winCinematicFacingDegrees);
        AttachPlayerToPoint(outroCarSeatPoint != null ? outroCarSeatPoint : introCarTransform, parentToSeat: true, facingDegrees: winCinematicFacingDegrees);
        float carStartDuration = PlayCarStartSfx();
        float carStartSfxEndTime = carStartDuration > 0f ? Time.unscaledTime + carStartDuration : float.NegativeInfinity;
        ApplyPlayerFacingDegrees(winCinematicFacingDegrees);
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, false);

        PlayCarAnimation(closeDoorAnimationState);
        PlayCarDoorCloseSfx();
        if (outroDoorCloseWait > 0f)
            yield return new WaitForSecondsRealtime(outroDoorCloseWait);

        float remainingCarStartWait = carStartSfxEndTime - Time.unscaledTime;
        if (remainingCarStartWait > 0f)
            yield return new WaitForSecondsRealtime(remainingCarStartWait);

        if (outroDriveTarget != null)
            yield return DriveCarToPoint(introCarTransform, outroDriveTarget, outroDriveSpeed, outroDriveAcceleration, outroDriveDeceleration, startAtCruiseSpeed: true, continuePastTarget: true);

        yield return FadeOverlayToBlackForScreen();
        StopAllCarAudio(suppressAutoRestart: true);
        SetEndScreenPointerVisible(true);

        if (gameWinMessageText != null)
            gameWinMessageText.text = ResolveMissionCompletedMessage();

        if (gameWinScreen != null)
            yield return ShowEndScreenWithFade(gameWinScreen, restoreFailureTimeScaleWhenShown: false);
        else
            fadeImageFader?.SetAlphaImmediate(0f);

        PlayJobSuccessRewardsPresentation();
    }

    /// <summary>
    /// Fades to black, shows the requested end screen, and clears the overlay alpha.
    /// </summary>
    private IEnumerator FadeAndShowScreen(GameObject screen, bool restoreFailureTimeScaleWhenShown = false)
    {
        yield return FadeOverlayToBlackForScreen();
        yield return ShowEndScreenWithFade(screen, restoreFailureTimeScaleWhenShown);
    }

    /// <summary>
    /// Shows an end screen and fades its CanvasGroup after the shared black overlay has cleared.
    /// </summary>
    private IEnumerator ShowEndScreenWithFade(GameObject screen, bool restoreFailureTimeScaleWhenShown)
    {
        CanvasGroup screenCanvasGroup = ResolveEndScreenCanvasGroup(screen);
        PrepareEndScreenCanvasGroup(screenCanvasGroup);

        if (screen != null)
            screen.SetActive(true);

        if (restoreFailureTimeScaleWhenShown)
            RestoreFailureTimeScale();

        fadeImageFader?.SetAlphaImmediate(0f);

        yield return FadeEndScreenCanvasGroup(screenCanvasGroup);
    }

    /// <summary>
    /// Resolves the CanvasGroup used to fade a configured end screen.
    /// </summary>
    private CanvasGroup ResolveEndScreenCanvasGroup(GameObject screen)
    {
        if (screen == questFailScreen)
            return questFailScreenCanvasGroup != null ? questFailScreenCanvasGroup : screen != null ? screen.GetComponent<CanvasGroup>() : null;

        if (screen == playerKilledScreen)
            return playerKilledScreenCanvasGroup != null ? playerKilledScreenCanvasGroup : screen != null ? screen.GetComponent<CanvasGroup>() : null;

        if (screen == gameWinScreen)
            return gameWinScreenCanvasGroup != null ? gameWinScreenCanvasGroup : screen != null ? screen.GetComponent<CanvasGroup>() : null;

        return screen != null ? screen.GetComponent<CanvasGroup>() : null;
    }

    /// <summary>
    /// Places an end-screen CanvasGroup in a hidden non-interactive state before activation.
    /// </summary>
    private static void PrepareEndScreenCanvasGroup(CanvasGroup screenCanvasGroup)
    {
        if (screenCanvasGroup == null)
            return;

        screenCanvasGroup.alpha = 0f;
        screenCanvasGroup.interactable = false;
        screenCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Fades an end-screen CanvasGroup in using unscaled time and then enables interaction.
    /// </summary>
    private IEnumerator FadeEndScreenCanvasGroup(CanvasGroup screenCanvasGroup)
    {
        if (screenCanvasGroup == null)
            yield break;

        endScreenFadeTween?.Kill();
        if (endScreenFadeDuration <= 0f)
        {
            CompleteEndScreenFade(screenCanvasGroup);
            yield break;
        }

        endScreenFadeTween = screenCanvasGroup
            .DOFade(1f, endScreenFadeDuration)
            .SetEase(endScreenFadeEase)
            .SetUpdate(true);

        yield return endScreenFadeTween.WaitForCompletion();
        endScreenFadeTween = null;
        CompleteEndScreenFade(screenCanvasGroup);
    }

    /// <summary>
    /// Restores the fully visible and interactive state of an end screen.
    /// </summary>
    private void CompleteEndScreenFade(CanvasGroup screenCanvasGroup)
    {
        endScreenFadeTween = null;
        if (screenCanvasGroup == null)
            return;

        screenCanvasGroup.alpha = 1f;
        screenCanvasGroup.interactable = true;
        screenCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Runs mission failure presentation flow for death or rule-based failure.
    /// </summary>
    private IEnumerator HandleMissionFailedRoutine(
        bool playerWasKilled,
        string screenMessage,
        Transform focusTarget = null,
        bool applyPlayerKilledTint = false)
    {
        if (missionEnded)
            yield break;

        missionEnded = true;
        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse();
        BlockPlayerControls(true);
        BeginFailurePresentation(focusTarget, applyPlayerKilledTint);
        if (TryResolveMissionMusicController())
            missionMusicController.PlayGameOverMusic();

        if (playerWasKilled)
        {
            if (playerKilledMessageText != null)
                playerKilledMessageText.text = string.IsNullOrWhiteSpace(screenMessage) ? ResolvePlayerKilledMessage() : screenMessage;
        }
        else if (questFailMessageText != null)
            questFailMessageText.text = string.IsNullOrWhiteSpace(screenMessage) ? "Mission Failed." : screenMessage;

        float screenDelay = ResolveFailureScreenDelay();
        if (screenDelay > 0f)
            yield return new WaitForSecondsRealtime(screenDelay);

        yield return FadeAndShowScreen(
            playerWasKilled ? playerKilledScreen : questFailScreen,
            restoreFailureTimeScaleWhenShown: true);
        SetEndScreenPointerVisible(true);
    }

    /// <summary>
    /// Flags a failure state as triggered and starts failure handling.
    /// </summary>
    private void TriggerMissionFailure(FailureRuntimeState failureState, Transform focusTarget = null)
    {
        if (failureState == null || GameplayConsoleCheatState.NoFailures)
            return;

        failureState.Triggered = true;
        StartCoroutine(HandleMissionFailedRoutine(
            playerWasKilled: false,
            screenMessage: ResolveFailureScreenMessage(failureState.Definition),
            focusTarget: focusTarget));
    }

    /// <summary>
    /// Registers retry, quit, and continue button callbacks for end screens.
    /// </summary>
    private void RegisterScreenButtonCallbacks()
    {
        if (questFailRetryButton != null)
            questFailRetryButton.onClick.AddListener(RetryCurrentMission);

        if (questFailQuitButton != null)
            questFailQuitButton.onClick.AddListener(QuitToHideout);

        if (playerKilledRetryButton != null)
            playerKilledRetryButton.onClick.AddListener(RetryCurrentMission);

        if (playerKilledQuitButton != null)
            playerKilledQuitButton.onClick.AddListener(QuitToHideout);

        if (gameWinContinueButton != null)
            gameWinContinueButton.onClick.AddListener(ContinueToHideoutAfterWin);
    }

    /// <summary>
    /// Unregisters retry, quit, and continue button callbacks for end screens.
    /// </summary>
    private void UnregisterScreenButtonCallbacks()
    {
        if (questFailRetryButton != null)
            questFailRetryButton.onClick.RemoveListener(RetryCurrentMission);

        if (questFailQuitButton != null)
            questFailQuitButton.onClick.RemoveListener(QuitToHideout);

        if (playerKilledRetryButton != null)
            playerKilledRetryButton.onClick.RemoveListener(RetryCurrentMission);

        if (playerKilledQuitButton != null)
            playerKilledQuitButton.onClick.RemoveListener(QuitToHideout);

        if (gameWinContinueButton != null)
            gameWinContinueButton.onClick.RemoveListener(ContinueToHideoutAfterWin);
    }

    /// <summary>
    /// Fades the shared overlay to black before showing an end screen or changing scenes.
    /// </summary>
    private IEnumerator FadeOverlayToBlackForScreen()
    {
        if (fadeImageFader == null)
            yield break;

        if (fadeImageFader.CurrentAlpha < 0.999f)
        {
            Tween fadeTween = fadeImageFader.FadeIn(screenFadeDuration);
            if (fadeTween != null)
                yield return fadeTween.WaitForCompletion();
        }
        else
            fadeImageFader.SetAlphaImmediate(1f);
    }

    /// <summary>
    /// Fades the shared overlay back out after temporary full-screen black.
    /// </summary>
    private IEnumerator FadeOverlayOutAndRestore()
    {
        if (fadeImageFader == null)
            yield break;

        if (fadeImageFader.CurrentAlpha > 0.001f)
        {
            Tween fadeTween = fadeImageFader.FadeOut(screenFadeDuration);
            if (fadeTween != null)
                yield return fadeTween.WaitForCompletion();
        }
        else
            fadeImageFader.SetAlphaImmediate(0f);
    }

    /// <summary>
    /// Reloads the active mission scene using the prepared replay runtime loadouts.
    /// </summary>
    public void RetryCurrentMission()
    {
        if (sceneTransitionInProgress)
            return;

        PlayerEquipmentRuntimeSession.RestorePreparedQuestLoadoutForReplay();
        PlayerPerkRuntimeSession.RestorePreparedEquippedPerks();

        Scene activeScene = SceneManager.GetActiveScene();
        if (!SceneLoadUtility.CanLoadScene(activeScene.buildIndex, activeScene.name))
            return;

        sceneTransitionInProgress = true;
        StartCoroutine(LoadSceneRoutine(activeScene.buildIndex, activeScene.name, clearCurrentJob: false, completeCurrentJob: false));
    }

    /// <summary>
    /// Returns to the hideout scene without completing the active job.
    /// </summary>
    public void QuitToHideout()
    {
        if (sceneTransitionInProgress || !SceneLoadUtility.IsBuildSceneAvailable(hideoutSceneBuildIndex))
            return;

        sceneTransitionInProgress = true;
        StartCoroutine(LoadSceneRoutine(hideoutSceneBuildIndex, hideoutSceneName, clearCurrentJob: false, completeCurrentJob: false));
    }

    /// <summary>
    /// Returns to the hideout scene and completes the active job first.
    /// </summary>
    public void ContinueToHideoutAfterWin()
    {
        if (sceneTransitionInProgress || !SceneLoadUtility.IsBuildSceneAvailable(hideoutSceneBuildIndex))
            return;

        sceneTransitionInProgress = true;
        StartCoroutine(LoadSceneRoutine(hideoutSceneBuildIndex, hideoutSceneName, clearCurrentJob: false, completeCurrentJob: false));
    }

    /// <summary>
    /// Commits successful job rewards once before their saved values are presented by the win screen.
    /// </summary>
    private void PrepareJobSuccessRewards()
    {
        if (jobCompletionRewardPrepared)
            return;

        jobCompletionRewardPrepared = HideoutRuntimeSession.TryCompleteJob(currentJob, out jobCompletionRewardResult);
        if (gameWinContinueButton != null)
            gameWinContinueButton.interactable = false;
    }

    /// <summary>
    /// Starts the success reward presentation or immediately restores controls when no presenter is configured.
    /// </summary>
    private void PlayJobSuccessRewardsPresentation()
    {
        if (!jobCompletionRewardPrepared || jobSuccessRewardsUi == null)
        {
            EnableWinCompletionControls();
            return;
        }

        jobSuccessRewardsUi.Play(jobCompletionRewardResult, EnableWinCompletionControls);
    }

    /// <summary>
    /// Enables the mission success continuation control after reward presentation finishes.
    /// </summary>
    private void EnableWinCompletionControls()
    {
        if (gameWinContinueButton != null)
            gameWinContinueButton.interactable = true;
    }

    /// <summary>
    /// Fades out, updates hideout runtime job state, and loads the requested scene.
    /// </summary>
    private IEnumerator LoadSceneRoutine(int sceneBuildIndex, string fallbackSceneName, bool clearCurrentJob, bool completeCurrentJob)
    {
        ResetFailurePresentation();

        if (!SceneLoadUtility.CanLoadScene(sceneBuildIndex, fallbackSceneName))
        {
            sceneTransitionInProgress = false;
            yield break;
        }

        if ((questFailScreen != null && questFailScreen.activeInHierarchy) ||
            (playerKilledScreen != null && playerKilledScreen.activeInHierarchy) ||
            (gameWinScreen != null && gameWinScreen.activeInHierarchy))
        {
            fadeImageFader?.SetAlphaImmediate(0f);
        }

        yield return FadeOverlayToBlackForScreen();

        if (completeCurrentJob)
            HideoutRuntimeSession.CompleteJob(currentJob);
        else if (clearCurrentJob)
        {
            HideoutRuntimeSession.ClearCurrentJob();
            HideoutRuntimeSession.ClearActiveMissionJob();
        }
        else if (sceneBuildIndex == hideoutSceneBuildIndex)
            HideoutRuntimeSession.ClearActiveMissionJob();

        if (SceneLoadUtility.TryLoadScene(sceneBuildIndex, fallbackSceneName))
            yield break;

        sceneTransitionInProgress = false;
        Debug.LogWarning($"Could not load scene. Build Index: {sceneBuildIndex}, Fallback Name: {fallbackSceneName}", this);

        yield return FadeOverlayOutAndRestore();
    }

    /// <summary>
    /// Resolves the text shown for a triggered failure definition.
    /// </summary>
    private string ResolveFailureScreenMessage(HideoutJobFailureDefinition definition)
    {
        return definition != null ? definition.FailureScreenMessage : "Mission Failed.";
    }

    /// <summary>
    /// Resolves the text shown when the player is killed or incapacitated.
    /// </summary>
    private string ResolvePlayerKilledMessage()
    {
        return string.IsNullOrWhiteSpace(playerKilledMessage) ? "Game Over." : playerKilledMessage.Trim();
    }

    /// <summary>
    /// Resolves the text shown after mission success.
    /// </summary>
    private string ResolveMissionCompletedMessage()
    {
        return string.IsNullOrWhiteSpace(missionCompletedMessage) ? "Mission Complete." : missionCompletedMessage.Trim();
    }

    /// <summary>
    /// Resets global per-scene runtime state before mission setup begins.
    /// </summary>
    private void ResetSceneScopedRuntimeState()
    {
        ResetFailurePresentation();
        MissionRuntimeEvents.ResetRuntimeState();
        GameplayConsoleCheatState.ResetRuntimeState();
        FocusRevealTarget.ResetRuntimeState();
        SetEndScreenPointerVisible(false);
    }

    /// <summary>
    /// Shows or hides end-screen cursor state and crosshair suppression.
    /// </summary>
    private void SetEndScreenPointerVisible(bool visible)
    {
        DynamicCrosshairUI dynamicCrosshairUi = FindFirstObjectByType<DynamicCrosshairUI>();
        if (dynamicCrosshairUi != null)
            dynamicCrosshairUi.SetUiSuppressed(visible);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = visible;
    }
}

}
