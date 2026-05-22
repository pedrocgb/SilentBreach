using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;

namespace Breezeblocks.Missions
{

public enum MissionMusicCue
{
    None,
    Lurking,
    Alerted,
    GameOver
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Mission Music Controller")]
public class MissionMusicController : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private AudioSource musicSource;

    [FoldoutGroup("Audio")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [FoldoutGroup("Audio"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float fadeOutDuration = 0.6f;

    [FoldoutGroup("Audio"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float fadeInDuration = 0.6f;

    [FoldoutGroup("Lurking")]
    [SerializeField] private AudioClip lurkingMusic;

    [FoldoutGroup("Lurking"), Range(0f, 1f)]
    [SerializeField] private float lurkingVolume = 1f;

    [FoldoutGroup("Alerted")]
    [SerializeField] private AudioClip alertedMusic;

    [FoldoutGroup("Alerted"), Range(0f, 1f)]
    [SerializeField] private float alertedVolume = 1f;

    [FoldoutGroup("Game Over")]
    [SerializeField] private AudioClip gameOverMusic;

    [FoldoutGroup("Game Over"), Range(0f, 1f)]
    [SerializeField] private float gameOverVolume = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public MissionMusicCue CurrentCue => currentCue;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float ExternalVolumeMultiplier => externalVolumeMultiplier;

    private Coroutine transitionRoutine;
    private Tween activeFadeTween;
    private MissionMusicCue currentCue;
    private float currentBaseVolume;
    private float externalVolumeMultiplier = 1f;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
        ConfigureSource();
        StopImmediately();
    }

    private void OnDisable()
    {
        activeFadeTween?.Kill();
        activeFadeTween = null;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }

    private void OnValidate()
    {
        CacheReferences();
        ConfigureSource();
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        lurkingVolume = Mathf.Clamp01(lurkingVolume);
        alertedVolume = Mathf.Clamp01(alertedVolume);
        gameOverVolume = Mathf.Clamp01(gameOverVolume);
    }

    private void Update()
    {
        ApplyResolvedSourceVolume();
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug")]
    public void PlayLurkingMusic()
    {
        if (currentCue == MissionMusicCue.Alerted || currentCue == MissionMusicCue.GameOver)
            return;

        QueueCue(MissionMusicCue.Lurking);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug")]
    public void PlayAlertedMusic()
    {
        QueueCue(MissionMusicCue.Alerted);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug")]
    public void PlayGameOverMusic()
    {
        QueueCue(MissionMusicCue.GameOver);
    }

    [Button(ButtonSizes.Medium), FoldoutGroup("Debug")]
    public void StopMusic()
    {
        QueueCue(MissionMusicCue.None);
    }

    public void StopImmediately()
    {
        activeFadeTween?.Kill();
        activeFadeTween = null;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        currentCue = MissionMusicCue.None;

        if (musicSource == null)
            return;

        musicSource.Stop();
        musicSource.clip = null;
        currentBaseVolume = 0f;
        ApplyResolvedSourceVolume();
    }

    public void SetExternalVolumeMultiplier(float multiplier)
    {
        externalVolumeMultiplier = Mathf.Clamp01(multiplier);
        ApplyResolvedSourceVolume();
    }

    private void QueueCue(MissionMusicCue cue)
    {
        if (musicSource == null)
            CacheReferences();

        ConfigureSource();

        if (musicSource == null)
            return;

        if (cue == currentCue && musicSource.isPlaying)
            return;

        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        activeFadeTween?.Kill();
        activeFadeTween = null;
        transitionRoutine = StartCoroutine(TransitionToCueRoutine(cue));
    }

    private IEnumerator TransitionToCueRoutine(MissionMusicCue cue)
    {
        if (musicSource != null && musicSource.isPlaying && musicSource.volume > 0f)
        {
            activeFadeTween = DOTween.To(
                    () => currentBaseVolume,
                    value => currentBaseVolume = Mathf.Clamp01(value),
                    0f,
                    fadeOutDuration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);

            yield return activeFadeTween.WaitForCompletion();
        }

        if (musicSource != null)
            musicSource.Stop();

        activeFadeTween = null;

        if (!TryResolveCueSettings(cue, out AudioClip clip, out float targetVolume, out bool shouldLoop) || clip == null || musicSource == null)
        {
            currentCue = MissionMusicCue.None;
            transitionRoutine = null;
            yield break;
        }

        currentCue = cue;
        musicSource.clip = clip;
        musicSource.loop = shouldLoop;
        currentBaseVolume = 0f;
        ApplyResolvedSourceVolume();
        musicSource.Play();

        activeFadeTween = DOTween.To(
                () => currentBaseVolume,
                value => currentBaseVolume = Mathf.Clamp01(value),
                Mathf.Clamp01(targetVolume),
                fadeInDuration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);

        yield return activeFadeTween.WaitForCompletion();
        activeFadeTween = null;
        transitionRoutine = null;
    }

    private bool TryResolveCueSettings(MissionMusicCue cue, out AudioClip clip, out float volume, out bool loop)
    {
        clip = null;
        volume = 0f;
        loop = true;

        switch (cue)
        {
            case MissionMusicCue.Lurking:
                clip = lurkingMusic;
                volume = lurkingVolume;
                loop = true;
                return true;

            case MissionMusicCue.Alerted:
                clip = alertedMusic;
                volume = alertedVolume;
                loop = true;
                return true;

            case MissionMusicCue.GameOver:
                clip = gameOverMusic;
                volume = gameOverVolume;
                loop = false;
                return true;

            default:
                return false;
        }
    }

    private void CacheReferences()
    {
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();
    }

    private void ConfigureSource()
    {
        if (musicSource == null)
            return;

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.outputAudioMixerGroup = outputMixerGroup;
        musicSource.spatialBlend = 0f;
        musicSource.dopplerLevel = 0f;
        ApplyResolvedSourceVolume();
    }

    private void ApplyResolvedSourceVolume()
    {
        if (musicSource == null)
            return;

        musicSource.volume = Mathf.Clamp01(currentBaseVolume * externalVolumeMultiplier);
    }
}

}
