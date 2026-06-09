using Breezeblocks.Missions;
using Breezeblocks.WeaponSystem;
using Pathfinding;
using UnityEngine;

public partial class EnemyCombatantAI
{
    /// <summary>
    /// Advances drafted combat logic, aiming, and firing behavior.
    /// </summary>
    private void Update()
    {
        if (GameplayMissionController.EnemyRuntimeBlockedAtMissionStart)
            return;

        UpdateReloadState();

        if (isFlashbanged)
        {
            UpdateFlashbangedBehavior();
            return;
        }

        SyncVisionState();

        if (!isDrafted && ShouldDraftCombatForCurrentAwareness())
            BeginDraftedCombat();

        if (equippedFirearm == null || currentProjectile == null || !isDrafted)
            return;

        isAiming = true;
        UpdateAimDirection();
        UpdateAccurateMode();

        if (Time.time < nextCombatDecisionTime)
            return;

        nextCombatDecisionTime = Time.time + combatDecisionInterval;
        UpdateCombatBehavior();
    }

    /// <summary>
    /// Starts the drafted combat runtime state for the current target.
    /// </summary>
    private void BeginDraftedCombat()
    {
        if (!enabled || equippedFirearm == null || currentProjectile == null)
            return;

        isDrafted = true;
        currentCombatMode = EnemyCombatMode.CombatDelay;
        combatDelayEndTime = Time.time + combatDelay;
        lostSightLingerEndTime = 0f;
        lostSightShootingEndTime = 0f;
        nextCombatDecisionTime = Time.time;
        nextCoverEvaluationTime = Time.time;
        nextMarksmanAccurateDecisionTime = Time.time;
        currentTarget = enemyVisionAI != null ? enemyVisionAI.TargetTransform : null;
        lastSeenTargetPosition = ResolveCurrentAimPoint();
        plannedBurstShotsRemaining = 0;
        marksmanWantsAccurateShots = false;
        coverUser?.ClearActiveCover();
        enemyMovementController?.ClearDetectedMovementOverride(false);
        enemyMovementController?.HoldDetectedPosition();
        enemyMovementController?.ClearFacingOverride();

        if (debugCombat)
            Debug.Log($"{name} entered drafted combat mode.", this);
    }

    /// <summary>
    /// Clears drafted combat runtime state and optional cover state.
    /// </summary>
    private void EndDraftedCombat(bool clearCoverState)
    {
        if (!isDrafted)
            return;

        isDrafted = false;
        isAiming = false;
        isAccurate = false;
        accurateAimTimer = 0f;
        currentCombatMode = EnemyCombatMode.None;
        plannedBurstShotsRemaining = 0;
        marksmanWantsAccurateShots = false;
        lostSightLingerEndTime = 0f;
        lostSightShootingEndTime = 0f;
        currentSelectedCover = null;
        currentSelectedCoverPoint = Vector2.zero;
        currentSelectedCoverProtectionDirection = Vector2.zero;

        if (clearCoverState && coverUser != null)
            coverUser.ClearActiveCover();

        enemyMovementController?.ClearDetectedMovementOverride(false);
        enemyMovementController?.ClearFacingOverride();
    }

    /// <summary>
    /// Synchronizes local target and line-of-sight state from the vision component.
    /// </summary>
    private void SyncVisionState()
    {
        currentTarget = enemyVisionAI != null ? enemyVisionAI.TargetTransform : null;
        hasClearVisualOnTarget = enemyVisionAI != null && enemyVisionAI.CanCurrentlyDetectTarget && enemyVisionAI.HasLineOfSight;

        if (hasClearVisualOnTarget)
            lastSeenTargetPosition = ResolveCurrentAimPoint();
    }

    /// <summary>
    /// Returns whether the current awareness state should draft ranged combat.
    /// </summary>
    private bool ShouldDraftCombatForCurrentAwareness()
    {
        if (enemyMovementController == null ||
            equippedFirearm == null ||
            currentProjectile == null)
        {
            return false;
        }

        EnemyState awarenessState = enemyMovementController.CurrentState;
        return awarenessState == EnemyState.Detected ||
               (awarenessState == EnemyState.Alert && hasClearVisualOnTarget);
    }

