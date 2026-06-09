using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Noise")]
[RequireComponent(typeof(PlayerTopDownMotor2D))]
public class PlayerNoise : MonoBehaviour
{
    private const float MinimumNoiseEventInterval = 0.02f;
    private const int SpeedLevelsCount = 10;

    [FoldoutGroup("References"), Required]
    [SerializeField] private SoundMeterUI soundMeterUI;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerNoiseEmitter noiseEmitter;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ArmorLoadout armorLoadout;

    private float idleNoise;

    private float walkNoiseAtMinSpeed = 0.35f;

    private float walkNoiseAtMaxSpeed = 0.75f;

    private float sprintNoiseAtMaxSpeed = 1f;

    private bool emitMovementNoiseEvents = true;

    private float movementNoiseEventInterval = 0.2f;

    private float minimumMovementNoiseToEmit = 0.05f;

    private float movementNoiseIntensityMultiplier = 1f;

    private NoiseType walkMovementNoiseType = NoiseType.Common;

    private NoiseType sprintMovementNoiseType = NoiseType.Common;
    private float perkSprintNoiseMultiplier = 1f;
    private float[] perkWalkNoiseSpeedLevelMultipliers = CreateDefaultWalkNoiseSpeedLevelMultipliers();

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public float CurrentNoiseAmount { get; private set; }

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public float BaseNoiseAmount { get; private set; }

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public float SpikeNoiseAmount { get; private set; }

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public float LastMovementNoiseEmitTime => _lastMovementNoiseEmitTime;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public NoiseType LastMovementNoiseType => _lastMovementNoiseType;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public float LastMovementNoiseIntensity => _lastMovementNoiseIntensity;

    private readonly System.Collections.Generic.List<NoiseSpike> _noiseSpikes = new();
    private float _nextMovementNoiseEventTime;
    private float _lastMovementNoiseEmitTime = float.NegativeInfinity;
    private NoiseType _lastMovementNoiseType;
    private float _lastMovementNoiseIntensity;

    private struct NoiseSpike
    {
        public float Amount;
        public float EndTime;
    }

    // Executes the Reset routine.
    private void Reset()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (noiseEmitter == null)
            noiseEmitter = GetComponent<PlayerNoiseEmitter>();

        if (armorLoadout == null)
            armorLoadout = GetComponent<ArmorLoadout>();

        perkSprintNoiseMultiplier = 1f;
        ResetPerkWalkNoiseMultipliers();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (noiseEmitter == null)
            noiseEmitter = GetComponent<PlayerNoiseEmitter>();

        if (armorLoadout == null)
            armorLoadout = GetComponent<ArmorLoadout>();

