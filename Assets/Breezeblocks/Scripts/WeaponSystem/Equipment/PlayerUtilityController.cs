using System;
using System.Collections;
using Rewired;
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

    [FoldoutGroup("References")]
    [SerializeField] private ActorStaggerController actorStaggerController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerNoise playerNoise;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController playerEquipmentController;

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

    private Player rewiredPlayer;
    private Coroutine busyRoutine;
    private bool inputBlocked;
    private bool isChargingThrowable;
    private float throwableChargeStartedAt;

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

    private void Awake()
    {
        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>();

        if (aimCamera == null && Camera.main != null)
            aimCamera = Camera.main.GetComponent<PlayerAimCamera2D>();

        if (aimCamera == null)
            aimCamera = FindFirstObjectByType<PlayerAimCamera2D>();

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

        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        SetFlashlightEnabled(false, playSfx: false);
        ResolveRewiredPlayer();
    }

    private void OnEnable()
    {
        ResolveRewiredPlayer();
        UpdateAimCameraState();
    }

    private void OnDisable()
    {
        IsAiming = false;
        ResetThrowableInputState();
        UpdateAimCameraState();
    }

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
            if (IsAiming)
            {
                IsAiming = false;
                UpdateAimCameraState();
            }

            return;
        }

        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return;

        bool aimHeld = busyRoutine == null && rewiredPlayer.GetButton(aimAction);
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

        if (busyRoutine == null && rewiredPlayer.GetButtonDown(primaryAction))
            HandlePrimaryAction();
    }

    public void EquipUtility(UtilityItemData utilityItem)
    {
        if (utilityItem == null || IsBusy)
            return;

        busyRoutine = StartCoroutine(EquipUtilityRoutine(utilityItem));
    }

    public void HolsterCurrentUtility()
    {
        if (EquippedUtility == null || IsBusy)
            return;

        busyRoutine = StartCoroutine(HolsterUtilityRoutine());
    }

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

    public void ClearEquippedUtilityFromConsumption(UtilityItemData consumedUtility)
    {
        if (EquippedUtility == null || consumedUtility == null || EquippedUtility != consumedUtility)
            return;

        SetFlashlightEnabled(false, playSfx: false);
        EquippedUtility = null;
        IsAiming = false;
        ResetThrowableInputState();
        UpdateAimCameraState();
        NotifyUtilityStateChanged();
    }

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

    private IEnumerator HolsterUtilityRoutine()
    {
        yield return HolsterCurrentUtilityInternal();
        busyRoutine = null;
    }

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

    private void ApplyInitialUtilityState(UtilityItemData utilityItem)
    {
        bool enableFlashlight = utilityItem is FlashlightUtilityData flashlightData && flashlightData.StartEnabledWhenEquipped;
        if (utilityItem is ThrowableUtilityData throwableData)
            RegisterThrowablePrefab(throwableData);

        SetFlashlightEnabled(enableFlashlight, playSfx: false);
        ResetThrowableInputState();
        UpdateAimCameraState();
    }

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

            if (rewiredPlayer.GetButtonDown(cancelThrowableAction))
            {
                CancelThrowableCharge();
                return;
            }

            if (rewiredPlayer.GetButtonUp(primaryAction))
            {
                busyRoutine = StartCoroutine(ThrowThrowableRoutine(throwableData, ThrowableChargeProgress01));
                return;
            }

            return;
        }

        if (rewiredPlayer.GetButtonDown(primaryAction))
            BeginThrowableCharge();
    }

    private void BeginThrowableCharge()
    {
        isChargingThrowable = true;
        throwableChargeStartedAt = Time.time;
        ThrowableChargeProgress01 = 0f;
        NotifyUtilityStateChanged();
    }

    private void CancelThrowableCharge()
    {
        if (!isChargingThrowable)
            return;

        ResetThrowableInputState();
        NotifyUtilityStateChanged();
    }

    private IEnumerator ThrowThrowableRoutine(ThrowableUtilityData throwableData, float chargeProgress01)
    {
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

    private bool TrySpawnThrowable(ThrowableUtilityData throwableData, float chargeProgress01)
    {
        if (throwableData == null || throwableData.ThrowableWorldPrefab == null || !HasThrowableUsesAvailable(throwableData))
            return false;

        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

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

    private Vector2 ResolveThrowableAimDirection()
    {
        if (playerVisionLight != null && playerVisionLight.FacingDirection.sqrMagnitude > MinimumThrowDirectionSqr)
            return playerVisionLight.FacingDirection.normalized;

        Vector2 transformUp = transform.up;
        return transformUp.sqrMagnitude > MinimumThrowDirectionSqr ? transformUp.normalized : Vector2.up;
    }

    private float ResolveThrowableChargeProgress01(ThrowableUtilityData throwableData)
    {
        if (throwableData == null)
            return 0f;

        float threshold = Mathf.Max(0.01f, throwableData.ChargeThreshold);
        return Mathf.Clamp01((Time.time - throwableChargeStartedAt) / threshold);
    }

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

    private void HandlePrimaryAction()
    {
        if (EquippedUtility is not FlashlightUtilityData flashlightData)
            return;

        SetFlashlightEnabled(!IsFlashlightOn, playSfx: true, flashlightData);
    }

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
            NotifyUtilityStateChanged();

        if (!playSfx)
            return;

        flashlightData ??= EquippedUtility as FlashlightUtilityData;
        if (flashlightData == null)
            return;

        EmitNoiseSpike(flashlightData.ToggleNoise, flashlightData.ToggleNoiseDuration, flashlightData.ToggleSfxType, flashlightData.ToggleExtremeNoise);

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        if (worldSfxManager == null)
            return;

        Vector3 origin = sfxOrigin != null ? sfxOrigin.position : transform.position;
        worldSfxManager.PlayClipSetAt(origin, flashlightData.ToggleSfx, flashlightData.ToggleSfxType);
    }

    private void UpdateAimCameraState()
    {
        if (aimCamera == null)
            return;

        aimCamera.SetFollowTarget(transform);
        aimCamera.SetAimState(IsAiming, EquippedUtility != null ? EquippedUtility.AimPanDistance : 0f);
    }

    private void NotifyUtilityStateChanged()
    {
        UtilityStateChanged?.Invoke();
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }

    private void RegisterThrowablePrefab(ThrowableUtilityData throwableData)
    {
        if (throwableData == null || throwableData.ThrowableWorldPrefab == null)
            return;

        if (globalObjectPooler == null)
            globalObjectPooler = GlobalObjectPooler.Instance;

        globalObjectPooler?.RegisterPrefab(throwableData.ThrowableWorldPrefab.gameObject, throwableData.ThrowablePoolPrewarm);
        if (throwableData.ResolveEffectPrefab != null)
            globalObjectPooler?.RegisterPrefab(throwableData.ResolveEffectPrefab, throwableData.ResolveEffectPoolPrewarm);
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

    private void ResetThrowableInputState()
    {
        isChargingThrowable = false;
        throwableChargeStartedAt = 0f;
        ThrowableChargeProgress01 = 0f;
        ThrowableThrowProgress01 = 0f;
    }
}

}
