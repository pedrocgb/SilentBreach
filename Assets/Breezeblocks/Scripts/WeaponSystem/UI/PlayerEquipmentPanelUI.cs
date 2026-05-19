using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/UI/Player Equipment Panel")]
public class PlayerEquipmentPanelUI : MonoBehaviour
{
    [Serializable]
    private sealed class FirearmContextView
    {
        public GameObject root;
        public Image iconImage;
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public string classFormat = "Class: {0}";
        public TMP_Text classText;
        public string slotsFormat = "Slots: {0}";
        public TMP_Text slotsText;
        public string fireModeFormat = "Fire Mode: {0}";
        public TMP_Text fireModesText;
        public string ammoFormat = "Ammo: {0}/{1}";
        public TMP_Text ammoText;
        public string reloadFormat = "Reload: {0}";
        public TMP_Text reloadText;
    }

    [Serializable]
    private sealed class UtilityContextView
    {
        public GameObject root;
        public Image iconImage;
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public string typeFormat = "Type: {0}";
        public TMP_Text utilityTypeText;
        public string slotsFormat = "Slots: {0}";
        public TMP_Text slotsText;
        public string handlingFormat = "Handling: {0}";
        public TMP_Text handlingText;
    }

    [Serializable]
    private sealed class ArmorContextView
    {
        public GameObject root;
        public Image iconImage;
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public string armorClassFormat = "Armor Class: {0}";
        public TMP_Text armorClassText;
        public string armorValueFormat = "Armor Value: {0}";
        public TMP_Text armorValueText;
        public string rotationPenaltyFormat = "Rotation Penalty: {0}";
        public TMP_Text rotationPenaltyText;
        public string movementNoiseFormat = "Movement Noise: {0}";
        public TMP_Text movementNoiseText;
    }

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController equipmentController;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("References")]
    [SerializeField] private bool hideOnStart = true;

    [FoldoutGroup("References"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<PlayerEquipmentSlotViewUI> slotViews = new();

    [FoldoutGroup("Contexts")]
    [SerializeField] private FirearmContextView firearmContext = new();

    [FoldoutGroup("Contexts")]
    [SerializeField] private UtilityContextView utilityContext = new();

    [FoldoutGroup("Contexts")]
    [SerializeField] private ArmorContextView armorContext = new();

    [FoldoutGroup("Contexts")]
    [SerializeField] private GameObject noSelectionContextRoot;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    private EquipmentSlotType activeContextSlot = EquipmentSlotType.None;

    private void Awake()
    {
        ResolveReferences();
        BindSlotEvents();
        Subscribe();
        if (hideOnStart)
            SetVisible(false);
        else
            SetVisible(IsVisible);

        Refresh();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindSlotEvents();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        UnbindSlotEvents();
        activeContextSlot = EquipmentSlotType.None;
    }

    public void SetVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.SetActive(visible);

        if (!visible)
        {
            activeContextSlot = EquipmentSlotType.None;
            HideAllContexts();
            return;
        }

        Refresh();
    }

    private void ResolveReferences()
    {
        if (equipmentController == null)
            equipmentController = FindFirstObjectByType<PlayerEquipmentController>();

        if (panelRoot == null)
            panelRoot = gameObject;
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

    private void BindSlotEvents()
    {
        for (int i = 0; i < slotViews.Count; i++)
        {
            PlayerEquipmentSlotViewUI slotView = slotViews[i];
            if (slotView == null)
                continue;

            slotView.SetDragAndDropEnabled(slotView.SlotType.IsHandSlot());
            slotView.PointerEntered -= HandleSlotPointerEntered;
            slotView.PointerExited -= HandleSlotPointerExited;
            slotView.Clicked -= HandleSlotClicked;
            slotView.DropReceived -= HandleSlotDropReceived;
            slotView.PointerEntered += HandleSlotPointerEntered;
            slotView.PointerExited += HandleSlotPointerExited;
            slotView.Clicked += HandleSlotClicked;
            slotView.DropReceived += HandleSlotDropReceived;
        }
    }

    private void UnbindSlotEvents()
    {
        for (int i = 0; i < slotViews.Count; i++)
        {
            PlayerEquipmentSlotViewUI slotView = slotViews[i];
            if (slotView == null)
                continue;

            slotView.SetDragAndDropEnabled(false);
            slotView.PointerEntered -= HandleSlotPointerEntered;
            slotView.PointerExited -= HandleSlotPointerExited;
            slotView.Clicked -= HandleSlotClicked;
            slotView.DropReceived -= HandleSlotDropReceived;
        }
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

            EquipmentSlotType slotType = slotView.SlotType;
            slotView.Refresh(
                ResolveItemForSlot(slotType),
                equipmentController.IsSlotCurrentlyHeld(slotType),
                ResolveSlotLabel(slotType),
                ResolveHotkeyLabel(slotType));
        }

        RefreshActiveContext();
    }

    private EquipmentItemData ResolveItemForSlot(EquipmentSlotType slotType)
    {
        if (equipmentController == null)
            return null;

        return slotType == EquipmentSlotType.Armor
            ? equipmentController.EquippedArmorItem
            : equipmentController.GetItemInSlot(slotType);
    }

    private void HandleSlotPointerEntered(PlayerEquipmentSlotViewUI slotView)
    {
        if (slotView == null)
            return;

        activeContextSlot = slotView.SlotType;
        RefreshActiveContext();
    }

    private void HandleSlotPointerExited(PlayerEquipmentSlotViewUI slotView)
    {
        if (slotView == null || activeContextSlot != slotView.SlotType)
            return;

        activeContextSlot = EquipmentSlotType.None;
        HideAllContexts();
    }

    private void HandleSlotClicked(PlayerEquipmentSlotViewUI slotView)
    {
        if (slotView == null || equipmentController == null || !slotView.SlotType.IsHandSlot())
            return;

        activeContextSlot = slotView.SlotType;
        equipmentController.TryEquipSlot(slotView.SlotType);
        Refresh();
    }

    private void HandleSlotDropReceived(PlayerEquipmentSlotViewUI targetSlotView, PlayerEquipmentSlotViewUI sourceSlotView)
    {
        if (equipmentController == null || targetSlotView == null || sourceSlotView == null)
            return;

        if (!equipmentController.TryMoveItemBetweenSlots(sourceSlotView.SlotType, targetSlotView.SlotType))
            return;

        activeContextSlot = targetSlotView.SlotType;
        Refresh();
    }

    private void RefreshActiveContext()
    {
        if (!IsVisible)
            return;

        if (activeContextSlot == EquipmentSlotType.None)
        {
            HideAllContexts();
            return;
        }

        ShowContextForSlot(activeContextSlot);
    }

    private void ShowContextForSlot(EquipmentSlotType slotType)
    {
        EquipmentItemData item = ResolveItemForSlot(slotType);
        HideAllContexts();

        if (item == null)
        {
            if (noSelectionContextRoot != null)
                noSelectionContextRoot.SetActive(true);

            return;
        }

        switch (item)
        {
            case FirearmData firearmData:
                PopulateFirearmContext(firearmData, slotType);
                break;

            case MeleeWeaponData meleeWeaponData:
                PopulateMeleeContext(meleeWeaponData);
                break;

            case FlashlightUtilityData flashlightUtilityData:
                PopulateUtilityContext(flashlightUtilityData, slotType);
                break;

            case UtilityItemData utilityItemData:
                PopulateUtilityContext(utilityItemData, slotType);
                break;

            case ArmorData armorData:
                PopulateArmorContext(armorData);
                break;
        }
    }

    private void PopulateFirearmContext(FirearmData firearmData, EquipmentSlotType slotType)
    {
        if (firearmContext.root != null)
            firearmContext.root.SetActive(true);

        SetImage(firearmContext.iconImage, firearmData.Icon);
        SetText(firearmContext.nameText, firearmData.DisplayName);
        SetText(firearmContext.descriptionText, firearmData.Description);
        SetText(firearmContext.classText, string.Format(firearmContext.classFormat, firearmData.Class));
        SetText(firearmContext.slotsText, string.Format(firearmContext.slotsFormat, FormatAllowedSlots(firearmData.AllowedSlots)));
        SetText(firearmContext.fireModesText, string.Format(firearmContext.fireModeFormat, firearmData.Modes.ToString().Replace(", ", " / ")));

        int loadedAmmo = firearmData.AmmoCapacity;
        int reserveAmmo = firearmData.DefaultReserveAmmo;
        if (equipmentController != null)
            equipmentController.TryGetRuntimeFirearmState(slotType, out loadedAmmo, out reserveAmmo);

        SetText(firearmContext.ammoText, string.Format(firearmContext.ammoFormat, loadedAmmo, reserveAmmo));

        string reloadSummary = firearmData.ReloadStyle == ReloadType.BulletPerBullet
            ? $"Per bullet, {firearmData.ReloadTime:0.##}s each"
            : $"Magazine, {firearmData.ReloadTime:0.##}s";
        SetText(firearmContext.reloadText, string.Format(firearmContext.reloadFormat, reloadSummary));
    }

    private void PopulateUtilityContext(UtilityItemData utilityItemData, EquipmentSlotType slotType)
    {
        if (utilityContext.root != null)
            utilityContext.root.SetActive(true);

        SetImage(utilityContext.iconImage, utilityItemData.Icon);
        SetText(utilityContext.nameText, utilityItemData.DisplayName);
        SetText(utilityContext.descriptionText, utilityItemData.Description);
        SetText(utilityContext.utilityTypeText, string.Format(utilityContext.typeFormat, utilityItemData is FlashlightUtilityData ? "Flashlight" : utilityItemData.UtilityTypeName));
        SetText(utilityContext.slotsText, string.Format(utilityContext.slotsFormat, FormatAllowedSlots(utilityItemData.AllowedSlots)));

        string handlingSummary = $"Equip {utilityItemData.EquipTime:0.##}s | Holster {utilityItemData.HolsterTime:0.##}s";
        if (utilityItemData is ThrowableUtilityData throwableData &&
            equipmentController != null &&
            equipmentController.TryGetRuntimeThrowableState(slotType, out int remainingUses, out int maxUses))
        {
            handlingSummary = $"Uses {remainingUses}/{maxUses} | Throw {throwableData.MinTravelDistance:0.##}-{throwableData.MaxTravelDistance:0.##} | {handlingSummary}";
        }

        SetText(utilityContext.handlingText, string.Format(utilityContext.handlingFormat, handlingSummary));
    }

    private void PopulateMeleeContext(MeleeWeaponData meleeWeaponData)
    {
        if (utilityContext.root != null)
            utilityContext.root.SetActive(true);

        SetImage(utilityContext.iconImage, meleeWeaponData.Icon);
        SetText(utilityContext.nameText, meleeWeaponData.DisplayName);
        SetText(utilityContext.descriptionText, meleeWeaponData.Description);
        SetText(utilityContext.utilityTypeText, string.Format(utilityContext.typeFormat, $"{meleeWeaponData.GripType} Melee"));
        SetText(utilityContext.slotsText, string.Format(utilityContext.slotsFormat, FormatAllowedSlots(meleeWeaponData.AllowedSlots)));
        SetText(
            utilityContext.handlingText,
            string.Format(
                utilityContext.handlingFormat,
                $"Damage {meleeWeaponData.Damage:0.#} | Attack {meleeWeaponData.AttackAnimationDuration:0.##}s total | Swing {meleeWeaponData.AttackSwingDuration:0.##}s"));
    }

    private void PopulateArmorContext(ArmorData armorData)
    {
        if (armorContext.root != null)
            armorContext.root.SetActive(true);

        SetImage(armorContext.iconImage, armorData.Icon);
        SetText(armorContext.nameText, armorData.DisplayName);
        SetText(armorContext.descriptionText, armorData.Description);
        SetText(armorContext.armorClassText, string.Format(armorContext.armorClassFormat, armorData.ArmorClass));
        SetText(armorContext.armorValueText, string.Format(armorContext.armorValueFormat, armorData.ArmorValue.ToString("0.##")));
        SetText(armorContext.rotationPenaltyText, string.Format(armorContext.rotationPenaltyFormat, $"{armorData.RotationPenalty:0.#}%"));
        SetText(armorContext.movementNoiseText, string.Format(armorContext.movementNoiseFormat, $"+{armorData.MovementNoiseModifierPercent:0.#}%"));
    }

    private void HideAllContexts()
    {
        SetActive(firearmContext.root, false);
        SetActive(utilityContext.root, false);
        SetActive(armorContext.root, false);
        SetActive(noSelectionContextRoot, false);
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
            EquipmentSlotType.Armor => string.Empty,
            _ => string.Empty
        };
    }

    private static string FormatAllowedSlots(EquipmentSlotMask slotMask)
    {
        List<string> slotNames = new();
        if ((slotMask & EquipmentSlotMask.Primary) != 0)
            slotNames.Add("Primary");

        if ((slotMask & EquipmentSlotMask.Secondary) != 0)
            slotNames.Add("Secondary");

        if ((slotMask & EquipmentSlotMask.Belt) != 0)
            slotNames.Add("Belt");

        if ((slotMask & EquipmentSlotMask.Armor) != 0)
            slotNames.Add("Armor");

        return slotNames.Count > 0 ? string.Join(", ", slotNames) : "None";
    }

    private static void SetText(TMP_Text textField, string value)
    {
        if (textField != null)
            textField.text = value ?? string.Empty;
    }

    private static void SetImage(Image imageField, Sprite sprite)
    {
        if (imageField == null)
            return;

        imageField.sprite = sprite;
        imageField.enabled = sprite != null;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }
}
}