    /// <summary>
    /// Runs flashbanged fallback firing behavior while regular combat logic is suspended.
    /// </summary>
    private void UpdateFlashbangedBehavior()
    {
        isAiming = equippedFirearm != null;
        isAccurate = false;
        accurateAimTimer = 0f;
        plannedBurstShotsRemaining = 0;

        if (equippedFirearm == null || currentProjectile == null)
            return;

        if (enemyMovementController != null && enemyMovementController.CurrentFacingDirection.sqrMagnitude > MinimumDirectionSqr)
        {
            currentAimDirection = enemyMovementController.CurrentFacingDirection.normalized;
        }
        else
        {
            currentAimDirection = transform.up;
        }

        if (currentAimDirection.sqrMagnitude <= MinimumDirectionSqr)
            currentAimDirection = transform.up;

        if (currentLoadedAmmo <= 0 || Time.time < nextAllowedFireTime)
            return;

        currentAimDirection = RotateDirection(currentAimDirection, flashbangAimlessRotationSpeed * Time.deltaTime);
        FireCurrentMode();
    }

    /// <summary>
    /// Evaluates the next high-level ranged combat action.
    /// </summary>
    private void UpdateCombatBehavior()
    {
        if (currentTarget == null && lastSeenTargetPosition == Vector2.zero)
            return;

        if (!hasClearVisualOnTarget)
        {
            if (currentCombatMode != EnemyCombatMode.LostSightLinger)
                EnterLostSightLinger();

            UpdateLostSightLinger();
            return;
        }

        UpdateCoverSelection(force: Time.time >= nextCoverEvaluationTime);

        if (Time.time < combatDelayEndTime)
        {
            currentCombatMode = EnemyCombatMode.CombatDelay;
            UpdateCombatPositioning(allowShooting: false);
            return;
        }

        UpdateCombatPositioning(allowShooting: true);
        UpdateReloadIntent();
        TryFireAccordingToBehavior();
    }

    /// <summary>
    /// Starts the linger window that follows a lost visual on the target.
    /// </summary>
    private void EnterLostSightLinger()
    {
        currentCombatMode = EnemyCombatMode.LostSightLinger;
        lostSightLingerEndTime = Time.time + lostSightLingerDuration;
        lostSightShootingEndTime = Time.time + lostSightShootingLingerDuration;
        enemyMovementController?.HoldDetectedPosition();
        enemyMovementController?.SetFacingPoint(lastSeenTargetPosition);
    }

    /// <summary>
    /// Maintains position and optional blind fire during the lost-sight linger window.
    /// </summary>
    private void UpdateLostSightLinger()
    {
        enemyMovementController?.HoldDetectedPosition();
        enemyMovementController?.SetFacingPoint(lastSeenTargetPosition);

        if (CanContinueShootingDuringLostSightLinger())
        {
            UpdateReloadIntent();
            TryFireAccordingToBehavior();
        }

        if (Time.time < lostSightLingerEndTime)
            return;

        EndDraftedCombat(clearCoverState: true);
        enemyMovementController?.EnterAlertState(force: true);
    }

    /// <summary>
    /// Moves the enemy into its current tactical firing position.
    /// </summary>
    private void UpdateCombatPositioning(bool allowShooting)
    {
        Vector2 currentThreatPosition = ResolveCurrentAimPoint();

        if (currentSelectedCover != null)
        {
            MoveToSelectedCover(currentThreatPosition);
            if (!allowShooting)
                return;

            if (combatIntelligence == EnemyCombatIntelligence.Marksman && !coverUser.IsInCover)
                return;

            if ((combatIntelligence == EnemyCombatIntelligence.Sharpshooter || combatIntelligence == EnemyCombatIntelligence.Expert) &&
                !coverUser.IsInCover)
            {
                return;
            }

            currentCombatMode = EnemyCombatMode.Engaging;
            return;
        }

        coverUser?.ClearActiveCover();

        if (combatIntelligence == EnemyCombatIntelligence.Marksman)
        {
            UpdateMarksmanNoCoverPositioning(currentThreatPosition);
            return;
        }

        UpdateFallbackPositioning(currentThreatPosition);
    }

