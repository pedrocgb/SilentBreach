using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public abstract class EquipmentItemData : ScriptableObject
{
    [FoldoutGroup("Item"), LabelText("Display Name")]
    [SerializeField] private string itemDisplayName;

    [FoldoutGroup("Item"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite itemIcon;

    [FoldoutGroup("Item"), MultiLineProperty(4)]
    [SerializeField] private string itemDescription;

    [FoldoutGroup("Item"), ShowInInspector, ReadOnly]
    public abstract EquipmentItemKind ItemKind { get; }

    public virtual EquipmentSlotMask AllowedSlots => EquipmentSlotMask.None;
    public virtual Sprite HeldVisualSprite => Icon;
    public string DisplayName => string.IsNullOrWhiteSpace(itemDisplayName) ? name : itemDisplayName;
    public Sprite Icon => itemIcon;
    public string Description => itemDescription;

    public bool SupportsSlot(EquipmentSlotType slotType)
    {
        EquipmentSlotMask allowedSlots = AllowedSlots;
        return allowedSlots != EquipmentSlotMask.None && (allowedSlots & slotType.ToMask()) != 0;
    }

    protected void ValidateCommonItemFields()
    {
        itemDisplayName = itemDisplayName != null ? itemDisplayName.Trim() : string.Empty;
        itemDescription ??= string.Empty;
    }
}
}
