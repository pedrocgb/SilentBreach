using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Breezeblocks/Actor Profiles/Enemy Stats Profile", fileName = "Enemy Stats Profile")]
public class EnemyStatsProfile : ScriptableObject
{
    [FoldoutGroup("Core"), InlineProperty, HideLabel]
    public ActorHealthSettings Health = new();

    [FoldoutGroup("Core"), InlineProperty, HideLabel]
    public ActorStaggerSettings Stagger = new();

    [FoldoutGroup("Movement"), InlineProperty, HideLabel]
    public EnemyMovementSettings Movement = new();

    [FoldoutGroup("Vision"), InlineProperty, HideLabel]
    public EnemyVisionSettings Vision = new();

    [FoldoutGroup("Hearing"), InlineProperty, HideLabel]
    public EnemyHearingSettings Hearing = new();

    [FoldoutGroup("Combat"), InlineProperty, HideLabel]
    public EnemyCombatSettings Combat = new();

    [FoldoutGroup("Melee"), InlineProperty, HideLabel]
    public EnemyMeleeSettings Melee = new();
}
