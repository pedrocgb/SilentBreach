using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Breezeblocks.Missions;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Flashbang Effect")]
public class PlayerFlashbangEffect : MonoBehaviour
{
    private const string RuntimeCanvasName = "Player Flashbang Canvas";
    private const string RuntimeImageName = "Flashbang Whiteout";
    private const string RuntimeAudioName = "Flashbang Ringing";

    [FoldoutGroup("References")]
    [SerializeField] private Canvas overlayCanvas;

    [FoldoutGroup("References")]
    [SerializeField] private Image whiteoutImage;

    [FoldoutGroup("References")]
    [SerializeField] private AudioSource ringingAudioSource;

    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("References")]
    [SerializeField] private MissionMusicController missionMusicController;

    [FoldoutGroup("References")]
    [SerializeField] private GameplayMissionController gameplayMissionController;

    [FoldoutGroup("Audio")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float ringingMaxVolume = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float EffectTimeRemaining => Mathf.Max(0f, effectEndTime - Time.unscaledTime);

    private float effectEndTime = float.NegativeInfinity;
    private float recoveryStartTime = float.NegativeInfinity;

    // Executes the EnsureOn routine.
    public static PlayerFlashbangEffect EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        PlayerFlashbangEffect effect = actorRoot.GetComponent<PlayerFlashbangEffect>();
        if (effect == null)
            effect = actorRoot.AddComponent<PlayerFlashbangEffect>();

        effect.EnsureRuntimePresentation();
        return effect;
    }

    // Executes the Awake routine.
    private void Awake()
    {
        EnsureRuntimePresentation();
        ResolveManagedAudioControllers();
        SetWhiteoutAlpha(0f);
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        StopEffect();
    }

    // Executes the Update routine.
    private void Update()
    {
        if (effectEndTime <= float.NegativeInfinity)
            return;

        if (Time.unscaledTime >= effectEndTime)
        {
            StopEffect();
            return;
        }

        float recoveryProgress = ResolveRecoveryProgress();
        SetWhiteoutAlpha(1f - recoveryProgress);
        ApplyAudioSuppression(recoveryProgress);

        if (ringingAudioSource != null)
        {
            ringingAudioSource.volume = Mathf.Clamp01(ringingMaxVolume) * (1f - recoveryProgress);
            if (!ringingAudioSource.isPlaying && ringingAudioSource.clip != null)
                ringingAudioSource.Play();
        }
    }

    // Executes the ApplyFlashbang routine.
    public void ApplyFlashbang(float duration, float recoveryThreshold, AudioClip ringingLoopClip, float ringingSpatialBlend)
    {
        duration = Mathf.Max(0.01f, duration);
        recoveryThreshold = Mathf.Clamp(recoveryThreshold, 0f, duration);

        enabled = true;
        EnsureRuntimePresentation();
        ResolveManagedAudioControllers();
        effectEndTime = Mathf.Max(effectEndTime, Time.unscaledTime + duration);
        recoveryStartTime = Mathf.Max(recoveryStartTime, Time.unscaledTime + recoveryThreshold);
        SetWhiteoutAlpha(1f);
        ApplyAudioSuppression(0f);

        if (ringingAudioSource != null)
        {
            ringingAudioSource.spatialBlend = Mathf.Clamp01(ringingSpatialBlend);
            if (ringingLoopClip != null)
                ringingAudioSource.clip = ringingLoopClip;

            if (ringingAudioSource.clip != null)
            {
                ringingAudioSource.loop = true;
                ringingAudioSource.volume = Mathf.Clamp01(ringingMaxVolume);
                if (!ringingAudioSource.isPlaying)
                    ringingAudioSource.Play();
            }
        }
    }

    // Executes the ResolveRecoveryProgress routine.
    private float ResolveRecoveryProgress()
    {
        if (Time.unscaledTime <= recoveryStartTime)
            return 0f;

        if (effectEndTime <= recoveryStartTime)
            return 1f;

        return Mathf.InverseLerp(recoveryStartTime, effectEndTime, Time.unscaledTime);
    }

    // Executes the StopEffect routine.
    private void StopEffect()
    {
        effectEndTime = float.NegativeInfinity;
        recoveryStartTime = float.NegativeInfinity;
        SetWhiteoutAlpha(0f);
        ApplyAudioSuppression(1f);

        if (ringingAudioSource != null)
            ringingAudioSource.Stop();
    }

    // Executes the EnsureRuntimePresentation routine.
    private void EnsureRuntimePresentation()
    {
        if (overlayCanvas == null)
        {
            Canvas[] canvases = transform.root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && string.Equals(canvases[i].name, RuntimeCanvasName, StringComparison.Ordinal))
                {
                    overlayCanvas = canvases[i];
                    break;
                }
            }
        }

        if (whiteoutImage == null && overlayCanvas != null)
        {
            Transform existingImage = overlayCanvas.transform.Find(RuntimeImageName);
            if (existingImage != null)
                whiteoutImage = existingImage.GetComponent<Image>();
        }

        if (ringingAudioSource == null)
        {
            Transform existingAudio = transform.Find(RuntimeAudioName);
            if (existingAudio != null)
                ringingAudioSource = existingAudio.GetComponent<AudioSource>();
        }

        if (ringingAudioSource != null)
        {
            ResolveAudioRouting();
            ringingAudioSource.playOnAwake = false;
            ringingAudioSource.loop = true;
            ringingAudioSource.outputAudioMixerGroup = outputMixerGroup;
            ringingAudioSource.ignoreListenerVolume = false;

            if (worldSfxManager != null)
            {
                ringingAudioSource.minDistance = worldSfxManager.DefaultMinDistance;
                ringingAudioSource.maxDistance = worldSfxManager.DefaultMaxDistance;
            }
        }
    }

    // Executes the ResolveAudioRouting routine.
    private void ResolveAudioRouting()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        if (outputMixerGroup == null && worldSfxManager != null)
            outputMixerGroup = worldSfxManager.OutputMixerGroup;
    }

    // Executes the ResolveManagedAudioControllers routine.
    private void ResolveManagedAudioControllers()
    {
        ResolveAudioRouting();

        if (missionMusicController == null)
            missionMusicController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<MissionMusicController>(gameObject);

        if (gameplayMissionController == null)
            gameplayMissionController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<GameplayMissionController>(gameObject);
    }

    // Executes the ApplyAudioSuppression routine.
    private void ApplyAudioSuppression(float recoveryProgress)
    {
        float volumeMultiplier = Mathf.Clamp01(recoveryProgress);

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        if (missionMusicController == null)
            missionMusicController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<MissionMusicController>(gameObject);

        if (gameplayMissionController == null)
            gameplayMissionController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<GameplayMissionController>(gameObject);

        if (worldSfxManager != null)
            worldSfxManager.SetExternalVolumeMultiplier(volumeMultiplier);

        if (missionMusicController != null)
            missionMusicController.SetExternalVolumeMultiplier(volumeMultiplier);

        if (gameplayMissionController != null)
            gameplayMissionController.SetExternalCarAudioVolumeMultiplier(volumeMultiplier);
    }

    // Executes the SetWhiteoutAlpha routine.
    private void SetWhiteoutAlpha(float alpha)
    {
        if (whiteoutImage == null)
            return;

        Color color = whiteoutImage.color;
        color.a = Mathf.Clamp01(alpha);
        whiteoutImage.color = color;
        whiteoutImage.enabled = color.a > 0.001f;
    }
}

}
