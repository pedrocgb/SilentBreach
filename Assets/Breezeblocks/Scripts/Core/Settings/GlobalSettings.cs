using System;
using Breezeblocks.Settings;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

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

    [FoldoutGroup("Equipment UI/Prefixes"), LabelText("Item Kind Prefix")]
    [SerializeField] private string itemKindPrefix = "Tipo: ";

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

    [FoldoutGroup("Equipment UI/Item Kinds"), LabelText("Melee Text")]
    [SerializeField] private string meleeItemKindText = "Arma Branca";

    [FoldoutGroup("Equipment UI/Item Kinds"), LabelText("Firearm Text")]
    [SerializeField] private string firearmItemKindText = "Arma de Fogo";

    [FoldoutGroup("Equipment UI/Item Kinds"), LabelText("Utility Text")]
    [SerializeField] private string utilityItemKindText = "Utilitário";

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

    [FoldoutGroup("Hideout UI/Perks"), LabelText("Perk Tier Prefix")]
    [SerializeField] private string perkTierText = "Tier: ";

    [FoldoutGroup("Hideout UI/Perks"), LabelText("Perk Cost Prefix")]
    [SerializeField] private string perkCostText = "Cost: ";

    [FoldoutGroup("Hideout UI/Perks"), LabelText("Perks Text")]
    [SerializeField] private string perksText = "Pontos de Talento";

    [FoldoutGroup("Hideout UI/Progression"), LabelText("Experience Text")]
    [SerializeField] private string experienceText = "Experiência";

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
    public string ItemKindPrefix => Fallback(itemKindPrefix, "Tipo: ");
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
    public string PerkTierText => Fallback(perkTierText, "Tier: ");
    public string PerkCostText => Fallback(perkCostText, "Cost: ");
    public string PerksText => Fallback(perksText, "Pontos de Talento");
    public string ExperienceText => Fallback(experienceText, "Experiência");

    /// <summary>
    /// Returns the localized text used for a boolean equipment value.
    /// </summary>
    public string GetBoolText(bool value)
    {
        return value ? YesText : NoText;
    }

    /// <summary>
    /// Returns the configured display name for the supplied equipment slot.
    /// </summary>
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

    /// <summary>
    /// Returns the configured display label for an equipment item kind.
    /// </summary>
    public string GetItemKindText(EquipmentItemKind itemKind)
    {
        return itemKind switch
        {
            EquipmentItemKind.Melee => Fallback(meleeItemKindText, "Arma Branca"),
            EquipmentItemKind.Firearm => Fallback(firearmItemKindText, "Arma de Fogo"),
            EquipmentItemKind.Utility => Fallback(utilityItemKindText, "Utilitário"),
            EquipmentItemKind.Armor => GetSlotDisplayName(EquipmentSlotType.Armor),
            _ => "Item"
        };
    }

    /// <summary>
    /// Returns the configured display label for a throwable utility behavior.
    /// </summary>
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

    /// <summary>
    /// Returns the configured display label for a firearm class.
    /// </summary>
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

    /// <summary>
    /// Returns the configured display label for a firearm grip type.
    /// </summary>
    public string GetFirearmGripText(FirearmGripType gripType)
    {
        return gripType switch
        {
            FirearmGripType.OneHanded => OneHandedGripText,
            FirearmGripType.TwoHanded => TwoHandedGripText,
            _ => "Grip"
        };
    }

    /// <summary>
    /// Returns the configured display label for a melee grip type.
    /// </summary>
    public string GetMeleeGripText(MeleeGripType gripType)
    {
        return gripType switch
        {
            MeleeGripType.OneHanded => OneHandedGripText,
            MeleeGripType.TwoHanded => TwoHandedGripText,
            _ => "Grip"
        };
    }

    /// <summary>
    /// Returns the configured display label for a throwable detonation mode.
    /// </summary>
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

    /// <summary>
    /// Returns the configured hideout label for the supplied job difficulty level.
    /// </summary>
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

    /// <summary>
    /// Returns a trimmed fallback value when the configured text is empty.
    /// </summary>
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

[Serializable]
public sealed class MissionFailurePresentationSettings
{
    [FoldoutGroup("Timing"), Range(0.01f, 1f), LabelText("Target Time Scale")]
    [SerializeField] private float targetTimeScale = 0.3f;

