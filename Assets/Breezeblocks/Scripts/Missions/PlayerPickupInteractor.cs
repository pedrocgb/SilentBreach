using System;
using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Player Pickup Interactor")]
public class PlayerPickupInteractor : MonoBehaviour
{
    private const float MinimumRange = 0.01f;

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string pickUpAction = "Pick Up";

    [FoldoutGroup("References")]
    [SerializeField] private Transform interactionOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerPickupInventory pickupInventory;

    [FoldoutGroup("Detection"), MinValue(MinimumRange)]
    [SerializeField] private float pickupRange = 1.25f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public PickableItemWorld CurrentPickable => currentPickable;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    public event Action<PickableItemWorld> CurrentPickableChanged;
    public event Action<PickableItemWorld> PickedUp;

    private Player rewiredPlayer;
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

        ResolveRewiredPlayer();
    }

    private void Update()
    {
        RefreshCurrentPickable();

        if (inputBlocked)
            return;

        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        if (currentPickable != null && rewiredPlayer.GetButtonDown(pickUpAction))
            TryPickUpCurrent();
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
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

    private void RefreshCurrentPickable()
    {
        PickableItemWorld bestPickable = null;
        float bestDistanceSqr = float.PositiveInfinity;
        Vector3 origin = interactionOrigin != null ? interactionOrigin.position : transform.position;
        float maxDistanceSqr = Mathf.Max(MinimumRange, pickupRange) * Mathf.Max(MinimumRange, pickupRange);

        var activeItems = PickableItemWorld.ActiveItems;
        for (int i = 0; i < activeItems.Count; i++)
        {
            PickableItemWorld candidate = activeItems[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsCollected)
                continue;

            float distanceSqr = ((Vector2)(candidate.transform.position - origin)).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestPickable = candidate;
        }

        if (currentPickable == bestPickable)
            return;

        currentPickable = bestPickable;
        CurrentPickableChanged?.Invoke(currentPickable);
    }

    private void TryPickUpCurrent()
    {
        if (currentPickable == null || pickupInventory == null)
            return;

        PickableItemWorld collected = currentPickable;
        if (!collected.Collect(gameObject))
            return;

        pickupInventory.AddItem(collected);
        currentPickable = null;
        CurrentPickableChanged?.Invoke(null);
        PickedUp?.Invoke(collected);
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }
}

}
