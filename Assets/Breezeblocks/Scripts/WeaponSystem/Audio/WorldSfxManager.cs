using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Audio/World SFX Manager")]
public class WorldSfxManager : MonoBehaviour
{
    [System.Serializable]
    private class OcclusionSettings
    {
        [EnumToggleButtons]
        public NoiseType noiseType = NoiseType.Common;

        [Range(0f, 1f)]
        [Tooltip("Volume multiplier applied once per blocking wall. Example: 0.9 means 10% reduction per wall.")]
        public float perWallVolumeMultiplier = 0.5f;

        [Range(0.01f, 1f)]
        [Tooltip("Low-pass cutoff multiplier applied once per blocking wall.")]
        public float perWallLowPassMultiplier = 0.55f;
    }

    private sealed class RuntimeSource
    {
        public AudioSource Source;
        public AudioLowPassFilter LowPassFilter;
        public float BusyUntil;
        public float BaseVolume;
    }

    public static WorldSfxManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<WorldSfxManager>();
                if (instance == null)
                {
                    GameObject managerObject = new("World SFX Manager");
                    instance = managerObject.AddComponent<WorldSfxManager>();
                }
            }

            return instance;
        }
    }

    [FoldoutGroup("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [FoldoutGroup("Pooling"), MinValue(1)]
    [SerializeField] private int initialPoolSize = 12;

    [FoldoutGroup("Pooling"), MinValue(1)]
    [SerializeField] private int maxPoolSize = 48;

    [FoldoutGroup("Audio")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 1f;

    [FoldoutGroup("Audio"), MinValue(0f)]
    [SerializeField] private float minDistance = 1.5f;

    [FoldoutGroup("Audio"), MinValue(0f)]
    [SerializeField] private float maxDistance = 24f;

    [FoldoutGroup("Audio")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [FoldoutGroup("Audio"), MinValue(0f)]
    [SerializeField] private float dopplerLevel = 0f;

    [FoldoutGroup("Audio"), Range(0f, 360f)]
    [SerializeField] private float spread;

    [FoldoutGroup("Audio"), Range(0, 256)]
    [SerializeField] private int priority = 96;

    [FoldoutGroup("Occlusion")]
    [SerializeField] private bool enablePlayerOcclusion = true;

    [FoldoutGroup("Occlusion")]
    [Tooltip("Optional explicit listener override. If empty, the active AudioListener transform is used.")]
    [SerializeField] private Transform listenerTransformOverride;

    [FoldoutGroup("Occlusion")]
    [SerializeField] private LayerMask occlusionMask;

    [FoldoutGroup("Occlusion"), MinValue(1)]
    [SerializeField] private int maxOcclusionHits = 16;

    [FoldoutGroup("Occlusion"), MinValue(10f)]
    [SerializeField] private float openLowPassCutoff = 22000f;

    [FoldoutGroup("Occlusion"), MinValue(10f)]
    [SerializeField] private float minimumOccludedLowPassCutoff = 500f;

    [FoldoutGroup("Occlusion")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<OcclusionSettings> occlusionByNoiseType = new()
    {
        new OcclusionSettings { noiseType = NoiseType.Loud, perWallVolumeMultiplier = 0.9f, perWallLowPassMultiplier = 0.8f },
        new OcclusionSettings { noiseType = NoiseType.Common, perWallVolumeMultiplier = 0.5f, perWallLowPassMultiplier = 0.55f },
        new OcclusionSettings { noiseType = NoiseType.Silent, perWallVolumeMultiplier = 0.2f, perWallLowPassMultiplier = 0.4f }
    };

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public int RuntimeSourceCount => runtimeSources.Count;

    public AudioMixerGroup OutputMixerGroup => outputMixerGroup;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float ExternalVolumeMultiplier => externalVolumeMultiplier;

    private static WorldSfxManager instance;
    private readonly List<RuntimeSource> runtimeSources = new();
    private RaycastHit2D[] occlusionHitBuffer;
    private readonly HashSet<int> uniqueOccluderIds = new();
    private AudioListener cachedAudioListener;
    private float externalVolumeMultiplier = 1f;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        ValidateSettings();
        Prewarm(initialPoolSize);
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    public bool PlayClipSetAt(Vector3 position, AudioClipSet clipSet, NoiseType noiseType, float additionalVolumeMultiplier = 1f, float delay = 0f)
    {
        if (clipSet == null || !clipSet.HasAnyClip)
            return false;

        AudioClip clip = clipSet.GetRandomClip();
        float pitch = clipSet.GetRandomPitch();
        float resolvedSpatialBlend = clipSet.ResolveSpatialBlend(spatialBlend);
        return PlayClipAt(position, clip, noiseType, clipSet.Volume * Mathf.Max(0f, additionalVolumeMultiplier), pitch, delay, resolvedSpatialBlend);
    }

    public bool PlayClipSetAt(Vector3 position, AudioClipSet clipSet, NoiseType noiseType, out float playbackDuration, float additionalVolumeMultiplier = 1f, float delay = 0f)
    {
        playbackDuration = 0f;
        if (clipSet == null || !clipSet.HasAnyClip)
            return false;

        AudioClip clip = clipSet.GetRandomClip();
        float pitch = clipSet.GetRandomPitch();
        playbackDuration = clip != null ? clip.length / Mathf.Max(0.01f, pitch) : 0f;
        float resolvedSpatialBlend = clipSet.ResolveSpatialBlend(spatialBlend);
        return PlayClipAt(position, clip, noiseType, clipSet.Volume * Mathf.Max(0f, additionalVolumeMultiplier), pitch, delay, resolvedSpatialBlend);
    }

    public bool PlayClipAt(Vector3 position, AudioClip clip, NoiseType noiseType, float volume = 1f, float pitch = 1f, float delay = 0f, float spatialBlendOverride = -1f)
    {
        if (clip == null)
            return false;

        RuntimeSource runtimeSource = GetAvailableSource();
        if (runtimeSource == null || runtimeSource.Source == null)
            return false;

        AudioSource source = runtimeSource.Source;
        source.transform.position = position;
        source.clip = clip;
        runtimeSource.BaseVolume = Mathf.Max(0f, volume);
        source.volume = 0f;
        source.pitch = Mathf.Max(0.01f, pitch);
        source.outputAudioMixerGroup = outputMixerGroup;
        source.spatialBlend = spatialBlendOverride >= 0f ? Mathf.Clamp01(spatialBlendOverride) : spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = rolloffMode;
        source.dopplerLevel = dopplerLevel;
        source.spread = spread;
        source.priority = priority;
        source.loop = false;
        ApplyPlayerOcclusion(runtimeSource, position, noiseType, runtimeSource.BaseVolume);
        source.PlayDelayed(Mathf.Max(0f, delay));

        float clipDuration = Mathf.Max(0.01f, clip.length / Mathf.Max(0.01f, source.pitch));
        runtimeSource.BusyUntil = Time.time + Mathf.Max(0f, delay) + clipDuration;
        return true;
    }

    public void SetExternalVolumeMultiplier(float multiplier)
    {
        externalVolumeMultiplier = Mathf.Clamp01(multiplier);

        for (int i = 0; i < runtimeSources.Count; i++)
        {
            RuntimeSource runtimeSource = runtimeSources[i];
            if (runtimeSource?.Source == null)
                continue;

            ApplyRuntimeSourceVolume(runtimeSource, runtimeSource.BaseVolume);
        }
    }

    private void Prewarm(int targetCount)
    {
        int clampedTarget = Mathf.Clamp(targetCount, 0, maxPoolSize);
        while (runtimeSources.Count < clampedTarget)
            runtimeSources.Add(CreateRuntimeSource(runtimeSources.Count));
    }

    private RuntimeSource GetAvailableSource()
    {
        float now = Time.time;
        for (int i = 0; i < runtimeSources.Count; i++)
        {
            RuntimeSource runtimeSource = runtimeSources[i];
            if (runtimeSource?.Source == null)
                continue;

            if (!runtimeSource.Source.isPlaying && now >= runtimeSource.BusyUntil)
                return runtimeSource;
        }

        if (runtimeSources.Count >= maxPoolSize)
            return runtimeSources.Count > 0 ? runtimeSources[0] : null;

        RuntimeSource newSource = CreateRuntimeSource(runtimeSources.Count);
        runtimeSources.Add(newSource);
        return newSource;
    }

    private RuntimeSource CreateRuntimeSource(int index)
    {
        GameObject sourceObject = new($"World SFX Source {index + 1}");
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        AudioLowPassFilter lowPassFilter = sourceObject.AddComponent<AudioLowPassFilter>();
        source.playOnAwake = false;
        source.loop = false;
        source.outputAudioMixerGroup = outputMixerGroup;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = rolloffMode;
        source.dopplerLevel = dopplerLevel;
        source.spread = spread;
        source.priority = priority;

        return new RuntimeSource
        {
            Source = source,
            LowPassFilter = lowPassFilter,
            BusyUntil = 0f,
            BaseVolume = 0f
        };
    }

    private void ValidateSettings()
    {
        initialPoolSize = Mathf.Max(1, initialPoolSize);
        maxPoolSize = Mathf.Max(initialPoolSize, maxPoolSize);
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minDistance = Mathf.Max(0f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        dopplerLevel = Mathf.Max(0f, dopplerLevel);
        spread = Mathf.Clamp(spread, 0f, 360f);
        priority = Mathf.Clamp(priority, 0, 256);
        maxOcclusionHits = Mathf.Max(1, maxOcclusionHits);
        openLowPassCutoff = Mathf.Max(10f, openLowPassCutoff);
        minimumOccludedLowPassCutoff = Mathf.Clamp(minimumOccludedLowPassCutoff, 10f, openLowPassCutoff);
        occlusionByNoiseType ??= new List<OcclusionSettings>();
        for (int i = 0; i < occlusionByNoiseType.Count; i++)
        {
            OcclusionSettings settings = occlusionByNoiseType[i];
            if (settings == null)
                continue;

            settings.perWallVolumeMultiplier = Mathf.Clamp01(settings.perWallVolumeMultiplier);
            settings.perWallLowPassMultiplier = Mathf.Clamp(settings.perWallLowPassMultiplier, 0.01f, 1f);
        }

        if (occlusionHitBuffer == null || occlusionHitBuffer.Length != maxOcclusionHits)
            occlusionHitBuffer = new RaycastHit2D[maxOcclusionHits];
    }

    private void ApplyPlayerOcclusion(RuntimeSource runtimeSource, Vector3 sourcePosition, NoiseType noiseType, float baseVolume)
    {
        if (runtimeSource?.Source == null)
            return;

        AudioLowPassFilter lowPassFilter = runtimeSource.LowPassFilter;
        if (!enablePlayerOcclusion)
        {
            ApplyRuntimeSourceVolume(runtimeSource, baseVolume);
            if (lowPassFilter != null)
                lowPassFilter.enabled = false;
            return;
        }

        Transform listenerTransform = ResolveListenerTransform();
        if (listenerTransform == null || occlusionMask.value == 0)
        {
            ApplyRuntimeSourceVolume(runtimeSource, baseVolume);
            if (lowPassFilter != null)
                lowPassFilter.enabled = false;
            return;
        }

        int wallCount = CountBlockingWalls(listenerTransform.position, sourcePosition);
        if (wallCount <= 0)
        {
            ApplyRuntimeSourceVolume(runtimeSource, baseVolume);
            if (lowPassFilter != null)
                lowPassFilter.enabled = false;
            return;
        }

        OcclusionSettings settings = ResolveOcclusionSettings(noiseType);
        float occludedVolume = baseVolume * Mathf.Pow(settings.perWallVolumeMultiplier, wallCount);
        ApplyRuntimeSourceVolume(runtimeSource, occludedVolume);

        if (lowPassFilter == null)
            return;

        lowPassFilter.enabled = true;
        lowPassFilter.cutoffFrequency = Mathf.Max(
            minimumOccludedLowPassCutoff,
            openLowPassCutoff * Mathf.Pow(settings.perWallLowPassMultiplier, wallCount));
    }

    private int CountBlockingWalls(Vector3 listenerPosition, Vector3 sourcePosition)
    {
        if (occlusionHitBuffer == null || occlusionHitBuffer.Length != maxOcclusionHits)
            occlusionHitBuffer = new RaycastHit2D[maxOcclusionHits];

        Vector2 start = new(listenerPosition.x, listenerPosition.y);
        Vector2 end = new(sourcePosition.x, sourcePosition.y);
        int hitCount = Physics2D.Linecast(start, end, new ContactFilter2D
        {
            useLayerMask = occlusionMask.value != 0,
            layerMask = occlusionMask,
            useTriggers = false
        }, occlusionHitBuffer);

        uniqueOccluderIds.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = occlusionHitBuffer[i].collider;
            if (collider == null)
                continue;

            Transform root = collider.transform.root;
            uniqueOccluderIds.Add(root != null ? root.gameObject.GetInstanceID() : collider.gameObject.GetInstanceID());
        }

        return uniqueOccluderIds.Count;
    }

    private OcclusionSettings ResolveOcclusionSettings(NoiseType noiseType)
    {
        if (occlusionByNoiseType != null)
        {
            for (int i = 0; i < occlusionByNoiseType.Count; i++)
            {
                OcclusionSettings settings = occlusionByNoiseType[i];
                if (settings != null && settings.noiseType == noiseType)
                    return settings;
            }
        }

        return new OcclusionSettings { noiseType = noiseType };
    }

    private Transform ResolveListenerTransform()
    {
        if (listenerTransformOverride != null)
            return listenerTransformOverride;

        if (cachedAudioListener == null)
            cachedAudioListener = FindFirstObjectByType<AudioListener>();

        return cachedAudioListener != null ? cachedAudioListener.transform : null;
    }

    private void ApplyRuntimeSourceVolume(RuntimeSource runtimeSource, float volume)
    {
        if (runtimeSource?.Source == null)
            return;

        runtimeSource.BaseVolume = Mathf.Max(0f, volume);
        runtimeSource.Source.volume = Mathf.Max(0f, runtimeSource.BaseVolume) * Mathf.Clamp01(externalVolumeMultiplier);
    }
}
}