    [FoldoutGroup("Timing"), MinValue(0f), SuffixLabel("s", true), LabelText("Slow Motion Duration")]
    [SerializeField] private float slowMotionDuration = 0.6f;

    [FoldoutGroup("Timing"), MinValue(0f), SuffixLabel("s", true), LabelText("Screen Delay")]
    [SerializeField] private float screenDelay = 1.5f;

    [FoldoutGroup("Enemy Focus Zoom"), MinValue(0.01f), LabelText("Orthographic Size")]
    [Tooltip("Target camera orthographic size during enemy-focused failures. Smaller values zoom in further.")]
    [SerializeField] private float enemyFocusOrthographicSize = 3f;

    [FoldoutGroup("Enemy Focus Zoom"), MinValue(0f), SuffixLabel("s", true), LabelText("Zoom Duration")]
    [Tooltip("Unscaled duration used to reach the enemy-focus zoom.")]
    [SerializeField] private float enemyFocusZoomDuration = 0.6f;

    [FoldoutGroup("Player Killed Tint"), ColorUsage(false, false), LabelText("Tint Color")]
    [SerializeField] private Color playerKilledTintColor = new(1f, 0.18f, 0.18f, 1f);

    [FoldoutGroup("Player Killed Tint"), MinValue(0f), SuffixLabel("s", true), LabelText("Tint Duration")]
    [SerializeField] private float playerKilledTintDuration = 0.8f;

    public float TargetTimeScale => Mathf.Clamp(targetTimeScale, 0.01f, 1f);
    public float SlowMotionDuration => Mathf.Max(0f, slowMotionDuration);
    public float ScreenDelay => Mathf.Max(0f, screenDelay);
    public float EnemyFocusOrthographicSize => Mathf.Max(0.01f, enemyFocusOrthographicSize);
    public float EnemyFocusZoomDuration => Mathf.Max(0f, enemyFocusZoomDuration);
    public Color PlayerKilledTintColor => playerKilledTintColor;
    public float PlayerKilledTintDuration => Mathf.Max(0f, playerKilledTintDuration);
}

[AddComponentMenu("Breezeblocks/Global Settings")]
public class GlobalSettings : MonoBehaviour
{
    public static GlobalSettings Instance { get; private set; }

    [FoldoutGroup("Input Modes"), Tooltip("If true, sprint works as toggle (press once). If false, hold to sprint.")]
    [SerializeField] private bool sprintToggleEnabled = false;

    [FoldoutGroup("Input Modes"), Tooltip("If true, focus works as toggle (press once). If false, hold to focus.")]
    [SerializeField] private bool focusToggleEnabled = false;

    [FoldoutGroup("Input Modes"), Tooltip("If true, dragging a body requires holding interact. If false, interact toggles dragging on and off.")]
    [SerializeField] private bool dragRequiresHoldInput = true;

    [FoldoutGroup("Player Settings")]
    [FoldoutGroup("Player Settings/Audio"), AssetsOnly]
    [SerializeField] private AudioMixer settingsAudioMixer;

    [FoldoutGroup("Player Settings/Audio")]
    [SerializeField] private string masterVolumeParameter = "MasterVolume";

    [FoldoutGroup("Player Settings/Audio")]
    [SerializeField] private string musicVolumeParameter = "MusicVolume";

    [FoldoutGroup("Player Settings/Audio")]
    [SerializeField] private string sfxVolumeParameter = "SfxVolume";

    [FoldoutGroup("Player Settings/Audio")]
    [SerializeField] private string uiVolumeParameter = "UiVolume";

    [FoldoutGroup("Player Settings/Audio")]
    [SerializeField] private string ambientVolumeParameter = "AmbientVolume";

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

    [FoldoutGroup("Combat"), MinValue(0f), SuffixLabel("s", true)]
    [Tooltip("How long sleeping enemies wait after waking up before resuming their requested behavior.")]
    [SerializeField] private float sleepWakeActionDelay = 1f;

