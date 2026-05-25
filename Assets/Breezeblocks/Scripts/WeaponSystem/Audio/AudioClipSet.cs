using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[Serializable]
public class AudioClipSet
{
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<AudioClip> clips = new();

    [MinValue(0f)]
    [SerializeField] private float volume = 1f;

    [MinMaxSlider(0.1f, 3f, true)]
    [SerializeField] private Vector2 pitchRange = Vector2.one;

    [LabelText("Override Spatial Blend")]
    [SerializeField] private bool overrideSpatialBlend;

    [ShowIf(nameof(overrideSpatialBlend))]
    [Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 1f;

    [LabelText("Override 3D Distance")]
    [SerializeField] private bool overrideDistanceRange;

    [ShowIf(nameof(overrideDistanceRange))]
    [MinValue(0f)]
    [SerializeField] private float minDistance = 1.5f;

    [ShowIf(nameof(overrideDistanceRange))]
    [MinValue(0f)]
    [SerializeField] private float maxDistance = 24f;

    public IReadOnlyList<AudioClip> Clips => clips;
    public float Volume => volume;
    public Vector2 PitchRange => pitchRange;
    public bool HasAnyClip => clips != null && clips.Count > 0;
    public bool OverrideSpatialBlend => overrideSpatialBlend;
    public float SpatialBlend => Mathf.Clamp01(spatialBlend);
    public bool OverrideDistanceRange => overrideDistanceRange;
    public float MinDistance => Mathf.Max(0f, minDistance);
    public float MaxDistance => Mathf.Max(MinDistance, maxDistance);

    public AudioClip GetRandomClip()
    {
        if (!HasAnyClip)
            return null;

        return clips.Count == 1
            ? clips[0]
            : clips[UnityEngine.Random.Range(0, clips.Count)];
    }

    public float GetRandomPitch()
    {
        float minPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
        float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
        return Mathf.Approximately(minPitch, maxPitch)
            ? minPitch
            : UnityEngine.Random.Range(minPitch, maxPitch);
    }

    public float ResolveSpatialBlend(float fallbackSpatialBlend)
    {
        return overrideSpatialBlend
            ? Mathf.Clamp01(spatialBlend)
            : Mathf.Clamp01(fallbackSpatialBlend);
    }

    public float ResolveMinDistance(float fallbackMinDistance)
    {
        return overrideDistanceRange
            ? Mathf.Max(0f, minDistance)
            : Mathf.Max(0f, fallbackMinDistance);
    }

    public float ResolveMaxDistance(float resolvedMinDistance, float fallbackMaxDistance)
    {
        return overrideDistanceRange
            ? Mathf.Max(Mathf.Max(0f, resolvedMinDistance), maxDistance)
            : Mathf.Max(Mathf.Max(0f, resolvedMinDistance), fallbackMaxDistance);
    }

    public void Validate()
    {
        clips ??= new List<AudioClip>();
        volume = Mathf.Max(0f, volume);

        float minPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
        float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
        pitchRange = new Vector2(minPitch, maxPitch);
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minDistance = Mathf.Max(0f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
    }
}
}
