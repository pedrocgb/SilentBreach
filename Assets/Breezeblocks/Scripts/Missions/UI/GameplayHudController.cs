using DG.Tweening;
using Rewired;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using Breezeblocks.WeaponSystem;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Gameplay HUD Controller")]
public class GameplayHudController : MonoBehaviour
{
    private static readonly HudUiSettings DefaultHudSettings = new();

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string showHudAction = "Show HUD";

    [FoldoutGroup("References")]
    [SerializeField] private CanvasGroup hudCanvasGroup;

    [FoldoutGroup("References")]
    [SerializeField] private CanvasGroup objectivesCanvasGroup;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text armorValueText;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController playerEquipmentController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerStaminaController playerStaminaController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerFocusController playerFocusController;

    [FoldoutGroup("References")]
    [SerializeField] private ArmorLoadout armorLoadout;

    [FoldoutGroup("Armor UI")]
    [SerializeField] private Color brokenArmorTextColor = Color.red;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsHudVisible => hudCanvasGroup == null || hudCanvasGroup.alpha > 0.001f;

    private Player rewiredPlayer;
    private Tween hudFadeTween;
    private Tween objectivesFadeTween;
    private float autoHideDeadline = -1f;
    private Color armorValueDefaultColor = Color.white;
    private GlobalSettings subscribedGlobalSettings;

    private void Reset()
    {
        if (hudCanvasGroup == null)
            hudCanvasGroup = GetComponent<CanvasGroup>();

        if (hudCanvasGroup == null)
            hudCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (objectivesCanvasGroup == null)
            objectivesCanvasGroup = hudCanvasGroup;

        ResolvePlayerReferences();
    }

    private void Awake()
    {
        if (hudCanvasGroup == null)
            hudCanvasGroup = GetComponent<CanvasGroup>();

        if (hudCanvasGroup == null)
            hudCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (objectivesCanvasGroup == null)
            objectivesCanvasGroup = hudCanvasGroup;

        ResolvePlayerReferences();
        ResolveRewiredPlayer();
        CacheArmorTextDefaults();
        RefreshArmorValueUi();
        ApplyVisibilityImmediate(hudVisible: true, objectivesVisible: true);
    }

    private void OnEnable()
    {
        ResolvePlayerReferences();
        ResolveRewiredPlayer();
        Subscribe();
        RefreshArmorValueUi();
        ShowHud(immediate: true);
        ScheduleAutoHideIfNeeded();
    }

    private void OnDisable()
    {
        Unsubscribe();
        hudFadeTween?.Kill();
        hudFadeTween = null;
        objectivesFadeTween?.Kill();
        objectivesFadeTween = null;
        autoHideDeadline = -1f;
    }

    public void HandleGameplayStarted()
    {
        ShowHud(immediate: false);
        ScheduleAutoHideIfNeeded();
    }

    private void Update()
    {
        if (rewiredPlayer == null)
            ResolveRewiredPlayer();

        if (rewiredPlayer != null && rewiredPlayer.GetButtonDown(showHudAction))
            ShowTemporarily();

        HudUiSettings settings = ResolveHudSettings();
        if (settings.HudAlwaysOn || autoHideDeadline < 0f || Time.unscaledTime < autoHideDeadline)
            return;

        autoHideDeadline = -1f;
        HideHud();
    }

    private void ResolvePlayerReferences()
    {
        if (playerEquipmentController == null)
            playerEquipmentController = FindFirstObjectByType<PlayerEquipmentController>();

        if (playerEquipmentController != null)
        {
            if (playerWeaponController == null)
                playerWeaponController = playerEquipmentController.GetComponent<PlayerWeaponController>();

            if (playerUtilityController == null)
                playerUtilityController = playerEquipmentController.GetComponent<PlayerUtilityController>();

            if (playerMeleeController == null)
                playerMeleeController = playerEquipmentController.GetComponent<PlayerMeleeController>();

            if (playerMotor == null)
                playerMotor = playerEquipmentController.GetComponent<PlayerTopDownMotor2D>();

            if (playerStaminaController == null)
                playerStaminaController = playerEquipmentController.GetComponent<PlayerStaminaController>();

            if (playerFocusController == null)
                playerFocusController = playerEquipmentController.GetComponent<PlayerFocusController>();

            if (armorLoadout == null)
                armorLoadout = playerEquipmentController.GetComponent<ArmorLoadout>();
        }

        playerWeaponController ??= FindFirstObjectByType<PlayerWeaponController>();
        playerUtilityController ??= FindFirstObjectByType<PlayerUtilityController>();
        playerMeleeController ??= FindFirstObjectByType<PlayerMeleeController>();
        playerMotor ??= FindFirstObjectByType<PlayerTopDownMotor2D>();
        playerStaminaController ??= FindFirstObjectByType<PlayerStaminaController>();
        playerFocusController ??= FindFirstObjectByType<PlayerFocusController>();
        armorLoadout ??= FindFirstObjectByType<ArmorLoadout>();
    }

