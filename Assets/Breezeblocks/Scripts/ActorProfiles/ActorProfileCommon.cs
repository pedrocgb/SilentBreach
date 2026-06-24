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

    [FoldoutGroup("Recovery"), Range(0f, 1f)]
    public float RestoredHealthFractionOnWake = 1f;

    [FoldoutGroup("State Presentation"), PreviewField(72, ObjectFieldAlignment.Left)]
    public Sprite IncapacitatedSprite;

    [FoldoutGroup("State Presentation"), PreviewField(72, ObjectFieldAlignment.Left)]
    public Sprite DeadSprite;

    [FoldoutGroup("State Presentation"), PreviewField(72, ObjectFieldAlignment.Left)]
    public Sprite SleepingSprite;

    /// <summary>
    /// Clamps health and recovery settings to safe runtime ranges.
    /// </summary>
    public void Validate()
    {
        MaxHealth = Mathf.Max(0f, MaxHealth);
        RestoredHealthFractionOnWake = Mathf.Clamp01(RestoredHealthFractionOnWake);
    }
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
public class MissionActorIdentitySettings
{
    [FoldoutGroup("Identity")]
    public string ActorId;

    [FoldoutGroup("Identity")]
    public string ActorDisplayName;

    [FoldoutGroup("Identity")]
    public bool IsInnocent;

    /// <summary>
    /// Trims identity strings so mission matching remains consistent.
    /// </summary>
    public void Validate()
    {
        ActorId = ActorId != null ? ActorId.Trim() : string.Empty;
        ActorDisplayName = ActorDisplayName != null ? ActorDisplayName.Trim() : string.Empty;
    }
}

[Serializable]
public class EnemySleepSettings
{
    [FoldoutGroup("Startup")]
    public bool StartSleeping;

    [FoldoutGroup("Startup"), ShowIf(nameof(StartSleeping)), EnumToggleButtons]
    public EnemySleepType StartSleepType = EnemySleepType.NormalSleep;

    [FoldoutGroup("Wake Thresholds"), Range(0f, 1f)]
    public float NormalSleepWakeThreshold = 0.4f;

    [FoldoutGroup("Wake Thresholds"), Range(0f, 1f)]
    public float DeepSleepWakeThreshold = 0.8f;

    [FoldoutGroup("Auto Wake"), MinValue(0f), SuffixLabel("s", true)]
    public float NormalSleepAutoWakeDelay = 120f;

    [FoldoutGroup("Auto Wake"), MinValue(0f), SuffixLabel("s", true)]
    public float DeepSleepAutoWakeDelay = 240f;

    [FoldoutGroup("Auto Wake"), MinValue(0f), SuffixLabel("s", true)]
    public float ForcedSleepAutoWakeDelay = 60f;

    /// <summary>
    /// Clamps sleep settings to safe runtime ranges.
    /// </summary>
    public void Validate()
    {
        NormalSleepWakeThreshold = Mathf.Clamp01(NormalSleepWakeThreshold);
        DeepSleepWakeThreshold = Mathf.Clamp01(DeepSleepWakeThreshold);
        NormalSleepAutoWakeDelay = Mathf.Max(0f, NormalSleepAutoWakeDelay);
        DeepSleepAutoWakeDelay = Mathf.Max(0f, DeepSleepAutoWakeDelay);
        ForcedSleepAutoWakeDelay = Mathf.Max(0f, ForcedSleepAutoWakeDelay);
    }
}

[Serializable]
public class EnemyRoomAwarenessSettings
{
    private const float MinimumInterval = 0.02f;

    [FoldoutGroup("Room Awareness")]
    public bool RoomAwareness = true;

