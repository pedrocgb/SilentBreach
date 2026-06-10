using System;
using Breezeblocks.Input;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Player Interactor")]
[RequireComponent(typeof(PlayerPickupInventory))]
public class PlayerPickupInteractor : MonoBehaviour
{
    private const float MinimumRange = 0.01f;
    private const string LegacyPickUpActionName = "Pick Up";
    private const string DefaultInteractActionName = "Interact";

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired"), LabelText("Interact Action")]
    [SerializeField] private string pickUpAction = DefaultInteractActionName;

    [FoldoutGroup("References")]
    [SerializeField] private Transform interactionOrigin;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerPickupInventory pickupInventory;

    [FoldoutGroup("Detection"), MinValue(MinimumRange), LabelText("Interaction Range")]
    [SerializeField] private float pickupRange = 1.25f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public PlayerWorldInteractable CurrentInteractable => currentInteractable;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public PickableItemWorld CurrentPickable => currentPickable;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    public event Action<PlayerWorldInteractable> CurrentInteractableChanged;
    public event Action<PickableItemWorld> CurrentPickableChanged;
    public event Action<PlayerWorldInteractable> Interacted;
    public event Action<PickableItemWorld> PickedUp;

    private IPlayerInputReader inputReader;
    private PlayerWorldInteractable currentInteractable;
    private PickableItemWorld currentPickable;
    private IPlayerHoldInteractable activeHoldInteractable;
    private bool activeHoldInteractionUsesToggleMode;
    private bool inputBlocked;

    /// <summary>
    /// Populates default scene references for the interaction origin and pickup inventory.
    /// </summary>
    private void Reset()
    {
        if (interactionOrigin == null)
            interactionOrigin = transform;

        if (pickupInventory == null)
            pickupInventory = GetComponent<PlayerPickupInventory>();
    }

    /// <summary>
    /// Caches required runtime dependencies and creates the Rewired-backed input reader.
    /// </summary>
    private void Awake()
    {
        if (interactionOrigin == null)
            interactionOrigin = transform;

        if (pickupInventory == null)
            pickupInventory = GetComponent<PlayerPickupInventory>();

        MigrateLegacyActionName();
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
    }

    /// <summary>
    /// Refreshes the nearest interactable and consumes the interact action when allowed.
    /// </summary>
    private void Update()
    {
        RefreshCurrentInteractable();

        if (inputReader == null)
            inputReader = new RewiredPlayerInputReader(rewiredPlayerId);

        if (!inputReader.IsReady)
            return;

        if (activeHoldInteractable != null)
        {
            UpdateHoldInteraction();
            return;
        }

        if (inputBlocked)
            return;

        if (currentInteractable is IPlayerHoldInteractable holdInteractable)
        {
            if (inputReader.GetButtonDown(pickUpAction))
                TryBeginHoldInteraction(currentInteractable, holdInteractable);

            return;
        }

        if (currentInteractable != null && inputReader.GetButtonDown(pickUpAction))
            TryInteractCurrent();
    }

