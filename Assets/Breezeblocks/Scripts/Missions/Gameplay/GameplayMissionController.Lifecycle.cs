using System.Collections;
using System.Collections.Generic;
using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Rewired;
using UnityEngine;

namespace Breezeblocks.Missions
{

public partial class GameplayMissionController
{
    /// <summary>
    /// Caches scene references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Prepares mission state, UI, and player state before gameplay begins.
    /// </summary>
    private void Awake()
    {
        EnemyRuntimeBlockedAtMissionStart = true;
        CacheReferences();
        ResetSceneScopedRuntimeState();
        GameplayConsoleController.EnsureOn(gameObject);
        CachePlayerComponentDefaultStates();
        PrepareCarAudio();
        PrepareUiDefaults();
        InitializeJobRuntime();
        SetCollidersEnabled(collidersToEnableAfterGameplayStart, false);
        SetGameObjectsActive(gameObjectsToEnableAfterGameplayStart, false);

        if (missionEscapeTrigger != null)
        {
            missionEscapeTrigger.Bind(this);
            missionEscapeTrigger.SetEscapeEnabled(false);
        }

        BlockPlayerControls(true);
        SetIntroVisionLightActive(false);

        if (playIntroCinematic && CanPlayIntroCinematic())
            ApplyPlayerFacingDegrees(introInitialPlayerFacingDegrees);
    }

    /// <summary>
    /// Subscribes mission runtime listeners and end-screen callbacks.
    /// </summary>
    private void OnEnable()
    {
        EnemyRuntimeBlockedAtMissionStart = true;
        MissionRuntimeEvents.ActorKilled += HandleActorKilled;
        MissionRuntimeEvents.ActorIncapacitated += HandleActorIncapacitated;
        MissionRuntimeEvents.ItemPickedUp += HandleItemPickedUp;
        MissionRuntimeEvents.EnemyStateChanged += HandleEnemyStateChanged;
        MissionRuntimeEvents.EnemyPlayerFullyDetected += HandleEnemyPlayerFullyDetected;
        RegisterScreenButtonCallbacks();

        if (playerHealth != null)
        {
            playerHealth.Died += HandlePlayerDied;
            playerHealth.Incapacitated += HandlePlayerIncapacitated;
        }
    }

    /// <summary>
    /// Starts mission startup flow after scene initialization completes.
    /// </summary>
    private void Start()
    {
        RestartMissionStatusEntryBuild();
        startupSequenceRoutine = StartCoroutine(BeginMissionStartupRoutine());
    }

    /// <summary>
    /// Advances intro skip polling, time-limit checks, and car audio upkeep.
    /// </summary>
    private void Update()
    {
        if (introRoutine != null && introSkipRoutine == null)
            TryHandleIntroSkipInput();

        if (!missionEnded && gameplayStarted)
            UpdateTimeLimitFailures(Time.deltaTime);

        EnsureCarIdleLoopRunning();
        ApplyCarLoopVolumes();
        RefreshTimeLimitUi();
    }

