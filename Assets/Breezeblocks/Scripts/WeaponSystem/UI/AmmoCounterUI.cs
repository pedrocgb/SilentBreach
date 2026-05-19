using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/UI/Ammo Counter UI")]
public class AmmoCounterUI : MonoBehaviour
{
    private const string DefaultGeneratedTextName = "Ammo Counter Text";
    private const string DefaultAmmoFormat = "{0} / {1}";
    private const string DefaultReloadingText = "Reloading...";
    private const string DefaultNoWeaponText = "-- / --";

    [FoldoutGroup("References"), Tooltip("Optional explicit weapon controller reference. If empty, auto-finds one.")]
    [SerializeField] private PlayerWeaponController weaponController;

    [FoldoutGroup("References"), Tooltip("Optional explicit equipment controller reference. If empty, auto-finds one.")]
    [SerializeField] private PlayerEquipmentController equipmentController;

    [FoldoutGroup("References"), Tooltip("Optional explicit utility controller reference. If empty, auto-finds one.")]
    [SerializeField] private PlayerUtilityController utilityController;

    [FoldoutGroup("References"), Tooltip("Optional existing TMP text. If empty, one is created at runtime as a child.")]
    [SerializeField] private TMP_Text ammoText;

    [FoldoutGroup("Format")]
    [SerializeField] private string ammoFormat = "{0} / {1}";

    [FoldoutGroup("Format")]
    [SerializeField] private string reloadingText = "Reloading...";

    [FoldoutGroup("Format")]
    [SerializeField] private string noWeaponText = "-- / --";

    [FoldoutGroup("Generated Text"), MinValue(0)]
    [SerializeField] private int fontSize = 28;

    [FoldoutGroup("Generated Text")]
    [SerializeField] private Color textColor = Color.white;

    [FoldoutGroup("Generated Text")]
    [SerializeField] private Vector2 generatedAnchorMin = new(0.5f, 0f);

    [FoldoutGroup("Generated Text")]
    [SerializeField] private Vector2 generatedAnchorMax = new(0.5f, 0f);

    [FoldoutGroup("Generated Text")]
    [SerializeField] private Vector2 generatedPivot = new(0.5f, 0f);

    [FoldoutGroup("Generated Text")]
    [SerializeField] private Vector2 generatedAnchoredPosition = new(0f, -36f);

    [FoldoutGroup("Generated Text")]
    [SerializeField] private Vector2 generatedSizeDelta = new(220f, 36f);

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public string CurrentDisplayText => _currentDisplayText;

    private string _currentDisplayText;
    private PlayerWeaponController _subscribedWeaponController;
    private PlayerEquipmentController _subscribedEquipmentController;
    private PlayerUtilityController _subscribedUtilityController;

    private void Awake()
    {
        if (DisableIfNestedDuplicate())
            return;

        ResolveReferences();
        EnsureTextReference();
        SubscribeToControllers();
        RefreshDisplay();
    }

    private void OnEnable()
    {
        if (DisableIfNestedDuplicate())
            return;

        ResolveReferences();
        EnsureTextReference();
        SubscribeToControllers();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnsubscribeFromControllers();
    }

    private void OnValidate()
    {
        fontSize = Mathf.Max(0, fontSize);
        generatedSizeDelta.x = Mathf.Max(0f, generatedSizeDelta.x);
        generatedSizeDelta.y = Mathf.Max(0f, generatedSizeDelta.y);
    }

    private void Update()
    {
        if (!enabled)
            return;

        if (weaponController == null || equipmentController == null || utilityController == null)
        {
            ResolveReferences();
            SubscribeToControllers();
        }

        RefreshDisplay();
    }

    private void ResolveReferences()
    {
        if (weaponController == null)
            weaponController = FindFirstObjectByType<PlayerWeaponController>();

        if (equipmentController == null)
            equipmentController = FindFirstObjectByType<PlayerEquipmentController>();

        if (utilityController == null)
            utilityController = FindFirstObjectByType<PlayerUtilityController>();

        if (ammoText == null)
            ammoText = ResolveBestTextReference();
    }

