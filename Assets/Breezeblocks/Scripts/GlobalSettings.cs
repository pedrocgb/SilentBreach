using System;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class EquipmentContextUiSettings
{
    [FoldoutGroup("Equipment UI")]
    [FoldoutGroup("Equipment UI/Text"), LabelText("Yes Text")]
    [SerializeField] private string yesText = "Yes";

    [FoldoutGroup("Equipment UI/Text"), LabelText("No Text")]
    [SerializeField] private string noText = "No";

    [FoldoutGroup("Equipment UI/Text"), LabelText("Rounds Per Second Text")]
    [SerializeField] private string roundsPerSecondText = "rounds/s";

    [FoldoutGroup("Equipment UI/Grip Types"), LabelText("One Handed Text")]
    [SerializeField] private string oneHandedGripText = "One Handed";

    [FoldoutGroup("Equipment UI/Grip Types"), LabelText("Two Handed Text")]
    [SerializeField] private string twoHandedGripText = "Two Handed";

    [FoldoutGroup("Equipment UI/Slot Names"), LabelText("Primary Slot Name")]
    [SerializeField] private string primarySlotName = "Primary";

    [FoldoutGroup("Equipment UI/Slot Names"), LabelText("Secondary Slot Name")]
    [SerializeField] private string secondarySlotName = "Secondary";

    [FoldoutGroup("Equipment UI/Slot Names"), LabelText("Belt Slot Name")]
    [SerializeField] private string beltSlotName = "Belt";

    [FoldoutGroup("Equipment UI/Slot Names"), LabelText("Armor Slot Name")]
    [SerializeField] private string armorSlotName = "Armor";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Grip Prefix")]
    [SerializeField] private string gripPrefix = "Grip: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Lethal Prefix")]
    [SerializeField] private string lethalPrefix = "Lethal: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Stamina Cost Prefix")]
    [SerializeField] private string staminaCostPrefix = "Stamina Cost: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Armor Penetration Prefix")]
    [SerializeField] private string armorPenetrationPrefix = "Armor Penetration: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Firearm Penetration Prefix")]
    [SerializeField] private string firearmPenetrationPrefix = "Penetration: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Slots Prefix")]
    [SerializeField] private string slotsPrefix = "Slots: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Class Prefix")]
    [SerializeField] private string classPrefix = "Class: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Fire Mode Prefix")]
    [SerializeField] private string fireModePrefix = "Fire Mode: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Fire Rate Prefix")]
    [SerializeField] private string fireRatePrefix = "Fire Rate: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Spread Prefix")]
    [SerializeField] private string spreadPrefix = "Spread: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Ammo Prefix")]
    [SerializeField] private string ammoPrefix = "Ammo: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Reserve Ammo Prefix")]
    [SerializeField] private string reserveAmmoPrefix = "Reserve Ammo: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Reload Time Prefix")]
    [SerializeField] private string reloadTimePrefix = "Reload Time: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Utility Type Prefix")]
    [SerializeField] private string utilityTypePrefix = "Type: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Quantity Prefix")]
    [SerializeField] private string quantityPrefix = "Quantity: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Flashbang Duration Prefix")]
    [SerializeField] private string flashbangDurationPrefix = "Flashbang Duration: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Explosion Radius Prefix")]
    [SerializeField] private string explosionRadiusPrefix = "Explosion Radius: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Explosion Type Prefix")]
    [SerializeField] private string explosionTypePrefix = "Explosion Type: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Detonation Delay Prefix")]
    [SerializeField] private string detonationDelayPrefix = "Detonation Delay: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Armor Class Prefix")]
    [SerializeField] private string armorClassPrefix = "Armor Class: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Armor Value Prefix")]
    [SerializeField] private string armorValuePrefix = "Armor Value: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Rotation Penalty Prefix")]
    [SerializeField] private string rotationPenaltyPrefix = "Rotation Penalty: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Movement Noise Increase Prefix")]
    [SerializeField] private string movementNoiseIncreasePrefix = "Movement Noise Increase: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Movement Speed Penalty Prefix")]
    [SerializeField] private string movementSpeedPenaltyPrefix = "Movement Speed Penalty: ";

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Prefix Color")]
    [SerializeField] private Color prefixColor = Color.white;

    [FoldoutGroup("Equipment UI/Utility Types"), LabelText("Noise Maker Text")]
    [SerializeField] private string throwableNoiseMakerText = "Noise Maker";

    [FoldoutGroup("Equipment UI/Utility Types"), LabelText("Direct Damage Text")]
    [SerializeField] private string throwableDirectDamageText = "Damage";

    [FoldoutGroup("Equipment UI/Utility Types"), LabelText("Explosion Text")]
    [SerializeField] private string throwableExplosionText = "Explosion";

    [FoldoutGroup("Equipment UI/Utility Types"), LabelText("Flashbang Text")]
    [SerializeField] private string throwableFlashbangText = "Flashbang";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Pistol Text")]
    [SerializeField] private string pistolClassText = "Pistol";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Revolver Text")]
    [SerializeField] private string revolverClassText = "Revolver";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("SMG Text")]
    [SerializeField] private string smgClassText = "SMG";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Shotgun Text")]
    [SerializeField] private string shotgunClassText = "Shotgun";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Pump Shotgun Text")]
    [SerializeField] private string pumpShotgunClassText = "Pump Shotgun";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Semi Auto Shotgun Text")]
    [SerializeField] private string semiAutoShotgunClassText = "Semi Auto Shotgun";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Rifle Text")]
    [SerializeField] private string rifleClassText = "Rifle";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Assault Rifle Text")]
    [SerializeField] private string assaultRifleClassText = "Assault Rifle";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Carbine Text")]
    [SerializeField] private string carbineClassText = "Carbine";

    [FoldoutGroup("Equipment UI/Firearm Classes"), LabelText("Sniper Rifle Text")]
    [SerializeField] private string sniperRifleClassText = "Sniper Rifle";

    [FoldoutGroup("Equipment UI/Detonation Modes"), LabelText("On Hit Text")]
    [SerializeField] private string detonationOnHitText = "On Hit";

    [FoldoutGroup("Equipment UI/Detonation Modes"), LabelText("On Timer Text")]
    [SerializeField] private string detonationOnTimerText = "On Timer";

    [FoldoutGroup("Equipment UI/Detonation Modes"), LabelText("On Hit And Timer Text")]
    [SerializeField] private string detonationOnHitAndTimerText = "On Hit and Timer";

    [FoldoutGroup("Hideout UI")]
    [FoldoutGroup("Hideout UI/Job Levels"), LabelText("Level Prefix")]
    [SerializeField] private string jobLevelPrefix = "Level: ";

    [FoldoutGroup("Hideout UI/Job Levels"), LabelText("Easy Text")]
    [SerializeField] private string easyJobLevelText = "Easy";

    [FoldoutGroup("Hideout UI/Job Levels"), LabelText("Medium Text")]
    [SerializeField] private string mediumJobLevelText = "Medium";

    [FoldoutGroup("Hideout UI/Job Levels"), LabelText("Hard Text")]
    [SerializeField] private string hardJobLevelText = "Hard";

    [FoldoutGroup("Hideout UI/Job Levels"), LabelText("Insane Text")]
    [SerializeField] private string insaneJobLevelText = "Insane";

    public string YesText => Fallback(yesText, "Yes");
    public string NoText => Fallback(noText, "No");
    public string RoundsPerSecondText => Fallback(roundsPerSecondText, "rounds/s");
    public string OneHandedGripText => Fallback(oneHandedGripText, "One Handed");
    public string TwoHandedGripText => Fallback(twoHandedGripText, "Two Handed");
    public string GripPrefix => Fallback(gripPrefix, "Grip: ");
    public string LethalPrefix => Fallback(lethalPrefix, "Lethal: ");
    public string StaminaCostPrefix => Fallback(staminaCostPrefix, "Stamina Cost: ");
    public string ArmorPenetrationPrefix => Fallback(armorPenetrationPrefix, "Armor Penetration: ");
    public string FirearmPenetrationPrefix => Fallback(firearmPenetrationPrefix, "Penetration: ");
    public string SlotsPrefix => Fallback(slotsPrefix, "Slots: ");
    public string ClassPrefix => Fallback(classPrefix, "Class: ");
    public string FireModePrefix => Fallback(fireModePrefix, "Fire Mode: ");
    public string FireRatePrefix => Fallback(fireRatePrefix, "Fire Rate: ");
    public string SpreadPrefix => Fallback(spreadPrefix, "Spread: ");
    public string AmmoPrefix => Fallback(ammoPrefix, "Ammo: ");
    public string ReserveAmmoPrefix => Fallback(reserveAmmoPrefix, "Reserve Ammo: ");
    public string ReloadTimePrefix => Fallback(reloadTimePrefix, "Reload Time: ");
    public string UtilityTypePrefix => Fallback(utilityTypePrefix, "Type: ");
    public string QuantityPrefix => Fallback(quantityPrefix, "Quantity: ");
    public string FlashbangDurationPrefix => Fallback(flashbangDurationPrefix, "Flashbang Duration: ");
    public string ExplosionRadiusPrefix => Fallback(explosionRadiusPrefix, "Explosion Radius: ");
    public string ExplosionTypePrefix => Fallback(explosionTypePrefix, "Explosion Type: ");
    public string DetonationDelayPrefix => Fallback(detonationDelayPrefix, "Detonation Delay: ");
    public string ArmorClassPrefix => Fallback(armorClassPrefix, "Armor Class: ");
    public string ArmorValuePrefix => Fallback(armorValuePrefix, "Armor Value: ");
    public string RotationPenaltyPrefix => Fallback(rotationPenaltyPrefix, "Rotation Penalty: ");
    public string MovementNoiseIncreasePrefix => Fallback(movementNoiseIncreasePrefix, "Movement Noise Increase: ");
    public string MovementSpeedPenaltyPrefix => Fallback(movementSpeedPenaltyPrefix, "Movement Speed Penalty: ");
    public Color PrefixColor => prefixColor;
    public string JobLevelPrefix => Fallback(jobLevelPrefix, "Level: ");

    public string GetBoolText(bool value)
    {
        return value ? YesText : NoText;
    }

    public string GetSlotDisplayName(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Primary => Fallback(primarySlotName, "Primary"),
            EquipmentSlotType.Secondary => Fallback(secondarySlotName, "Secondary"),
            EquipmentSlotType.Belt => Fallback(beltSlotName, "Belt"),
            EquipmentSlotType.Armor => Fallback(armorSlotName, "Armor"),
            _ => "None"
        };
    }

    public string GetThrowableBehaviorText(ThrowableUtilityBehavior behavior)
    {
        return behavior switch
        {
            ThrowableUtilityBehavior.NoiseMaker => Fallback(throwableNoiseMakerText, "Noise Maker"),
            ThrowableUtilityBehavior.DirectDamage => Fallback(throwableDirectDamageText, "Damage"),
            ThrowableUtilityBehavior.Explosion => Fallback(throwableExplosionText, "Explosion"),
            ThrowableUtilityBehavior.Flashbang => Fallback(throwableFlashbangText, "Flashbang"),
            _ => "Utility"
        };
    }

    public string GetFirearmClassText(FirearmClass firearmClass)
    {
        return firearmClass switch
        {
            FirearmClass.Pistol => Fallback(pistolClassText, "Pistol"),
            FirearmClass.Revolver => Fallback(revolverClassText, "Revolver"),
            FirearmClass.SMG => Fallback(smgClassText, "SMG"),
            FirearmClass.Shotgun => Fallback(shotgunClassText, "Shotgun"),
            FirearmClass.PumpShotgun => Fallback(pumpShotgunClassText, "Pump Shotgun"),
            FirearmClass.SemiAutoShotgun => Fallback(semiAutoShotgunClassText, "Semi Auto Shotgun"),
            FirearmClass.Rifle => Fallback(rifleClassText, "Rifle"),
            FirearmClass.AssaultRifle => Fallback(assaultRifleClassText, "Assault Rifle"),
            FirearmClass.Carbine => Fallback(carbineClassText, "Carbine"),
            FirearmClass.SniperRifle => Fallback(sniperRifleClassText, "Sniper Rifle"),
            _ => "Firearm"
        };
    }

    public string GetFirearmGripText(FirearmGripType gripType)
    {
        return gripType switch
        {
            FirearmGripType.OneHanded => OneHandedGripText,
            FirearmGripType.TwoHanded => TwoHandedGripText,
            _ => "Grip"
        };
    }

    public string GetMeleeGripText(MeleeGripType gripType)
    {
        return gripType switch
        {
            MeleeGripType.OneHanded => OneHandedGripText,
            MeleeGripType.TwoHanded => TwoHandedGripText,
            _ => "Grip"
        };
    }

    public string GetDetonationModeText(ThrowableDetonationMode detonationMode)
    {
        return detonationMode switch
        {
            ThrowableDetonationMode.OnHit => Fallback(detonationOnHitText, "On Hit"),
            ThrowableDetonationMode.OnTimer => Fallback(detonationOnTimerText, "On Timer"),
            ThrowableDetonationMode.OnHitAndTimer => Fallback(detonationOnHitAndTimerText, "On Hit and Timer"),
            _ => "Detonation"
        };
    }

    public string GetJobLevelText(Breezeblocks.HideoutSystem.HideoutJobLevel jobLevel)
    {
        return jobLevel switch
        {
            Breezeblocks.HideoutSystem.HideoutJobLevel.Easy => Fallback(easyJobLevelText, "Easy"),
            Breezeblocks.HideoutSystem.HideoutJobLevel.Medium => Fallback(mediumJobLevelText, "Medium"),
            Breezeblocks.HideoutSystem.HideoutJobLevel.Hard => Fallback(hardJobLevelText, "Hard"),
            Breezeblocks.HideoutSystem.HideoutJobLevel.Insane => Fallback(insaneJobLevelText, "Insane"),
            _ => "Unknown"
        };
    }

    private static string Fallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

