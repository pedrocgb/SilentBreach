using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Breezeblocks.Missions
{

public partial class GameplayMissionController
{
    /// <summary>
    /// Handles actor-killed mission event and updates objective or failure state.
    /// </summary>
    private void HandleActorKilled(MissionActorEvent actorEvent)
    {
        if (missionEnded)
            return;

        RegisterObjectiveProgress(actorEvent, HideoutJobObjectiveType.KillTarget);

        if (IsPlayerInstigator(actorEvent.InstigatorRoot))
            EvaluateFailureRulesForActorEvent(actorEvent, harmed: true, killed: true);
    }

    /// <summary>
    /// Handles actor-incapacitated mission event and updates objective or failure state.
    /// </summary>
    private void HandleActorIncapacitated(MissionActorEvent actorEvent)
    {
        if (missionEnded)
            return;

        RegisterObjectiveProgress(actorEvent, HideoutJobObjectiveType.IncapacitateTarget);

        if (IsPlayerInstigator(actorEvent.InstigatorRoot))
            EvaluateFailureRulesForActorEvent(actorEvent, harmed: true, killed: false);
    }

    /// <summary>
    /// Handles mission pickup events for retrieve-item objectives.
    /// </summary>
    private void HandleItemPickedUp(MissionPickupEvent pickupEvent)
    {
        if (missionEnded || !IsPlayerRoot(pickupEvent.PickerRoot))
            return;

        if (pickupEvent.PickableItem == null)
            return;

        int sourceId = pickupEvent.PickableItem.GetInstanceID();
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null ||
                state.Definition == null ||
                state.Definition.ObjectiveType != HideoutJobObjectiveType.RetrieveItem ||
                state.IsComplete ||
                state.CountedSourceIds.Contains(sourceId) ||
                !MatchesReferenceId(state.Definition.ReferenceId, pickupEvent.ItemId))
            {
                continue;
            }

            state.CountedSourceIds.Add(sourceId);
            state.CompletedCount = Mathf.Min(state.CompletedCount + 1, state.RequiredCount);
        }

        RefreshObjectivesAndEscapeState();
    }

    /// <summary>
    /// Handles enemy alert-state entry for alert-based failure rules.
    /// </summary>
    private void HandleEnemyStateChanged(EnemyStateChangedEvent stateEvent)
    {
        if (missionEnded || !gameplayStarted)
            return;

        bool enteredAlertState = stateEvent.NewState == EnemyState.Alert && stateEvent.PreviousState != EnemyState.Alert;
        if (!enteredAlertState)
            return;

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState failureState = failureStates[i];
            if (failureState == null || failureState.Triggered || failureState.Definition == null)
                continue;

            if (failureState.Definition.FailureType != HideoutJobFailureType.DontAlert)
                continue;

            TriggerMissionFailure(failureState);
            return;
        }
    }

    /// <summary>
    /// Handles full player detection for detection-based failure rules.
    /// </summary>
    private void HandleEnemyPlayerFullyDetected(EnemyVisualDetectionEvent detectionEvent)
    {
        if (missionEnded || !gameplayStarted)
            return;

        if (TryResolveMissionMusicController())
            missionMusicController.PlayAlertedMusic();

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState failureState = failureStates[i];
            if (failureState == null || failureState.Triggered || failureState.Definition == null)
                continue;

            if (failureState.Definition.FailureType != HideoutJobFailureType.DontBeDetected)
                continue;

            TriggerMissionFailure(failureState);
            return;
        }
    }

    /// <summary>
    /// Handles player death by starting mission failure flow.
    /// </summary>
    private void HandlePlayerDied(ActorDamageContext context)
    {
        if (missionEnded)
            return;

        StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: true, screenMessage: ResolvePlayerKilledMessage()));
    }

    /// <summary>
    /// Handles player incapacitation by starting mission failure flow.
    /// </summary>
    private void HandlePlayerIncapacitated(ActorDamageContext context)
    {
        if (missionEnded)
            return;

        StartCoroutine(HandleMissionFailedRoutine(playerWasKilled: true, screenMessage: ResolvePlayerKilledMessage()));
    }

    /// <summary>
    /// Registers objective progress for mission actor events that match objective rules.
    /// </summary>
    private void RegisterObjectiveProgress(MissionActorEvent actorEvent, HideoutJobObjectiveType objectiveType)
    {
        int sourceId = actorEvent.ActorHealth != null ? actorEvent.ActorHealth.GetInstanceID() : 0;
        string actorId = actorEvent.Identity != null ? actorEvent.Identity.ActorId : string.Empty;

        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null ||
                state.Definition == null ||
                state.Definition.ObjectiveType != objectiveType ||
                state.IsComplete ||
                (sourceId != 0 && state.CountedSourceIds.Contains(sourceId)) ||
                !MatchesReferenceId(state.Definition.ReferenceId, actorId))
            {
                continue;
            }

            if (sourceId != 0)
                state.CountedSourceIds.Add(sourceId);

            state.CompletedCount = Mathf.Min(state.CompletedCount + 1, state.RequiredCount);
        }

        RefreshObjectivesAndEscapeState();
    }

    /// <summary>
    /// Evaluates actor-based failure rules after player harms or kills someone.
    /// </summary>
    private void EvaluateFailureRulesForActorEvent(MissionActorEvent actorEvent, bool harmed, bool killed)
    {
        if (actorEvent.ActorHealth == null || actorEvent.ActorHealth == playerHealth)
            return;

        MissionActorIdentity identity = actorEvent.Identity;
        bool isInnocent = identity != null && identity.IsInnocent;

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState failureState = failureStates[i];
            if (failureState == null || failureState.Triggered || failureState.Definition == null)
                continue;

            bool shouldTrigger = failureState.Definition.FailureType switch
            {
                HideoutJobFailureType.DontHarmInnocent => harmed && isInnocent,
                HideoutJobFailureType.DontKillInnocent => killed && isInnocent,
                HideoutJobFailureType.DontHarmAnyone => harmed,
                HideoutJobFailureType.DontKillAnyone => killed,
                _ => false
            };

            if (!shouldTrigger)
                continue;

            TriggerMissionFailure(failureState);
            return;
        }
    }

    /// <summary>
    /// Recomputes objective completion and refreshes escape state when mission progress changes.
    /// </summary>
    private void RefreshObjectivesAndEscapeState()
    {
        if (missionEnded)
            return;

        bool wereObjectivesCompleted = objectivesCompleted;
        objectivesCompleted = AreAllObjectivesComplete();
        RefreshMissionTexts();

        if (!wereObjectivesCompleted && objectivesCompleted)
            HandleAllObjectivesCompleted();
    }

    /// <summary>
    /// Returns whether every objective runtime state is complete.
    /// </summary>
    private bool AreAllObjectivesComplete()
    {
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            if (!objectiveStates[i].IsComplete)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Enables escape state and blinking prompt after all objectives complete.
    /// </summary>
    private void HandleAllObjectivesCompleted()
    {
        if (!objectivesCompleted || !gameplayStarted)
            return;

        if (missionEscapeTrigger != null)
            missionEscapeTrigger.SetEscapeEnabled(true);

        if (escapeNowText == null)
            return;

        escapeNowText.gameObject.SetActive(true);
        escapePromptSequence?.Kill();
        SetTextAlpha(escapeNowText, 1f);
        escapeNowText.rectTransform.localScale = Vector3.one;
        escapePromptSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(escapeNowText.rectTransform.DOScale(1.06f, 0.55f).SetEase(Ease.InOutSine))
            .Join(DOTween.ToAlpha(() => escapeNowText.color, color => escapeNowText.color = color, 0.45f, 0.55f))
            .Append(escapeNowText.rectTransform.DOScale(1f, 0.55f).SetEase(Ease.InOutSine))
            .Join(DOTween.ToAlpha(() => escapeNowText.color, color => escapeNowText.color = color, 1f, 0.55f))
            .SetLoops(-1, LoopType.Restart);
    }

    /// <summary>
    /// Refreshes mission objective and failure text presentation.
    /// </summary>
    private void RefreshMissionTexts()
    {
        if (jobNameText != null)
            jobNameText.text = currentJob != null ? currentJob.JobTitle : string.Empty;

        if (jobTitleText != null)
            jobTitleText.text = currentJob != null ? currentJob.JobTitle : string.Empty;

        if (UseMissionStatusEntryList)
        {
            if (jobObjectivesText != null)
                jobObjectivesText.text = string.Empty;

            if (jobFailureText != null)
                jobFailureText.text = string.Empty;

            RefreshMissionStatusEntriesFromStates();
            return;
        }

        if (jobObjectivesText != null)
            jobObjectivesText.text = BuildObjectiveText();

        if (jobFailureText != null)
            jobFailureText.text = BuildFailureText();
    }

    /// <summary>
    /// Registers pooled mission status entry prefabs with shared pooler.
    /// </summary>
    private void RegisterMissionStatusEntryPrefabs()
    {
        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        if (globalObjectPooler == null)
            return;

        if (objectiveStatusEntryPrefab != null)
            globalObjectPooler.RegisterPrefab(objectiveStatusEntryPrefab.gameObject);

        if (failureStatusEntryPrefab != null)
            globalObjectPooler.RegisterPrefab(failureStatusEntryPrefab.gameObject);
    }

    /// <summary>
    /// Rebuilds pooled mission status list when mission view should use entry prefabs.
    /// </summary>
    private void RestartMissionStatusEntryBuild()
    {
        if (missionStatusEntryBuildRoutine != null)
        {
            StopCoroutine(missionStatusEntryBuildRoutine);
            missionStatusEntryBuildRoutine = null;
        }

        ClearMissionStatusEntries();

        if (!UseMissionStatusEntryList || !isActiveAndEnabled)
            return;

        missionStatusEntryBuildRoutine = StartCoroutine(BuildMissionStatusEntriesRoutine());
    }

    /// <summary>
    /// Spawns objective and failure status entries in display order.
    /// </summary>
    private IEnumerator BuildMissionStatusEntriesRoutine()
    {
        bool spawnedAnyObjectives = false;
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null || state.Definition == null)
                continue;

            spawnedAnyObjectives = true;
            state.EntryView = SpawnMissionStatusEntry(
                objectiveStatusEntryPrefab,
                BuildObjectiveLine(state, applyStrikethrough: false),
                state.IsComplete);

            yield return WaitForMissionStatusEntry(state.EntryView);
        }

        if (!spawnedAnyObjectives && currentJob != null && !string.IsNullOrWhiteSpace(currentJob.ObjectivesText))
            yield return WaitForMissionStatusEntry(SpawnMissionStatusEntry(objectiveStatusEntryPrefab, currentJob.ObjectivesText, useStrikethrough: false));

        bool spawnedAnyFailures = false;
        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Definition == null)
                continue;

            spawnedAnyFailures = true;
            state.EntryView = SpawnMissionStatusEntry(
                failureStatusEntryPrefab,
                state.Definition.DisplayText,
                useStrikethrough: false);

            yield return WaitForMissionStatusEntry(state.EntryView);
        }

        if (!spawnedAnyFailures && currentJob != null && !string.IsNullOrWhiteSpace(currentJob.TermsOfFailureText))
            yield return WaitForMissionStatusEntry(SpawnMissionStatusEntry(failureStatusEntryPrefab, currentJob.TermsOfFailureText, useStrikethrough: false));

        missionStatusEntryBuildRoutine = null;
    }

    /// <summary>
    /// Waits for entry fade-in and configured spacing before next entry spawns.
    /// </summary>
    private IEnumerator WaitForMissionStatusEntry(MissionStatusEntryUI entryView)
    {
        DG.Tweening.Tween fadeTween = entryView != null
            ? entryView.PlayFadeIn(missionStatusEntryFadeDuration)
            : null;

        if (fadeTween != null)
            yield return fadeTween.WaitForCompletion();

        if (missionStatusEntrySpawnInterval > 0f)
            yield return new WaitForSecondsRealtime(missionStatusEntrySpawnInterval);
    }

    /// <summary>
    /// Spawns one mission status entry from pool or instantiates fallback.
    /// </summary>
    private MissionStatusEntryUI SpawnMissionStatusEntry(MissionStatusEntryUI prefab, string text, bool useStrikethrough)
    {
        if (prefab == null || missionStatusContentRoot == null)
            return null;

        MissionStatusEntryUI entryView = null;
        if (globalObjectPooler != null)
            entryView = globalObjectPooler.Spawn(prefab, Vector3.zero, Quaternion.identity, missionStatusContentRoot);

        if (entryView == null)
            entryView = Instantiate(prefab, missionStatusContentRoot);

        if (entryView == null)
            return null;

        entryView.transform.SetParent(missionStatusContentRoot, false);
        entryView.transform.SetAsLastSibling();
        entryView.PrepareForDisplay();
        entryView.SetText(text, useStrikethrough);
        activeMissionStatusEntries.Add(entryView);
        return entryView;
    }

    /// <summary>
    /// Clears spawned mission status entries and detaches them from runtime states.
    /// </summary>
    private void ClearMissionStatusEntries()
    {
        for (int i = 0; i < activeMissionStatusEntries.Count; i++)
        {
            MissionStatusEntryUI entryView = activeMissionStatusEntries[i];
            if (entryView == null)
                continue;

            GlobalPooledObject pooledObject = entryView.GetComponent<GlobalPooledObject>();
            if (pooledObject != null)
            {
                pooledObject.ReturnToPool();
                continue;
            }

            Destroy(entryView.gameObject);
        }

        activeMissionStatusEntries.Clear();

        for (int i = 0; i < objectiveStates.Count; i++)
        {
            if (objectiveStates[i] != null)
                objectiveStates[i].EntryView = null;
        }

        for (int i = 0; i < failureStates.Count; i++)
        {
            if (failureStates[i] != null)
                failureStates[i].EntryView = null;
        }
    }

    /// <summary>
    /// Refreshes existing mission status entries from objective and failure runtime state.
    /// </summary>
    private void RefreshMissionStatusEntriesFromStates()
    {
        if (!UseMissionStatusEntryList)
            return;

        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state?.EntryView == null)
                continue;

            state.EntryView.SetText(BuildObjectiveLine(state, applyStrikethrough: false), state.IsComplete);
        }

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state?.EntryView == null || state.Definition == null)
                continue;

            state.EntryView.SetText(state.Definition.DisplayText, useStrikethrough: false);
        }
    }

    /// <summary>
    /// Builds fallback objective text block when list-entry UI not used.
    /// </summary>
    private string BuildObjectiveText()
    {
        if (objectiveStates.Count == 0)
            return currentJob != null ? currentJob.ObjectivesText : string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < objectiveStates.Count; i++)
        {
            ObjectiveRuntimeState state = objectiveStates[i];
            if (state == null || state.Definition == null)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            string line = BuildObjectiveLine(state, applyStrikethrough: true);
            builder.Append("- ");
            builder.Append(line);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds fallback failure text block when list-entry UI not used.
    /// </summary>
    private string BuildFailureText()
    {
        if (failureStates.Count == 0)
            return currentJob != null ? currentJob.TermsOfFailureText : string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Definition == null)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append("- ");
            builder.Append(state.Definition.DisplayText);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds one objective line with optional progress text and strike-through markup.
    /// </summary>
    private string BuildObjectiveLine(ObjectiveRuntimeState state, bool applyStrikethrough)
    {
        if (state == null || state.Definition == null)
            return string.Empty;

        string line = state.DisplayText;
        if (state.RequiredCount > 1)
            line = $"{line} ({Mathf.Min(state.CompletedCount, state.RequiredCount)}/{state.RequiredCount})";

        if (applyStrikethrough && state.IsComplete)
            line = $"<s>{line}</s>";

        return line;
    }

    /// <summary>
    /// Decrements active time-limit failures and triggers failure when timer expires.
    /// </summary>
    private void UpdateTimeLimitFailures(float deltaTime)
    {
        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Triggered || state.Definition == null || state.Definition.FailureType != HideoutJobFailureType.TimeLimit)
                continue;

            state.TimeRemaining = Mathf.Max(0f, state.TimeRemaining - deltaTime);
            if (state.TimeRemaining > 0f)
                continue;

            TriggerMissionFailure(state);
            return;
        }
    }

    /// <summary>
    /// Refreshes time-limit timer visibility, text, and warning visuals.
    /// </summary>
    private void RefreshTimeLimitUi()
    {
        if (timerContent == null)
            return;

        FailureRuntimeState activeTimeLimit = GetActiveTimeLimitFailure();
        if (activeTimeLimit == null)
        {
            timerContent.gameObject.SetActive(false);
            timeLimitText.text = string.Empty;
            timeLimitText.color = timeLimitDefaultColor;
            StopTimeLimitWarningPulse();
            return;
        }

        timerContent.gameObject.SetActive(true);
        float remainingTime = Mathf.Max(0f, activeTimeLimit.TimeRemaining);
        timeLimitText.text = FormatTimeLimitText(remainingTime);

        bool useWarningVisuals = remainingTime <= Mathf.Max(0f, timeLimitWarningThresholdSeconds);
        if (useWarningVisuals)
        {
            timeLimitText.color = timeLimitWarningColor;
            StartTimeLimitWarningPulse();
        }
        else
        {
            timeLimitText.color = timeLimitDefaultColor;
            StopTimeLimitWarningPulse();
        }
    }

    /// <summary>
    /// Returns active time-limit failure with lowest remaining time.
    /// </summary>
    private FailureRuntimeState GetActiveTimeLimitFailure()
    {
        FailureRuntimeState activeState = null;
        float lowestRemainingTime = float.PositiveInfinity;

        for (int i = 0; i < failureStates.Count; i++)
        {
            FailureRuntimeState state = failureStates[i];
            if (state == null || state.Definition == null || state.Definition.FailureType != HideoutJobFailureType.TimeLimit || state.Triggered)
                continue;

            if (state.TimeRemaining >= lowestRemainingTime)
                continue;

            lowestRemainingTime = state.TimeRemaining;
            activeState = state;
        }

        return activeState;
    }

    /// <summary>
    /// Formats remaining time using minutes, seconds, and optional milliseconds.
    /// </summary>
    private string FormatTimeLimitText(float remainingTime)
    {
        remainingTime = Mathf.Max(0f, remainingTime);
        bool showMilliseconds = remainingTime <= Mathf.Max(0f, timeLimitMillisecondsThresholdSeconds);
        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime) % 60;

        if (!showMilliseconds)
            return $"{minutes:00}:{seconds:00}";

        int milliseconds = Mathf.Clamp(Mathf.FloorToInt((remainingTime - Mathf.Floor(remainingTime)) * 1000f), 0, 999);
        return $"{minutes:00}:{seconds:00}:{milliseconds:000}";
    }

    /// <summary>
    /// Starts looping pulse animation for time-limit warning state.
    /// </summary>
    private void StartTimeLimitWarningPulse()
    {
        if (timerContent == null || timeLimitWarningSequence != null)
            return;

        timeLimitText.rectTransform.localScale = Vector3.one;
        timeLimitWarningSequence = DOTween.Sequence()
            .SetUpdate(true)
            .Append(timeLimitText.rectTransform.DOScale(timeLimitWarningPulseScale, timeLimitWarningPulseDuration).SetEase(Ease.InOutSine))
            .Append(timeLimitText.rectTransform.DOScale(1f, timeLimitWarningPulseDuration).SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Restart);
    }

    /// <summary>
    /// Stops time-limit warning pulse and optionally resets text scale.
    /// </summary>
    private void StopTimeLimitWarningPulse(bool resetScale = true)
    {
        timeLimitWarningSequence?.Kill();
        timeLimitWarningSequence = null;

        if (resetScale && timeLimitText != null)
            timeLimitText.rectTransform.localScale = Vector3.one;
    }

    /// <summary>
    /// Resolves mission music controller if not already cached.
    /// </summary>
    private bool TryResolveMissionMusicController()
    {
        if (missionMusicController == null)
            missionMusicController = GetComponent<MissionMusicController>();

        if (missionMusicController == null)
            missionMusicController = FindFirstObjectByType<MissionMusicController>();

        return missionMusicController != null;
    }

    /// <summary>
    /// Returns whether candidate instigator belongs to player root.
    /// </summary>
    private bool IsPlayerInstigator(GameObject instigatorRoot)
    {
        return IsPlayerRoot(instigatorRoot);
    }

    /// <summary>
    /// Returns whether candidate object belongs to cached player root.
    /// </summary>
    private bool IsPlayerRoot(GameObject candidateRoot)
    {
        return candidateRoot != null &&
               playerRoot != null &&
               candidateRoot.transform.root == playerRoot.root;
    }

    /// <summary>
    /// Compares two mission reference ids using trimmed case-insensitive match.
    /// </summary>
    private static bool MatchesReferenceId(string expectedId, string actualId)
    {
        if (string.IsNullOrWhiteSpace(expectedId))
            return true;

        if (string.IsNullOrWhiteSpace(actualId))
            return false;

        return string.Equals(expectedId.Trim(), actualId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Enables or disables each collider in provided list.
    /// </summary>
    private static void SetCollidersEnabled(IReadOnlyList<Collider2D> colliders, bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    /// <summary>
    /// Enables or disables each game object in provided list.
    /// </summary>
    private static void SetGameObjectsActive(IReadOnlyList<GameObject> gameObjects, bool active)
    {
        if (gameObjects == null)
            return;

        for (int i = 0; i < gameObjects.Count; i++)
        {
            if (gameObjects[i] != null)
                gameObjects[i].SetActive(active);
        }
    }

    /// <summary>
    /// Sets text alpha while preserving existing RGB channels.
    /// </summary>
    private static void SetTextAlpha(TMP_Text textField, float alpha)
    {
        if (textField == null)
            return;

        Color color = textField.color;
        color.a = Mathf.Clamp01(alpha);
        textField.color = color;
    }
}

}
