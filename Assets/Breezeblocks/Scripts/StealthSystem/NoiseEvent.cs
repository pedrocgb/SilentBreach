using UnityEngine;

public readonly struct NoiseEvent
{
    public NoiseEvent(Vector2 position, float intensity, NoiseType noiseType, GameObject source, float timeCreated = -1f)
    {
        Position = position;
        Intensity = intensity;
        NoiseType = noiseType;
        Source = source;
        TimeCreated = timeCreated >= 0f ? timeCreated : Time.time;
    }

    public Vector2 Position { get; }
    public float Intensity { get; }
    public NoiseType NoiseType { get; }
    public GameObject Source { get; }
    public float TimeCreated { get; }
}
