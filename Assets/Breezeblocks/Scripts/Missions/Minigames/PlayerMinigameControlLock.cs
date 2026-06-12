using Breezeblocks.WeaponSystem;
using UnityEngine;

namespace Breezeblocks.Missions
{

/// <summary>
/// Blocks and restores player gameplay controls while a UI minigame owns input.
/// </summary>
public sealed class PlayerMinigameControlLock
{
    private PlayerEquipmentController playerEquipmentController;
    private PlayerTopDownMotor2D playerMotor;
    private PlayerVisionLight playerVisionLight;
    private PlayerWeaponController playerWeaponController;
    private PlayerUtilityController playerUtilityController;
    private PlayerMeleeController playerMeleeController;
    private PlayerPickupInteractor playerPickupInteractor;
    private PlayerFocusController playerFocusController;

    /// <summary>
    /// Resolves the gameplay-control components belonging to the supplied player root.
    /// </summary>
    public void Bind(GameObject playerRoot)
    {
        playerEquipmentController = playerRoot != null ? playerRoot.GetComponent<PlayerEquipmentController>() : null;
        playerMotor = playerRoot != null ? playerRoot.GetComponent<PlayerTopDownMotor2D>() : null;
        playerVisionLight = playerRoot != null ? playerRoot.GetComponentInChildren<PlayerVisionLight>(true) : null;
        playerWeaponController = playerRoot != null ? playerRoot.GetComponent<PlayerWeaponController>() : null;
        playerUtilityController = playerRoot != null ? playerRoot.GetComponent<PlayerUtilityController>() : null;
        playerMeleeController = playerRoot != null ? playerRoot.GetComponent<PlayerMeleeController>() : null;
        playerPickupInteractor = playerRoot != null ? playerRoot.GetComponent<PlayerPickupInteractor>() : null;
        playerFocusController = playerRoot != null ? playerRoot.GetComponent<PlayerFocusController>() : null;
    }

    /// <summary>
    /// Blocks or restores every player action that must remain unavailable during a minigame.
    /// </summary>
    public void SetBlocked(bool blocked)
    {
        playerEquipmentController?.SetInputBlocked(blocked);
        playerMotor?.SetInputBlocked(blocked);
        playerVisionLight?.SetInputBlocked(blocked);
        playerWeaponController?.SetInputBlocked(blocked);
        playerUtilityController?.SetInputBlocked(blocked);
        playerMeleeController?.SetInputBlocked(blocked);
        playerPickupInteractor?.SetInputBlocked(blocked);
        playerFocusController?.SetInputBlocked(blocked);
    }

    /// <summary>
    /// Releases cached player references after the owning minigame session ends.
    /// </summary>
    public void Clear()
    {
        playerEquipmentController = null;
        playerMotor = null;
        playerVisionLight = null;
        playerWeaponController = null;
        playerUtilityController = null;
        playerMeleeController = null;
        playerPickupInteractor = null;
        playerFocusController = null;
    }
}

}
