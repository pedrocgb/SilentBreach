using Sirenix.OdinInspector;
using UnityEngine;

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

    private AudioSource audioSource;

    /// <summary>
    /// Caches and configures the same-object AudioSource used for siren playback.
    /// </summary>
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ConfigureAudioSource();
    }

    /// <summary>
    /// Clamps siren values while editing.
    /// </summary>
    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// Starts or stops the configured looping siren clip.
    /// </summary>
    public void SetAlarmActive(bool active)
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            ConfigureAudioSource();
        }

        if (active)
        {
            PlaySiren();
            return;
        }

        StopSiren();
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
}

}
