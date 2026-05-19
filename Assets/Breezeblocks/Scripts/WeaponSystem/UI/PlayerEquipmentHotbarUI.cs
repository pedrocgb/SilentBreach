using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/UI/Player Equipment Hotbar")]
public class PlayerEquipmentHotbarUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController equipmentController;

    [FoldoutGroup("References"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<PlayerEquipmentSlotViewUI> slotViews = new();

    private void Awake()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (equipmentController == null)
            equipmentController = FindFirstObjectByType<PlayerEquipmentController>();
    }

    private void Subscribe()
    {
        if (equipmentController != null)
        {
            equipmentController.EquipmentChanged -= Refresh;
            equipmentController.EquipmentChanged += Refresh;
        }
    }

    private void Unsubscribe()
    {
        if (equipmentController != null)
            equipmentController.EquipmentChanged -= Refresh;
    }

    private void Refresh()
    {
        if (equipmentController == null)
            return;

        for (int i = 0; i < slotViews.Count; i++)
        {
            PlayerEquipmentSlotViewUI slotView = slotViews[i];
            if (slotView == null)
                continue;

            slotView.SetDragAndDropEnabled(false);
            EquipmentSlotType slotType = slotView.SlotType;
            slotView.Refresh(
                equipmentController.GetItemInSlot(slotType),
                equipmentController.IsSlotCurrentlyHeld(slotType),
                ResolveSlotLabel(slotType),
                ResolveHotkeyLabel(slotType));
        }
    }

    private static string ResolveSlotLabel(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Primary => "Primary",
            EquipmentSlotType.Secondary => "Secondary",
            EquipmentSlotType.Belt => "Belt",
            EquipmentSlotType.Armor => "Armor",
            _ => string.Empty
        };
    }

    private static string ResolveHotkeyLabel(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Primary => "1",
            EquipmentSlotType.Secondary => "2",
            EquipmentSlotType.Belt => "3",
            _ => string.Empty
        };
    }
}
}
