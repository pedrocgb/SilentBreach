using System.Collections;
using Breezeblocks.Missions;
using Breezeblocks.WeaponSystem;
using UnityEngine;

public partial class EnemyMeleeCombatantAI
{
    /// <summary>
    /// Caches same-object references when the component is first added.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        EnsureDamageSource();
    }

    /// <summary>
    /// Resolves runtime references and validates melee settings.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        EnsureDamageSource();
        ClampSettings();
    }

    /// <summary>
    /// Applies the initial weapon readiness that matches the current awareness state.
    /// </summary>
    private void Start()
    {
        ApplyWeaponReadinessForState(enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Idle, isInitialState: true);
    }

    /// <summary>
    /// Subscribes to movement awareness-state changes.
    /// </summary>
    private void OnEnable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged += HandleMovementStateChanged;
    }

    /// <summary>
    /// Unsubscribes from movement awareness-state changes and clears active attacks.
    /// </summary>
    private void OnDisable()
    {
        if (enemyMovementController != null)
            enemyMovementController.StateChanged -= HandleMovementStateChanged;

        CancelActiveAttack();
        meleeDamageSource?.SetDamageActive(false);
    }

    /// <summary>
    /// Keeps inspector-time references and settings valid.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
        EnsureDamageSource();
        ClampSettings();
    }

    /// <summary>
    /// Evaluates whether the melee combatant should begin an attack this frame.
    /// </summary>
    private void Update()
    {
        if (GameplayMissionController.EnemyRuntimeBlockedAtMissionStart)
            return;

        if (enemyMovementController == null ||
            EquippedMeleeWeapon == null ||
            !EnemyAiStateUtility.IsCombatAwarenessState(enemyMovementController.CurrentState) ||
            (enemyMovementController.CurrentState == EnemyState.Alert && !HasClearVisualOnTarget()) ||
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

    /// <summary>
    /// Applies serialized melee settings from the enemy definition.
    /// </summary>
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

        ApplyWeaponReadinessForState(enemyMovementController.CurrentState, isInitialState: false);
    }

    /// <summary>
    /// Reacts to awareness-state changes from the movement controller.
    /// </summary>
    private void HandleMovementStateChanged(EnemyState previousState, EnemyState newState)
    {
        if (!EnemyAiStateUtility.IsCombatAwarenessState(newState) ||
            (newState == EnemyState.Alert && !HasClearVisualOnTarget()))
        {
            CancelActiveAttack();
        }

        ApplyWeaponReadinessForState(newState, isInitialState: false);
    }

    /// <summary>
    /// Keeps the melee weapon drawn or holstered according to the current awareness state.
    /// </summary>
    private void ApplyWeaponReadinessForState(EnemyState state, bool isInitialState)
    {
        if (EnemyAiStateUtility.RequiresReadiedWeapon(state))
        {
            bool rememberAwarenessDraw = !startArmed && state != EnemyState.Alert && state != EnemyState.Detected;
            EnsureWeaponEquipped(rememberAwarenessDraw);
            return;
        }

        if (!EnemyAiStateUtility.IsCalmState(state))
            return;

        if (startArmed)
        {
            EnsureWeaponEquipped(false);
            return;
        }

        if (isInitialState || weaponEquippedForAwareness)
            HolsterCurrentWeapon();
    }

    /// <summary>
    /// Ensures the configured melee weapon is currently equipped.
    /// </summary>
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

    /// <summary>
    /// Holsters the current melee weapon and refreshes the damage source.
    /// </summary>
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

    /// <summary>
    /// Applies or clears the flashbang state for the melee combatant.
    /// </summary>
    public void SetFlashbanged(bool flashbanged)
    {
        isFlashbanged = flashbanged;
        if (flashbanged)
            CancelActiveAttack();
    }

    /// <summary>
    /// Runs the attack animation timing, facing updates, and damage window toggles.
    /// </summary>
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
        meleeDamageSource?.PlaySwingSfx();
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

    /// <summary>
    /// Stops the current attack routine and clears any active damage window.
    /// </summary>
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

    /// <summary>
    /// Resolves the current target point from movement detection or vision data.
    /// </summary>
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

    /// <summary>
    /// Resolves same-object references needed by melee combat.
    /// </summary>
    private void CacheReferences()
    {
        enemyMovementController ??= GetComponent<EnemyMovementController>();
        enemyVisionAI ??= GetComponent<EnemyVisionAI>();
        orbitHandsAnimator ??= CharacterOrbitHandsAnimator.EnsureOn(gameObject);
    }

    /// <summary>
    /// Ensures a melee damage source exists on the held item transform.
    /// </summary>
    private void EnsureDamageSource()
    {
        CacheReferences();
        if (orbitHandsAnimator == null || orbitHandsAnimator.HeldItemTransform == null)
            return;

        meleeDamageSource = MeleeDamageSource.EnsureOn(orbitHandsAnimator.HeldItemTransform.gameObject);
    }

    /// <summary>
    /// Reconfigures the melee damage source using the equipped weapon.
    /// </summary>
    private void RefreshDamageSource()
    {
        EnsureDamageSource();
        if (meleeDamageSource != null)
            meleeDamageSource.Configure(gameObject, EquippedMeleeWeapon);
    }

    /// <summary>
    /// Extends the busy timer by the supplied duration.
    /// </summary>
    private void SetBusyFor(float duration)
    {
        busyUntilTime = Mathf.Max(busyUntilTime, Time.time + Mathf.Max(0f, duration));
    }

    /// <summary>
    /// Clamps melee settings to safe runtime ranges.
    /// </summary>
    private void ClampSettings()
    {
        attackDecisionInterval = Mathf.Max(MinimumDecisionInterval, attackDecisionInterval);
    }

    /// <summary>
    /// Returns whether the enemy has a clear visible line to its target.
    /// </summary>
    private bool HasClearVisualOnTarget()
    {
        return enemyVisionAI != null && enemyVisionAI.CanCurrentlyDetectTarget && enemyVisionAI.HasLineOfSight;
    }
}
