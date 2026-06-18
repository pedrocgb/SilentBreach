using System;
using System.Collections;
using System.Collections.Generic;
using Breezeblocks.Input;
using Breezeblocks.HideoutSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Player Weapon Controller")]
[RequireComponent(typeof(PlayerTopDownMotor2D))]
public class PlayerWeaponController : MonoBehaviour
{
    private static readonly FireMode[] FireModeCycleOrder =
    {
        FireMode.SemiAuto,
        FireMode.FullAuto,
        FireMode.Burst,
        FireMode.Pump,
        FireMode.BoltAction
    };

    private const float MinDirectionSqr = 0.0001f;

    private int rewiredPlayerId = 1;
    private string aimAction = "Aim";
    private string fireAction = "Fire";
    private string reloadAction = "Reload";
    private string cycleFireModeAction = "Cycle Fire Mode";

    [FoldoutGroup("References"), Tooltip("Optional fire origin. Defaults to this transform if left empty.")]
    [SerializeField] private Transform firePoint;

    [FoldoutGroup("References"), Tooltip("Optional stable origin used to resolve mouse aim direction. Defaults to the vision pivot, then this transform.")]
    [SerializeField] private Transform aimOrigin;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerNoise playerNoise;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerVisibility playerVisibility;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerAimCamera2D aimCamera;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ArmorLoadout armorLoadout;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorStaggerController actorStaggerController;

