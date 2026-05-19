using System;
using System.Collections.Generic;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;
using Breezeblocks.WeaponSystem;

public enum EnemyCombatIntelligence
{
    Marksman,
    Sharpshooter,
    Expert
}

public enum EnemyCombatMode
{
    None,
    CombatDelay,
    MovingToCover,
    HoldingFallback,
    Engaging,
    LostSightLinger
}

internal enum EnemyCombatWeaponPolicy
{
    Immediate,
    AccurateOnly,
    BurstOnAccurate
}

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[RequireComponent(typeof(EnemyVisionAI))]
[RequireComponent(typeof(CoverUser2D))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Combatant AI")]
public class EnemyCombatantAI : MonoBehaviour
{
    private static readonly FireMode[] FireModeCycleOrder =
    {
        FireMode.SemiAuto,
        FireMode.FullAuto,
        FireMode.Burst,
        FireMode.Pump,
        FireMode.BoltAction
    };

    private const float MinimumInterval = 0.02f;
    private const float MinimumDirectionSqr = 0.0001f;
    private const float MinimumRange = 0.01f;
    private const int MinimumCoverResults = 4;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyVisionAI enemyVisionAI;

    [FoldoutGroup("References")]
    [SerializeField] private CoverUser2D coverUser;

    [FoldoutGroup("References")]
    [SerializeField] private Transform firePoint;

    [FoldoutGroup("References")]
    [SerializeField] private Transform aimOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private Rigidbody2D movementBody;

    [FoldoutGroup("References")]
    [SerializeField] private ActorStaggerController actorStaggerController;

    private bool startArmed = true;

    private FirearmData startingFirearm;

    private ProjectileData startingProjectile;

    private int startingLoadedAmmo = -1;

    private int startingReserveAmmo = -1;

    private FirearmData stowedFirearm;

    private ProjectileData stowedProjectile;

    private int stowedLoadedAmmo = -1;

    private int stowedReserveAmmo = -1;

    private EnemyCombatIntelligence combatIntelligence = EnemyCombatIntelligence.Marksman;

    private float combatDelay = 1.25f;

    private float lostSightLingerDuration = 2f;

    private float lostSightShootingLingerDuration = 0.75f;

    private float combatDecisionInterval = 0.1f;

    private float stationarySpeedThreshold = 0.05f;

    private float effectiveCombatRangeMultiplier = 0.9f;

    private float fireAngleTolerance = 8f;

    [FoldoutGroup("Combat")]
    [SerializeField] private Transform noCoverFallbackPoint;

    private float coverDetectionRange = 8f;

    private LayerMask coverDetectionMask = ~0;

    private string coverTag = "Cover";

    private float coverReevaluationInterval = 0.35f;

    private float coverArrivalDistance = 0.35f;

    private float coverRepositionDotThreshold = 0.2f;

    private int maxCoverResults = 16;

    private float defaultAimRotationSpeed = 720f;

    private float debugTraceDuration = 0.1f;

    private float marksmanAccurateDecisionInterval = 1f;

    private float marksmanAccurateModeChance = 0.5f;

    private int rifleBurstShotsMinimum = 2;

    private int rifleBurstShotsMaximum = 4;

    [FoldoutGroup("Pooling"), Tooltip("Optional explicit reference to the shared global pooler. If empty, the singleton instance is used.")]
    [SerializeField] private GlobalObjectPooler globalObjectPooler;

    [FoldoutGroup("Audio"), Tooltip("Optional explicit reference to the pooled world SFX manager. If empty, the singleton instance is used.")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    private HitscanProjectile projectilePrefab;

    private int projectilePoolPrewarm = 16;

    private MuzzleFlashEffect muzzleFlashPrefab;

    private int muzzleFlashPoolPrewarm = 8;

    private float muzzleFlashRotationOffset;

    private bool debugCombat;

    private bool drawCombatGizmos = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDrafted => isDrafted;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool ShouldIgnoreNoiseEvents => isDrafted && hasClearVisualOnTarget;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EnemyCombatMode CurrentCombatMode => currentCombatMode;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public FirearmData EquippedFirearm => equippedFirearm;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public ProjectileData CurrentProjectile => currentProjectile;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentLoadedAmmo => currentLoadedAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int CurrentReserveAmmo => currentReserveAmmo;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsReloading => isReloading;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAiming => isAiming;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAccurate => isAccurate;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentAimDirection => currentAimDirection;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastSeenTargetPosition => lastSeenTargetPosition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasClearVisualOnTarget => hasClearVisualOnTarget;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public CombatCover2D CurrentSelectedCover => currentSelectedCover;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentSelectedCoverPoint => currentSelectedCoverPoint;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CombatDelayRemaining => isDrafted && currentCombatMode == EnemyCombatMode.CombatDelay
        ? Mathf.Max(0f, combatDelayEndTime - Time.time)
        : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float LostSightLingerRemaining => isDrafted && currentCombatMode == EnemyCombatMode.LostSightLinger
        ? Mathf.Max(0f, lostSightLingerEndTime - Time.time)
        : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float LostSightShootingRemaining => isDrafted && currentCombatMode == EnemyCombatMode.LostSightLinger
        ? Mathf.Max(0f, lostSightShootingEndTime - Time.time)
        : 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int PlannedBurstShotsRemaining => plannedBurstShotsRemaining;

    private Transform currentTarget;
    private FirearmData equippedFirearm;
    private ProjectileData currentProjectile;
    private EnemyCombatMode currentCombatMode;
    private float combatDelayEndTime;
    private float lostSightLingerEndTime;
    private float lostSightShootingEndTime;
    private float nextCombatDecisionTime;
    private float nextCoverEvaluationTime;
    private float nextAllowedFireTime;
    private float accurateAimTimer;
    private float nextReloadTickTime;
    private float magazineReloadEndSequenceTime;
    private float nextMarksmanAccurateDecisionTime;
    private Vector2 currentAimDirection = Vector2.up;
    private Vector2 lastSeenTargetPosition;
    private Vector2 currentSelectedCoverPoint;
    private Vector2 currentSelectedCoverProtectionDirection;
    private CombatCover2D currentSelectedCover;
    private readonly List<FireMode> availableFireModes = new();
    private FireMode currentFireMode;
    private int currentLoadedAmmo;
    private int currentReserveAmmo;
    private int plannedBurstShotsRemaining;
    private bool isDrafted;
    private bool isAiming;
    private bool isAccurate;
    private bool isReloading;
    private bool magazineReloadSequencePlayed;
    private bool hasClearVisualOnTarget;
    private bool marksmanWantsAccurateShots;
    private bool weaponEquippedForAwareness;
    private Collider2D[] coverResults;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        EnsureCoverBuffer();
        ResolveGlobalObjectPooler();
        ResolveWorldSfxManager();
        RegisterPooledPrefabs();
    }

    private void Start()
    {
        ApplyWeaponReadinessForState(enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Idle, isInitialState: true);
    }

    private void OnEnable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged += HandleMovementStateChanged;
    }

    private void OnDisable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged -= HandleMovementStateChanged;
    }

    private void OnValidate()
    {
        ClampSettings();
        CacheReferences();
        EnsureCoverBuffer();
    }

    private void Update()
    {
        UpdateReloadState();
        SyncVisionState();

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
        drawCombatGizmos = settings.DrawCombatGizmos;

        ClampSettings();
        EnsureCoverBuffer();
        ResetStowedWeaponLoadout();
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void DebugEquipStartingWeapon()
    {
        EquipConfiguredWeaponLoadout();
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void DebugDraftCombat()
    {
        BeginDraftedCombat();
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug Actions")]
    public void DebugEndCombat()
    {
        EndDraftedCombat(clearCoverState: true);
        enemyMovementController?.ReturnToStart();
    }

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

    public bool HandleDetectedTargetLost(Vector2 lastKnownPosition)
    {
        if (!isDrafted || !enabled)
            return false;

        lastSeenTargetPosition = lastKnownPosition;
        EnterLostSightLinger();
        return true;
    }

    public void HandleInvestigativeNoiseHeard(NoiseEvent noiseEvent)
    {
        if (!isDrafted || hasClearVisualOnTarget)
            return;

        if (debugCombat)
        {
            Debug.Log($"{name} heard a new investigative noise during combat and is dropping back into search behavior.", this);
        }

        EndDraftedCombat(clearCoverState: true);
    }

    private void HandleMovementStateChanged(EnemyState previousState, EnemyState newState)
    {
        if (previousState == EnemyState.Detected && newState != EnemyState.Detected)
            EndDraftedCombat(clearCoverState: true);

        ApplyWeaponReadinessForState(newState, isInitialState: false);

        if (newState == EnemyState.Detected)
        {
            BeginDraftedCombat();
            return;
        }
    }

    private void ApplyWeaponReadinessForState(EnemyState state, bool isInitialState)
    {
        if (isInitialState)
        {
            ResetStowedWeaponLoadout();
            weaponEquippedForAwareness = false;
        }

        if (RequiresReadiedWeapon(state))
        {
            bool rememberAwarenessDraw = !startArmed && state != EnemyState.Alert && state != EnemyState.Detected;
            EnsureWeaponEquipped(rememberAwarenessDraw);
            return;
        }

        if (IsCalmState(state))
        {
            if (startArmed)
            {
                EnsureWeaponEquipped(false);
                return;
            }

            if (isInitialState)
            {
                HolsterCurrentWeapon();
                return;
            }

            if (weaponEquippedForAwareness)
                HolsterCurrentWeapon();
        }
    }

    private static bool RequiresReadiedWeapon(EnemyState state)
    {
        return state == EnemyState.Suspicious ||
               state == EnemyState.Searching ||
               state == EnemyState.Alert ||
               state == EnemyState.Detected;
    }

    private static bool IsCalmState(EnemyState state)
    {
        return state == EnemyState.Idle ||
               state == EnemyState.Patrol ||
               state == EnemyState.ReturningToStart;
    }

    private void ResetStowedWeaponLoadout()
    {
        stowedFirearm = startingFirearm;
        stowedProjectile = startingProjectile;
        stowedLoadedAmmo = startingLoadedAmmo;
        stowedReserveAmmo = startingReserveAmmo;
    }

    private void EnsureWeaponEquipped(bool rememberAwarenessDraw)
    {
        if (equippedFirearm == null || currentProjectile == null)
            EquipConfiguredWeaponLoadout();

        if (equippedFirearm == null || currentProjectile == null)
            return;

        if (rememberAwarenessDraw)
            weaponEquippedForAwareness = true;
        else
            weaponEquippedForAwareness = false;
    }

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

    private void StoreCurrentWeaponLoadout()
    {
        if (equippedFirearm == null)
            return;

        stowedFirearm = equippedFirearm;
        stowedProjectile = currentProjectile;
        stowedLoadedAmmo = currentLoadedAmmo;
        stowedReserveAmmo = currentReserveAmmo;
    }

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

    private void SyncVisionState()
    {
        currentTarget = enemyVisionAI != null ? enemyVisionAI.TargetTransform : null;
        hasClearVisualOnTarget = enemyVisionAI != null && enemyVisionAI.CanCurrentlyDetectTarget && enemyVisionAI.HasLineOfSight;

        if (hasClearVisualOnTarget)
            lastSeenTargetPosition = ResolveCurrentAimPoint();
    }

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

    private void EnterLostSightLinger()
    {
        currentCombatMode = EnemyCombatMode.LostSightLinger;
        lostSightLingerEndTime = Time.time + lostSightLingerDuration;
        lostSightShootingEndTime = Time.time + lostSightShootingLingerDuration;
        enemyMovementController?.HoldDetectedPosition();
        enemyMovementController?.SetFacingPoint(lastSeenTargetPosition);
    }

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

    private bool ResolveMarksmanFireIntent()
    {
        if (coverUser != null && coverUser.IsInCover)
        {
            if (Time.time >= nextMarksmanAccurateDecisionTime)
            {
                marksmanWantsAccurateShots = UnityEngine.Random.value <= marksmanAccurateModeChance;
                nextMarksmanAccurateDecisionTime = Time.time + marksmanAccurateDecisionInterval;
            }

            return !marksmanWantsAccurateShots || isAccurate;
        }

        return true;
    }

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
            EnemyCombatWeaponPolicy.AccurateOnly => alwaysPreferAccurate ? isAccurate : isAccurate,
            EnemyCombatWeaponPolicy.BurstOnAccurate => ResolveBurstFireIntent(alwaysPreferAccurate),
            _ => false
        };
    }

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
        plannedBurstShotsRemaining = UnityEngine.Random.Range(minShots, maxShots + 1) - 1;
        return true;
    }

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

    private bool CanContinueShootingDuringLostSightLinger()
    {
        return currentCombatMode == EnemyCombatMode.LostSightLinger &&
               Time.time < lostSightShootingEndTime;
    }

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

    private void FirePumpShot()
    {
        if (!TryConsumeCurrentRound(out ProjectileData projectile))
            return;

        int pellets = Mathf.Max(1, equippedFirearm.PelletCount);
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
        projectile = currentProjectile;
        if (equippedFirearm == null || projectile == null || currentLoadedAmmo <= 0)
            return false;

        currentLoadedAmmo--;
        SpawnMuzzleFlash();
        PlayShotSequenceSfx();
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

            Vector2 shotDirection = ApplySpread(currentAimDirection);
            hitscanProjectile.Fire(gameObject, origin, shotDirection, projectile, debugTraceDuration);
        }
    }

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
        float angleOffset = UnityEngine.Random.Range(-halfAngle, halfAngle);
        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float finalAngle = baseAngle + angleOffset;
        float radians = finalAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }

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

    private void ConsumeAccurateStanceAfterShot()
    {
        if (!isAccurate)
            return;

        accurateAimTimer = 0f;
        isAccurate = false;
    }

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

    private Vector2 ResolveAimOriginPosition()
    {
        if (aimOrigin != null)
            return aimOrigin.position;

        if (firePoint != null)
            return firePoint.position;

        return transform.position;
    }

    private bool IsNearPosition(Vector2 position, float threshold)
    {
        Vector2 delta = position - CurrentPosition;
        return delta.sqrMagnitude <= threshold * threshold;
    }

    private bool IsMoving()
    {
        if (enemyMovementController != null)
            return enemyMovementController.CurrentMovementSpeed > stationarySpeedThreshold;

        if (movementBody != null)
            return movementBody.linearVelocity.magnitude > stationarySpeedThreshold;

        return false;
    }

    private float ResolveEffectiveCombatRange()
    {
        if (currentProjectile == null)
            return MinimumRange;

        return Mathf.Max(MinimumRange, currentProjectile.Range * effectiveCombatRangeMultiplier);
    }

    private int ResolveCurrentAmmoCapacity()
    {
        return equippedFirearm != null ? equippedFirearm.AmmoCapacity : 0;
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

    private void CacheReferences()
    {
        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyVisionAI == null)
            enemyVisionAI = GetComponent<EnemyVisionAI>();

        if (coverUser == null)
            coverUser = GetComponent<CoverUser2D>();

        if (movementBody == null)
            movementBody = GetComponent<Rigidbody2D>();

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        if (firePoint == null)
            firePoint = transform;

        if (aimOrigin == null)
            aimOrigin = firePoint != null ? firePoint : transform;
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

    private void PlayMagazineReloadStartSfx()
    {
        if (equippedFirearm == null || equippedFirearm.ReloadStyle != ReloadType.Magazine)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, equippedFirearm.ReloadStartSfx, equippedFirearm.ReloadNoiseType);
    }

    private void PlayMagazineReloadEndSequenceSfx()
    {
        if (equippedFirearm == null || equippedFirearm.ReloadStyle != ReloadType.Magazine)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        Vector3 origin = transform.position;
        worldSfxManager.PlayClipSetAt(origin, equippedFirearm.ReloadEndSfx, equippedFirearm.ReloadNoiseType, out float triggerDelay);
        worldSfxManager.PlayClipSetAt(origin, equippedFirearm.ReloadTriggerSfx, equippedFirearm.ReloadNoiseType, 1f, triggerDelay);
    }

    private void PlayBulletReloadSfx()
    {
        if (equippedFirearm == null || equippedFirearm.ReloadStyle != ReloadType.BulletPerBullet)
            return;

        ResolveWorldSfxManager();
        if (worldSfxManager == null)
            return;

        worldSfxManager.PlayClipSetAt(transform.position, equippedFirearm.BulletReloadSfx, equippedFirearm.ReloadNoiseType);
    }

    private void EnsureCoverBuffer()
    {
        int requiredSize = Mathf.Max(MinimumCoverResults, maxCoverResults);
        if (coverResults == null || coverResults.Length != requiredSize)
            coverResults = new Collider2D[requiredSize];
    }

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

    private Vector2 CurrentPosition => movementBody != null ? movementBody.position : (Vector2)transform.position;

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

    private void OnDrawGizmosSelected()
    {
        if (!drawCombatGizmos)
            return;

        Vector3 origin = aimOrigin != null ? aimOrigin.position : transform.position;

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(origin, coverDetectionRange);

        if (currentSelectedCover != null)
        {
            Gizmos.color = new Color(0.25f, 1f, 0.7f, 0.9f);
            Gizmos.DrawLine(origin, currentSelectedCoverPoint);
            Gizmos.DrawSphere(currentSelectedCoverPoint, 0.14f);
            Gizmos.DrawLine(currentSelectedCoverPoint, currentSelectedCoverPoint + (currentSelectedCoverProtectionDirection.normalized * 0.8f));
        }

        Gizmos.color = new Color(1f, 0.85f, 0.25f, 0.9f);
        Gizmos.DrawLine(origin, origin + (Vector3)(currentAimDirection.normalized * 1.25f));

        if (noCoverFallbackPoint != null)
        {
            Gizmos.color = new Color(0.7f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(noCoverFallbackPoint.position, 0.18f);
        }
    }
}
