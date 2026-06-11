using System.Collections.Generic;
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
               LockpickMinigameController.HasRegisteredInstance;
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
        doorInteractable?.RefreshInteractionPresentation();
    }

    /// <summary>
    /// Restores the authored locked state for this door and refreshes the interaction prompt.
    /// </summary>
    public void ResetLock()
    {
        isLocked = startsLocked;
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
