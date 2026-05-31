using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.HideoutSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Hideout/Perks Panel UI")]
public sealed class HideoutPerksPanelUI : MonoBehaviour
{
    [Serializable]
    private sealed class TierSectionReferences
    {
        public HideoutPerkTier tier = HideoutPerkTier.TierI;
        public TMP_Text titleText;
        public GameObject titlePadlockObject;
        public Graphic backgroundGraphic;
        public RectTransform contentRoot;

        [NonSerialized] public bool hasCachedBackgroundColor;
        [NonSerialized] public Color defaultBackgroundColor = Color.white;
    }

    [FoldoutGroup("Data"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutPerkDefinition> availablePerks = new();

    [FoldoutGroup("Data")]
    [SerializeField] private string resourcesSearchPath = string.Empty;

    [FoldoutGroup("Data"), MinValue(1)]
    [SerializeField] private int maxEquippedPerks = 3;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text titleText;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text perkPointsText;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text emptyStateText;

    [FoldoutGroup("Tiers")]
    [SerializeField] private TierSectionReferences tierOneSection = new() { tier = HideoutPerkTier.TierI };

    [FoldoutGroup("Tiers")]
    [SerializeField] private TierSectionReferences tierTwoSection = new() { tier = HideoutPerkTier.TierII };

    [FoldoutGroup("Tiers")]
    [SerializeField] private TierSectionReferences tierThreeSection = new() { tier = HideoutPerkTier.TierIII };

    [FoldoutGroup("Selection")]
    [SerializeField] private RectTransform selectedPerksContent;

    [FoldoutGroup("Selection")]
    [SerializeField] private HideoutPerkItemUI availablePerkItemPrefab;

    [FoldoutGroup("Selection")]
    [SerializeField] private HideoutPerkItemUI selectedPerkItemPrefab;

    [FoldoutGroup("Selection")]
    [SerializeField] private Button confirmButton;

    [FoldoutGroup("Details")]
    [SerializeField] private Image selectedPerkIconImage;

    [FoldoutGroup("Details")]
    [SerializeField] private TMP_Text selectedPerkNameText;

    [FoldoutGroup("Details")]
    [SerializeField] private TMP_Text selectedPerkDescriptionText;

    [FoldoutGroup("Details")]
    [SerializeField] private TMP_Text selectedPerkTierText;

    [FoldoutGroup("Details")]
    [SerializeField] private TMP_Text selectedPerkCostText;

    [FoldoutGroup("Visuals")]
    [SerializeField] private Color lockedTierBackgroundTint = new(0.22f, 0.18f, 0.18f, 0.9f);

    private readonly List<HideoutPerkDefinition> configuredPerks = new();
    private readonly List<HideoutPerkDefinition> workingEquippedPerks = new();

    private HideoutPerkDefinition selectedPerk;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
        RefreshView();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        RefreshView();
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        CollectConfiguredPerks();
        PrepareTemplates();
        ConfigureTierTitles();
        CacheTierBackgroundDefaults();
        BindButtons();
        SyncWorkingSelectionFromConfirmed();
        EnsureValidSelection();
        initialized = true;
    }

    private void BindButtons()
    {
        if (confirmButton == null)
            return;

        confirmButton.onClick.RemoveListener(ConfirmSelection);
        confirmButton.onClick.AddListener(ConfirmSelection);
    }

    private void CollectConfiguredPerks()
    {
        configuredPerks.Clear();
        HashSet<string> addedPerkIds = new(StringComparer.OrdinalIgnoreCase);

        if (availablePerks.Count > 0)
        {
            for (int i = 0; i < availablePerks.Count; i++)
                TryAddConfiguredPerk(availablePerks[i], addedPerkIds);
        }
        else
        {
            HideoutPerkDefinition[] resourcePerks = Resources.LoadAll<HideoutPerkDefinition>(resourcesSearchPath ?? string.Empty);
            for (int i = 0; i < resourcePerks.Length; i++)
                TryAddConfiguredPerk(resourcePerks[i], addedPerkIds);
        }

        configuredPerks.Sort(ComparePerks);
    }

    private void TryAddConfiguredPerk(HideoutPerkDefinition perkDefinition, HashSet<string> addedPerkIds)
    {
        if (perkDefinition == null || addedPerkIds == null || string.IsNullOrWhiteSpace(perkDefinition.PerkId))
            return;

        if (!addedPerkIds.Add(perkDefinition.PerkId))
            return;

        configuredPerks.Add(perkDefinition);
    }

    private void PrepareTemplates()
    {
        if (availablePerkItemPrefab != null)
            availablePerkItemPrefab.gameObject.SetActive(false);

        if (selectedPerkItemPrefab != null)
            selectedPerkItemPrefab.gameObject.SetActive(false);
    }

    private void ConfigureTierTitles()
    {
        ConfigureTierTitle(tierOneSection, "Tier I");
        ConfigureTierTitle(tierTwoSection, "Tier II");
        ConfigureTierTitle(tierThreeSection, "Tier III");
    }

    private void CacheTierBackgroundDefaults()
    {
        CacheTierBackgroundDefault(tierOneSection);
        CacheTierBackgroundDefault(tierTwoSection);
        CacheTierBackgroundDefault(tierThreeSection);
    }

    private static void CacheTierBackgroundDefault(TierSectionReferences tierSection)
    {
        if (tierSection == null || tierSection.backgroundGraphic == null)
            return;

        tierSection.defaultBackgroundColor = tierSection.backgroundGraphic.color;
        tierSection.hasCachedBackgroundColor = true;
    }

    private static void ConfigureTierTitle(TierSectionReferences tierSection, string fallbackTitle)
    {
        if (tierSection == null || tierSection.titleText == null)
            return;

        if (string.IsNullOrWhiteSpace(tierSection.titleText.text))
            tierSection.titleText.text = fallbackTitle;
    }

    private void SyncWorkingSelectionFromConfirmed()
    {
        workingEquippedPerks.Clear();
        PlayerPerkRuntimeLoadout confirmedLoadout = PlayerPerkRuntimeSession.PeekEquippedPerks();
        IReadOnlyList<HideoutPerkDefinition> confirmedPerks = confirmedLoadout.EquippedPerks;
        for (int i = 0; i < confirmedPerks.Count; i++)
            TryAddWorkingEquippedPerk(ResolveConfiguredPerk(confirmedPerks[i]), enforceCapacity: true);
    }

    private void EnsureValidSelection()
    {
        if (selectedPerk != null && configuredPerks.Contains(selectedPerk))
            return;

        selectedPerk = configuredPerks.Count > 0 ? configuredPerks[0] : null;
    }

    public void RefreshView()
    {
        if (!initialized)
            EnsureInitialized();

        EnsureValidSelection();

        if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            titleText.text = "Perks";

        SetText(perkPointsText, HideoutRuntimeSession.PerkPoints.ToString());
        RefreshTierLocks();
        RefreshSelectedPerkDetails();
        RebuildTierLists();
        RebuildSelectedPerksList();
        RefreshConfirmButton();

        bool showEmptyState = configuredPerks.Count == 0;
        string emptyMessage = showEmptyState ? "No perks configured." : string.Empty;
        SetOptionalTextState(emptyStateText, showEmptyState, emptyMessage);
    }

    private void RefreshTierLocks()
    {
        SetTierLockState(tierOneSection, false);
        SetTierLockState(tierTwoSection, !IsTierRequirementFulfilled(HideoutPerkTier.TierII));
        SetTierLockState(tierThreeSection, !IsTierRequirementFulfilled(HideoutPerkTier.TierIII));
    }

    private void RefreshSelectedPerkDetails()
    {
        if (selectedPerk == null)
        {
            SetImage(selectedPerkIconImage, null);
            SetText(selectedPerkNameText, string.Empty);
            SetText(selectedPerkDescriptionText, string.Empty);
            SetText(selectedPerkTierText, string.Empty);
            SetText(selectedPerkCostText, string.Empty);
            return;
        }

        EquipmentContextUiSettings settings = ResolveUiSettings();
        SetImage(selectedPerkIconImage, selectedPerk.Icon);
        SetText(selectedPerkNameText, selectedPerk.PerkName);
        SetText(selectedPerkDescriptionText, BuildPerkDescription(selectedPerk));
        SetText(selectedPerkTierText, $"{settings.PerkTierText}{ResolveTierText(selectedPerk.Tier)}");
        SetText(selectedPerkCostText, $"{settings.PerkCostText}{selectedPerk.Cost}");
    }

    private void RebuildTierLists()
    {
        ClearTierSection(tierOneSection);
        ClearTierSection(tierTwoSection);
        ClearTierSection(tierThreeSection);

        if (configuredPerks.Count == 0 || availablePerkItemPrefab == null)
            return;

        for (int i = 0; i < configuredPerks.Count; i++)
        {
            HideoutPerkDefinition perkDefinition = configuredPerks[i];
            RectTransform contentRoot = ResolveContentRoot(perkDefinition.Tier);
            if (contentRoot == null)
                continue;

            HideoutPerkItemUI itemView = Instantiate(availablePerkItemPrefab, contentRoot);
            itemView.gameObject.name = $"{perkDefinition.PerkName} Perk";
            itemView.gameObject.SetActive(true);

            bool isUnlocked = HideoutRuntimeSession.IsPerkUnlocked(perkDefinition);
            bool isEquipped = IsPerkEquipped(perkDefinition);
            bool isSelected = selectedPerk == perkDefinition;
            bool canUnlockTier = IsTierRequirementFulfilled(perkDefinition.Tier);
            bool showBuyButton = isSelected && !isUnlocked && canUnlockTier;
            bool showEquipButton = isSelected && isUnlocked && !isEquipped;
            bool canBuy = showBuyButton && HideoutRuntimeSession.PerkPoints >= perkDefinition.Cost;
            bool canEquip = showEquipButton && workingEquippedPerks.Count < maxEquippedPerks;

            itemView.Bind(
                perkDefinition,
                isSelected,
                isUnlocked,
                isEquipped,
                showBuyButton,
                canBuy,
                showEquipButton,
                canEquip,
                () => SelectPerk(perkDefinition),
                () => BuyPerk(perkDefinition),
                () => EquipPerk(perkDefinition),
                canEquip ? () => EquipPerk(perkDefinition) : null);
        }
    }

    private void RebuildSelectedPerksList()
    {
        HideoutPerkItemUI prefabToUse = selectedPerkItemPrefab != null ? selectedPerkItemPrefab : availablePerkItemPrefab;
        Transform preservedTemplate = ResolvePreservedTemplate(
            selectedPerksContent,
            prefabToUse != null ? prefabToUse.transform : null);
        ClearGeneratedChildren(selectedPerksContent, preservedTemplate);

        if (selectedPerksContent == null || prefabToUse == null)
            return;

        for (int i = 0; i < workingEquippedPerks.Count; i++)
        {
            HideoutPerkDefinition perkDefinition = workingEquippedPerks[i];
            if (perkDefinition == null)
                continue;

            HideoutPerkItemUI itemView = Instantiate(prefabToUse, selectedPerksContent);
            itemView.gameObject.name = $"{perkDefinition.PerkName} Equipped";
            itemView.gameObject.SetActive(true);
            itemView.Bind(
                perkDefinition,
                selectedPerk == perkDefinition,
                true,
                true,
                false,
                false,
                false,
                false,
                () => UnequipPerk(perkDefinition),
                null,
                null);
        }
    }

    private void ClearTierSection(TierSectionReferences tierSection)
    {
        if (tierSection == null)
            return;

        Transform preservedTemplate = ResolvePreservedTemplate(
            tierSection.contentRoot,
            availablePerkItemPrefab != null ? availablePerkItemPrefab.transform : null);
        ClearGeneratedChildren(tierSection.contentRoot, preservedTemplate);
    }

    private RectTransform ResolveContentRoot(HideoutPerkTier perkTier)
    {
        return perkTier switch
        {
            HideoutPerkTier.TierI => tierOneSection != null ? tierOneSection.contentRoot : null,
            HideoutPerkTier.TierII => tierTwoSection != null ? tierTwoSection.contentRoot : null,
            HideoutPerkTier.TierIII => tierThreeSection != null ? tierThreeSection.contentRoot : null,
            _ => null
        };
    }

    private void SelectPerk(HideoutPerkDefinition perkDefinition)
    {
        selectedPerk = perkDefinition;
        RefreshView();
    }

    private void BuyPerk(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null || HideoutRuntimeSession.IsPerkUnlocked(perkDefinition))
            return;

        if (!IsTierRequirementFulfilled(perkDefinition.Tier))
            return;

        if (!HideoutRuntimeSession.TrySpendPerkPoints(perkDefinition.Cost))
            return;

        if (!HideoutRuntimeSession.UnlockPerk(perkDefinition))
        {
            HideoutRuntimeSession.AddPerkPoints(perkDefinition.Cost);
            return;
        }

        RefreshView();
    }