    /// <summary>
    /// Moves toward or settles into the currently selected cover slot.
    /// </summary>
    private void MoveToSelectedCover(Vector2 threatPosition)
    {
        if (currentSelectedCover == null)
            return;

        if (IsNearPosition(currentSelectedCoverPoint, coverArrivalDistance))
        {
            coverUser?.SetActiveCover(currentSelectedCover, currentSelectedCoverPoint, currentSelectedCoverProtectionDirection, threatPosition);
            enemyMovementController?.HoldDetectedPosition();
            enemyMovementController?.SetFacingPoint(threatPosition);
            currentCombatMode = EnemyCombatMode.Engaging;
            return;
        }

        coverUser?.ClearActiveCover();
        enemyMovementController?.SetDetectedDestination(currentSelectedCoverPoint, EnemySpeedType.Sprint);
        currentCombatMode = EnemyCombatMode.MovingToCover;
    }

    /// <summary>
    /// Positions marksmen at their preferred effective range when no cover is used.
    /// </summary>
    private void UpdateMarksmanNoCoverPositioning(Vector2 threatPosition)
    {
        Vector2 toThreat = threatPosition - CurrentPosition;
        float distanceToThreat = toThreat.magnitude;
        float effectiveRange = ResolveEffectiveCombatRange();

        if (distanceToThreat > effectiveRange && toThreat.sqrMagnitude > MinimumDirectionSqr)
        {
            Vector2 desiredCombatPosition = threatPosition - (toThreat.normalized * effectiveRange);
            if (!IsNearPosition(desiredCombatPosition, coverArrivalDistance))
            {
                enemyMovementController?.SetDetectedDestination(desiredCombatPosition, EnemySpeedType.Run);
                currentCombatMode = EnemyCombatMode.MovingToCover;
                return;
            }

            enemyMovementController?.HoldDetectedPosition();
            enemyMovementController?.SetFacingPoint(threatPosition);
            currentCombatMode = EnemyCombatMode.Engaging;
            return;
        }

        enemyMovementController?.HoldDetectedPosition();
        enemyMovementController?.SetFacingPoint(threatPosition);
        currentCombatMode = EnemyCombatMode.Engaging;
    }

    /// <summary>
    /// Uses the configured fallback point when no cover slot is available.
    /// </summary>
    private void UpdateFallbackPositioning(Vector2 threatPosition)
    {
        if (noCoverFallbackPoint != null && !IsNearPosition(noCoverFallbackPoint.position, coverArrivalDistance))
        {
            enemyMovementController?.SetDetectedDestination(noCoverFallbackPoint.position, EnemySpeedType.Sprint);
            currentCombatMode = EnemyCombatMode.HoldingFallback;
            return;
        }

        enemyMovementController?.HoldDetectedPosition();
        enemyMovementController?.SetFacingPoint(threatPosition);
        currentCombatMode = EnemyCombatMode.HoldingFallback;
    }

