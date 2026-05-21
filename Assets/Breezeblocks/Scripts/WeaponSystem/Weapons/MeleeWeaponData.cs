using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public enum MeleeGripType
{
    OneHanded,
    TwoHanded
}

[Serializable]
public sealed class MeleeHitSfxLayerEntry
{
    [FoldoutGroup("SFX"), LabelText("Layers")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [FoldoutGroup("SFX"), LabelText("Hit SFX"), InlineProperty]
    [SerializeField] private AudioClipSet hitSfx = new();

    public LayerMask TargetLayers => targetLayers;
    public AudioClipSet HitSfx => hitSfx;

    public bool MatchesLayer(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    public void Validate()
    {
        hitSfx ??= new AudioClipSet();
        hitSfx.Validate();
    }
}

[CreateAssetMenu(fileName = "MeleeWeaponData", menuName = "Breezeblocks/Weapons/Melee Weapon Data")]
public class MeleeWeaponData : EquipmentItemData
{
    [FoldoutGroup("Melee")]
    [FoldoutGroup("Melee/Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite heldVisualSprite;

    [FoldoutGroup("Melee/Loadout"), EnumToggleButtons]
    [SerializeField] private EquipmentSlotMask allowedSlots = EquipmentSlotMask.Primary;

    [FoldoutGroup("Melee/Handling"), EnumToggleButtons]
    [SerializeField] private MeleeGripType gripType = MeleeGripType.OneHanded;

    [FoldoutGroup("Melee/Handling"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float equipTime = 0.2f;

    [FoldoutGroup("Melee/Handling"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float holsterTime = 0.2f;

    [FoldoutGroup("Melee/Aiming"), MinValue(0f)]
    [SerializeField] private float aimPanDistance = 3.5f;

    [FoldoutGroup("Melee/Aiming"), MinValue(0f)]
    [SerializeField] private float aimRotationSpeed = 720f;

    [FoldoutGroup("Melee/Handling"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float attackAnimationDuration = 0.75f;

    [FoldoutGroup("Melee/Handling"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float attackSwingDuration = 0.18f;

    [FoldoutGroup("Melee/Handling"), Range(0f, 1f)]
    [SerializeField] private float attackActiveStartNormalized = 0.15f;

    [FoldoutGroup("Melee/Handling"), Range(0f, 1f)]
    [SerializeField] private float attackActiveEndNormalized = 0.55f;

    [FoldoutGroup("Melee/Damage"), MinValue(0f)]
    [SerializeField] private float damage = 20f;

    [FoldoutGroup("Melee/Damage")]
    [SerializeField] private bool isLethal = true;

    [FoldoutGroup("Melee/Damage"), MinValue(0)]
    [SerializeField] private int armorPenetration;

    [FoldoutGroup("Melee/Damage"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float staggerDuration = 0.15f;

    [FoldoutGroup("Melee/Damage"), MinValue(0f)]
    [SerializeField] private float attackReachDistance = 1.15f;

    [FoldoutGroup("Melee/Impact")]
    [SerializeField] private bool appliesPushForce;

    [FoldoutGroup("Melee/Impact"), ShowIf(nameof(appliesPushForce)), MinValue(0f)]
    [SerializeField] private float pushForce = 5f;

    [FoldoutGroup("Melee/SFX"), Title("Swing SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet swingSfx = new();

    [FoldoutGroup("Melee/SFX"), Title("Default Hit SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet defaultHitSfx = new();

    [FoldoutGroup("Melee/SFX")]
    [LabelText("Hit SFX By Layer")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<MeleeHitSfxLayerEntry> hitSfxByLayer = new();

    [FoldoutGroup("Melee/Hitbox")]
    [SerializeField] private Vector2 hitboxSize = new(0.6f, 0.2f);

    [FoldoutGroup("Melee/Hitbox")]
    [SerializeField] private Vector2 hitboxOffset = new(0.3f, 0f);

    [FoldoutGroup("Melee/Noise"), MinValue(0f)]
    [SerializeField] private float attackNoise = 0.65f;

    [FoldoutGroup("Melee/Noise"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float attackNoiseDuration = 0.15f;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private NoiseType attackNoiseType = NoiseType.Common;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private bool attackExtremeNoise;

    [FoldoutGroup("Melee/Noise"), MinValue(0f)]
    [SerializeField] private float equipNoise = 0.2f;

    [FoldoutGroup("Melee/Noise"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float equipNoiseDuration = 0.2f;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private NoiseType equipNoiseType = NoiseType.Common;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private bool equipExtremeNoise;

    [FoldoutGroup("Melee/Noise"), MinValue(0f)]
    [SerializeField] private float holsterNoise = 0.2f;

    [FoldoutGroup("Melee/Noise"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float holsterNoiseDuration = 0.2f;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private NoiseType holsterNoiseType = NoiseType.Common;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private bool holsterExtremeNoise;

    public override EquipmentItemKind ItemKind => EquipmentItemKind.Melee;
    public override EquipmentSlotMask AllowedSlots => allowedSlots & EquipmentSlotMask.HandSlots;
    public override Sprite HeldVisualSprite => heldVisualSprite != null ? heldVisualSprite : Icon;

    public MeleeGripType GripType => gripType;
    public float EquipTime => equipTime;
    public float HolsterTime => holsterTime;
    public float AimPanDistance => aimPanDistance;
    public float AimRotationSpeed => aimRotationSpeed;
    public float AttackAnimationDuration => attackAnimationDuration;
    public float AttackSwingDuration => Mathf.Clamp(attackSwingDuration, 0.01f, attackAnimationDuration);
    public float AttackActiveStartNormalized => attackActiveStartNormalized;
    public float AttackActiveEndNormalized => attackActiveEndNormalized;
    public float Damage => damage;
    public bool IsLethal => isLethal;
    public int ArmorPenetration => armorPenetration;
    public float StaggerDuration => staggerDuration;
    public float AttackReachDistance => attackReachDistance;
    public bool AppliesPushForce => appliesPushForce;
    public float PushForce => pushForce;
    public AudioClipSet SwingSfx => swingSfx;
    public AudioClipSet DefaultHitSfx => defaultHitSfx;
    public IReadOnlyList<MeleeHitSfxLayerEntry> HitSfxByLayer => hitSfxByLayer;
    public Vector2 HitboxSize => hitboxSize;
    public Vector2 HitboxOffset => hitboxOffset;
    public float AttackNoise => attackNoise;
    public float AttackNoiseDuration => attackNoiseDuration;
    public NoiseType AttackNoiseType => attackNoiseType;
    public bool AttackExtremeNoise => attackExtremeNoise;
    public float EquipNoise => equipNoise;
    public float EquipNoiseDuration => equipNoiseDuration;
    public NoiseType EquipNoiseType => equipNoiseType;
    public bool EquipExtremeNoise => equipExtremeNoise;
    public float HolsterNoise => holsterNoise;
    public float HolsterNoiseDuration => holsterNoiseDuration;
    public NoiseType HolsterNoiseType => holsterNoiseType;
    public bool HolsterExtremeNoise => holsterExtremeNoise;

    public AudioClipSet ResolveHitSfxForLayer(int layer)
    {
        if (hitSfxByLayer != null)
        {
            for (int i = 0; i < hitSfxByLayer.Count; i++)
            {
                MeleeHitSfxLayerEntry entry = hitSfxByLayer[i];
                if (entry == null || !entry.MatchesLayer(layer) || entry.HitSfx == null || !entry.HitSfx.HasAnyClip)
                    continue;

                return entry.HitSfx;
            }
        }

        return defaultHitSfx != null && defaultHitSfx.HasAnyClip ? defaultHitSfx : null;
    }

    private void OnValidate()
    {
        ValidateCommonItemFields();
        allowedSlots &= EquipmentSlotMask.HandSlots;
        if (allowedSlots == EquipmentSlotMask.None)
            allowedSlots = EquipmentSlotMask.Primary;

        equipTime = Mathf.Max(0f, equipTime);
        holsterTime = Mathf.Max(0f, holsterTime);
        aimPanDistance = Mathf.Max(0f, aimPanDistance);
        aimRotationSpeed = Mathf.Max(0f, aimRotationSpeed);
        attackAnimationDuration = Mathf.Max(0.01f, attackAnimationDuration);
        attackSwingDuration = Mathf.Clamp(attackSwingDuration, 0.01f, attackAnimationDuration);
        attackActiveStartNormalized = Mathf.Clamp01(attackActiveStartNormalized);
        attackActiveEndNormalized = Mathf.Clamp(attackActiveEndNormalized, attackActiveStartNormalized, 1f);
        damage = Mathf.Max(0f, damage);
        armorPenetration = Mathf.Max(0, armorPenetration);
        staggerDuration = Mathf.Max(0f, staggerDuration);
        attackReachDistance = Mathf.Max(0f, attackReachDistance);
        pushForce = Mathf.Max(0f, pushForce);
        swingSfx ??= new AudioClipSet();
        swingSfx.Validate();
        defaultHitSfx ??= new AudioClipSet();
        defaultHitSfx.Validate();
        hitSfxByLayer ??= new List<MeleeHitSfxLayerEntry>();
        for (int i = 0; i < hitSfxByLayer.Count; i++)
            hitSfxByLayer[i]?.Validate();
        hitboxSize.x = Mathf.Max(0.01f, hitboxSize.x);
        hitboxSize.y = Mathf.Max(0.01f, hitboxSize.y);
        attackNoise = Mathf.Max(0f, attackNoise);
        attackNoiseDuration = Mathf.Max(0f, attackNoiseDuration);
        equipNoise = Mathf.Max(0f, equipNoise);
        equipNoiseDuration = Mathf.Max(0f, equipNoiseDuration);
        holsterNoise = Mathf.Max(0f, holsterNoise);
        holsterNoiseDuration = Mathf.Max(0f, holsterNoiseDuration);
    }
}
}
