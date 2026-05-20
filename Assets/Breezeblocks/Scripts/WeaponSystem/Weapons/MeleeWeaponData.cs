using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public enum MeleeGripType
{
    OneHanded,
    TwoHanded
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

    [FoldoutGroup("Melee/Noise"), MinValue(0f)]
    [SerializeField] private float equipNoise = 0.2f;

    [FoldoutGroup("Melee/Noise"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float equipNoiseDuration = 0.2f;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private NoiseType equipNoiseType = NoiseType.Common;

    [FoldoutGroup("Melee/Noise"), MinValue(0f)]
    [SerializeField] private float holsterNoise = 0.2f;

    [FoldoutGroup("Melee/Noise"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float holsterNoiseDuration = 0.2f;

    [FoldoutGroup("Melee/Noise")]
    [SerializeField] private NoiseType holsterNoiseType = NoiseType.Common;

    public override EquipmentItemKind ItemKind => EquipmentItemKind.Melee;
    public override EquipmentSlotMask AllowedSlots => allowedSlots & EquipmentSlotMask.HandSlots;
    public override Sprite HeldVisualSprite => heldVisualSprite != null ? heldVisualSprite : Icon;

    public MeleeGripType GripType => gripType;
    public float EquipTime => equipTime;
    public float HolsterTime => holsterTime;
    public float AttackAnimationDuration => attackAnimationDuration;
    public float AttackSwingDuration => Mathf.Clamp(attackSwingDuration, 0.01f, attackAnimationDuration);
    public float AttackActiveStartNormalized => attackActiveStartNormalized;
    public float AttackActiveEndNormalized => attackActiveEndNormalized;
    public float Damage => damage;
    public bool IsLethal => isLethal;
    public int ArmorPenetration => armorPenetration;
    public float StaggerDuration => staggerDuration;
    public float AttackReachDistance => attackReachDistance;
    public Vector2 HitboxSize => hitboxSize;
    public Vector2 HitboxOffset => hitboxOffset;
    public float AttackNoise => attackNoise;
    public float AttackNoiseDuration => attackNoiseDuration;
    public NoiseType AttackNoiseType => attackNoiseType;
    public float EquipNoise => equipNoise;
    public float EquipNoiseDuration => equipNoiseDuration;
    public NoiseType EquipNoiseType => equipNoiseType;
    public float HolsterNoise => holsterNoise;
    public float HolsterNoiseDuration => holsterNoiseDuration;
    public NoiseType HolsterNoiseType => holsterNoiseType;

    private void OnValidate()
    {
        ValidateCommonItemFields();
        allowedSlots &= EquipmentSlotMask.HandSlots;
        if (allowedSlots == EquipmentSlotMask.None)
            allowedSlots = EquipmentSlotMask.Primary;

        equipTime = Mathf.Max(0f, equipTime);
        holsterTime = Mathf.Max(0f, holsterTime);
        attackAnimationDuration = Mathf.Max(0.01f, attackAnimationDuration);
        attackSwingDuration = Mathf.Clamp(attackSwingDuration, 0.01f, attackAnimationDuration);
        attackActiveStartNormalized = Mathf.Clamp01(attackActiveStartNormalized);
        attackActiveEndNormalized = Mathf.Clamp(attackActiveEndNormalized, attackActiveStartNormalized, 1f);
        damage = Mathf.Max(0f, damage);
        armorPenetration = Mathf.Max(0, armorPenetration);
        staggerDuration = Mathf.Max(0f, staggerDuration);
        attackReachDistance = Mathf.Max(0f, attackReachDistance);
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
