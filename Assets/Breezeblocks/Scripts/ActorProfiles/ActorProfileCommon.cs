using System;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class ActorHealthSettings
{
    [FoldoutGroup("Health"), MinValue(0f)]
    public float MaxHealth = 100f;

    [FoldoutGroup("Health")]
    public bool IsInvincible;
}

[Serializable]
public class ActorStaggerSettings
{
    [FoldoutGroup("Stagger")]
    public bool EnableStagger = true;

    [FoldoutGroup("Stagger"), MinValue(0f), SuffixLabel("u/s", true)]
    public float StaggeredMoveSpeed = 1.2f;

    [FoldoutGroup("Stagger"), Range(0f, 100f), SuffixLabel("%", true)]
    public float TurnSpeedReductionPercent = 40f;
}

[Serializable]
public class PlayerMovementSettings
{
    private const int SpeedLevelsCount = 10;

    [FoldoutGroup("Speed Levels"), LabelText("Walk Speed Levels")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, NumberOfItemsPerPage = SpeedLevelsCount)]
    public float[] WalkSpeedLevels =
    {
        1f, 1.2f, 1.4f, 1.6f, 1.8f, 2f, 2.2f, 2.4f, 2.6f, 2.8f
    };

    [FoldoutGroup("Movement"), MinValue(1), MaxValue(SpeedLevelsCount)]
    public int SelectedSpeedLevel = 1;

    [FoldoutGroup("Movement"), MinValue(0f), SuffixLabel("u/s^2", true)]
    public float Acceleration = 28f;

    [FoldoutGroup("Movement"), MinValue(0f), SuffixLabel("u/s^2", true)]
    public float Deceleration = 34f;

    [FoldoutGroup("Movement"), MinValue(0f), SuffixLabel("x", true)]
    public float SprintSpeedMultiplier = 1.5f;

    [FoldoutGroup("Movement")]
    public bool NormalizeInput = true;

    [FoldoutGroup("Physics")]
    public bool ForceZeroGravity = true;

    [FoldoutGroup("Physics")]
    public bool FreezeRotationZ = true;
}

[Serializable]
public class PlayerNoiseSettings
{
    private const float MinimumNoiseEventInterval = 0.02f;

    [FoldoutGroup("Noise Profile"), Range(0f, 1f)]
    public float IdleNoise;

    [FoldoutGroup("Noise Profile"), Range(0f, 1f)]
    public float WalkNoiseAtMinSpeed = 0.35f;

    [FoldoutGroup("Noise Profile"), Range(0f, 1f)]
    public float WalkNoiseAtMaxSpeed = 0.75f;

    [FoldoutGroup("Noise Profile"), Range(0f, 1f)]
    public float SprintNoiseAtMaxSpeed = 1f;

    [FoldoutGroup("Noise Events")]
    public bool EmitMovementNoiseEvents = true;

    [FoldoutGroup("Noise Events"), ShowIf(nameof(EmitMovementNoiseEvents)), MinValue(MinimumNoiseEventInterval), SuffixLabel("s", true)]
    public float MovementNoiseEventInterval = 0.2f;

    [FoldoutGroup("Noise Events"), ShowIf(nameof(EmitMovementNoiseEvents)), MinValue(0f)]
    public float MinimumMovementNoiseToEmit = 0.05f;

    [FoldoutGroup("Noise Events"), ShowIf(nameof(EmitMovementNoiseEvents)), MinValue(0f)]
    public float MovementNoiseIntensityMultiplier = 1f;

    [FoldoutGroup("Noise Events"), ShowIf(nameof(EmitMovementNoiseEvents))]
    public NoiseType WalkMovementNoiseType = NoiseType.Common;

    [FoldoutGroup("Noise Events"), ShowIf(nameof(EmitMovementNoiseEvents))]
    public NoiseType SprintMovementNoiseType = NoiseType.Common;
}

[Serializable]
public class PlayerNoiseEmitterSettings
{
    [FoldoutGroup("Emission"), MinValue(0f)]
    public float IntensityMultiplier = 1f;

    [FoldoutGroup("Debug")]
    public bool DebugLogging;
}

