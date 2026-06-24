using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

public enum DoorButtonAction
{
    ToggleLock,
    ToggleOpen
}

[Serializable]
public sealed class DoorButtonTarget
{
    [Required]
    [SerializeField] private DoorInteractable door;

    [SerializeField] private DoorButtonAction action = DoorButtonAction.ToggleOpen;

    [NonSerialized] private DoorLockState doorLockState;

    /// <summary>
    /// Caches optional lock state from configured door target.
    /// </summary>
    public void CacheReferences()
    {
        doorLockState = door != null ? door.GetComponent<DoorLockState>() : null;
    }

    /// <summary>
    /// Applies active or inactive button state to configured door action.
    /// </summary>
    public bool Apply(bool active, GameObject interactorRoot)
    {
        if (door == null)
            return false;

        if (action == DoorButtonAction.ToggleLock)
        {
            if (doorLockState == null)
                CacheReferences();

            if (doorLockState == null)
                return false;

            doorLockState.SetLocked(!active);
            return true;
        }

        if (door.IsOpen == active)
            return true;

        UnlockDoorIfOpening(active);
        return door.TrySetOpen(active, playFeedback: true, interactorRoot);
    }

    /// <summary>
    /// Allows door buttons to open locked doors without requiring player lockpicking first.
    /// </summary>
    private void UnlockDoorIfOpening(bool active)
    {
        if (!active)
            return;

        if (doorLockState == null)
            CacheReferences();

        if (doorLockState != null && doorLockState.IsLocked)
            doorLockState.SetLocked(false);
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Door Button Interactable")]
public sealed class DoorButtonInteractable : PlayerWorldInteractable, IInputCodeSessionTarget
{
    private const string DefaultInteractionLabel = "Door Button";

    [FoldoutGroup("Door Button")]
    [SerializeField] private string interactionLabel = DefaultInteractionLabel;

    [FoldoutGroup("Door Button"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<DoorButtonTarget> targets = new();

    [FoldoutGroup("Input Code")]
    [SerializeField] private bool requiresInputCode;

    [FoldoutGroup("Input Code"), ShowIf(nameof(requiresInputCode)), AssetsOnly, Required]
    [SerializeField] private InputCodeMinigameDefinition inputCodeDefinition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsActivated => isActivated;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsCodeAuthorized => !requiresInputCode || isCodeAuthorized;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int RemainingAttempts
    {
        get
        {
            EnsureAttemptsInitialized();
            return remainingAttempts;
        }
    }

    public InputCodeMinigameDefinition Definition => requiresInputCode ? inputCodeDefinition : null;
    public override string InteractionDisplayName => string.IsNullOrWhiteSpace(interactionLabel) ? DefaultInteractionLabel : interactionLabel;

    private bool isActivated;
    private bool isCodeAuthorized;
    private bool isCodeFailed;
    private bool attemptsInitialized;
    private int remainingAttempts;

    /// <summary>
    /// Caches configured door dependencies and initializes input-code attempts.
    /// </summary>
    private void Awake()
    {
        CacheTargetReferences();
        EnsureAttemptsInitialized();
    }

    /// <summary>
    /// Initializes state before registering this interactable for player discovery.
    /// </summary>
    protected override void OnEnable()
    {
        CacheTargetReferences();
        EnsureAttemptsInitialized();
        base.OnEnable();
    }

    /// <summary>
    /// Normalizes authoring data and refreshes cached target dependencies.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        interactionLabel = string.IsNullOrWhiteSpace(interactionLabel)
            ? DefaultInteractionLabel
            : interactionLabel.Trim();
        CacheTargetReferences();
    }

    /// <summary>
    /// Returns whether player can authorize or operate this button in its current state.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        if (!base.CanInteract(interactorRoot) || targets == null || targets.Count == 0)
            return false;

        if (!requiresInputCode || isCodeAuthorized)
            return true;

        EnsureAttemptsInitialized();
        return !isCodeFailed &&
               inputCodeDefinition != null &&
               remainingAttempts > 0 &&
               InputCodeController.HasRegisteredInstance;
    }

    /// <summary>
    /// Opens code authorization when required, otherwise toggles all configured door actions.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        if (requiresInputCode && !isCodeAuthorized)
            return InputCodeController.TryBeginActiveSession(interactorRoot, this);

        bool nextActivated = !isActivated;
        bool changedAnyTarget = false;
        for (int i = 0; i < targets.Count; i++)
        {
            DoorButtonTarget target = targets[i];
            if (target != null)
                changedAnyTarget |= target.Apply(nextActivated, interactorRoot);
        }

        if (changedAnyTarget)
            isActivated = nextActivated;

        return changedAnyTarget;
    }

    /// <summary>
    /// Consumes one persistent failed code attempt for this button.
    /// </summary>
    public int ConsumeFailedAttempt(GameObject interactorRoot)
    {
        EnsureAttemptsInitialized();
        remainingAttempts = Mathf.Max(0, remainingAttempts - 1);
        return remainingAttempts;
    }

    /// <summary>
    /// Authorizes normal button interactions after correct code entry.
    /// </summary>
    public void NotifySucceeded(GameObject interactorRoot)
    {
        isCodeAuthorized = true;
        isCodeFailed = false;
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Permanently disables code attempts after available attempts are exhausted.
    /// </summary>
    public void NotifyFailed(GameObject interactorRoot)
    {
        isCodeAuthorized = false;
        isCodeFailed = true;
        remainingAttempts = 0;
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Restores button, authorization, and attempt state for gameplay testing.
    /// </summary>
    [Button(ButtonSizes.Small)]
    public void ResetRuntimeState()
    {
        isActivated = false;
        isCodeAuthorized = false;
        isCodeFailed = false;
        attemptsInitialized = false;
        EnsureAttemptsInitialized();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Caches lock components for configured door targets outside interaction hot paths.
    /// </summary>
    private void CacheTargetReferences()
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
            targets[i]?.CacheReferences();
    }

    /// <summary>
    /// Initializes persistent input-code attempts once for this gameplay run.
    /// </summary>
    private void EnsureAttemptsInitialized()
    {
        if (attemptsInitialized)
            return;

        remainingAttempts = requiresInputCode && inputCodeDefinition != null
            ? inputCodeDefinition.MaxAttempts
            : 0;
        attemptsInitialized = true;
    }
}

}