    private GlobalObjectPooler globalObjectPooler;
    private WorldSfxManager worldSfxManager;
    private HitscanProjectile projectilePrefab;
    private int projectilePoolPrewarm = 16;
    private MuzzleFlashEffect muzzleFlashPrefab;
    private int muzzleFlashPoolPrewarm = 8;
    private float lookRotationSpeed = 720f;
    private float stationarySpeedThreshold = 0.05f;
    private float debugTraceDuration = 0.1f;
    private float muzzleFlashRotationOffset;
    private bool autoEquipDebugWeaponOnStart;
    private FirearmData debugFirearm;
    private ProjectileData debugProjectile;
    private int debugStartingLoadedAmmo = -1;
    private int debugStartingReserveAmmo = -1;
    private int debugReserveAmmoAddAmount = 12;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public FirearmData EquippedFirearm { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public ProjectileData CurrentProjectile { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public FireMode CurrentFireMode { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAiming { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAccurate { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsReloading => _reloadRoutine != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsBusy => _weaponRoutine != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentAmmo => firearmRuntimeState.LoadedAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentLoadedAmmo => firearmRuntimeState.LoadedAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentReserveAmmo => firearmRuntimeState.ReserveAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentAmmoCapacity => EquippedFirearm != null ? EquippedFirearm.AmmoCapacity : 0;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasReserveAmmo => GameplayConsoleCheatState.InfiniteReserveAmmo || firearmRuntimeState.ReserveAmmo > 0;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentAimDirection { get; private set; } = Vector2.right;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float AccurateAimTimer => _accurateAimTimer;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float CurrentAccuracyProgress01 => ResolveCurrentAccuracyProgress01();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentSpreadAngle => ResolveCurrentSpreadAngle();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public List<FireMode> AvailableFireModes => _availableFireModes;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    public event Action WeaponStateChanged;
    public event Action WeaponFired;

    private IPlayerInputReader inputReader;
    private IPointerInputReader pointerInputReader;
    private Camera _mainCamera;
    private Coroutine _reloadRoutine;
    private Coroutine _weaponRoutine;
    private float _accurateAimTimer;
    private float _nextAllowedFireTime;
    private readonly FirearmRuntimeState firearmRuntimeState = new();
    private readonly List<FireMode> _availableFireModes = new();
    private PlayerPerkModifierSet perkModifiers = new();
    private bool inputBlocked;

    // Executes the Reset routine.
    private void Reset()
    {
        playerMotor = GetComponent<PlayerTopDownMotor2D>();
        playerNoise = GetComponent<PlayerNoise>();
        playerVisibility = GetComponent<PlayerVisibility>();
        armorLoadout = GetComponent<ArmorLoadout>();
        playerVisionLight = GetComponentInChildren<PlayerVisionLight>();
        firePoint = transform;
        aimOrigin = playerVisionLight != null ? playerVisionLight.transform : transform;
        actorStaggerController = GetComponent<ActorStaggerController>();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (playerNoise == null)
            playerNoise = GetComponent<PlayerNoise>();

        if (playerVisibility == null)
            playerVisibility = GetComponent<PlayerVisibility>();

        if (armorLoadout == null)
            armorLoadout = GetComponent<ArmorLoadout>();

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>();

        if (firePoint == null)
            firePoint = transform;

        if (aimOrigin == null)
            aimOrigin = playerVisionLight != null ? playerVisionLight.transform : transform;

        aimCamera = WeaponRuntimeUtility.ResolveAimCamera(aimCamera, gameObject);

        ResolveGlobalObjectPooler();
        ResolveWorldSfxManager();
        RegisterPooledPrefabs();

        if (aimCamera != null)
            aimCamera.SetFollowTarget(transform);

        WeaponRuntimeUtility.EnsureCombinedInputReaders(ref inputReader, ref pointerInputReader, rewiredPlayerId);
    }

    // Executes the Start routine.
    private void Start()
    {
        if (autoEquipDebugWeaponOnStart && debugFirearm != null && GetComponent<PlayerEquipmentController>() == null)
            EquipWeapon(debugFirearm, debugProjectile, debugStartingLoadedAmmo, debugStartingReserveAmmo);
    }

    // Executes the OnValidate routine.
    private void OnValidate()
    {
        lookRotationSpeed = Mathf.Max(0f, lookRotationSpeed);
        stationarySpeedThreshold = Mathf.Max(0f, stationarySpeedThreshold);
        debugTraceDuration = Mathf.Max(0f, debugTraceDuration);
        projectilePoolPrewarm = Mathf.Max(0, projectilePoolPrewarm);
        muzzleFlashPoolPrewarm = Mathf.Max(0, muzzleFlashPoolPrewarm);
        debugReserveAmmoAddAmount = Mathf.Max(0, debugReserveAmmoAddAmount);
    }

    /// <summary>
    /// Applies profile-authored Rewired player id and firearm action names.
    /// </summary>
    public void ApplyControls(PlayerControlsSettings settings)
    {
        if (settings == null)
            return;

        rewiredPlayerId = Mathf.Max(0, settings.RewiredPlayerId);
        aimAction = settings.AimAction;
        fireAction = settings.FireAction;
        reloadAction = settings.ReloadAction;
        cycleFireModeAction = settings.CycleFireModeAction;
        inputReader = null;
        pointerInputReader = null;
        WeaponRuntimeUtility.EnsureCombinedInputReaders(ref inputReader, ref pointerInputReader, rewiredPlayerId);
    }

    /// <summary>
    /// Applies profile-authored firearm controller tuning, pooling, and debug loadout values.
    /// </summary>
    public void ApplySettings(PlayerWeaponControllerSettings settings)
    {
        if (settings == null)
            return;

        projectilePrefab = settings.ProjectilePrefab;
        projectilePoolPrewarm = Mathf.Max(0, settings.ProjectilePoolPrewarm);
        muzzleFlashPrefab = settings.MuzzleFlashPrefab;
        muzzleFlashPoolPrewarm = Mathf.Max(0, settings.MuzzleFlashPoolPrewarm);
        lookRotationSpeed = Mathf.Max(0f, settings.LookRotationSpeed);
        stationarySpeedThreshold = Mathf.Max(0f, settings.StationarySpeedThreshold);
        debugTraceDuration = Mathf.Max(0f, settings.DebugTraceDuration);
        muzzleFlashRotationOffset = settings.MuzzleFlashRotationOffset;
        autoEquipDebugWeaponOnStart = settings.AutoEquipDebugWeaponOnStart;
        debugFirearm = settings.DebugFirearm;
        debugProjectile = settings.DebugProjectile;
        debugStartingLoadedAmmo = Mathf.Max(-1, settings.DebugStartingLoadedAmmo);
        debugStartingReserveAmmo = Mathf.Max(-1, settings.DebugStartingReserveAmmo);
        debugReserveAmmoAddAmount = Mathf.Max(0, settings.DebugReserveAmmoAddAmount);
        RegisterPooledPrefabs();
    }

    // Executes the Update routine.
    private void Update()
    {
        WeaponRuntimeUtility.EnsureCombinedInputReaders(ref inputReader, ref pointerInputReader, rewiredPlayerId);

        if (!inputReader.IsReady)
            return;

        if (inputBlocked)
        {
            ResetAimRuntimeState();
            return;
        }

        if (EquippedFirearm == null)
        {
            ResetAimRuntimeState(notifyCameraState: false);
            return;
        }

        UpdateAimState();
        UpdateAimDirection();
        UpdateAccurateMode();
        UpdateAimCameraState();
        HandleFireModeInput();
        HandleReloadInput();
        HandleFireInput();
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug Actions")]
    // Executes the DebugEquipSelectedWeapon routine.
    public void DebugEquipSelectedWeapon()
    {
        EquipWeapon(debugFirearm, debugProjectile, debugStartingLoadedAmmo, debugStartingReserveAmmo);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug Actions")]
    // Executes the DebugAddReserveAmmo routine.
    public void DebugAddReserveAmmo()
    {
        AddReserveAmmo(debugReserveAmmoAddAmount);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug Actions")]
    // Executes the DebugHolsterWeapon routine.
    public void DebugHolsterWeapon()
    {
        HolsterWeapon();
    }

    // Executes the EquipWeapon routine.
    public void EquipWeapon(FirearmData firearm, ProjectileData requestedProjectile, int startingLoadedAmmo = -1, int startingReserveAmmo = -1)
    {
        if (firearm == null || IsBusy)
            return;

        if (IsReloading && (EquippedFirearm == null || EquippedFirearm.ReloadStyle == ReloadType.Magazine))
            return;

        CancelBulletPerBulletReload();

        ProjectileData resolvedProjectile = firearm.SupportsProjectile(requestedProjectile)
            ? requestedProjectile
            : firearm.CompatibleProjectiles.Count > 0 ? firearm.CompatibleProjectiles[0] : null;

        if (resolvedProjectile == null)
            return;

        _weaponRoutine = StartCoroutine(EquipWeaponRoutine(firearm, resolvedProjectile, startingLoadedAmmo, startingReserveAmmo));
    }

    // Executes the AddReserveAmmo routine.
    public bool AddReserveAmmo(int amount)
    {
        if (amount <= 0 || EquippedFirearm == null)
            return false;

        if (!firearmRuntimeState.AddReserveAmmo(amount))
            return false;

        EnsureConsoleAmmoReserveBuffer();
        NotifyWeaponStateChanged();
        return true;
    }

    // Executes the EnsureConsoleAmmoReserveBuffer routine.
    public void EnsureConsoleAmmoReserveBuffer()
    {
        if (!GameplayConsoleCheatState.InfiniteReserveAmmo || EquippedFirearm == null)
            return;

        if (!firearmRuntimeState.EnsureReserveBuffer(EquippedFirearm, GameplayConsoleCheatState.InfiniteReserveAmmo))
            return;

        NotifyWeaponStateChanged();
    }

    // Executes the HolsterWeapon routine.
    public void HolsterWeapon()
    {
        if (EquippedFirearm == null || IsBusy)
            return;

        if (IsReloading && EquippedFirearm.ReloadStyle == ReloadType.Magazine)
            return;

        CancelBulletPerBulletReload();
        _weaponRoutine = StartCoroutine(HolsterWeaponRoutine());
    }

    // Executes the SetInputBlocked routine.
    public void SetInputBlocked(bool blocked)
    {
        if (inputBlocked == blocked)
            return;

        inputBlocked = blocked;
        if (blocked)
            ResetAimRuntimeState();
    }

    // Executes the EquipWeaponRoutine routine.
    private IEnumerator EquipWeaponRoutine(FirearmData firearm, ProjectileData projectile, int startingLoadedAmmo, int startingReserveAmmo)
    {
        if (EquippedFirearm != null)
            yield return HolsterCurrentWeaponInternal();

        yield return new WaitForSeconds(firearm.EquipTime);

        EquippedFirearm = firearm;
        CurrentProjectile = projectile;
        firearmRuntimeState.Initialize(firearm, startingLoadedAmmo, startingReserveAmmo, GameplayConsoleCheatState.InfiniteReserveAmmo);
        EnsureConsoleAmmoReserveBuffer();
        RebuildAvailableFireModes();
        CurrentAimDirection = ResolveEquipAimDirection();
        EmitNoiseSpike(firearm.EquipNoise, GlobalSettings.Instance != null ? GlobalSettings.Instance.EquipNoiseDuration : 0.4f, firearm.EquipNoiseType, firearm.EquipExtremeNoise);
        NotifyWeaponStateChanged();

        _weaponRoutine = null;
    }

    // Executes the HolsterWeaponRoutine routine.
    private IEnumerator HolsterWeaponRoutine()
    {
        yield return HolsterCurrentWeaponInternal();
        _weaponRoutine = null;
    }

    // Executes the HolsterCurrentWeaponInternal routine.
    private IEnumerator HolsterCurrentWeaponInternal()
    {
        FirearmData weaponBeingHolstered = EquippedFirearm;
        if (weaponBeingHolstered == null)
            yield break;

        ResetAimRuntimeState(notifyCameraState: false, notifyStateChanged: false);

        yield return new WaitForSeconds(weaponBeingHolstered.HolsterTime);

        EmitNoiseSpike(weaponBeingHolstered.HolsterNoise, GlobalSettings.Instance != null ? GlobalSettings.Instance.HolsterNoiseDuration : 0.6f, weaponBeingHolstered.HolsterNoiseType, weaponBeingHolstered.HolsterExtremeNoise);
        ClearEquippedWeaponState();
        NotifyWeaponStateChanged();
    }

    // Executes the HandleFireModeInput routine.
    private void HandleFireModeInput()
    {
        if (EquippedFirearm == null || _availableFireModes.Count <= 1 || !inputReader.GetButtonDown(cycleFireModeAction))
            return;

        int currentIndex = _availableFireModes.IndexOf(CurrentFireMode);
        if (currentIndex < 0)
            currentIndex = 0;

        currentIndex = (currentIndex + 1) % _availableFireModes.Count;
        CurrentFireMode = _availableFireModes[currentIndex];
        NotifyWeaponStateChanged();
    }

    /// <summary>
    /// Starts reload when current firearm has capacity, reserve ammo, and reload input is pressed.
    /// </summary>
    private void HandleReloadInput()
    {
        if (EquippedFirearm == null || !inputReader.GetButtonDown(reloadAction))
            return;

        if (IsReloading || CurrentProjectile == null)
            return;

        if (CurrentLoadedAmmo >= CurrentAmmoCapacity || !HasReserveAmmo)
            return;

        if (EquippedFirearm.ReloadStyle == ReloadType.Magazine)
        {
            _reloadRoutine = StartCoroutine(MagazineReloadRoutine());
            NotifyWeaponStateChanged();
            return;
        }

        _reloadRoutine = StartCoroutine(BulletPerBulletReloadRoutine());
        NotifyWeaponStateChanged();
    }

    // Executes the MagazineReloadRoutine routine.
    private IEnumerator MagazineReloadRoutine()
    {
        PlayMagazineReloadStartSfx();
        EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType, EquippedFirearm.ReloadExtremeNoise);
        float reloadDuration = ResolveCurrentReloadDuration();
        float midReloadSfxDelay = reloadDuration * EquippedFirearm.MagazineReloadMidSfxNormalizedTime;
        float remainingReloadDelay = Mathf.Max(0f, reloadDuration - midReloadSfxDelay);

        if (midReloadSfxDelay > 0f)
            yield return new WaitForSeconds(midReloadSfxDelay);

        PlayMagazineReloadEndSequenceSfx();

        if (remainingReloadDelay > 0f)
            yield return new WaitForSeconds(remainingReloadDelay);

        int roundsToTransfer = firearmRuntimeState.TransferMagazineRounds(CurrentAmmoCapacity, GameplayConsoleCheatState.InfiniteReserveAmmo);
        if (roundsToTransfer > 0)
        {
            EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType, EquippedFirearm.ReloadExtremeNoise);
        }

        _reloadRoutine = null;
        NotifyWeaponStateChanged();
    }

    // Executes the BulletPerBulletReloadRoutine routine.
    private IEnumerator BulletPerBulletReloadRoutine()
    {
        EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType, EquippedFirearm.ReloadExtremeNoise);
        yield return new WaitForSeconds(ResolveCurrentReloadDuration());

        if (EquippedFirearm != null &&
            CurrentProjectile != null &&
            firearmRuntimeState.TryLoadSingleRound(CurrentAmmoCapacity, GameplayConsoleCheatState.InfiniteReserveAmmo))
        {
            PlayBulletReloadSfx();
            EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType, EquippedFirearm.ReloadExtremeNoise);
            NotifyWeaponStateChanged();
        }

        _reloadRoutine = null;
        NotifyWeaponStateChanged();
    }

    // Executes the CancelBulletPerBulletReload routine.
    private void CancelBulletPerBulletReload()
    {
        if (_reloadRoutine == null || EquippedFirearm == null || EquippedFirearm.ReloadStyle != ReloadType.BulletPerBullet)
            return;

        StopCoroutine(_reloadRoutine);
        _reloadRoutine = null;
        NotifyWeaponStateChanged();
    }

    // Executes the HandleFireInput routine.
    private void HandleFireInput()
    {
        if (EquippedFirearm == null || CurrentProjectile == null || CurrentFireMode == FireMode.None)
            return;

        bool fireRequested = CurrentFireMode == FireMode.FullAuto
            ? inputReader.GetButton(fireAction)
            : inputReader.GetButtonDown(fireAction);

        if (!fireRequested)
            return;

        if (EquippedFirearm.ReloadStyle == ReloadType.BulletPerBullet && IsReloading)
            CancelBulletPerBulletReload();

        if (!CanFireRequestedShot())
            return;

        FireCurrentMode();
    }

    // Executes the FireCurrentMode routine.
    private void FireCurrentMode()
    {
        switch (CurrentFireMode)
        {
            case FireMode.Burst:
                FireBurst();
                break;

            case FireMode.Pump:
                FirePumpShot();
                break;

            default:
                FireSingleRound();
                break;
        }

        _nextAllowedFireTime = Time.time + (EquippedFirearm.FireRate > 0f ? 1f / EquippedFirearm.FireRate : 0f);
    }

    // Executes the FireBurst routine.
    private void FireBurst()
    {
        FireRounds(Mathf.Max(1, EquippedFirearm.BurstCount), 1);
    }

    // Executes the FirePumpShot routine.
    private void FirePumpShot()
    {
        FireRounds(1, Mathf.Max(1, EquippedFirearm.PelletCount));
    }

    // Executes the FireSingleRound routine.
    private void FireSingleRound()
    {
        FireRounds(1, 1);
    }

    // Executes the FireRounds routine.
    private void FireRounds(int roundsToConsume, int projectileCount)
    {
        int resolvedRounds = Mathf.Max(1, roundsToConsume);
        int resolvedProjectileCount = Mathf.Max(1, projectileCount);
        for (int i = 0; i < resolvedRounds; i++)
        {
            if (!TryConsumeCurrentRound(out ProjectileData projectile))
                break;

            SpawnProjectile(projectile, resolvedProjectileCount);
            ConsumeAccurateStanceAfterShot();
        }
    }

    // Executes the TryConsumeCurrentRound routine.
    private bool TryConsumeCurrentRound(out ProjectileData projectile)
    {
        projectile = CurrentProjectile;
        if (EquippedFirearm == null || CurrentProjectile == null || !firearmRuntimeState.TryConsumeRound())
            return false;

        EmitNoiseSpike(EquippedFirearm.ShootNoise, GlobalSettings.Instance != null ? GlobalSettings.Instance.ShotNoiseDuration : 0.1f, EquippedFirearm.ShootNoiseType, EquippedFirearm.ShootExtremeNoise);
        SpawnMuzzleFlash();
        ApplyShotVisibility();
        ApplyScreenshake();
        PlayShotSequenceSfx();
        NotifyWeaponStateChanged();
        WeaponFired?.Invoke();
        return true;
    }

    // Executes the SpawnProjectile routine.
    private void SpawnProjectile(ProjectileData projectile, int projectileCount)
    {
        if (projectile == null || globalObjectPooler == null || projectilePrefab == null)
            return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        for (int i = 0; i < projectileCount; i++)
        {
            HitscanProjectile hitscanProjectile = globalObjectPooler.Spawn(projectilePrefab, origin, Quaternion.identity, null, projectilePoolPrewarm);
            if (hitscanProjectile == null)
                continue;

            Vector2 shotDirection = ApplySpread(CurrentAimDirection);
            hitscanProjectile.Fire(gameObject, origin, shotDirection, projectile, debugTraceDuration);
        }
    }

    // Executes the SpawnMuzzleFlash routine.
    private void SpawnMuzzleFlash()
    {
        if (globalObjectPooler == null ||
            EquippedFirearm == null ||
            EquippedFirearm.HideMuzzleFlash ||
            muzzleFlashPrefab == null)
        return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        float angle = Mathf.Atan2(CurrentAimDirection.y, CurrentAimDirection.x) * Mathf.Rad2Deg + muzzleFlashRotationOffset;
        MuzzleFlashEffect flashEffect = globalObjectPooler.Spawn(muzzleFlashPrefab, origin, Quaternion.Euler(0f, 0f, angle), firePoint, muzzleFlashPoolPrewarm);
        if (flashEffect != null)
            flashEffect.Play(EquippedFirearm.MuzzleFlashSize, EquippedFirearm.MuzzleFlashDuration);
    }

    // Executes the ApplyShotVisibility routine.
    private void ApplyShotVisibility()
    {
        if (EquippedFirearm == null || EquippedFirearm.HideMuzzleFlash || playerVisibility == null)
            return;

        playerVisibility.ApplyMuzzleFlashVisibility();
    }

    // Executes the ApplyScreenshake routine.
    private void ApplyScreenshake()
    {
        if (EquippedFirearm == null || aimCamera == null)
            return;

        aimCamera.PlayScreenshake(EquippedFirearm.ScreenshakePower, EquippedFirearm.ScreenshakeDuration);
    }

    // Executes the ApplySpread routine.
    private Vector2 ApplySpread(Vector2 baseDirection)
    {
        if (baseDirection.sqrMagnitude <= MinDirectionSqr)
            return Vector2.right;

        float spread = ResolvePerkAdjustedSpreadAngle();
        if (IsAccurate)
            spread *= 1f - Mathf.Clamp01(EquippedFirearm.Accuracy / 100f);

        if (spread <= 0f)
            return baseDirection.normalized;

        float halfAngle = spread * 0.5f;
        float angleOffset = UnityEngine.Random.Range(-halfAngle, halfAngle);
        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float finalAngle = baseAngle + angleOffset;
        float radians = finalAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    // Executes the UpdateAimDirection routine.
    private void UpdateAimDirection()
    {
        float effectiveSpeed = GetEffectiveLookSpeed();

        if (playerVisionLight != null)
        {
            CurrentAimDirection = playerVisionLight.DriveMouseLook(effectiveSpeed, Time.deltaTime, IsAiming);
            if (CurrentAimDirection.sqrMagnitude <= MinDirectionSqr)
                CurrentAimDirection = ResolveFallbackAimDirection();

            return;
        }

        Vector2 targetDirection = ResolveMouseDirection();
        if (targetDirection.sqrMagnitude <= MinDirectionSqr)
            targetDirection = ResolveFallbackAimDirection();

        CurrentAimDirection = RotateAimDirectionTowards(CurrentAimDirection, targetDirection, effectiveSpeed, Time.deltaTime);
    }

    /// <summary>
    /// Keeps held aim state active during reload so aiming no longer cancels reload flow.
    /// </summary>
    private void UpdateAimState()
    {
        bool aimRequested = EquippedFirearm != null && !IsBusy && inputReader.GetButton(aimAction);
        IsAiming = aimRequested;
    }

    /// <summary>
    /// Builds accurate-aim state only while standing still, aiming, and not actively reloading.
    /// </summary>
    private void UpdateAccurateMode()
    {
        if (!IsAiming || EquippedFirearm == null || IsReloading || !IsStandingStill())
        {
            _accurateAimTimer = 0f;
            IsAccurate = false;
            return;
        }

        float requiredAimTime = ResolveCurrentRequiredAimTime();
        if (requiredAimTime <= 0f)
        {
            _accurateAimTimer = 0f;
            IsAccurate = true;
            return;
        }

        _accurateAimTimer += Time.deltaTime;
        IsAccurate = _accurateAimTimer >= requiredAimTime;
    }

    // Executes the UpdateAimCameraState routine.
    private void UpdateAimCameraState()
    {
        if (aimCamera == null || EquippedFirearm == null)
            return;

        aimCamera.SetFollowTarget(transform);
        aimCamera.SetAimState(IsAiming, EquippedFirearm.AimPanDistance);
    }

    // Executes the IsStandingStill routine.
    private bool IsStandingStill()
    {
        if (playerMotor == null)
            return true;

        return !playerMotor.HasMovementInput && playerMotor.CurrentPlanarSpeed <= stationarySpeedThreshold;
    }

    // Executes the GetEffectiveLookSpeed routine.
    private float GetEffectiveLookSpeed()
    {
        float speed = IsAiming && EquippedFirearm != null ? EquippedFirearm.AimSpeed : lookRotationSpeed;
        float staggerMultiplier = actorStaggerController != null ? actorStaggerController.TurnSpeedMultiplier : 1f;
        float effectiveSpeed = speed * staggerMultiplier;
        if (playerVisionLight == null && armorLoadout != null)
            effectiveSpeed *= armorLoadout.RotationSpeedMultiplier;

        return effectiveSpeed;
    }

    // Executes the ResolveMouseDirection routine.
    private Vector2 ResolveMouseDirection()
    {
        Camera camera = GetMainCamera();
        Vector3 origin = GetAimOriginPosition();

        if (camera == null)
            return CurrentAimDirection;

        Vector2 screenPosition = pointerInputReader != null
            ? pointerInputReader.GetScreenPositionOrDefault()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float depth = Mathf.Abs(camera.transform.position.z - origin.z);
        Vector3 mouseWorld = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        mouseWorld.z = origin.z;
        Vector2 direction = (Vector2)(mouseWorld - origin);

        if (direction.sqrMagnitude <= MinDirectionSqr)
            return CurrentAimDirection;

        return direction.normalized;
    }

    // Executes the GetAimOriginPosition routine.
    private Vector3 GetAimOriginPosition()
    {
        if (aimOrigin != null)
            return aimOrigin.position;

        if (playerVisionLight != null)
            return playerVisionLight.transform.position;

        return transform.position;
    }

    // Executes the ResolveFallbackAimDirection routine.
    private Vector2 ResolveFallbackAimDirection()
    {
        if (CurrentAimDirection.sqrMagnitude > MinDirectionSqr)
            return CurrentAimDirection.normalized;

        if (playerVisionLight != null && playerVisionLight.FacingDirection.sqrMagnitude > MinDirectionSqr)
            return playerVisionLight.FacingDirection;

        if (playerMotor != null && playerMotor.LastMoveDirection.sqrMagnitude > MinDirectionSqr)
            return playerMotor.LastMoveDirection.normalized;

        return Vector2.right;
    }

    // Executes the ResolveEquipAimDirection routine.
    private Vector2 ResolveEquipAimDirection()
    {
        if (playerVisionLight != null && playerVisionLight.FacingDirection.sqrMagnitude > MinDirectionSqr)
            return playerVisionLight.FacingDirection.normalized;

        if (CurrentAimDirection.sqrMagnitude > MinDirectionSqr)
            return CurrentAimDirection.normalized;

        if (playerMotor != null && playerMotor.LastMoveDirection.sqrMagnitude > MinDirectionSqr)
            return playerMotor.LastMoveDirection.normalized;

        return Vector2.right;
    }

    // Executes the RotateAimDirectionTowards routine.
    private static Vector2 RotateAimDirectionTowards(Vector2 currentDirection, Vector2 targetDirection, float speedDegreesPerSecond, float deltaTime)
    {
        if (targetDirection.sqrMagnitude <= MinDirectionSqr)
            return currentDirection.sqrMagnitude > MinDirectionSqr ? currentDirection.normalized : Vector2.right;

        Vector2 normalizedTargetDirection = targetDirection.normalized;
        if (currentDirection.sqrMagnitude <= MinDirectionSqr || speedDegreesPerSecond <= 0f)
            return normalizedTargetDirection;

        float maxRadiansDelta = speedDegreesPerSecond * Mathf.Deg2Rad * deltaTime;
        Vector3 rotatedDirection = Vector3.RotateTowards(currentDirection.normalized, normalizedTargetDirection, maxRadiansDelta, 0f);
        return new Vector2(rotatedDirection.x, rotatedDirection.y).normalized;
    }

    // Executes the GetMainCamera routine.
    private Camera GetMainCamera()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        return _mainCamera;
    }

    // Executes the RebuildAvailableFireModes routine.
    private void RebuildAvailableFireModes()
    {
        _availableFireModes.Clear();
        if (EquippedFirearm == null)
        {
            CurrentFireMode = FireMode.None;
            return;
        }

        for (int i = 0; i < FireModeCycleOrder.Length; i++)
        {
            FireMode mode = FireModeCycleOrder[i];
            if (EquippedFirearm.SupportsFireMode(mode))
                _availableFireModes.Add(mode);
        }

        CurrentFireMode = _availableFireModes.Count > 0 ? _availableFireModes[0] : FireMode.None;
    }

    // Executes the ResolveGlobalObjectPooler routine.
    private void ResolveGlobalObjectPooler()
    {
        globalObjectPooler = WeaponRuntimeUtility.ResolveGlobalObjectPooler(globalObjectPooler);
    }

    // Executes the ResolveWorldSfxManager routine.
    private void ResolveWorldSfxManager()
    {
        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);
    }

    // Executes the RegisterPooledPrefabs routine.
    private void RegisterPooledPrefabs()
    {
        if (globalObjectPooler == null)
            return;

        if (projectilePrefab != null)
            globalObjectPooler.RegisterPrefab(projectilePrefab.gameObject, projectilePoolPrewarm);

        if (muzzleFlashPrefab != null)
            globalObjectPooler.RegisterPrefab(muzzleFlashPrefab.gameObject, muzzleFlashPoolPrewarm);
    }

    // Executes the PlayShotSequenceSfx routine.
    private void PlayShotSequenceSfx()
    {
        if (EquippedFirearm == null)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        worldSfxManager.PlayClipSetAt(origin, EquippedFirearm.ShotSfx, EquippedFirearm.ShootNoiseType);

        if (EquippedFirearm.CasingSfx != null && EquippedFirearm.CasingSfx.HasAnyClip)
            worldSfxManager.PlayClipSetAt(origin, EquippedFirearm.CasingSfx, EquippedFirearm.ShootNoiseType, 1f, EquippedFirearm.CasingDelay);
    }

    // Executes the PlayMagazineReloadStartSfx routine.
    private void PlayMagazineReloadStartSfx()
    {
        if (EquippedFirearm == null || EquippedFirearm.ReloadStyle != ReloadType.Magazine)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, EquippedFirearm.ReloadStartSfx, EquippedFirearm.ReloadNoiseType);
    }

    // Executes the PlayMagazineReloadEndSequenceSfx routine.
    private void PlayMagazineReloadEndSequenceSfx()
    {
        if (EquippedFirearm == null || EquippedFirearm.ReloadStyle != ReloadType.Magazine)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        Vector3 origin = transform.position;
        worldSfxManager.PlayClipSetAt(origin, EquippedFirearm.ReloadEndSfx, EquippedFirearm.ReloadNoiseType, out float triggerDelay);
        worldSfxManager.PlayClipSetAt(origin, EquippedFirearm.ReloadTriggerSfx, EquippedFirearm.ReloadNoiseType, 1f, triggerDelay);
    }

    // Executes the PlayBulletReloadSfx routine.
    private void PlayBulletReloadSfx()
    {
        if (EquippedFirearm == null || EquippedFirearm.ReloadStyle != ReloadType.BulletPerBullet)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, EquippedFirearm.BulletReloadSfx, EquippedFirearm.ReloadNoiseType);
    }

    // Executes the EmitNoiseSpike routine.
    private void EmitNoiseSpike(float amount, float duration)
    {
        if (playerNoise != null)
            playerNoise.AddNoiseSpike(amount, duration);
    }

    // Executes the EmitNoiseSpike routine.
    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        EmitNoiseSpike(amount, duration, noiseType, false);
    }

