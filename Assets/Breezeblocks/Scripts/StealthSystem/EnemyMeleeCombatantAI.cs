using System;
using System.Collections;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyMovementController))]
[RequireComponent(typeof(EnemyVisionAI))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Melee Combatant AI")]
public class EnemyMeleeCombatantAI : MonoBehaviour
{
    private const float MinimumDecisionInterval = 0.02f;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyVisionAI enemyVisionAI;

    [FoldoutGroup("References")]
    [SerializeField] private CharacterOrbitHandsAnimator orbitHandsAnimator;

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

    private void Reset()
    {
        CacheReferences();
        EnsureDamageSource();
    }

    private void Awake()
    {
        CacheReferences();
        EnsureDamageSource();
        ClampSettings();
    }

    private void Start()
    {
        ApplyWeaponReadinessForState(enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Idle, isInitialState: true);
    }

    private void OnEnable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged += HandleMovementStateChanged;
    }

    private void OnDisable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged -= HandleMovementStateChanged;

        CancelActiveAttack();
        meleeDamageSource?.SetDamageActive(false);
    }

    private void OnValidate()
    {
        CacheReferences();
        EnsureDamageSource();
        ClampSettings();
    }

    private void Update()
    {
        if (enemyMovementController == null ||
            EquippedMeleeWeapon == null ||
            enemyMovementController.CurrentState != EnemyState.Detected ||
            isFlashbanged ||
            IsBusy ||
            Time.time < nextAttackDecisionTime)
        {
            return;
        }

        nextAttackDecisionTime = Time.time + attackDecisionInterval;
        if (!TryResolveTargetPoint(out Vector2 targetPoint))
        {
            enemyMovementController.ClearDetectedMovementOverride(true);
            return;
        }

        enemyMovementController.SetFacingPoint(targetPoint);
        float attackRange = Mathf.Max(0f, EquippedMeleeWeapon.AttackReachDistance);
        float distanceToTarget = Vector2.Distance(transform.position, targetPoint);
        if (distanceToTarget > attackRange)
        {
            enemyMovementController.ClearDetectedMovementOverride(true);
            return;
        }

        enemyMovementController.HoldDetectedPosition();
        attackRoutine = StartCoroutine(AttackRoutine());
    }

    public void ApplySettings(EnemyMeleeSettings settings)
    {
        if (settings == null)
            return;

        startArmed = settings.StartArmed;
        startingWeapon = settings.StartingWeapon;
        attackDecisionInterval = settings.AttackDecisionInterval;
        debugMelee = settings.DebugMelee;
        ClampSettings();

        if (!Application.isPlaying || enemyMovementController == null)
            return;

        if (startingWeapon == null)
        {
            HolsterCurrentWeapon();
            return;
        }

        if (enemyMovementController != null)
            ApplyWeaponReadinessForState(enemyMovementController.CurrentState, isInitialState: false);
    }

    private void HandleMovementStateChanged(EnemyState previousState, EnemyState newState)
    {
        if (newState != EnemyState.Detected)
            CancelActiveAttack();

        ApplyWeaponReadinessForState(newState, isInitialState: false);
    }

    private void ApplyWeaponReadinessForState(EnemyState state, bool isInitialState)
    {
        if (RequiresReadiedWeapon(state))
        {
            bool rememberAwarenessDraw = !startArmed && state != EnemyState.Alert && state != EnemyState.Detected;
            EnsureWeaponEquipped(rememberAwarenessDraw);
            return;
        }

        if (!IsCalmState(state))
            return;

        if (startArmed)
        {
            EnsureWeaponEquipped(false);
            return;
        }

        if (isInitialState || weaponEquippedForAwareness)
            HolsterCurrentWeapon();
    }

    private void EnsureWeaponEquipped(bool rememberAwarenessDraw)
    {
        if (startingWeapon == null)
            return;

        if (EquippedMeleeWeapon != startingWeapon)
        {
            EquippedMeleeWeapon = startingWeapon;
            SetBusyFor(startingWeapon.EquipTime);
            RefreshDamageSource();
            if (debugMelee)
                Debug.Log($"{name} readied {startingWeapon.name}.", this);
        }

        weaponEquippedForAwareness = rememberAwarenessDraw;
    }

    private void HolsterCurrentWeapon()
    {
        MeleeWeaponData weaponBeingHolstered = EquippedMeleeWeapon;
        if (weaponBeingHolstered == null)
        {
            weaponEquippedForAwareness = false;
            return;
        }

        EquippedMeleeWeapon = null;
        weaponEquippedForAwareness = false;
        SetBusyFor(weaponBeingHolstered.HolsterTime);
        RefreshDamageSource();

        if (debugMelee)
            Debug.Log($"{name} holstered {weaponBeingHolstered.name}.", this);
    }

    public void SetFlashbanged(bool flashbanged)
    {
        isFlashbanged = flashbanged;
        if (flashbanged)
            CancelActiveAttack();
    }

    private IEnumerator AttackRoutine()
    {
        MeleeWeaponData meleeWeapon = EquippedMeleeWeapon;
        if (meleeWeapon == null)
        {
            attackRoutine = null;
            yield break;
        }

        RefreshDamageSource();
        meleeDamageSource?.BeginSwing();
        IsAttacking = true;
        AttackProgress01 = 0f;

        bool damageWindowActive = false;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, meleeWeapon.AttackAnimationDuration);
        float swingDuration = Mathf.Clamp(meleeWeapon.AttackSwingDuration, 0.01f, duration);

        while (elapsed < duration)
        {
            if (enemyMovementController != null && TryResolveTargetPoint(out Vector2 targetPoint))
            {
                enemyMovementController.SetFacingPoint(targetPoint);
                enemyMovementController.HoldDetectedPosition();
            }

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            AttackProgress01 = normalizedTime;
            float swingProgress = Mathf.Clamp01(elapsed / swingDuration);

            bool shouldDealDamage =
                elapsed <= swingDuration &&
                swingProgress >= meleeWeapon.AttackActiveStartNormalized &&
                swingProgress <= meleeWeapon.AttackActiveEndNormalized;

            if (shouldDealDamage != damageWindowActive && meleeDamageSource != null)
            {
                if (shouldDealDamage)
                    meleeDamageSource.BeginSwing();

                meleeDamageSource.SetDamageActive(shouldDealDamage);
                damageWindowActive = shouldDealDamage;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (meleeDamageSource != null)
            meleeDamageSource.SetDamageActive(false);

        IsAttacking = false;
        AttackProgress01 = 0f;
        attackRoutine = null;

        if (enemyMovementController != null)
            enemyMovementController.ClearDetectedMovementOverride(true);
    }

    private void CancelActiveAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        IsAttacking = false;
        AttackProgress01 = 0f;
        meleeDamageSource?.SetDamageActive(false);
    }

    private bool TryResolveTargetPoint(out Vector2 targetPoint)
    {
        targetPoint = Vector2.zero;

        Transform targetTransform = enemyMovementController != null ? enemyMovementController.DetectedTarget : null;
        if (targetTransform == null && enemyVisionAI != null)
            targetTransform = enemyVisionAI.TargetTransform;

        if (targetTransform == null)
            return false;

        if (enemyVisionAI != null &&
            enemyVisionAI.TargetTransform == targetTransform &&
            enemyVisionAI.TargetVisibilityComponent != null)
        {
            targetPoint = enemyVisionAI.TargetVisibilityComponent.SamplePosition;
            return true;
        }

        targetPoint = targetTransform.position;
        return true;
    }

    private void CacheReferences()
    {
        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyVisionAI == null)
            enemyVisionAI = GetComponent<EnemyVisionAI>();

        if (orbitHandsAnimator == null)
            orbitHandsAnimator = CharacterOrbitHandsAnimator.EnsureOn(gameObject);
    }

    private void EnsureDamageSource()
    {
        CacheReferences();
        if (orbitHandsAnimator == null || orbitHandsAnimator.HeldItemTransform == null)
            return;

        meleeDamageSource = MeleeDamageSource.EnsureOn(orbitHandsAnimator.HeldItemTransform.gameObject);
    }

    private void RefreshDamageSource()
    {
        EnsureDamageSource();
        if (meleeDamageSource != null)
            meleeDamageSource.Configure(gameObject, EquippedMeleeWeapon);
    }

    private void SetBusyFor(float duration)
    {
        busyUntilTime = Mathf.Max(busyUntilTime, Time.time + Mathf.Max(0f, duration));
    }

    private void ClampSettings()
    {
        attackDecisionInterval = Mathf.Max(MinimumDecisionInterval, attackDecisionInterval);
    }

    private static bool RequiresReadiedWeapon(EnemyState state)
    {
        return state == EnemyState.Suspicious ||
               state == EnemyState.Searching ||
               state == EnemyState.Alert ||
               state == EnemyState.Detected;
    }

    private static bool IsCalmState(EnemyState state)
    {
        return state == EnemyState.Idle ||
               state == EnemyState.Patrol ||
               state == EnemyState.ReturningToStart;
    }
}
