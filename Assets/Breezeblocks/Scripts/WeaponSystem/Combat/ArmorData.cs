using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[CreateAssetMenu(fileName = "ArmorData", menuName = "Breezeblocks/Combat/Armor Data")]
public class ArmorData : EquipmentItemData
{
    [FoldoutGroup("Armor"), Range(1, 5)]
    [SerializeField] private int armorClass = 1;

    [FoldoutGroup("Armor"), MinValue(0f)]
    [SerializeField] private float armorValue = 100f;

    [FoldoutGroup("Armor"), Range(0f, 100f), SuffixLabel("%", true)]
    [SerializeField] private float rotationPenalty;

    [FoldoutGroup("Armor"), Range(0f, 1000f), SuffixLabel("%", true)]
    [SerializeField] private float movementNoiseModifierPercent;

    public override EquipmentItemKind ItemKind => EquipmentItemKind.Armor;
    public override EquipmentSlotMask AllowedSlots => EquipmentSlotMask.Armor;
    public int ArmorClass => armorClass;
    public float ArmorValue => armorValue;
    public float RotationPenalty => rotationPenalty;
    public float MovementNoiseModifierPercent => movementNoiseModifierPercent;

    private void OnValidate()
    {
        ValidateCommonItemFields();
        armorClass = Mathf.Clamp(armorClass, 1, 5);
        armorValue = Mathf.Max(0f, armorValue);
        rotationPenalty = Mathf.Clamp(rotationPenalty, 0f, 100f);
        movementNoiseModifierPercent = Mathf.Max(0f, movementNoiseModifierPercent);
    }
}
}
