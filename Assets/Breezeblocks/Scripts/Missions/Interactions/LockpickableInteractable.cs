using System;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Breezeblocks.Missions
{

[Serializable]
public sealed class LockpickUnlockedUnityEvent : UnityEvent
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Lockpickable Interactable")]
public sealed class LockpickableInteractable : PlayerWorldInteractable, ILockpickSessionTarget
{
    [FoldoutGroup("Lockpick")]
    [SerializeField] private string lockedInteractionLabel = "Pick Lock";

    [FoldoutGroup("Lockpick"), AssetsOnly]
    [SerializeField] private LockpickMinigameDefinition definition;

    [FoldoutGroup("Events")]
    [SerializeField] private LockpickUnlockedUnityEvent onUnlocked = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsUnlocked => isUnlocked;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public LockpickMinigameDefinition Definition => definition;

    public override string InteractionDisplayName =>
        !isUnlocked && !string.IsNullOrWhiteSpace(lockedInteractionLabel)
            ? lockedInteractionLabel
            : base.InteractionDisplayName;

    private bool isUnlocked;

    /// <summary>
    /// Trims the authored interaction label while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        lockedInteractionLabel = lockedInteractionLabel != null ? lockedInteractionLabel.Trim() : string.Empty;
    }

    /// <summary>
    /// Returns whether this lock may currently start the shared lockpicking minigame.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        return !isUnlocked &&
               definition != null &&
               LockpickMinigameController.HasRegisteredInstance &&
               PlayerLockpickInventoryUtility.HasAnyLockpickUses(interactorRoot) &&
               base.CanInteract(interactorRoot);
    }

    /// <summary>
    /// Opens the shared lockpicking minigame for this lock when the player interacts with it.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        return LockpickMinigameController.TryBeginActiveSession(interactorRoot, this);
    }

    /// <summary>
    /// Marks this lock as permanently unlocked and invokes the configured unlock event once.
    /// </summary>
    public void NotifyUnlocked(GameObject interactorRoot)
    {
        if (isUnlocked)
            return;

        isUnlocked = true;
        onUnlocked?.Invoke();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Resets the runtime unlocked state so the lock can be picked again.
    /// </summary>
    public void ResetLock()
    {
        isUnlocked = false;
        RefreshInteractionPresentation();
    }
}

}