    private void EquipPerk(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null || !HideoutRuntimeSession.IsPerkUnlocked(perkDefinition))
            return;

        if (!TryAddWorkingEquippedPerk(perkDefinition, enforceCapacity: true))
            return;

        selectedPerk = perkDefinition;
        RefreshView();
    }

    private void UnequipPerk(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null)
            return;

        string perkId = perkDefinition.PerkId;
        for (int i = workingEquippedPerks.Count - 1; i >= 0; i--)
        {
            HideoutPerkDefinition equippedPerk = workingEquippedPerks[i];
            if (equippedPerk == null || !string.Equals(equippedPerk.PerkId, perkId, StringComparison.OrdinalIgnoreCase))
                continue;

            workingEquippedPerks.RemoveAt(i);
        }

        selectedPerk = perkDefinition;
        RefreshView();
    }

    private bool TryAddWorkingEquippedPerk(HideoutPerkDefinition perkDefinition, bool enforceCapacity)
    {
        perkDefinition = ResolveConfiguredPerk(perkDefinition);
        if (perkDefinition == null)
            return false;

        if (IsPerkEquipped(perkDefinition))
            return false;

        if (enforceCapacity && workingEquippedPerks.Count >= maxEquippedPerks)
            return false;

        workingEquippedPerks.Add(perkDefinition);
        return true;
    }

    private HideoutPerkDefinition ResolveConfiguredPerk(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null || string.IsNullOrWhiteSpace(perkDefinition.PerkId))
            return null;

        for (int i = 0; i < configuredPerks.Count; i++)
        {
            HideoutPerkDefinition configuredPerk = configuredPerks[i];
            if (configuredPerk != null &&
                string.Equals(configuredPerk.PerkId, perkDefinition.PerkId, StringComparison.OrdinalIgnoreCase))
            {
                return configuredPerk;
            }
        }

        return null;
    }

    private bool IsPerkEquipped(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null)
            return false;

        string perkId = perkDefinition.PerkId;
        for (int i = 0; i < workingEquippedPerks.Count; i++)
        {
            HideoutPerkDefinition equippedPerk = workingEquippedPerks[i];
            if (equippedPerk != null && string.Equals(equippedPerk.PerkId, perkId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ConfirmSelection()
    {
        PlayerPerkRuntimeLoadout loadout = new();
        loadout.SetPerks(workingEquippedPerks);
        PlayerPerkRuntimeSession.SetEquippedPerks(loadout);
        RefreshView();
    }

    private void RefreshConfirmButton()
    {
        if (confirmButton != null)
            confirmButton.interactable = true;
    }

    private bool IsTierRequirementFulfilled(HideoutPerkTier perkTier)
    {
        return perkTier switch
        {
            HideoutPerkTier.TierI => true,
            HideoutPerkTier.TierII => CountUnlockedPerksInTier(HideoutPerkTier.TierI) >= 3,
            HideoutPerkTier.TierIII => CountUnlockedPerksInTier(HideoutPerkTier.TierII) >= 2,
            _ => false
        };
    }

    private int CountUnlockedPerksInTier(HideoutPerkTier perkTier)
    {
        int count = 0;
        for (int i = 0; i < configuredPerks.Count; i++)
        {
            HideoutPerkDefinition perkDefinition = configuredPerks[i];
            if (perkDefinition == null || perkDefinition.Tier != perkTier || !HideoutRuntimeSession.IsPerkUnlocked(perkDefinition))
                continue;

            count++;
        }

        return count;
    }

    private static string BuildPerkDescription(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null)
            return string.Empty;

        bool hasDescription = !string.IsNullOrWhiteSpace(perkDefinition.Description);
        bool hasEffect = !string.IsNullOrWhiteSpace(perkDefinition.Effect);
        if (hasDescription && hasEffect)
            return $"{perkDefinition.Description}\n{perkDefinition.Effect}";

        if (hasDescription)
            return perkDefinition.Description;

        return hasEffect ? perkDefinition.Effect : string.Empty;
    }

    private static string ResolveTierText(HideoutPerkTier perkTier)
    {
        return perkTier switch
        {
            HideoutPerkTier.TierI => "I",
            HideoutPerkTier.TierII => "II",
            HideoutPerkTier.TierIII => "III",
            _ => string.Empty
        };
    }

    private static int ComparePerks(HideoutPerkDefinition left, HideoutPerkDefinition right)
    {
        int tierComparison = (left != null ? (int)left.Tier : 0).CompareTo(right != null ? (int)right.Tier : 0);
        if (tierComparison != 0)
            return tierComparison;

        return string.Compare(
            left != null ? left.PerkName : string.Empty,
            right != null ? right.PerkName : string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Transform ResolvePreservedTemplate(RectTransform contentRoot, Transform templateTransform)
    {
        if (contentRoot == null || templateTransform == null)
            return null;

        return templateTransform.parent == contentRoot ? templateTransform : null;
    }

    private static void ClearGeneratedChildren(RectTransform contentRoot, Transform preservedTemplate)
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (child == preservedTemplate)
                continue;

            UnityEngine.Object.Destroy(child.gameObject);
        }
    }

    private static void SetOptionalTextState(TMP_Text textField, bool visible, string value)
    {
        if (textField == null)
            return;

        textField.gameObject.SetActive(visible);
        if (visible)
            textField.text = value ?? string.Empty;
    }

    private static void SetText(TMP_Text textField, string value)
    {
        if (textField != null)
            textField.text = value ?? string.Empty;
    }

    private void SetTierLockState(TierSectionReferences tierSection, bool isLocked)
    {
        if (tierSection == null)
            return;

        SetActive(tierSection.titlePadlockObject, isLocked);
        if (tierSection.backgroundGraphic == null)
            return;

        if (!tierSection.hasCachedBackgroundColor)
            CacheTierBackgroundDefault(tierSection);

        tierSection.backgroundGraphic.color = isLocked
            ? lockedTierBackgroundTint
            : tierSection.defaultBackgroundColor;
    }

    private static void SetImage(Image imageField, Sprite sprite)
    {
        if (imageField == null)
            return;

        imageField.sprite = sprite;
        imageField.enabled = sprite != null;
    }

    private static EquipmentContextUiSettings ResolveUiSettings()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.EquipmentContextUi
            : new EquipmentContextUiSettings();
    }

    private static void SetActive(GameObject target, bool visible)
    {
        if (target != null)
            target.SetActive(visible);
    }
}

}
