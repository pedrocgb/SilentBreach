using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Breezeblocks.Missions
{

[Serializable]
public sealed class CutWireOutcomeUnityEvent : UnityEvent
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Cut Wire/Interactable")]
public sealed class CutWireInteractable : PlayerWorldInteractable, ICutWireSessionTarget
{
    private const string DefaultInteractionLabel = "Painel de Energia";

    [FoldoutGroup("Cut Wire")]
    [SerializeField] private string interactionLabel = DefaultInteractionLabel;

    [FoldoutGroup("Cut Wire"), AssetsOnly]
    [SerializeField] private CutWireMinigameDefinition definition;

    [FoldoutGroup("Events")]
    [SerializeField] private CutWireOutcomeUnityEvent onSucceeded = new();

    [FoldoutGroup("Events")]
    [SerializeField] private CutWireOutcomeUnityEvent onFailed = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsResolved => isResolved;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool WasSuccessful => wasSuccessful;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public IReadOnlyList<bool> CutStates => cutStates;

    public CutWireMinigameDefinition Definition => definition;
    public override string InteractionDisplayName =>
        string.IsNullOrWhiteSpace(interactionLabel) ? DefaultInteractionLabel : interactionLabel;

    private bool[] cutStates = Array.Empty<bool>();
    private bool isResolved;
    private bool wasSuccessful;

    /// <summary>
    /// Normalizes the player-facing interaction label while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        interactionLabel = string.IsNullOrWhiteSpace(interactionLabel)
            ? DefaultInteractionLabel
            : interactionLabel.Trim();
    }

    /// <summary>
    /// Ensures runtime wire-state capacity before this target becomes discoverable.
    /// </summary>
    protected override void OnEnable()
    {
        EnsureCutStateCapacity();
        base.OnEnable();
    }

    /// <summary>
    /// Returns whether this unresolved fuse box can start the shared cut-wire session.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        return !isResolved &&
               definition != null &&
               CutWireMinigameController.HasRegisteredInstance &&
               base.CanInteract(interactorRoot);
    }

    /// <summary>
    /// Opens the shared cut-wire panel for this independent fuse-box target.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        EnsureCutStateCapacity();
        return CutWireMinigameController.TryBeginActiveSession(interactorRoot, this);
    }

    /// <summary>
    /// Persists one cut immediately so closing and reopening retains visual progress.
    /// </summary>
    public void NotifyWireCut(int wireIndex)
    {
        EnsureCutStateCapacity();
        if (wireIndex >= 0 && wireIndex < cutStates.Length)
            cutStates[wireIndex] = true;
    }

    /// <summary>
    /// Resolves this fuse box successfully and invokes its configured success effects once.
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
    /// Resolves this fuse box as failed and invokes its configured failure effects once.
    /// </summary>
    public void NotifyFailed(GameObject interactorRoot)
    {
        if (isResolved)
            return;

        isResolved = true;
        wasSuccessful = false;
        onFailed?.Invoke();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Clears runtime outcome and cut progress so this target can be tested again.
    /// </summary>
    [Button(ButtonSizes.Small)]
    public void ResetRuntimeState()
    {
        isResolved = false;
        wasSuccessful = false;
        EnsureCutStateCapacity(clearExisting: true);
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Resizes runtime cut storage to match the current definition while optionally clearing progress.
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
