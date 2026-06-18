using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[CreateAssetMenu(fileName = "LockpickUtilityData", menuName = "Breezeblocks/Equipment/Lockpick Utility")]
public sealed class LockpickUtilityData : UtilityItemData
{
    [FoldoutGroup("Lockpicks"), MinValue(1)]
    [SerializeField] private int maxUses = 10;

    public override string UtilityTypeName => "Lockpicks";
    public int MaxUses => Mathf.Max(1, maxUses);

    /// <summary>
    /// Normalizes lockpick uses after inspector edits.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        maxUses = Mathf.Max(1, maxUses);
    }
}

}
