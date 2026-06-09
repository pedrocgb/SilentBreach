using System;
using System.Collections;
using Breezeblocks.Input;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Equipment/Player Utility Controller")]
public class PlayerUtilityController : MonoBehaviour
{
    private const float MinimumFlashlightDirectionSqr = 0.0001f;
    private const float MinimumThrowDirectionSqr = 0.0001f;

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string aimAction = "Aim";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string primaryAction = "Fire";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string cancelThrowableAction = "Cancel Throw";

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerAimCamera2D aimCamera;

    [FoldoutGroup("References")]
    [SerializeField] private Transform sfxOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private Transform throwableSpawnOrigin;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorStaggerController actorStaggerController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerNoise playerNoise;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerEquipmentController playerEquipmentController;

    [FoldoutGroup("Flashlight")]
    [Tooltip("Dedicated Light2D used by the flashlight utility. Assign this explicitly instead of the player vision light.")]
    [SerializeField] private Light2D flashlightLight;

    [FoldoutGroup("Pooling")]
    [SerializeField] private GlobalObjectPooler globalObjectPooler;

    [FoldoutGroup("Audio")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public UtilityItemData EquippedUtility { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public ThrowableUtilityData EquippedThrowable => EquippedUtility as ThrowableUtilityData;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAiming { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsBusy => busyRoutine != null || isChargingThrowable || ThrowableThrowProgress01 > 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsFlashlightOn { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsChargingThrowable => isChargingThrowable;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float ThrowableChargeProgress01 { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float ThrowableThrowProgress01 { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    public bool HasActiveFlashlight => IsFlashlightOn && flashlightLight != null && flashlightLight.enabled && flashlightLight.gameObject.activeInHierarchy;
    public Vector2 FlashlightWorldPosition => flashlightLight != null ? (Vector2)flashlightLight.transform.position : (Vector2)transform.position;
    public Vector2 FlashlightFacingDirection => flashlightLight != null ? (Vector2)flashlightLight.transform.up : playerVisionLight != null ? playerVisionLight.FacingDirection : (Vector2)transform.up;
    public float FlashlightOuterRadius => flashlightLight != null ? flashlightLight.pointLightOuterRadius : 0f;
    public float FlashlightOuterAngle => flashlightLight != null ? flashlightLight.pointLightOuterAngle : 0f;

    public event Action UtilityStateChanged;
    public event Action UtilityActivated;

    private IPlayerInputReader inputReader;
    private Coroutine busyRoutine;
    private bool inputBlocked;
    private bool isChargingThrowable;
    private float throwableChargeStartedAt;

    // Executes the Reset routine.
    private void Reset()
    {
        playerVisionLight = GetComponentInChildren<PlayerVisionLight>();
        if (Camera.main != null)
            aimCamera = Camera.main.GetComponent<PlayerAimCamera2D>();

        sfxOrigin = transform;
        throwableSpawnOrigin = playerVisionLight != null ? playerVisionLight.transform : transform;
        actorStaggerController = GetComponent<ActorStaggerController>();
        playerNoise = GetComponent<PlayerNoise>();
        playerEquipmentController = GetComponent<PlayerEquipmentController>();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>();

        aimCamera = WeaponRuntimeUtility.ResolveAimCamera(aimCamera, gameObject);

        if (sfxOrigin == null)
            sfxOrigin = transform;

        if (throwableSpawnOrigin == null)
            throwableSpawnOrigin = playerVisionLight != null ? playerVisionLight.transform : transform;

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        if (playerNoise == null)
            playerNoise = GetComponent<PlayerNoise>();

        if (playerEquipmentController == null)
            playerEquipmentController = GetComponent<PlayerEquipmentController>();

        globalObjectPooler = WeaponRuntimeUtility.ResolveGlobalObjectPooler(globalObjectPooler);
        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);

        SetFlashlightEnabled(false, playSfx: false);
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
        IsAiming = false;
        ResetThrowableInputState();
        UpdateAimCameraState();
    }

    // Executes the Update routine.
    private void Update()
    {
        if (inputBlocked)
        {
            if (IsAiming || isChargingThrowable)
            {
                IsAiming = false;
                CancelThrowableCharge();
                UpdateAimCameraState();
                NotifyUtilityStateChanged();
            }

            return;
        }

        if (EquippedUtility == null)
        {
            bool unarmedAimActive = playerEquipmentController != null && playerEquipmentController.IsUnarmedAiming;
            if (IsAiming && !unarmedAimActive)
            {
                IsAiming = false;
                UpdateAimCameraState();
            }

            MaintainMouseLookWhenNoUtilityIsHeld();

            return;
        }

        inputReader = WeaponRuntimeUtility.EnsureInputReader(inputReader, rewiredPlayerId);

        if (!inputReader.IsReady)
            return;

        bool aimHeld = busyRoutine == null && inputReader.GetButton(aimAction);
        if (aimHeld != IsAiming)
        {
            IsAiming = aimHeld;
            UpdateAimCameraState();
        }

        if (playerVisionLight != null)
        {
            float lookSpeed = IsAiming && EquippedUtility != null
                ? EquippedUtility.AimRotationSpeed
                : playerVisionLight.RotationSmoothing;
            if (actorStaggerController != null)
                lookSpeed *= actorStaggerController.TurnSpeedMultiplier;

            playerVisionLight.DriveMouseLook(lookSpeed, Time.deltaTime);
        }

        if (EquippedUtility is ThrowableUtilityData throwableData)
        {
            UpdateThrowableInput(throwableData);
            return;
        }

        if (busyRoutine == null && inputReader.GetButtonDown(primaryAction))
            HandlePrimaryAction();
    }

    // Executes the EquipUtility routine.
    public void EquipUtility(UtilityItemData utilityItem)
    {
        if (utilityItem == null || IsBusy)
            return;

        busyRoutine = StartCoroutine(EquipUtilityRoutine(utilityItem));
    }

    // Executes the HolsterCurrentUtility routine.
    public void HolsterCurrentUtility()
    {
        if (EquippedUtility == null || IsBusy)
            return;

        busyRoutine = StartCoroutine(HolsterUtilityRoutine());
    }

    // Executes the SetInputBlocked routine.
    public void SetInputBlocked(bool blocked)
    {
        if (inputBlocked == blocked)
            return;

        inputBlocked = blocked;
        if (!blocked)
            return;

        bool changedState = IsAiming || isChargingThrowable;
        IsAiming = false;
        CancelThrowableCharge();
        UpdateAimCameraState();
        if (changedState)
            NotifyUtilityStateChanged();
    }

    // Executes the TryGetActiveFlashlightCone routine.
    public bool TryGetActiveFlashlightCone(out Vector2 source, out Vector2 direction, out float outerRadius, out float outerAngle)
    {
        source = FlashlightWorldPosition;
        direction = FlashlightFacingDirection;
        outerRadius = FlashlightOuterRadius;
        outerAngle = FlashlightOuterAngle;

        return HasActiveFlashlight &&
               outerRadius > 0f &&
               outerAngle > 0f &&
               direction.sqrMagnitude > MinimumFlashlightDirectionSqr;
    }

    // Executes the ClearEquippedUtilityFromConsumption routine.
    public void ClearEquippedUtilityFromConsumption(UtilityItemData consumedUtility)
    {
        if (EquippedUtility == null || consumedUtility == null || EquippedUtility != consumedUtility)
            return;

        SetFlashlightEnabled(false, playSfx: false);
        EquippedUtility = null;
        IsAiming = false;
        ResetThrowableInputState();
        UpdateAimCameraState();
        MaintainMouseLookWhenNoUtilityIsHeld();
        NotifyUtilityStateChanged();
    }

    // Executes the EquipUtilityRoutine routine.
    private IEnumerator EquipUtilityRoutine(UtilityItemData utilityItem)
    {
        if (EquippedUtility != null)
            yield return HolsterCurrentUtilityInternal();

        if (utilityItem.EquipTime > 0f)
            yield return new WaitForSeconds(utilityItem.EquipTime);

        EquippedUtility = utilityItem;
        IsAiming = false;
        EmitNoiseSpike(utilityItem.EquipNoise, utilityItem.EquipNoiseDuration, utilityItem.EquipNoiseType, utilityItem.EquipExtremeNoise);
        ApplyInitialUtilityState(utilityItem);
        NotifyUtilityStateChanged();
        busyRoutine = null;
    }

    // Executes the HolsterUtilityRoutine routine.
    private IEnumerator HolsterUtilityRoutine()
    {
        yield return HolsterCurrentUtilityInternal();
        busyRoutine = null;
    }

    // Executes the HolsterCurrentUtilityInternal routine.
    private IEnumerator HolsterCurrentUtilityInternal()
    {
        UtilityItemData utilityBeingHolstered = EquippedUtility;
        if (utilityBeingHolstered == null)
            yield break;

        IsAiming = false;
        CancelThrowableCharge();
        UpdateAimCameraState();

        if (utilityBeingHolstered.HolsterTime > 0f)
            yield return new WaitForSeconds(utilityBeingHolstered.HolsterTime);

        SetFlashlightEnabled(false, playSfx: false);
        EmitNoiseSpike(
            utilityBeingHolstered.HolsterNoise,
            utilityBeingHolstered.HolsterNoiseDuration,
            utilityBeingHolstered.HolsterNoiseType,
            utilityBeingHolstered.HolsterExtremeNoise);
        EquippedUtility = null;
        ResetThrowableInputState();
        NotifyUtilityStateChanged();
    }

    // Executes the ApplyInitialUtilityState routine.
    private void ApplyInitialUtilityState(UtilityItemData utilityItem)
    {
        bool enableFlashlight = utilityItem is FlashlightUtilityData flashlightData && flashlightData.StartEnabledWhenEquipped;
        if (utilityItem is ThrowableUtilityData throwableData)
            RegisterThrowablePrefab(throwableData);

        SetFlashlightEnabled(enableFlashlight, playSfx: false);
        ResetThrowableInputState();
        UpdateAimCameraState();
    }

    // Executes the UpdateThrowableInput routine.
    private void UpdateThrowableInput(ThrowableUtilityData throwableData)
    {
        if (throwableData == null)
            return;

        if (!HasThrowableUsesAvailable(throwableData))
        {
            if (isChargingThrowable)
            {
                CancelThrowableCharge();
                return;
            }

            return;
        }

        if (busyRoutine != null)
            return;

        if (isChargingThrowable)
        {
            ThrowableChargeProgress01 = ResolveThrowableChargeProgress01(throwableData);

            if (inputReader.GetButtonDown(cancelThrowableAction))
            {
                CancelThrowableCharge();
                return;
            }

            if (inputReader.GetButtonUp(primaryAction))
            {
                busyRoutine = StartCoroutine(ThrowThrowableRoutine(throwableData, ThrowableChargeProgress01));
                return;
            }

            return;
        }

        if (inputReader.GetButtonDown(primaryAction))
            BeginThrowableCharge();
    }

    // Executes the BeginThrowableCharge routine.
    private void BeginThrowableCharge()
    {
        isChargingThrowable = true;
        throwableChargeStartedAt = Time.time;
        ThrowableChargeProgress01 = 0f;
        NotifyUtilityStateChanged();
        UtilityActivated?.Invoke();
    }

    // Executes the CancelThrowableCharge routine.
    private void CancelThrowableCharge()
    {
        if (!isChargingThrowable)
            return;

        ResetThrowableInputState();
        NotifyUtilityStateChanged();
    }

    // Executes the ThrowThrowableRoutine routine.
    private IEnumerator ThrowThrowableRoutine(ThrowableUtilityData throwableData, float chargeProgress01)
    {
        if (aimCamera != null)
        {
            aimCamera.SetFollowTarget(transform);
            aimCamera.SetAimState(false, 0f);
        }

        ResetThrowableInputState();
        NotifyUtilityStateChanged();

        if (!TrySpawnThrowable(throwableData, chargeProgress01))
        {
            busyRoutine = null;
            yield break;
        }

        float animationDuration = Mathf.Max(0.01f, throwableData.ThrowAnimationDuration);
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            ThrowableThrowProgress01 = Mathf.Clamp01(elapsed / animationDuration);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ThrowableThrowProgress01 = 1f;
        yield return null;

        if (playerEquipmentController != null)
            playerEquipmentController.ConsumeCurrentHeldUtility();
        else
            ClearEquippedUtilityFromConsumption(throwableData);

        if (EquippedThrowable == throwableData)
        {
            ThrowableThrowProgress01 = 0f;
            NotifyUtilityStateChanged();
        }

        busyRoutine = null;
    }

    // Executes the TrySpawnThrowable routine.
    private bool TrySpawnThrowable(ThrowableUtilityData throwableData, float chargeProgress01)
    {
        if (throwableData == null || throwableData.ThrowableWorldPrefab == null || !HasThrowableUsesAvailable(throwableData))
            return false;

        globalObjectPooler = WeaponRuntimeUtility.ResolveGlobalObjectPooler(globalObjectPooler);

        if (globalObjectPooler == null)
            return false;

        Vector3 spawnPosition = throwableSpawnOrigin != null ? throwableSpawnOrigin.position : transform.position;
        Vector2 throwDirection = ResolveThrowableAimDirection();
        ThrowableWorldObject throwableWorldObject = globalObjectPooler.Spawn(
            throwableData.ThrowableWorldPrefab,
            spawnPosition,
            Quaternion.identity,
            null,
            throwableData.ThrowablePoolPrewarm);
        if (throwableWorldObject == null)
            return false;

        throwableWorldObject.Launch(throwableData, gameObject, spawnPosition, throwDirection, Mathf.Clamp01(chargeProgress01));
        return true;
    }

    // Executes the ResolveThrowableAimDirection routine.
    private Vector2 ResolveThrowableAimDirection()
    {
        if (playerVisionLight != null && playerVisionLight.FacingDirection.sqrMagnitude > MinimumThrowDirectionSqr)
            return playerVisionLight.FacingDirection.normalized;

        Vector2 transformUp = transform.up;
        return transformUp.sqrMagnitude > MinimumThrowDirectionSqr ? transformUp.normalized : Vector2.up;
    }

    // Executes the ResolveThrowableChargeProgress01 routine.
    private float ResolveThrowableChargeProgress01(ThrowableUtilityData throwableData)
    {
        if (throwableData == null)
            return 0f;

        float threshold = Mathf.Max(0.01f, throwableData.ChargeThreshold);
        return Mathf.Clamp01((Time.time - throwableChargeStartedAt) / threshold);
    }

    // Executes the HasThrowableUsesAvailable routine.
    private bool HasThrowableUsesAvailable(ThrowableUtilityData throwableData)
    {
        if (throwableData == null)
            return false;

        if (playerEquipmentController == null || !playerEquipmentController.CurrentHeldSlot.IsHandSlot())
            return true;

        if (!playerEquipmentController.TryGetRuntimeThrowableState(playerEquipmentController.CurrentHeldSlot, out int remainingUses, out _))
            return true;

        return remainingUses > 0;
    }

    // Executes the HandlePrimaryAction routine.
    private void HandlePrimaryAction()
    {
        if (EquippedUtility is not FlashlightUtilityData flashlightData)
            return;

        SetFlashlightEnabled(!IsFlashlightOn, playSfx: true, flashlightData);
    }

    // Executes the SetFlashlightEnabled routine.
    private void SetFlashlightEnabled(bool enabled, bool playSfx, FlashlightUtilityData flashlightData = null)
    {
        bool previousState = IsFlashlightOn;
        IsFlashlightOn = enabled && EquippedUtility is FlashlightUtilityData;

        if (flashlightLight != null)
        {
            if (flashlightLight.gameObject.activeSelf != IsFlashlightOn)
                flashlightLight.gameObject.SetActive(IsFlashlightOn);

            flashlightLight.enabled = IsFlashlightOn;
        }

        if (previousState != IsFlashlightOn)
        {
            NotifyUtilityStateChanged();
            UtilityActivated?.Invoke();
        }

        if (!playSfx)
            return;

        flashlightData ??= EquippedUtility as FlashlightUtilityData;
        if (flashlightData == null)
            return;

        EmitNoiseSpike(flashlightData.ToggleNoise, flashlightData.ToggleNoiseDuration, flashlightData.ToggleSfxType, flashlightData.ToggleExtremeNoise);

        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);

        if (worldSfxManager == null)
            return;

        Vector3 origin = sfxOrigin != null ? sfxOrigin.position : transform.position;
        worldSfxManager.PlayClipSetAt(origin, flashlightData.ToggleSfx, flashlightData.ToggleSfxType);
    }

    // Executes the UpdateAimCameraState routine.
    private void UpdateAimCameraState()
    {
        if (aimCamera == null)
            return;

        aimCamera.SetFollowTarget(transform);
        aimCamera.SetAimState(IsAiming, EquippedUtility != null ? EquippedUtility.AimPanDistance : 0f);
    }

    // Executes the NotifyUtilityStateChanged routine.
    private void NotifyUtilityStateChanged()
    {
        UtilityStateChanged?.Invoke();
    }

    // Executes the RegisterThrowablePrefab routine.
    private void RegisterThrowablePrefab(ThrowableUtilityData throwableData)
    {
        if (throwableData == null || throwableData.ThrowableWorldPrefab == null)
            return;

        globalObjectPooler = WeaponRuntimeUtility.ResolveGlobalObjectPooler(globalObjectPooler);

        globalObjectPooler?.RegisterPrefab(throwableData.ThrowableWorldPrefab.gameObject, throwableData.ThrowablePoolPrewarm);
        if (throwableData.ResolveEffectPrefab != null)
            globalObjectPooler?.RegisterPrefab(throwableData.ResolveEffectPrefab, throwableData.ResolveEffectPoolPrewarm);
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

    // Executes the ResetThrowableInputState routine.
    private void ResetThrowableInputState()
    {
        isChargingThrowable = false;
        throwableChargeStartedAt = 0f;
        ThrowableChargeProgress01 = 0f;
        ThrowableThrowProgress01 = 0f;
    }

    // Executes the MaintainMouseLookWhenNoUtilityIsHeld routine.
    private void MaintainMouseLookWhenNoUtilityIsHeld()
    {
        if (playerVisionLight == null || inputBlocked)
            return;

        if (playerEquipmentController != null && playerEquipmentController.CurrentHeldItem != null)
            return;

        if (playerEquipmentController != null && playerEquipmentController.IsUnarmedAiming)
        {
            if (aimCamera != null)
            {
                aimCamera.SetFollowTarget(transform);
                aimCamera.SetAimState(true, playerEquipmentController.CurrentUnarmedAimPanDistance);
            }

            return;
        }

        float lookSpeed = playerVisionLight.RotationSmoothing;
        if (actorStaggerController != null)
            lookSpeed *= actorStaggerController.TurnSpeedMultiplier;

        playerVisionLight.DriveMouseLook(lookSpeed, Time.deltaTime);
    }
}

}
