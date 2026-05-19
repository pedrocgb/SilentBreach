using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Noise")]
[RequireComponent(typeof(PlayerTopDownMotor2D))]
public class PlayerNoise : MonoBehaviour
{
    private const float MinimumNoiseEventInterval = 0.02f;

    [FoldoutGroup("References"), Required]
    [SerializeField] private SoundMeterUI soundMeterUI;

    [FoldoutGroup("References"), Tooltip("If empty, auto-finds on this GameObject.")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References"), Tooltip("Optional helper used to broadcast noise events to AI hearing.")]
    [SerializeField] private PlayerNoiseEmitter noiseEmitter;

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

    private void Reset()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (noiseEmitter == null)
            noiseEmitter = GetComponent<PlayerNoiseEmitter>();
    }

    private void Awake()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (noiseEmitter == null)
            noiseEmitter = GetComponent<PlayerNoiseEmitter>();
    }

    private void OnValidate()
    {
        walkNoiseAtMaxSpeed = Mathf.Max(walkNoiseAtMinSpeed, walkNoiseAtMaxSpeed);
        movementNoiseEventInterval = Mathf.Max(MinimumNoiseEventInterval, movementNoiseEventInterval);
        minimumMovementNoiseToEmit = Mathf.Max(0f, minimumMovementNoiseToEmit);
        movementNoiseIntensityMultiplier = Mathf.Max(0f, movementNoiseIntensityMultiplier);
    }

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

    public void AddNoiseSpike(float amount, float duration)
    {
        AddNoiseSpike(amount, duration, NoiseType.Common);
    }

    public void AddNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        if (amount <= 0f || duration <= 0f)
            return;

        _noiseSpikes.Add(new NoiseSpike
        {
            Amount = amount,
            EndTime = Time.time + duration
        });

        EmitInstantNoise(amount, noiseType);
    }

    public void EmitInstantNoise(float amount, NoiseType noiseType)
    {
        if (amount <= 0f || noiseEmitter == null)
            return;

        noiseEmitter.EmitNoise(amount, noiseType);
    }

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

    private float CalculateNoiseFromMotor()
    {
        if (!playerMotor.HasMovementInput)
            return idleNoise;

        float currentSpeed = playerMotor.CurrentPlanarSpeed;

        if (playerMotor.IsSprinting)
        {
            float sprintT = SafeInverseLerp(playerMotor.MaxWalkSpeed, playerMotor.MaxSprintSpeed, currentSpeed);
            return Mathf.Lerp(walkNoiseAtMaxSpeed, sprintNoiseAtMaxSpeed, sprintT);
        }

        float walkT = SafeInverseLerp(playerMotor.MinWalkSpeed, playerMotor.MaxWalkSpeed, playerMotor.CurrentTargetSpeed);
        return Mathf.Lerp(walkNoiseAtMinSpeed, walkNoiseAtMaxSpeed, walkT) * playerMotor.CurrentMotionRatio;
    }

    private static float SafeInverseLerp(float a, float b, float value)
    {
        if (Mathf.Approximately(a, b))
            return 0f;

        return Mathf.InverseLerp(a, b, value);
    }

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

    private NoiseType ResolveMovementNoiseType()
    {
        if (playerMotor.IsSprinting)
            return sprintMovementNoiseType;

        return walkMovementNoiseType;
    }
}
