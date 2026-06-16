using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

/// <summary>
/// Saves and restores the player's equipped hideout perk loadout across sessions.
/// </summary>
public static class HideoutPerkLoadoutPersistence
{
    /// <summary>
    /// Restores the saved equipped perk loadout before gameplay or hideout scenes consume runtime perk data.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreRuntimeLoadoutBeforeSceneLoad()
    {
        if (PlayerPerkRuntimeSession.PeekEquippedPerks().Count > 0)
            return;

        RestoreRuntimeLoadoutFromSave(null);
    }

    /// <summary>
    /// Returns the active runtime loadout, restoring the saved loadout when runtime data is still empty.
    /// </summary>
    public static PlayerPerkRuntimeLoadout GetOrRestoreRuntimeLoadout(IEnumerable<HideoutPerkDefinition> preferredPerkDefinitions)
    {
        PlayerPerkRuntimeLoadout runtimeLoadout = PlayerPerkRuntimeSession.PeekEquippedPerks();
        return runtimeLoadout.Count > 0
            ? runtimeLoadout
            : RestoreRuntimeLoadoutFromSave(preferredPerkDefinitions);
    }

    /// <summary>
    /// Persists the supplied equipped perk definitions into the durable hideout save snapshot.
    /// </summary>
    public static void SaveEquippedPerks(IEnumerable<HideoutPerkDefinition> perkDefinitions)
    {
        HideoutSaveSnapshot snapshot = HideoutSaveSystem.TryLoad(out HideoutSaveSnapshot loadedSnapshot)
            ? loadedSnapshot
            : new HideoutSaveSnapshot { HasHideoutProgress = HideoutRuntimeSession.IsInitialized };

        snapshot.EquippedPerkIds = BuildEquippedPerkIdList(perkDefinitions);
        HideoutSaveSystem.Save(snapshot);
    }

    /// <summary>
    /// Rebuilds the runtime perk session from the equipped perk identifiers stored in the save file.
    /// </summary>
    private static PlayerPerkRuntimeLoadout RestoreRuntimeLoadoutFromSave(IEnumerable<HideoutPerkDefinition> preferredPerkDefinitions)
    {
        PlayerPerkRuntimeLoadout restoredLoadout = new();
        restoredLoadout.SetPerks(ResolveSavedPerkDefinitions(preferredPerkDefinitions));
        PlayerPerkRuntimeSession.SetEquippedPerks(restoredLoadout);
        return PlayerPerkRuntimeSession.PeekEquippedPerks();
    }

    /// <summary>
    /// Resolves saved perk identifiers back into perk definition assets while preserving save order.
    /// </summary>
    private static List<HideoutPerkDefinition> ResolveSavedPerkDefinitions(IEnumerable<HideoutPerkDefinition> preferredPerkDefinitions)
    {
        List<HideoutPerkDefinition> resolvedPerks = new();
        if (!HideoutSaveSystem.TryLoad(out HideoutSaveSnapshot snapshot) ||
            snapshot.EquippedPerkIds == null ||
            snapshot.EquippedPerkIds.Count <= 0)
        {
            return resolvedPerks;
        }

        Dictionary<string, HideoutPerkDefinition> perkLookup = BuildPerkLookup(preferredPerkDefinitions);
        if (perkLookup.Count < snapshot.EquippedPerkIds.Count)
            AddPerksToLookup(perkLookup, Resources.LoadAll<HideoutPerkDefinition>(string.Empty));

        if (perkLookup.Count < snapshot.EquippedPerkIds.Count)
            AddPerksToLookup(perkLookup, Resources.FindObjectsOfTypeAll<HideoutPerkDefinition>());

        HashSet<string> addedPerkIds = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < snapshot.EquippedPerkIds.Count; i++)
        {
            string perkId = snapshot.EquippedPerkIds[i] != null ? snapshot.EquippedPerkIds[i].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(perkId) ||
                !addedPerkIds.Add(perkId) ||
                !perkLookup.TryGetValue(perkId, out HideoutPerkDefinition perkDefinition) ||
                perkDefinition == null)
            {
                continue;
            }

            resolvedPerks.Add(perkDefinition);
        }

        return resolvedPerks;
    }

    /// <summary>
    /// Builds a fast lookup table for candidate perk definitions keyed by stable perk identifier.
    /// </summary>
    private static Dictionary<string, HideoutPerkDefinition> BuildPerkLookup(IEnumerable<HideoutPerkDefinition> perkDefinitions)
    {
        Dictionary<string, HideoutPerkDefinition> perkLookup = new(StringComparer.OrdinalIgnoreCase);
        AddPerksToLookup(perkLookup, perkDefinitions);
        return perkLookup;
    }

    /// <summary>
    /// Adds valid perk definitions into the supplied lookup without overwriting earlier matches.
    /// </summary>
    private static void AddPerksToLookup(
        IDictionary<string, HideoutPerkDefinition> perkLookup,
        IEnumerable<HideoutPerkDefinition> perkDefinitions)
    {
        if (perkLookup == null || perkDefinitions == null)
            return;

        foreach (HideoutPerkDefinition perkDefinition in perkDefinitions)
        {
            if (perkDefinition == null || string.IsNullOrWhiteSpace(perkDefinition.PerkId))
                continue;

            string perkId = perkDefinition.PerkId.Trim();
            if (!perkLookup.ContainsKey(perkId))
                perkLookup.Add(perkId, perkDefinition);
        }
    }

    /// <summary>
    /// Converts equipped perk definitions into sanitized stable identifiers for save persistence.
    /// </summary>
    private static List<string> BuildEquippedPerkIdList(IEnumerable<HideoutPerkDefinition> perkDefinitions)
    {
        List<string> perkIds = new();
        if (perkDefinitions == null)
            return perkIds;

        HashSet<string> uniquePerkIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (HideoutPerkDefinition perkDefinition in perkDefinitions)
        {
            string perkId = perkDefinition != null ? perkDefinition.PerkId?.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(perkId) || !uniquePerkIds.Add(perkId))
                continue;

            perkIds.Add(perkId);
        }

        return perkIds;
    }
}

}