    private void Subscribe()
    {
        if (playerEquipmentController != null)
        {
            playerEquipmentController.EquipmentChanged -= HandleEquipmentChanged;
            playerEquipmentController.EquipmentChanged += HandleEquipmentChanged;
            playerEquipmentController.HeldItemEquipping -= HandleHeldItemEquipping;
            playerEquipmentController.HeldItemEquipping += HandleHeldItemEquipping;
            playerEquipmentController.HeldItemHolstering -= HandleHeldItemHolstering;
            playerEquipmentController.HeldItemHolstering += HandleHeldItemHolstering;
        }

        if (playerWeaponController != null)
        {
            playerWeaponController.WeaponFired -= HandleWeaponFired;
            playerWeaponController.WeaponFired += HandleWeaponFired;
        }

        if (playerUtilityController != null)
        {
            playerUtilityController.UtilityActivated -= HandleUtilityActivated;
            playerUtilityController.UtilityActivated += HandleUtilityActivated;
        }

        if (playerMeleeController != null)
        {
            playerMeleeController.AttackStarted -= HandleMeleeAttackStarted;
            playerMeleeController.AttackStarted += HandleMeleeAttackStarted;
        }

        if (playerMotor != null)
        {
            playerMotor.ManualSpeedLevelChanged -= HandleManualSpeedLevelChanged;
            playerMotor.ManualSpeedLevelChanged += HandleManualSpeedLevelChanged;
        }

        if (playerStaminaController != null)
        {
            playerStaminaController.StaminaSpent -= HandleStaminaSpent;
            playerStaminaController.StaminaSpent += HandleStaminaSpent;
        }

        if (playerFocusController != null)
        {
            playerFocusController.FocusSpent -= HandleFocusSpent;
            playerFocusController.FocusSpent += HandleFocusSpent;
        }

        if (armorLoadout != null)
        {
            armorLoadout.ArmorChanged -= RefreshArmorValueUi;
            armorLoadout.ArmorChanged += RefreshArmorValueUi;
        }

        if (GlobalSettings.Instance != null)
        {
            subscribedGlobalSettings = GlobalSettings.Instance;
            subscribedGlobalSettings.SettingsChanged -= HandleSettingsChanged;
            subscribedGlobalSettings.SettingsChanged += HandleSettingsChanged;
        }
    }

    private void Unsubscribe()
    {
        if (playerEquipmentController != null)
        {
            playerEquipmentController.EquipmentChanged -= HandleEquipmentChanged;
            playerEquipmentController.HeldItemEquipping -= HandleHeldItemEquipping;
            playerEquipmentController.HeldItemHolstering -= HandleHeldItemHolstering;
        }

        if (playerWeaponController != null)
            playerWeaponController.WeaponFired -= HandleWeaponFired;

        if (playerUtilityController != null)
            playerUtilityController.UtilityActivated -= HandleUtilityActivated;

        if (playerMeleeController != null)
            playerMeleeController.AttackStarted -= HandleMeleeAttackStarted;

        if (playerMotor != null)
            playerMotor.ManualSpeedLevelChanged -= HandleManualSpeedLevelChanged;

        if (playerStaminaController != null)
            playerStaminaController.StaminaSpent -= HandleStaminaSpent;

        if (playerFocusController != null)
            playerFocusController.FocusSpent -= HandleFocusSpent;

        if (armorLoadout != null)
            armorLoadout.ArmorChanged -= RefreshArmorValueUi;

        if (subscribedGlobalSettings != null)
        {
            subscribedGlobalSettings.SettingsChanged -= HandleSettingsChanged;
            subscribedGlobalSettings = null;
        }
    }

    private void CacheArmorTextDefaults()
    {
        if (armorValueText != null)
            armorValueDefaultColor = armorValueText.color;
    }