    // Executes the EmitNoiseSpike routine.
    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType, bool isExtremeNoise)
    {
        WeaponRuntimeUtility.EmitNoise(playerNoise, amount, duration, noiseType, isExtremeNoise);
    }

    // Executes the ConsumeAccurateStanceAfterShot routine.
    private void ConsumeAccurateStanceAfterShot()
    {
        if (!IsAccurate)
            return;

        IsAccurate = false;
        _accurateAimTimer = 0f;
    }

    // Executes the ResolveCurrentRequiredAimTime routine.
    private float ResolveCurrentRequiredAimTime()
    {
        if (EquippedFirearm == null)
            return 0f;

        return Mathf.Max(0f, EquippedFirearm.AimTime * perkModifiers.AccurateAimTimeMultiplier);
    }

    // Executes the ResolveCurrentAccuracyProgress01 routine.
    private float ResolveCurrentAccuracyProgress01()
    {
        if (!IsAiming || EquippedFirearm == null || !IsStandingStill())
            return 0f;

        float requiredAimTime = ResolveCurrentRequiredAimTime();
        if (requiredAimTime <= 0f)
            return 1f;

        return Mathf.Clamp01(_accurateAimTimer / requiredAimTime);
    }

    // Executes the ResolveCurrentSpreadAngle routine.
    private float ResolveCurrentSpreadAngle()
    {
        if (EquippedFirearm == null)
            return 0f;

        float accurateSpreadMultiplier = 1f - Mathf.Clamp01(EquippedFirearm.Accuracy / 100f);
        return ResolvePerkAdjustedSpreadAngle() * Mathf.Lerp(1f, accurateSpreadMultiplier, ResolveCurrentAccuracyProgress01());
    }

    // Executes the ApplyPerkModifiers routine.
    public void ApplyPerkModifiers(PlayerPerkModifierSet modifiers)
    {
        perkModifiers = modifiers != null ? modifiers.Clone() : new PlayerPerkModifierSet();
        NotifyWeaponStateChanged();
    }

    // Executes the ResolveCurrentReloadDuration routine.
    private float ResolveCurrentReloadDuration()
    {
        if (EquippedFirearm == null)
            return 0f;

        return Mathf.Max(0f, EquippedFirearm.ReloadTime * perkModifiers.ReloadTimeMultiplier);
    }

    // Executes the ResolvePerkAdjustedSpreadAngle routine.
    private float ResolvePerkAdjustedSpreadAngle()
    {
        if (EquippedFirearm == null)
            return 0f;

        return Mathf.Max(0f, EquippedFirearm.Spread * ResolveFirearmSpreadMultiplier());
    }

    // Executes the ResolveFirearmSpreadMultiplier routine.
    private float ResolveFirearmSpreadMultiplier()
    {
        return perkModifiers != null
            ? Mathf.Max(0f, perkModifiers.GetFirearmSpreadMultiplier(EquippedFirearm.Class))
            : 1f;
    }

    // Executes the CanFireRequestedShot routine.
    private bool CanFireRequestedShot()
    {
        return IsAiming &&
               !IsBusy &&
               !IsReloading &&
               Time.time >= _nextAllowedFireTime &&
               CurrentLoadedAmmo > 0;
    }

    // Executes the ResetAimRuntimeState routine.
    private void ResetAimRuntimeState(bool notifyCameraState = true, bool notifyStateChanged = true)
    {
        bool wasChanged = IsAiming || IsAccurate || _accurateAimTimer > 0f;
        IsAiming = false;
        IsAccurate = false;
        _accurateAimTimer = 0f;

        if (notifyCameraState)
            UpdateAimCameraState();

        if (notifyStateChanged && wasChanged)
            NotifyWeaponStateChanged();
    }

    // Executes the ClearEquippedWeaponState routine.
    private void ClearEquippedWeaponState()
    {
        EquippedFirearm = null;
        CurrentProjectile = null;
        CurrentFireMode = FireMode.None;
        firearmRuntimeState.Clear();
        _availableFireModes.Clear();
    }

    // Executes the NotifyWeaponStateChanged routine.
    private void NotifyWeaponStateChanged()
    {
        WeaponStateChanged?.Invoke();
    }

}
}