[Serializable]
public class PlayerVisibilitySettings
{
    private const float MinimumSampleInterval = 0.02f;

    [FoldoutGroup("Visibility"), MinValue(MinimumSampleInterval), SuffixLabel("s", true)]
    public float VisibilitySampleInterval = 0.05f;

    [FoldoutGroup("Visibility"), MinValue(0f)]
    public float VisibilityIncreaseSpeed = 3f;

    [FoldoutGroup("Visibility"), MinValue(0f)]
    public float VisibilityDecreaseSpeed = 2f;

    [FoldoutGroup("Visibility"), Range(0f, 1f)]
    public float MinimumVisibility;

    [FoldoutGroup("Visibility"), Range(0f, 1f)]
    public float MaximumVisibility = 1f;

    [FoldoutGroup("Visibility"), MinValue(0f), SuffixLabel("s", true)]
    public float MuzzleFlashVisibilityDuration = 0.35f;

    [FoldoutGroup("Debug")]
    public bool DebugDraw;
}

[Serializable]
public class PlayerVisionLightSettings
{
    [FoldoutGroup("Shape"), MinValue(0f)]
    public float MaxViewRadius = 8f;

    [FoldoutGroup("Shape"), MinValue(0f)]
    public float MinViewRadius = 3f;

    [FoldoutGroup("Shape"), Range(0f, 360f)]
    public float ViewAngle = 120f;

    [FoldoutGroup("Shape"), Range(0f, 1f)]
    public float InnerRadiusFraction = 0.5f;

    [FoldoutGroup("Shape"), Range(0f, 1f)]
    public float InnerAngleFraction = 0.3f;

    [FoldoutGroup("Orientation")]
    public bool LookAtMouse = true;

    [FoldoutGroup("Orientation"), MinValue(0f)]
    public float RotationSmoothing = 720f;

    [FoldoutGroup("Orientation")]
    public float RotationOffset = -90f;

    [FoldoutGroup("Vision Level"), Range(0f, 1f)]
    public float VisionLevel01 = 1f;

    [FoldoutGroup("Fallback"), ShowIf(nameof(UsesExternalDirection))]
    public Vector2 ExternalDirection = Vector2.right;

    private bool UsesExternalDirection => !LookAtMouse;
}

[Serializable]
public class PlayerStaminaSettings
{
    [FoldoutGroup("Stamina"), MinValue(0f)]
    public float MaxStamina = 100f;

    [FoldoutGroup("Stamina"), MinValue(0f)]
    public float SprintDrainPerSecond = 20f;

    [FoldoutGroup("Stamina"), MinValue(0f)]
    public float RegenerationPerSecond = 32f;

    [FoldoutGroup("Stamina"), MinValue(0f), SuffixLabel("s", true)]
    public float RegenerationDelayAfterSpend = 1f;

    [FoldoutGroup("Stamina"), Range(0f, 100f), SuffixLabel("%", true)]
    public float StaggerStaminaLossPercent = 12f;

    [FoldoutGroup("Stamina"), MinValue(0f)]
    public float MovementThreshold = 0.05f;
}

[Serializable]
public class EnemyMovementSettings
{
    private const float MinimumDistance = 0.01f;
    private const float MinimumInterval = 0.02f;

    [FoldoutGroup("State")]
    public EnemyState StartingState = EnemyState.Idle;

    [FoldoutGroup("State"), InlineProperty, LabelText("Start Facing")]
    public EnemyFacingSettings StartingPointFacing = new();

    [FoldoutGroup("Movement Speeds"), MinValue(0f)]
    public float WalkSpeed = 1.5f;

    [FoldoutGroup("Movement Speeds"), MinValue(0f)]
    public float RunSpeed = 3.25f;

    [FoldoutGroup("Movement Speeds"), MinValue(0f)]
    public float SprintSpeed = 5f;

    [FoldoutGroup("Acceleration"), MinValue(0f)]
    public float Acceleration = 10f;

    [FoldoutGroup("Acceleration"), MinValue(0f)]
    public float Deceleration = 14f;

