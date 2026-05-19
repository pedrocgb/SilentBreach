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

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string aimAction = "Aim";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string primaryAction = "Fire";

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerAimCamera2D aimCamera;

    [FoldoutGroup("References")]
    [SerializeField] private Transform sfxOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private ActorStaggerController actorStaggerController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerNoise playerNoise;

    [FoldoutGroup("Flashlight")]
    [Tooltip("Dedicated Light2D used by the flashlight utility. Assign this explicitly instead of the player vision light.")]
    [SerializeField] private Light2D flashlightLight;

    [FoldoutGroup("Audio")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public UtilityItemData EquippedUtility { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAiming { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsBusy => busyRoutine != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsFlashlightOn { get; private set; }

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

    private void Reset()
    {
        playerVisionLight = GetComponentInChildren<PlayerVisionLight>();
        if (Camera.main != null)
            aimCamera = Camera.main.GetComponent<PlayerAimCamera2D>();

        sfxOrigin = transform;
        actorStaggerController = GetComponent<ActorStaggerController>();
        playerNoise = GetComponent<PlayerNoise>();
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

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        if (playerNoise == null)
            playerNoise = GetComponent<PlayerNoise>();

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
        UpdateAimCameraState();
    }

    private void Update()
    {
        if (inputBlocked)
        {
            if (IsAiming)
            {
                IsAiming = false;
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

        bool aimHeld = !IsBusy && rewiredPlayer.GetButton(aimAction);
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

        if (!IsBusy && rewiredPlayer.GetButtonDown(primaryAction))
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
        if (blocked && IsAiming)
        {
            IsAiming = false;
            UpdateAimCameraState();
            NotifyUtilityStateChanged();
        }
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

    private IEnumerator EquipUtilityRoutine(UtilityItemData utilityItem)
    {
        if (EquippedUtility != null)
            yield return HolsterCurrentUtilityInternal();

        if (utilityItem.EquipTime > 0f)
            yield return new WaitForSeconds(utilityItem.EquipTime);

        EquippedUtility = utilityItem;
        IsAiming = false;
        EmitNoiseSpike(utilityItem.EquipNoise, utilityItem.EquipNoiseDuration, utilityItem.EquipNoiseType);
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
        UpdateAimCameraState();

        if (utilityBeingHolstered.HolsterTime > 0f)
            yield return new WaitForSeconds(utilityBeingHolstered.HolsterTime);

        SetFlashlightEnabled(false, playSfx: false);
        EmitNoiseSpike(
            utilityBeingHolstered.HolsterNoise,
            utilityBeingHolstered.HolsterNoiseDuration,
            utilityBeingHolstered.HolsterNoiseType);
        EquippedUtility = null;
        NotifyUtilityStateChanged();
    }

    private void ApplyInitialUtilityState(UtilityItemData utilityItem)
    {
        bool enableFlashlight = utilityItem is FlashlightUtilityData flashlightData && flashlightData.StartEnabledWhenEquipped;
        SetFlashlightEnabled(enableFlashlight, playSfx: false);
        UpdateAimCameraState();
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

        EmitNoiseSpike(flashlightData.ToggleNoise, flashlightData.ToggleNoiseDuration, flashlightData.ToggleSfxType);

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

    private void EmitNoiseSpike(float amount, float duration, NoiseType noiseType)
    {
        if (playerNoise != null)
            playerNoise.AddNoiseSpike(amount, duration, noiseType);
    }
}
}
