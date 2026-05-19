using System;

namespace Breezeblocks.WeaponSystem
{

public enum EquipmentSlotType
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Belt = 3,
    Armor = 4
}

[Flags]
public enum EquipmentSlotMask
{
    None = 0,
    Primary = 1 << 0,
    Secondary = 1 << 1,
    Belt = 1 << 2,
    Armor = 1 << 3,
    HandSlots = Primary | Secondary | Belt,
    All = HandSlots | Armor
}

public enum EquipmentItemKind
{
    Firearm,
    Melee,
    Utility,
    Armor
}

public static class EquipmentSlotUtility
{
    public static EquipmentSlotMask ToMask(this EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Primary => EquipmentSlotMask.Primary,
            EquipmentSlotType.Secondary => EquipmentSlotMask.Secondary,
            EquipmentSlotType.Belt => EquipmentSlotMask.Belt,
            EquipmentSlotType.Armor => EquipmentSlotMask.Armor,
            _ => EquipmentSlotMask.None
        };
    }

    public static bool IsHandSlot(this EquipmentSlotType slotType)
    {
        return slotType == EquipmentSlotType.Primary ||
               slotType == EquipmentSlotType.Secondary ||
               slotType == EquipmentSlotType.Belt;
    }
}
}
