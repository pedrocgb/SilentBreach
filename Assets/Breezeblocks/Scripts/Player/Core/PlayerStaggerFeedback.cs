using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Stagger Feedback")]
[RequireComponent(typeof(ActorStaggerController))]
public class PlayerStaggerFeedback : MonoBehaviour
{
    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorStaggerController actorStaggerController;

    [FoldoutGroup("References")]
    [SerializeField] private Volume targetVolume;

    private float fullStrengthReferenceDuration = 0.5f;
    private float effectLerpSpeed = 10f;
    private float maxVignetteIntensity = 0.32f;
    private float maxChromaticAberration = 0.22f;
    private float maxLensDistortion = -0.18f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentEffectStrength => currentEffectStrength;

    private VolumeProfile runtimeProfile;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private float currentEffectStrength;
    private float baseVignetteIntensity;
    private float baseChromaticAberrationIntensity;
    private float baseLensDistortionIntensity;

    // Executes the Reset routine.
    private void Reset()
    {
        actorStaggerController = GetComponent<ActorStaggerController>();
        if (targetVolume == null)
            targetVolume = PlayerSceneReferenceUtility.FindPlayerVolume(gameObject);
    }

    // Executes the Awake routine.
    private void Awake()
    {
        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        if (targetVolume == null)
            targetVolume = PlayerSceneReferenceUtility.FindPlayerVolume(gameObject);

        CacheVolumeOverrides();
        ApplyEffectStrength(0f);
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        ApplyEffectStrength(0f);
    }

    /// <summary>
    /// Clamps profile-applied feedback values while editing.
    /// </summary>
    private void OnValidate()
    {
        ClampSettings();
    }

    /// <summary>
    /// Applies profile-authored stagger feedback post-processing values.
    /// </summary>
    public void ApplySettings(PlayerStaggerFeedbackSettings settings)
    {
        if (settings == null)
            return;

        fullStrengthReferenceDuration = settings.FullStrengthReferenceDuration;
        effectLerpSpeed = settings.EffectLerpSpeed;
        maxVignetteIntensity = settings.MaxVignetteIntensity;
        maxChromaticAberration = settings.MaxChromaticAberration;
        maxLensDistortion = settings.MaxLensDistortion;
        ClampSettings();
    }

    // Executes the Update routine.
    private void Update()
    {
        float targetStrength = 0f;
        if (actorStaggerController != null && actorStaggerController.IsStaggered)
            targetStrength = Mathf.Clamp01(actorStaggerController.RemainingStaggerTime / Mathf.Max(0.01f, fullStrengthReferenceDuration));

        if (effectLerpSpeed <= 0f)
            currentEffectStrength = targetStrength;
        else
            currentEffectStrength = Mathf.MoveTowards(currentEffectStrength, targetStrength, effectLerpSpeed * Time.deltaTime);

        ApplyEffectStrength(currentEffectStrength);
    }

    // Executes the CacheVolumeOverrides routine.
    private void CacheVolumeOverrides()
    {
        if (targetVolume == null)
            return;

        runtimeProfile = targetVolume.profile;
        if (runtimeProfile == null)
            return;

        if (!runtimeProfile.TryGet(out vignette))
            vignette = runtimeProfile.Add<Vignette>(true);

        if (!runtimeProfile.TryGet(out chromaticAberration))
            chromaticAberration = runtimeProfile.Add<ChromaticAberration>(true);

        if (!runtimeProfile.TryGet(out lensDistortion))
            lensDistortion = runtimeProfile.Add<LensDistortion>(true);

        baseVignetteIntensity = vignette != null ? vignette.intensity.value : 0f;
        baseChromaticAberrationIntensity = chromaticAberration != null ? chromaticAberration.intensity.value : 0f;
        baseLensDistortionIntensity = lensDistortion != null ? lensDistortion.intensity.value : 0f;
    }

    // Executes the ApplyEffectStrength routine.
    private void ApplyEffectStrength(float strength)
    {
        if (runtimeProfile == null)
            return;

        if (vignette != null)
        {
            vignette.active = true;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = Mathf.Lerp(baseVignetteIntensity, maxVignetteIntensity, strength);
        }

        if (chromaticAberration != null)
        {
            chromaticAberration.active = true;
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.value = Mathf.Lerp(baseChromaticAberrationIntensity, maxChromaticAberration, strength);
        }

        if (lensDistortion != null)
        {
            lensDistortion.active = true;
            lensDistortion.intensity.overrideState = true;
            lensDistortion.intensity.value = Mathf.Lerp(baseLensDistortionIntensity, maxLensDistortion, strength);
        }
    }

    /// <summary>
    /// Keeps feedback intensity and timing values in safe ranges.
    /// </summary>
    private void ClampSettings()
    {
        fullStrengthReferenceDuration = Mathf.Max(0.01f, fullStrengthReferenceDuration);
        effectLerpSpeed = Mathf.Max(0f, effectLerpSpeed);
        maxVignetteIntensity = Mathf.Clamp01(maxVignetteIntensity);
        maxChromaticAberration = Mathf.Clamp01(maxChromaticAberration);
        maxLensDistortion = Mathf.Clamp(maxLensDistortion, -1f, 1f);
    }
}
