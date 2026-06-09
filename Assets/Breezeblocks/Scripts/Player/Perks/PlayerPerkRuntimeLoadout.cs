using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

[Serializable]
public sealed class PlayerPerkRuntimeLoadout
{
    [SerializeField] private List<HideoutPerkDefinition> equippedPerks = new();

    public IReadOnlyList<HideoutPerkDefinition> EquippedPerks => equippedPerks;
    public int Count => equippedPerks.Count;

    public bool Contains(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null)
            return false;

        string perkId = perkDefinition.PerkId;
        for (int i = 0; i < equippedPerks.Count; i++)
        {
            HideoutPerkDefinition equippedPerk = equippedPerks[i];
            if (equippedPerk != null && string.Equals(equippedPerk.PerkId, perkId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public void SetPerks(IEnumerable<HideoutPerkDefinition> perkDefinitions)
    {
        equippedPerks.Clear();
        if (perkDefinitions == null)
            return;

        HashSet<string> addedPerkIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (HideoutPerkDefinition perkDefinition in perkDefinitions)
        {
            if (perkDefinition == null || string.IsNullOrWhiteSpace(perkDefinition.PerkId))
                continue;

            if (!addedPerkIds.Add(perkDefinition.PerkId))
                continue;

            equippedPerks.Add(perkDefinition);
        }
    }

    public void Clear()
    {
        equippedPerks.Clear();
    }

    public PlayerPerkRuntimeLoadout Clone()
    {
        PlayerPerkRuntimeLoadout clone = new();
        clone.SetPerks(equippedPerks);
        return clone;
    }
}

public static class PlayerPerkRuntimeSession
{
    private static PlayerPerkRuntimeLoadout equippedPerks = new();
    private static PlayerPerkRuntimeLoadout preparedEquippedPerks = new();

    public static bool HasEquippedPerks => equippedPerks != null && equippedPerks.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        equippedPerks = new PlayerPerkRuntimeLoadout();
        preparedEquippedPerks = new PlayerPerkRuntimeLoadout();
    }

    public static void SetEquippedPerks(PlayerPerkRuntimeLoadout loadout)
    {
        equippedPerks = loadout != null ? loadout.Clone() : new PlayerPerkRuntimeLoadout();
        preparedEquippedPerks = equippedPerks.Clone();
    }

    public static void SetEquippedPerks(IEnumerable<HideoutPerkDefinition> perkDefinitions)
    {
        PlayerPerkRuntimeLoadout loadout = new();
        loadout.SetPerks(perkDefinitions);
        equippedPerks = loadout;
        preparedEquippedPerks = equippedPerks.Clone();
    }

    public static PlayerPerkRuntimeLoadout PeekEquippedPerks()
    {
        return equippedPerks != null ? equippedPerks.Clone() : new PlayerPerkRuntimeLoadout();
    }

    public static void ClearEquippedPerks()
    {
        equippedPerks = new PlayerPerkRuntimeLoadout();
        preparedEquippedPerks = new PlayerPerkRuntimeLoadout();
    }

    public static PlayerPerkRuntimeLoadout PeekPreparedEquippedPerks()
    {
        return preparedEquippedPerks != null ? preparedEquippedPerks.Clone() : new PlayerPerkRuntimeLoadout();
    }

    public static void RestorePreparedEquippedPerks()
    {
        equippedPerks = preparedEquippedPerks != null ? preparedEquippedPerks.Clone() : new PlayerPerkRuntimeLoadout();
    }
}

}
