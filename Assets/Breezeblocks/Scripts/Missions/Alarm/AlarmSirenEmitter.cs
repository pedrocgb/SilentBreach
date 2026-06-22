using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("Breezeblocks/Missions/Alarm/Alarm Siren Emitter")]
public sealed class AlarmSirenEmitter : MonoBehaviour
{
    [FoldoutGroup("Siren"), AssetsOnly]
    [SerializeField] private AudioClip sirenLoop;

    [FoldoutGroup("Siren"), Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [FoldoutGroup("Siren"), MinMaxSlider(0.1f, 3f, true)]
    [SerializeField] private Vector2 pitchRange = Vector2.one;

    [FoldoutGroup("Siren")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [FoldoutGroup("Spatial"), Range(0f, 1f)]
    [SerializeField] private float spatialBlend = 1f;

    [FoldoutGroup("Spatial"), MinValue(0f)]
    [SerializeField] private float minDistance = 1.5f;

    [FoldoutGroup("Spatial"), MinValue(0f)]
    [SerializeField] private float maxDistance = 24f;

    [FoldoutGroup("Spatial")]
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [FoldoutGroup("Spatial"), MinValue(0f)]
    [SerializeField] private float dopplerLevel;

    [FoldoutGroup("Spatial"), Range(0f, 360f)]
    [SerializeField] private float spread;

    [FoldoutGroup("Spatial"), Range(0, 256)]
    [SerializeField] private int priority = 96;

    [FoldoutGroup("Noise")]
    [SerializeField] private bool emitNoise = true;

    [FoldoutGroup("Noise"), ShowIf(nameof(emitNoise)), MinValue(0f)]
    [SerializeField] private float noiseAmount = 1f;

    [FoldoutGroup("Noise"), ShowIf(nameof(emitNoise))]
    [SerializeField] private NoiseType noiseType = NoiseType.Loud;

    [FoldoutGroup("Noise"), ShowIf(nameof(emitNoise))]
    [SerializeField] private bool extremeNoise;

    [FoldoutGroup("Noise"), ShowIf(nameof(emitNoise)), MinValue(0.05f), SuffixLabel("s", true)]
    [SerializeField] private float noiseInterval = 1f;

    private AudioSource audioSource;
    private Coroutine noiseEmissionRoutine;
    private bool isAlarmActive;

    /// <summary>
    /// Caches and configures the same-object AudioSource used for siren playback.
    /// </summary>
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
    }

    /// <summary>
    /// Resumes authored siren feedback when this emitter is re-enabled during an active alarm.
    /// </summary>
    private void OnEnable()
    {
        if (!isAlarmActive || audioSource == null)
            return;

        PlaySiren();
        StartNoiseEmission();
    }

    /// <summary>
    /// Stops local playback and noise work while preserving active alarm state for re-enabling.
    /// </summary>
    private void OnDisable()
    {
        StopSiren();
        StopNoiseEmission();
    }

    /// <summary>
    /// Clamps siren values while editing.
    /// </summary>
    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
        float minimumPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
        float maximumPitch = Mathf.Max(minimumPitch, Mathf.Max(pitchRange.x, pitchRange.y));
        pitchRange = new Vector2(minimumPitch, maximumPitch);
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minDistance = Mathf.Max(0f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        dopplerLevel = Mathf.Max(0f, dopplerLevel);
        spread = Mathf.Clamp(spread, 0f, 360f);
        priority = Mathf.Clamp(priority, 0, 256);
        noiseAmount = Mathf.Max(0f, noiseAmount);
        noiseInterval = Mathf.Max(0.05f, noiseInterval);
    }

    /// <summary>
    /// Starts or stops the configured looping siren clip.
    /// </summary>
    public void SetAlarmActive(bool active)
    {
        isAlarmActive = active;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            ConfigureAudioSource();
        }

        if (active)
        {
            PlaySiren();
            StartNoiseEmission();
            return;
        }

        StopSiren();
        StopNoiseEmission();
    }

    /// <summary>
    /// Applies loop and volume settings before siren playback begins.
    /// </summary>
    private void ConfigureAudioSource()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.outputAudioMixerGroup = outputMixerGroup;
        audioSource.spatialBlend = spatialBlend;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.rolloffMode = rolloffMode;
        audioSource.dopplerLevel = dopplerLevel;
        audioSource.spread = spread;
        audioSource.priority = priority;
    }

    /// <summary>
    /// Plays the assigned siren clip, or the AudioSource's existing clip if no override is configured.
    /// </summary>
    private void PlaySiren()
    {
        if (audioSource == null)
            return;

        if (sirenLoop != null)
            audioSource.clip = sirenLoop;

        if (audioSource.clip == null)
            return;

        audioSource.loop = true;
        audioSource.volume = volume;
        audioSource.pitch = ResolvePitch();
        ConfigureAudioSource();
        if (!audioSource.isPlaying)
            audioSource.Play();
    }

    /// <summary>
    /// Stops siren playback without destroying the configured AudioSource clip.
    /// </summary>
    private void StopSiren()
    {
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    /// <summary>
    /// Starts one periodic hearing-noise routine while alarm remains active.
    /// </summary>
    private void StartNoiseEmission()
    {
        if (!emitNoise || noiseAmount <= 0f || noiseEmissionRoutine != null || !isActiveAndEnabled)
            return;

        noiseEmissionRoutine = StartCoroutine(EmitNoiseWhileActive());
    }

    /// <summary>
    /// Stops periodic hearing noise when alarm or emitter becomes inactive.
    /// </summary>
    private void StopNoiseEmission()
    {
        if (noiseEmissionRoutine == null)
            return;

        StopCoroutine(noiseEmissionRoutine);
        noiseEmissionRoutine = null;
    }

    /// <summary>
    /// Emits alarm noise immediately and at controlled intervals until alarm stops.
    /// </summary>
    private IEnumerator EmitNoiseWhileActive()
    {
        WaitForSeconds interval = new(Mathf.Max(0.05f, noiseInterval));
        while (isAlarmActive && isActiveAndEnabled)
        {
            NoiseManager.EmitNoise(transform.position, noiseAmount, noiseType, gameObject, extremeNoise);
            yield return interval;
        }

        noiseEmissionRoutine = null;
    }

    /// <summary>
    /// Selects one pitch inside authored range whenever siren playback starts.
    /// </summary>
    private float ResolvePitch()
    {
        float minimumPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
        float maximumPitch = Mathf.Max(minimumPitch, Mathf.Max(pitchRange.x, pitchRange.y));
        return Mathf.Approximately(minimumPitch, maximumPitch)
            ? minimumPitch
            : Random.Range(minimumPitch, maximumPitch);
    }
}

}
