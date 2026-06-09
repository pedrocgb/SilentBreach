using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Player Pickup Inventory")]
public class PlayerPickupInventory : MonoBehaviour
{
    private readonly Dictionary<string, int> itemCounts = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, int> InventoryChanged;

    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return 0;

        return itemCounts.TryGetValue(itemId.Trim(), out int count) ? count : 0;
    }

    public bool HasItem(string itemId)
    {
        return GetItemCount(itemId) > 0;
    }

    public int AddItem(PickableItemWorld pickableItem)
    {
        if (pickableItem == null)
            return 0;

        string resolvedId = pickableItem.ItemId;
        int nextCount = GetItemCount(resolvedId) + 1;
        itemCounts[resolvedId] = nextCount;
        InventoryChanged?.Invoke(resolvedId, nextCount);
        return nextCount;
    }
}

}
