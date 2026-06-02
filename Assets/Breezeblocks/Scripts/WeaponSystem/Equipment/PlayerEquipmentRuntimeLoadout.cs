using System;
using System.Collections.Generic;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[Serializable]
public sealed class RuntimeEquipmentSlotLoadout
{
    [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.None;
    [SerializeField] private EquipmentItemData item;
    [SerializeField] private ProjectileData firearmProjectile;
    [SerializeField] private int loadedAmmo;
    [SerializeField] private int reserveAmmo;

    public EquipmentSlotType SlotType
    {
        get => slotType;
        set => slotType = value.IsHandSlot() ? value : EquipmentSlotType.None;
    }

    public EquipmentItemData Item
    {
        get => item;
        set => item = value;
    }

    public ProjectileData FirearmProjectile
    {
        get => firearmProjectile;
        set => firearmProjectile = value;
    }

    public int LoadedAmmo
    {
        get => loadedAmmo;
        set => loadedAmmo = Mathf.Max(0, value);
    }

    public int ReserveAmmo
    {
        get => reserveAmmo;
        set => reserveAmmo = Mathf.Max(0, value);
    }

    public RuntimeEquipmentSlotLoadout Clone()
    {
        return new RuntimeEquipmentSlotLoadout
        {
            SlotType = SlotType,
            Item = Item,
            FirearmProjectile = FirearmProjectile,
            LoadedAmmo = LoadedAmmo,
            ReserveAmmo = ReserveAmmo
        };
    }
}

[Serializable]
public sealed class PlayerEquipmentRuntimeLoadout
{
    [SerializeField] private EquipmentSlotType heldSlot = EquipmentSlotType.None;
    [SerializeField] private ArmorData armorItem;
    [SerializeField] private List<RuntimeEquipmentSlotLoadout> handSlots = new();

    public EquipmentSlotType HeldSlot
    {
        get => heldSlot;
        set => heldSlot = value.IsHandSlot() ? value : EquipmentSlotType.None;
    }

    public ArmorData ArmorItem
    {
        get => armorItem;
        set => armorItem = value;
    }

    public IReadOnlyList<RuntimeEquipmentSlotLoadout> HandSlots => handSlots;

    public RuntimeEquipmentSlotLoadout GetSlot(EquipmentSlotType slotType)
    {
        for (int i = 0; i < handSlots.Count; i++)
        {
            RuntimeEquipmentSlotLoadout slot = handSlots[i];
            if (slot != null && slot.SlotType == slotType)
                return slot;
        }

        return null;
    }

    public void SetSlot(EquipmentSlotType slotType, EquipmentItemData item, ProjectileData firearmProjectile, int loadedAmmo, int reserveAmmo)
    {
        if (!slotType.IsHandSlot())
            return;

        RuntimeEquipmentSlotLoadout slot = GetSlot(slotType);
        if (slot == null)
        {
            slot = new RuntimeEquipmentSlotLoadout();
            handSlots.Add(slot);
        }

        slot.SlotType = slotType;
        slot.Item = item;
        slot.FirearmProjectile = firearmProjectile;
        slot.LoadedAmmo = loadedAmmo;
        slot.ReserveAmmo = reserveAmmo;
    }

    public void Clear()
    {
        handSlots.Clear();
        armorItem = null;
        heldSlot = EquipmentSlotType.None;
    }

    public PlayerEquipmentRuntimeLoadout Clone()
    {
        PlayerEquipmentRuntimeLoadout clone = new()
        {
            HeldSlot = HeldSlot,
            ArmorItem = ArmorItem
        };

        for (int i = 0; i < handSlots.Count; i++)
        {
            RuntimeEquipmentSlotLoadout slot = handSlots[i];
            if (slot != null)
                clone.handSlots.Add(slot.Clone());
        }

        return clone;
    }
}

public static class PlayerEquipmentRuntimeSession
{
    private static PlayerEquipmentRuntimeLoadout pendingQuestLoadout;
    private static PlayerEquipmentRuntimeLoadout preparedQuestLoadout;

    public static bool HasPendingQuestLoadout => pendingQuestLoadout != null;
    public static bool HasPreparedQuestLoadout => preparedQuestLoadout != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        pendingQuestLoadout = null;
        preparedQuestLoadout = null;
    }

    public static void SetPendingQuestLoadout(PlayerEquipmentRuntimeLoadout loadout)
    {
        pendingQuestLoadout = loadout?.Clone();
        preparedQuestLoadout = loadout?.Clone();
    }

    public static bool TryConsumePendingQuestLoadout(out PlayerEquipmentRuntimeLoadout loadout)
    {
        if (pendingQuestLoadout == null)
        {
            loadout = null;
            return false;
        }

        loadout = pendingQuestLoadout.Clone();
        pendingQuestLoadout = null;
        return true;
    }

    public static PlayerEquipmentRuntimeLoadout PeekPendingQuestLoadout()
    {
        return pendingQuestLoadout?.Clone();
    }

    public static PlayerEquipmentRuntimeLoadout PeekPreparedQuestLoadout()
    {
        return preparedQuestLoadout?.Clone();
    }

    public static bool RestorePreparedQuestLoadoutForReplay()
    {
        if (preparedQuestLoadout == null)
        {
            pendingQuestLoadout = null;
            return false;
        }

        pendingQuestLoadout = preparedQuestLoadout.Clone();
        return true;
    }

    public static void ClearPendingQuestLoadout()
    {
        pendingQuestLoadout = null;
    }
}

}
