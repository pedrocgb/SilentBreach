using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Breezeblocks.Missions
{

[Serializable]
public sealed class AlarmBoxOutcomeUnityEvent : UnityEvent
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Alarm/Alarm Box Interactable")]
public sealed class AlarmBoxInteractable : PlayerWorldInteractable, ICutWireSessionTarget
{
    private const string DefaultInteractionLabel = "Painel de Alarme";

    [FoldoutGroup("Alarm Box")]
    [SerializeField] private string interactionLabel = DefaultInteractionLabel;

    [FoldoutGroup("Alarm Box"), AssetsOnly]
    [SerializeField] private CutWireMinigameDefinition definition;

    [FoldoutGroup("Alarm Box")]
    [SerializeField] private AlarmController alarmController;

    [FoldoutGroup("Events")]
    [SerializeField] private AlarmBoxOutcomeUnityEvent onDisarmed = new();

    [FoldoutGroup("Events")]
    [SerializeField] private AlarmBoxOutcomeUnityEvent onFailedAttempt = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDisarmed => isDisarmed;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public IReadOnlyList<bool> CutStates => cutStates;

    public CutWireMinigameDefinition Definition => definition;
    public override string InteractionDisplayName => string.IsNullOrWhiteSpace(interactionLabel) ? DefaultInteractionLabel : interactionLabel;

    private bool[] cutStates = Array.Empty<bool>();
    private bool isDisarmed;

    /// <summary>
    /// Initializes runtime wire progress for the alarm box.
    /// </summary>
    private void Awake()
    {
        EnsureCutStateCapacity(clearExisting: true);
    }

    /// <summary>
    /// Normalizes authored labels while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        interactionLabel = string.IsNullOrWhiteSpace(interactionLabel)
            ? DefaultInteractionLabel
            : interactionLabel.Trim();
    }

    /// <summary>
    /// Ensures runtime cut storage before the alarm box becomes interactable.
    /// </summary>
    protected override void OnEnable()
    {
        EnsureCutStateCapacity();
        base.OnEnable();
    }

    /// <summary>
    /// Returns whether this alarm box can open the shared cut-wire minigame.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        return !isDisarmed &&
               definition != null &&
               CutWireController.HasRegisteredInstance &&
               base.CanInteract(interactorRoot);
    }

    /// <summary>
    /// Opens the shared cut-wire minigame for the alarm box.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        EnsureCutStateCapacity(clearExisting: true);
        return CutWireController.TryBeginActiveSession(interactorRoot, this);
    }

    /// <summary>
    /// Stores wire progress while the current cut-wire attempt is active.
    /// </summary>
    public void NotifyWireCut(int wireIndex)
    {
        EnsureCutStateCapacity();
        if (wireIndex >= 0 && wireIndex < cutStates.Length)
            cutStates[wireIndex] = true;
    }

    /// <summary>
    /// Disarms the configured alarm after the cut-wire solution succeeds.
    /// </summary>
    public void NotifySucceeded(GameObject interactorRoot)
    {
        if (isDisarmed)
            return;

        isDisarmed = true;
        alarmController?.DisarmAlarm();
        onDisarmed?.Invoke();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Triggers the configured alarm after a failed cut-wire attempt and prepares a fresh retry.
    /// </summary>
    public void NotifyFailed(GameObject interactorRoot)
    {
        alarmController?.TriggerAlarm();
        onFailedAttempt?.Invoke();
        EnsureCutStateCapacity(clearExisting: true);
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Restores this alarm box to an armed test state.
    /// </summary>
    [Button(ButtonSizes.Small)]
    public void ResetRuntimeState()
    {
        isDisarmed = false;
        EnsureCutStateCapacity(clearExisting: true);
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Resizes runtime cut storage to match the current minigame definition.
    /// </summary>
    private void EnsureCutStateCapacity(bool clearExisting = false)
    {
        int targetCount = definition != null ? definition.WireCount : 0;
        if (!clearExisting && cutStates != null && cutStates.Length == targetCount)
            return;

        bool[] nextStates = new bool[targetCount];
        if (!clearExisting && cutStates != null)
            Array.Copy(cutStates, nextStates, Mathf.Min(cutStates.Length, nextStates.Length));

        cutStates = nextStates;
    }
}

}