    [FoldoutGroup("AI")]
    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Left"), LabelText("Left Min"), SuffixLabel("deg", true)]
    [SerializeField] private float leftLookaroundMinAngle = 135f;

    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Left"), LabelText("Left Max"), SuffixLabel("deg", true)]
    [SerializeField] private float leftLookaroundMaxAngle = 225f;

    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Right"), LabelText("Right Min"), SuffixLabel("deg", true)]
    [SerializeField] private float rightLookaroundMinAngle = -45f;

    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Right"), LabelText("Right Max"), SuffixLabel("deg", true)]
    [SerializeField] private float rightLookaroundMaxAngle = 45f;

    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Up"), LabelText("Up Min"), SuffixLabel("deg", true)]
    [SerializeField] private float upLookaroundMinAngle = 45f;

    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Up"), LabelText("Up Max"), SuffixLabel("deg", true)]
    [SerializeField] private float upLookaroundMaxAngle = 135f;

    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Down"), LabelText("Down Min"), SuffixLabel("deg", true)]
    [SerializeField] private float downLookaroundMinAngle = -135f;

    [FoldoutGroup("AI/Light Switch Lookaround"), HorizontalGroup("AI/Light Switch Lookaround/Down"), LabelText("Down Max"), SuffixLabel("deg", true)]
    [SerializeField] private float downLookaroundMaxAngle = -45f;

    [FoldoutGroup("Player"), Range(0f, 100f), SuffixLabel("%", true)]
    [Tooltip("How much slower the player moves while dragging a body.")]
    [SerializeField] private float dragSlowPercentage = 35f;

    [FoldoutGroup("Equipment UI"), InlineProperty]
    [SerializeField] private EquipmentContextUiSettings equipmentContextUi = new();

    [FoldoutGroup("HUD"), InlineProperty]
    [SerializeField] private HudUiSettings hudUi = new();

    [FoldoutGroup("Mission Failure"), InlineProperty]
    [SerializeField] private MissionFailurePresentationSettings missionFailurePresentation = new();

    public bool SprintToggleEnabled => sprintToggleEnabled;
    public bool FocusToggleEnabled => focusToggleEnabled;
    public bool DragRequiresHoldInput => dragRequiresHoldInput;
    public float ShotNoiseDuration => shotNoiseDuration;
    public float EquipNoiseDuration => equipNoiseDuration;
    public float HolsterNoiseDuration => holsterNoiseDuration;
    public float IncapacitatedWakeUpDelay => incapacitatedWakeUpDelay;
    public float SleepWakeActionDelay => sleepWakeActionDelay;
    public float LeftLookaroundMinAngle => leftLookaroundMinAngle;
    public float LeftLookaroundMaxAngle => leftLookaroundMaxAngle;
    public float RightLookaroundMinAngle => rightLookaroundMinAngle;
    public float RightLookaroundMaxAngle => rightLookaroundMaxAngle;
    public float UpLookaroundMinAngle => upLookaroundMinAngle;
    public float UpLookaroundMaxAngle => upLookaroundMaxAngle;
    public float DownLookaroundMinAngle => downLookaroundMinAngle;
    public float DownLookaroundMaxAngle => downLookaroundMaxAngle;
    public float DragSlowPercentage => dragSlowPercentage;
    public EquipmentContextUiSettings EquipmentContextUi => equipmentContextUi ??= new EquipmentContextUiSettings();
    public string PerksText => EquipmentContextUi.PerksText;
    public string ExperienceText => EquipmentContextUi.ExperienceText;
    public HudUiSettings HudUi => hudUi ??= new HudUiSettings();
    public MissionFailurePresentationSettings MissionFailurePresentation =>
        missionFailurePresentation ??= new MissionFailurePresentationSettings();

    public event Action SettingsChanged;

