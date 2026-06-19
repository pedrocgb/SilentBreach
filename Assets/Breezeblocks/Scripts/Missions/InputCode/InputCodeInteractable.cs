using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Breezeblocks.Missions
{

[Serializable]
public sealed class InputCodeOutcomeUnityEvent : UnityEvent
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Input Code/Interactable")]
public sealed class InputCodeInteractable : PlayerWorldInteractable, IInputCodeSessionTarget
{
    private const string DefaultInteractionLabel = "Painel de Código";

    [FoldoutGroup("Input Code")]
    [SerializeField] private string interactionLabel = DefaultInteractionLabel;

    [FoldoutGroup("Input Code"), AssetsOnly]
    [SerializeField] private InputCodeMinigameDefinition definition;

    [FoldoutGroup("Events")]
    [SerializeField] private InputCodeOutcomeUnityEvent onSucceeded = new();

    [FoldoutGroup("Events")]
    [SerializeField] private InputCodeOutcomeUnityEvent onFailed = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsResolved => isResolved;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool WasSuccessful => wasSuccessful;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int RemainingAttempts
    {
        get
        {
            EnsureAttemptsInitialized();
            return remainingAttempts;
        }
    }

    public InputCodeMinigameDefinition Definition => definition;
    public override string InteractionDisplayName => string.IsNullOrWhiteSpace(interactionLabel) ? DefaultInteractionLabel : interactionLabel;

    private bool isResolved;
    private bool wasSuccessful;
    private int remainingAttempts;
    private bool attemptsInitialized;

    /// <summary>
    /// Initializes attempts before this input-code panel can be used.
    /// </summary>
    private void Awake()
    {
        EnsureAttemptsInitialized();
    }

    /// <summary>
    /// Normalizes the authored interaction label while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        interactionLabel = string.IsNullOrWhiteSpace(interactionLabel)
            ? DefaultInteractionLabel
            : interactionLabel.Trim();
    }

    /// <summary>
    /// Ensures attempt state exists before the interactable is registered.
    /// </summary>
    protected override void OnEnable()
    {
        EnsureAttemptsInitialized();
        base.OnEnable();
    }

    /// <summary>
    /// Returns whether this code panel can currently open the shared input-code UI.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        EnsureAttemptsInitialized();
        return !isResolved &&
               definition != null &&
               remainingAttempts > 0 &&
               InputCodeController.HasRegisteredInstance &&
               base.CanInteract(interactorRoot);
    }

    /// <summary>
    /// Opens the shared input-code minigame for this panel.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        EnsureAttemptsInitialized();
        return InputCodeController.TryBeginActiveSession(interactorRoot, this);
    }

    /// <summary>
    /// Consumes one failed code attempt and returns the remaining attempts.
    /// </summary>
    public int ConsumeFailedAttempt(GameObject interactorRoot)
    {
        EnsureAttemptsInitialized();
        remainingAttempts = Mathf.Max(0, remainingAttempts - 1);
        return remainingAttempts;
    }

    /// <summary>
    /// Marks this code panel as successfully solved.
    /// </summary>
    public void NotifySucceeded(GameObject interactorRoot)
    {
        if (isResolved)
            return;

        isResolved = true;
        wasSuccessful = true;
        onSucceeded?.Invoke();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Marks this code panel as failed after all attempts are consumed.
    /// </summary>
    public void NotifyFailed(GameObject interactorRoot)
    {
        if (isResolved)
            return;

        isResolved = true;
        wasSuccessful = false;
        remainingAttempts = 0;
        onFailed?.Invoke();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Restores attempts and outcome state for gameplay testing.
    /// </summary>
    [Button(ButtonSizes.Small)]
    public void ResetRuntimeState()
    {
        isResolved = false;
        wasSuccessful = false;
        attemptsInitialized = false;
        EnsureAttemptsInitialized();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Initializes remaining attempts from the active definition once per gameplay run.
    /// </summary>
    private void EnsureAttemptsInitialized()
    {
        if (attemptsInitialized)
            return;

        remainingAttempts = definition != null ? definition.MaxAttempts : 0;
        attemptsInitialized = true;
    }
}

}