    private void EnsureTextReference()
    {
        if (ammoText != null)
            return;

        GameObject textObject = new GameObject(DefaultGeneratedTextName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(transform, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = generatedAnchorMin;
        rectTransform.anchorMax = generatedAnchorMax;
        rectTransform.pivot = generatedPivot;
        rectTransform.anchoredPosition = generatedAnchoredPosition;
        rectTransform.sizeDelta = generatedSizeDelta;

        TextMeshProUGUI generatedText = textObject.GetComponent<TextMeshProUGUI>();
        generatedText.font = TMP_Settings.defaultFontAsset;
        generatedText.fontSize = fontSize;
        generatedText.alignment = TextAlignmentOptions.Center;
        generatedText.color = textColor;
        generatedText.raycastTarget = false;
        generatedText.textWrappingMode = TextWrappingModes.NoWrap;
        generatedText.overflowMode = TextOverflowModes.Overflow;
        ammoText = generatedText;
    }

    private TMP_Text ResolveBestTextReference()
    {
        TMP_Text selfText = GetComponent<TMP_Text>();
        if (selfText != null)
            return selfText;

        Transform generatedChild = transform.Find(DefaultGeneratedTextName);
        if (generatedChild != null && generatedChild.TryGetComponent(out TMP_Text generatedText))
            return generatedText;

        TMP_Text[] childTexts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < childTexts.Length; i++)
        {
            TMP_Text candidate = childTexts[i];
            if (candidate != null && candidate.transform.parent == transform)
                return candidate;
        }

        return null;
    }

    private void SubscribeToControllers()
    {
        SubscribeToWeaponController();
        SubscribeToEquipmentController();
        SubscribeToUtilityController();
    }

    private void SubscribeToWeaponController()
    {
        if (_subscribedWeaponController == weaponController)
            return;

        if (_subscribedWeaponController != null)
            _subscribedWeaponController.WeaponStateChanged -= HandleWeaponStateChanged;

        if (weaponController == null)
            return;

        weaponController.WeaponStateChanged += HandleWeaponStateChanged;
        _subscribedWeaponController = weaponController;
    }

    private void SubscribeToEquipmentController()
    {
        if (_subscribedEquipmentController == equipmentController)
            return;

        if (_subscribedEquipmentController != null)
            _subscribedEquipmentController.EquipmentChanged -= HandleEquipmentStateChanged;

        if (equipmentController == null)
            return;

        equipmentController.EquipmentChanged += HandleEquipmentStateChanged;
        _subscribedEquipmentController = equipmentController;
    }

    private void SubscribeToUtilityController()
    {
        if (_subscribedUtilityController == utilityController)
            return;

        if (_subscribedUtilityController != null)
            _subscribedUtilityController.UtilityStateChanged -= HandleUtilityStateChanged;

        if (utilityController == null)
            return;

        utilityController.UtilityStateChanged += HandleUtilityStateChanged;
        _subscribedUtilityController = utilityController;
    }

    private void UnsubscribeFromControllers()
    {
        if (_subscribedWeaponController != null)
        {
            _subscribedWeaponController.WeaponStateChanged -= HandleWeaponStateChanged;
            _subscribedWeaponController = null;
        }

        if (_subscribedEquipmentController != null)
        {
            _subscribedEquipmentController.EquipmentChanged -= HandleEquipmentStateChanged;
            _subscribedEquipmentController = null;
        }

        if (_subscribedUtilityController != null)
        {
            _subscribedUtilityController.UtilityStateChanged -= HandleUtilityStateChanged;
            _subscribedUtilityController = null;
        }
    }

    private void HandleWeaponStateChanged()
    {
        RefreshDisplay();
    }

    private void HandleEquipmentStateChanged()
    {
        RefreshDisplay();
    }

    private void HandleUtilityStateChanged()
    {
        RefreshDisplay();
    }

    private bool DisableIfNestedDuplicate()
    {
        AmmoCounterUI[] counters = GetComponentsInParent<AmmoCounterUI>(true);
        if (counters.Length <= 1)
            return false;

        AmmoCounterUI rootMostCounter = null;
        int rootMostDepth = int.MaxValue;

        for (int i = 0; i < counters.Length; i++)
        {
            AmmoCounterUI counter = counters[i];
            if (counter == null)
                continue;

            int depth = GetHierarchyDepth(counter.transform);
            if (depth < rootMostDepth)
            {
                rootMostDepth = depth;
                rootMostCounter = counter;
            }
        }

        if (rootMostCounter == null || rootMostCounter == this)
            return false;

        enabled = false;
        return true;
    }

    private static int GetHierarchyDepth(Transform target)
    {
        int depth = 0;
        Transform current = target;
        while (current != null)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }

    private void RefreshDisplay()
    {
        if (ammoText == null)
            return;

        string nextText = ResolveDisplayText();
        if (_currentDisplayText == nextText)
            return;

        _currentDisplayText = nextText;
        ammoText.text = nextText;
    }

    private string ResolveDisplayText()
    {
        if (weaponController != null && weaponController.EquippedFirearm != null && weaponController.CurrentProjectile != null)
        {
            if (weaponController.IsReloading)
                return string.IsNullOrWhiteSpace(reloadingText) ? DefaultReloadingText : reloadingText;

            string resolvedAmmoFormat = string.IsNullOrWhiteSpace(ammoFormat) ? DefaultAmmoFormat : ammoFormat;
            return string.Format(resolvedAmmoFormat, weaponController.CurrentLoadedAmmo, weaponController.CurrentReserveAmmo);
        }

        if (utilityController != null && utilityController.EquippedThrowable != null)
        {
            string resolvedAmmoFormat = string.IsNullOrWhiteSpace(ammoFormat) ? DefaultAmmoFormat : ammoFormat;
            if (equipmentController != null &&
                equipmentController.CurrentHeldSlot.IsHandSlot() &&
                equipmentController.TryGetRuntimeThrowableState(equipmentController.CurrentHeldSlot, out int remainingUses, out int maxUses))
            {
                return string.Format(resolvedAmmoFormat, remainingUses, maxUses);
            }

            int fallbackUses = utilityController.EquippedThrowable.MaxUses;
            return string.Format(resolvedAmmoFormat, fallbackUses, fallbackUses);
        }

        return string.IsNullOrWhiteSpace(noWeaponText) ? DefaultNoWeaponText : noWeaponText;
    }
}
}
