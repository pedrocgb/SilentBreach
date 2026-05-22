using System;
using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Player Interactor")]
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

    [FoldoutGroup("References")]
    [SerializeField] private PlayerPickupInventory pickupInventory;

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

    private Player rewiredPlayer;
    private PlayerWorldInteractable currentInteractable;
    private PickableItemWorld currentPickable;
    private bool inputBlocked;

    private void Reset()
    {
        if (interactionOrigin == null)
            interactionOrigin = transform;

        if (pickupInventory == null)
            pickupInventory = GetComponent<PlayerPickupInventory>() ?? gameObject.AddComponent<PlayerPickupInventory>();
    }

    private void Awake()
    {
        if (interactionOrigin == null)
            interactionOrigin = transform;

        if (pickupInventory == null)
            pickupInventory = GetComponent<PlayerPickupInventory>() ?? gameObject.AddComponent<PlayerPickupInventory>();

        MigrateLegacyActionName();
        ResolveRewiredPlayer();
    }

    private void Update()
    {
        RefreshCurrentInteractable();

        if (inputBlocked)
            return;

        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        if (currentInteractable != null && rewiredPlayer.GetButtonDown(pickUpAction))
            TryInteractCurrent();
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        CurrentInteractableChanged?.Invoke(currentInteractable);
        CurrentPickableChanged?.Invoke(currentPickable);
    }

    public bool HasCollectedItem(string itemId)
    {
        return pickupInventory != null && pickupInventory.HasItem(itemId);
    }

    public int GetCollectedItemCount(string itemId)
    {
        return pickupInventory != null ? pickupInventory.GetItemCount(itemId) : 0;
    }

    private void OnValidate()
    {
        pickupRange = Mathf.Max(MinimumRange, pickupRange);
        MigrateLegacyActionName();
    }

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

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }

    private void MigrateLegacyActionName()
    {
        if (string.IsNullOrWhiteSpace(pickUpAction) || string.Equals(pickUpAction, LegacyPickUpActionName, StringComparison.OrdinalIgnoreCase))
            pickUpAction = DefaultInteractActionName;
    }
}

}
