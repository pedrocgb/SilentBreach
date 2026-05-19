using System;
using System.Collections;
using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Equipment/Player Equipment Controller")]
public class PlayerEquipmentController : MonoBehaviour
{
    [Serializable]
    private sealed class HandEquipmentSlotDefinition
    {
        [AssetsOnly]
        public EquipmentItemData item;

        [ShowIf(nameof(IsFirearmItem)), AssetsOnly]
        public ProjectileData firearmProjectile;

        [ShowIf(nameof(IsFirearmItem)), MinValue(-1)]
        public int startingLoadedAmmo = -1;

        [ShowIf(nameof(IsFirearmItem)), MinValue(-1)]
        public int startingReserveAmmo = -1;

        private bool IsFirearmItem => item is FirearmData;
    }

    private sealed class RuntimeHandSlotState
    {
        public EquipmentSlotType SlotType;
        public EquipmentItemData Item;
        public ProjectileData FirearmProjectile;
        public int LoadedAmmo;
        public int ReserveAmmo;
    }

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string equipPrimaryAction = "Equip Primary";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string equipSecondaryAction = "Equip Secondary";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string equipBeltAction = "Equip Belt";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string toggleEquipmentPanelAction = "Toggle Equipment Panel";

    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("References")]
    [SerializeField] private ArmorLoadout armorLoadout;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentPanelUI equipmentPanelUI;

    [FoldoutGroup("References")]
    [SerializeField] private DynamicCrosshairUI dynamicCrosshairUI;

    [FoldoutGroup("Starting Equipment/Hand Slots"), LabelText("Primary")]
    [SerializeField] private HandEquipmentSlotDefinition primaryEquipment = new();

    [FoldoutGroup("Starting Equipment/Hand Slots"), LabelText("Secondary")]
    [SerializeField] private HandEquipmentSlotDefinition secondaryEquipment = new();

    [FoldoutGroup("Starting Equipment/Hand Slots"), LabelText("Belt")]
    [SerializeField] private HandEquipmentSlotDefinition beltEquipment = new();

    [FoldoutGroup("Starting Equipment"), AssetsOnly]
    [SerializeField] private ArmorData startingArmor;

    [FoldoutGroup("Starting Equipment")]
    [SerializeField] private EquipmentSlotType startingHeldSlot = EquipmentSlotType.Primary;

    [FoldoutGroup("Panel")]
    [SerializeField] private bool hideCrosshairWhilePanelVisible = true;

    [FoldoutGroup("Panel")]
    [SerializeField] private bool pauseGameWhilePanelVisible = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EquipmentSlotType CurrentHeldSlot { get; private set; } = EquipmentSlotType.None;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EquipmentItemData CurrentHeldItem { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSwitchingEquipment => switchRoutine != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsEquipmentPanelVisible => equipmentPanelUI != null && equipmentPanelUI.IsVisible;

    public ArmorData EquippedArmorItem => startingArmor;

    public event Action EquipmentChanged;
    public event Action<EquipmentItemData, float> HeldItemEquipping;
    public event Action<EquipmentItemData, float> HeldItemHolstering;

    private Player rewiredPlayer;
    private Coroutine switchRoutine;
    private bool suppressWeaponStateSync;
    private RuntimeHandSlotState primaryRuntime;
    private RuntimeHandSlotState secondaryRuntime;
    private RuntimeHandSlotState beltRuntime;
    private float cachedTimeScaleBeforePanel = 1f;

    private void Reset()
    {
        playerWeaponController = GetComponent<PlayerWeaponController>();
        playerUtilityController = GetComponent<PlayerUtilityController>();
        playerMeleeController = PlayerMeleeController.EnsureOn(gameObject);
        armorLoadout = GetComponent<ArmorLoadout>();
    }

    private void Awake()
    {
        if (playerWeaponController == null)
            playerWeaponController = GetComponent<PlayerWeaponController>();

        if (playerUtilityController == null)
            playerUtilityController = GetComponent<PlayerUtilityController>();

        playerMeleeController = PlayerMeleeController.EnsureOn(gameObject);

        if (armorLoadout == null)
            armorLoadout = GetComponent<ArmorLoadout>();

        if (equipmentPanelUI == null)
            equipmentPanelUI = FindSceneObjectIncludingInactive<PlayerEquipmentPanelUI>();

        if (dynamicCrosshairUI == null)
            dynamicCrosshairUI = FindSceneObjectIncludingInactive<DynamicCrosshairUI>();

        InitializeRuntimeSlots();
        ResolveRewiredPlayer();
        CharacterOrbitHandsAnimator.EnsureOn(gameObject);

        if (playerWeaponController != null)
            playerWeaponController.WeaponStateChanged += HandleWeaponStateChanged;
    }

    private void Start()
    {
        ApplyPendingRuntimeLoadoutIfAvailable();

        if (armorLoadout != null)
            armorLoadout.EquipArmor(startingArmor);

        EquipStartingSlot();
        ApplyPanelPresentation(IsEquipmentPanelVisible);
        NotifyEquipmentChanged();
    }

    private void OnDestroy()
    {
        if (playerWeaponController != null)
            playerWeaponController.WeaponStateChanged -= HandleWeaponStateChanged;
    }

    private void OnValidate()
    {
        startingHeldSlot = startingHeldSlot.IsHandSlot() ? startingHeldSlot : EquipmentSlotType.Primary;
        ValidateSlotAssignment(primaryEquipment, EquipmentSlotType.Primary);
        ValidateSlotAssignment(secondaryEquipment, EquipmentSlotType.Secondary);
        ValidateSlotAssignment(beltEquipment, EquipmentSlotType.Belt);

        if (!Application.isPlaying)
            playerMeleeController = PlayerMeleeController.EnsureOn(gameObject);

        if (!Application.isPlaying)
            CharacterOrbitHandsAnimator.EnsureOn(gameObject);
    }

    private void Update()
    {
        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        if (rewiredPlayer.GetButtonDown(toggleEquipmentPanelAction))
            ToggleEquipmentPanel();

        if (IsSwitchingEquipment)
            return;

        if (rewiredPlayer.GetButtonDown(equipPrimaryAction))
            TryEquipSlot(EquipmentSlotType.Primary);
        else if (rewiredPlayer.GetButtonDown(equipSecondaryAction))
            TryEquipSlot(EquipmentSlotType.Secondary);
        else if (rewiredPlayer.GetButtonDown(equipBeltAction))
            TryEquipSlot(EquipmentSlotType.Belt);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug")]
    public void DebugEquipPrimary()
    {
        TryEquipSlot(EquipmentSlotType.Primary);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug")]
    public void DebugEquipSecondary()
    {
        TryEquipSlot(EquipmentSlotType.Secondary);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug")]
    public void DebugEquipBelt()
    {
        TryEquipSlot(EquipmentSlotType.Belt);
    }

    public EquipmentItemData GetItemInSlot(EquipmentSlotType slotType)
    {
        RuntimeHandSlotState runtimeSlot = GetRuntimeSlot(slotType);
        if (runtimeSlot != null)
            return runtimeSlot.Item;

        return ResolveDefinitionItem(slotType);
    }

    public bool IsSlotCurrentlyHeld(EquipmentSlotType slotType)
    {
        return CurrentHeldSlot == slotType;
    }

    public bool TryEquipSlot(EquipmentSlotType slotType)
    {
        if (!slotType.IsHandSlot() || IsSwitchingEquipment)
            return false;

        RuntimeHandSlotState targetSlot = GetRuntimeSlot(slotType);
        if (targetSlot == null || targetSlot.Item == null)
            return false;

        if (!CanStartEquipmentSwitch())
            return false;

        if (CurrentHeldSlot == slotType && CurrentHeldItem == targetSlot.Item)
        {
            switchRoutine = StartCoroutine(HolsterCurrentHeldItemRoutine());
            return true;
        }

        switchRoutine = StartCoroutine(EquipSlotRoutine(targetSlot));
        return true;
    }

    public void ToggleEquipmentPanel()
    {
        if (equipmentPanelUI == null)
        {
            equipmentPanelUI = FindSceneObjectIncludingInactive<PlayerEquipmentPanelUI>();
            if (equipmentPanelUI == null)
                return;
        }

        SetEquipmentPanelVisible(!equipmentPanelUI.IsVisible);
    }

    public void SetEquipmentPanelVisible(bool visible)
    {
        if (equipmentPanelUI == null)
            return;

        equipmentPanelUI.SetVisible(visible);
        ApplyPanelPresentation(visible);
    }

    public bool TryGetRuntimeFirearmState(EquipmentSlotType slotType, out int loadedAmmo, out int reserveAmmo)
    {
        RuntimeHandSlotState slotState = GetRuntimeSlot(slotType);
        if (slotState == null || slotState.Item is not FirearmData)
        {
            loadedAmmo = 0;
            reserveAmmo = 0;
            return false;
        }

        loadedAmmo = slotState.LoadedAmmo;
        reserveAmmo = slotState.ReserveAmmo;
        return true;
    }

    public bool TryGetRuntimeThrowableState(EquipmentSlotType slotType, out int remainingUses, out int maxUses)
    {
        RuntimeHandSlotState slotState = GetRuntimeSlot(slotType);
        if (slotState == null || slotState.Item is not ThrowableUtilityData throwableData)
        {
            remainingUses = 0;
            maxUses = 0;
            return false;
        }

        maxUses = ResolveInitialThrowableUses(throwableData, -1);
        remainingUses = Mathf.Clamp(slotState.ReserveAmmo, 0, maxUses);
        return true;
    }

    public bool TryMoveItemBetweenSlots(EquipmentSlotType fromSlotType, EquipmentSlotType toSlotType)
    {
        if (!fromSlotType.IsHandSlot() || !toSlotType.IsHandSlot())
            return false;

        if (IsSwitchingEquipment || fromSlotType == toSlotType)
            return fromSlotType == toSlotType;

        RuntimeHandSlotState fromSlot = GetRuntimeSlot(fromSlotType);
        RuntimeHandSlotState toSlot = GetRuntimeSlot(toSlotType);
        if (fromSlot == null || toSlot == null || fromSlot.Item == null)
            return false;

        if (!fromSlot.Item.SupportsSlot(toSlotType))
            return false;

        if (toSlot.Item != null && !toSlot.Item.SupportsSlot(fromSlotType))
            return false;

        CacheCurrentFirearmState();

        bool heldItemMovedFromSource = CurrentHeldSlot == fromSlotType;
        bool heldItemMovedFromTarget = CurrentHeldSlot == toSlotType;

        if (toSlot.Item == null)
        {
            AssignSlotState(toSlot, fromSlot.Item, fromSlot.FirearmProjectile, fromSlot.LoadedAmmo, fromSlot.ReserveAmmo);
            ClearSlotState(fromSlot);
        }
        else
        {
            EquipmentItemData targetItem = toSlot.Item;
            ProjectileData targetProjectile = toSlot.FirearmProjectile;
            int targetLoadedAmmo = toSlot.LoadedAmmo;
            int targetReserveAmmo = toSlot.ReserveAmmo;

            AssignSlotState(toSlot, fromSlot.Item, fromSlot.FirearmProjectile, fromSlot.LoadedAmmo, fromSlot.ReserveAmmo);
            AssignSlotState(fromSlot, targetItem, targetProjectile, targetLoadedAmmo, targetReserveAmmo);
        }

        if (heldItemMovedFromSource)
            CurrentHeldSlot = toSlotType;
        else if (heldItemMovedFromTarget)
            CurrentHeldSlot = fromSlotType;

        CurrentHeldItem = CurrentHeldSlot.IsHandSlot() ? GetItemInSlot(CurrentHeldSlot) : null;
        SyncCurrentFirearmStateFromController();
        NotifyEquipmentChanged();
        return true;
    }

    public PlayerEquipmentRuntimeLoadout CaptureRuntimeLoadout()
    {
        CacheCurrentFirearmState();

        PlayerEquipmentRuntimeLoadout loadout = new()
        {
            ArmorItem = startingArmor,
            HeldSlot = CurrentHeldSlot.IsHandSlot() ? CurrentHeldSlot : startingHeldSlot
        };

        AppendRuntimeSlotLoadout(loadout, primaryRuntime);
        AppendRuntimeSlotLoadout(loadout, secondaryRuntime);
        AppendRuntimeSlotLoadout(loadout, beltRuntime);
        return loadout;
    }

    public bool ConsumeCurrentHeldUtility()
    {
        if (!CurrentHeldSlot.IsHandSlot() || CurrentHeldItem is not UtilityItemData utilityItem)
            return false;

        RuntimeHandSlotState slotState = GetRuntimeSlot(CurrentHeldSlot);
        if (slotState == null || slotState.Item != utilityItem)
            return false;

        if (utilityItem is ThrowableUtilityData throwableData)
        {
            int remainingUses = Mathf.Clamp(slotState.ReserveAmmo - 1, 0, throwableData.MaxUses);
            slotState.ReserveAmmo = remainingUses;
            if (remainingUses > 0)
            {
                NotifyEquipmentChanged();
                return true;
            }
        }

        playerUtilityController?.ClearEquippedUtilityFromConsumption(utilityItem);
        ClearSlotState(slotState);
        CurrentHeldSlot = EquipmentSlotType.None;
        CurrentHeldItem = null;
        NotifyEquipmentChanged();
        return true;
    }

    private void EquipStartingSlot()
    {
        RuntimeHandSlotState preferredSlot = GetRuntimeSlot(startingHeldSlot);
        if (preferredSlot != null && preferredSlot.Item != null)
        {
            ForceEquipSlotImmediately(preferredSlot);
            return;
        }

        RuntimeHandSlotState fallbackSlot = primaryRuntime?.Item != null ? primaryRuntime :
            secondaryRuntime?.Item != null ? secondaryRuntime :
            beltRuntime?.Item != null ? beltRuntime :
            null;

        if (fallbackSlot != null)
            ForceEquipSlotImmediately(fallbackSlot);
    }

    private void ForceEquipSlotImmediately(RuntimeHandSlotState slotState)
    {
        if (slotState == null || slotState.Item == null)
            return;

        suppressWeaponStateSync = true;
        bool equipped = false;

        if (slotState.Item is FirearmData firearmData)
        {
            if (playerWeaponController != null)
            {
                playerWeaponController.EquipWeapon(firearmData, slotState.FirearmProjectile, slotState.LoadedAmmo, slotState.ReserveAmmo);
                equipped = true;
            }
        }
        else if (slotState.Item is UtilityItemData utilityItemData)
        {
            if (playerUtilityController != null)
            {
                playerUtilityController.EquipUtility(utilityItemData);
                equipped = true;
            }
        }
        else if (slotState.Item is MeleeWeaponData meleeWeaponData)
        {
            if (playerMeleeController != null)
            {
                playerMeleeController.EquipWeapon(meleeWeaponData);
                equipped = true;
            }
        }

        CurrentHeldSlot = equipped ? slotState.SlotType : EquipmentSlotType.None;
        CurrentHeldItem = equipped ? slotState.Item : null;
        suppressWeaponStateSync = false;
    }

    private IEnumerator EquipSlotRoutine(RuntimeHandSlotState targetSlot)
    {
        suppressWeaponStateSync = true;
        CacheCurrentFirearmState();
        NotifyHeldItemHolstering(CurrentHeldItem, ResolveItemHolsterTime(CurrentHeldItem));
        yield return HolsterActiveControllersRoutine();

        CurrentHeldSlot = EquipmentSlotType.None;
        CurrentHeldItem = null;

        bool equipped = false;
        NotifyHeldItemEquipping(targetSlot.Item, ResolveItemEquipTime(targetSlot.Item));

        if (targetSlot.Item is FirearmData firearmData)
        {
            if (playerWeaponController != null)
            {
                playerWeaponController.EquipWeapon(firearmData, targetSlot.FirearmProjectile, targetSlot.LoadedAmmo, targetSlot.ReserveAmmo);
                while (playerWeaponController != null && (playerWeaponController.IsBusy || playerWeaponController.EquippedFirearm != firearmData))
                    yield return null;

                equipped = true;
            }
        }
        else if (targetSlot.Item is UtilityItemData utilityItemData)
        {
            if (playerUtilityController != null)
            {
                playerUtilityController.EquipUtility(utilityItemData);
                while (playerUtilityController != null && (playerUtilityController.IsBusy || playerUtilityController.EquippedUtility != utilityItemData))
                    yield return null;

                equipped = true;
            }
        }
        else if (targetSlot.Item is MeleeWeaponData meleeWeaponData)
        {
            if (playerMeleeController != null)
            {
                playerMeleeController.EquipWeapon(meleeWeaponData);
                while (playerMeleeController != null && (playerMeleeController.IsBusy || playerMeleeController.EquippedMeleeWeapon != meleeWeaponData))
                    yield return null;

                equipped = true;
            }
        }

        CurrentHeldSlot = equipped ? targetSlot.SlotType : EquipmentSlotType.None;
        CurrentHeldItem = equipped ? targetSlot.Item : null;
        suppressWeaponStateSync = false;
        SyncCurrentFirearmStateFromController();
        NotifyEquipmentChanged();
        switchRoutine = null;
    }

    private IEnumerator HolsterCurrentHeldItemRoutine()
    {
        suppressWeaponStateSync = true;
        CacheCurrentFirearmState();
        NotifyHeldItemHolstering(CurrentHeldItem, ResolveItemHolsterTime(CurrentHeldItem));
        yield return HolsterActiveControllersRoutine();

        CurrentHeldSlot = EquipmentSlotType.None;
        CurrentHeldItem = null;
        suppressWeaponStateSync = false;
        NotifyEquipmentChanged();
        switchRoutine = null;
    }

    private IEnumerator HolsterActiveControllersRoutine()
    {
        if (playerWeaponController != null && playerWeaponController.enabled && playerWeaponController.EquippedFirearm != null)
        {
            playerWeaponController.HolsterWeapon();
            while (playerWeaponController != null && (playerWeaponController.IsBusy || playerWeaponController.EquippedFirearm != null))
                yield return null;
        }

        if (playerUtilityController != null && playerUtilityController.enabled && playerUtilityController.EquippedUtility != null)
        {
            playerUtilityController.HolsterCurrentUtility();
            while (playerUtilityController != null && (playerUtilityController.IsBusy || playerUtilityController.EquippedUtility != null))
                yield return null;
        }

        if (playerMeleeController != null && playerMeleeController.enabled && playerMeleeController.EquippedMeleeWeapon != null)
        {
            playerMeleeController.HolsterWeapon();
            while (playerMeleeController != null && (playerMeleeController.IsBusy || playerMeleeController.EquippedMeleeWeapon != null))
                yield return null;
        }
    }

    private bool CanStartEquipmentSwitch()
    {
        if (playerWeaponController != null)
        {
            if (playerWeaponController.IsBusy)
                return false;

            if (playerWeaponController.IsReloading &&
                playerWeaponController.EquippedFirearm != null &&
                playerWeaponController.EquippedFirearm.ReloadStyle == ReloadType.Magazine)
            {
                return false;
            }
        }

        if (playerUtilityController != null && playerUtilityController.IsBusy)
            return false;

        if (playerMeleeController != null && playerMeleeController.IsBusy)
            return false;

        return true;
    }

    private void InitializeRuntimeSlots()
    {
        primaryRuntime = CreateRuntimeSlot(EquipmentSlotType.Primary, primaryEquipment);
        secondaryRuntime = CreateRuntimeSlot(EquipmentSlotType.Secondary, secondaryEquipment);
        beltRuntime = CreateRuntimeSlot(EquipmentSlotType.Belt, beltEquipment);
    }

    private RuntimeHandSlotState CreateRuntimeSlot(EquipmentSlotType slotType, HandEquipmentSlotDefinition definition)
    {
        if (definition == null)
            return null;

        EquipmentItemData item = definition.item;
        if (item != null && !item.SupportsSlot(slotType))
        {
            Debug.LogWarning($"{name} has {item.name} assigned to {slotType}, but that item does not support that slot.", this);
            item = null;
        }

        int loadedAmmo = 0;
        int reserveAmmo = 0;
        ProjectileData projectile = null;

        if (item is FirearmData firearmData)
        {
            projectile = firearmData.SupportsProjectile(definition.firearmProjectile)
                ? definition.firearmProjectile
                : firearmData.CompatibleProjectiles.Count > 0 ? firearmData.CompatibleProjectiles[0] : null;

            loadedAmmo = ResolveInitialLoadedAmmo(firearmData, definition.startingLoadedAmmo);
            reserveAmmo = ResolveInitialReserveAmmo(firearmData, definition.startingReserveAmmo);
        }
        else if (item is ThrowableUtilityData throwableData)
        {
            reserveAmmo = ResolveInitialThrowableUses(throwableData, -1);
        }

        return new RuntimeHandSlotState
        {
            SlotType = slotType,
            Item = item,
            FirearmProjectile = projectile,
            LoadedAmmo = loadedAmmo,
            ReserveAmmo = reserveAmmo
        };
    }

    private void CacheCurrentFirearmState()
    {
        if (CurrentHeldSlot == EquipmentSlotType.None || playerWeaponController == null || playerWeaponController.EquippedFirearm == null)
            return;

        RuntimeHandSlotState slotState = GetRuntimeSlot(CurrentHeldSlot);
        if (slotState == null)
            return;

        slotState.LoadedAmmo = playerWeaponController.CurrentLoadedAmmo;
        slotState.ReserveAmmo = playerWeaponController.CurrentReserveAmmo;
        slotState.FirearmProjectile = playerWeaponController.CurrentProjectile;
    }

    private void SyncCurrentFirearmStateFromController()
    {
        if (CurrentHeldSlot == EquipmentSlotType.None || playerWeaponController == null || playerWeaponController.EquippedFirearm == null)
            return;

        RuntimeHandSlotState slotState = GetRuntimeSlot(CurrentHeldSlot);
        if (slotState == null || slotState.Item is not FirearmData)
            return;

        slotState.LoadedAmmo = playerWeaponController.CurrentLoadedAmmo;
        slotState.ReserveAmmo = playerWeaponController.CurrentReserveAmmo;
        slotState.FirearmProjectile = playerWeaponController.CurrentProjectile;
    }

    private void ApplyPendingRuntimeLoadoutIfAvailable()
    {
        if (!PlayerEquipmentRuntimeSession.TryConsumePendingQuestLoadout(out PlayerEquipmentRuntimeLoadout loadout) || loadout == null)
            return;

        ApplyRuntimeLoadout(loadout);
    }

    private void ApplyRuntimeLoadout(PlayerEquipmentRuntimeLoadout loadout)
    {
        if (loadout == null)
            return;

        startingArmor = loadout.ArmorItem;
        ApplyRuntimeSlotLoadout(primaryRuntime, loadout.GetSlot(EquipmentSlotType.Primary));
        ApplyRuntimeSlotLoadout(secondaryRuntime, loadout.GetSlot(EquipmentSlotType.Secondary));
        ApplyRuntimeSlotLoadout(beltRuntime, loadout.GetSlot(EquipmentSlotType.Belt));

        EquipmentSlotType requestedHeldSlot = loadout.HeldSlot.IsHandSlot() ? loadout.HeldSlot : EquipmentSlotType.None;
        startingHeldSlot = GetItemInSlot(requestedHeldSlot) != null
            ? requestedHeldSlot
            : ResolveFirstPopulatedHandSlot();
    }

    private void HandleWeaponStateChanged()
    {
        if (suppressWeaponStateSync)
            return;

        SyncCurrentFirearmStateFromController();
    }

    private RuntimeHandSlotState GetRuntimeSlot(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Primary => primaryRuntime,
            EquipmentSlotType.Secondary => secondaryRuntime,
            EquipmentSlotType.Belt => beltRuntime,
            _ => null
        };
    }

    private static void AssignSlotState(RuntimeHandSlotState slotState, EquipmentItemData item, ProjectileData firearmProjectile, int loadedAmmo, int reserveAmmo)
    {
        if (slotState == null)
            return;

        slotState.Item = item;
        slotState.FirearmProjectile = firearmProjectile;
        slotState.LoadedAmmo = loadedAmmo;
        slotState.ReserveAmmo = reserveAmmo;
    }

    private static void ClearSlotState(RuntimeHandSlotState slotState)
    {
        AssignSlotState(slotState, null, null, 0, 0);
    }

    private static void AppendRuntimeSlotLoadout(PlayerEquipmentRuntimeLoadout loadout, RuntimeHandSlotState slotState)
    {
        if (loadout == null || slotState == null || slotState.Item == null)
            return;

        loadout.SetSlot(slotState.SlotType, slotState.Item, slotState.FirearmProjectile, slotState.LoadedAmmo, slotState.ReserveAmmo);
    }

    private static void ApplyRuntimeSlotLoadout(RuntimeHandSlotState slotState, RuntimeEquipmentSlotLoadout slotLoadout)
    {
        if (slotState == null)
            return;

        if (slotLoadout == null || slotLoadout.Item == null || !slotLoadout.Item.SupportsSlot(slotState.SlotType))
        {
            ClearSlotState(slotState);
            return;
        }

        EquipmentItemData item = slotLoadout.Item;
        ProjectileData projectile = null;
        int loadedAmmo = 0;
        int reserveAmmo = 0;

        if (item is FirearmData firearmData)
        {
            projectile = firearmData.SupportsProjectile(slotLoadout.FirearmProjectile)
                ? slotLoadout.FirearmProjectile
                : firearmData.CompatibleProjectiles.Count > 0 ? firearmData.CompatibleProjectiles[0] : null;

            loadedAmmo = ResolveInitialLoadedAmmo(firearmData, slotLoadout.LoadedAmmo);
            reserveAmmo = ResolveInitialReserveAmmo(firearmData, slotLoadout.ReserveAmmo);
        }
        else if (item is ThrowableUtilityData throwableData)
        {
            reserveAmmo = ResolveInitialThrowableUses(throwableData, slotLoadout.ReserveAmmo);
        }

        AssignSlotState(slotState, item, projectile, loadedAmmo, reserveAmmo);
    }

    private static int ResolveInitialLoadedAmmo(FirearmData firearmData, int requestedLoadedAmmo)
    {
        int ammoCapacity = firearmData != null ? firearmData.AmmoCapacity : 0;
        int resolvedLoadedAmmo = requestedLoadedAmmo < 0 ? ammoCapacity : requestedLoadedAmmo;
        return Mathf.Clamp(resolvedLoadedAmmo, 0, ammoCapacity);
    }

    private static int ResolveInitialReserveAmmo(FirearmData firearmData, int requestedReserveAmmo)
    {
        int defaultReserveAmmo = firearmData != null ? firearmData.DefaultReserveAmmo : 0;
        int resolvedReserveAmmo = requestedReserveAmmo < 0 ? defaultReserveAmmo : requestedReserveAmmo;
        return Mathf.Max(0, resolvedReserveAmmo);
    }

    private static int ResolveInitialThrowableUses(ThrowableUtilityData throwableData, int requestedUses)
    {
        int maxUses = throwableData != null ? throwableData.MaxUses : 0;
        int resolvedUses = requestedUses < 0 ? maxUses : requestedUses;
        return Mathf.Clamp(resolvedUses, 0, maxUses);
    }

    private void ValidateSlotAssignment(HandEquipmentSlotDefinition definition, EquipmentSlotType slotType)
    {
        if (definition == null || definition.item == null)
            return;

        if (!definition.item.SupportsSlot(slotType))
            definition.item = null;
    }

    private void NotifyEquipmentChanged()
    {
        EquipmentChanged?.Invoke();
    }

    private EquipmentItemData ResolveDefinitionItem(EquipmentSlotType slotType)
    {
        HandEquipmentSlotDefinition definition = slotType switch
        {
            EquipmentSlotType.Primary => primaryEquipment,
            EquipmentSlotType.Secondary => secondaryEquipment,
            EquipmentSlotType.Belt => beltEquipment,
            _ => null
        };

        return definition != null && definition.item != null && definition.item.SupportsSlot(slotType)
            ? definition.item
            : null;
    }

    private EquipmentSlotType ResolveFirstPopulatedHandSlot()
    {
        if (primaryRuntime != null && primaryRuntime.Item != null)
            return EquipmentSlotType.Primary;

        if (secondaryRuntime != null && secondaryRuntime.Item != null)
            return EquipmentSlotType.Secondary;

        if (beltRuntime != null && beltRuntime.Item != null)
            return EquipmentSlotType.Belt;

        return EquipmentSlotType.None;
    }

    private void ApplyPanelPresentation(bool panelVisible)
    {
        if (dynamicCrosshairUI == null)
            dynamicCrosshairUI = FindSceneObjectIncludingInactive<DynamicCrosshairUI>();

        if (dynamicCrosshairUI != null)
            dynamicCrosshairUI.SetUiSuppressed(panelVisible && hideCrosshairWhilePanelVisible);

        if (playerWeaponController != null)
            playerWeaponController.SetInputBlocked(panelVisible);

        if (playerUtilityController != null)
            playerUtilityController.SetInputBlocked(panelVisible);

        if (playerMeleeController != null)
            playerMeleeController.SetInputBlocked(panelVisible);

        if (!pauseGameWhilePanelVisible)
            return;

        if (panelVisible)
        {
            cachedTimeScaleBeforePanel = Time.timeScale > 0f ? Time.timeScale : cachedTimeScaleBeforePanel;
            Time.timeScale = 0f;
            return;
        }

        Time.timeScale = Mathf.Approximately(cachedTimeScaleBeforePanel, 0f) ? 1f : cachedTimeScaleBeforePanel;
    }

    private static T FindSceneObjectIncludingInactive<T>() where T : UnityEngine.Object
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate is not Component component)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }

    private float ResolveItemEquipTime(EquipmentItemData item)
    {
        return item switch
        {
            FirearmData firearmData => firearmData.EquipTime,
            MeleeWeaponData meleeWeaponData => meleeWeaponData.EquipTime,
            UtilityItemData utilityItemData => utilityItemData.EquipTime,
            _ => 0f
        };
    }

    private float ResolveItemHolsterTime(EquipmentItemData item)
    {
        return item switch
        {
            FirearmData firearmData => firearmData.HolsterTime,
            MeleeWeaponData meleeWeaponData => meleeWeaponData.HolsterTime,
            UtilityItemData utilityItemData => utilityItemData.HolsterTime,
            _ => 0f
        };
    }

    private void NotifyHeldItemEquipping(EquipmentItemData item, float duration)
    {
        if (item == null)
            return;

        HeldItemEquipping?.Invoke(item, Mathf.Max(0f, duration));
    }

    private void NotifyHeldItemHolstering(EquipmentItemData item, float duration)
    {
        if (item == null)
            return;

        HeldItemHolstering?.Invoke(item, Mathf.Max(0f, duration));
    }
}
}
