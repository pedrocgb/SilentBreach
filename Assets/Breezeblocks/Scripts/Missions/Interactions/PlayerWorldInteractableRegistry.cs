using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.Missions
{

/// <summary>
/// Tracks active world interactables and resolves the closest valid candidate for a player.
/// </summary>
public static class PlayerWorldInteractableRegistry
{
    private static readonly List<PlayerWorldInteractable> ActiveInteractablesInternal = new();

    /// <summary>
    /// Exposes the currently registered interactables for systems that only need read access.
    /// </summary>
    public static IReadOnlyList<PlayerWorldInteractable> ActiveInteractables => ActiveInteractablesInternal;

    /// <summary>
    /// Adds an interactable to the active registry if it is not already tracked.
    /// </summary>
    public static void Register(PlayerWorldInteractable interactable)
    {
        if (interactable == null || ActiveInteractablesInternal.Contains(interactable))
            return;

        ActiveInteractablesInternal.Add(interactable);
    }

    /// <summary>
    /// Removes an interactable from the active registry.
    /// </summary>
    public static void Unregister(PlayerWorldInteractable interactable)
    {
        if (interactable == null)
            return;

        ActiveInteractablesInternal.Remove(interactable);
    }

    /// <summary>
    /// Finds the closest interactable the supplied interactor is currently allowed to use.
    /// </summary>
    public static PlayerWorldInteractable FindClosestInteractable(Vector3 origin, float maxDistance, GameObject interactorRoot)
    {
        float maxDistanceSqr = Mathf.Max(0f, maxDistance);
        maxDistanceSqr *= maxDistanceSqr;

        PlayerWorldInteractable bestInteractable = null;
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < ActiveInteractablesInternal.Count; i++)
        {
            PlayerWorldInteractable candidate = ActiveInteractablesInternal[i];
            if (candidate == null || !candidate.CanInteract(interactorRoot))
                continue;

            float distanceSqr = ((Vector2)(candidate.InteractionPosition - origin)).sqrMagnitude;
            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestInteractable = candidate;
        }

        return bestInteractable;
    }
}

}
