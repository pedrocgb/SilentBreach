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
public sealed class CutWireInteractable : PlayerWorldInteractable, ICutWireSessionTarget, ILockpickSessionTarget
{
    private const string DefaultInteractionLabel = "Painel de Energia";
    private const string DefaultLockedInteractionLabel = "Pick Lock";

    [FoldoutGroup("Cut Wire")]
    [SerializeField] private string interactionLabel = DefaultInteractionLabel;

    [FoldoutGroup("Cut Wire"), AssetsOnly]
    [SerializeField] private CutWireMinigameDefinition definition;

    [FoldoutGroup("Lock")]
    [SerializeField] private bool startsLocked;

    [FoldoutGroup("Lock")]
    [SerializeField] private string lockedInteractionLabel = DefaultLockedInteractionLabel;

    [FoldoutGroup("Lock"), AssetsOnly, ShowIf(nameof(startsLocked))]
    [SerializeField] private LockpickMinigameDefinition lockpickDefinition;

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

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsLocked => isLocked;

    public CutWireMinigameDefinition Definition => definition;
    public override string InteractionDisplayName =>
        isLocked
            ? string.IsNullOrWhiteSpace(lockedInteractionLabel) ? DefaultLockedInteractionLabel : lockedInteractionLabel
            : string.IsNullOrWhiteSpace(interactionLabel) ? DefaultInteractionLabel : interactionLabel;

    LockpickMinigameDefinition ILockpickSessionTarget.Definition => lockpickDefinition;

    private bool[] cutStates = Array.Empty<bool>();
    private bool isResolved;
    private bool wasSuccessful;
    private bool isLocked;

    /// <summary>
    /// Initializes the authored lock state before the fuse box becomes interactable.
    /// </summary>
    private void Awake()
    {
        isLocked = startsLocked;
        EnsureCutStateCapacity();
    }

    /// <summary>
    /// Normalizes the player-facing interaction label while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        interactionLabel = string.IsNullOrWhiteSpace(interactionLabel)
            ? DefaultInteractionLabel
            : interactionLabel.Trim();
        lockedInteractionLabel = string.IsNullOrWhiteSpace(lockedInteractionLabel)
            ? DefaultLockedInteractionLabel
            : lockedInteractionLabel.Trim();
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
    /// Returns whether this unresolved fuse box can start its currently required minigame session.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        if (isResolved || definition == null || !base.CanInteract(interactorRoot))
            return false;

        return isLocked
            ? lockpickDefinition != null && LockpickMinigameController.HasRegisteredInstance
            : CutWireController.HasRegisteredInstance;
    }

    /// <summary>
    /// Routes locked fuse boxes through lockpicking and unlocked fuse boxes through cut-wire gameplay.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        if (isLocked)
            return LockpickMinigameController.TryBeginActiveSession(interactorRoot, this);

        EnsureCutStateCapacity();
        return CutWireController.TryBeginActiveSession(interactorRoot, this);
    }

    /// <summary>
    /// Enables cut-wire interaction after the shared lockpicking minigame succeeds.
    /// </summary>
    public void NotifyUnlocked(GameObject interactorRoot)
    {
        if (!isLocked)
            return;

        isLocked = false;
        RefreshInteractionPresentation();
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
        isLocked = startsLocked;
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