    /// <summary>
    /// Temporarily blocks or re-enables interaction input and refresh notifications.
    /// </summary>
    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        CurrentInteractableChanged?.Invoke(currentInteractable);
        CurrentPickableChanged?.Invoke(currentPickable);
    }

    /// <summary>
    /// Returns whether the inventory already contains at least one copy of the given item id.
    /// </summary>
    public bool HasCollectedItem(string itemId)
    {
        return pickupInventory != null && pickupInventory.HasItem(itemId);
    }

    /// <summary>
    /// Returns how many copies of the given item id the player has collected.
    /// </summary>
    public int GetCollectedItemCount(string itemId)
    {
        return pickupInventory != null ? pickupInventory.GetItemCount(itemId) : 0;
    }

    /// <summary>
    /// Clamps inspector values and normalizes migrated input action names.
    /// </summary>
    private void OnValidate()
    {
        pickupRange = Mathf.Max(MinimumRange, pickupRange);
        MigrateLegacyActionName();
    }

    /// <summary>
    /// Resolves and publishes the closest currently valid world interactable.
    /// </summary>
    private void RefreshCurrentInteractable()
    {
        Vector3 origin = interactionOrigin != null ? interactionOrigin.position : transform.position;
        PlayerWorldInteractable bestInteractable = PlayerWorldInteractableRegistry.FindClosestInteractable(
            origin,
            Mathf.Max(MinimumRange, pickupRange),
            gameObject);

        if (currentInteractable == bestInteractable)
            return;

        currentInteractable = bestInteractable;
        currentPickable = bestInteractable as PickableItemWorld;
        CurrentInteractableChanged?.Invoke(currentInteractable);
        CurrentPickableChanged?.Invoke(currentPickable);
    }

    /// <summary>
    /// Attempts to use the current interactable and records pickups in the inventory when applicable.
    /// </summary>
    private void TryInteractCurrent()
    {
        if (currentInteractable == null)
            return;

        PlayerWorldInteractable interacted = currentInteractable;
        if (!interacted.TryInteract(gameObject))
            return;

        if (interacted is PickableItemWorld collected)
        {
            pickupInventory?.AddItem(collected);
            PickedUp?.Invoke(collected);
        }

        Interacted?.Invoke(interacted);
        RefreshCurrentInteractable();
    }

    /// <summary>
    /// Attempts to begin a held interaction on the current interactable.
    /// </summary>
    private void TryBeginHoldInteraction(PlayerWorldInteractable interactable, IPlayerHoldInteractable holdInteractable)
    {
        if (interactable == null || holdInteractable == null)
            return;

        if (!holdInteractable.TryBeginHold(gameObject))
            return;

        activeHoldInteractable = holdInteractable;
        activeHoldInteractionUsesToggleMode = ShouldUseToggleHoldInteraction(holdInteractable);
        Interacted?.Invoke(interactable);
    }

    /// <summary>
    /// Maintains or ends the active held interaction based on the interact button state.
    /// </summary>
    private void UpdateHoldInteraction()
    {
        if (activeHoldInteractable == null)
            return;

        if (!activeHoldInteractable.IsHoldActive(gameObject))
        {
            EndActiveHoldInteraction();
            return;
        }

        if (activeHoldInteractionUsesToggleMode)
        {
            if (inputReader.GetButtonDown(pickUpAction))
            {
                EndActiveHoldInteraction();
                return;
            }

            activeHoldInteractable.TickHold(gameObject, Time.deltaTime);
            return;
        }

        if (!inputReader.GetButton(pickUpAction))
        {
            EndActiveHoldInteraction();
            return;
        }

        activeHoldInteractable.TickHold(gameObject, Time.deltaTime);
    }

    /// <summary>
    /// Ends the current held interaction and refreshes nearby interactable state.
    /// </summary>
    private void EndActiveHoldInteraction()
    {
        if (activeHoldInteractable != null)
            activeHoldInteractable.EndHold(gameObject);

        activeHoldInteractable = null;
        activeHoldInteractionUsesToggleMode = false;
        RefreshCurrentInteractable();
    }

    /// <summary>
    /// Returns whether the supplied held interaction should use click-to-toggle behavior.
    /// </summary>
    private static bool ShouldUseToggleHoldInteraction(IPlayerHoldInteractable holdInteractable)
    {
        return holdInteractable is DragBodyInteractable &&
               GlobalSettings.Instance != null &&
               !GlobalSettings.Instance.DragRequiresHoldInput;
    }

    /// <summary>
    /// Migrates legacy pickup action names to the shared interact action.
    /// </summary>
    private void MigrateLegacyActionName()
    {
        if (string.IsNullOrWhiteSpace(pickUpAction) || string.Equals(pickUpAction, LegacyPickUpActionName, StringComparison.OrdinalIgnoreCase))
            pickUpAction = DefaultInteractActionName;
    }
}

}