    [FoldoutGroup("Room Awareness"), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float RoomCheckInterval = 0.15f;

    [FoldoutGroup("Light Reaction")]
    public bool ConfusedByLightsOff;

    [FoldoutGroup("Light Reaction"), HideIf(nameof(ConfusedByLightsOff)), MinValue(0f), SuffixLabel("s", true)]
    public float WaitBeforeSwitchDuration = 1f;

    [FoldoutGroup("Light Reaction"), HideIf(nameof(ConfusedByLightsOff)), MinValue(0f), SuffixLabel("s", true)]
    public float LookAroundDurationAfterTurningLightsOn = 2.5f;

    [FoldoutGroup("Light Reaction"), HideIf(nameof(ConfusedByLightsOff)), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float LookAroundTurnInterval = 0.45f;

    [FoldoutGroup("Light Reaction"), HideIf(nameof(ConfusedByLightsOff)), MinValue(0f), SuffixLabel("deg/s", true)]
    public float LookAroundRotationSpeed = 420f;

    [FoldoutGroup("Confused Reaction"), ShowIf(nameof(ConfusedByLightsOff)), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float ConfusedReactionDuration = 1.2f;

    [FoldoutGroup("Door State Awareness")]
    public bool DoorStateAwareness = true;

    [FoldoutGroup("Door State Awareness"), MinValue(0f), SuffixLabel("s", true)]
    public float WaitBeforeDoorStateFixDuration = 1f;

    /// <summary>
    /// Clamps room awareness settings to safe runtime ranges.
    /// </summary>
    public void Validate()
    {
        RoomCheckInterval = Mathf.Max(MinimumInterval, RoomCheckInterval);
        WaitBeforeSwitchDuration = Mathf.Max(0f, WaitBeforeSwitchDuration);
        LookAroundDurationAfterTurningLightsOn = Mathf.Max(0f, LookAroundDurationAfterTurningLightsOn);
        LookAroundTurnInterval = Mathf.Max(MinimumInterval, LookAroundTurnInterval);
        LookAroundRotationSpeed = Mathf.Max(0f, LookAroundRotationSpeed);
        ConfusedReactionDuration = Mathf.Max(MinimumInterval, ConfusedReactionDuration);
        WaitBeforeDoorStateFixDuration = Mathf.Max(0f, WaitBeforeDoorStateFixDuration);
    }
}

[Serializable]
public class EnemyDoorBellReactionSettings
{
    private const float MinimumInterval = 0.02f;

    [FoldoutGroup("Door Bell Reaction")]
    public bool ReactToDoorBell = true;

    [FoldoutGroup("Door Bell Reaction"), ShowIf(nameof(ReactToDoorBell)), MinValue(0)]
    public int ReactionsBeforeAlert = 2;

    [FoldoutGroup("Door Bell Reaction"), ShowIf(nameof(ReactToDoorBell)), MinValue(0f), SuffixLabel("s", true)]
    public float RepeatIgnoreDuration = 1.5f;

    [FoldoutGroup("Door Bell Reaction"), ShowIf(nameof(ReactToDoorBell)), EnumToggleButtons]
    public EnemySpeedType MoveSpeed = EnemySpeedType.Walk;

    [FoldoutGroup("Door Bell Reaction"), ShowIf(nameof(ReactToDoorBell)), MinValue(0f), SuffixLabel("s", true)]
    public float StandDuration = 1f;

    [FoldoutGroup("Door Bell Reaction"), ShowIf(nameof(ReactToDoorBell)), MinValue(0f), SuffixLabel("s", true)]
    public float LookAroundDuration = 2.5f;

    [FoldoutGroup("Door Bell Reaction"), ShowIf(nameof(ReactToDoorBell)), MinValue(MinimumInterval), SuffixLabel("s", true)]
    public float LookAroundTurnInterval = 0.5f;

    /// <summary>
    /// Clamps doorbell reaction settings to safe runtime ranges.
    /// </summary>
    public void Validate()
    {
        ReactionsBeforeAlert = Mathf.Max(0, ReactionsBeforeAlert);
        RepeatIgnoreDuration = Mathf.Max(0f, RepeatIgnoreDuration);
        StandDuration = Mathf.Max(0f, StandDuration);
        LookAroundDuration = Mathf.Max(0f, LookAroundDuration);
        LookAroundTurnInterval = Mathf.Max(MinimumInterval, LookAroundTurnInterval);
    }
}

[Serializable]
public class EnemyConfusedReactionSettings
{
    [FoldoutGroup("Animation"), SuffixLabel("s", true), MinValue(0.02f)]
    public float PoseHoldDuration = 0.2f;

    [FoldoutGroup("Animation")]
    public Vector2 FirstLocalPosition = new(1f, 0f);

    [FoldoutGroup("Animation"), SuffixLabel("deg", true)]
    public float FirstLocalRotationZ = 30f;

    [FoldoutGroup("Animation")]
    public Vector2 SecondLocalPosition = new(-1f, 0f);

    [FoldoutGroup("Animation"), SuffixLabel("deg", true)]
    public float SecondLocalRotationZ = -30f;

    /// <summary>
    /// Clamps confused indicator timings to safe runtime ranges.
    /// </summary>
    public void Validate()
    {
        PoseHoldDuration = Mathf.Max(0.02f, PoseHoldDuration);
    }
}

[Serializable]
public class ActorFootstepSfxSettings
{
    private const float MinimumSpeed = 0.01f;

    [FoldoutGroup("SFX"), InlineProperty, HideLabel]
    public AudioClipSet FootstepSfx = new();

    [FoldoutGroup("SFX"), EnumToggleButtons]
    public NoiseType FootstepSoundType = NoiseType.Common;

    [FoldoutGroup("Timing"), MinValue(0f)]
    public float MinSpeedThreshold = 0.2f;

    [FoldoutGroup("Timing"), MinValue(MinimumSpeed)]
    public float SpeedForFastestStep = 5f;

    [FoldoutGroup("Timing"), MinValue(0.01f), SuffixLabel("s", true)]
    public float SlowStepInterval = 0.5f;

    [FoldoutGroup("Timing"), MinValue(0.01f), SuffixLabel("s", true)]
    public float FastStepInterval = 0.22f;

    [FoldoutGroup("Mix"), Range(0f, 1f)]
    public float MinimumVolumeMultiplier = 0.7f;

    [FoldoutGroup("Mix"), Range(0f, 2f)]
    public float MaximumVolumeMultiplier = 1f;

    /// <summary>
    /// Clamps footstep audio settings and validates nested clip data.
    /// </summary>
    public void Validate()
    {
        FootstepSfx ??= new AudioClipSet();
        FootstepSfx.Validate();
        MinSpeedThreshold = Mathf.Max(0f, MinSpeedThreshold);
        SpeedForFastestStep = Mathf.Max(MinimumSpeed, SpeedForFastestStep);
        SlowStepInterval = Mathf.Max(0.01f, SlowStepInterval);
        FastStepInterval = Mathf.Max(0.01f, FastStepInterval);
        MaximumVolumeMultiplier = Mathf.Max(0f, MaximumVolumeMultiplier);
        MinimumVolumeMultiplier = Mathf.Clamp(MinimumVolumeMultiplier, 0f, MaximumVolumeMultiplier);
    }
}

[Serializable]
public class CharacterOrbitHandsSettings
{
    [FoldoutGroup("Rig"), MinValue(0f)]
    public float SideOffset = 0.55f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    public float LocomotionSwingAmplitude = 0.28f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    public float HoldDistance = 0.52f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    public float HoldHandSeparation = 0.12f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    public float HeldItemScale = 0.75f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    public float BodyDragHoldDistance = 0.24f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    public float BodyDragHandSeparation = 0.05f;

    [FoldoutGroup("Rig")]
    public float HeldItemRotationOffset;

    [FoldoutGroup("Rig"), MinValue(0f)]
    public float AutoCreatedHandScale = 0.7f;

    [FoldoutGroup("Motion"), MinValue(0f)]
    public float SwingCyclesPerSpeedUnit = 1.35f;

    [FoldoutGroup("Motion"), MinValue(0f)]
    public float MinimumMoveSpeedForSwing = 0.05f;

    [FoldoutGroup("Motion"), MinValue(0f)]
    public float LocomotionDirectionSmoothing = 18f;

    [FoldoutGroup("Motion"), MinValue(0f)]
    public float EnemyUnarmedHandSmoothing = 22f;

    /// <summary>
    /// Clamps hand rig and motion settings to non-negative ranges.
    /// </summary>
    public void Validate()
    {
        SideOffset = Mathf.Max(0f, SideOffset);
        LocomotionSwingAmplitude = Mathf.Max(0f, LocomotionSwingAmplitude);
        HoldDistance = Mathf.Max(0f, HoldDistance);
        HoldHandSeparation = Mathf.Max(0f, HoldHandSeparation);
        HeldItemScale = Mathf.Max(0f, HeldItemScale);
        BodyDragHoldDistance = Mathf.Max(0f, BodyDragHoldDistance);
        BodyDragHandSeparation = Mathf.Max(0f, BodyDragHandSeparation);
        AutoCreatedHandScale = Mathf.Max(0f, AutoCreatedHandScale);
        SwingCyclesPerSpeedUnit = Mathf.Max(0f, SwingCyclesPerSpeedUnit);
        MinimumMoveSpeedForSwing = Mathf.Max(0f, MinimumMoveSpeedForSwing);
        LocomotionDirectionSmoothing = Mathf.Max(0f, LocomotionDirectionSmoothing);
        EnemyUnarmedHandSmoothing = Mathf.Max(0f, EnemyUnarmedHandSmoothing);
    }
}

[Serializable]
public class PlayerControlsSettings
{
    [FoldoutGroup("Player"), MinValue(0)]
    public int RewiredPlayerId = 1;

    [FoldoutGroup("Movement")]
    public string MoveHorizontalAction = "Move Horizontal";

    [FoldoutGroup("Movement")]
    public string MoveVerticalAction = "Move Vertical";

    [FoldoutGroup("Movement")]
    public string SprintAction = "Sprint";

    [FoldoutGroup("Movement")]
    public string ToggleMinMaxSpeedAction = "Toggle Speed MinMax";

    [FoldoutGroup("Movement")]
    public string MouseWheelAxisAction = "Mouse Wheel";

    [FoldoutGroup("Combat")]
    public string AimAction = "Aim";

    [FoldoutGroup("Combat")]
    public string FireAction = "Fire";

    [FoldoutGroup("Combat")]
    public string ReloadAction = "Reload";

    [FoldoutGroup("Combat")]
    public string CycleFireModeAction = "Cycle Fire Mode";

    [FoldoutGroup("Utility")]
    public string CancelThrowableAction = "Cancel Throw";

    [FoldoutGroup("Equipment")]
    public string EquipPrimaryAction = "Equip Primary";

    [FoldoutGroup("Equipment")]
    public string EquipSecondaryAction = "Equip Secondary";

    [FoldoutGroup("Equipment")]
    public string EquipBeltAction = "Equip Belt";

    [FoldoutGroup("Equipment")]
    public string ToggleEquipmentPanelAction = "Toggle Equipment Panel";

    [FoldoutGroup("Interaction")]
    public string InteractAction = "Interact";

    [FoldoutGroup("Focus")]
    public string FocusAction = "Focus";

    /// <summary>
    /// Normalizes player id and action names used by Rewired-backed player systems.
    /// </summary>
    public void Validate()
    {
        RewiredPlayerId = Mathf.Max(0, RewiredPlayerId);
        MoveHorizontalAction = NormalizeAction(MoveHorizontalAction, "Move Horizontal");
        MoveVerticalAction = NormalizeAction(MoveVerticalAction, "Move Vertical");
        SprintAction = NormalizeAction(SprintAction, "Sprint");
        ToggleMinMaxSpeedAction = NormalizeAction(ToggleMinMaxSpeedAction, "Toggle Speed MinMax");
        MouseWheelAxisAction = NormalizeAction(MouseWheelAxisAction, "Mouse Wheel");
        AimAction = NormalizeAction(AimAction, "Aim");
        FireAction = NormalizeAction(FireAction, "Fire");
        ReloadAction = NormalizeAction(ReloadAction, "Reload");
        CycleFireModeAction = NormalizeAction(CycleFireModeAction, "Cycle Fire Mode");
        CancelThrowableAction = NormalizeAction(CancelThrowableAction, "Cancel Throw");
        EquipPrimaryAction = NormalizeAction(EquipPrimaryAction, "Equip Primary");
        EquipSecondaryAction = NormalizeAction(EquipSecondaryAction, "Equip Secondary");
        EquipBeltAction = NormalizeAction(EquipBeltAction, "Equip Belt");
        ToggleEquipmentPanelAction = NormalizeAction(ToggleEquipmentPanelAction, "Toggle Equipment Panel");
        InteractAction = NormalizeAction(InteractAction, "Interact");
        FocusAction = NormalizeAction(FocusAction, "Focus");
    }

    /// <summary>
    /// Keeps action names non-empty so Rewired reads remain valid after inspector edits.
    /// </summary>
    private static string NormalizeAction(string actionName, string fallback)
    {
        return string.IsNullOrWhiteSpace(actionName) ? fallback : actionName.Trim();
    }
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

    [FoldoutGroup("UI")]
    public bool FillVelocityByLevel = true;
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

    [FoldoutGroup("Orientation"), MinValue(0f)]
    public float UnarmedAimRotationSpeed = 720f;

    [FoldoutGroup("Orientation"), MinValue(0f)]
    public float UnarmedAimPanDistance = 0f;

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

    [FoldoutGroup("UI")]
    public string StaminaTextFormat = "{0:0}/{1:0}";

    [FoldoutGroup("UI Feedback"), MinValue(0f), SuffixLabel("s", true)]
    public float InsufficientStaminaShakeDuration = 0.2f;

    [FoldoutGroup("UI Feedback"), MinValue(0f)]
    public float InsufficientStaminaShakeStrength = 18f;

    [FoldoutGroup("UI Feedback"), MinValue(1)]
    public int InsufficientStaminaShakeVibrato = 18;

    /// <summary>
    /// Clamps stamina values and UI feedback settings to safe ranges.
    /// </summary>
    public void Validate()
    {
        MaxStamina = Mathf.Max(0f, MaxStamina);
        SprintDrainPerSecond = Mathf.Max(0f, SprintDrainPerSecond);
        RegenerationPerSecond = Mathf.Max(0f, RegenerationPerSecond);
        RegenerationDelayAfterSpend = Mathf.Max(0f, RegenerationDelayAfterSpend);
        StaggerStaminaLossPercent = Mathf.Clamp(StaggerStaminaLossPercent, 0f, 100f);
        MovementThreshold = Mathf.Max(0f, MovementThreshold);
        StaminaTextFormat = string.IsNullOrWhiteSpace(StaminaTextFormat) ? "{0:0}/{1:0}" : StaminaTextFormat.Trim();
        InsufficientStaminaShakeDuration = Mathf.Max(0f, InsufficientStaminaShakeDuration);
        InsufficientStaminaShakeStrength = Mathf.Max(0f, InsufficientStaminaShakeStrength);
        InsufficientStaminaShakeVibrato = Mathf.Max(1, InsufficientStaminaShakeVibrato);
    }
}

[Serializable]
public class PlayerFocusSettings
{
    [FoldoutGroup("Focus"), MinValue(0f), SuffixLabel("s", true)]
    public float MaxFocusSeconds = 6f;

    [FoldoutGroup("Focus")]
    public bool Regenerate = true;

    [FoldoutGroup("Focus"), ShowIf(nameof(Regenerate)), MinValue(0f)]
    public float RegenerationPerSecond = 1.25f;

    [FoldoutGroup("Focus"), ShowIf(nameof(Regenerate)), MinValue(0f), SuffixLabel("s", true)]
    public float RegenerationDelayAfterUse = 1.25f;

    [FoldoutGroup("Focus Effect"), Range(-100f, 100f)]
    public float FocusSaturation = -100f;

    [FoldoutGroup("Focus Effect"), MinValue(0f), SuffixLabel("s", true)]
    public float FocusTransitionDuration = 0.22f;

    /// <summary>
    /// Clamps focus timing and visual effect values to valid ranges.
    /// </summary>
    public void Validate()
    {
        MaxFocusSeconds = Mathf.Max(0f, MaxFocusSeconds);
        RegenerationPerSecond = Mathf.Max(0f, RegenerationPerSecond);
        RegenerationDelayAfterUse = Mathf.Max(0f, RegenerationDelayAfterUse);
        FocusSaturation = Mathf.Clamp(FocusSaturation, -100f, 100f);
        FocusTransitionDuration = Mathf.Max(0f, FocusTransitionDuration);
    }
}

[Serializable]
public class PlayerStaggerFeedbackSettings
{
    [FoldoutGroup("Effect"), MinValue(0.01f), SuffixLabel("s", true)]
    public float FullStrengthReferenceDuration = 0.5f;

    [FoldoutGroup("Effect"), MinValue(0f)]
    public float EffectLerpSpeed = 10f;

    [FoldoutGroup("Effect"), Range(0f, 1f)]
    public float MaxVignetteIntensity = 0.32f;

    [FoldoutGroup("Effect"), Range(0f, 1f)]
    public float MaxChromaticAberration = 0.22f;

    [FoldoutGroup("Effect"), Range(-1f, 1f)]
    public float MaxLensDistortion = -0.18f;

    /// <summary>
    /// Clamps stagger feedback post-processing values to safe ranges.
    /// </summary>
    public void Validate()
    {
        FullStrengthReferenceDuration = Mathf.Max(0.01f, FullStrengthReferenceDuration);
        EffectLerpSpeed = Mathf.Max(0f, EffectLerpSpeed);
        MaxVignetteIntensity = Mathf.Clamp01(MaxVignetteIntensity);
        MaxChromaticAberration = Mathf.Clamp01(MaxChromaticAberration);
        MaxLensDistortion = Mathf.Clamp(MaxLensDistortion, -1f, 1f);
    }
}

[Serializable]
public class PlayerWeaponControllerSettings
{
    [FoldoutGroup("Pooling"), AssetsOnly]
    public HitscanProjectile ProjectilePrefab;

    [FoldoutGroup("Pooling"), MinValue(0)]
    public int ProjectilePoolPrewarm = 16;

    [FoldoutGroup("Pooling"), AssetsOnly]
    public MuzzleFlashEffect MuzzleFlashPrefab;

    [FoldoutGroup("Pooling"), MinValue(0)]
    public int MuzzleFlashPoolPrewarm = 8;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    public float LookRotationSpeed = 720f;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    public float StationarySpeedThreshold = 0.05f;

    [FoldoutGroup("Aiming"), MinValue(0f)]
    public float DebugTraceDuration = 0.1f;

    [FoldoutGroup("Feedback")]
    public float MuzzleFlashRotationOffset;

    [FoldoutGroup("Debug Loadout")]
    public bool AutoEquipDebugWeaponOnStart;

    [FoldoutGroup("Debug Loadout"), AssetsOnly]
    public FirearmData DebugFirearm;

    [FoldoutGroup("Debug Loadout"), AssetsOnly]
    public ProjectileData DebugProjectile;

    [FoldoutGroup("Debug Loadout"), MinValue(-1)]
    public int DebugStartingLoadedAmmo = -1;

    [FoldoutGroup("Debug Loadout"), MinValue(-1)]
    public int DebugStartingReserveAmmo = -1;

    [FoldoutGroup("Debug Loadout"), MinValue(0)]
    public int DebugReserveAmmoAddAmount = 12;

    /// <summary>
    /// Clamps weapon controller timing, pooling, and debug values to safe ranges.
    /// </summary>
    public void Validate()
    {
        ProjectilePoolPrewarm = Mathf.Max(0, ProjectilePoolPrewarm);
        MuzzleFlashPoolPrewarm = Mathf.Max(0, MuzzleFlashPoolPrewarm);
        LookRotationSpeed = Mathf.Max(0f, LookRotationSpeed);
        StationarySpeedThreshold = Mathf.Max(0f, StationarySpeedThreshold);
        DebugTraceDuration = Mathf.Max(0f, DebugTraceDuration);
        DebugStartingLoadedAmmo = Mathf.Max(-1, DebugStartingLoadedAmmo);
        DebugStartingReserveAmmo = Mathf.Max(-1, DebugStartingReserveAmmo);
        DebugReserveAmmoAddAmount = Mathf.Max(0, DebugReserveAmmoAddAmount);
    }
}

[Serializable]
public class PlayerEquipmentSlotSettings
{
    [AssetsOnly]
    public EquipmentItemData Item;

    [ShowIf(nameof(IsFirearmItem)), AssetsOnly]
    public ProjectileData FirearmProjectile;

    [ShowIf(nameof(IsFirearmItem)), MinValue(-1)]
    public int StartingLoadedAmmo = -1;

    [ShowIf(nameof(IsFirearmItem)), MinValue(-1)]
    public int StartingReserveAmmo = -1;

    private bool IsFirearmItem => Item is FirearmData;

    /// <summary>
    /// Clamps ammo overrides to valid sentinel or non-negative values.
    /// </summary>
    public void Validate()
    {
        StartingLoadedAmmo = Mathf.Max(-1, StartingLoadedAmmo);
        StartingReserveAmmo = Mathf.Max(-1, StartingReserveAmmo);
    }
}

[Serializable]
public class PlayerEquipmentSettings
{
    [FoldoutGroup("Starting Equipment/Hand Slots"), LabelText("Primary")]
    public PlayerEquipmentSlotSettings PrimaryEquipment = new();

    [FoldoutGroup("Starting Equipment/Hand Slots"), LabelText("Secondary")]
    public PlayerEquipmentSlotSettings SecondaryEquipment = new();

    [FoldoutGroup("Starting Equipment/Hand Slots"), LabelText("Belt")]
    public PlayerEquipmentSlotSettings BeltEquipment = new();

    [FoldoutGroup("Starting Equipment"), AssetsOnly]
    public ArmorData StartingArmor;

    [FoldoutGroup("Starting Equipment")]
    public EquipmentSlotType StartingHeldSlot = EquipmentSlotType.Primary;

    [FoldoutGroup("Panel")]
    public bool HideCrosshairWhilePanelVisible = true;

    [FoldoutGroup("Panel")]
    public bool PauseGameWhilePanelVisible = true;

    /// <summary>
    /// Ensures nested loadout settings exist and starting slot points to a hand slot.
    /// </summary>
    public void Validate()
    {
        PrimaryEquipment ??= new PlayerEquipmentSlotSettings();
        SecondaryEquipment ??= new PlayerEquipmentSlotSettings();
        BeltEquipment ??= new PlayerEquipmentSlotSettings();
        PrimaryEquipment.Validate();
        SecondaryEquipment.Validate();
        BeltEquipment.Validate();
        StartingHeldSlot = StartingHeldSlot.IsHandSlot() ? StartingHeldSlot : EquipmentSlotType.Primary;
    }
}

[Serializable]
public class PlayerInteractionSettings
{
    private const float MinimumRange = 0.01f;

    [FoldoutGroup("Detection"), MinValue(MinimumRange)]
    public float InteractionRange = 1.25f;

    /// <summary>
    /// Clamps interaction detection values to safe ranges.
    /// </summary>
    public void Validate()
    {
        InteractionRange = Mathf.Max(MinimumRange, InteractionRange);
    }
}

[Serializable]
public class PlayerBodyDragSettings
{
    private const float MinimumDragFollowSpeed = 0.01f;
    private const float MinimumDragNoiseInterval = 0.02f;

    [FoldoutGroup("Drag"), MinValue(0f)]
    public float DragDistance = 0.7f;

    [FoldoutGroup("Drag")]
    public float DragVerticalOffset;

    [FoldoutGroup("Drag"), MinValue(MinimumDragFollowSpeed)]
    public float DragFollowSpeed = 5f;

    [FoldoutGroup("Drag"), MinValue(0f)]
    public float MovingBodyThreshold = 0.05f;

    [FoldoutGroup("Drag Noise"), InlineProperty, HideLabel]
    public AudioClipSet DragMovementSfx = new();

    [FoldoutGroup("Drag Noise"), MinValue(MinimumDragNoiseInterval)]
    public float DragNoiseInterval = 0.28f;

    [FoldoutGroup("Drag Noise"), MinValue(0f)]
    public float DragNoiseIntensity = 0.35f;

    [FoldoutGroup("Drag Noise")]
    public NoiseType DragNoiseType = NoiseType.Common;

    [FoldoutGroup("Drag Noise")]
    public bool DragNoiseExtreme;

    [FoldoutGroup("Drag Noise"), MinValue(0f)]
    public float DragSfxVolumeMultiplier = 1f;

    /// <summary>
    /// Clamps body drag movement, noise, and SFX settings to safe ranges.
    /// </summary>
    public void Validate()
    {
        DragDistance = Mathf.Max(0f, DragDistance);
        DragFollowSpeed = Mathf.Max(MinimumDragFollowSpeed, DragFollowSpeed);
        MovingBodyThreshold = Mathf.Max(0f, MovingBodyThreshold);
        DragMovementSfx ??= new AudioClipSet();
        DragMovementSfx.Validate();
        DragNoiseInterval = Mathf.Max(MinimumDragNoiseInterval, DragNoiseInterval);
        DragNoiseIntensity = Mathf.Max(0f, DragNoiseIntensity);
        DragSfxVolumeMultiplier = Mathf.Max(0f, DragSfxVolumeMultiplier);
    }
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

    [FoldoutGroup("Doors")]
    public bool AllowClosedDoorTraversalWhilePatrol = true;

    [FoldoutGroup("Doors")]
    public bool AllowClosedDoorTraversalWhileAlert = true;

    [FoldoutGroup("Doors")]
    public bool AllowClosedDoorTraversalWhileSuspicious = true;

    [FoldoutGroup("Doors")]
    public bool AllowClosedDoorTraversalWhileSearching = true;

    [FoldoutGroup("Doors")]
    public bool AllowClosedDoorTraversalWhileReturningToStart = true;

    [FoldoutGroup("Doors")]
    public bool AllowClosedDoorTraversalWhileFleeing = true;

    [FoldoutGroup("Doors")]
    public bool AllowClosedDoorTraversalWhileDetected;

    [FoldoutGroup("Doors"), Range(0, 31)]
    public int ClosedDoorPathTag = 1;

    [FoldoutGroup("Doors"), MinValue(0)]
    public int ClosedDoorTagPenalty;

    [FoldoutGroup("Doors"), MinValue(0)]
    public int ClosedDoorPatrolTagPenalty;

    [FoldoutGroup("Doors")]
    public LayerMask DoorDetectionMask = Physics2D.AllLayers;

    [FoldoutGroup("Doors"), MinValue(0.05f), SuffixLabel("u", true)]
    public float DoorAutoOpenRange = 0.9f;

    [FoldoutGroup("Doors"), MinValue(0.01f), SuffixLabel("u", true)]
    public float DoorAutoOpenRadius = 0.18f;

    [FoldoutGroup("Doors"), MinValue(0f), SuffixLabel("s", true)]
    public float DoorAutoOpenCooldown = 0.2f;

    [FoldoutGroup("Doors"), MinValue(MinimumDistance), SuffixLabel("u", true)]
    public float DoorPreferredRouteProbeDistance = 3f;

    [FoldoutGroup("Doors"), MinValue(MinimumDistance), SuffixLabel("u", true)]
    public float DoorPreferredRouteProbeWidth = 0.75f;

    [FoldoutGroup("Doors")]
    public bool CloseDoorsAfterPassing;

    [FoldoutGroup("Doors"), ShowIf(nameof(CloseDoorsAfterPassing))]
    public bool RelockIgnoredLockedDoorsAfterPassing;

    [FoldoutGroup("Doors"), ShowIf(nameof(CloseDoorsAfterPassing)), MinValue(MinimumDistance), SuffixLabel("u", true)]
    public float DoorCloseAfterPassDistance = 0.75f;

    [FoldoutGroup("Doors"), ShowIf(nameof(CloseDoorsAfterPassing)), MinValue(0f), SuffixLabel("s", true)]
    public float DoorCloseAfterOpenDelay = 0.25f;

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

    [FoldoutGroup("Detection")]
    public bool ReactToBodies = true;

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
    /// <summary>
    /// Clones a float array so runtime systems can safely mutate their local copy.
    /// </summary>
    public static float[] CloneFloatArray(float[] source)
    {
        if (source == null)
            return null;

        float[] clone = new float[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    /// <summary>
    /// Clones an animation curve so runtime systems can safely mutate their local copy.
    /// </summary>
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

    /// <summary>
    /// Clones enemy facing settings so runtime systems can safely mutate their local copy.
    /// </summary>
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
