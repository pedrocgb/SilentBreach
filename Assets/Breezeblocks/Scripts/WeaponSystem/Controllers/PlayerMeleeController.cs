using System;
using System.Collections;
using Breezeblocks.Input;
using Breezeblocks.HideoutSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Player Melee Controller")]
[RequireComponent(typeof(CharacterOrbitHandsAnimator))]
public class PlayerMeleeController : MonoBehaviour
{
    private int rewiredPlayerId = 1;
    private string aimAction = "Aim";
    private string fireAction = "Fire";

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerAimCamera2D aimCamera;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerNoise playerNoise;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorStaggerController actorStaggerController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerStaminaController playerStaminaController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private CharacterOrbitHandsAnimator orbitHandsAnimator;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public MeleeWeaponData EquippedMeleeWeapon { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsBusy => busyRoutine != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAiming { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAttacking { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float AttackProgress01 { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    public event Action MeleeStateChanged;
    public event Action AttackStarted;

    // Executes the EnsureOn routine.
    public static PlayerMeleeController EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        PlayerMeleeController meleeController = actorRoot.GetComponent<PlayerMeleeController>();
        if (meleeController == null)
            meleeController = actorRoot.AddComponent<PlayerMeleeController>();

        meleeController.CacheReferences();
        meleeController.EnsureDamageSource();
        return meleeController;
    }

    private IPlayerInputReader inputReader;
    private Coroutine busyRoutine;
    private MeleeDamageSource meleeDamageSource;
    private bool inputBlocked;
    private float defaultLookRotationSpeed = -1f;
    private float perkMeleeStaminaCostMultiplier = 1f;

    // Executes the Reset routine.
    private void Reset()
    {
        CacheReferences();
        EnsureDamageSource();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        CacheReferences();
        EnsureDamageSource();
        inputReader = WeaponRuntimeUtility.EnsureInputReader(inputReader, rewiredPlayerId);
    }

    // Executes the OnEnable routine.
    private void OnEnable()
    {
        inputReader = WeaponRuntimeUtility.EnsureInputReader(inputReader, rewiredPlayerId);
        UpdateAimCameraState();
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        if (busyRoutine != null)
        {
            StopCoroutine(busyRoutine);
            busyRoutine = null;
        }

        IsAiming = false;
        IsAttacking = false;
        AttackProgress01 = 0f;
        meleeDamageSource?.SetDamageActive(false);
        UpdateAimCameraState();
    }

    // Executes the Update routine.
    private void Update()
    {
        if (inputBlocked || EquippedMeleeWeapon == null)
        {
            SetAimState(false);
            return;
        }

        inputReader = WeaponRuntimeUtility.EnsureInputReader(inputReader, rewiredPlayerId);

        if (!inputReader.IsReady)
            return;

        bool aimHeld = !IsBusy && inputReader.GetButton(aimAction);
        SetAimState(aimHeld);
        UpdateLookDirection(EquippedMeleeWeapon);

        if (IsBusy)
            return;

        if (inputReader.GetButtonDown(fireAction))
        {
            if (!CanSpendAttackStamina(EquippedMeleeWeapon))
            {
                playerStaminaController?.PlayInsufficientStaminaFeedback();
                return;
            }

            SetAimState(false);
            busyRoutine = StartCoroutine(AttackRoutine());
        }
    }

    // Executes the EquipWeapon routine.
    public void EquipWeapon(MeleeWeaponData meleeWeapon)
    {
        if (meleeWeapon == null || IsBusy)
            return;

        SetAimState(false);
        busyRoutine = StartCoroutine(EquipWeaponRoutine(meleeWeapon));
    }

    // Executes the HolsterWeapon routine.
    public void HolsterWeapon()
    {
        if (EquippedMeleeWeapon == null || IsBusy)
            return;

        SetAimState(false);
        busyRoutine = StartCoroutine(HolsterWeaponRoutine());
    }

    // Executes the SetInputBlocked routine.
    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        if (blocked)
            SetAimState(false);
    }

    // Executes the ApplyPerkModifiers routine.
    public void ApplyPerkModifiers(PlayerPerkModifierSet modifiers)
    {
        perkMeleeStaminaCostMultiplier = modifiers != null ? Mathf.Max(0f, modifiers.MeleeStaminaCostMultiplier) : 1f;
    }

    /// <summary>
    /// Applies profile-authored Rewired player id and melee action names.
    /// </summary>
    public void ApplyControls(PlayerControlsSettings settings)
    {
        if (settings == null)
            return;

        rewiredPlayerId = Mathf.Max(0, settings.RewiredPlayerId);
        aimAction = settings.AimAction;
        fireAction = settings.FireAction;
        inputReader = null;
        inputReader = WeaponRuntimeUtility.EnsureInputReader(inputReader, rewiredPlayerId);
    }

    // Executes the EquipWeaponRoutine routine.
    private IEnumerator EquipWeaponRoutine(MeleeWeaponData meleeWeapon)
    {
        if (EquippedMeleeWeapon != null)
            yield return HolsterCurrentWeaponInternal();

        if (meleeWeapon.EquipTime > 0f)
            yield return new WaitForSeconds(meleeWeapon.EquipTime);

        EquippedMeleeWeapon = meleeWeapon;
        AttackProgress01 = 0f;
        IsAttacking = false;
        RefreshDamageSource();
        EmitNoiseSpike(meleeWeapon.EquipNoise, meleeWeapon.EquipNoiseDuration, meleeWeapon.EquipNoiseType, meleeWeapon.EquipExtremeNoise);
        UpdateAimCameraState();
        NotifyMeleeStateChanged();
        busyRoutine = null;
    }

    // Executes the HolsterWeaponRoutine routine.
    private IEnumerator HolsterWeaponRoutine()
    {
        yield return HolsterCurrentWeaponInternal();
        busyRoutine = null;
    }

    // Executes the HolsterCurrentWeaponInternal routine.
    private IEnumerator HolsterCurrentWeaponInternal()
    {
        MeleeWeaponData weaponBeingHolstered = EquippedMeleeWeapon;
        if (weaponBeingHolstered == null)
            yield break;

        IsAttacking = false;
        AttackProgress01 = 0f;
        if (meleeDamageSource != null)
            meleeDamageSource.SetDamageActive(false);

        if (weaponBeingHolstered.HolsterTime > 0f)
            yield return new WaitForSeconds(weaponBeingHolstered.HolsterTime);

        EmitNoiseSpike(weaponBeingHolstered.HolsterNoise, weaponBeingHolstered.HolsterNoiseDuration, weaponBeingHolstered.HolsterNoiseType, weaponBeingHolstered.HolsterExtremeNoise);
        EquippedMeleeWeapon = null;
        RefreshDamageSource();
        UpdateAimCameraState();
        NotifyMeleeStateChanged();
    }

    // Executes the AttackRoutine routine.
    private IEnumerator AttackRoutine()
    {
        MeleeWeaponData meleeWeapon = EquippedMeleeWeapon;
        if (meleeWeapon == null)
        {
            busyRoutine = null;
            yield break;
        }

        if (!SpendAttackStamina(meleeWeapon))
        {
            playerStaminaController?.PlayInsufficientStaminaFeedback();
            busyRoutine = null;
            yield break;
        }

        RefreshDamageSource();
        meleeDamageSource?.BeginSwing();
        meleeDamageSource?.PlaySwingSfx();

        EmitNoiseSpike(meleeWeapon.AttackNoise, meleeWeapon.AttackNoiseDuration, meleeWeapon.AttackNoiseType, meleeWeapon.AttackExtremeNoise);
        IsAttacking = true;
        AttackProgress01 = 0f;
        NotifyMeleeStateChanged();
        AttackStarted?.Invoke();

        bool damageWindowActive = false;
        float duration = Mathf.Max(0.01f, meleeWeapon.AttackAnimationDuration);
        float swingDuration = Mathf.Clamp(meleeWeapon.AttackSwingDuration, 0.01f, duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
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
        NotifyMeleeStateChanged();
        busyRoutine = null;
    }

    // Executes the CacheReferences routine.
    private void CacheReferences()
    {
        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>(true);

        aimCamera = WeaponRuntimeUtility.ResolveAimCamera(aimCamera, gameObject);

        if (playerNoise == null)
            playerNoise = GetComponent<PlayerNoise>();

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        if (playerStaminaController == null)
            playerStaminaController = GetComponent<PlayerStaminaController>();

        if (orbitHandsAnimator == null)
            orbitHandsAnimator = GetComponent<CharacterOrbitHandsAnimator>();

        if (defaultLookRotationSpeed < 0f && playerVisionLight != null)
            defaultLookRotationSpeed = playerVisionLight.RotationSmoothing;
    }

    // Executes the EnsureDamageSource routine.
    private void EnsureDamageSource()
    {
        CacheReferences();
        if (orbitHandsAnimator == null || orbitHandsAnimator.HeldItemTransform == null)
            return;

        meleeDamageSource = MeleeDamageSource.EnsureOn(orbitHandsAnimator.HeldItemTransform.gameObject);
    }

    // Executes the RefreshDamageSource routine.
    private void RefreshDamageSource()
    {
        EnsureDamageSource();
        if (meleeDamageSource != null)
            meleeDamageSource.Configure(gameObject, EquippedMeleeWeapon);
    }

    // Executes the EmitNoiseSpike routine.
    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        EmitNoiseSpike(amount, duration, noiseType, false);
    }

    // Executes the EmitNoiseSpike routine.
    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType, bool isExtremeNoise)
    {
        WeaponRuntimeUtility.EmitNoise(playerNoise, amount, duration, noiseType, isExtremeNoise);
    }

    // Executes the NotifyMeleeStateChanged routine.
    private void NotifyMeleeStateChanged()
    {
        MeleeStateChanged?.Invoke();
    }

    // Executes the SetAimState routine.
    private void SetAimState(bool aiming)
    {
        if (IsAiming == aiming)
            return;

        IsAiming = aiming;
        UpdateAimCameraState();
        NotifyMeleeStateChanged();
    }

    // Executes the UpdateLookDirection routine.
    private void UpdateLookDirection(MeleeWeaponData meleeWeapon)
    {
        if (playerVisionLight == null || meleeWeapon == null)
            return;

        float lookSpeed = IsAiming ? meleeWeapon.AimRotationSpeed : Mathf.Max(0f, defaultLookRotationSpeed);
        if (actorStaggerController != null)
            lookSpeed *= actorStaggerController.TurnSpeedMultiplier;

        playerVisionLight.DriveMouseLook(lookSpeed, Time.deltaTime, IsAiming);
    }

    // Executes the UpdateAimCameraState routine.
    private void UpdateAimCameraState()
    {
        if (aimCamera == null)
            return;

        aimCamera.SetFollowTarget(transform);
        aimCamera.SetAimState(IsAiming, EquippedMeleeWeapon != null ? EquippedMeleeWeapon.AimPanDistance : 0f);
    }

    // Executes the CanSpendAttackStamina routine.
    private bool CanSpendAttackStamina(MeleeWeaponData meleeWeapon)
    {
        if (meleeWeapon == null)
            return false;

        float staminaCost = ResolveAttackStaminaCost(meleeWeapon);
        if (staminaCost <= 0f || playerStaminaController == null)
            return true;

        return playerStaminaController.HasStamina(staminaCost);
    }

    // Executes the SpendAttackStamina routine.
    private bool SpendAttackStamina(MeleeWeaponData meleeWeapon)
    {
        if (meleeWeapon == null)
            return false;

        float staminaCost = ResolveAttackStaminaCost(meleeWeapon);
        if (staminaCost <= 0f || playerStaminaController == null)
            return true;

        return playerStaminaController.TrySpendStamina(staminaCost, playFeedbackOnFailure: false);
    }

    // Executes the ResolveAttackStaminaCost routine.
    private float ResolveAttackStaminaCost(MeleeWeaponData meleeWeapon)
    {
        return meleeWeapon == null
            ? 0f
            : Mathf.Max(0f, meleeWeapon.StaminaCost * perkMeleeStaminaCostMultiplier);
    }
}
}
