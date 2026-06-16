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

    [FoldoutGroup("Item"), MinValue(0)]
    [SerializeField] private int shopPrice = 100;

    [FoldoutGroup("Item"), ShowInInspector, ReadOnly]
    public abstract EquipmentItemKind ItemKind { get; }

    public virtual EquipmentSlotMask AllowedSlots => ItemKind == EquipmentItemKind.Armor
        ? EquipmentSlotMask.Armor
        : EquipmentSlotMask.HandSlots;
    public virtual Sprite HeldVisualSprite => Icon;
    public string DisplayName => string.IsNullOrWhiteSpace(itemDisplayName) ? name : itemDisplayName;
    public Sprite Icon => itemIcon;
    public string Description => itemDescription;
    public int ShopPrice => Mathf.Max(0, shopPrice);

    /// <summary>
    /// Returns whether this item can be equipped in the requested slot.
    /// </summary>
    public bool SupportsSlot(EquipmentSlotType slotType)
    {
        EquipmentSlotMask allowedSlots = AllowedSlots;
        return allowedSlots != EquipmentSlotMask.None && (allowedSlots & slotType.ToMask()) != 0;
    }

    /// <summary>
    /// Normalizes shared equipment authoring values.
    /// </summary>
    protected void ValidateCommonItemFields()
    {
        itemDisplayName = itemDisplayName != null ? itemDisplayName.Trim() : string.Empty;
        itemDescription ??= string.Empty;
        shopPrice = Mathf.Max(0, shopPrice);
    }
}
}
