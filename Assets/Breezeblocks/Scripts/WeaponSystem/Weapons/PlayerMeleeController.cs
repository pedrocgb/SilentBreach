using System;
using System.Collections;
using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Player Melee Controller")]
public class PlayerMeleeController : MonoBehaviour
{
    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string aimAction = "Aim";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string fireAction = "Fire";

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerAimCamera2D aimCamera;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerNoise playerNoise;

    [FoldoutGroup("References")]
    [SerializeField] private ActorStaggerController actorStaggerController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerStaminaController playerStaminaController;

    [FoldoutGroup("References")]
    [SerializeField] private CharacterOrbitHandsAnimator orbitHandsAnimator;

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

    private Player rewiredPlayer;
    private Coroutine busyRoutine;
    private MeleeDamageSource meleeDamageSource;
    private bool inputBlocked;
    private float defaultLookRotationSpeed = -1f;

    private void Reset()
    {
        CacheReferences();
        EnsureDamageSource();
    }

    private void Awake()
    {
        CacheReferences();
        EnsureDamageSource();
        ResolveRewiredPlayer();
    }

    private void OnEnable()
    {
        ResolveRewiredPlayer();
        UpdateAimCameraState();
    }

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

    private void Update()
    {
        if (inputBlocked || EquippedMeleeWeapon == null)
        {
            SetAimState(false);
            return;
        }

        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        bool aimHeld = !IsBusy && rewiredPlayer.GetButton(aimAction);
        SetAimState(aimHeld);
        UpdateLookDirection(EquippedMeleeWeapon);

        if (IsBusy)
            return;

        if (rewiredPlayer.GetButtonDown(fireAction))
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

    public void EquipWeapon(MeleeWeaponData meleeWeapon)
    {
        if (meleeWeapon == null || IsBusy)
            return;

        SetAimState(false);
        busyRoutine = StartCoroutine(EquipWeaponRoutine(meleeWeapon));
    }

    public void HolsterWeapon()
    {
        if (EquippedMeleeWeapon == null || IsBusy)
            return;

        SetAimState(false);
        busyRoutine = StartCoroutine(HolsterWeaponRoutine());
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        if (blocked)
            SetAimState(false);
    }

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

    private IEnumerator HolsterWeaponRoutine()
    {
        yield return HolsterCurrentWeaponInternal();
        busyRoutine = null;
    }

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

    private void CacheReferences()
    {
        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>(true);

        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main.GetComponent<PlayerAimCamera2D>();

        if (aimCamera == null)
            aimCamera = FindFirstObjectByType<PlayerAimCamera2D>();

        if (playerNoise == null)
            playerNoise = GetComponent<PlayerNoise>();

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        if (playerStaminaController == null)
            playerStaminaController = GetComponent<PlayerStaminaController>();

        if (orbitHandsAnimator == null)
            orbitHandsAnimator = CharacterOrbitHandsAnimator.EnsureOn(gameObject);

        if (defaultLookRotationSpeed < 0f && playerVisionLight != null)
            defaultLookRotationSpeed = playerVisionLight.RotationSmoothing;
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

    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        EmitNoiseSpike(amount, duration, noiseType, false);
    }

    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType, bool isExtremeNoise)
    {
        if (playerNoise != null)
            playerNoise.AddNoiseSpike(amount, duration, noiseType, isExtremeNoise);
    }

    private void NotifyMeleeStateChanged()
    {
        MeleeStateChanged?.Invoke();
    }

    private void SetAimState(bool aiming)
    {
        if (IsAiming == aiming)
            return;

        IsAiming = aiming;
        UpdateAimCameraState();
        NotifyMeleeStateChanged();
    }

    private void UpdateLookDirection(MeleeWeaponData meleeWeapon)
    {
        if (playerVisionLight == null || meleeWeapon == null)
            return;

        float lookSpeed = IsAiming ? meleeWeapon.AimRotationSpeed : Mathf.Max(0f, defaultLookRotationSpeed);
        if (actorStaggerController != null)
            lookSpeed *= actorStaggerController.TurnSpeedMultiplier;

        playerVisionLight.DriveMouseLook(lookSpeed, Time.deltaTime);
    }

    private void UpdateAimCameraState()
    {
        if (aimCamera == null)
            return;

        aimCamera.SetFollowTarget(transform);
        aimCamera.SetAimState(IsAiming, EquippedMeleeWeapon != null ? EquippedMeleeWeapon.AimPanDistance : 0f);
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }

    private bool CanSpendAttackStamina(MeleeWeaponData meleeWeapon)
    {
        if (meleeWeapon == null)
            return false;

        float staminaCost = Mathf.Max(0f, meleeWeapon.StaminaCost);
        if (staminaCost <= 0f || playerStaminaController == null)
            return true;

        return playerStaminaController.HasStamina(staminaCost);
    }

    private bool SpendAttackStamina(MeleeWeaponData meleeWeapon)
    {
        if (meleeWeapon == null)
            return false;

        float staminaCost = Mathf.Max(0f, meleeWeapon.StaminaCost);
        if (staminaCost <= 0f || playerStaminaController == null)
            return true;

        return playerStaminaController.TrySpendStamina(staminaCost, playFeedbackOnFailure: false);
    }
}
}
