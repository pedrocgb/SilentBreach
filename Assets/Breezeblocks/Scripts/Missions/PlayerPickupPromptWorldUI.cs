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

    private void Awake()
    {
        if (pickupInteractor == null)
            pickupInteractor = GetComponentInParent<PlayerPickupInteractor>();

        Refresh();
    }

    private void OnEnable()
    {
        if (pickupInteractor == null)
            pickupInteractor = GetComponentInParent<PlayerPickupInteractor>();

        if (pickupInteractor != null)
            pickupInteractor.CurrentInteractableChanged += HandleInteractableChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (pickupInteractor != null)
            pickupInteractor.CurrentInteractableChanged -= HandleInteractableChanged;
    }

    private void HandleInteractableChanged(PlayerWorldInteractable interactable)
    {
        Refresh();
    }

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
