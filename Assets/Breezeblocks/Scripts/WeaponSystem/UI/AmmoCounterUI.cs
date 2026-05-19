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

    private void Awake()
    {
        if (DisableIfNestedDuplicate())
            return;

        ResolveReferences();
        EnsureTextReference();
        SubscribeToWeaponController();
        RefreshDisplay();
    }

    private void OnEnable()
    {
        if (DisableIfNestedDuplicate())
            return;

        ResolveReferences();
        EnsureTextReference();
        SubscribeToWeaponController();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnsubscribeFromWeaponController();
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

        if (weaponController == null)
        {
            ResolveReferences();
            SubscribeToWeaponController();
        }

        RefreshDisplay();
    }

    private void ResolveReferences()
    {
        if (weaponController == null)
            weaponController = FindFirstObjectByType<PlayerWeaponController>();

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

    private void SubscribeToWeaponController()
    {
        if (_subscribedWeaponController == weaponController)
            return;

        UnsubscribeFromWeaponController();

        if (weaponController == null)
            return;

        weaponController.WeaponStateChanged += HandleWeaponStateChanged;
        _subscribedWeaponController = weaponController;
    }

    private void UnsubscribeFromWeaponController()
    {
        if (_subscribedWeaponController == null)
            return;

        _subscribedWeaponController.WeaponStateChanged -= HandleWeaponStateChanged;
        _subscribedWeaponController = null;
    }

    private void HandleWeaponStateChanged()
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
        if (weaponController == null || weaponController.EquippedFirearm == null || weaponController.CurrentProjectile == null)
            return string.IsNullOrWhiteSpace(noWeaponText) ? DefaultNoWeaponText : noWeaponText;

        if (weaponController.IsReloading)
            return string.IsNullOrWhiteSpace(reloadingText) ? DefaultReloadingText : reloadingText;

        string resolvedAmmoFormat = string.IsNullOrWhiteSpace(ammoFormat) ? DefaultAmmoFormat : ammoFormat;
        return string.Format(resolvedAmmoFormat, weaponController.CurrentLoadedAmmo, weaponController.CurrentReserveAmmo);
    }
}
}