    /// <summary>
    /// Unsubscribes listeners and stops active runtime sequences when disabled.
    /// </summary>
    private void OnDisable()
    {
        EnemyRuntimeBlockedAtMissionStart = false;
        MissionRuntimeEvents.ActorKilled -= HandleActorKilled;
        MissionRuntimeEvents.ActorIncapacitated -= HandleActorIncapacitated;
        MissionRuntimeEvents.ItemPickedUp -= HandleItemPickedUp;
        MissionRuntimeEvents.EnemyStateChanged -= HandleEnemyStateChanged;
        MissionRuntimeEvents.EnemyPlayerFullyDetected -= HandleEnemyPlayerFullyDetected;
        UnregisterScreenButtonCallbacks();

        if (playerHealth != null)
        {
            playerHealth.Died -= HandlePlayerDied;
            playerHealth.Incapacitated -= HandlePlayerIncapacitated;
        }

        escapePromptSequence?.Kill();
        escapePromptSequence = null;
        StopTimeLimitWarningPulse(resetScale: false);
        carEngineLoopTween?.Kill();
        carEngineLoopTween = null;
        activeCinematicPlayerMoveTween?.Kill();
        activeCinematicPlayerMoveTween = null;

        if (startupSequenceRoutine != null)
        {
            StopCoroutine(startupSequenceRoutine);
            startupSequenceRoutine = null;
        }

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        if (introSkipRoutine != null)
        {
            StopCoroutine(introSkipRoutine);
            introSkipRoutine = null;
        }

        if (missionStatusEntryBuildRoutine != null)
        {
            StopCoroutine(missionStatusEntryBuildRoutine);
            missionStatusEntryBuildRoutine = null;
        }

        StopContinuousCarDrive();
        StopAllCarAudio(suppressAutoRestart: false);
        ClearMissionStatusEntries();

        if (playerVisionLight != null)
            playerVisionLight.enabled = playerVisionLightDefaultEnabled;

        if (playerFocusController != null)
            playerFocusController.enabled = playerFocusControllerDefaultEnabled;
    }

    /// <summary>
    /// Handles player arrival at mission escape trigger.
    /// </summary>
    public void TryHandleEscapeTrigger(GameObject enteringRoot)
    {
        if (!objectivesCompleted || missionEnded || !IsPlayerRoot(enteringRoot))
            return;

        StartCoroutine(PlayWinRoutine());
    }