    /// <summary>
    /// Chooses the best nearby cover slot for the current threat position.
    /// </summary>
    private void UpdateCoverSelection(bool force)
    {
        if (!force && Time.time < nextCoverEvaluationTime)
            return;

        nextCoverEvaluationTime = Time.time + coverReevaluationInterval;

        Vector2 threatPosition = ResolveCurrentAimPoint();
        if (currentSelectedCover != null &&
            currentSelectedCover.TryGetCoverSlot(threatPosition, out Vector2 refreshedCoverPoint, out Vector2 refreshedProtectionDirection))
        {
            float repositionDot = Vector2.Dot(currentSelectedCoverProtectionDirection.normalized, refreshedProtectionDirection.normalized);
            if (repositionDot >= coverRepositionDotThreshold)
            {
                currentSelectedCoverPoint = refreshedCoverPoint;
                currentSelectedCoverProtectionDirection = refreshedProtectionDirection;
                return;
            }
        }

        currentSelectedCover = null;
        currentSelectedCoverPoint = Vector2.zero;
        currentSelectedCoverProtectionDirection = Vector2.zero;

        EnsureCoverBuffer();
        int resultCount = Physics2D.OverlapCircle(CurrentPosition, coverDetectionRange, new ContactFilter2D
        {
            useLayerMask = coverDetectionMask.value != 0,
            layerMask = coverDetectionMask,
            useTriggers = false
        }, coverResults);

        float bestScore = float.MaxValue;
        for (int i = 0; i < resultCount; i++)
        {
            Collider2D coverCollider = coverResults[i];
            if (coverCollider == null)
                continue;

            CombatCover2D candidateCover = coverCollider.GetComponentInParent<CombatCover2D>();
            if (candidateCover == null)
                continue;

            if (!string.IsNullOrWhiteSpace(coverTag) &&
                candidateCover.gameObject.tag != coverTag &&
                coverCollider.gameObject.tag != coverTag)
            {
                continue;
            }

            if (!candidateCover.TryGetCoverSlot(threatPosition, out Vector2 candidateSlot, out Vector2 candidateProtectionDirection))
                continue;

            float score = Vector2.Distance(CurrentPosition, candidateSlot);
            if (score >= bestScore)
                continue;

            bestScore = score;
            currentSelectedCover = candidateCover;
            currentSelectedCoverPoint = candidateSlot;
            currentSelectedCoverProtectionDirection = candidateProtectionDirection;
        }
    }

    /// <summary>
    /// Starts reloading when empty or when safely in cover.
    /// </summary>
    private void UpdateReloadIntent()
    {
        if (equippedFirearm == null || currentProjectile == null || isReloading)
            return;

        if (currentLoadedAmmo <= 0 && currentReserveAmmo > 0)
        {
            BeginReload();
            return;
        }

        if (coverUser != null &&
            coverUser.IsInCover &&
            currentLoadedAmmo < ResolveCurrentAmmoCapacity() &&
            currentReserveAmmo > 0 &&
            !hasClearVisualOnTarget)
        {
            BeginReload();
        }
    }

    /// <summary>
    /// Evaluates whether this frame should fire based on tactical state and weapon policy.
    /// </summary>
    private void TryFireAccordingToBehavior()
    {
        if (equippedFirearm == null || currentProjectile == null || isReloading || Time.time < nextAllowedFireTime)
            return;

        if (currentLoadedAmmo <= 0)
        {
            BeginReload();
            return;
        }

        Vector2 aimPoint = ResolveCurrentAimPoint();
        if (Vector2.Distance(CurrentPosition, aimPoint) > currentProjectile.Range)
            return;

        if (Vector2.Angle(currentAimDirection, (aimPoint - ResolveAimOriginPosition()).normalized) > fireAngleTolerance)
            return;

        if (!CanShootInCurrentTacticalPosition())
            return;

        bool shouldFire = combatIntelligence switch
        {
            EnemyCombatIntelligence.Marksman => ResolveMarksmanFireIntent(),
            EnemyCombatIntelligence.Sharpshooter => ResolveSharpshooterOrExpertFireIntent(alwaysPreferAccurate: false),
            EnemyCombatIntelligence.Expert => ResolveSharpshooterOrExpertFireIntent(alwaysPreferAccurate: true),
            _ => false
        };

        if (!shouldFire)
            return;

        FireCurrentMode();
    }

    /// <summary>
    /// Resolves whether a marksman should fire now or continue waiting for accurate shots.
    /// </summary>
    private bool ResolveMarksmanFireIntent()
    {
        if (coverUser != null && coverUser.IsInCover)
        {
            if (Time.time >= nextMarksmanAccurateDecisionTime)
            {
                marksmanWantsAccurateShots = Random.value <= marksmanAccurateModeChance;
                nextMarksmanAccurateDecisionTime = Time.time + marksmanAccurateDecisionInterval;
            }

            return !marksmanWantsAccurateShots || isAccurate;
        }

        return true;
    }

