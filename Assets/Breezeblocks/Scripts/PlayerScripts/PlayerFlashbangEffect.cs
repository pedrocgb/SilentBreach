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

    private void Awake()
    {
        EnsureRuntimePresentation();
        ResolveManagedAudioControllers();
        SetWhiteoutAlpha(0f);
    }

    private void OnDisable()
    {
        StopEffect();
    }

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

    private float ResolveRecoveryProgress()
    {
        if (Time.unscaledTime <= recoveryStartTime)
            return 0f;

        if (effectEndTime <= recoveryStartTime)
            return 1f;

        return Mathf.InverseLerp(recoveryStartTime, effectEndTime, Time.unscaledTime);
    }

    private void StopEffect()
    {
        effectEndTime = float.NegativeInfinity;
        recoveryStartTime = float.NegativeInfinity;
        SetWhiteoutAlpha(0f);
        ApplyAudioSuppression(1f);

        if (ringingAudioSource != null)
            ringingAudioSource.Stop();
    }

    private void EnsureRuntimePresentation()
    {
        if (overlayCanvas == null)
        {
            GameObject canvasObject = GameObject.Find(RuntimeCanvasName);
            if (canvasObject == null)
            {
                canvasObject = new GameObject(RuntimeCanvasName);
                overlayCanvas = canvasObject.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }
            else
            {
                overlayCanvas = canvasObject.GetComponent<Canvas>();
            }
        }

        if (whiteoutImage == null && overlayCanvas != null)
        {
            Transform existingImage = overlayCanvas.transform.Find(RuntimeImageName);
            if (existingImage != null)
            {
                whiteoutImage = existingImage.GetComponent<Image>();
            }
            else
            {
                GameObject imageObject = new GameObject(RuntimeImageName);
                imageObject.transform.SetParent(overlayCanvas.transform, false);
                RectTransform rectTransform = imageObject.AddComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                whiteoutImage = imageObject.AddComponent<Image>();
                whiteoutImage.color = Color.white;
                whiteoutImage.raycastTarget = false;
            }
        }

        if (ringingAudioSource == null)
        {
            Transform existingAudio = transform.Find(RuntimeAudioName);
            if (existingAudio != null)
            {
                ringingAudioSource = existingAudio.GetComponent<AudioSource>();
            }
            else
            {
                GameObject audioObject = new GameObject(RuntimeAudioName);
                audioObject.transform.SetParent(transform, false);
                ringingAudioSource = audioObject.AddComponent<AudioSource>();
            }
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

    private void ResolveAudioRouting()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        if (outputMixerGroup == null && worldSfxManager != null)
            outputMixerGroup = worldSfxManager.OutputMixerGroup;
    }

    private void ResolveManagedAudioControllers()
    {
        ResolveAudioRouting();

        if (missionMusicController == null)
            missionMusicController = FindFirstObjectByType<MissionMusicController>();

        if (gameplayMissionController == null)
            gameplayMissionController = FindFirstObjectByType<GameplayMissionController>();
    }

    private void ApplyAudioSuppression(float recoveryProgress)
    {
        float volumeMultiplier = Mathf.Clamp01(recoveryProgress);

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        if (missionMusicController == null)
            missionMusicController = FindFirstObjectByType<MissionMusicController>();

        if (gameplayMissionController == null)
            gameplayMissionController = FindFirstObjectByType<GameplayMissionController>();

        if (worldSfxManager != null)
            worldSfxManager.SetExternalVolumeMultiplier(volumeMultiplier);

        if (missionMusicController != null)
            missionMusicController.SetExternalVolumeMultiplier(volumeMultiplier);

        if (gameplayMissionController != null)
            gameplayMissionController.SetExternalCarAudioVolumeMultiplier(volumeMultiplier);
    }

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
