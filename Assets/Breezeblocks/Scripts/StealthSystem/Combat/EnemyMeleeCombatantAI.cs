using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[RequireComponent(typeof(EnemyVisionAI))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Melee Combatant AI")]
public partial class EnemyMeleeCombatantAI : MonoBehaviour
{
    private const float MinimumDecisionInterval = 0.02f;

    private EnemyMovementController enemyMovementController;
    private EnemyVisionAI enemyVisionAI;
    private CharacterOrbitHandsAnimator orbitHandsAnimator;

    private bool startArmed = true;
    private MeleeWeaponData startingWeapon;
    private float attackDecisionInterval = 0.05f;
    private bool debugMelee;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public MeleeWeaponData EquippedMeleeWeapon { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAttacking { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float AttackProgress01 { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsBusy => attackRoutine != null || Time.time < busyUntilTime;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsFlashbanged => isFlashbanged;

    private bool weaponEquippedForAwareness;
    private float nextAttackDecisionTime;
    private float busyUntilTime = float.NegativeInfinity;
    private Coroutine attackRoutine;
    private MeleeDamageSource meleeDamageSource;
    private bool isFlashbanged;
}