    /// <summary>
    /// Resolves whether sharpshooter and expert enemies are ready to fire.
    /// </summary>
    private bool ResolveSharpshooterOrExpertFireIntent(bool alwaysPreferAccurate)
    {
        if (currentSelectedCover != null && (coverUser == null || !coverUser.IsInCover))
            return false;

        if (currentSelectedCover == null && noCoverFallbackPoint != null && !IsNearPosition(noCoverFallbackPoint.position, coverArrivalDistance))
            return false;

        EnemyCombatWeaponPolicy weaponPolicy = ResolveWeaponPolicy();
        return weaponPolicy switch
        {
            EnemyCombatWeaponPolicy.Immediate => true,
            EnemyCombatWeaponPolicy.AccurateOnly => isAccurate,
            EnemyCombatWeaponPolicy.BurstOnAccurate => ResolveBurstFireIntent(alwaysPreferAccurate),
            _ => false
        };
    }

    /// <summary>
    /// Handles burst-plan generation for automatic weapons that prefer accurate volleys.
    /// </summary>
    private bool ResolveBurstFireIntent(bool alwaysPreferAccurate)
    {
        if (plannedBurstShotsRemaining > 0)
        {
            plannedBurstShotsRemaining--;
            return true;
        }

        if (!isAccurate && alwaysPreferAccurate)
            return false;

        if (!isAccurate)
            return false;

        int minShots = Mathf.Max(1, rifleBurstShotsMinimum);
        int maxShots = Mathf.Max(minShots, rifleBurstShotsMaximum);
        plannedBurstShotsRemaining = Random.Range(minShots, maxShots + 1) - 1;
        return true;
    }

    /// <summary>
    /// Returns whether tactical positioning allows the current shot.
    /// </summary>
    private bool CanShootInCurrentTacticalPosition()
    {
        if (!hasClearVisualOnTarget)
            return CanContinueShootingDuringLostSightLinger();

        if (currentCombatMode == EnemyCombatMode.CombatDelay)
            return false;

        if (combatIntelligence == EnemyCombatIntelligence.Marksman)
            return true;

        if (currentSelectedCover != null)
            return coverUser != null && coverUser.IsInCover;

        return false;
    }

    /// <summary>
    /// Returns whether blind-fire is still allowed during the lost-sight linger window.
    /// </summary>
    private bool CanContinueShootingDuringLostSightLinger()
    {
        return currentCombatMode == EnemyCombatMode.LostSightLinger &&
               Time.time < lostSightShootingEndTime;
    }

    /// <summary>
    /// Resolves the tactical firing policy associated with the equipped weapon class.
    /// </summary>
    private EnemyCombatWeaponPolicy ResolveWeaponPolicy()
    {
        if (equippedFirearm == null)
            return EnemyCombatWeaponPolicy.Immediate;

        return equippedFirearm.Class switch
        {
            FirearmClass.Pistol => EnemyCombatWeaponPolicy.AccurateOnly,
            FirearmClass.Revolver => EnemyCombatWeaponPolicy.AccurateOnly,
            FirearmClass.Carbine => EnemyCombatWeaponPolicy.AccurateOnly,
            FirearmClass.SniperRifle => EnemyCombatWeaponPolicy.AccurateOnly,
            FirearmClass.Shotgun => EnemyCombatWeaponPolicy.Immediate,
            FirearmClass.PumpShotgun => EnemyCombatWeaponPolicy.Immediate,
            FirearmClass.SemiAutoShotgun => EnemyCombatWeaponPolicy.Immediate,
            FirearmClass.Rifle => EnemyCombatWeaponPolicy.BurstOnAccurate,
            FirearmClass.AssaultRifle => EnemyCombatWeaponPolicy.BurstOnAccurate,
            FirearmClass.SMG => EnemyCombatWeaponPolicy.BurstOnAccurate,
            _ => EnemyCombatWeaponPolicy.AccurateOnly
        };
    }

