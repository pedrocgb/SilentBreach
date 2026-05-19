using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Dynamic Crosshair UI")]
public class DynamicCrosshairUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController weaponController;

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

    private void Reset()
    {
        weaponController = FindFirstObjectByType<PlayerWeaponController>();
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (crosshairRoot == null)
            crosshairRoot = transform as RectTransform;
    }

    private void Awake()
    {
        if (weaponController == null)
            weaponController = FindFirstObjectByType<PlayerWeaponController>();

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (crosshairRoot == null)
            crosshairRoot = transform as RectTransform;
    }

    private void OnEnable()
    {
        ApplyCursorVisibility(true);
    }

    private void OnDisable()
    {
        ApplyCursorVisibility(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        ApplyCursorVisibility(hasFocus);
    }

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

    private void UpdateCrosshairPosition()
    {
        RectTransform canvasRect = targetCanvas != null ? targetCanvas.transform as RectTransform : crosshairRoot.parent as RectTransform;
        if (canvasRect == null)
            return;

        Camera eventCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera
            : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, eventCamera, out Vector2 localPoint))
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

    private float ResolveTargetSpreadPixels()
    {
        if (weaponController == null || weaponController.EquippedFirearm == null)
            return hipFireSpreadPixels;

        if (!weaponController.IsAiming)
            return hipFireSpreadPixels;

        float normalizedSpread = maxReferenceSpreadAngle <= 0f
            ? 0f
            : Mathf.Clamp01(weaponController.CurrentSpreadAngle / maxReferenceSpreadAngle);

        return Mathf.Lerp(closedSpreadPixels, openSpreadPixels, normalizedSpread);
    }

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

    private void ApplyCursorVisibility(bool hasFocus)
    {
        if (!hideSystemCursor)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = uiSuppressed || !hasFocus;
    }

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
