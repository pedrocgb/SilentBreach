using Sirenix.OdinInspector;
using UnityEngine;
using Breezeblocks.Input;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Dynamic Crosshair UI")]
public class DynamicCrosshairUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController weaponController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController equipmentController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerUtilityController utilityController;

    [FoldoutGroup("References")]
    [SerializeField] private Canvas targetCanvas;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform crosshairRoot;

    [FoldoutGroup("References"), Required]
    [SerializeField] private RectTransform topLine;

    [FoldoutGroup("References"), Required]
    [SerializeField] private RectTransform bottomLine;

    [FoldoutGroup("References"), Required]
    [SerializeField] private RectTransform leftLine;

    [FoldoutGroup("References"), Required]
    [SerializeField] private RectTransform rightLine;

    [FoldoutGroup("Cursor")]
    [SerializeField] private bool hideSystemCursor = true;

    [FoldoutGroup("Cursor")]
    [SerializeField] private bool followMouse = true;

    [FoldoutGroup("Spread"), MinValue(0f)]
    [SerializeField] private float closedSpreadPixels = 8f;

    [FoldoutGroup("Spread"), MinValue(0f)]
    [SerializeField] private float openSpreadPixels = 40f;

    [FoldoutGroup("Spread"), MinValue(0f)]
    [SerializeField] private float hipFireSpreadPixels = 52f;

    [FoldoutGroup("Spread"), MinValue(0.01f)]
    [SerializeField] private float maxReferenceSpreadAngle = 16f;

    [FoldoutGroup("Animation"), MinValue(0f)]
    [SerializeField] private float spreadLerpSpeed = 18f;

    [FoldoutGroup("Animation"), MinValue(0f)]
    [SerializeField] private float mouseFollowLerpSpeed = 28f;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public float CurrentSpreadPixels => currentSpreadPixels;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public float TargetSpreadPixels => targetSpreadPixels;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public bool IsUiSuppressed => uiSuppressed;

    private float currentSpreadPixels;
    private float targetSpreadPixels;
    private bool uiSuppressed;
    private IPointerInputReader pointerInputReader;

    // Executes the Reset routine.
    private void Reset()
    {
        weaponController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerWeaponController>(gameObject);
        if (equipmentController == null)
            equipmentController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerEquipmentController>(gameObject);

        if (utilityController == null)
            utilityController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerUtilityController>(gameObject);

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (crosshairRoot == null)
            crosshairRoot = transform as RectTransform;
    }

    // Executes the Awake routine.
    private void Awake()
    {
        pointerInputReader ??= new RewiredPlayerInputReader();
        if (weaponController == null)
            weaponController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerWeaponController>(gameObject);

        if (equipmentController == null)
            equipmentController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerEquipmentController>(gameObject);

        if (utilityController == null)
            utilityController = PlayerSceneReferenceUtility.FindFirstComponentInLoadedScenes<PlayerUtilityController>(gameObject);

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (crosshairRoot == null)
            crosshairRoot = transform as RectTransform;
    }

    // Executes the OnEnable routine.
    private void OnEnable()
    {
        ApplyCursorVisibility(true);
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        ApplyCursorVisibility(false);
    }

    // Executes the OnApplicationFocus routine.
    private void OnApplicationFocus(bool hasFocus)
    {
        ApplyCursorVisibility(hasFocus);
    }

    // Executes the Update routine.
    private void Update()
    {
        if (crosshairRoot == null)
            return;

        if (crosshairRoot.gameObject.activeSelf != !uiSuppressed)
            crosshairRoot.gameObject.SetActive(!uiSuppressed);

        if (uiSuppressed)
            return;

        if (followMouse)
            UpdateCrosshairPosition();

        UpdateSpread();
        ApplySpreadToLines();
    }

    // Executes the UpdateCrosshairPosition routine.
    private void UpdateCrosshairPosition()
    {
        RectTransform canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : crosshairRoot.parent as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;
        Vector2 screenPosition = pointerInputReader != null
            ? pointerInputReader.GetScreenPositionOrDefault()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 localPoint))
            return;

        if (mouseFollowLerpSpeed <= 0f)
        {
            crosshairRoot.anchoredPosition = localPoint;
            return;
        }

        crosshairRoot.anchoredPosition = Vector2.Lerp(
            crosshairRoot.anchoredPosition,
            localPoint,
            1f - Mathf.Exp(-mouseFollowLerpSpeed * Time.unscaledDeltaTime));
    }

    // Executes the UpdateSpread routine.
    private void UpdateSpread()
    {
        targetSpreadPixels = ResolveTargetSpreadPixels();

        if (spreadLerpSpeed <= 0f)
        {
            currentSpreadPixels = targetSpreadPixels;
            return;
        }

        currentSpreadPixels = Mathf.Lerp(
            currentSpreadPixels,
            targetSpreadPixels,
            1f - Mathf.Exp(-spreadLerpSpeed * Time.unscaledDeltaTime));
    }

    // Executes the ResolveTargetSpreadPixels routine.
    private float ResolveTargetSpreadPixels()
    {
        if (equipmentController != null && equipmentController.CurrentHeldItem is MeleeWeaponData)
            return closedSpreadPixels;

        if (utilityController != null && utilityController.EquippedThrowable != null)
        {
            if (!utilityController.IsChargingThrowable)
                return hipFireSpreadPixels;

            return Mathf.Lerp(hipFireSpreadPixels, 0f, utilityController.ThrowableChargeProgress01);
        }

        if (weaponController == null || weaponController.EquippedFirearm == null)
            return hipFireSpreadPixels;

        if (!weaponController.IsAiming)
            return hipFireSpreadPixels;

        float normalizedSpread = maxReferenceSpreadAngle <= 0f
            ? 0f
            : Mathf.Clamp01(weaponController.CurrentSpreadAngle / maxReferenceSpreadAngle);

        return Mathf.Lerp(closedSpreadPixels, openSpreadPixels, normalizedSpread);
    }

    // Executes the ApplySpreadToLines routine.
    private void ApplySpreadToLines()
    {
        if (topLine != null)
            topLine.anchoredPosition = Vector2.up * currentSpreadPixels;

        if (bottomLine != null)
            bottomLine.anchoredPosition = Vector2.down * currentSpreadPixels;

        if (leftLine != null)
            leftLine.anchoredPosition = Vector2.left * currentSpreadPixels;

        if (rightLine != null)
            rightLine.anchoredPosition = Vector2.right * currentSpreadPixels;
    }

    // Executes the ApplyCursorVisibility routine.
    private void ApplyCursorVisibility(bool hasFocus)
    {
        if (!hideSystemCursor)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = uiSuppressed || !hasFocus;
    }

    // Executes the SetUiSuppressed routine.
    public void SetUiSuppressed(bool suppressed)
    {
        if (uiSuppressed == suppressed)
            return;

        uiSuppressed = suppressed;
        if (crosshairRoot != null)
            crosshairRoot.gameObject.SetActive(!uiSuppressed);

        ApplyCursorVisibility(Application.isFocused);
    }
}
}