    /// <summary>
    /// Starts a reload sequence if the weapon and ammo state allow it.
    /// </summary>
    private void BeginReload()
    {
        if (equippedFirearm == null ||
            currentProjectile == null ||
            isReloading ||
            currentLoadedAmmo >= ResolveCurrentAmmoCapacity() ||
            currentReserveAmmo <= 0)
        {
            return;
        }

        isReloading = true;
        nextReloadTickTime = Time.time + equippedFirearm.ReloadTime;
        magazineReloadSequencePlayed = false;
        magazineReloadEndSequenceTime = Time.time + (equippedFirearm.ReloadTime * equippedFirearm.MagazineReloadMidSfxNormalizedTime);

        if (equippedFirearm.ReloadStyle == ReloadType.Magazine)
            PlayMagazineReloadStartSfx();
    }

    /// <summary>
    /// Advances the active reload sequence and transfers ammo when ready.
    /// </summary>
    private void UpdateReloadState()
    {
        if (!isReloading || equippedFirearm == null || currentProjectile == null)
            return;

        if (equippedFirearm.ReloadStyle == ReloadType.Magazine)
        {
            if (!magazineReloadSequencePlayed && Time.time >= magazineReloadEndSequenceTime)
            {
                PlayMagazineReloadEndSequenceSfx();
                magazineReloadSequencePlayed = true;
            }

            if (Time.time < nextReloadTickTime)
                return;

            int missingRounds = Mathf.Max(0, ResolveCurrentAmmoCapacity() - currentLoadedAmmo);
            int roundsToTransfer = Mathf.Min(missingRounds, currentReserveAmmo);
            currentLoadedAmmo += roundsToTransfer;
            currentReserveAmmo -= roundsToTransfer;
            isReloading = false;
            magazineReloadSequencePlayed = false;
            return;
        }

        if (Time.time < nextReloadTickTime)
            return;

        if (currentLoadedAmmo < ResolveCurrentAmmoCapacity() && currentReserveAmmo > 0)
        {
            currentLoadedAmmo++;
            currentReserveAmmo--;
            PlayBulletReloadSfx();
        }

        if (currentLoadedAmmo >= ResolveCurrentAmmoCapacity() || currentReserveAmmo <= 0)
            isReloading = false;
        else
            nextReloadTickTime = Time.time + equippedFirearm.ReloadTime;
    }

    /// <summary>
    /// Fires the weapon using the currently selected fire mode.
    /// </summary>
    private void FireCurrentMode()
    {
        switch (currentFireMode)
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

        nextAllowedFireTime = Time.time + (equippedFirearm.FireRate > 0f ? 1f / equippedFirearm.FireRate : 0f);
    }

    /// <summary>
    /// Fires a burst sequence using the current projectile.
    /// </summary>
    private void FireBurst()
    {
        int burstShots = Mathf.Max(1, equippedFirearm.BurstCount);
        for (int i = 0; i < burstShots; i++)
        {
            if (!TryConsumeCurrentRound(out ProjectileData projectile))
                break;

            SpawnProjectile(projectile, 1);
            ConsumeAccurateStanceAfterShot();
        }
    }

    /// <summary>
    /// Fires a multi-pellet pump shot.
    /// </summary>
    private void FirePumpShot()
    {
        if (!TryConsumeCurrentRound(out ProjectileData projectile))
            return;

        int pellets = Mathf.Max(1, equippedFirearm.PelletCount);
        SpawnProjectile(projectile, pellets);
        ConsumeAccurateStanceAfterShot();
    }

    /// <summary>
    /// Fires a single round from the current weapon.
    /// </summary>
    private void FireSingleRound()
    {
        if (!TryConsumeCurrentRound(out ProjectileData projectile))
            return;

        SpawnProjectile(projectile, 1);
        ConsumeAccurateStanceAfterShot();
    }

