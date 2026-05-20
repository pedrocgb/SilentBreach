using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Player Pickup Prompt World UI")]
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
            pickupInteractor.CurrentPickableChanged += HandlePickableChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (pickupInteractor != null)
            pickupInteractor.CurrentPickableChanged -= HandlePickableChanged;
    }

    private void HandlePickableChanged(PickableItemWorld pickable)
    {
        Refresh();
    }

    private void Refresh()
    {
        bool visible = pickupInteractor != null && !pickupInteractor.IsInputBlocked && pickupInteractor.CurrentPickable != null;
        if (pickUpPromptRoot != null)
            pickUpPromptRoot.SetActive(visible);

        if (pickUpPromptImage != null)
            pickUpPromptImage.enabled = visible;
    }
}

}