[Serializable]
public sealed class HudUiSettings
{
    [FoldoutGroup("HUD"), LabelText("HUD Always On")]
    [SerializeField] private bool hudAlwaysOn = true;

    [FoldoutGroup("HUD"), LabelText("Objectives Always On")]
    [SerializeField] private bool objectivesAlwaysOn = true;

    [FoldoutGroup("HUD"), LabelText("Auto Hide Delay"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float autoHideDelaySeconds = 4f;

    [FoldoutGroup("HUD"), LabelText("Fade In Duration"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float fadeInDuration = 0.18f;

    [FoldoutGroup("HUD"), LabelText("Fade Out Duration"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float fadeOutDuration = 0.4f;

    public bool HudAlwaysOn => hudAlwaysOn;
    public bool ObjectivesAlwaysOn => objectivesAlwaysOn;
    public float AutoHideDelaySeconds => Mathf.Max(0f, autoHideDelaySeconds);
    public float FadeInDuration => Mathf.Max(0f, fadeInDuration);
    public float FadeOutDuration => Mathf.Max(0f, fadeOutDuration);
}

[AddComponentMenu("Breezeblocks/Global Settings")]
public class GlobalSettings : MonoBehaviour
{
    public static GlobalSettings Instance { get; private set; }

    [FoldoutGroup("Input Modes"), Tooltip("If true, sprint works as toggle (press once). If false, hold to sprint.")]
    [SerializeField] private bool sprintToggleEnabled = false;

    [FoldoutGroup("Input Modes"), Tooltip("If true, focus works as toggle (press once). If false, hold to focus.")]
    [SerializeField] private bool focusToggleEnabled = false;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [Tooltip("How long a firearm shot noise spike lasts.")]
    [SerializeField] private float shotNoiseDuration = 0.1f;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [Tooltip("How long an equip noise spike lasts.")]
    [SerializeField] private float equipNoiseDuration = 0.4f;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [Tooltip("How long a holster noise spike lasts.")]
    [SerializeField] private float holsterNoiseDuration = 0.6f;

    [FoldoutGroup("Combat"), MinValue(0f), SuffixLabel("s", true)]
    [Tooltip("How long incapacitated actors stay down before waking up again.")]
    [SerializeField] private float incapacitatedWakeUpDelay = 60f;

    [FoldoutGroup("Equipment UI"), InlineProperty]
    [SerializeField] private EquipmentContextUiSettings equipmentContextUi = new();

    [FoldoutGroup("HUD"), InlineProperty]
    [SerializeField] private HudUiSettings hudUi = new();

    public bool SprintToggleEnabled => sprintToggleEnabled;
    public bool FocusToggleEnabled => focusToggleEnabled;
    public float ShotNoiseDuration => shotNoiseDuration;
    public float EquipNoiseDuration => equipNoiseDuration;
    public float HolsterNoiseDuration => holsterNoiseDuration;
    public float IncapacitatedWakeUpDelay => incapacitatedWakeUpDelay;
    public EquipmentContextUiSettings EquipmentContextUi => equipmentContextUi ??= new EquipmentContextUiSettings();
    public HudUiSettings HudUi => hudUi ??= new HudUiSettings();

    public event Action SettingsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        shotNoiseDuration = Mathf.Max(0f, shotNoiseDuration);
        equipNoiseDuration = Mathf.Max(0f, equipNoiseDuration);
        holsterNoiseDuration = Mathf.Max(0f, holsterNoiseDuration);
        incapacitatedWakeUpDelay = Mathf.Max(0f, incapacitatedWakeUpDelay);
        equipmentContextUi ??= new EquipmentContextUiSettings();
        hudUi ??= new HudUiSettings();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void ToggleSprintMode()
    {
        SetSprintToggleEnabled(!sprintToggleEnabled);
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void ToggleFocusMode()
    {
        SetFocusToggleEnabled(!focusToggleEnabled);
    }

    public void SetSprintToggleEnabled(bool enabled)
    {
        if (sprintToggleEnabled == enabled)
            return;

        sprintToggleEnabled = enabled;
        SettingsChanged?.Invoke();
    }

    public void SetFocusToggleEnabled(bool enabled)
    {
        if (focusToggleEnabled == enabled)
            return;

        focusToggleEnabled = enabled;
        SettingsChanged?.Invoke();
    }
}
