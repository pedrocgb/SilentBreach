using Breezeblocks.Missions;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

public partial class EnemyCombatantAI
{
    /// <summary>
    /// Caches same-object references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Resolves runtime dependencies and prepares pooled combat resources.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        EnsureCoverBuffer();
        ResolveGlobalObjectPooler();
        ResolveWorldSfxManager();
        RegisterPooledPrefabs();
    }

    /// <summary>
    /// Applies the initial weapon readiness that matches the current movement awareness.
    /// </summary>
    private void Start()
    {
        ApplyWeaponReadinessForState(enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Idle, isInitialState: true);
    }

    /// <summary>
    /// Subscribes to awareness-state changes.
    /// </summary>
    private void OnEnable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged += HandleMovementStateChanged;
    }

    /// <summary>
    /// Unsubscribes from awareness-state changes.
    /// </summary>
    private void OnDisable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged -= HandleMovementStateChanged;
    }

    /// <summary>
    /// Keeps inspector-time settings and cached references valid.
    /// </summary>
    private void OnValidate()
    {
        ClampSettings();
        CacheReferences();
        EnsureCoverBuffer();
    }

    /// <summary>
    /// Applies serialized combat settings from the enemy definition.
    /// </summary>
    public void ApplySettings(EnemyCombatSettings settings)
    {
        if (settings == null)
            return;

        CacheReferences();

        startArmed = settings.StartArmed;
        startingFirearm = settings.StartingFirearm;
        startingProjectile = settings.StartingProjectile;
        startingLoadedAmmo = settings.StartingLoadedAmmo;
        startingReserveAmmo = settings.StartingReserveAmmo;
        combatIntelligence = settings.CombatIntelligence;
        combatDelay = settings.CombatDelay;
        lostSightLingerDuration = settings.LostSightLingerDuration;
        lostSightShootingLingerDuration = settings.LostSightShootingLingerDuration;
        combatDecisionInterval = settings.CombatDecisionInterval;
        stationarySpeedThreshold = settings.StationarySpeedThreshold;
        effectiveCombatRangeMultiplier = settings.EffectiveCombatRangeMultiplier;
        fireAngleTolerance = settings.FireAngleTolerance;
        coverDetectionRange = settings.CoverDetectionRange;
        coverDetectionMask = settings.CoverDetectionMask;
        coverTag = settings.CoverTag;
        coverReevaluationInterval = settings.CoverReevaluationInterval;
        coverArrivalDistance = settings.CoverArrivalDistance;
        coverRepositionDotThreshold = settings.CoverRepositionDotThreshold;
        maxCoverResults = settings.MaxCoverResults;
        defaultAimRotationSpeed = settings.DefaultAimRotationSpeed;
        debugTraceDuration = settings.DebugTraceDuration;
        marksmanAccurateDecisionInterval = settings.MarksmanAccurateDecisionInterval;
        marksmanAccurateModeChance = settings.MarksmanAccurateModeChance;
        rifleBurstShotsMinimum = settings.RifleBurstShotsMinimum;
        rifleBurstShotsMaximum = settings.RifleBurstShotsMaximum;
        projectilePrefab = settings.ProjectilePrefab;
        projectilePoolPrewarm = settings.ProjectilePoolPrewarm;
        muzzleFlashPrefab = settings.MuzzleFlashPrefab;
        muzzleFlashPoolPrewarm = settings.MuzzleFlashPoolPrewarm;
        muzzleFlashRotationOffset = settings.MuzzleFlashRotationOffset;
        debugCombat = settings.DebugCombat;
        ClampSettings();
        EnsureCoverBuffer();
        ResetStowedWeaponLoadout();
    }

    /// <summary>
    /// Forces the configured starting weapon to be equipped for debugging.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void DebugEquipStartingWeapon()
    {
        EquipConfiguredWeaponLoadout();
    }

    /// <summary>
    /// Forces drafted combat for debugging.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void DebugDraftCombat()
    {
        BeginDraftedCombat();
    }

    /// <summary>
    /// Ends drafted combat and returns the enemy to its start point for debugging.
    /// </summary>
    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void DebugEndCombat()
    {
        EndDraftedCombat(clearCoverState: true);
        enemyMovementController?.ReturnToStart();
    }

    /// <summary>
    /// Equips a firearm loadout and resolves a valid projectile for it.
    /// </summary>
    public void EquipWeapon(FirearmData firearm, ProjectileData projectile, int loadedAmmo = -1, int reserveAmmo = -1)
    {
        if (firearm == null)
            return;

        ProjectileData resolvedProjectile = firearm.SupportsProjectile(projectile)
            ? projectile
            : firearm.CompatibleProjectiles.Count > 0 ? firearm.CompatibleProjectiles[0] : null;

        if (resolvedProjectile == null)
            return;

        equippedFirearm = firearm;
        currentProjectile = resolvedProjectile;
        currentLoadedAmmo = ResolveInitialLoadedAmmo(firearm, loadedAmmo);
        currentReserveAmmo = ResolveInitialReserveAmmo(firearm, reserveAmmo);
        accurateAimTimer = 0f;
        isAccurate = false;
        isReloading = false;
        plannedBurstShotsRemaining = 0;
        RebuildAvailableFireModes();
    }

    /// <summary>
    /// Records a newly lost target position while the enemy is drafted.
    /// </summary>
    public bool HandleDetectedTargetLost(Vector2 lastKnownPosition)
    {
        if (!isDrafted || !enabled)
            return false;

        lastSeenTargetPosition = lastKnownPosition;
        EnterLostSightLinger();
        return true;
    }

    /// <summary>
    /// Drops out of drafted combat when a new investigative noise should reclaim control.
    /// </summary>
    public void HandleInvestigativeNoiseHeard(NoiseEvent noiseEvent)
    {
        if (!isDrafted || hasClearVisualOnTarget)
            return;

        if (debugCombat)
            Debug.Log($"{name} heard a new investigative noise during combat and is dropping back into search behavior.", this);

        EndDraftedCombat(clearCoverState: true);
    }

    /// <summary>
    /// Applies or clears the flashbang state for the ranged combatant.
    /// </summary>
    public void SetFlashbanged(bool flashbanged, float aimlessRotationSpeed)
    {
        isFlashbanged = flashbanged;
        flashbangAimlessRotationSpeed = Mathf.Max(0f, aimlessRotationSpeed);

        if (!flashbanged)
            return;

        isReloading = false;
        magazineReloadSequencePlayed = false;
        plannedBurstShotsRemaining = 0;
        isAccurate = false;
        accurateAimTimer = 0f;
    }

    /// <summary>
    /// Reacts to awareness-state transitions from the movement controller.
    /// </summary>
    private void HandleMovementStateChanged(EnemyState previousState, EnemyState newState)
    {
        if (EnemyAiStateUtility.IsCombatAwarenessState(previousState) && !EnemyAiStateUtility.IsCombatAwarenessState(newState))
            EndDraftedCombat(clearCoverState: true);

        ApplyWeaponReadinessForState(newState, isInitialState: false);

        if (newState == EnemyState.Detected)
            BeginDraftedCombat();
    }

    /// <summary>
    /// Keeps the weapon drawn or holstered according to the current awareness state.
    /// </summary>
    private void ApplyWeaponReadinessForState(EnemyState state, bool isInitialState)
    {
        if (isInitialState)
        {
            ResetStowedWeaponLoadout();
            weaponEquippedForAwareness = false;
        }

        if (EnemyAiStateUtility.RequiresReadiedWeapon(state))
        {
            bool rememberAwarenessDraw = !startArmed && state != EnemyState.Alert && state != EnemyState.Detected;
            EnsureWeaponEquipped(rememberAwarenessDraw);
            return;
        }

        if (!EnemyAiStateUtility.IsCalmState(state))
            return;

        if (startArmed)
        {
            EnsureWeaponEquipped(false);
            return;
        }

        if (isInitialState || weaponEquippedForAwareness)
            HolsterCurrentWeapon();
    }

    /// <summary>
    /// Resets the stowed loadout snapshot back to the configured starting values.
    /// </summary>
    private void ResetStowedWeaponLoadout()
    {
        stowedFirearm = startingFirearm;
        stowedProjectile = startingProjectile;
        stowedLoadedAmmo = startingLoadedAmmo;
        stowedReserveAmmo = startingReserveAmmo;
    }

    /// <summary>
    /// Ensures a valid weapon loadout is active for the current awareness state.
    /// </summary>
    private void EnsureWeaponEquipped(bool rememberAwarenessDraw)
    {
        if (equippedFirearm == null || currentProjectile == null)
            EquipConfiguredWeaponLoadout();

        if (equippedFirearm == null || currentProjectile == null)
            return;

        weaponEquippedForAwareness = rememberAwarenessDraw;
    }

    /// <summary>
    /// Equips the currently stowed loadout or falls back to the starting configuration.
    /// </summary>
    private void EquipConfiguredWeaponLoadout()
    {
        FirearmData firearmToEquip = stowedFirearm != null ? stowedFirearm : startingFirearm;
        ProjectileData projectileToEquip = stowedProjectile != null ? stowedProjectile : startingProjectile;
        int loadedAmmoToEquip = stowedFirearm != null ? stowedLoadedAmmo : startingLoadedAmmo;
        int reserveAmmoToEquip = stowedFirearm != null ? stowedReserveAmmo : startingReserveAmmo;

        if (firearmToEquip == null)
            return;

        EquipWeapon(firearmToEquip, projectileToEquip, loadedAmmoToEquip, reserveAmmoToEquip);
    }

    /// <summary>
    /// Holsters the current firearm and stores its ammo state for later reuse.
    /// </summary>
    private void HolsterCurrentWeapon()
    {
        StoreCurrentWeaponLoadout();
        equippedFirearm = null;
        currentProjectile = null;
        currentLoadedAmmo = 0;
        currentReserveAmmo = 0;
        isAiming = false;
        isAccurate = false;
        accurateAimTimer = 0f;
        isReloading = false;
        magazineReloadSequencePlayed = false;
        plannedBurstShotsRemaining = 0;
        weaponEquippedForAwareness = false;
        nextReloadTickTime = 0f;
        magazineReloadEndSequenceTime = 0f;
    }

    /// <summary>
    /// Stores the currently equipped firearm state so it can be restored after holstering.
    /// </summary>
    private void StoreCurrentWeaponLoadout()
    {
        if (equippedFirearm == null)
            return;

        stowedFirearm = equippedFirearm;
        stowedProjectile = currentProjectile;
        stowedLoadedAmmo = currentLoadedAmmo;
        stowedReserveAmmo = currentReserveAmmo;
    }

    /// <summary>
    /// Clamps the initial loaded ammo value to the firearm capacity.
    /// </summary>
    private int ResolveInitialLoadedAmmo(FirearmData firearm, int requestedLoadedAmmo)
    {
        int ammoCapacity = firearm != null ? firearm.AmmoCapacity : 0;
        int defaultLoadedAmmo = ammoCapacity;
        int resolvedAmmo = requestedLoadedAmmo < 0 ? defaultLoadedAmmo : requestedLoadedAmmo;
        return Mathf.Clamp(resolvedAmmo, 0, ammoCapacity);
    }

    /// <summary>
    /// Resolves the initial reserve ammo using the firearm default when no override is supplied.
    /// </summary>
    private int ResolveInitialReserveAmmo(FirearmData firearm, int requestedReserveAmmo)
    {
        int defaultReserveAmmo = firearm != null ? firearm.DefaultReserveAmmo : 0;
        int resolvedReserveAmmo = requestedReserveAmmo < 0 ? defaultReserveAmmo : requestedReserveAmmo;
        return Mathf.Max(0, resolvedReserveAmmo);
    }

    /// <summary>
    /// Returns the current magazine capacity for the equipped firearm.
    /// </summary>
    private int ResolveCurrentAmmoCapacity()
    {
        return equippedFirearm != null ? equippedFirearm.AmmoCapacity : 0;
    }

    /// <summary>
    /// Resolves same-object references and defaults optional aim transforms.
    /// </summary>
    private void CacheReferences()
    {
        enemyMovementController ??= GetComponent<EnemyMovementController>();
        enemyVisionAI ??= GetComponent<EnemyVisionAI>();
        coverUser ??= GetComponent<CoverUser2D>();
        movementBody ??= GetComponent<Rigidbody2D>();
        actorStaggerController ??= GetComponent<ActorStaggerController>();

        if (firePoint == null)
            firePoint = transform;

        if (aimOrigin == null)
            aimOrigin = firePoint != null ? firePoint : transform;
    }

    /// <summary>
    /// Resolves the shared projectile pooler singleton when needed.
    /// </summary>
    private void ResolveGlobalObjectPooler()
    {
        globalObjectPooler ??= GlobalObjectPooler.Instance;
    }

    /// <summary>
    /// Resolves the shared world SFX manager singleton when needed.
    /// </summary>
    private void ResolveWorldSfxManager()
    {
        worldSfxManager ??= WorldSfxManager.Instance;
    }

    /// <summary>
    /// Registers pooled combat prefabs with the global pooler.
    /// </summary>
    private void RegisterPooledPrefabs()
    {
        if (globalObjectPooler == null)
            return;

        if (projectilePrefab != null)
            globalObjectPooler.RegisterPrefab(projectilePrefab.gameObject, projectilePoolPrewarm);

        if (muzzleFlashPrefab != null)
            globalObjectPooler.RegisterPrefab(muzzleFlashPrefab.gameObject, muzzleFlashPoolPrewarm);
    }

    /// <summary>
    /// Plays the shot and casing audio sequence for the active weapon.
    /// </summary>
    private void PlayShotSequenceSfx()
    {
        if (equippedFirearm == null)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        worldSfxManager.PlayClipSetAt(origin, equippedFirearm.ShotSfx, equippedFirearm.ShootNoiseType);

        if (equippedFirearm.CasingSfx != null && equippedFirearm.CasingSfx.HasAnyClip)
            worldSfxManager.PlayClipSetAt(origin, equippedFirearm.CasingSfx, equippedFirearm.ShootNoiseType, 1f, equippedFirearm.CasingDelay);
    }

    /// <summary>
    /// Plays the opening magazine reload audio sequence.
    /// </summary>
    private void PlayMagazineReloadStartSfx()
    {
        if (equippedFirearm == null || equippedFirearm.ReloadStyle != ReloadType.Magazine)
            return;

        EmitNoiseEvent(equippedFirearm.ReloadNoise, equippedFirearm.ReloadNoiseType, equippedFirearm.ReloadExtremeNoise);

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, equippedFirearm.ReloadStartSfx, equippedFirearm.ReloadNoiseType);
    }

    /// <summary>
    /// Plays the closing magazine reload audio sequence.
    /// </summary>
    private void PlayMagazineReloadEndSequenceSfx()
    {
        if (equippedFirearm == null || equippedFirearm.ReloadStyle != ReloadType.Magazine)
            return;

        EmitNoiseEvent(equippedFirearm.ReloadNoise, equippedFirearm.ReloadNoiseType, equippedFirearm.ReloadExtremeNoise);

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        Vector3 origin = transform.position;
        worldSfxManager.PlayClipSetAt(origin, equippedFirearm.ReloadEndSfx, equippedFirearm.ReloadNoiseType, out float triggerDelay);
        worldSfxManager.PlayClipSetAt(origin, equippedFirearm.ReloadTriggerSfx, equippedFirearm.ReloadNoiseType, 1f, triggerDelay);
    }

    /// <summary>
    /// Plays the per-shell reload sound for bullet-by-bullet weapons.
    /// </summary>
    private void PlayBulletReloadSfx()
    {
        if (equippedFirearm == null || equippedFirearm.ReloadStyle != ReloadType.BulletPerBullet)
            return;

        EmitNoiseEvent(equippedFirearm.ReloadNoise, equippedFirearm.ReloadNoiseType, equippedFirearm.ReloadExtremeNoise);

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, equippedFirearm.BulletReloadSfx, equippedFirearm.ReloadNoiseType);
    }

    /// <summary>
    /// Emits a world noise event from the weapon origin.
    /// </summary>
    private void EmitNoiseEvent(float amount, NoiseType noiseType, bool isExtremeNoise)
    {
        if (amount <= 0f)
            return;

        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        NoiseManager.EmitNoise(origin, amount, noiseType, gameObject, isExtremeNoise);
    }

    /// <summary>
    /// Ensures the reusable cover overlap buffer matches the configured maximum.
    /// </summary>
    private void EnsureCoverBuffer()
    {
        int requiredSize = Mathf.Max(MinimumCoverResults, maxCoverResults);
        if (coverResults == null || coverResults.Length != requiredSize)
            coverResults = new Collider2D[requiredSize];
    }

    /// <summary>
    /// Clamps inspector settings to safe runtime ranges.
    /// </summary>
    private void ClampSettings()
    {
        combatDelay = Mathf.Max(0f, combatDelay);
        lostSightLingerDuration = Mathf.Max(0f, lostSightLingerDuration);
        lostSightShootingLingerDuration = Mathf.Max(0f, lostSightShootingLingerDuration);
        combatDecisionInterval = Mathf.Max(MinimumInterval, combatDecisionInterval);
        stationarySpeedThreshold = Mathf.Max(0f, stationarySpeedThreshold);
        effectiveCombatRangeMultiplier = Mathf.Clamp(effectiveCombatRangeMultiplier, 0.1f, 1f);
        fireAngleTolerance = Mathf.Clamp(fireAngleTolerance, 0f, 45f);
        coverDetectionRange = Mathf.Max(0f, coverDetectionRange);
        coverReevaluationInterval = Mathf.Max(MinimumInterval, coverReevaluationInterval);
        coverArrivalDistance = Mathf.Max(0f, coverArrivalDistance);
        coverRepositionDotThreshold = Mathf.Clamp(coverRepositionDotThreshold, -1f, 1f);
        maxCoverResults = Mathf.Max(MinimumCoverResults, maxCoverResults);
        defaultAimRotationSpeed = Mathf.Max(0f, defaultAimRotationSpeed);
        debugTraceDuration = Mathf.Max(0f, debugTraceDuration);
        marksmanAccurateDecisionInterval = Mathf.Max(MinimumInterval, marksmanAccurateDecisionInterval);
        marksmanAccurateModeChance = Mathf.Clamp01(marksmanAccurateModeChance);
        rifleBurstShotsMinimum = Mathf.Max(1, rifleBurstShotsMinimum);
        rifleBurstShotsMaximum = Mathf.Max(rifleBurstShotsMinimum, rifleBurstShotsMaximum);
        projectilePoolPrewarm = Mathf.Max(0, projectilePoolPrewarm);
        muzzleFlashPoolPrewarm = Mathf.Max(0, muzzleFlashPoolPrewarm);
    }

    /// <summary>
    /// Rotates one direction vector toward another using a degree-per-second limit.
    /// </summary>
    private static Vector2 RotateDirectionTowards(Vector2 currentDirection, Vector2 targetDirection, float speedDegreesPerSecond, float deltaTime)
    {
        if (targetDirection.sqrMagnitude <= MinimumDirectionSqr)
            return currentDirection.sqrMagnitude > MinimumDirectionSqr ? currentDirection.normalized : Vector2.up;

        Vector2 normalizedTargetDirection = targetDirection.normalized;
        if (currentDirection.sqrMagnitude <= MinimumDirectionSqr || speedDegreesPerSecond <= 0f)
            return normalizedTargetDirection;

        float maxRadiansDelta = speedDegreesPerSecond * Mathf.Deg2Rad * deltaTime;
        Vector3 rotatedDirection = Vector3.RotateTowards(currentDirection.normalized, normalizedTargetDirection, maxRadiansDelta, 0f);
        return new Vector2(rotatedDirection.x, rotatedDirection.y).normalized;
    }
}
