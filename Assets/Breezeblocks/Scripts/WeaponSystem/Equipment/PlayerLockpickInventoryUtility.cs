using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public static class PlayerLockpickInventoryUtility
{
    /// <summary>
    /// Returns whether the supplied player root owns at least one usable lockpick.
    /// </summary>
    public static bool HasAnyLockpickUses(GameObject interactorRoot)
    {
        return TryGetTotalLockpickUses(interactorRoot, out int totalUses) && totalUses > 0;
    }

    /// <summary>
    /// Reads total remaining lockpick uses from every player equipment slot.
    /// </summary>
    public static bool TryGetTotalLockpickUses(GameObject interactorRoot, out int totalUses)
    {
        totalUses = 0;
        PlayerEquipmentController equipmentController = ResolveEquipmentController(interactorRoot);
        return equipmentController != null && equipmentController.TryGetTotalLockpickUses(out totalUses);
    }

    /// <summary>
    /// Consumes one lockpick use from the supplied player equipment slots.
    /// </summary>
    public static bool TryConsumeLockpickUse(GameObject interactorRoot)
    {
        PlayerEquipmentController equipmentController = ResolveEquipmentController(interactorRoot);
        return equipmentController != null && equipmentController.TryConsumeLockpickUse();
    }

    /// <summary>
    /// Resolves the player equipment controller from the interaction root without requiring scene references.
    /// </summary>
    private static PlayerEquipmentController ResolveEquipmentController(GameObject interactorRoot)
    {
        if (interactorRoot == null)
            return null;

        if (interactorRoot.TryGetComponent(out PlayerEquipmentController directController))
            return directController;

        PlayerEquipmentController parentController = interactorRoot.GetComponentInParent<PlayerEquipmentController>();
        if (parentController != null)
            return parentController;

        return interactorRoot.GetComponentInChildren<PlayerEquipmentController>(true);
    }
}

}
