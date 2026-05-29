using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Breezeblocks.WeaponSystem
{

[Serializable]
public enum EquipmentContextSelectionMode
{
    HoverOrClick,
    ClickOnly
}

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

        [FormerlySerializedAs("classText")]
        public TMP_Text firearmClassText;

        public TMP_Text firearmGripText;

        [FormerlySerializedAs("fireModesText")]
        public TMP_Text firearmFireModeText;

        public TMP_Text firearmFireRateText;
        public TMP_Text firearmSpreadText;

        [FormerlySerializedAs("ammoText")]
        public TMP_Text firearmAmmoText;

        public TMP_Text firearmReserveAmmoText;

        [FormerlySerializedAs("reloadText")]
        public TMP_Text firearmReloadTimeText;

        [FormerlySerializedAs("slotsText")]
        public TMP_Text firearmSlotsText;

        public TMP_Text firearmPenetrationText;
        public TMP_Text firearmLethalText;
        public TMP_Text meleeGripText;
        public TMP_Text meleeLethalText;
        public TMP_Text meleeStaminaCostText;
        public TMP_Text meleeArmorPenetrationText;
        public TMP_Text meleeSlotsText;
    }

    [Serializable]
    private sealed class UtilityContextView
    {
        public GameObject root;
        public Image iconImage;
        public TMP_Text nameText;
        public TMP_Text descriptionText;

        [FormerlySerializedAs("utilityTypeText")]
        public TMP_Text utilityTypeText;

        [FormerlySerializedAs("slotsText")]
        public TMP_Text slotsText;

        [FormerlySerializedAs("handlingText")]
        public TMP_Text quantityText;

        public TMP_Text detonationModeText;
        public TMP_Text detonationDelayText;
        public TMP_Text explosionRadiusText;
        public TMP_Text flashbangDurationText;
        public TMP_Text lethalText;
    }

    [Serializable]
    private sealed class ArmorContextView
    {
        public GameObject root;
        public Image iconImage;
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public TMP_Text armorClassText;
        public TMP_Text armorValueText;
        public TMP_Text rotationPenaltyText;
        public TMP_Text movementNoiseText;
        public TMP_Text movementSpeedPenaltyText;
    }

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController equipmentController;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("References")]
    [SerializeField] private bool hideOnStart = true;

    [FoldoutGroup("References")]
    [SerializeField] private EquipmentContextSelectionMode contextSelectionMode = EquipmentContextSelectionMode.HoverOrClick;

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

    private static readonly EquipmentContextUiSettings DefaultUiSettings = new();
    private EquipmentSlotType activeContextSlot = EquipmentSlotType.None;
    private EquipmentItemData manualContextItem;
    private EquipmentSlotType manualContextSlot = EquipmentSlotType.None;
    private ProjectileData manualContextProjectile;
    private int manualLoadedAmmo = -1;
    private int manualReserveAmmo = -1;
    private bool hasManualContextOverride;
    private bool showManualNoSelectionOverride;
    private bool isShuttingDown;

    private void Awake()
    {
        isShuttingDown = false;
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
        isShuttingDown = false;
        ResolveReferences();
        BindSlotEvents();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        isShuttingDown = true;
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

    public void ShowContextForItem(
        EquipmentItemData item,
        EquipmentSlotType slotType = EquipmentSlotType.None,
        ProjectileData firearmProjectile = null,
        int loadedAmmo = -1,
        int reserveAmmo = -1)
    {
        if (isShuttingDown)
            return;

        manualContextItem = item;
        manualContextSlot = slotType;
        manualContextProjectile = firearmProjectile;
        manualLoadedAmmo = loadedAmmo;
        manualReserveAmmo = reserveAmmo;
        hasManualContextOverride = item != null;
        showManualNoSelectionOverride = item == null;
        RefreshManualContext();
    }

    public void ShowNoSelectionContext()
    {
        if (isShuttingDown)
            return;

        ClearManualContextState();
        showManualNoSelectionOverride = true;
        RefreshManualContext();
    }

    public void ClearManualContext()
    {
        if (isShuttingDown)
            return;

        ClearManualContextState();
        RefreshActiveContext();
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
        if (isShuttingDown)
            return;

        if (equipmentController == null)
        {
            RefreshManualContext();
            return;
        }

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
        if (contextSelectionMode == EquipmentContextSelectionMode.ClickOnly)
            return;

        if (slotView == null)
            return;

        ClearManualContextState();
        activeContextSlot = slotView.SlotType;
        RefreshActiveContext();
    }

    private void HandleSlotPointerExited(PlayerEquipmentSlotViewUI slotView)
    {
        if (contextSelectionMode == EquipmentContextSelectionMode.ClickOnly)
            return;

        if (slotView == null || activeContextSlot != slotView.SlotType)
            return;

        activeContextSlot = EquipmentSlotType.None;
        HideAllContexts();
    }

    private void HandleSlotClicked(PlayerEquipmentSlotViewUI slotView)
    {
        if (slotView == null)
            return;

        ClearManualContextState();
        activeContextSlot = slotView.SlotType;
        if (equipmentController != null && slotView.SlotType.IsHandSlot())
        {
            equipmentController.TryEquipSlot(slotView.SlotType);
            Refresh();
            return;
        }

        RefreshActiveContext();
    }

    private void HandleSlotDropReceived(PlayerEquipmentSlotViewUI targetSlotView, PlayerEquipmentSlotViewUI sourceSlotView)
    {
        if (equipmentController == null || targetSlotView == null || sourceSlotView == null)
            return;

        if (!equipmentController.TryMoveItemBetweenSlots(sourceSlotView.SlotType, targetSlotView.SlotType))
            return;

        activeContextSlot = targetSlotView.SlotType;
        ClearManualContextState();
        Refresh();
    }

    private void RefreshActiveContext()
    {
        if (isShuttingDown)
            return;

        if (!IsVisible)
            return;

        if (hasManualContextOverride || showManualNoSelectionOverride)
        {
            RefreshManualContext();
            return;
        }

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
        ShowContextForItemInternal(item, slotType);
    }

    private void PopulateFirearmContext(
        FirearmData firearmData,
        EquipmentSlotType slotType,
        ProjectileData projectileOverride = null,
        int loadedAmmoOverride = -1,
        int reserveAmmoOverride = -1)
    {
        EquipmentContextUiSettings settings = ResolveUiSettings();
        SetActive(firearmContext.root, true);
        HideWeaponContextDetailFields();

        SetImage(firearmContext.iconImage, firearmData.Icon);
        SetPlainText(firearmContext.nameText, firearmData.DisplayName, true);
        SetPlainText(firearmContext.descriptionText, firearmData.Description, true);
        SetPrefixedText(firearmContext.firearmClassText, settings.ClassPrefix, ResolveFirearmClassText(settings, firearmData.Class), true);
        SetPrefixedText(firearmContext.firearmGripText, settings.GripPrefix, ResolveFirearmGripText(settings, firearmData.GripType), true);
        SetPrefixedText(firearmContext.firearmFireModeText, settings.FireModePrefix, FormatFireModes(firearmData.Modes), true);
        SetPrefixedText(
            firearmContext.firearmFireRateText,
            settings.FireRatePrefix,
            $"{firearmData.FireRate:0.##} {ResolveRoundsPerSecondText(settings)}",
            true);
        SetPrefixedText(firearmContext.firearmSpreadText, settings.SpreadPrefix, $"{firearmData.Spread:0.##}°", true);

        int loadedAmmo = loadedAmmoOverride >= 0 ? loadedAmmoOverride : firearmData.AmmoCapacity;
        int reserveAmmo = reserveAmmoOverride >= 0 ? reserveAmmoOverride : firearmData.DefaultReserveAmmo;
        if (loadedAmmoOverride < 0 && reserveAmmoOverride < 0 && equipmentController != null)
            equipmentController.TryGetRuntimeFirearmState(slotType, out loadedAmmo, out reserveAmmo);

        SetPrefixedText(firearmContext.firearmAmmoText, settings.AmmoPrefix, loadedAmmo.ToString(), true);
        SetPrefixedText(firearmContext.firearmReserveAmmoText, settings.ReserveAmmoPrefix, reserveAmmo.ToString(), true);
        SetPrefixedText(firearmContext.firearmReloadTimeText, settings.ReloadTimePrefix, $"{firearmData.ReloadTime:0.##}s", true);
        SetPrefixedText(firearmContext.firearmSlotsText, settings.SlotsPrefix, FormatAllowedSlots(firearmData.AllowedSlots, settings), true);
        ProjectileData primaryUiProjectile = ResolvePrimaryCompatibleProjectile(firearmData);
        ProjectileData activeProjectile = projectileOverride ?? ResolveFirearmProjectile(slotType, firearmData);
        SetPrefixedText(
            firearmContext.firearmPenetrationText,
            settings.FirearmPenetrationPrefix,
            ((activeProjectile ?? primaryUiProjectile) != null ? (activeProjectile ?? primaryUiProjectile).Penetration : 0).ToString(),
            true);
        SetPrefixedText(
            firearmContext.firearmLethalText,
            settings.LethalPrefix,
            ResolveBoolText(settings, activeProjectile?.IsLethal ?? true),
            true);
    }

    private void PopulateUtilityContext(UtilityItemData utilityItemData, EquipmentSlotType slotType, int quantityOverride = -1)
    {
        EquipmentContextUiSettings settings = ResolveUiSettings();
        SetActive(utilityContext.root, true);
        HideUtilityContextDetailFields();

        SetImage(utilityContext.iconImage, utilityItemData.Icon);
        SetPlainText(utilityContext.nameText, utilityItemData.DisplayName, true);
        SetPlainText(utilityContext.descriptionText, utilityItemData.Description, true);
        SetPrefixedText(utilityContext.slotsText, settings.SlotsPrefix, FormatAllowedSlots(utilityItemData.AllowedSlots, settings), true);

        bool isFlashlight = utilityItemData is FlashlightUtilityData;
        if (isFlashlight)
            return;

        string utilityType = utilityItemData is ThrowableUtilityData throwableData
            ? ResolveThrowableTypeText(settings, throwableData.Behavior)
            : utilityItemData.UtilityTypeName;
        SetPrefixedText(utilityContext.utilityTypeText, settings.UtilityTypePrefix, utilityType, true);

        string quantityValue = string.Empty;
        bool showQuantity = false;
        if (utilityItemData is ThrowableUtilityData throwableQuantityData)
        {
            int maxUses = quantityOverride >= 0 ? quantityOverride : throwableQuantityData.MaxUses;
            if (quantityOverride < 0 &&
                equipmentController != null &&
                equipmentController.TryGetRuntimeThrowableState(slotType, out _, out int runtimeMaxUses))
            {
                maxUses = runtimeMaxUses;
            }

            quantityValue = maxUses.ToString();
            showQuantity = true;
        }

        SetPrefixedText(utilityContext.quantityText, settings.QuantityPrefix, quantityValue, showQuantity);

        if (utilityItemData is not ThrowableUtilityData explosiveOrFlashbangData ||
            (!explosiveOrFlashbangData.UsesExplosion && !explosiveOrFlashbangData.UsesFlashbang))
        {
            return;
        }

        SetPrefixedText(
            utilityContext.explosionRadiusText,
            settings.ExplosionRadiusPrefix,
            $"{explosiveOrFlashbangData.EffectRadius:0.##}m",
            true);
        SetPrefixedText(
            utilityContext.detonationModeText,
            settings.ExplosionTypePrefix,
            ResolveDetonationModeText(settings, explosiveOrFlashbangData.DetonationMode),
            true);
        SetPrefixedText(
            utilityContext.lethalText,
            settings.LethalPrefix,
            ResolveBoolText(settings, ResolveThrowableIsLethal(explosiveOrFlashbangData)),
            true);

        bool showDelay = explosiveOrFlashbangData.DetonationMode == ThrowableDetonationMode.OnTimer ||
                         explosiveOrFlashbangData.DetonationMode == ThrowableDetonationMode.OnHitAndTimer;
        SetPrefixedText(
            utilityContext.detonationDelayText,
            settings.DetonationDelayPrefix,
            $"{explosiveOrFlashbangData.DetonationDelay:0.##}s",
            showDelay);
        SetPrefixedText(
            utilityContext.flashbangDurationText,
            settings.FlashbangDurationPrefix,
            $"{explosiveOrFlashbangData.FlashbangDuration:0.##}s",
            explosiveOrFlashbangData.UsesFlashbang);
    }

    private void PopulateMeleeContext(MeleeWeaponData meleeWeaponData)
    {
        EquipmentContextUiSettings settings = ResolveUiSettings();
        SetActive(firearmContext.root, true);
        HideWeaponContextDetailFields();

        SetImage(firearmContext.iconImage, meleeWeaponData.Icon);
        SetPlainText(firearmContext.nameText, meleeWeaponData.DisplayName, true);
        SetPlainText(firearmContext.descriptionText, meleeWeaponData.Description, true);
        SetPrefixedText(firearmContext.meleeGripText, settings.GripPrefix, ResolveMeleeGripText(settings, meleeWeaponData.GripType), true);
        SetPrefixedText(
            firearmContext.meleeLethalText,
            settings.LethalPrefix,
            ResolveBoolText(settings, meleeWeaponData.IsLethal),
            true);
        SetPrefixedText(firearmContext.meleeStaminaCostText, settings.StaminaCostPrefix, meleeWeaponData.StaminaCost.ToString("0.##"), true);
        SetPrefixedText(
            firearmContext.meleeArmorPenetrationText,
            settings.ArmorPenetrationPrefix,
            meleeWeaponData.ArmorPenetration.ToString(),
            true);
        SetPrefixedText(firearmContext.meleeSlotsText, settings.SlotsPrefix, FormatAllowedSlots(meleeWeaponData.AllowedSlots, settings), true);
    }

    private void PopulateArmorContext(ArmorData armorData)
    {
        EquipmentContextUiSettings settings = ResolveUiSettings();
        SetActive(armorContext.root, true);
        HideArmorContextDetailFields();

        SetImage(armorContext.iconImage, armorData.Icon);
        SetPlainText(armorContext.nameText, armorData.DisplayName, true);
        SetPlainText(armorContext.descriptionText, string.Empty, false);
        SetPrefixedText(armorContext.armorClassText, settings.ArmorClassPrefix, armorData.ArmorClass.ToString(), true);
        SetPrefixedText(armorContext.armorValueText, settings.ArmorValuePrefix, armorData.ArmorValue.ToString("0.##"), true);
        SetPrefixedText(armorContext.rotationPenaltyText, settings.RotationPenaltyPrefix, $"{armorData.RotationPenalty:0.##}%", true);
        SetPrefixedText(
            armorContext.movementNoiseText,
            settings.MovementNoiseIncreasePrefix,
            $"{armorData.MovementNoiseModifierPercent:0.##}%",
            true);
        SetPrefixedText(
            armorContext.movementSpeedPenaltyText,
            settings.MovementSpeedPenaltyPrefix,
            $"{armorData.MovementSpeedPenaltyPercent:0.##}%",
            true);
    }

    private void HideAllContexts()
    {
        SetActive(firearmContext.root, false);
        SetActive(utilityContext.root, false);
        SetActive(armorContext.root, false);
        SetActive(noSelectionContextRoot, false);
    }

    private void RefreshManualContext()
    {
        if (isShuttingDown)
            return;

        if (!IsVisible)
            return;

        if (hasManualContextOverride)
        {
            ShowContextForItemInternal(
                manualContextItem,
                manualContextSlot,
                manualContextProjectile,
                manualLoadedAmmo,
                manualReserveAmmo);
            return;
        }

        if (showManualNoSelectionOverride)
        {
            HideAllContexts();
            SetActive(noSelectionContextRoot, true);
        }
    }

    private void ShowContextForItemInternal(
        EquipmentItemData item,
        EquipmentSlotType slotType,
        ProjectileData projectileOverride = null,
        int loadedAmmoOverride = -1,
        int reserveAmmoOverride = -1)
    {
        HideAllContexts();

        if (item == null)
        {
            SetActive(noSelectionContextRoot, true);
            return;
        }

        switch (item)
        {
            case FirearmData firearmData:
                PopulateFirearmContext(firearmData, slotType, projectileOverride, loadedAmmoOverride, reserveAmmoOverride);
                break;

            case MeleeWeaponData meleeWeaponData:
                PopulateMeleeContext(meleeWeaponData);
                break;

            case FlashlightUtilityData flashlightUtilityData:
                PopulateUtilityContext(flashlightUtilityData, slotType, reserveAmmoOverride);
                break;

            case UtilityItemData utilityItemData:
                PopulateUtilityContext(utilityItemData, slotType, reserveAmmoOverride);
                break;

            case ArmorData armorData:
                PopulateArmorContext(armorData);
                break;

            default:
                SetActive(noSelectionContextRoot, true);
                break;
        }
    }

    private void ClearManualContextState()
    {
        manualContextItem = null;
        manualContextSlot = EquipmentSlotType.None;
        manualContextProjectile = null;
        manualLoadedAmmo = -1;
        manualReserveAmmo = -1;
        hasManualContextOverride = false;
        showManualNoSelectionOverride = false;
    }

    private void HideWeaponContextDetailFields()
    {
        HideTextObject(firearmContext.nameText);
        HideTextObject(firearmContext.descriptionText);
        HideTextObject(firearmContext.firearmClassText);
        HideTextObject(firearmContext.firearmGripText);
        HideTextObject(firearmContext.firearmFireModeText);
        HideTextObject(firearmContext.firearmFireRateText);
        HideTextObject(firearmContext.firearmSpreadText);
        HideTextObject(firearmContext.firearmAmmoText);
        HideTextObject(firearmContext.firearmReserveAmmoText);
        HideTextObject(firearmContext.firearmReloadTimeText);
        HideTextObject(firearmContext.firearmSlotsText);
        HideTextObject(firearmContext.firearmPenetrationText);
        HideTextObject(firearmContext.firearmLethalText);
        HideTextObject(firearmContext.meleeGripText);
        HideTextObject(firearmContext.meleeLethalText);
        HideTextObject(firearmContext.meleeStaminaCostText);
        HideTextObject(firearmContext.meleeArmorPenetrationText);
        HideTextObject(firearmContext.meleeSlotsText);
    }

    private void HideUtilityContextDetailFields()
    {
        HideTextObject(utilityContext.nameText);
        HideTextObject(utilityContext.descriptionText);
        HideTextObject(utilityContext.utilityTypeText);
        HideTextObject(utilityContext.slotsText);
        HideTextObject(utilityContext.quantityText);
        HideTextObject(utilityContext.detonationModeText);
        HideTextObject(utilityContext.detonationDelayText);
        HideTextObject(utilityContext.explosionRadiusText);
        HideTextObject(utilityContext.flashbangDurationText);
        HideTextObject(utilityContext.lethalText);
    }

    private void HideArmorContextDetailFields()
    {
        HideTextObject(armorContext.nameText);
        HideTextObject(armorContext.descriptionText);
        HideTextObject(armorContext.armorClassText);
        HideTextObject(armorContext.armorValueText);
        HideTextObject(armorContext.rotationPenaltyText);
        HideTextObject(armorContext.movementNoiseText);
        HideTextObject(armorContext.movementSpeedPenaltyText);
    }

    private ProjectileData ResolveFirearmProjectile(EquipmentSlotType slotType, FirearmData firearmData)
    {
        if (equipmentController != null &&
            equipmentController.TryGetRuntimeFirearmProjectile(slotType, out ProjectileData runtimeProjectile) &&
            runtimeProjectile != null)
        {
            return runtimeProjectile;
        }

        return firearmData != null && firearmData.CompatibleProjectiles.Count > 0
            ? firearmData.CompatibleProjectiles[0]
            : null;
    }

    private static ProjectileData ResolvePrimaryCompatibleProjectile(FirearmData firearmData)
    {
        return firearmData != null && firearmData.CompatibleProjectiles.Count > 0
            ? firearmData.CompatibleProjectiles[0]
            : null;
    }

    private static EquipmentContextUiSettings ResolveUiSettings()
    {
        return GlobalSettings.Instance != null ? GlobalSettings.Instance.EquipmentContextUi : DefaultUiSettings;
    }

    private static string ResolveRoundsPerSecondText(EquipmentContextUiSettings settings)
    {
        return settings != null ? settings.RoundsPerSecondText : "rounds/s";
    }

    private static string ResolveBoolText(EquipmentContextUiSettings settings, bool value)
    {
        return settings != null ? settings.GetBoolText(value) : (value ? "Yes" : "No");
    }

    private static string ResolveThrowableTypeText(EquipmentContextUiSettings settings, ThrowableUtilityBehavior behavior)
    {
        if (settings != null)
            return settings.GetThrowableBehaviorText(behavior);

        return behavior switch
        {
            ThrowableUtilityBehavior.NoiseMaker => "Noise Maker",
            ThrowableUtilityBehavior.DirectDamage => "Damage",
            ThrowableUtilityBehavior.Explosion => "Explosion",
            ThrowableUtilityBehavior.Flashbang => "Flashbang",
            _ => "Utility"
        };
    }

    private static string ResolveFirearmClassText(EquipmentContextUiSettings settings, FirearmClass firearmClass)
    {
        return settings != null ? settings.GetFirearmClassText(firearmClass) : NicifyText(firearmClass.ToString());
    }

    private static string ResolveFirearmGripText(EquipmentContextUiSettings settings, FirearmGripType gripType)
    {
        return settings != null ? settings.GetFirearmGripText(gripType) : NicifyText(gripType.ToString());
    }

    private static string ResolveMeleeGripText(EquipmentContextUiSettings settings, MeleeGripType gripType)
    {
        return settings != null ? settings.GetMeleeGripText(gripType) : NicifyText(gripType.ToString());
    }

    private static string ResolveDetonationModeText(EquipmentContextUiSettings settings, ThrowableDetonationMode detonationMode)
    {
        return settings != null ? settings.GetDetonationModeText(detonationMode) : detonationMode switch
        {
            ThrowableDetonationMode.OnHit => "On Hit",
            ThrowableDetonationMode.OnTimer => "On Timer",
            ThrowableDetonationMode.OnHitAndTimer => "On Hit and Timer",
            _ => "Detonation"
        };
    }

    private static bool ResolveThrowableIsLethal(ThrowableUtilityData throwableData)
    {
        if (throwableData == null)
            return false;

        if (throwableData.UsesExplosion)
            return throwableData.ExplosionDamage > 0f;

        return false;
    }

    private static string ResolveSlotLabel(EquipmentSlotType slotType)
    {
        return ResolveUiSettings().GetSlotDisplayName(slotType);
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

    private static string FormatAllowedSlots(EquipmentSlotMask slotMask, EquipmentContextUiSettings settings)
    {
        List<string> slotNames = new();
        if ((slotMask & EquipmentSlotMask.Primary) != 0)
            slotNames.Add(settings != null ? settings.GetSlotDisplayName(EquipmentSlotType.Primary) : "Primary");

        if ((slotMask & EquipmentSlotMask.Secondary) != 0)
            slotNames.Add(settings != null ? settings.GetSlotDisplayName(EquipmentSlotType.Secondary) : "Secondary");

        if ((slotMask & EquipmentSlotMask.Belt) != 0)
            slotNames.Add(settings != null ? settings.GetSlotDisplayName(EquipmentSlotType.Belt) : "Belt");

        if ((slotMask & EquipmentSlotMask.Armor) != 0)
            slotNames.Add(settings != null ? settings.GetSlotDisplayName(EquipmentSlotType.Armor) : "Armor");

        return slotNames.Count > 0 ? string.Join(", ", slotNames) : "None";
    }

    private static string FormatFireModes(FireMode fireModes)
    {
        if (fireModes == FireMode.None)
            return "None";

        string[] names = fireModes.ToString().Split(',');
        for (int i = 0; i < names.Length; i++)
            names[i] = NicifyText(names[i].Trim());

        return string.Join(" / ", names);
    }

    private static string NicifyText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim().Replace("_", " ");
        List<char> buffer = new List<char>(trimmed.Length * 2);
        for (int i = 0; i < trimmed.Length; i++)
        {
            char current = trimmed[i];
            if (i > 0 &&
                char.IsUpper(current) &&
                !char.IsWhiteSpace(trimmed[i - 1]) &&
                !char.IsUpper(trimmed[i - 1]))
            {
                buffer.Add(' ');
            }

            buffer.Add(current);
        }

        return new string(buffer.ToArray());
    }

    private static void SetPlainText(TMP_Text textField, string value, bool visible)
    {
        if (textField == null)
            return;

        GameObject visibilityTarget = ResolveVisibilityTarget(textField);
        if (visibilityTarget != null)
            visibilityTarget.SetActive(visible);

        if (visible)
            textField.text = value ?? string.Empty;
    }

    private static void SetPrefixedText(TMP_Text textField, string prefix, string value, bool visible)
    {
        if (textField == null)
            return;

        GameObject visibilityTarget = ResolveVisibilityTarget(textField);
        if (visibilityTarget != null)
            visibilityTarget.SetActive(visible);

        if (!visible)
            return;

        string resolvedPrefix = prefix ?? string.Empty;
        if (!string.IsNullOrEmpty(resolvedPrefix) && !char.IsWhiteSpace(resolvedPrefix[resolvedPrefix.Length - 1]))
            resolvedPrefix += " ";

        EquipmentContextUiSettings settings = ResolveUiSettings();
        string prefixHex = ColorUtility.ToHtmlStringRGB(settings.PrefixColor);
        string formattedPrefix = string.IsNullOrEmpty(resolvedPrefix)
            ? string.Empty
            : $"<color=#{prefixHex}>{resolvedPrefix}</color>";

        textField.text = $"{formattedPrefix}{value ?? string.Empty}";
    }

    private static void HideTextObject(TMP_Text textField)
    {
        if (textField == null)
            return;

        GameObject visibilityTarget = ResolveVisibilityTarget(textField);
        if (visibilityTarget != null)
            visibilityTarget.SetActive(false);
    }

    private static GameObject ResolveVisibilityTarget(TMP_Text textField)
    {
        if (textField == null)
            return null;

        Transform parent = textField.transform.parent;
        return parent != null ? parent.gameObject : textField.gameObject;
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