    /// <summary>
    /// Applies mission music cue only for valid active player triggers.
    /// </summary>
    public bool TryHandleMusicTrigger(GameObject enteringRoot, MissionMusicCue cue)
    {
        if (missionEnded || !gameplayStarted || !IsPlayerRoot(enteringRoot) || missionMusicController == null)
            return false;

        switch (cue)
        {
            case MissionMusicCue.Lurking:
                missionMusicController.PlayLurkingMusic();
                return true;
            case MissionMusicCue.Alerted:
                missionMusicController.PlayAlertedMusic();
                return true;
            case MissionMusicCue.GameOver:
                missionMusicController.PlayGameOverMusic();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Builds objective and failure runtime state from current job asset.
    /// </summary>
    private void InitializeJobRuntime()
    {
        currentJob = ResolveCurrentJob();
        objectiveStates.Clear();
        failureStates.Clear();

        if (currentJob != null)
        {
            IReadOnlyList<HideoutJobObjectiveDefinition> objectives = currentJob.GameplayObjectives;
            for (int i = 0; i < objectives.Count; i++)
            {
                HideoutJobObjectiveDefinition definition = objectives[i];
                if (definition == null)
                    continue;

                objectiveStates.Add(new ObjectiveRuntimeState { Definition = definition });
            }

            IReadOnlyList<HideoutJobFailureDefinition> failures = currentJob.GameplayFailures;
            for (int i = 0; i < failures.Count; i++)
            {
                HideoutJobFailureDefinition definition = failures[i];
                if (definition == null)
                    continue;

                failureStates.Add(new FailureRuntimeState
                {
                    Definition = definition,
                    TimeRemaining = definition.FailureType == HideoutJobFailureType.TimeLimit ? definition.TimeLimitSeconds : 0f
                });
            }
        }

        objectivesCompleted = objectiveStates.Count == 0;
        RefreshMissionTexts();
        RefreshTimeLimitUi();

        if (objectivesCompleted)
            HandleAllObjectivesCompleted();
    }

    /// <summary>
    /// Resolves current mission job from runtime session or fallback asset.
    /// </summary>
    private HideoutJobDefinition ResolveCurrentJob()
    {
        HideoutJobDefinition runtimeJob = HideoutRuntimeSession.CurrentJob;
        HideoutJobDefinition resolvedJob = runtimeJob != null ? runtimeJob : fallbackMission;
        if (resolvedJob != null && resolvedJob != runtimeJob)
            HideoutRuntimeSession.SetCurrentJob(resolvedJob);

        if (resolvedJob != null)
            HideoutRuntimeSession.SetActiveMissionJob(resolvedJob);

        return resolvedJob;
    }

    /// <summary>
    /// Resolves mission, player, and runtime service references.
    /// </summary>
    private void CacheReferences()
    {
        if (playerRoot == null && playerMotor != null)
            playerRoot = playerMotor.transform;

        if (playerRoot == null && playerHealth != null)
            playerRoot = playerHealth.transform;

        if (playerRoot == null && playerWeaponController != null)
            playerRoot = playerWeaponController.transform;

        if (playerRoot == null)
        {
            PlayerTopDownMotor2D foundMotor = FindFirstObjectByType<PlayerTopDownMotor2D>();
            if (foundMotor != null)
                playerRoot = foundMotor.transform;
        }

        if (playerRoot == null)
            return;

        if (playerBody == null)
            playerBody = playerRoot.GetComponent<Rigidbody2D>();

        if (playerMotor == null)
            playerMotor = playerRoot.GetComponent<PlayerTopDownMotor2D>();

        if (playerVisionLight == null)
            playerVisionLight = playerRoot.GetComponentInChildren<PlayerVisionLight>(true);

        if (playerEquipmentController == null)
            playerEquipmentController = playerRoot.GetComponent<PlayerEquipmentController>();

        if (playerWeaponController == null)
            playerWeaponController = playerRoot.GetComponent<PlayerWeaponController>();

        if (playerUtilityController == null)
            playerUtilityController = playerRoot.GetComponent<PlayerUtilityController>();

        if (playerMeleeController == null)
            playerMeleeController = playerRoot.GetComponent<PlayerMeleeController>();

        if (playerPickupInteractor == null)
            playerPickupInteractor = playerRoot.GetComponent<PlayerPickupInteractor>();

        if (playerFocusController == null)
            playerFocusController = playerRoot.GetComponent<PlayerFocusController>();

        if (playerHealth == null)
            playerHealth = playerRoot.GetComponent<ActorHealth>();

        if (missionMusicController == null)
            missionMusicController = GetComponent<MissionMusicController>();

        if (missionMusicController == null)
            missionMusicController = FindFirstObjectByType<MissionMusicController>();

        if (gameplayHudController == null)
            gameplayHudController = FindFirstObjectByType<GameplayHudController>();

        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        ResolveRewiredPlayer();
    }

    /// <summary>
    /// Caches default enabled states for player components restored after cinematics.
    /// </summary>
    private void CachePlayerComponentDefaultStates()
    {
        if (playerComponentDefaultStatesCached)
            return;

        playerVisionLightDefaultEnabled = playerVisionLight != null && playerVisionLight.enabled;
        playerFocusControllerDefaultEnabled = playerFocusController != null && playerFocusController.enabled;
        playerComponentDefaultStatesCached = true;
    }

    /// <summary>
    /// Prepares mission UI to known initial state.
    /// </summary>
    private void PrepareUiDefaults()
    {
        if (fadeImageFader != null)
            fadeImageFader.SetAlphaImmediate(1f);

        RegisterMissionStatusEntryPrefabs();
        ClearMissionStatusEntries();

        if (questFailScreen != null)
            questFailScreen.SetActive(false);

        if (playerKilledScreen != null)
            playerKilledScreen.SetActive(false);

        if (gameWinScreen != null)
            gameWinScreen.SetActive(false);

        if (questFailMessageText != null)
            questFailMessageText.text = string.Empty;

        if (playerKilledMessageText != null)
            playerKilledMessageText.text = ResolvePlayerKilledMessage();

        if (gameWinMessageText != null)
            gameWinMessageText.text = ResolveMissionCompletedMessage();

        if (escapeNowText != null)
        {
            escapeNowText.gameObject.SetActive(false);
            SetTextAlpha(escapeNowText, 0f);
            escapeNowText.rectTransform.localScale = Vector3.one;
        }

        if (timerContent != null)
        {
            timerContent.SetActive(false);
            timeLimitDefaultColor = timeLimitText.color;
            timeLimitText.text = string.Empty;
            timeLimitText.color = timeLimitDefaultColor;
            timeLimitText.rectTransform.localScale = Vector3.one;
        }

        if (UseMissionStatusEntryList)
        {
            if (jobObjectivesText != null)
                jobObjectivesText.text = string.Empty;

            if (jobFailureText != null)
                jobFailureText.text = string.Empty;
        }
    }

    /// <summary>
    /// Validates car audio settings and creates loop sources when car exists.
    /// </summary>
    private void PrepareCarAudio()
    {
        suppressCarAudioAutoRestart = false;
        carDoorOpenSfx ??= new AudioClipSet();
        carDoorOpenSfx.Validate();
        carDoorCloseSfx ??= new AudioClipSet();
        carDoorCloseSfx.Validate();
        carStartSfx ??= new AudioClipSet();
        carStartSfx.Validate();
        carEngineLoopSfx ??= new AudioClipSet();
        carEngineLoopSfx.Validate();
        carIdleLoopSfx ??= new AudioClipSet();
        carIdleLoopSfx.Validate();

        carLoopSpatialBlend = Mathf.Clamp01(carLoopSpatialBlend);
        carLoopMinDistance = Mathf.Max(0f, carLoopMinDistance);
        carLoopMaxDistance = Mathf.Max(carLoopMinDistance, carLoopMaxDistance);
        carLoopDopplerLevel = Mathf.Max(0f, carLoopDopplerLevel);
        carLoopSpread = Mathf.Clamp(carLoopSpread, 0f, 360f);
        carLoopPriority = Mathf.Clamp(carLoopPriority, 0, 256);

        if (introCarTransform == null)
            return;

        carIdleLoopSource = EnsureCarLoopSource(carIdleLoopSource, "Car Idle Loop Source", carIdleLoopLocalOffset);
        carEngineLoopSource = EnsureCarLoopSource(carEngineLoopSource, "Car Engine Loop Source", carEngineLoopLocalOffset);
        EnsureCarIdleLoopRunning();
    }

    /// <summary>
    /// Resolves shared world SFX service when needed.
    /// </summary>
    private void ResolveWorldSfxManager()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;
    }

    /// <summary>
    /// Resolves configured Rewired player for intro-skip input.
    /// </summary>
    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }

