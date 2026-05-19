using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Noise Emitter")]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [FoldoutGroup("References")]
    [Tooltip("Optional explicit origin for emitted noises. If empty, this transform is used.")]
    [SerializeField] private Transform noiseOrigin;

    private float intensityMultiplier = 1f;

    private bool debugLogging;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastNoisePosition => _lastNoisePosition;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float LastNoiseIntensity => _lastNoiseIntensity;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public NoiseType LastNoiseType => _lastNoiseType;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float LastNoiseTime => _lastNoiseTime;

    [SerializeField, HideInInspector] private Vector2 _lastNoisePosition;
    [SerializeField, HideInInspector] private float _lastNoiseIntensity;
    [SerializeField, HideInInspector] private NoiseType _lastNoiseType;
    [SerializeField, HideInInspector] private float _lastNoiseTime;

    public void EmitNoise(float intensity, NoiseType noiseType)
    {
        EmitNoise(GetNoiseOriginPosition(), intensity, noiseType, gameObject);
    }

    public void EmitNoise(Vector2 position, float intensity, NoiseType noiseType, GameObject source = null)
    {
        float scaledIntensity = Mathf.Max(0f, intensity * intensityMultiplier);
        if (scaledIntensity <= 0f)
            return;

        GameObject resolvedSource = source != null ? source : gameObject;
        NoiseManager.EmitNoise(position, scaledIntensity, noiseType, resolvedSource);

        _lastNoisePosition = position;
        _lastNoiseIntensity = scaledIntensity;
        _lastNoiseType = noiseType;
        _lastNoiseTime = Time.time;

        if (debugLogging)
            Debug.Log($"{name} emitted {noiseType} noise at {position} with intensity {scaledIntensity:0.00}.", this);
    }

    public void ApplySettings(PlayerNoiseEmitterSettings settings)
    {
        if (settings == null)
            return;

        intensityMultiplier = Mathf.Max(0f, settings.IntensityMultiplier);
        debugLogging = settings.DebugLogging;
    }

    public Vector2 GetNoiseOriginPosition()
    {
        return noiseOrigin != null ? (Vector2)noiseOrigin.position : (Vector2)transform.position;
    }
}