    /// <summary>
    /// Consumes one loaded round and plays its supporting effects.
    /// </summary>
    private bool TryConsumeCurrentRound(out ProjectileData projectile)
    {
        projectile = currentProjectile;
        if (equippedFirearm == null || projectile == null || currentLoadedAmmo <= 0)
            return false;

        currentLoadedAmmo--;
        EmitNoiseEvent(equippedFirearm.ShootNoise, equippedFirearm.ShootNoiseType, equippedFirearm.ShootExtremeNoise);
        SpawnMuzzleFlash();
        PlayShotSequenceSfx();
        return true;
    }

    /// <summary>
    /// Spawns one or more pooled projectile effects along the current aim direction.
    /// </summary>
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

            Vector2 shotDirection = ApplySpread(currentAimDirection);
            hitscanProjectile.Fire(gameObject, origin, shotDirection, projectile, debugTraceDuration);
        }
    }

    /// <summary>
    /// Spawns a pooled muzzle flash aligned to the current aim direction.
    /// </summary>
    private void SpawnMuzzleFlash()
    {
        if (globalObjectPooler == null || equippedFirearm == null || muzzleFlashPrefab == null)
            return;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        float angle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg + muzzleFlashRotationOffset;
        MuzzleFlashEffect flashEffect = globalObjectPooler.Spawn(
            muzzleFlashPrefab,
            origin,
            Quaternion.Euler(0f, 0f, angle),
            firePoint,
            muzzleFlashPoolPrewarm);

        if (flashEffect != null)
            flashEffect.Play(equippedFirearm.MuzzleFlashSize, equippedFirearm.MuzzleFlashDuration);
    }

    /// <summary>
    /// Applies weapon spread to a shot direction and returns a normalized result.
    /// </summary>
    private Vector2 ApplySpread(Vector2 baseDirection)
    {
        if (baseDirection.sqrMagnitude <= MinimumDirectionSqr)
            return Vector2.up;

        float spread = Mathf.Max(0f, equippedFirearm != null ? equippedFirearm.Spread : 0f);
        if (isAccurate)
            spread *= 1f - Mathf.Clamp01(equippedFirearm.Accuracy / 100f);

        if (spread <= 0f)
            return baseDirection.normalized;

        float halfAngle = spread * 0.5f;
        float angleOffset = Random.Range(-halfAngle, halfAngle);
        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float finalAngle = baseAngle + angleOffset;
        float radians = finalAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    /// <summary>
    /// Rotates the current aim direction toward the current target point.
    /// </summary>
    private void UpdateAimDirection()
    {
        Vector2 aimPoint = ResolveCurrentAimPoint();
        Vector2 targetDirection = aimPoint - ResolveAimOriginPosition();
        if (targetDirection.sqrMagnitude <= MinimumDirectionSqr)
            targetDirection = currentAimDirection.sqrMagnitude > MinimumDirectionSqr ? currentAimDirection : transform.up;

        float aimSpeed = equippedFirearm != null ? equippedFirearm.AimSpeed : defaultAimRotationSpeed;
        if (actorStaggerController != null)
            aimSpeed *= actorStaggerController.TurnSpeedMultiplier;

        currentAimDirection = RotateDirectionTowards(currentAimDirection, targetDirection.normalized, aimSpeed, Time.deltaTime);
    }

    /// <summary>
    /// Tracks whether the current aim has settled into the accurate stance.
    /// </summary>
    private void UpdateAccurateMode()
    {
        if (!isAiming || equippedFirearm == null || IsMoving())
        {
            accurateAimTimer = 0f;
            isAccurate = false;
            return;
        }

        float requiredAimTime = equippedFirearm.AimTime;
        if (requiredAimTime <= 0f)
        {
            accurateAimTimer = 0f;
            isAccurate = true;
            return;
        }

        accurateAimTimer += Time.deltaTime;
        isAccurate = accurateAimTimer >= requiredAimTime;
    }

    /// <summary>
    /// Rotates a direction vector by a delta angle in degrees.
    /// </summary>
    private static Vector2 RotateDirection(Vector2 direction, float angleDelta)
    {
        if (direction.sqrMagnitude <= MinimumDirectionSqr)
            direction = Vector2.up;

        float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float radians = (currentAngle + angleDelta) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

    /// <summary>
    /// Consumes the accurate stance after a shot that should reset aim commitment.
    /// </summary>
    private void ConsumeAccurateStanceAfterShot()
    {
        if (!isAccurate)
            return;

        accurateAimTimer = 0f;
        isAccurate = false;
    }

    /// <summary>
    /// Rebuilds the list of supported fire modes for the equipped weapon.
    /// </summary>
    private void RebuildAvailableFireModes()
    {
        availableFireModes.Clear();
        if (equippedFirearm == null)
        {
            currentFireMode = FireMode.None;
            return;
        }

        for (int i = 0; i < FireModeCycleOrder.Length; i++)
        {
            FireMode mode = FireModeCycleOrder[i];
            if (equippedFirearm.SupportsFireMode(mode))
                availableFireModes.Add(mode);
        }

        currentFireMode = ResolvePreferredFireMode();
    }

    /// <summary>
    /// Resolves the default fire mode preference for the equipped weapon.
    /// </summary>
    private FireMode ResolvePreferredFireMode()
    {
        if (availableFireModes.Count <= 0)
            return FireMode.None;

        FireMode firearmClassPreference = equippedFirearm != null
            ? equippedFirearm.Class switch
            {
                FirearmClass.Rifle => FireMode.FullAuto,
                FirearmClass.AssaultRifle => FireMode.FullAuto,
                FirearmClass.SMG => FireMode.FullAuto,
                FirearmClass.Shotgun => FireMode.SemiAuto,
                FirearmClass.PumpShotgun => FireMode.Pump,
                FirearmClass.SemiAutoShotgun => FireMode.SemiAuto,
                _ => FireMode.SemiAuto
            }
            : FireMode.SemiAuto;

        if (availableFireModes.Contains(firearmClassPreference))
            return firearmClassPreference;

        if (availableFireModes.Contains(FireMode.FullAuto))
            return FireMode.FullAuto;

        return availableFireModes[0];
    }

    /// <summary>
    /// Resolves the best current aim point based on visibility and last known information.
    /// </summary>
    private Vector2 ResolveCurrentAimPoint()
    {
        if (hasClearVisualOnTarget && enemyVisionAI != null)
        {
            PlayerVisibility targetVisibility = enemyVisionAI.TargetVisibilityComponent;
            if (targetVisibility != null)
                return targetVisibility.SamplePosition;

            if (currentTarget != null)
                return currentTarget.position;
        }

        return lastSeenTargetPosition != Vector2.zero
            ? lastSeenTargetPosition
            : currentTarget != null ? (Vector2)currentTarget.position : (Vector2)transform.position;
    }

    /// <summary>
    /// Resolves the transform position used as the aim origin.
    /// </summary>
    private Vector2 ResolveAimOriginPosition()
    {
        if (aimOrigin != null)
            return aimOrigin.position;

        if (firePoint != null)
            return firePoint.position;

        return transform.position;
    }

    /// <summary>
    /// Returns whether the current body position is within a threshold of the target point.
    /// </summary>
    private bool IsNearPosition(Vector2 position, float threshold)
    {
        Vector2 delta = position - CurrentPosition;
        return delta.sqrMagnitude <= threshold * threshold;
    }

    /// <summary>
    /// Returns whether the combatant is moving faster than the stationary threshold.
    /// </summary>
    private bool IsMoving()
    {
        if (enemyMovementController != null)
            return enemyMovementController.CurrentMovementSpeed > stationarySpeedThreshold;

        if (movementBody != null)
            return movementBody.linearVelocity.magnitude > stationarySpeedThreshold;

        return false;
    }

    /// <summary>
    /// Resolves the effective preferred combat range for the current projectile.
    /// </summary>
    private float ResolveEffectiveCombatRange()
    {
        if (currentProjectile == null)
            return MinimumRange;

        return Mathf.Max(MinimumRange, currentProjectile.Range * effectiveCombatRangeMultiplier);
    }
}
