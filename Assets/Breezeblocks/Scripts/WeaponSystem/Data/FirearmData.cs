using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public enum FirearmClass
{
    Pistol,
    Revolver,
    SMG,
    Shotgun,
    PumpShotgun,
    SemiAutoShotgun,
    Rifle,
    AssaultRifle,
    Carbine,
    SniperRifle
}

public enum ReloadType
{
    Magazine,
    BulletPerBullet
}

[Flags]
public enum FireMode
{
    None = 0,
    SemiAuto = 1 << 0,
    FullAuto = 1 << 1,
    Burst = 1 << 2,
    Pump = 1 << 3,
    BoltAction = 1 << 4
}

public enum FirearmGripType
{
    OneHanded,
    TwoHanded
}

[CreateAssetMenu(fileName = "FirearmData", menuName = "Breezeblocks/Weapons/Firearm Data")]
public class FirearmData : EquipmentItemData
{
    [FoldoutGroup("Firearm/Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite heldVisualSprite;

    [FoldoutGroup("Firearm")]
    [FoldoutGroup("Firearm/Classification")]
    [SerializeField] private FirearmClass firearmClass;

    [FoldoutGroup("Firearm/Loadout"), EnumToggleButtons]
    [SerializeField] private EquipmentSlotMask allowedSlots = EquipmentSlotMask.Primary;

    [FoldoutGroup("Firearm/Handling"), EnumToggleButtons]
    [SerializeField] private FirearmGripType gripType = FirearmGripType.OneHanded;

    [FoldoutGroup("Firearm/Visuals"), ShowIf(nameof(UsesTwoHandedGrip)), MinValue(0f)]
    [LabelText("Front Hand Forward Offset")]
    [Tooltip("How far the support hand is pushed forward when the firearm uses a two-handed grip.")]
    [SerializeField] private float twoHandedFrontHandForwardOffset = 0.14f;

    [FoldoutGroup("Firearm/Performance"), MinValue(0f)]
    [SerializeField] private float fireRate = 1f;

    [FoldoutGroup("Firearm/Performance"), MinValue(0f)]
    [SerializeField] private float spread;

    [FoldoutGroup("Firearm/Performance"), Range(0f, 100f), SuffixLabel("%", true)]
    [SerializeField] private float accuracy = 100f;

    [FoldoutGroup("Firearm/Reload"), MinValue(0f)]
    [SerializeField] private float reloadTime = 1f;

    [FoldoutGroup("Firearm/Reload")]
    [SerializeField] private ReloadType reloadType = ReloadType.Magazine;

    [FoldoutGroup("Firearm/Reload"), MinValue(1)]
    [SerializeField] private int ammoCapacity = 6;

    [FoldoutGroup("Firearm/Reload"), MinValue(0)]
    [SerializeField] private int defaultReserveAmmo = 24;

    [FoldoutGroup("Firearm/Reload"), MinValue(0f)]
    [SerializeField] private float reloadNoiseDuration = 0.2f;

    [FoldoutGroup("Firearm/Reload"), ShowIf(nameof(UsesMagazineReload)), Range(0f, 1f)]
    [LabelText("Mid Reload SFX Time")]
    [Tooltip("Normalized moment during a magazine reload when the reload-end and trigger SFX should play.")]
    [SerializeField] private float magazineReloadMidSfxNormalizedTime = 0.5f;

    [FoldoutGroup("Firearm/Compatibility")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<ProjectileData> compatibleProjectiles = new();

    [FoldoutGroup("Firearm/Handling"), EnumToggleButtons]
    [SerializeField] private FireMode fireMode = FireMode.SemiAuto;

    [FoldoutGroup("Firearm/Handling"), ShowIf(nameof(UsesBurstMode)), MinValue(1)]
    [SerializeField] private int burstCount = 3;

    [FoldoutGroup("Firearm/Handling"), ShowIf(nameof(UsesPumpMode)), MinValue(1)]
    [SerializeField] private int pelletCount = 8;

    [FoldoutGroup("Firearm/Aiming"), MinValue(0f)]
    [SerializeField] private float aimSpeed = 720f;

    [FoldoutGroup("Firearm/Aiming"), MinValue(0f)]
    [SerializeField] private float aimPanDistance = 0f;

    [FoldoutGroup("Firearm/Aiming"), MinValue(0f)]
    [SerializeField] private float aimTime = 1f;

    [FoldoutGroup("Firearm/Handling"), MinValue(0f)]
    [SerializeField] private float equipTime = 0.25f;

    [FoldoutGroup("Firearm/Handling"), MinValue(0f)]
    [SerializeField] private float holsterTime = 0.25f;

    [FoldoutGroup("Firearm/Feedback"), MinValue(0f)]
    [SerializeField] private float screenshakePower;

    [FoldoutGroup("Firearm/Feedback"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float screenshakeDuration = 0.08f;

    [FoldoutGroup("Firearm/Feedback")]
    [SerializeField] private bool hideMuzzleFlash;

    [FoldoutGroup("Firearm/Feedback"), MinValue(0f)]
    [ShowIf(nameof(ShowsMuzzleFlash))]
    [SerializeField] private float muzzleFlashSize = 1f;

    [FoldoutGroup("Firearm/Feedback"), MinValue(0f)]
    [ShowIf(nameof(ShowsMuzzleFlash))]
    [SerializeField] private float muzzleFlashDuration = 0.1f;

    [FoldoutGroup("Firearm/SFX"), LabelText("Shot SFX"), InlineProperty]
    [SerializeField] private AudioClipSet shotSfx = new();

    [FoldoutGroup("Firearm/SFX"), LabelText("Casing Delay"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float casingDelay = 0.05f;

    [FoldoutGroup("Firearm/SFX"), LabelText("Casing SFX"), InlineProperty]
    [SerializeField] private AudioClipSet casingSfx = new();

    [FoldoutGroup("Firearm/SFX"), ShowIf(nameof(UsesMagazineReload)), LabelText("Reload Start SFX"), InlineProperty]
    [SerializeField] private AudioClipSet reloadStartSfx = new();

    [FoldoutGroup("Firearm/SFX"), ShowIf(nameof(UsesMagazineReload)), LabelText("Reload End SFX"), InlineProperty]
    [SerializeField] private AudioClipSet reloadEndSfx = new();

    [FoldoutGroup("Firearm/SFX"), ShowIf(nameof(UsesMagazineReload)), LabelText("Reload Trigger SFX"), InlineProperty]
    [SerializeField] private AudioClipSet reloadTriggerSfx = new();

    [FoldoutGroup("Firearm/SFX"), ShowIf(nameof(UsesBulletReload)), LabelText("Bullet Reload SFX"), InlineProperty]
    [SerializeField] private AudioClipSet bulletReloadSfx = new();

    [FoldoutGroup("Firearm/Noise"), MinValue(0f)]
    [SerializeField] private float shootNoise = 1f;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private NoiseType shootNoiseType = NoiseType.Loud;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private bool shootExtremeNoise = true;

    [FoldoutGroup("Firearm/Noise"), MinValue(0f)]
    [SerializeField] private float equipNoise = 0.25f;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private NoiseType equipNoiseType = NoiseType.Common;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private bool equipExtremeNoise;

    [FoldoutGroup("Firearm/Noise"), MinValue(0f)]
    [SerializeField] private float holsterNoise = 0.25f;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private NoiseType holsterNoiseType = NoiseType.Common;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private bool holsterExtremeNoise;

    [FoldoutGroup("Firearm/Noise"), MinValue(0f)]
    [SerializeField] private float reloadNoise = 0.5f;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private NoiseType reloadNoiseType = NoiseType.Common;

    [FoldoutGroup("Firearm/Noise")]
    [SerializeField] private bool reloadExtremeNoise;

    public override EquipmentItemKind ItemKind => EquipmentItemKind.Firearm;
    public override EquipmentSlotMask AllowedSlots => allowedSlots & EquipmentSlotMask.HandSlots;
    public override Sprite HeldVisualSprite => heldVisualSprite != null ? heldVisualSprite : Icon;
    public FirearmClass Class => firearmClass;
    public FirearmGripType GripType => gripType;
    public float TwoHandedFrontHandForwardOffset => twoHandedFrontHandForwardOffset;
    public float FireRate => fireRate;
    public float Spread => spread;
    public float Accuracy => accuracy;
    public float ReloadTime => reloadTime;
    public ReloadType ReloadStyle => reloadType;
    public int AmmoCapacity => ammoCapacity;
    public int DefaultReserveAmmo => defaultReserveAmmo;
    public float ReloadNoiseDuration => reloadNoiseDuration;
    public float MagazineReloadMidSfxNormalizedTime => magazineReloadMidSfxNormalizedTime;
    public IReadOnlyList<ProjectileData> CompatibleProjectiles => compatibleProjectiles;
    public FireMode Modes => fireMode;
    public int BurstCount => burstCount;
    public int PelletCount => pelletCount;
    public float AimSpeed => aimSpeed;
    public float AimPanDistance => aimPanDistance;
    public float AimTime => aimTime;
    public float EquipTime => equipTime;
    public float HolsterTime => holsterTime;
    public float ScreenshakePower => screenshakePower;
    public float ScreenshakeDuration => screenshakeDuration;
    public bool HideMuzzleFlash => hideMuzzleFlash;
    public float MuzzleFlashSize => muzzleFlashSize;
    public float MuzzleFlashDuration => muzzleFlashDuration;
    public AudioClipSet ShotSfx => shotSfx;
    public float CasingDelay => casingDelay;
    public AudioClipSet CasingSfx => casingSfx;
    public AudioClipSet ReloadStartSfx => reloadStartSfx;
    public AudioClipSet ReloadEndSfx => reloadEndSfx;
    public AudioClipSet ReloadTriggerSfx => reloadTriggerSfx;
    public AudioClipSet BulletReloadSfx => bulletReloadSfx;
    public float ShootNoise => shootNoise;
    public NoiseType ShootNoiseType => shootNoiseType;
    public bool ShootExtremeNoise => shootExtremeNoise;
    public float EquipNoise => equipNoise;
    public NoiseType EquipNoiseType => equipNoiseType;
    public bool EquipExtremeNoise => equipExtremeNoise;
    public float HolsterNoise => holsterNoise;
    public NoiseType HolsterNoiseType => holsterNoiseType;
    public bool HolsterExtremeNoise => holsterExtremeNoise;
    public float ReloadNoise => reloadNoise;
    public NoiseType ReloadNoiseType => reloadNoiseType;
    public bool ReloadExtremeNoise => reloadExtremeNoise;

    public bool SupportsFireMode(FireMode mode)
    {
        return (fireMode & mode) != 0;
    }

    public bool SupportsProjectile(ProjectileData projectile)
    {
        return projectile != null && compatibleProjectiles.Contains(projectile);
    }

    private bool UsesBurstMode => SupportsFireMode(FireMode.Burst);
    private bool UsesPumpMode => SupportsFireMode(FireMode.Pump);
    private bool UsesMagazineReload => reloadType == ReloadType.Magazine;
    private bool UsesBulletReload => reloadType == ReloadType.BulletPerBullet;
    private bool UsesTwoHandedGrip => gripType == FirearmGripType.TwoHanded;
    private bool ShowsMuzzleFlash => !hideMuzzleFlash;

    private void OnValidate()
    {
        ValidateCommonItemFields();
        allowedSlots &= EquipmentSlotMask.HandSlots;
        if (allowedSlots == EquipmentSlotMask.None)
            allowedSlots = EquipmentSlotMask.Primary;

        fireRate = Mathf.Max(0f, fireRate);
        twoHandedFrontHandForwardOffset = Mathf.Max(0f, twoHandedFrontHandForwardOffset);
        spread = Mathf.Max(0f, spread);
        accuracy = Mathf.Clamp(accuracy, 0f, 100f);
        reloadTime = Mathf.Max(0f, reloadTime);
        ammoCapacity = Mathf.Max(1, ammoCapacity);
        defaultReserveAmmo = Mathf.Max(0, defaultReserveAmmo);
        reloadNoiseDuration = Mathf.Max(0f, reloadNoiseDuration);
        magazineReloadMidSfxNormalizedTime = Mathf.Clamp01(magazineReloadMidSfxNormalizedTime);
        aimSpeed = Mathf.Max(0f, aimSpeed);
        aimPanDistance = Mathf.Max(0f, aimPanDistance);
        aimTime = Mathf.Max(0f, aimTime);
        equipTime = Mathf.Max(0f, equipTime);
        holsterTime = Mathf.Max(0f, holsterTime);
        screenshakePower = Mathf.Max(0f, screenshakePower);
        screenshakeDuration = Mathf.Max(0f, screenshakeDuration);
        muzzleFlashSize = Mathf.Max(0f, muzzleFlashSize);
        muzzleFlashDuration = Mathf.Max(0f, muzzleFlashDuration);
        casingDelay = Mathf.Max(0f, casingDelay);
        shootNoise = Mathf.Max(0f, shootNoise);
        equipNoise = Mathf.Max(0f, equipNoise);
        holsterNoise = Mathf.Max(0f, holsterNoise);
        reloadNoise = Mathf.Max(0f, reloadNoise);

        compatibleProjectiles ??= new List<ProjectileData>();
        shotSfx ??= new AudioClipSet();
        casingSfx ??= new AudioClipSet();
        reloadStartSfx ??= new AudioClipSet();
        reloadEndSfx ??= new AudioClipSet();
        reloadTriggerSfx ??= new AudioClipSet();
        bulletReloadSfx ??= new AudioClipSet();

        shotSfx.Validate();
        casingSfx.Validate();
        reloadStartSfx.Validate();
        reloadEndSfx.Validate();
        reloadTriggerSfx.Validate();
        bulletReloadSfx.Validate();

        burstCount = UsesBurstMode ? Mathf.Max(1, burstCount) : 0;
        pelletCount = UsesPumpMode ? Mathf.Max(1, pelletCount) : 0;
    }
}
}
