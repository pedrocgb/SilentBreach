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

    [FoldoutGroup("Effect"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float fullStrengthReferenceDuration = 0.5f;

    [FoldoutGroup("Effect"), MinValue(0f)]
    [SerializeField] private float effectLerpSpeed = 10f;

    [FoldoutGroup("Effect"), Range(0f, 1f)]
    [SerializeField] private float maxVignetteIntensity = 0.32f;

    [FoldoutGroup("Effect"), Range(0f, 1f)]
    [SerializeField] private float maxChromaticAberration = 0.22f;

    [FoldoutGroup("Effect"), Range(-1f, 1f)]
    [SerializeField] private float maxLensDistortion = -0.18f;

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
}
