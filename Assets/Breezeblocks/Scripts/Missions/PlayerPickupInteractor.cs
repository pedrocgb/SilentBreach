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
    private bool inputBlocked;

    // Executes the Reset routine.
    private void Reset()
    {
        if (interactionOrigin == null)
            interactionOrigin = transform;

        if (pickupInventory == null)
            pickupInventory = GetComponent<PlayerPickupInventory>();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        if (interactionOrigin == null)
            interactionOrigin = transform;

        if (pickupInventory == null)
            pickupInventory = GetComponent<PlayerPickupInventory>();

        MigrateLegacyActionName();
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
    }

    // Executes the Update routine.
    private void Update()
    {
        RefreshCurrentInteractable();

        if (inputBlocked)
            return;

        if (inputReader == null)
            inputReader = new RewiredPlayerInputReader(rewiredPlayerId);

        if (!inputReader.IsReady)
            return;

        if (currentInteractable != null && inputReader.GetButtonDown(pickUpAction))
            TryInteractCurrent();
    }

    // Executes the SetInputBlocked routine.
    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        CurrentInteractableChanged?.Invoke(currentInteractable);
        CurrentPickableChanged?.Invoke(currentPickable);
    }

    // Executes the HasCollectedItem routine.
    public bool HasCollectedItem(string itemId)
    {
        return pickupInventory != null && pickupInventory.HasItem(itemId);
    }

    // Executes the GetCollectedItemCount routine.
    public int GetCollectedItemCount(string itemId)
    {
        return pickupInventory != null ? pickupInventory.GetItemCount(itemId) : 0;
    }

    // Executes the OnValidate routine.
    private void OnValidate()
    {
        pickupRange = Mathf.Max(MinimumRange, pickupRange);
        MigrateLegacyActionName();
    }

    // Executes the RefreshCurrentInteractable routine.
    private void RefreshCurrentInteractable()
    {
        PlayerWorldInteractable bestInteractable = null;
        float bestDistanceSqr = float.PositiveInfinity;
        Vector3 origin = interactionOrigin != null ? interactionOrigin.position : transform.position;
        float maxDistanceSqr = Mathf.Max(MinimumRange, pickupRange) * Mathf.Max(MinimumRange, pickupRange);

        var activeInteractables = PlayerWorldInteractable.ActiveInteractables;
        for (int i = 0; i < activeInteractables.Count; i++)
        {
            PlayerWorldInteractable candidate = activeInteractables[i];
            if (candidate == null || !candidate.CanInteract(gameObject))
                continue;

            float distanceSqr = ((Vector2)(candidate.InteractionPosition - origin)).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestInteractable = candidate;
        }

        if (currentInteractable == bestInteractable)
            return;

        currentInteractable = bestInteractable;
        currentPickable = bestInteractable as PickableItemWorld;
        CurrentInteractableChanged?.Invoke(currentInteractable);
        CurrentPickableChanged?.Invoke(currentPickable);
    }

    // Executes the TryInteractCurrent routine.
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

    // Executes the MigrateLegacyActionName routine.
    private void MigrateLegacyActionName()
    {
        if (string.IsNullOrWhiteSpace(pickUpAction) || string.Equals(pickUpAction, LegacyPickUpActionName, StringComparison.OrdinalIgnoreCase))
            pickUpAction = DefaultInteractActionName;
    }
}

}
