using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public abstract class UtilityItemData : EquipmentItemData
{
    [FoldoutGroup("Utility/Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite heldVisualSprite;

    [FoldoutGroup("Utility")]
    [FoldoutGroup("Utility/Loadout"), EnumToggleButtons]
    [SerializeField] private EquipmentSlotMask allowedSlots = EquipmentSlotMask.Belt;

    [FoldoutGroup("Utility/Handling"), MinValue(0f)]
    [SerializeField] private float equipTime = 0.2f;

    [FoldoutGroup("Utility/Handling"), MinValue(0f)]
    [SerializeField] private float holsterTime = 0.2f;

    [FoldoutGroup("Utility/Aiming"), MinValue(0f)]
    [SerializeField] private float aimPanDistance = 6f;

    [FoldoutGroup("Utility/Aiming"), MinValue(0f)]
    [SerializeField] private float aimRotationSpeed = 720f;

    [FoldoutGroup("Utility/Noise"), MinValue(0f)]
    [SerializeField] private float equipNoise;

    [FoldoutGroup("Utility/Noise"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float equipNoiseDuration = 0.2f;

    [FoldoutGroup("Utility/Noise")]
    [SerializeField] private NoiseType equipNoiseType = NoiseType.Silent;

    [FoldoutGroup("Utility/Noise"), MinValue(0f)]
    [SerializeField] private float holsterNoise;

    [FoldoutGroup("Utility/Noise"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float holsterNoiseDuration = 0.2f;

    [FoldoutGroup("Utility/Noise")]
    [SerializeField] private NoiseType holsterNoiseType = NoiseType.Silent;

    public override EquipmentItemKind ItemKind => EquipmentItemKind.Utility;
    public override EquipmentSlotMask AllowedSlots => allowedSlots & EquipmentSlotMask.HandSlots;
    public override Sprite HeldVisualSprite => heldVisualSprite != null ? heldVisualSprite : Icon;

    public float EquipTime => equipTime;
    public float HolsterTime => holsterTime;
    public float AimPanDistance => aimPanDistance;
    public float AimRotationSpeed => aimRotationSpeed;
    public float EquipNoise => equipNoise;
    public float EquipNoiseDuration => equipNoiseDuration;
    public NoiseType EquipNoiseType => equipNoiseType;
    public float HolsterNoise => holsterNoise;
    public float HolsterNoiseDuration => holsterNoiseDuration;
    public NoiseType HolsterNoiseType => holsterNoiseType;
    public virtual string UtilityTypeName => "Utility";

    protected virtual void OnValidate()
    {
        ValidateCommonItemFields();
        allowedSlots &= EquipmentSlotMask.HandSlots;
        if (allowedSlots == EquipmentSlotMask.None)
            allowedSlots = EquipmentSlotMask.Belt;

        equipTime = Mathf.Max(0f, equipTime);
        holsterTime = Mathf.Max(0f, holsterTime);
        aimPanDistance = Mathf.Max(0f, aimPanDistance);
        aimRotationSpeed = Mathf.Max(0f, aimRotationSpeed);
        equipNoise = Mathf.Max(0f, equipNoise);
        equipNoiseDuration = Mathf.Max(0f, equipNoiseDuration);
        holsterNoise = Mathf.Max(0f, holsterNoise);
        holsterNoiseDuration = Mathf.Max(0f, holsterNoiseDuration);
    }
}
}
