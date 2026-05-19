using System;
using System.Collections;
using System.Collections.Generic;
using Rewired;
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

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string aimAction = "Aim";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string fireAction = "Fire";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string reloadAction = "Reload";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string cycleFireModeAction = "Cycle Fire Mode";

    [FoldoutGroup("References"), Tooltip("Optional fire origin. Defaults to this transform if left empty.")]
    [SerializeField] private Transform firePoint;

    [FoldoutGroup("References"), Tooltip("Optional stable origin used to resolve mouse aim direction. Defaults to the vision pivot, then this transform.")]
    [SerializeField] private Transform aimOrigin;

    [FoldoutGroup("References"), Tooltip("Optional override. If empty, auto-finds on this GameObject.")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerNoise playerNoise;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisibility playerVisibility;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerAimCamera2D aimCamera;

    [FoldoutGroup("References")]
    [SerializeField] private ArmorLoadout armorLoadout;

    [FoldoutGroup("References")]
    [SerializeField] private ActorStaggerController actorStaggerController;

    [FoldoutGroup("Pooling"), Tooltip("Optional explicit reference to the shared global pooler. If empty, the singleton instance is used.")]
    [SerializeField] private GlobalObjectPooler globalObjectPooler;

    [FoldoutGroup("Audio"), Tooltip("Optional explicit reference to the pooled world SFX manager. If empty, the singleton instance is used.")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("Pooling"), AssetsOnly]
    [SerializeField] private HitscanProjectile projectilePrefab;

    [FoldoutGroup("Pooling"), MinValue(0)]
    [SerializeField] private int projectilePoolPrewarm = 16;

    [FoldoutGroup("Pooling"), AssetsOnly]
    [SerializeField] private MuzzleFlashEffect muzzleFlashPrefab;

    [FoldoutGroup("Pooling"), MinValue(0)]
    [SerializeField] private int muzzleFlashPoolPrewarm = 8;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    [SerializeField] private float lookRotationSpeed = 720f;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    [SerializeField] private float stationarySpeedThreshold = 0.05f;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    [SerializeField] private float debugTraceDuration = 0.1f;

    [FoldoutGroup("Feedback")]
    [SerializeField] private float muzzleFlashRotationOffset;

    [FoldoutGroup("Debug Loadout")]
    [SerializeField] private bool autoEquipDebugWeaponOnStart;

    [FoldoutGroup("Debug Loadout"), AssetsOnly]
    [SerializeField] private FirearmData debugFirearm;

    [FoldoutGroup("Debug Loadout"), AssetsOnly]
    [SerializeField] private ProjectileData debugProjectile;

    [FoldoutGroup("Debug Loadout"), MinValue(-1)]
    [SerializeField] private int debugStartingLoadedAmmo = -1;

    [FoldoutGroup("Debug Loadout"), MinValue(-1)]
    [SerializeField] private int debugStartingReserveAmmo = -1;

    [FoldoutGroup("Debug Loadout"), MinValue(0)]
    [SerializeField] private int debugReserveAmmoAddAmount = 12;

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
    public int CurrentAmmo => currentLoadedAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentLoadedAmmo => currentLoadedAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentReserveAmmo => currentReserveAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentAmmoCapacity => EquippedFirearm != null ? EquippedFirearm.AmmoCapacity : 0;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasReserveAmmo => currentReserveAmmo > 0;

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

    private Player _player;
    private Camera _mainCamera;
    private Coroutine _reloadRoutine;
    private Coroutine _weaponRoutine;
    private float _accurateAimTimer;
    private float _nextAllowedFireTime;
    private int currentLoadedAmmo;
    private int currentReserveAmmo;
    private readonly List<FireMode> _availableFireModes = new();
    private bool inputBlocked;

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

        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main.GetComponent<PlayerAimCamera2D>();

        if (aimCamera == null)
            aimCamera = FindFirstObjectByType<PlayerAimCamera2D>();

        ResolveGlobalObjectPooler();
        ResolveWorldSfxManager();
        RegisterPooledPrefabs();

        if (aimCamera != null)
            aimCamera.SetFollowTarget(transform);

        ResolveRewiredPlayer();
    }

    private void Start()
    {
        if (autoEquipDebugWeaponOnStart && debugFirearm != null && GetComponent<PlayerEquipmentController>() == null)
            EquipWeapon(debugFirearm, debugProjectile, debugStartingLoadedAmmo, debugStartingReserveAmmo);
    }

    private void OnValidate()
    {
        lookRotationSpeed = Mathf.Max(0f, lookRotationSpeed);
        stationarySpeedThreshold = Mathf.Max(0f, stationarySpeedThreshold);
        debugTraceDuration = Mathf.Max(0f, debugTraceDuration);
        projectilePoolPrewarm = Mathf.Max(0, projectilePoolPrewarm);
        muzzleFlashPoolPrewarm = Mathf.Max(0, muzzleFlashPoolPrewarm);
        debugReserveAmmoAddAmount = Mathf.Max(0, debugReserveAmmoAddAmount);
    }

    private void Update()
    {
        if (_player == null && !ResolveRewiredPlayer())
            return;

        if (inputBlocked)
        {
            if (IsAiming || IsAccurate)
            {
                IsAiming = false;
                IsAccurate = false;
                _accurateAimTimer = 0f;
                UpdateAimCameraState();
                NotifyWeaponStateChanged();
            }

            return;
        }

        if (EquippedFirearm == null)
        {
            if (IsAiming || IsAccurate)
            {
                IsAiming = false;
                IsAccurate = false;
                _accurateAimTimer = 0f;
                NotifyWeaponStateChanged();
            }

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
    public void DebugEquipSelectedWeapon()
    {
        EquipWeapon(debugFirearm, debugProjectile, debugStartingLoadedAmmo, debugStartingReserveAmmo);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug Actions")]
    public void DebugAddReserveAmmo()
    {
        AddReserveAmmo(debugReserveAmmoAddAmount);
    }

    [Button(ButtonSizes.Medium)]
    [FoldoutGroup("Debug Actions")]
    public void DebugHolsterWeapon()
    {
        HolsterWeapon();
    }

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

    public bool AddReserveAmmo(int amount)
    {
        if (amount <= 0 || EquippedFirearm == null)
            return false;

        currentReserveAmmo += amount;
        NotifyWeaponStateChanged();
        return true;
    }

    public void HolsterWeapon()
    {
        if (EquippedFirearm == null || IsBusy)
            return;

        if (IsReloading && EquippedFirearm.ReloadStyle == ReloadType.Magazine)
            return;

        CancelBulletPerBulletReload();
        _weaponRoutine = StartCoroutine(HolsterWeaponRoutine());
    }

    public void SetInputBlocked(bool blocked)
    {
        if (inputBlocked == blocked)
            return;

        inputBlocked = blocked;
        if (blocked)
        {
            IsAiming = false;
            IsAccurate = false;
            _accurateAimTimer = 0f;
            UpdateAimCameraState();
            NotifyWeaponStateChanged();
        }
    }

    private IEnumerator EquipWeaponRoutine(FirearmData firearm, ProjectileData projectile, int startingLoadedAmmo, int startingReserveAmmo)
    {
        if (EquippedFirearm != null)
            yield return HolsterCurrentWeaponInternal();

        yield return new WaitForSeconds(firearm.EquipTime);

        EquippedFirearm = firearm;
        CurrentProjectile = projectile;
        currentLoadedAmmo = ResolveInitialLoadedAmmo(firearm, startingLoadedAmmo);
        currentReserveAmmo = ResolveInitialReserveAmmo(firearm, startingReserveAmmo);
        RebuildAvailableFireModes();
        CurrentAimDirection = playerMotor != null ? playerMotor.LastMoveDirection : Vector2.right;
        EmitNoiseSpike(firearm.EquipNoise, GlobalSettings.Instance != null ? GlobalSettings.Instance.EquipNoiseDuration : 0.4f, firearm.EquipNoiseType);
        NotifyWeaponStateChanged();

        _weaponRoutine = null;
    }

    private IEnumerator HolsterWeaponRoutine()
    {
        yield return HolsterCurrentWeaponInternal();
        _weaponRoutine = null;
    }

    private IEnumerator HolsterCurrentWeaponInternal()
    {
        FirearmData weaponBeingHolstered = EquippedFirearm;
        if (weaponBeingHolstered == null)
            yield break;

        IsAiming = false;
        IsAccurate = false;
        _accurateAimTimer = 0f;

        yield return new WaitForSeconds(weaponBeingHolstered.HolsterTime);

        EmitNoiseSpike(weaponBeingHolstered.HolsterNoise, GlobalSettings.Instance != null ? GlobalSettings.Instance.HolsterNoiseDuration : 0.6f, weaponBeingHolstered.HolsterNoiseType);
        EquippedFirearm = null;
        CurrentProjectile = null;
        CurrentFireMode = FireMode.None;
        currentLoadedAmmo = 0;
        currentReserveAmmo = 0;
        _availableFireModes.Clear();
        NotifyWeaponStateChanged();
    }

    private void HandleFireModeInput()
    {
        if (EquippedFirearm == null || _availableFireModes.Count <= 1 || !_player.GetButtonDown(cycleFireModeAction))
            return;

        int currentIndex = _availableFireModes.IndexOf(CurrentFireMode);
        if (currentIndex < 0)
            currentIndex = 0;

        currentIndex = (currentIndex + 1) % _availableFireModes.Count;
        CurrentFireMode = _availableFireModes[currentIndex];
        NotifyWeaponStateChanged();
    }

    private void HandleReloadInput()
    {
        if (EquippedFirearm == null || !_player.GetButtonDown(reloadAction))
            return;

        if (EquippedFirearm.ReloadStyle == ReloadType.BulletPerBullet && IsReloading)
        {
            CancelBulletPerBulletReload();
            return;
        }

        if (IsReloading || CurrentProjectile == null)
            return;

        if (currentLoadedAmmo >= CurrentAmmoCapacity || currentReserveAmmo <= 0)
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

    private IEnumerator MagazineReloadRoutine()
    {
        PlayMagazineReloadStartSfx();
        EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType);
        float reloadDuration = Mathf.Max(0f, EquippedFirearm.ReloadTime);
        float midReloadSfxDelay = reloadDuration * EquippedFirearm.MagazineReloadMidSfxNormalizedTime;
        float remainingReloadDelay = Mathf.Max(0f, reloadDuration - midReloadSfxDelay);

        if (midReloadSfxDelay > 0f)
            yield return new WaitForSeconds(midReloadSfxDelay);

        PlayMagazineReloadEndSequenceSfx();

        if (remainingReloadDelay > 0f)
            yield return new WaitForSeconds(remainingReloadDelay);

        int missingRounds = Mathf.Max(0, CurrentAmmoCapacity - currentLoadedAmmo);
        int roundsToTransfer = Mathf.Min(missingRounds, currentReserveAmmo);
        if (roundsToTransfer > 0)
        {
            currentLoadedAmmo += roundsToTransfer;
            currentReserveAmmo -= roundsToTransfer;
            EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType);
        }

        _reloadRoutine = null;
        NotifyWeaponStateChanged();
    }

    private IEnumerator BulletPerBulletReloadRoutine()
    {
        bool loadedAnyRound = false;
        EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType);

        while (EquippedFirearm != null &&
               CurrentProjectile != null &&
               currentLoadedAmmo < CurrentAmmoCapacity &&
               currentReserveAmmo > 0)
        {
            yield return new WaitForSeconds(EquippedFirearm.ReloadTime);

            if (currentReserveAmmo <= 0 || currentLoadedAmmo >= CurrentAmmoCapacity)
                break;

            currentLoadedAmmo++;
            currentReserveAmmo--;
            loadedAnyRound = true;
            PlayBulletReloadSfx();
            NotifyWeaponStateChanged();
        }

        if (loadedAnyRound && EquippedFirearm != null)
            EmitNoiseSpike(EquippedFirearm.ReloadNoise, EquippedFirearm.ReloadNoiseDuration, EquippedFirearm.ReloadNoiseType);

        _reloadRoutine = null;
        NotifyWeaponStateChanged();
    }

    private void CancelBulletPerBulletReload()
    {
        if (_reloadRoutine == null || EquippedFirearm == null || EquippedFirearm.ReloadStyle != ReloadType.BulletPerBullet)
            return;

        StopCoroutine(_reloadRoutine);
        _reloadRoutine = null;
        NotifyWeaponStateChanged();
    }

    private void HandleFireInput()
    {
        if (EquippedFirearm == null || CurrentProjectile == null || CurrentFireMode == FireMode.None)
            return;

        bool fireRequested = CurrentFireMode == FireMode.FullAuto
            ? _player.GetButton(fireAction)
            : _player.GetButtonDown(fireAction);

        if (!fireRequested)
            return;

        if (EquippedFirearm.ReloadStyle == ReloadType.BulletPerBullet && IsReloading)
            CancelBulletPerBulletReload();

        if (!IsAiming || IsBusy || IsReloading || Time.time < _nextAllowedFireTime || currentLoadedAmmo <= 0)
            return;

        FireCurrentMode();
    }

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

    private void FireBurst()
    {
        int burstShots = Mathf.Max(1, EquippedFirearm.BurstCount);
        for (int i = 0; i < burstShots; i++)
        {
            if (!TryConsumeCurrentRound(out ProjectileData projectile))
                break;

            SpawnProjectile(projectile, 1);
            ConsumeAccurateStanceAfterShot();
        }
    }

    private void FirePumpShot()
    {
        if (!TryConsumeCurrentRound(out ProjectileData projectile))
            return;

        int pellets = Mathf.Max(1, EquippedFirearm.PelletCount);
        SpawnProjectile(projectile, pellets);
        ConsumeAccurateStanceAfterShot();
    }

    private void FireSingleRound()
    {
        if (!TryConsumeCurrentRound(out ProjectileData projectile))
            return;

        SpawnProjectile(projectile, 1);
        ConsumeAccurateStanceAfterShot();
    }

    private bool TryConsumeCurrentRound(out ProjectileData projectile)
    {
        projectile = CurrentProjectile;
        if (EquippedFirearm == null || CurrentProjectile == null || currentLoadedAmmo <= 0)
            return false;

        currentLoadedAmmo--;
        EmitNoiseSpike(EquippedFirearm.ShootNoise, GlobalSettings.Instance != null ? GlobalSettings.Instance.ShotNoiseDuration : 0.1f, EquippedFirearm.ShootNoiseType);
        SpawnMuzzleFlash();
        ApplyShotVisibility();
        ApplyScreenshake();
        PlayShotSequenceSfx();
        NotifyWeaponStateChanged();
        return true;
    }

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

    private void ApplyShotVisibility()
    {
        if (EquippedFirearm == null || EquippedFirearm.HideMuzzleFlash || playerVisibility == null)
            return;

        playerVisibility.ApplyMuzzleFlashVisibility();
    }

    private void ApplyScreenshake()
    {
        if (EquippedFirearm == null || aimCamera == null)
            return;

        aimCamera.PlayScreenshake(EquippedFirearm.ScreenshakePower, EquippedFirearm.ScreenshakeDuration);
    }

    private Vector2 ApplySpread(Vector2 baseDirection)
    {
        if (baseDirection.sqrMagnitude <= MinDirectionSqr)
            return Vector2.right;

        float spread = Mathf.Max(0f, EquippedFirearm != null ? EquippedFirearm.Spread : 0f);
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

    private void UpdateAimDirection()
    {
        float effectiveSpeed = GetEffectiveLookSpeed();

        if (playerVisionLight != null)
        {
            CurrentAimDirection = playerVisionLight.DriveMouseLook(effectiveSpeed, Time.deltaTime);
            if (CurrentAimDirection.sqrMagnitude <= MinDirectionSqr)
                CurrentAimDirection = ResolveFallbackAimDirection();

            return;
        }

        Vector2 targetDirection = ResolveMouseDirection();
        if (targetDirection.sqrMagnitude <= MinDirectionSqr)
            targetDirection = ResolveFallbackAimDirection();

        CurrentAimDirection = RotateAimDirectionTowards(CurrentAimDirection, targetDirection, effectiveSpeed, Time.deltaTime);
    }

    private void UpdateAimState()
    {
        bool aimRequested = EquippedFirearm != null && !IsBusy && _player.GetButton(aimAction);

        if (aimRequested && EquippedFirearm != null && EquippedFirearm.ReloadStyle == ReloadType.BulletPerBullet && IsReloading)
            CancelBulletPerBulletReload();

        IsAiming = aimRequested && !IsReloading;
    }

    private void UpdateAccurateMode()
    {
        if (!IsAiming || EquippedFirearm == null || !IsStandingStill())
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

    private void UpdateAimCameraState()
    {
        if (aimCamera == null || EquippedFirearm == null)
            return;

        aimCamera.SetFollowTarget(transform);
        aimCamera.SetAimState(IsAiming, CurrentProjectile != null ? CurrentProjectile.Range : 0f);
    }

    private bool IsStandingStill()
    {
        if (playerMotor == null)
            return true;

        return !playerMotor.HasMovementInput && playerMotor.CurrentPlanarSpeed <= stationarySpeedThreshold;
    }

    private float GetEffectiveLookSpeed()
    {
        float speed = IsAiming && EquippedFirearm != null ? EquippedFirearm.AimSpeed : lookRotationSpeed;
        float rotationPenaltyPercent = armorLoadout != null ? armorLoadout.RotationPenaltyPercent : 0f;
        float staggerMultiplier = actorStaggerController != null ? actorStaggerController.TurnSpeedMultiplier : 1f;
        return speed * (1f - Mathf.Clamp01(rotationPenaltyPercent / 100f)) * staggerMultiplier;
    }

    private Vector2 ResolveMouseDirection()
    {
        Camera camera = GetMainCamera();
        Vector3 origin = GetAimOriginPosition();

        if (camera == null)
            return CurrentAimDirection;

        Vector3 mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = origin.z;
        Vector2 direction = (Vector2)(mouseWorld - origin);

        if (direction.sqrMagnitude <= MinDirectionSqr)
            return CurrentAimDirection;

        return direction.normalized;
    }

    private Vector3 GetAimOriginPosition()
    {
        if (aimOrigin != null)
            return aimOrigin.position;

        if (playerVisionLight != null)
            return playerVisionLight.transform.position;

        return transform.position;
    }

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

    private Camera GetMainCamera()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        return _mainCamera;
    }

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

    private void ResolveGlobalObjectPooler()
    {
        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;
    }

    private void ResolveWorldSfxManager()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;
    }

    private void RegisterPooledPrefabs()
    {
        if (globalObjectPooler == null)
            return;

        if (projectilePrefab != null)
            globalObjectPooler.RegisterPrefab(projectilePrefab.gameObject, projectilePoolPrewarm);

        if (muzzleFlashPrefab != null)
            globalObjectPooler.RegisterPrefab(muzzleFlashPrefab.gameObject, muzzleFlashPoolPrewarm);
    }

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

    private void PlayMagazineReloadStartSfx()
    {
        if (EquippedFirearm == null || EquippedFirearm.ReloadStyle != ReloadType.Magazine)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, EquippedFirearm.ReloadStartSfx, EquippedFirearm.ReloadNoiseType);
    }

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

    private void PlayBulletReloadSfx()
    {
        if (EquippedFirearm == null || EquippedFirearm.ReloadStyle != ReloadType.BulletPerBullet)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, EquippedFirearm.BulletReloadSfx, EquippedFirearm.ReloadNoiseType);
    }

    private void EmitNoiseSpike(float amount, float duration)
    {
        if (playerNoise != null)
            playerNoise.AddNoiseSpike(amount, duration);
    }

    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        if (playerNoise != null)
            playerNoise.AddNoiseSpike(amount, duration, noiseType);
    }

    private void ConsumeAccurateStanceAfterShot()
    {
        if (!IsAccurate)
            return;

        IsAccurate = false;
        _accurateAimTimer = 0f;
    }

    private float ResolveCurrentRequiredAimTime()
    {
        if (EquippedFirearm == null)
            return 0f;

        return Mathf.Max(0f, EquippedFirearm.AimTime);
    }

    private float ResolveCurrentAccuracyProgress01()
    {
        if (!IsAiming || EquippedFirearm == null || !IsStandingStill())
            return 0f;

        float requiredAimTime = ResolveCurrentRequiredAimTime();
        if (requiredAimTime <= 0f)
            return 1f;

        return Mathf.Clamp01(_accurateAimTimer / requiredAimTime);
    }

    private float ResolveCurrentSpreadAngle()
    {
        if (EquippedFirearm == null)
            return 0f;

        float accurateSpreadMultiplier = 1f - Mathf.Clamp01(EquippedFirearm.Accuracy / 100f);
        return EquippedFirearm.Spread * Mathf.Lerp(1f, accurateSpreadMultiplier, ResolveCurrentAccuracyProgress01());
    }

    private int ResolveInitialLoadedAmmo(FirearmData firearm, int requestedLoadedAmmo)
    {
        int ammoCapacity = firearm != null ? firearm.AmmoCapacity : 0;
        int defaultLoadedAmmo = ammoCapacity;
        int resolvedAmmo = requestedLoadedAmmo < 0 ? defaultLoadedAmmo : requestedLoadedAmmo;
        return Mathf.Clamp(resolvedAmmo, 0, ammoCapacity);
    }

    private int ResolveInitialReserveAmmo(FirearmData firearm, int requestedReserveAmmo)
    {
        int defaultReserveAmmo = firearm != null ? firearm.DefaultReserveAmmo : 0;
        int resolvedReserveAmmo = requestedReserveAmmo < 0 ? defaultReserveAmmo : requestedReserveAmmo;
        return Mathf.Max(0, resolvedReserveAmmo);
    }

    private void NotifyWeaponStateChanged()
    {
        WeaponStateChanged?.Invoke();
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        _player = ReInput.players.GetPlayer(rewiredPlayerId);
        return _player != null;
    }
}
}
