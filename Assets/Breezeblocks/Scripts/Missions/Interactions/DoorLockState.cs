using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(DoorInteractable))]
[AddComponentMenu("Breezeblocks/Missions/Door Lock State")]
public sealed class DoorLockState : MonoBehaviour, ILockpickSessionTarget
{
    private const string DefaultLockedInteractionLabel = "Pick Lock";

    [FoldoutGroup("Lock State")]
    [SerializeField] private bool startsLocked = true;

    [FoldoutGroup("Lock State")]
    [SerializeField] private string lockedInteractionLabel = DefaultLockedInteractionLabel;

    [FoldoutGroup("Lock State"), AssetsOnly]
    [SerializeField] private LockpickMinigameDefinition definition;

    [FoldoutGroup("Enemy Overrides"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<EnemyMovementController> enemiesThatIgnoreLockedState = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsLocked => isLocked;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string LockedInteractionDisplayName => string.IsNullOrWhiteSpace(lockedInteractionLabel) ? DefaultLockedInteractionLabel : lockedInteractionLabel;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool StartsLocked => startsLocked;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public LockpickMinigameDefinition Definition => definition;

    private DoorInteractable doorInteractable;
    private bool isLocked;
    private bool blockedAttemptRevealed;

    /// <summary>
    /// Caches the same-object door reference and initializes the authored runtime lock state.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        isLocked = startsLocked;
    }

    /// <summary>
    /// Normalizes inspector-authored values and removes null enemy overrides while editing.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
        lockedInteractionLabel = lockedInteractionLabel != null ? lockedInteractionLabel.Trim() : string.Empty;
        enemiesThatIgnoreLockedState.RemoveAll(enemy => enemy == null);
    }

    /// <summary>
    /// Returns whether the supplied player may currently start the lockpicking minigame for this door.
    /// </summary>
    public bool CanPlayerAttemptUnlock(GameObject interactorRoot)
    {
        return isLocked &&
               interactorRoot != null &&
               definition != null &&
               PlayerLockpickInventoryUtility.HasAnyLockpickUses(interactorRoot) &&
               LockpickMinigameController.HasRegisteredInstance;
    }

    /// <summary>
    /// Returns whether the locked door should remain available as a denied interaction prompt.
    /// </summary>
    public bool CanPlayerInspectLockedState(GameObject interactorRoot)
    {
        return isLocked && interactorRoot != null && definition != null;
    }

    /// <summary>
    /// Returns whether the player currently has lockpick uses available for this lock.
    /// </summary>
    public bool HasPlayerLockpickUses(GameObject interactorRoot)
    {
        return PlayerLockpickInventoryUtility.HasAnyLockpickUses(interactorRoot);
    }

    /// <summary>
    /// Resolves the locked door prompt label for a player with or without lockpicks.
    /// </summary>
    public string GetPlayerLockedInteractionDisplayName(GameObject interactorRoot)
    {
        if (HasPlayerLockpickUses(interactorRoot))
            return LockedInteractionDisplayName;

        return LockpickMinigameController.ResolveNoLockpickDisplayName(blockedAttemptRevealed);
    }

    /// <summary>
    /// Starts the shared lockpicking minigame for this locked door when the player is allowed to unlock it.
    /// </summary>
    public bool TryBeginLockpick(GameObject interactorRoot)
    {
        return CanPlayerAttemptUnlock(interactorRoot) &&
               LockpickMinigameController.TryBeginActiveSession(interactorRoot, this);
    }

    /// <summary>
    /// Marks this lock as visibly denied and returns the prompt feedback payload to play.
    /// </summary>
    public InteractionPromptFeedback CreateBlockedAttemptFeedback()
    {
        blockedAttemptRevealed = true;
        doorInteractable?.RefreshInteractionPresentation();
        return LockpickMinigameController.CreateNoLockpickPromptFeedback();
    }

    /// <summary>
    /// Plays locked-door SFX and noise from the supplied world position.
    /// </summary>
    public void PlayBlockedAttemptWorldFeedback(Vector3 position, GameObject source)
    {
        LockpickMinigameController.PlayNoLockpickWorldFeedback(position, source != null ? source : gameObject);
    }

    /// <summary>
    /// Returns whether the supplied enemy is allowed to ignore the lock and operate the door normally.
    /// </summary>
    public bool CanEnemyIgnoreLockedState(EnemyMovementController enemyMovementController)
    {
        return enemyMovementController != null && enemiesThatIgnoreLockedState.Contains(enemyMovementController);
    }

    /// <summary>
    /// Returns whether the supplied actor may bypass this door's locked state.
    /// </summary>
    public bool CanActorBypassLockedState(GameObject actorRoot)
    {
        if (!isLocked)
            return true;

        EnemyMovementController enemyMovementController = ResolveEnemyMovementController(actorRoot);
        return CanEnemyIgnoreLockedState(enemyMovementController);
    }

    /// <summary>
    /// Marks the door as unlocked and refreshes any prompt currently presenting its interaction label.
    /// </summary>
    public void NotifyUnlocked(GameObject interactorRoot)
    {
        if (!isLocked)
            return;

        isLocked = false;
        blockedAttemptRevealed = false;
        doorInteractable?.RefreshInteractionPresentation();
    }

    /// <summary>
    /// Restores the authored locked state for this door and refreshes the interaction prompt.
    /// </summary>
    public void ResetLock()
    {
        isLocked = startsLocked;
        blockedAttemptRevealed = false;
        doorInteractable?.RefreshInteractionPresentation();
    }

    /// <summary>
    /// Caches the required same-object door component used to refresh presentation after lock changes.
    /// </summary>
    private void CacheReferences()
    {
        doorInteractable = GetComponent<DoorInteractable>();
    }

    /// <summary>
    /// Resolves an enemy movement controller from the supplied actor root when one is present.
    /// </summary>
    private static EnemyMovementController ResolveEnemyMovementController(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        if (actorRoot.TryGetComponent(out EnemyMovementController directController))
            return directController;

        return actorRoot.GetComponentInParent<EnemyMovementController>();
    }
}

}
