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
public partial class EnemyCombatantAI : MonoBehaviour
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

    private EnemyMovementController enemyMovementController;
    private EnemyVisionAI enemyVisionAI;
    private CoverUser2D coverUser;

    [FoldoutGroup("References")]
    [SerializeField] private Transform firePoint;

    [FoldoutGroup("References")]
    [SerializeField] private Transform aimOrigin;

    private Rigidbody2D movementBody;
    private ActorStaggerController actorStaggerController;

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

    private GlobalObjectPooler globalObjectPooler;
    private WorldSfxManager worldSfxManager;

    private HitscanProjectile projectilePrefab;

    private int projectilePoolPrewarm = 16;

    private MuzzleFlashEffect muzzleFlashPrefab;

    private int muzzleFlashPoolPrewarm = 8;

    private float muzzleFlashRotationOffset;

    private bool debugCombat;

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

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsFlashbanged => isFlashbanged;

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
    private bool isFlashbanged;
    private float flashbangAimlessRotationSpeed;

    private Vector2 CurrentPosition => movementBody != null ? movementBody.position : (Vector2)transform.position;
}
