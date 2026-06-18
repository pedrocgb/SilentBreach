using System;
using System.Collections.Generic;
using Breezeblocks.HideoutSystem;
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

    [Serializable]
    private sealed class EquippedPerksView
    {
        public GameObject root;
        public RectTransform contentRoot;
        public HideoutPerkItemUI itemPrefab;
        public TMP_Text emptyStateText;
        public Image selectedPerkIconImage;
        public TMP_Text selectedPerkNameText;
        public TMP_Text selectedPerkDescriptionText;
        public TMP_Text selectedPerkTierText;
        public TMP_Text selectedPerkCostText;
    }

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController equipmentController;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("References")]
    [SerializeField] private bool hideOnStart = true;

    [FoldoutGroup("References")]
    [SerializeField] private EquipmentContextSelectionMode contextSelectionMode = EquipmentContextSelectionMode.ClickOnly;

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

    [FoldoutGroup("Perks")]
    [SerializeField] private EquippedPerksView equippedPerksView = new();

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
    private readonly List<HideoutPerkDefinition> equippedPerks = new();
    private HideoutPerkDefinition selectedPerk;

    /// <summary>
    /// Resolves runtime references, subscribes to events, and draws the initial panel state.
    /// </summary>
    private void Awake()
    {
        isShuttingDown = false;
        contextSelectionMode = EquipmentContextSelectionMode.ClickOnly;
        ResolveReferences();
        PreparePerkTemplate();
        BindSlotEvents();
        Subscribe();
        if (hideOnStart)
            SetVisible(false);
        else
            SetVisible(IsVisible);

        Refresh();
    }

    /// <summary>
    /// Rebinds runtime references and refreshes the panel whenever it becomes active.
    /// </summary>
    private void OnEnable()
    {
        isShuttingDown = false;
        contextSelectionMode = EquipmentContextSelectionMode.ClickOnly;
        ResolveReferences();
        PreparePerkTemplate();
        BindSlotEvents();
        Subscribe();
        Refresh();
    }

    /// <summary>
    /// Detaches runtime listeners and clears transient selection state when the panel is disabled.
    /// </summary>
    private void OnDisable()
    {
        isShuttingDown = true;
        Unsubscribe();
        UnbindSlotEvents();
        activeContextSlot = EquipmentSlotType.None;
    }

    /// <summary>
    /// Shows or hides the equipment panel and refreshes its contents when it becomes visible.
    /// </summary>
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

    /// <summary>
    /// Forces the context area to display the supplied item instead of following slot hover or click state.
    /// </summary>
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

    /// <summary>
    /// Forces the context area to show the empty-selection state.
    /// </summary>
    public void ShowNoSelectionContext()
    {
        if (isShuttingDown)
            return;

        ClearManualContextState();
        showManualNoSelectionOverride = true;
        RefreshManualContext();
    }

    /// <summary>
    /// Returns context display ownership to the currently selected or hovered slot.
    /// </summary>
    public void ClearManualContext()
    {
        if (isShuttingDown)
            return;

        ClearManualContextState();
        RefreshActiveContext();
    }

    /// <summary>
    /// Refreshes the read-only gameplay perk section after runtime perk changes.
    /// </summary>
    public void RefreshEquippedPerksFromRuntime()
    {
        RefreshEquippedPerks();
    }

    /// <summary>
    /// Resolves optional scene references that this panel depends on at runtime.
    /// </summary>
    private void ResolveReferences()
    {
        if (equipmentController == null)
            equipmentController = FindFirstObjectByType<PlayerEquipmentController>();

        if (panelRoot == null)
            panelRoot = gameObject;
    }

    /// <summary>
    /// Subscribes to equipment change notifications so the panel stays synchronized with gameplay state.
    /// </summary>
    private void Subscribe()
    {
        if (equipmentController != null)
        {
            equipmentController.EquipmentChanged -= Refresh;
            equipmentController.EquipmentChanged += Refresh;
        }
    }

    /// <summary>
    /// Removes runtime event subscriptions owned by this panel.
    /// </summary>
    private void Unsubscribe()
    {
        if (equipmentController != null)
            equipmentController.EquipmentChanged -= Refresh;
    }

    /// <summary>
    /// Hooks slot pointer and drag events for every configured slot view.
    /// </summary>
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

    /// <summary>
    /// Unhooks all slot view events and disables drag handling while the panel is inactive.
    /// </summary>
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

    /// <summary>
    /// Rebuilds slot visuals, perk visuals, and the active context from current runtime state.
    /// </summary>
    private void Refresh()
    {
        if (isShuttingDown)
            return;

        RefreshEquippedPerks();

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
                activeContextSlot == slotType,
                ResolveSlotLabel(slotType),
                ResolveHotkeyLabel(slotType));
        }

        RefreshActiveContext();
    }

    /// <summary>
    /// Synchronizes the read-only equipped perk strip and the selected perk details.
    /// </summary>
    private void RefreshEquippedPerks()
    {
        SyncEquippedPerksFromRuntime();
        bool hasEquippedPerks = equippedPerks.Count > 0;
        SetEquippedPerksRootVisible(hasEquippedPerks);
        EnsureValidPerkSelection();
        RebuildEquippedPerkList();
        RefreshSelectedPerkDetails();
    }

    /// <summary>
    /// Copies the currently equipped perk definitions from the shared runtime loadout.
    /// </summary>
    private void SyncEquippedPerksFromRuntime()
    {
        equippedPerks.Clear();

        PlayerPerkRuntimeLoadout runtimeLoadout = PlayerPerkRuntimeSession.PeekEquippedPerks();
        IReadOnlyList<HideoutPerkDefinition> runtimePerks = runtimeLoadout.EquippedPerks;
        for (int i = 0; i < runtimePerks.Count; i++)
        {
            HideoutPerkDefinition perkDefinition = runtimePerks[i];
            if (perkDefinition == null)
                continue;

            equippedPerks.Add(perkDefinition);
        }
    }

    /// <summary>
    /// Keeps the selected perk reference pointing at a currently equipped runtime instance when possible.
    /// </summary>
    private void EnsureValidPerkSelection()
    {
        if (selectedPerk != null)
        {
            for (int i = 0; i < equippedPerks.Count; i++)
            {
                if (AreSamePerk(selectedPerk, equippedPerks[i]))
                {
                    selectedPerk = equippedPerks[i];
                    return;
                }
            }
        }

        selectedPerk = equippedPerks.Count > 0 ? equippedPerks[0] : null;
    }

    /// <summary>
    /// Hides the configured perk item template so only spawned runtime entries are shown.
    /// </summary>
    private void PreparePerkTemplate()
    {
        if (equippedPerksView.itemPrefab != null)
            equippedPerksView.itemPrefab.gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows the gameplay perk section only when at least one runtime perk exists.
    /// </summary>
    private void SetEquippedPerksRootVisible(bool visible)
    {
        if (equippedPerksView.root != null)
            equippedPerksView.root.SetActive(visible);
    }

    /// <summary>
    /// Recreates the equipped perk list from the current runtime perk collection.
    /// </summary>
    private void RebuildEquippedPerkList()
    {
        Transform preservedTemplate = ResolvePreservedTemplate(
            equippedPerksView.contentRoot,
            equippedPerksView.itemPrefab != null ? equippedPerksView.itemPrefab.transform : null);
        ClearGeneratedChildren(equippedPerksView.contentRoot, preservedTemplate);

        bool showEmptyState = equippedPerks.Count == 0;
        SetOptionalTextState(equippedPerksView.emptyStateText, showEmptyState, showEmptyState ? "No perks equipped." : string.Empty);

        if (equippedPerksView.contentRoot == null || equippedPerksView.itemPrefab == null)
            return;

        for (int i = 0; i < equippedPerks.Count; i++)
        {
            HideoutPerkDefinition perkDefinition = equippedPerks[i];
            HideoutPerkItemUI itemView = Instantiate(equippedPerksView.itemPrefab, equippedPerksView.contentRoot);
            itemView.gameObject.name = $"{perkDefinition.PerkName} Equipped";
            itemView.gameObject.SetActive(true);
            itemView.Bind(
                perkDefinition,
                AreSamePerk(selectedPerk, perkDefinition),
                true,
                true,
                false,
                false,
                false,
                false,
                () => SelectPerk(perkDefinition),
                null,
                null);
        }
    }

    /// <summary>
    /// Marks one equipped perk as selected and refreshes the perk detail display.
    /// </summary>
    private void SelectPerk(HideoutPerkDefinition perkDefinition)
    {
        selectedPerk = perkDefinition;
        RebuildEquippedPerkList();
        RefreshSelectedPerkDetails();
    }

    /// <summary>
    /// Updates the equipped-perk detail panel from the currently selected perk.
    /// </summary>
    private void RefreshSelectedPerkDetails()
    {
        if (selectedPerk == null)
        {
            SetImage(equippedPerksView.selectedPerkIconImage, null);
            SetPlainText(equippedPerksView.selectedPerkNameText, string.Empty, false);
            SetPlainText(equippedPerksView.selectedPerkDescriptionText, string.Empty, false);
            SetPlainText(equippedPerksView.selectedPerkTierText, string.Empty, false);
            SetPlainText(equippedPerksView.selectedPerkCostText, string.Empty, false);
            return;
        }

        EquipmentContextUiSettings settings = ResolveUiSettings();
        SetImage(equippedPerksView.selectedPerkIconImage, selectedPerk.Icon);
        SetPlainText(equippedPerksView.selectedPerkNameText, selectedPerk.PerkName, true);
        SetPlainText(equippedPerksView.selectedPerkDescriptionText, BuildPerkDescription(selectedPerk), true);
        SetPlainText(
            equippedPerksView.selectedPerkTierText,
            $"{settings.PerkTierText}{ResolvePerkTierText(selectedPerk.Tier)}",
            true);
        SetPlainText(
            equippedPerksView.selectedPerkCostText,
            $"{settings.PerkCostText}{selectedPerk.Cost}",
            true);
    }

    /// <summary>
    /// Resolves the current equipment item occupying the supplied slot.
    /// </summary>
    private EquipmentItemData ResolveItemForSlot(EquipmentSlotType slotType)
    {
        if (equipmentController == null)
            return null;

        return slotType == EquipmentSlotType.Armor
            ? equipmentController.EquippedArmorItem
            : equipmentController.GetItemInSlot(slotType);
    }

    /// <summary>
    /// Shows hovered slot context when the panel is configured to react to pointer hover.
    /// </summary>
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

    /// <summary>
    /// Clears hover-driven context when the pointer leaves the active slot.
    /// </summary>
    private void HandleSlotPointerExited(PlayerEquipmentSlotViewUI slotView)
    {
        if (contextSelectionMode == EquipmentContextSelectionMode.ClickOnly)
            return;

        if (slotView == null || activeContextSlot != slotView.SlotType)
            return;

        activeContextSlot = EquipmentSlotType.None;
        HideAllContexts();
    }

    /// <summary>
    /// Selects a slot, equips it when applicable, and refreshes the related context.
    /// </summary>
    private void HandleSlotClicked(PlayerEquipmentSlotViewUI slotView)
    {
        if (slotView == null)
            return;

        ClearManualContextState();
        activeContextSlot = slotView.SlotType;
        if (equipmentController != null && slotView.SlotType.IsHandSlot())
            equipmentController.TryEquipSlot(slotView.SlotType);

        Refresh();
    }

    /// <summary>
    /// Moves an item between slots after a successful drag-and-drop operation.
    /// </summary>
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

    /// <summary>
    /// Chooses which context state should currently be displayed for the panel.
    /// </summary>
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

    /// <summary>
    /// Displays context for the equipment item currently occupying the supplied slot.
    /// </summary>
    private void ShowContextForSlot(EquipmentSlotType slotType)
    {
        EquipmentItemData item = ResolveItemForSlot(slotType);
        ShowContextForItemInternal(item, slotType);
    }

    /// <summary>
    /// Populates the firearm detail context using the supplied item and runtime ammo state.
    /// </summary>
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
        SetPrefixedText(firearmContext.firearmSlotsText, settings.ItemKindPrefix, ResolveItemKindText(settings, firearmData.ItemKind), true);
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

    /// <summary>
    /// Populates the utility detail context using the supplied item and runtime quantity state.
    /// </summary>
    private void PopulateUtilityContext(UtilityItemData utilityItemData, EquipmentSlotType slotType, int quantityOverride = -1)
    {
        EquipmentContextUiSettings settings = ResolveUiSettings();
        SetActive(utilityContext.root, true);
        HideUtilityContextDetailFields();

        SetImage(utilityContext.iconImage, utilityItemData.Icon);
        SetPlainText(utilityContext.nameText, utilityItemData.DisplayName, true);
        SetPlainText(utilityContext.descriptionText, utilityItemData.Description, true);
        SetPrefixedText(utilityContext.slotsText, settings.ItemKindPrefix, ResolveItemKindText(settings, utilityItemData.ItemKind), true);

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
        else if (utilityItemData is LockpickUtilityData lockpickData)
        {
            int remainingUses = quantityOverride >= 0 ? quantityOverride : lockpickData.MaxUses;
            if (quantityOverride < 0 &&
                equipmentController != null &&
                equipmentController.TryGetRuntimeLockpickState(slotType, out int runtimeRemainingUses, out _))
            {
                remainingUses = runtimeRemainingUses;
            }

            quantityValue = remainingUses.ToString();
            showQuantity = true;
        }

        SetPrefixedText(utilityContext.quantityText, settings.QuantityPrefix, quantityValue, showQuantity);

        if (utilityItemData is not ThrowableUtilityData lethalThrowableData)
            return;

        bool showLethal = lethalThrowableData.UsesDirectDamage || lethalThrowableData.UsesExplosion;
        SetPrefixedText(
            utilityContext.lethalText,
            settings.LethalPrefix,
            ResolveBoolText(settings, ResolveThrowableIsLethal(lethalThrowableData)),
            showLethal);

        if (!lethalThrowableData.UsesExplosion && !lethalThrowableData.UsesFlashbang)
            return;

        SetPrefixedText(
            utilityContext.explosionRadiusText,
            settings.ExplosionRadiusPrefix,
            $"{lethalThrowableData.EffectRadius:0.##}m",
            true);
        SetPrefixedText(
            utilityContext.detonationModeText,
            settings.ExplosionTypePrefix,
            ResolveDetonationModeText(settings, lethalThrowableData.DetonationMode),
            true);

        bool showDelay = lethalThrowableData.DetonationMode == ThrowableDetonationMode.OnTimer ||
                         lethalThrowableData.DetonationMode == ThrowableDetonationMode.OnHitAndTimer;
        SetPrefixedText(
            utilityContext.detonationDelayText,
            settings.DetonationDelayPrefix,
            $"{lethalThrowableData.DetonationDelay:0.##}s",
            showDelay);
        SetPrefixedText(
            utilityContext.flashbangDurationText,
            settings.FlashbangDurationPrefix,
            $"{lethalThrowableData.FlashbangDuration:0.##}s",
            lethalThrowableData.UsesFlashbang);
    }

    /// <summary>
    /// Populates the melee detail context using the supplied weapon definition.
    /// </summary>
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
        SetPrefixedText(firearmContext.meleeSlotsText, settings.ItemKindPrefix, ResolveItemKindText(settings, meleeWeaponData.ItemKind), true);
    }

    /// <summary>
    /// Populates and reveals the armor equipment context.
    /// </summary>
    private void PopulateArmorContext(ArmorData armorData)
    {
        EquipmentContextUiSettings settings = ResolveUiSettings();
        HideArmorContextDetailFields();

        SetImage(armorContext.iconImage, armorData.Icon);
        SetPlainText(armorContext.nameText, armorData.DisplayName, true);
        SetPlainText(armorContext.descriptionText, armorData.Description, true);
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
        SetActive(armorContext.root, true);
    }

    /// <summary>
    /// Hides every context root before a new context state is selected.
    /// </summary>
    private void HideAllContexts()
    {
        SetActive(firearmContext.root, false);
        SetActive(utilityContext.root, false);
        SetActive(armorContext.root, false);
        SetActive(noSelectionContextRoot, false);
    }

    /// <summary>
    /// Reapplies the current manual context override when one is active.
    /// </summary>
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

    /// <summary>
    /// Routes the supplied item to the correct specialized context renderer.
    /// </summary>
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

    /// <summary>
    /// Clears the bookkeeping used by manual context overrides.
    /// </summary>
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

    /// <summary>
    /// Hides all firearm and melee context detail rows before they are repopulated.
    /// </summary>
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

    /// <summary>
    /// Hides all utility context detail rows before they are repopulated.
    /// </summary>
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

    /// <summary>
    /// Hides all armor context detail rows before they are repopulated.
    /// </summary>
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

    /// <summary>
    /// Resolves the projectile definition currently relevant for firearm context display.
    /// </summary>
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

    /// <summary>
    /// Returns the first compatible projectile as a fallback UI reference.
    /// </summary>
    private static ProjectileData ResolvePrimaryCompatibleProjectile(FirearmData firearmData)
    {
        return firearmData != null && firearmData.CompatibleProjectiles.Count > 0
            ? firearmData.CompatibleProjectiles[0]
            : null;
    }

    /// <summary>
    /// Compares two perks using their stable runtime identifier.
    /// </summary>
    private static bool AreSamePerk(HideoutPerkDefinition left, HideoutPerkDefinition right)
    {
        if (left == null || right == null)
            return false;

        return string.Equals(left.PerkId, right.PerkId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the detail string used for perk description panels.
    /// </summary>
    private static string BuildPerkDescription(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null)
            return string.Empty;

        bool hasDescription = !string.IsNullOrWhiteSpace(perkDefinition.Description);
        bool hasEffect = !string.IsNullOrWhiteSpace(perkDefinition.Effect);
        if (hasDescription && hasEffect)
            return $"{perkDefinition.Description}\n{perkDefinition.Effect}";

        if (hasDescription)
            return perkDefinition.Description;

        return hasEffect ? perkDefinition.Effect : string.Empty;
    }

    /// <summary>
    /// Converts a perk tier enum into its Roman numeral label.
    /// </summary>
    private static string ResolvePerkTierText(HideoutPerkTier perkTier)
    {
        return perkTier switch
        {
            HideoutPerkTier.TierI => "I",
            HideoutPerkTier.TierII => "II",
            HideoutPerkTier.TierIII => "III",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Returns the global equipment UI settings or a local fallback instance when unavailable.
    /// </summary>
    private static EquipmentContextUiSettings ResolveUiSettings()
    {
        return GlobalSettings.Instance != null ? GlobalSettings.Instance.EquipmentContextUi : DefaultUiSettings;
    }

    /// <summary>
    /// Returns the localized rounds-per-second suffix used by firearm rate labels.
    /// </summary>
    private static string ResolveRoundsPerSecondText(EquipmentContextUiSettings settings)
    {
        return settings != null ? settings.RoundsPerSecondText : "rounds/s";
    }

    /// <summary>
    /// Returns the localized boolean label used by equipment context rows.
    /// </summary>
    private static string ResolveBoolText(EquipmentContextUiSettings settings, bool value)
    {
        return settings != null ? settings.GetBoolText(value) : (value ? "Yes" : "No");
    }

    /// <summary>
    /// Returns the localized label for a throwable utility behavior.
    /// </summary>
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

    /// <summary>
    /// Returns the localized label for a firearm class.
    /// </summary>
    private static string ResolveFirearmClassText(EquipmentContextUiSettings settings, FirearmClass firearmClass)
    {
        return settings != null ? settings.GetFirearmClassText(firearmClass) : NicifyText(firearmClass.ToString());
    }

    /// <summary>
    /// Returns the localized label for a firearm grip type.
    /// </summary>
    private static string ResolveFirearmGripText(EquipmentContextUiSettings settings, FirearmGripType gripType)
    {
        return settings != null ? settings.GetFirearmGripText(gripType) : NicifyText(gripType.ToString());
    }

    /// <summary>
    /// Returns the localized label for a melee grip type.
    /// </summary>
    private static string ResolveMeleeGripText(EquipmentContextUiSettings settings, MeleeGripType gripType)
    {
        return settings != null ? settings.GetMeleeGripText(gripType) : NicifyText(gripType.ToString());
    }

    /// <summary>
    /// Returns the localized label for a throwable detonation mode.
    /// </summary>
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

    /// <summary>
    /// Determines whether the supplied throwable item should be presented as lethal.
    /// </summary>
    private static bool ResolveThrowableIsLethal(ThrowableUtilityData throwableData)
    {
        if (throwableData == null)
            return false;

        if (throwableData.UsesDirectDamage)
            return throwableData.DirectHitIsLethal;

        if (throwableData.UsesExplosion)
            return throwableData.ExplosionIsLethal;

        return false;
    }

    /// <summary>
    /// Returns the localized slot label shown on each equipment slot view.
    /// </summary>
    private static string ResolveSlotLabel(EquipmentSlotType slotType)
    {
        return ResolveUiSettings().GetSlotDisplayName(slotType);
    }

    /// <summary>
    /// Returns the hotkey label shown on each equipment slot view.
    /// </summary>
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

    /// <summary>
    /// Returns the localized display label for an equipment item kind.
    /// </summary>
    private static string ResolveItemKindText(EquipmentContextUiSettings settings, EquipmentItemKind itemKind)
    {
        return settings != null ? settings.GetItemKindText(itemKind) : NicifyText(itemKind.ToString());
    }

    /// <summary>
    /// Converts a firearm fire mode flag set into a readable display string.
    /// </summary>
    private static string FormatFireModes(FireMode fireModes)
    {
        if (fireModes == FireMode.None)
            return "None";

        string[] names = fireModes.ToString().Split(',');
        for (int i = 0; i < names.Length; i++)
            names[i] = NicifyText(names[i].Trim());

        return string.Join(" / ", names);
    }

    /// <summary>
    /// Inserts spaces into compact enum-style labels so they read cleanly in UI.
    /// </summary>
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

    /// <summary>
    /// Returns the template child that should be preserved while rebuilding a content list.
    /// </summary>
    private static Transform ResolvePreservedTemplate(RectTransform contentRoot, Transform templateTransform)
    {
        if (contentRoot == null || templateTransform == null)
            return null;

        return templateTransform.parent == contentRoot ? templateTransform : null;
    }

    /// <summary>
    /// Destroys generated content children while keeping an optional template child intact.
    /// </summary>
    private static void ClearGeneratedChildren(RectTransform contentRoot, Transform preservedTemplate)
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (child == preservedTemplate)
                continue;

            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Toggles an optional text field and assigns its content when it should be visible.
    /// </summary>
    private static void SetOptionalTextState(TMP_Text textField, bool visible, string value)
    {
        if (textField == null)
            return;

        textField.gameObject.SetActive(visible);
        if (visible)
            textField.text = value ?? string.Empty;
    }

    /// <summary>
    /// Applies plain text to a field while toggling its visibility container.
    /// </summary>
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

    /// <summary>
    /// Applies prefixed rich text to a field while toggling its visibility container.
    /// </summary>
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

    /// <summary>
    /// Hides the visibility container associated with a text field.
    /// </summary>
    private static void HideTextObject(TMP_Text textField)
    {
        if (textField == null)
            return;

        GameObject visibilityTarget = ResolveVisibilityTarget(textField);
        if (visibilityTarget != null)
            visibilityTarget.SetActive(false);
    }

    /// <summary>
    /// Resolves which GameObject should be toggled when showing or hiding a text field.
    /// </summary>
    private static GameObject ResolveVisibilityTarget(TMP_Text textField)
    {
        if (textField == null)
            return null;

        Transform parent = textField.transform.parent;
        return parent != null ? parent.gameObject : textField.gameObject;
    }

    /// <summary>
    /// Updates an image sprite and enables the image only when a sprite is available.
    /// </summary>
    private static void SetImage(Image imageField, Sprite sprite)
    {
        if (imageField == null)
            return;

        imageField.sprite = sprite;
        imageField.enabled = sprite != null;
    }

    /// <summary>
    /// Safely toggles a GameObject active state when the reference exists.
    /// </summary>
    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }
}
}
