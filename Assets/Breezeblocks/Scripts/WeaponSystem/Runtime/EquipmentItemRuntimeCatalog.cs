using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[CreateAssetMenu(fileName = "EquipmentItemRuntimeCatalog", menuName = "Breezeblocks/Equipment/Runtime Catalog")]
public class EquipmentItemRuntimeCatalog : ScriptableObject
{
    private const string ResourceLoadPath = "EquipmentItemRuntimeCatalog";

    [SerializeField, HideInInspector]
    private List<EquipmentItemData> items = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public IReadOnlyList<EquipmentItemData> Items => items;

    public static EquipmentItemRuntimeCatalog Load()
    {
        return Resources.Load<EquipmentItemRuntimeCatalog>(ResourceLoadPath);
    }

    public EquipmentItemData FindByDisplayName(string displayName)
    {
        string normalizedQuery = NormalizeLookupKey(displayName);
        if (string.IsNullOrEmpty(normalizedQuery))
            return null;

        for (int i = 0; i < items.Count; i++)
        {
            EquipmentItemData candidate = items[i];
            if (candidate == null)
                continue;

            if (string.Equals(NormalizeLookupKey(candidate.DisplayName), normalizedQuery, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    public static string NormalizeLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new(value.Length);
        bool previousWasSeparator = true;
        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            bool isSeparator = char.IsWhiteSpace(character) || character == '_' || character == '-';
            if (isSeparator)
            {
                if (!previousWasSeparator)
                    builder.Append(' ');

                previousWasSeparator = true;
                continue;
            }

            builder.Append(character);
            previousWasSeparator = false;
        }

        return builder.ToString().Trim();
    }

#if UNITY_EDITOR
    public bool ReplaceItemsForEditor(IReadOnlyList<EquipmentItemData> sourceItems)
    {
        sourceItems ??= Array.Empty<EquipmentItemData>();

        bool changed = items.Count != sourceItems.Count;
        if (!changed)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == sourceItems[i])
                    continue;

                changed = true;
                break;
            }
        }

        if (!changed)
            return false;

        items.Clear();
        for (int i = 0; i < sourceItems.Count; i++)
        {
            if (sourceItems[i] != null)
                items.Add(sourceItems[i]);
        }

        return true;
    }
#endif
}

}
