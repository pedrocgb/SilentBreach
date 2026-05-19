using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[CreateAssetMenu(fileName = "ProjectileData", menuName = "Breezeblocks/Weapons/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    [FoldoutGroup("Projectile"), MinValue(0)]
    [SerializeField] private int penetration;

    [FoldoutGroup("Projectile"), MinValue(0)]
    [SerializeField] private int damage = 10;

    [FoldoutGroup("Projectile"), MinValue(0f)]
    [SerializeField] private float range = 10f;

    [FoldoutGroup("Projectile"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float staggerDuration = 0.12f;

    public int Penetration => penetration;
    public int Damage => damage;
    public float Range => range;
    public float StaggerDuration => staggerDuration;

    private void OnValidate()
    {
        penetration = Mathf.Max(0, penetration);
        damage = Mathf.Max(0, damage);
        range = Mathf.Max(0f, range);
        staggerDuration = Mathf.Max(0f, staggerDuration);
    }
}
}