        perkSprintNoiseMultiplier = 1f;
        ResetPerkWalkNoiseMultipliers();
    }

    // Executes the OnValidate routine.
    private void OnValidate()
    {
        walkNoiseAtMaxSpeed = Mathf.Max(walkNoiseAtMinSpeed, walkNoiseAtMaxSpeed);
        movementNoiseEventInterval = Mathf.Max(MinimumNoiseEventInterval, movementNoiseEventInterval);
        minimumMovementNoiseToEmit = Mathf.Max(0f, minimumMovementNoiseToEmit);
        movementNoiseIntensityMultiplier = Mathf.Max(0f, movementNoiseIntensityMultiplier);
    }

    // Executes the Update routine.
    private void Update()
    {
        if (playerMotor == null)
            return;

        BaseNoiseAmount = CalculateNoiseFromMotor();
        SpikeNoiseAmount = CalculateSpikeNoise();
        CurrentNoiseAmount = BaseNoiseAmount + SpikeNoiseAmount;

        if (soundMeterUI != null)
            soundMeterUI.SetTargetNoiseAmount(CurrentNoiseAmount);

        TryEmitMovementNoiseEvent();
    }

    // Executes the AddNoiseSpike routine.
    public void AddNoiseSpike(float amount, float duration)
    {
        AddNoiseSpike(amount, duration, NoiseType.Common);
    }

    // Executes the AddNoiseSpike routine.
    public void AddNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        AddNoiseSpike(amount, duration, noiseType, false);
    }

    // Executes the AddNoiseSpike routine.
    public void AddNoiseSpike(float amount, float duration, NoiseType noiseType, bool isExtremeNoise)
    {
        if (amount <= 0f || duration <= 0f)
            return;

        _noiseSpikes.Add(new NoiseSpike
        {
            Amount = amount,
            EndTime = Time.time + duration
        });

        EmitInstantNoise(amount, noiseType, isExtremeNoise);
    }

    // Executes the EmitInstantNoise routine.
    public void EmitInstantNoise(float amount, NoiseType noiseType)
    {
        EmitInstantNoise(amount, noiseType, false);
    }

    // Executes the EmitInstantNoise routine.
    public void EmitInstantNoise(float amount, NoiseType noiseType, bool isExtremeNoise)
    {
        if (amount <= 0f || noiseEmitter == null)
            return;

        noiseEmitter.EmitNoise(amount, noiseType, isExtremeNoise);
    }

    // Executes the ApplySettings routine.
    public void ApplySettings(PlayerNoiseSettings settings)
    {
        if (settings == null)
            return;

        idleNoise = Mathf.Clamp01(settings.IdleNoise);
        walkNoiseAtMinSpeed = Mathf.Clamp01(settings.WalkNoiseAtMinSpeed);
        walkNoiseAtMaxSpeed = Mathf.Clamp01(settings.WalkNoiseAtMaxSpeed);
        sprintNoiseAtMaxSpeed = Mathf.Clamp01(settings.SprintNoiseAtMaxSpeed);
        emitMovementNoiseEvents = settings.EmitMovementNoiseEvents;
        movementNoiseEventInterval = Mathf.Max(MinimumNoiseEventInterval, settings.MovementNoiseEventInterval);
        minimumMovementNoiseToEmit = Mathf.Max(0f, settings.MinimumMovementNoiseToEmit);
        movementNoiseIntensityMultiplier = Mathf.Max(0f, settings.MovementNoiseIntensityMultiplier);
        walkMovementNoiseType = settings.WalkMovementNoiseType;
        sprintMovementNoiseType = settings.SprintMovementNoiseType;

        walkNoiseAtMaxSpeed = Mathf.Max(walkNoiseAtMinSpeed, walkNoiseAtMaxSpeed);
    }

    // Executes the ApplyPerkModifiers routine.
    public void ApplyPerkModifiers(PlayerPerkModifierSet modifiers)
    {
        perkSprintNoiseMultiplier = modifiers != null ? Mathf.Max(0f, modifiers.SprintNoiseMultiplier) : 1f;
        ResetPerkWalkNoiseMultipliers();

        if (modifiers == null)
            return;

        for (int i = 0; i < perkWalkNoiseSpeedLevelMultipliers.Length; i++)
            perkWalkNoiseSpeedLevelMultipliers[i] = modifiers.GetWalkNoiseMultiplierForSpeedLevel(i + 1);
    }

    // Executes the CalculateNoiseFromMotor routine.
    private float CalculateNoiseFromMotor()
    {
        if (!playerMotor.HasMovementInput)
            return idleNoise;

        float currentSpeed = playerMotor.CurrentPlanarSpeed;
        float movementNoiseMultiplier = armorLoadout != null ? Mathf.Max(0f, armorLoadout.MovementNoiseMultiplier) : 1f;

        if (playerMotor.IsSprinting)
        {
            float sprintT = SafeInverseLerp(playerMotor.MaxWalkSpeed, playerMotor.MaxSprintSpeed, currentSpeed);
            return Mathf.Lerp(walkNoiseAtMaxSpeed, sprintNoiseAtMaxSpeed, sprintT) *
                   movementNoiseMultiplier *
                   perkSprintNoiseMultiplier;
        }

        float walkT = SafeInverseLerp(playerMotor.MinWalkSpeed, playerMotor.MaxWalkSpeed, playerMotor.CurrentTargetSpeed);
        return Mathf.Lerp(walkNoiseAtMinSpeed, walkNoiseAtMaxSpeed, walkT) *
               playerMotor.CurrentMotionRatio *
               movementNoiseMultiplier *
               ResolveWalkNoiseSpeedLevelMultiplier();
    }

    // Executes the SafeInverseLerp routine.
    private static float SafeInverseLerp(float a, float b, float value)
    {
        if (Mathf.Approximately(a, b))
            return 0f;

        return Mathf.InverseLerp(a, b, value);
    }

    // Executes the CalculateSpikeNoise routine.
    private float CalculateSpikeNoise()
    {
        if (_noiseSpikes.Count <= 0)
            return 0f;

        float spikeTotal = 0f;
        float currentTime = Time.time;

        for (int i = _noiseSpikes.Count - 1; i >= 0; i--)
        {
            NoiseSpike spike = _noiseSpikes[i];
            if (spike.EndTime <= currentTime)
            {
                _noiseSpikes.RemoveAt(i);
                continue;
            }

            spikeTotal += spike.Amount;
        }

        return spikeTotal;
    }

    // Executes the TryEmitMovementNoiseEvent routine.
    private void TryEmitMovementNoiseEvent()
    {
        if (!emitMovementNoiseEvents || noiseEmitter == null || !playerMotor.HasMovementInput)
            return;

        if (Time.time < _nextMovementNoiseEventTime)
            return;

        float intensity = BaseNoiseAmount * movementNoiseIntensityMultiplier;
        if (intensity < minimumMovementNoiseToEmit)
            return;

        NoiseType noiseType = ResolveMovementNoiseType();
        noiseEmitter.EmitNoise(intensity, noiseType);

        _lastMovementNoiseEmitTime = Time.time;
        _lastMovementNoiseType = noiseType;
        _lastMovementNoiseIntensity = intensity;
        _nextMovementNoiseEventTime = Time.time + movementNoiseEventInterval;
    }

    // Executes the ResolveMovementNoiseType routine.
    private NoiseType ResolveMovementNoiseType()
    {
        if (playerMotor.IsSprinting)
            return sprintMovementNoiseType;

        return walkMovementNoiseType;
    }

    // Executes the ResolveWalkNoiseSpeedLevelMultiplier routine.
    private float ResolveWalkNoiseSpeedLevelMultiplier()
    {
        if (playerMotor == null || perkWalkNoiseSpeedLevelMultipliers == null || perkWalkNoiseSpeedLevelMultipliers.Length <= 0)
            return 1f;

        int index = Mathf.Clamp(playerMotor.EffectiveSpeedLevel - 1, 0, perkWalkNoiseSpeedLevelMultipliers.Length - 1);
        return Mathf.Max(0f, perkWalkNoiseSpeedLevelMultipliers[index]);
    }

    // Executes the ResetPerkWalkNoiseMultipliers routine.
    private void ResetPerkWalkNoiseMultipliers()
    {
        if (perkWalkNoiseSpeedLevelMultipliers == null || perkWalkNoiseSpeedLevelMultipliers.Length != SpeedLevelsCount)
            perkWalkNoiseSpeedLevelMultipliers = CreateDefaultWalkNoiseSpeedLevelMultipliers();

        for (int i = 0; i < perkWalkNoiseSpeedLevelMultipliers.Length; i++)
            perkWalkNoiseSpeedLevelMultipliers[i] = 1f;
    }

    // Executes the CreateDefaultWalkNoiseSpeedLevelMultipliers routine.
    private static float[] CreateDefaultWalkNoiseSpeedLevelMultipliers()
    {
        float[] multipliers = new float[SpeedLevelsCount];
        for (int i = 0; i < multipliers.Length; i++)
            multipliers[i] = 1f;

        return multipliers;
    }
}
