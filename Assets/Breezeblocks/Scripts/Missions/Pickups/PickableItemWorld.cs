using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Pickable Item World")]
public class PickableItemWorld : PlayerWorldInteractable
{
    private static readonly List<PickableItemWorld> ActiveItemsInternal = new();

    [FoldoutGroup("Item")]
    [SerializeField] private string itemId;

    [FoldoutGroup("Item")]
    [SerializeField] private string itemDisplayName;

    [FoldoutGroup("Item")]
    [SerializeField] private bool disableGameObjectOnPickup = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsCollected => isCollected;

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
    public string ItemDisplayName => string.IsNullOrWhiteSpace(itemDisplayName) ? ItemId : itemDisplayName;
    public static IReadOnlyList<PickableItemWorld> ActiveItems => ActiveItemsInternal;

    private bool isCollected;

    protected override void OnEnable()
    {
        base.OnEnable();
        isCollected = false;
        if (!ActiveItemsInternal.Contains(this))
            ActiveItemsInternal.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ActiveItemsInternal.Remove(this);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        itemId = itemId != null ? itemId.Trim() : string.Empty;
        itemDisplayName = itemDisplayName != null ? itemDisplayName.Trim() : string.Empty;
    }

    public override bool CanInteract(GameObject interactorRoot)
    {
        return base.CanInteract(interactorRoot) && !isCollected;
    }

    protected override bool Interact(GameObject interactorRoot)
    {
        return Collect(interactorRoot);
    }

    public bool Collect(GameObject pickerRoot)
    {
        if (isCollected)
            return false;

        isCollected = true;
        MissionRuntimeEvents.RaiseItemPickedUp(pickerRoot, this);

        if (disableGameObjectOnPickup)
            gameObject.SetActive(false);
        else
            ActiveItemsInternal.Remove(this);

        return true;
    }
}

}
