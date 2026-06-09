using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Player Interact Prompt World UI")]
public class PlayerPickupPromptWorldUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private PlayerPickupInteractor pickupInteractor;

    [FoldoutGroup("UI")]
    [SerializeField] private GameObject pickUpPromptRoot;

    [FoldoutGroup("UI")]
    [SerializeField] private Image pickUpPromptImage;

    /// <summary>
    /// Resolves the pickup interactor reference and applies the initial prompt visibility.
    /// </summary>
    private void Awake()
    {
        if (pickupInteractor == null)
            pickupInteractor = GetComponentInParent<PlayerPickupInteractor>();

        Refresh();
    }

    /// <summary>
    /// Subscribes to interactor changes so the prompt tracks the nearest interactable.
    /// </summary>
    private void OnEnable()
    {
        if (pickupInteractor == null)
            pickupInteractor = GetComponentInParent<PlayerPickupInteractor>();

        if (pickupInteractor != null)
            pickupInteractor.CurrentInteractableChanged += HandleInteractableChanged;

        Refresh();
    }

    /// <summary>
    /// Unsubscribes from interactor events when the prompt goes inactive.
    /// </summary>
    private void OnDisable()
    {
        if (pickupInteractor != null)
            pickupInteractor.CurrentInteractableChanged -= HandleInteractableChanged;
    }

    /// <summary>
    /// Refreshes prompt visibility whenever the current interactable changes.
    /// </summary>
    private void HandleInteractableChanged(PlayerWorldInteractable interactable)
    {
        Refresh();
    }

    /// <summary>
    /// Shows the prompt only while interaction is possible and input is not blocked.
    /// </summary>
    private void Refresh()
    {
        bool visible = pickupInteractor != null && !pickupInteractor.IsInputBlocked && pickupInteractor.CurrentInteractable != null;
        if (pickUpPromptRoot != null)
            pickUpPromptRoot.SetActive(visible);

        if (pickUpPromptImage != null)
            pickUpPromptImage.enabled = visible;
    }
}

}