    /// <summary>
    /// Blocks or restores player interaction subsystems during mission cinematics and screens.
    /// </summary>
    private void BlockPlayerControls(bool blocked)
    {
        if (playerEquipmentController != null)
        {
            playerEquipmentController.SetInputBlocked(blocked);
            if (blocked)
                playerEquipmentController.SetEquipmentPanelVisible(false);
        }

        playerMotor?.SetInputBlocked(blocked);
        playerVisionLight?.SetInputBlocked(blocked);
        playerWeaponController?.SetInputBlocked(blocked);
        playerUtilityController?.SetInputBlocked(blocked);
        playerMeleeController?.SetInputBlocked(blocked);
        playerPickupInteractor?.SetInputBlocked(blocked);

        if (playerFocusController != null)
        {
            if (!blocked)
                playerFocusController.enabled = playerFocusControllerDefaultEnabled;

            playerFocusController.SetInputBlocked(blocked);

            if (blocked)
                playerFocusController.enabled = false;
        }

        if (blocked && playerBody != null)
            playerBody.linearVelocity = Vector2.zero;

        if (!blocked && playerVisionLight != null)
            playerVisionLight.DriveMouseLook(playerVisionLight.RotationSmoothing, 0f);
    }

    /// <summary>
    /// Applies intro-specific visibility light enablement.
    /// </summary>
    private void SetIntroVisionLightActive(bool active)
    {
        if (playerVisionLight == null)
            return;

        playerVisionLight.enabled = active && playerVisionLightDefaultEnabled;
    }

    /// <summary>
    /// Rotates local up vector by angle in degrees.
    /// </summary>
    private static Vector2 RotateUpByDegrees(float degrees)
    {
        return (Quaternion.Euler(0f, 0f, degrees) * Vector2.up).normalized;
    }
}

}