    [FoldoutGroup("Acceleration"), MinValue(MinimumDistance)]
    public float StoppingDistance = 0.2f;

    [FoldoutGroup("Acceleration"), MinValue(MinimumDistance)]
    public float SlowdownDistance = 0.8f;

    [FoldoutGroup("Acceleration"), MinValue(0f)]
    public float MinimumMoveSpeed = 0.05f;

    [FoldoutGroup("Rotation")]
    public bool UseCustomRotation = true;

    [FoldoutGroup("Rotation"), ShowIf(nameof(UseCustomRotation)), MinValue(0f)]
    public float RotationSpeed = 360f;

    [FoldoutGroup("Rotation"), ShowIf(nameof(UseCustomRotation)), SuffixLabel("deg", true)]
    public float RotationAngleOffset = -90f;

    [FoldoutGroup("Rotation"), ShowIf(nameof(UseCustomRotation))]
    public bool FaceMovementDirection = true;

    [FoldoutGroup("Rotation"), ShowIf(nameof(UseCustomRotation))]
    public bool FaceTargetWhenDetected = true;

    [FoldoutGroup("Rotation"), ShowIf(nameof(UseCustomRotation))]
    public bool PreferPathSteeringDirection = true;

    [FoldoutGroup("Rotation"), ShowIf(nameof(UseCustomRotation))]
    public bool LockRotationWhenIdle = true;

    [FoldoutGroup("Patrol"), EnumToggleButtons]
    public EnemyPatrolMode PatrolMode = EnemyPatrolMode.Loop;

    [FoldoutGroup("Organization")]
    public bool DetachReferencedPointsToWorldOnAwake = true;

    [FoldoutGroup("Search or Suspicious")]
    public bool ReturnToStartAfterTemporaryStates = true;

    [FoldoutGroup("Search or Suspicious")]
    public bool Investigate = true;

    [FoldoutGroup("Search or Suspicious"), ShowIf(nameof(ReturnToStartAfterTemporaryStates))]
    public EnemySpeedType ReturnToStartSpeedType = EnemySpeedType.Walk;

    [FoldoutGroup("Alert")]
    public bool EnterAlertStateWhenTargetLost = true;

    [FoldoutGroup("Alert")]
    public bool ChaseTarget = true;

    [FoldoutGroup("Alert"), MinValue(0f), SuffixLabel("s", true)]
    public float AlertNoiseFocusDuration = 2f;

    [FoldoutGroup("Alert"), MinValue(0f), SuffixLabel("s", true)]
    public float AlertTargetLostDuration = 3f;

    [FoldoutGroup("Look Around"), MinValue(0f), SuffixLabel("s", true)]
    public float DefaultLookAroundDuration = 2.5f;