    private void RefreshArmorValueUi()
    {
        if (armorValueText == null)
            return;

        if (armorLoadout == null || !armorLoadout.HasEquippedArmor)
        {
            armorValueText.text = "-";
            armorValueText.color = armorValueDefaultColor;
            return;
        }

        float clampedArmorValue = Mathf.Max(0f, armorLoadout.CurrentArmorValue);
        armorValueText.text = clampedArmorValue.ToString("0.##");
        armorValueText.color = clampedArmorValue <= 0f ? brokenArmorTextColor : armorValueDefaultColor;
    }

    private void HandleEquipmentChanged()
    {
        RefreshArmorValueUi();
        ShowTemporarily();
    }

    private void HandleHeldItemEquipping(EquipmentItemData _, float __)
    {
        ShowTemporarily();
    }

    private void HandleHeldItemHolstering(EquipmentItemData _, float __)
    {
        ShowTemporarily();
    }

    private void HandleWeaponFired()
    {
        ShowTemporarily();
    }

    private void HandleUtilityActivated()
    {
        ShowTemporarily();
    }

    private void HandleMeleeAttackStarted()
    {
        ShowTemporarily();
    }

    private void HandleManualSpeedLevelChanged(int _)
    {
        ShowTemporarily();
    }

    private void HandleStaminaSpent(float amount)
    {
        if (amount > 0f)
            ShowTemporarily();
    }

    private void HandleFocusSpent(float amount)
    {
        if (amount > 0f)
            ShowTemporarily();
    }

    private void HandleSettingsChanged()
    {
        ShowHud(immediate: true);
        ScheduleAutoHideIfNeeded();
    }

    private void ShowTemporarily()
    {
        ShowHud(immediate: false);
        ScheduleAutoHideIfNeeded();
    }

    private void ScheduleAutoHideIfNeeded()
    {
        HudUiSettings settings = ResolveHudSettings();
        if (settings.HudAlwaysOn)
        {
            autoHideDeadline = -1f;
            return;
        }

        autoHideDeadline = Time.unscaledTime + settings.AutoHideDelaySeconds;
    }

    private void ShowHud(bool immediate)
    {
        SetCanvasGroupVisible(hudCanvasGroup, true, immediate, ResolveHudSettings().FadeInDuration);
        SetObjectivesVisible(true, immediate, ResolveHudSettings().FadeInDuration);
    }

    private void HideHud()
    {
        HudUiSettings settings = ResolveHudSettings();
        SetCanvasGroupVisible(hudCanvasGroup, false, immediate: false, settings.FadeOutDuration);
        SetObjectivesVisible(settings.ObjectivesAlwaysOn, immediate: false, settings.FadeOutDuration);
    }

    private void SetObjectivesVisible(bool visible, bool immediate, float duration)
    {
        if (objectivesCanvasGroup == null || objectivesCanvasGroup == hudCanvasGroup)
            return;

        SetCanvasGroupVisible(objectivesCanvasGroup, visible, immediate, duration, isObjectives: true);
    }

    private void ApplyVisibilityImmediate(bool hudVisible, bool objectivesVisible)
    {
        SetCanvasGroupVisible(hudCanvasGroup, hudVisible, immediate: true, duration: 0f);
        if (objectivesCanvasGroup != null && objectivesCanvasGroup != hudCanvasGroup)
            SetCanvasGroupVisible(objectivesCanvasGroup, objectivesVisible, immediate: true, duration: 0f, isObjectives: true);
    }

    private void SetCanvasGroupVisible(CanvasGroup canvasGroup, bool visible, bool immediate, float duration, bool isObjectives = false)
    {
        if (canvasGroup == null)
            return;

        Tween activeTween = isObjectives ? objectivesFadeTween : hudFadeTween;
        activeTween?.Kill();

        if (immediate || duration <= 0f)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            if (isObjectives)
                objectivesFadeTween = null;
            else
                hudFadeTween = null;

            return;
        }

        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        Tween tween = canvasGroup.DOFade(visible ? 1f : 0f, Mathf.Max(0f, duration))
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (isObjectives)
                    objectivesFadeTween = null;
                else
                    hudFadeTween = null;
            });

        if (isObjectives)
            objectivesFadeTween = tween;
        else
            hudFadeTween = tween;
    }

    private HudUiSettings ResolveHudSettings()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.HudUi
            : DefaultHudSettings;
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }
}
}
