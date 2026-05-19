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

    [FoldoutGroup("Armor"), MinValue(0f)]
    [SerializeField] private float weight = 1f;

    [FoldoutGroup("Armor"), Range(0f, 100f), SuffixLabel("%", true)]
    [SerializeField] private float rotationPenalty;

    public override EquipmentItemKind ItemKind => EquipmentItemKind.Armor;
    public override EquipmentSlotMask AllowedSlots => EquipmentSlotMask.Armor;
    public int ArmorClass => armorClass;
    public float ArmorValue => armorValue;
    public float Weight => weight;
    public float RotationPenalty => rotationPenalty;

    private void OnValidate()
    {
        ValidateCommonItemFields();
        armorClass = Mathf.Clamp(armorClass, 1, 5);
        armorValue = Mathf.Max(0f, armorValue);
        weight = Mathf.Max(0f, weight);
        rotationPenalty = Mathf.Clamp(rotationPenalty, 0f, 100f);
    }
}
}