    /// <summary>
    /// Registers the singleton instance and preserves it across scene loads.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        GameSettingsRuntime.ApplyToGlobalSettings();
    }

    /// <summary>
    /// Clears the singleton reference when this settings object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Clamps editable values and restores missing inline settings containers.
    /// </summary>
    private void OnValidate()
    {
        shotNoiseDuration = Mathf.Max(0f, shotNoiseDuration);
        equipNoiseDuration = Mathf.Max(0f, equipNoiseDuration);
        holsterNoiseDuration = Mathf.Max(0f, holsterNoiseDuration);
        incapacitatedWakeUpDelay = Mathf.Max(0f, incapacitatedWakeUpDelay);
        sleepWakeActionDelay = Mathf.Max(0f, sleepWakeActionDelay);
        ValidateAngleRange(ref leftLookaroundMinAngle, ref leftLookaroundMaxAngle);
        ValidateAngleRange(ref rightLookaroundMinAngle, ref rightLookaroundMaxAngle);
        ValidateAngleRange(ref upLookaroundMinAngle, ref upLookaroundMaxAngle);
        ValidateAngleRange(ref downLookaroundMinAngle, ref downLookaroundMaxAngle);
        dragSlowPercentage = Mathf.Clamp(dragSlowPercentage, 0f, 100f);
        equipmentContextUi ??= new EquipmentContextUiSettings();
        hudUi ??= new HudUiSettings();
        missionFailurePresentation ??= new MissionFailurePresentationSettings();
    }

    /// <summary>
    /// Resolves the configured angle range for one light-switch lookaround preset.
    /// </summary>
    public void GetLightSwitchLookaroundAngles(LightSwitchLookaroundPreset preset, out float minAngle, out float maxAngle)
    {
        switch (preset)
        {
            case LightSwitchLookaroundPreset.LeftLookaround:
                minAngle = LeftLookaroundMinAngle;
                maxAngle = LeftLookaroundMaxAngle;
                break;

            case LightSwitchLookaroundPreset.UpLookaround:
                minAngle = UpLookaroundMinAngle;
                maxAngle = UpLookaroundMaxAngle;
                break;

            case LightSwitchLookaroundPreset.DownLookaround:
                minAngle = DownLookaroundMinAngle;
                maxAngle = DownLookaroundMaxAngle;
                break;

            default:
                minAngle = RightLookaroundMinAngle;
                maxAngle = RightLookaroundMaxAngle;
                break;
        }
    }

    /// <summary>
    /// Keeps one authored minimum and maximum angle ordered correctly.
    /// </summary>
    private static void ValidateAngleRange(ref float minAngle, ref float maxAngle)
    {
        if (maxAngle < minAngle)
            maxAngle = minAngle;
    }

    /// <summary>
    /// Toggles sprint input between hold and toggle modes for quick testing.
    /// </summary>
    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void ToggleSprintMode()
    {
        SetSprintToggleEnabled(!sprintToggleEnabled);
    }

    /// <summary>
    /// Toggles focus input between hold and toggle modes for quick testing.
    /// </summary>
    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void ToggleFocusMode()
    {
        SetFocusToggleEnabled(!focusToggleEnabled);
    }

    /// <summary>
    /// Updates the configured sprint input mode and broadcasts the change when needed.
    /// </summary>
    public void SetSprintToggleEnabled(bool enabled)
    {
        if (sprintToggleEnabled == enabled)
            return;

        sprintToggleEnabled = enabled;
        SettingsChanged?.Invoke();
    }

    /// <summary>
    /// Updates the configured focus input mode and broadcasts the change when needed.
    /// </summary>
    public void SetFocusToggleEnabled(bool enabled)
    {
        if (focusToggleEnabled == enabled)
            return;

        focusToggleEnabled = enabled;
        SettingsChanged?.Invoke();
    }

    /// <summary>
    /// Updates the configured drag interaction mode and broadcasts the change when needed.
    /// </summary>
    public void SetDragRequiresHoldInput(bool requiresHold)
    {
        if (dragRequiresHoldInput == requiresHold)
            return;

        dragRequiresHoldInput = requiresHold;
        SettingsChanged?.Invoke();
    }

    /// <summary>
    /// Applies persisted player preferences that depend on designer-wired global assets.
    /// </summary>
    public void ApplyPlayerSettings(GameSettingsSaveData settings)
    {
        if (settings == null)
            return;

        SetSprintToggleEnabled(settings.ToggleSprint);
        SetDragRequiresHoldInput(!settings.ToggleDragBody);
        ApplyMixerVolume(masterVolumeParameter, settings.MasterVolume);
        ApplyMixerVolume(musicVolumeParameter, settings.MusicVolume);
        ApplyMixerVolume(sfxVolumeParameter, settings.SfxVolume);
        ApplyMixerVolume(uiVolumeParameter, settings.UiVolume);
        ApplyMixerVolume(ambientVolumeParameter, settings.AmbientVolume);
    }

    /// <summary>
    /// Applies one zero-to-one-hundred volume value to an exposed mixer parameter.
    /// </summary>
    private void ApplyMixerVolume(string parameterName, float percentage)
    {
        if (settingsAudioMixer == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        settingsAudioMixer.SetFloat(parameterName.Trim(), GameSettingsRuntime.VolumePercentToDecibels(percentage));
    }
}