    [FoldoutGroup("Look Around"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float LookAroundTurnInterval = 0.5f;

    [FoldoutGroup("Look Around"), MinValue(0f)]
    public float LookAroundRotationSpeed = 360f;

    [FoldoutGroup("Look Around"), Range(0f, 360f)]
    public float RandomLookAngleRange = 180f;

    [FoldoutGroup("Itinerary")]
    public bool UseItinerary;

    [FoldoutGroup("Itinerary"), ShowIf(nameof(UseItinerary))]
    public bool LoopItinerary = true;

    [FoldoutGroup("Detection Behavior"), EnumToggleButtons]
    public EnemyDetectionBehavior DetectionBehavior = EnemyDetectionBehavior.ChasePlayer;

    [FoldoutGroup("Detection Behavior")]
    public bool SearchLastKnownTargetPositionWhenTargetLost = true;

    [FoldoutGroup("Detection Behavior"), ShowIf(nameof(ShouldShowMissingFleeFallback))]
    public EnemyDetectionBehavior MissingFleePointFallbackBehavior = EnemyDetectionBehavior.StandStill;

    [FoldoutGroup("Fleeing")]
    public bool CanFlee = true;

    [FoldoutGroup("Fleeing"), ShowIf(nameof(CanFlee))]
    public bool StayAtFleePointForever = true;

    [FoldoutGroup("Fleeing"), ShowIf(nameof(CanFlee)), MinValue(MinimumDistance)]
    public float FleeStoppingDistance = 0.2f;

    [FoldoutGroup("Fleeing"), ShowIf(nameof(CanFlee))]
    public bool DisableHearingAfterFlee = true;

    [FoldoutGroup("Fleeing"), ShowIf(nameof(CanFlee))]
    public bool DisableVisionAfterFlee;

    [FoldoutGroup("Rigidbody")]
    public bool UseMovePosition = true;

    [FoldoutGroup("Rigidbody")]
    public bool UseVelocityMovement;

    [FoldoutGroup("Rigidbody")]
    public bool ApplyRecommendedRigidbodySettings = true;

    [FoldoutGroup("Rigidbody"), ShowIf(nameof(ApplyRecommendedRigidbodySettings))]
    public bool ForceZeroGravity = true;

    [FoldoutGroup("Rigidbody"), ShowIf(nameof(ApplyRecommendedRigidbodySettings))]
    public RigidbodyInterpolation2D RecommendedInterpolation = RigidbodyInterpolation2D.Interpolate;

    [FoldoutGroup("Rigidbody"), ShowIf(nameof(ApplyRecommendedRigidbodySettings))]
    public CollisionDetectionMode2D RecommendedCollisionDetection = CollisionDetectionMode2D.Continuous;

    private bool ShouldShowMissingFleeFallback =>
        DetectionBehavior == EnemyDetectionBehavior.FleeToPoint && !CanFlee;
}

[Serializable]
public class EnemyVisionSettings
{
    private const float MinimumVisionRange = 0.01f;
    private const float MinimumVisionCheckInterval = 0.02f;

    [FoldoutGroup("Vision"), MinValue(MinimumVisionRange)]
    public float VisionRange = 8f;

    [FoldoutGroup("Vision"), Range(0f, 360f)]
    public float VisionAngle = 90f;

    [FoldoutGroup("Vision")]
    public bool UseTransformUpAsForward = true;

    [FoldoutGroup("Vision"), ShowIf(nameof(ShouldShowLocalForwardDirection))]
    public Vector2 LocalForwardDirection = Vector2.up;

    [FoldoutGroup("Vision"), SuffixLabel("deg", true)]
    public float ForwardAngleOffset;

    [FoldoutGroup("Vision"), MinValue(MinimumVisionCheckInterval), SuffixLabel("s", true)]
    public float VisionCheckInterval = 0.1f;

    [FoldoutGroup("Vision")]
    public bool RequireLineOfSight = true;

    [FoldoutGroup("Vision"), ShowIf(nameof(RequireLineOfSight))]
    public LayerMask ObstacleMask;

    [FoldoutGroup("Detection"), Range(0f, 1f)]
    public float VisibilityThreshold = 0.35f;

    [FoldoutGroup("Detection"), MinValue(0f)]
    public float DetectionSpeed = 1.25f;

    [FoldoutGroup("Detection"), MinValue(0f)]
    public float DetectionDecaySpeed = 0.75f;

    [FoldoutGroup("Detection"), MinValue(0f)]
    public float FullDetectionRadius;

    [FoldoutGroup("Detection"), ShowIf(nameof(ShowFullDetectionSpeedMultiplier)), MinValue(0f)]
    public float FullDetectionSpeedMultiplier = 5f;

    [FoldoutGroup("Detection")]
    public bool ReactToFlashlight = true;

    [FoldoutGroup("Detection"), ShowIf(nameof(ReactToFlashlight)), MinValue(0f), SuffixLabel("s", true)]
    public float FlashlightSourceLostDuration = 2f;

    [FoldoutGroup("Detection"), ShowIf(nameof(ReactToFlashlight)), MinValue(0f)]
    public float FlashlightSourceUpdateDistance = 0.75f;

    [FoldoutGroup("Detection"), ShowIf(nameof(ReactToFlashlight)), Range(1, 9)]
    public int FlashlightVisibilitySampleCount = 5;

    [FoldoutGroup("Detection"), ShowIf(nameof(ReactToFlashlight)), MinValue(0f)]
    public float FlashlightVisibilitySurfaceOffset = 0.05f;

    [FoldoutGroup("Distance Bonus")]
    public bool UseDistanceDetectionMultiplier = true;

    [FoldoutGroup("Distance Bonus"), ShowIf(nameof(UseDistanceDetectionMultiplier)), MinValue(0f)]
    public float CloseRangeDistance = 1.5f;

    [FoldoutGroup("Distance Bonus"), ShowIf(nameof(UseDistanceDetectionMultiplier)), MinValue(0f)]
    public float NoBonusDistance = 6f;

    [FoldoutGroup("Distance Bonus"), ShowIf(nameof(UseDistanceDetectionMultiplier)), MinValue(1f)]
    public float CloseRangeDetectionMultiplier = 4f;

    [FoldoutGroup("Debug")]
    public bool DebugLogging;

    private bool ShouldShowLocalForwardDirection => !UseTransformUpAsForward;
    private bool ShowFullDetectionSpeedMultiplier => FullDetectionRadius > 0f;
}

[Serializable]
public class EnemyHearingSettings
{
    private const int MinimumObstructionChecks = 1;

    [FoldoutGroup("Hearing")]
    public bool EnableHearing = true;

    [FoldoutGroup("Hearing"), MinValue(0f)]
    public float LoudHearingRange = 15f;

    [FoldoutGroup("Hearing"), MinValue(0f)]
    public float CommonHearingRange = 8f;

    [FoldoutGroup("Hearing"), MinValue(0f)]
    public float SilentHearingRange = 3f;

    [FoldoutGroup("Hearing")]
    public bool IgnoreSilentSounds;

    [FoldoutGroup("Hearing"), MinValue(0f)]
    public float HearingThreshold = 0.2f;

    [FoldoutGroup("Accumulation"), MinValue(0f)]
    public float MaximumAccumulatedDetection = 1f;

    [FoldoutGroup("Accumulation"), MinValue(0f), SuffixLabel("s", true)]
    public float DetectionDecayDelay = 1f;

    [FoldoutGroup("Accumulation"), MinValue(0f)]
    public float DetectionDecayPerSecond = 0.2f;

    [FoldoutGroup("Distance Falloff"), MinValue(1f)]
    public float CloseDistanceMultiplier = 2f;

    [FoldoutGroup("Distance Falloff")]
    public AnimationCurve DistanceFalloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [FoldoutGroup("Obstruction")]
    public bool UseObstructionCheck = true;

    [FoldoutGroup("Obstruction"), ShowIf(nameof(UseObstructionCheck))]
    public LayerMask ObstructionLayerMask;

    [FoldoutGroup("Obstruction"), ShowIf(nameof(UseObstructionCheck)), Range(0f, 1f)]
    public float WallObstructionMultiplier = 0.2f;

    [FoldoutGroup("Obstruction"), ShowIf(nameof(UseObstructionCheck)), MinValue(MinimumObstructionChecks)]
    public int MaxObstructionChecks = 4;

    [FoldoutGroup("Obstruction"), ShowIf(nameof(UseObstructionCheck))]
    public bool StackObstructionMultipliers;

    [FoldoutGroup("Debug")]
    public bool DebugHearing;

}

[Serializable]
public class EnemyCombatSettings
{
    private const float MinimumInterval = 0.02f;
    private const int MinimumCoverResults = 4;

    [FoldoutGroup("Weapon Loadout")]
    [FormerlySerializedAs("AutoEquipStartingWeaponOnStart")]
    public bool StartArmed = true;

    [FoldoutGroup("Weapon Loadout"), AssetsOnly]
    public FirearmData StartingFirearm;

    [FoldoutGroup("Weapon Loadout"), AssetsOnly]
    public ProjectileData StartingProjectile;

    [FoldoutGroup("Weapon Loadout"), MinValue(-1)]
    public int StartingLoadedAmmo = -1;

    [FoldoutGroup("Weapon Loadout"), MinValue(-1)]
    public int StartingReserveAmmo = -1;

    [FoldoutGroup("Combat"), EnumToggleButtons]
    public EnemyCombatIntelligence CombatIntelligence = EnemyCombatIntelligence.Marksman;

    [FoldoutGroup("Combat"), MinValue(0f), SuffixLabel("s", true)]
    public float CombatDelay = 1.25f;

    [FoldoutGroup("Combat"), MinValue(0f), SuffixLabel("s", true)]
    public float LostSightLingerDuration = 2f;

    [FoldoutGroup("Combat"), MinValue(0f), SuffixLabel("s", true)]
    public float LostSightShootingLingerDuration = 0.75f;

    [FoldoutGroup("Combat"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float CombatDecisionInterval = 0.1f;

    [FoldoutGroup("Combat"), MinValue(0f)]
    public float StationarySpeedThreshold = 0.05f;

    [FoldoutGroup("Combat"), Range(0.1f, 1f)]
    public float EffectiveCombatRangeMultiplier = 0.9f;

    [FoldoutGroup("Combat"), Range(0f, 45f)]
    public float FireAngleTolerance = 8f;

    [FoldoutGroup("Cover"), MinValue(0f)]
    public float CoverDetectionRange = 8f;

    [FoldoutGroup("Cover")]
    public LayerMask CoverDetectionMask = ~0;

    [FoldoutGroup("Cover")]
    public string CoverTag = "Cover";

    [FoldoutGroup("Cover"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float CoverReevaluationInterval = 0.35f;

    [FoldoutGroup("Cover"), MinValue(0f)]
    public float CoverArrivalDistance = 0.35f;

    [FoldoutGroup("Cover"), Range(-1f, 1f)]
    public float CoverRepositionDotThreshold = 0.2f;

    [FoldoutGroup("Cover"), MinValue(MinimumCoverResults)]
    public int MaxCoverResults = 16;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    public float DefaultAimRotationSpeed = 720f;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    public float DebugTraceDuration = 0.1f;

    [FoldoutGroup("Marksman Behavior"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float MarksmanAccurateDecisionInterval = 1f;

    [FoldoutGroup("Marksman Behavior"), Range(0f, 1f)]
    public float MarksmanAccurateModeChance = 0.5f;

    [FoldoutGroup("Rifle Behavior"), MinValue(1)]
    public int RifleBurstShotsMinimum = 2;

    [FoldoutGroup("Rifle Behavior"), MinValue(1)]
    public int RifleBurstShotsMaximum = 4;

    [FoldoutGroup("Pooling"), AssetsOnly]
    public HitscanProjectile ProjectilePrefab;

    [FoldoutGroup("Pooling"), MinValue(0)]
    public int ProjectilePoolPrewarm = 16;

    [FoldoutGroup("Pooling"), AssetsOnly]
    public MuzzleFlashEffect MuzzleFlashPrefab;

    [FoldoutGroup("Pooling"), MinValue(0)]
    public int MuzzleFlashPoolPrewarm = 8;

    [FoldoutGroup("Feedback")]
    public float MuzzleFlashRotationOffset;

    [FoldoutGroup("Debug")]
    public bool DebugCombat;
}

[Serializable]
public class EnemyMeleeSettings
{
    private const float MinimumInterval = 0.02f;

    [FoldoutGroup("Weapon Loadout")]
    public bool StartArmed = true;

    [FoldoutGroup("Weapon Loadout"), AssetsOnly]
    public MeleeWeaponData StartingWeapon;

    [FoldoutGroup("Combat"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float AttackDecisionInterval = 0.05f;

    [FoldoutGroup("Debug")]
    public bool DebugMelee;
}

public static class ActorProfileDataUtility
{
    public static float[] CloneFloatArray(float[] source)
    {
        if (source == null)
            return null;

        float[] clone = new float[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    public static AnimationCurve CloneCurve(AnimationCurve source)
    {
        if (source == null)
            return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        AnimationCurve clone = new(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };

        return clone;
    }

    public static EnemyFacingSettings CloneFacing(EnemyFacingSettings source)
    {
        if (source == null)
            return new EnemyFacingSettings();

        return new EnemyFacingSettings
        {
            FacingMode = source.FacingMode,
            CustomAngle = source.CustomAngle
        };
    }
}
