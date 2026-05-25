using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public enum ThrowableUtilityBehavior
{
    NoiseMaker,
    DirectDamage,
    Explosion,
    Flashbang
}

public enum ThrowableDetonationMode
{
    OnHit,
    OnTimer,
    OnHitAndTimer
}

[CreateAssetMenu(fileName = "ThrowableUtilityData", menuName = "Breezeblocks/Equipment/Throwable Utility")]
public class ThrowableUtilityData : UtilityItemData
{
    [FoldoutGroup("Throwable"), EnumToggleButtons]
    [SerializeField] private ThrowableUtilityBehavior behavior = ThrowableUtilityBehavior.NoiseMaker;

    [FoldoutGroup("Throwable"), AssetsOnly, Required]
    [SerializeField] private ThrowableWorldObject throwableWorldPrefab;

    [FoldoutGroup("Throwable"), MinValue(0)]
    [SerializeField] private int throwablePoolPrewarm = 4;

    [FoldoutGroup("Throwable"), MinValue(1)]
    [SerializeField] private int maxUses = 1;

    [FoldoutGroup("Throwable/Handling"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float chargeThreshold = 0.8f;

    [FoldoutGroup("Throwable/Handling"), MinValue(0f)]
    [SerializeField] private float minThrowForce = 5f;

    [FoldoutGroup("Throwable/Handling"), MinValue(0f)]
    [SerializeField] private float maxThrowForce = 13f;

    [FoldoutGroup("Throwable/Handling"), MinValue(0f)]
    [SerializeField] private float minTravelDistance = 1.5f;

    [FoldoutGroup("Throwable/Handling"), MinValue(0f)]
    [SerializeField] private float maxTravelDistance = 10f;

    [FoldoutGroup("Throwable/Handling"), MinValue(0f), SuffixLabel("deg/s", true)]
    [SerializeField] private float throwSpinSpeed = 1080f;

    [FoldoutGroup("Throwable/Handling"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float throwAnimationDuration = 0.16f;

    [FoldoutGroup("Throwable/Impact"), MinValue(0f)]
    [SerializeField] private float impactNoise = 0.65f;

    [FoldoutGroup("Throwable/Impact")]
    [SerializeField] private NoiseType impactNoiseType = NoiseType.Common;

    [FoldoutGroup("Throwable/Impact")]
    [SerializeField] private bool impactExtremeNoise;

    [FoldoutGroup("Throwable/Impact"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float impactNoiseCooldown = 0.05f;

    [FoldoutGroup("Throwable/Impact"), LabelText("Impact SFX"), InlineProperty]
    [SerializeField] private AudioClipSet impactSfx = new();

    [FoldoutGroup("Throwable/Impact"), AssetsOnly]
    [SerializeField] private GameObject resolveEffectPrefab;

    [FoldoutGroup("Throwable/Impact"), MinValue(0)]
    [SerializeField] private int resolveEffectPoolPrewarm = 2;

    [FoldoutGroup("Throwable/Direct Damage"), ShowIf(nameof(UsesDirectDamage)), MinValue(0f)]
    [SerializeField] private float directHitDamage = 20f;

    [FoldoutGroup("Throwable/Direct Damage"), ShowIf(nameof(UsesDirectDamage)), MinValue(0)]
    [SerializeField] private int directHitPenetration;

    [FoldoutGroup("Throwable/Direct Damage"), ShowIf(nameof(UsesDirectDamage)), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float directHitStaggerDuration = 0.1f;

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesDetonation)), EnumToggleButtons]
    [SerializeField] private ThrowableDetonationMode detonationMode = ThrowableDetonationMode.OnTimer;

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesTimerDetonation)), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float detonationDelay = 3f;

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesDetonation)), MinValue(0f)]
    [SerializeField] private float detonationNoise = 1f;

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesDetonation))]
    [SerializeField] private NoiseType detonationNoiseType = NoiseType.Loud;

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesDetonation))]
    [SerializeField] private bool detonationExtremeNoise = true;

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesDetonation)), LabelText("Detonation SFX"), InlineProperty]
    [SerializeField] private AudioClipSet detonationSfx = new();

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesDetonation)), MinValue(0f)]
    [SerializeField] private float effectRadius = 3f;

    [FoldoutGroup("Throwable/Detonation"), ShowIf(nameof(UsesDetonation))]
    [SerializeField] private LayerMask effectObstacleMask;

    [FoldoutGroup("Throwable/Explosion"), ShowIf(nameof(UsesExplosion)), MinValue(0f)]
    [SerializeField] private float explosionDamage = 50f;

    [FoldoutGroup("Throwable/Explosion"), ShowIf(nameof(UsesExplosion))]
    [SerializeField] private bool applyExplosionKnockback = true;

    [FoldoutGroup("Throwable/Explosion"), ShowIf("@UsesExplosion && applyExplosionKnockback"), MinValue(0f)]
    [SerializeField] private float explosionKnockbackForce = 8f;

    [FoldoutGroup("Throwable/Flashbang"), ShowIf(nameof(UsesFlashbang)), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float flashbangDuration = 8f;

    [FoldoutGroup("Throwable/Flashbang"), ShowIf(nameof(UsesFlashbang)), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float flashbangRecoveryThreshold = 4f;

    [FoldoutGroup("Throwable/Flashbang"), ShowIf(nameof(UsesFlashbang)), MinValue(0f), SuffixLabel("deg/s", true)]
    [SerializeField] private float flashbangAimlessRotationSpeed = 240f;

    [FoldoutGroup("Throwable/Flashbang"), ShowIf(nameof(UsesFlashbang))]
    [SerializeField] private AudioClip playerRingingLoopClip;

    [FoldoutGroup("Throwable/Flashbang"), ShowIf(nameof(UsesFlashbang)), LabelText("Override Ringing Spatial Blend")]
    [SerializeField] private bool overridePlayerRingingSpatialBlend;

    [FoldoutGroup("Throwable/Flashbang"), ShowIf("@UsesFlashbang && overridePlayerRingingSpatialBlend"), Range(0f, 1f)]
    [SerializeField] private float playerRingingSpatialBlend;

    public override string UtilityTypeName => behavior switch
    {
        ThrowableUtilityBehavior.NoiseMaker => "Throwable Noise Maker",
        ThrowableUtilityBehavior.DirectDamage => "Throwable Damage",
        ThrowableUtilityBehavior.Explosion => "Throwable Explosive",
        ThrowableUtilityBehavior.Flashbang => "Throwable Flashbang",
        _ => "Throwable"
    };

    public ThrowableUtilityBehavior Behavior => behavior;
    public ThrowableWorldObject ThrowableWorldPrefab => throwableWorldPrefab;
    public int ThrowablePoolPrewarm => throwablePoolPrewarm;
    public int MaxUses => Mathf.Max(1, maxUses);
    public float ChargeThreshold => chargeThreshold;
    public float MinThrowForce => minThrowForce;
    public float MaxThrowForce => maxThrowForce;
    public float MinTravelDistance => minTravelDistance;
    public float MaxTravelDistance => Mathf.Max(minTravelDistance, maxTravelDistance);
    public float ThrowSpinSpeed => throwSpinSpeed;
    public float ThrowAnimationDuration => throwAnimationDuration;
    public float ImpactNoise => impactNoise;
    public NoiseType ImpactNoiseType => impactNoiseType;
    public bool ImpactExtremeNoise => impactExtremeNoise;
    public float ImpactNoiseCooldown => impactNoiseCooldown;
    public AudioClipSet ImpactSfx => impactSfx;
    public GameObject ResolveEffectPrefab => resolveEffectPrefab;
    public int ResolveEffectPoolPrewarm => resolveEffectPoolPrewarm;
    public float DirectHitDamage => directHitDamage;
    public int DirectHitPenetration => directHitPenetration;
    public float DirectHitStaggerDuration => directHitStaggerDuration;
    public ThrowableDetonationMode DetonationMode => detonationMode;
    public float DetonationDelay => detonationDelay;
    public float DetonationNoise => detonationNoise;
    public NoiseType DetonationNoiseType => detonationNoiseType;
    public bool DetonationExtremeNoise => detonationExtremeNoise;
    public AudioClipSet DetonationSfx => detonationSfx;
    public float EffectRadius => effectRadius;
    public LayerMask EffectObstacleMask => effectObstacleMask;
    public float ExplosionDamage => explosionDamage;
    public bool ApplyExplosionKnockback => applyExplosionKnockback && explosionKnockbackForce > 0f;
    public float ExplosionKnockbackForce => Mathf.Max(0f, explosionKnockbackForce);
    public float FlashbangDuration => flashbangDuration;
    public float FlashbangRecoveryThreshold => flashbangRecoveryThreshold;
    public float FlashbangAimlessRotationSpeed => flashbangAimlessRotationSpeed;
    public AudioClip PlayerRingingLoopClip => playerRingingLoopClip;
    public bool OverridePlayerRingingSpatialBlend => overridePlayerRingingSpatialBlend;
    public float PlayerRingingSpatialBlend => Mathf.Clamp01(playerRingingSpatialBlend);

    public bool UsesDirectDamage => behavior == ThrowableUtilityBehavior.DirectDamage;
    public bool UsesExplosion => behavior == ThrowableUtilityBehavior.Explosion;
    public bool UsesFlashbang => behavior == ThrowableUtilityBehavior.Flashbang;
    public bool UsesDetonation => UsesExplosion || UsesFlashbang;
    public bool UsesHitDetonation => UsesDetonation && (detonationMode == ThrowableDetonationMode.OnHit || detonationMode == ThrowableDetonationMode.OnHitAndTimer);
    public bool UsesTimerDetonation => UsesDetonation && (detonationMode == ThrowableDetonationMode.OnTimer || detonationMode == ThrowableDetonationMode.OnHitAndTimer);

    protected override void OnValidate()
    {
        base.OnValidate();
        throwablePoolPrewarm = Mathf.Max(0, throwablePoolPrewarm);
        maxUses = Mathf.Max(1, maxUses);
        chargeThreshold = Mathf.Max(0.01f, chargeThreshold);
        minThrowForce = Mathf.Max(0f, minThrowForce);
        maxThrowForce = Mathf.Max(minThrowForce, maxThrowForce);
        minTravelDistance = Mathf.Max(0f, minTravelDistance);
        maxTravelDistance = Mathf.Max(minTravelDistance, maxTravelDistance);
        throwSpinSpeed = Mathf.Max(0f, throwSpinSpeed);
        throwAnimationDuration = Mathf.Max(0.01f, throwAnimationDuration);
        impactNoise = Mathf.Max(0f, impactNoise);
        impactNoiseCooldown = Mathf.Max(0f, impactNoiseCooldown);
        resolveEffectPoolPrewarm = Mathf.Max(0, resolveEffectPoolPrewarm);
        impactSfx ??= new AudioClipSet();
        impactSfx.Validate();
        directHitDamage = Mathf.Max(0f, directHitDamage);
        directHitPenetration = Mathf.Max(0, directHitPenetration);
        directHitStaggerDuration = Mathf.Max(0f, directHitStaggerDuration);
        detonationDelay = Mathf.Max(0f, detonationDelay);
        detonationNoise = Mathf.Max(0f, detonationNoise);
        detonationSfx ??= new AudioClipSet();
        detonationSfx.Validate();
        effectRadius = Mathf.Max(0f, effectRadius);
        explosionDamage = Mathf.Max(0f, explosionDamage);
        explosionKnockbackForce = Mathf.Max(0f, explosionKnockbackForce);
        flashbangDuration = Mathf.Max(0.01f, flashbangDuration);
        flashbangRecoveryThreshold = Mathf.Clamp(flashbangRecoveryThreshold, 0f, flashbangDuration);
        flashbangAimlessRotationSpeed = Mathf.Max(0f, flashbangAimlessRotationSpeed);
        playerRingingSpatialBlend = Mathf.Clamp01(playerRingingSpatialBlend);
    }
}

}
