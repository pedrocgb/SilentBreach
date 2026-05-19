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
    [SerializeField] private string fireAction = "Fire";

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerNoise playerNoise;

    [FoldoutGroup("References")]
    [SerializeField] private CharacterOrbitHandsAnimator orbitHandsAnimator;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public MeleeWeaponData EquippedMeleeWeapon { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsBusy => busyRoutine != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAttacking { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float AttackProgress01 { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    public event Action MeleeStateChanged;

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

    private void OnDisable()
    {
        if (busyRoutine != null)
        {
            StopCoroutine(busyRoutine);
            busyRoutine = null;
        }

        IsAttacking = false;
        AttackProgress01 = 0f;
        meleeDamageSource?.SetDamageActive(false);
    }

    private void Update()
    {
        if (inputBlocked || EquippedMeleeWeapon == null || IsBusy)
            return;

        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        if (rewiredPlayer.GetButtonDown(fireAction))
            busyRoutine = StartCoroutine(AttackRoutine());
    }

    public void EquipWeapon(MeleeWeaponData meleeWeapon)
    {
        if (meleeWeapon == null || IsBusy)
            return;

        busyRoutine = StartCoroutine(EquipWeaponRoutine(meleeWeapon));
    }

    public void HolsterWeapon()
    {
        if (EquippedMeleeWeapon == null || IsBusy)
            return;

        busyRoutine = StartCoroutine(HolsterWeaponRoutine());
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
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
        EmitNoiseSpike(meleeWeapon.EquipNoise, meleeWeapon.EquipNoiseDuration, meleeWeapon.EquipNoiseType);
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

        EmitNoiseSpike(weaponBeingHolstered.HolsterNoise, weaponBeingHolstered.HolsterNoiseDuration, weaponBeingHolstered.HolsterNoiseType);
        EquippedMeleeWeapon = null;
        RefreshDamageSource();
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

        RefreshDamageSource();
        meleeDamageSource?.BeginSwing();

        EmitNoiseSpike(meleeWeapon.AttackNoise, meleeWeapon.AttackNoiseDuration, meleeWeapon.AttackNoiseType);
        IsAttacking = true;
        AttackProgress01 = 0f;
        NotifyMeleeStateChanged();

        bool damageWindowActive = false;
        float duration = Mathf.Max(0.01f, meleeWeapon.AttackAnimationDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (playerVisionLight != null)
                playerVisionLight.DriveMouseLook(playerVisionLight.RotationSmoothing, Time.deltaTime);

            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            AttackProgress01 = normalizedTime;

            bool shouldDealDamage =
                normalizedTime >= meleeWeapon.AttackActiveStartNormalized &&
                normalizedTime <= meleeWeapon.AttackActiveEndNormalized;

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

        if (playerNoise == null)
            playerNoise = GetComponent<PlayerNoise>();

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

    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        if (playerNoise != null)
            playerNoise.AddNoiseSpike(amount, duration, noiseType);
    }

    private void NotifyMeleeStateChanged()
    {
        MeleeStateChanged?.Invoke();
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }
}
}
