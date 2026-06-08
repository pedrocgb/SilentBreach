using System;
using System.Collections;
using Breezeblocks.Input;
using Breezeblocks.HideoutSystem;
using Breezeblocks.Missions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Equipment/Player Equipment Controller")]
[RequireComponent(typeof(PlayerMeleeController))]
public class PlayerEquipmentController : MonoBehaviour
{
    private static readonly EquipmentSlotType[] ConsoleSlotPreferenceOrder =
    {
        EquipmentSlotType.Primary,
        EquipmentSlotType.Secondary,
        EquipmentSlotType.Belt
    };

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

    [FoldoutGroup("Rewired")]
    [SerializeField] private string aimAction = "Aim";

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerPickupInteractor playerPickupInteractor;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerFocusController playerFocusController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ArmorLoadout armorLoadout;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentPanelUI equipmentPanelUI;

    [FoldoutGroup("References")]
    [SerializeField] private DynamicCrosshairUI dynamicCrosshairUI;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerAimCamera2D aimCamera;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorStaggerController actorStaggerController;

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

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsUnarmedAiming { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentUnarmedAimPanDistance => IsUnarmedAiming ? unarmedAimPanDistance : 0f;

    public ArmorData EquippedArmorItem => startingArmor;
    public int RewiredPlayerId => rewiredPlayerId;

    public event Action EquipmentChanged;
    public event Action<EquipmentItemData, float> HeldItemEquipping;
    public event Action<EquipmentItemData, float> HeldItemHolstering;

    private IPlayerInputReader inputReader;
    private Coroutine switchRoutine;
    private bool suppressWeaponStateSync;
    private RuntimeHandSlotState primaryRuntime;
    private RuntimeHandSlotState secondaryRuntime;
    private RuntimeHandSlotState beltRuntime;
    private float cachedTimeScaleBeforePanel = 1f;
    private bool inputBlocked;
    private float unarmedAimRotationSpeed = 720f;
    private float unarmedAimPanDistance;
    private PlayerPerkEffectController playerPerkEffectController;

    // Executes the Reset routine.
    private void Reset()
    {
        playerWeaponController = GetComponent<PlayerWeaponController>();
        playerUtilityController = GetComponent<PlayerUtilityController>();
        playerMeleeController = GetComponent<PlayerMeleeController>();
        playerPickupInteractor = GetComponent<PlayerPickupInteractor>();
        playerFocusController = GetComponent<PlayerFocusController>();
        armorLoadout = GetComponent<ArmorLoadout>();
        playerVisionLight = GetComponentInChildren<PlayerVisionLight>();
        if (Camera.main != null)
            aimCamera = Camera.main.GetComponent<PlayerAimCamera2D>();
        actorStaggerController = GetComponent<ActorStaggerController>();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        if (playerWeaponController == null)
            playerWeaponController = GetComponent<PlayerWeaponController>();

        if (playerUtilityController == null)
            playerUtilityController = GetComponent<PlayerUtilityController>();

        if (playerMeleeController == null)
            playerMeleeController = GetComponent<PlayerMeleeController>();

        if (armorLoadout == null)
            armorLoadout = GetComponent<ArmorLoadout>();

        if (playerPickupInteractor == null)
            playerPickupInteractor = GetComponent<PlayerPickupInteractor>();

        if (playerFocusController == null)
            playerFocusController = GetComponent<PlayerFocusController>();

        if (equipmentPanelUI == null)
            equipmentPanelUI = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerEquipmentPanelUI>(gameObject);

        if (dynamicCrosshairUI == null)
            dynamicCrosshairUI = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<DynamicCrosshairUI>(gameObject);

        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>();

        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main.GetComponent<PlayerAimCamera2D>();

        if (aimCamera == null)
            aimCamera = PlayerSceneReferenceUtility.FindPlayerAimCamera(gameObject);

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        playerPerkEffectController = PlayerPerkEffectController.EnsureOn(gameObject);
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
        InitializeRuntimeSlots();

        if (playerWeaponController != null)
            playerWeaponController.WeaponStateChanged += HandleWeaponStateChanged;
    }

    // Executes the Start routine.
    private void Start()
    {
        ApplyPendingRuntimeLoadoutIfAvailable();

        if (armorLoadout != null)
            armorLoadout.EquipArmor(startingArmor);

        EquipStartingSlot();
        ApplyPanelPresentation(IsEquipmentPanelVisible);
        NotifyEquipmentChanged();
        playerPerkEffectController?.ApplyRuntimePerks();
    }

    // Executes the OnDestroy routine.
    private void OnDestroy()
    {
        if (playerWeaponController != null)
            playerWeaponController.WeaponStateChanged -= HandleWeaponStateChanged;
    }

    // Executes the OnValidate routine.
    private void OnValidate()
    {
        startingHeldSlot = startingHeldSlot.IsHandSlot() ? startingHeldSlot : EquipmentSlotType.Primary;
        ValidateSlotAssignment(primaryEquipment, EquipmentSlotType.Primary);
        ValidateSlotAssignment(secondaryEquipment, EquipmentSlotType.Secondary);
        ValidateSlotAssignment(beltEquipment, EquipmentSlotType.Belt);

        if (!Application.isPlaying)
            playerMeleeController = GetComponent<PlayerMeleeController>();
    }

    // Executes the Update routine.
    private void Update()
    {
        if (inputReader == null)
            inputReader = new RewiredPlayerInputReader(rewiredPlayerId);

        if (!inputReader.IsReady)
            return;

        if (inputBlocked)
        {
            SetUnarmedAimState(false);
            return;
        }

        UpdateUnarmedAimState();

        if (inputReader.GetButtonDown(toggleEquipmentPanelAction))
            ToggleEquipmentPanel();

        if (IsSwitchingEquipment)
            return;

        if (inputReader.GetButtonDown(equipPrimaryAction))
            TryEquipSlot(EquipmentSlotType.Primary);
        else if (inputReader.GetButtonDown(equipSecondaryAction))
            TryEquipSlot(EquipmentSlotType.Secondary);
        else if (inputReader.GetButtonDown(equipBeltAction))
            TryEquipSlot(EquipmentSlotType.Belt);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug")]
    // Executes the DebugEquipPrimary routine.
    public void DebugEquipPrimary()
    {
        TryEquipSlot(EquipmentSlotType.Primary);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug")]
    // Executes the DebugEquipSecondary routine.
    public void DebugEquipSecondary()
    {
        TryEquipSlot(EquipmentSlotType.Secondary);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug")]
    // Executes the DebugEquipBelt routine.
    public void DebugEquipBelt()
    {
        TryEquipSlot(EquipmentSlotType.Belt);
    }

    // Executes the GetItemInSlot routine.
    public EquipmentItemData GetItemInSlot(EquipmentSlotType slotType)
    {
        RuntimeHandSlotState runtimeSlot = GetRuntimeSlot(slotType);
        if (runtimeSlot != null)
            return runtimeSlot.Item;

        return ResolveDefinitionItem(slotType);
    }

    // Executes the IsSlotCurrentlyHeld routine.
    public bool IsSlotCurrentlyHeld(EquipmentSlotType slotType)
    {
        return CurrentHeldSlot == slotType;
    }

    // Executes the TryEquipSlot routine.
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

    // Executes the ToggleEquipmentPanel routine.
    public void ToggleEquipmentPanel()
    {
        if (equipmentPanelUI == null)
        {
            equipmentPanelUI = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerEquipmentPanelUI>(gameObject);
            if (equipmentPanelUI == null)
                return;
        }

        SetEquipmentPanelVisible(!equipmentPanelUI.IsVisible);
    }

    // Executes the SetEquipmentPanelVisible routine.
    public void SetEquipmentPanelVisible(bool visible)
    {
        if (equipmentPanelUI == null)
            return;

        bool resolvedVisible = visible && !inputBlocked;
        equipmentPanelUI.SetVisible(resolvedVisible);
        ApplyPanelPresentation(resolvedVisible);
    }

    // Executes the SetInputBlocked routine.
    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        if (!blocked)
            return;

        SetUnarmedAimState(false);

        if (equipmentPanelUI != null && equipmentPanelUI.IsVisible)
            equipmentPanelUI.SetVisible(false);

        ApplyPanelPresentation(false);
    }

    // Executes the TryGetRuntimeFirearmState routine.
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

    // Executes the ApplyUnarmedAimSettings routine.
    public void ApplyUnarmedAimSettings(float rotationSpeed, float panDistance)
    {
        unarmedAimRotationSpeed = Mathf.Max(0f, rotationSpeed);
        unarmedAimPanDistance = Mathf.Max(0f, panDistance);
        UpdateUnarmedAimCameraState();
    }

    // Executes the TryGetRuntimeFirearmProjectile routine.
    public bool TryGetRuntimeFirearmProjectile(EquipmentSlotType slotType, out ProjectileData projectile)
    {
        RuntimeHandSlotState slotState = GetRuntimeSlot(slotType);
        if (slotState == null || slotState.Item is not FirearmData)
        {
            projectile = null;
            return false;
        }

        projectile = slotState.FirearmProjectile;
        return projectile != null;
    }

    // Executes the TryGetRuntimeThrowableState routine.
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

    // Executes the TryMoveItemBetweenSlots routine.
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

    // Executes the CaptureRuntimeLoadout routine.
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

    // Executes the ConsumeCurrentHeldUtility routine.
    public bool ConsumeCurrentHeldUtility()
    {
        if (!CurrentHeldSlot.IsHandSlot() || CurrentHeldItem is not UtilityItemData utilityItem)
            return false;

        RuntimeHandSlotState slotState = GetRuntimeSlot(CurrentHeldSlot);
        if (slotState == null || slotState.Item != utilityItem)
            return false;

        if (utilityItem is ThrowableUtilityData throwableData)
        {
            if (GameplayConsoleCheatState.InfiniteReserveAmmo)
            {
                slotState.ReserveAmmo = Mathf.Max(slotState.ReserveAmmo, ResolveInitialThrowableUses(throwableData, -1));
                NotifyEquipmentChanged();
                return true;
            }

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

    // Executes the ForceStoreEquipmentFromConsole routine.
    public void ForceStoreEquipmentFromConsole(EquipmentItemData item, Action<bool, string> onCompleted = null)
    {
        if (item == null)
        {
            onCompleted?.Invoke(false, "No equipment asset was provided.");
            return;
        }

        StartCoroutine(ForceStoreEquipmentFromConsoleRoutine(item, onCompleted));
    }

    // Executes the EquipStartingSlot routine.
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

    // Executes the ForceEquipSlotImmediately routine.
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

    // Executes the EquipSlotRoutine routine.
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

    // Executes the HolsterCurrentHeldItemRoutine routine.
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

    // Executes the HolsterActiveControllersRoutine routine.
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

    // Executes the CanStartEquipmentSwitch routine.
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

    // Executes the InitializeRuntimeSlots routine.
    private void InitializeRuntimeSlots()
    {
        primaryRuntime = CreateRuntimeSlot(EquipmentSlotType.Primary, primaryEquipment);
        secondaryRuntime = CreateRuntimeSlot(EquipmentSlotType.Secondary, secondaryEquipment);
        beltRuntime = CreateRuntimeSlot(EquipmentSlotType.Belt, beltEquipment);
    }

    // Executes the CreateRuntimeSlot routine.
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

    // Executes the CacheCurrentFirearmState routine.
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

    // Executes the SyncCurrentFirearmStateFromController routine.
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

    // Executes the ApplyPendingRuntimeLoadoutIfAvailable routine.
    private void ApplyPendingRuntimeLoadoutIfAvailable()
    {
        if (!PlayerEquipmentRuntimeSession.TryConsumePendingQuestLoadout(out PlayerEquipmentRuntimeLoadout loadout) || loadout == null)
            return;

        ApplyRuntimeLoadout(loadout);
    }

    // Executes the ApplyRuntimeLoadout routine.
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

    // Executes the HandleWeaponStateChanged routine.
    private void HandleWeaponStateChanged()
    {
        if (suppressWeaponStateSync)
            return;

        SyncCurrentFirearmStateFromController();
    }

    // Executes the ForceStoreEquipmentFromConsoleRoutine routine.
    private IEnumerator ForceStoreEquipmentFromConsoleRoutine(EquipmentItemData item, Action<bool, string> onCompleted)
    {
        while (IsSwitchingEquipment || !CanStartEquipmentSwitch())
            yield return null;

        if (item is ArmorData armorData)
        {
            startingArmor = armorData;
            armorLoadout?.EquipArmor(armorData);
            NotifyEquipmentChanged();
            onCompleted?.Invoke(true, $"{armorData.DisplayName} equipped as armor.");
            yield break;
        }

        if (!TryResolveConsoleTargetSlot(item, out RuntimeHandSlotState targetSlot))
        {
            onCompleted?.Invoke(false, $"{item.DisplayName} does not support a valid player equipment slot.");
            yield break;
        }

        suppressWeaponStateSync = true;
        CacheCurrentFirearmState();

        bool targetSlotWasHeld = CurrentHeldSlot == targetSlot.SlotType && CurrentHeldItem != null;
        if (targetSlotWasHeld)
        {
            NotifyHeldItemHolstering(CurrentHeldItem, ResolveItemHolsterTime(CurrentHeldItem));
            yield return HolsterActiveControllersRoutine();
            CurrentHeldSlot = EquipmentSlotType.None;
            CurrentHeldItem = null;
        }

        if (!TryResolveConsoleStoredState(item, out ProjectileData projectile, out int loadedAmmo, out int reserveAmmo, out string failureReason))
        {
            suppressWeaponStateSync = false;
            onCompleted?.Invoke(false, failureReason);
            yield break;
        }

        AssignSlotState(targetSlot, item, projectile, loadedAmmo, reserveAmmo);
        suppressWeaponStateSync = false;
        NotifyEquipmentChanged();
        onCompleted?.Invoke(true, $"{item.DisplayName} stored in {targetSlot.SlotType}.");
    }

    // Executes the GetRuntimeSlot routine.
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

    // Executes the AssignSlotState routine.
    private static void AssignSlotState(RuntimeHandSlotState slotState, EquipmentItemData item, ProjectileData firearmProjectile, int loadedAmmo, int reserveAmmo)
    {
        if (slotState == null)
            return;

        slotState.Item = item;
        slotState.FirearmProjectile = firearmProjectile;
        slotState.LoadedAmmo = loadedAmmo;
        slotState.ReserveAmmo = reserveAmmo;
    }

    // Executes the ClearSlotState routine.
    private static void ClearSlotState(RuntimeHandSlotState slotState)
    {
        AssignSlotState(slotState, null, null, 0, 0);
    }

    // Executes the AppendRuntimeSlotLoadout routine.
    private static void AppendRuntimeSlotLoadout(PlayerEquipmentRuntimeLoadout loadout, RuntimeHandSlotState slotState)
    {
        if (loadout == null || slotState == null || slotState.Item == null)
            return;

        loadout.SetSlot(slotState.SlotType, slotState.Item, slotState.FirearmProjectile, slotState.LoadedAmmo, slotState.ReserveAmmo);
    }

    // Executes the ApplyRuntimeSlotLoadout routine.
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

    // Executes the ResolveInitialLoadedAmmo routine.
    private static int ResolveInitialLoadedAmmo(FirearmData firearmData, int requestedLoadedAmmo)
    {
        int ammoCapacity = firearmData != null ? firearmData.AmmoCapacity : 0;
        int resolvedLoadedAmmo = requestedLoadedAmmo < 0 ? ammoCapacity : requestedLoadedAmmo;
        return Mathf.Clamp(resolvedLoadedAmmo, 0, ammoCapacity);
    }

    // Executes the ResolveInitialReserveAmmo routine.
    private static int ResolveInitialReserveAmmo(FirearmData firearmData, int requestedReserveAmmo)
    {
        int defaultReserveAmmo = firearmData != null ? firearmData.DefaultReserveAmmo : 0;
        int resolvedReserveAmmo = requestedReserveAmmo < 0 ? defaultReserveAmmo : requestedReserveAmmo;
        if (GameplayConsoleCheatState.InfiniteReserveAmmo && firearmData != null)
            resolvedReserveAmmo = Mathf.Max(resolvedReserveAmmo, Mathf.Max(1, firearmData.AmmoCapacity));

        return Mathf.Max(0, resolvedReserveAmmo);
    }

    // Executes the ResolveInitialThrowableUses routine.
    private static int ResolveInitialThrowableUses(ThrowableUtilityData throwableData, int requestedUses)
    {
        int maxUses = throwableData != null ? throwableData.MaxUses : 0;
        int resolvedUses = requestedUses < 0 ? maxUses : requestedUses;
        return Mathf.Clamp(resolvedUses, 0, maxUses);
    }

    // Executes the ValidateSlotAssignment routine.
    private void ValidateSlotAssignment(HandEquipmentSlotDefinition definition, EquipmentSlotType slotType)
    {
        if (definition == null || definition.item == null)
            return;

        if (!definition.item.SupportsSlot(slotType))
            definition.item = null;
    }

    // Executes the NotifyEquipmentChanged routine.
    private void NotifyEquipmentChanged()
    {
        EquipmentChanged?.Invoke();
    }

    // Executes the UpdateUnarmedAimState routine.
    private void UpdateUnarmedAimState()
    {
        if (CurrentHeldItem != null)
        {
            if (IsUnarmedAiming)
                SetUnarmedAimState(false);

            return;
        }

        bool canUnarmedAim =
            !IsSwitchingEquipment &&
            !IsEquipmentPanelVisible &&
            inputReader != null &&
            inputReader.GetButton(aimAction);

        SetUnarmedAimState(canUnarmedAim);

        if (playerVisionLight == null)
            return;

        float lookSpeed = IsUnarmedAiming ? unarmedAimRotationSpeed : playerVisionLight.RotationSmoothing;
        if (actorStaggerController != null)
            lookSpeed *= actorStaggerController.TurnSpeedMultiplier;

        playerVisionLight.DriveMouseLook(lookSpeed, Time.deltaTime);
    }

    // Executes the SetUnarmedAimState routine.
    private void SetUnarmedAimState(bool aiming)
    {
        if (IsUnarmedAiming == aiming)
        {
            UpdateUnarmedAimCameraState();
            return;
        }

        IsUnarmedAiming = aiming;
        UpdateUnarmedAimCameraState();
        NotifyEquipmentChanged();
    }

    // Executes the UpdateUnarmedAimCameraState routine.
    private void UpdateUnarmedAimCameraState()
    {
        if (aimCamera == null)
            return;

        aimCamera.SetFollowTarget(transform);
        aimCamera.SetAimState(IsUnarmedAiming, IsUnarmedAiming ? unarmedAimPanDistance : 0f);
    }

    // Executes the ResolveDefinitionItem routine.
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

    // Executes the ResolveFirstPopulatedHandSlot routine.
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

    // Executes the ApplyPanelPresentation routine.
    private void ApplyPanelPresentation(bool panelVisible)
    {
        bool shouldBlockHandInputs = panelVisible || inputBlocked;

        if (dynamicCrosshairUI == null)
            dynamicCrosshairUI = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<DynamicCrosshairUI>(gameObject);

        if (dynamicCrosshairUI != null)
            dynamicCrosshairUI.SetUiSuppressed(panelVisible && hideCrosshairWhilePanelVisible);

        if (playerWeaponController != null)
            playerWeaponController.SetInputBlocked(shouldBlockHandInputs);

        if (playerUtilityController != null)
            playerUtilityController.SetInputBlocked(shouldBlockHandInputs);

        if (playerMeleeController != null)
            playerMeleeController.SetInputBlocked(shouldBlockHandInputs);

        if (playerPickupInteractor != null)
            playerPickupInteractor.SetInputBlocked(shouldBlockHandInputs);

        if (playerFocusController != null)
            playerFocusController.SetInputBlocked(shouldBlockHandInputs);

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

    // Executes the ResolveItemEquipTime routine.
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

    // Executes the ResolveItemHolsterTime routine.
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

    // Executes the NotifyHeldItemEquipping routine.
    private void NotifyHeldItemEquipping(EquipmentItemData item, float duration)
    {
        if (item == null)
            return;

        HeldItemEquipping?.Invoke(item, Mathf.Max(0f, duration));
    }

    // Executes the NotifyHeldItemHolstering routine.
    private void NotifyHeldItemHolstering(EquipmentItemData item, float duration)
    {
        if (item == null)
            return;

        HeldItemHolstering?.Invoke(item, Mathf.Max(0f, duration));
    }

    // Executes the TryResolveConsoleTargetSlot routine.
    private bool TryResolveConsoleTargetSlot(EquipmentItemData item, out RuntimeHandSlotState targetSlot)
    {
        targetSlot = null;
        if (item == null)
            return false;

        for (int i = 0; i < ConsoleSlotPreferenceOrder.Length; i++)
        {
            EquipmentSlotType slotType = ConsoleSlotPreferenceOrder[i];
            if (!item.SupportsSlot(slotType))
                continue;

            RuntimeHandSlotState slotState = GetRuntimeSlot(slotType);
            if (slotState != null && slotState.Item == null)
            {
                targetSlot = slotState;
                return true;
            }
        }

        for (int i = 0; i < ConsoleSlotPreferenceOrder.Length; i++)
        {
            EquipmentSlotType slotType = ConsoleSlotPreferenceOrder[i];
            if (!item.SupportsSlot(slotType))
                continue;

            RuntimeHandSlotState slotState = GetRuntimeSlot(slotType);
            if (slotState != null)
            {
                targetSlot = slotState;
                return true;
            }
        }

        return false;
    }

    // Executes the TryResolveConsoleStoredState routine.
    private bool TryResolveConsoleStoredState(
        EquipmentItemData item,
        out ProjectileData projectile,
        out int loadedAmmo,
        out int reserveAmmo,
        out string failureReason)
    {
        projectile = null;
        loadedAmmo = 0;
        reserveAmmo = 0;
        failureReason = null;

        if (item is FirearmData firearmData)
        {
            projectile = firearmData.CompatibleProjectiles.Count > 0 ? firearmData.CompatibleProjectiles[0] : null;
            if (projectile == null)
            {
                failureReason = $"{firearmData.DisplayName} has no compatible projectile assigned.";
                return false;
            }

            loadedAmmo = ResolveInitialLoadedAmmo(firearmData, -1);
            reserveAmmo = ResolveInitialReserveAmmo(firearmData, -1);
            return true;
        }

        if (item is ThrowableUtilityData throwableData)
        {
            reserveAmmo = ResolveInitialThrowableUses(throwableData, -1);
            return true;
        }

        if (item is UtilityItemData || item is MeleeWeaponData)
            return true;

        failureReason = $"{item.DisplayName} is not a supported console-spawnable equipment type.";
        return false;
    }
}
}
