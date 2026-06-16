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

    [FoldoutGroup("References"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<TMP_Text> perkPointsTexts = new();

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

    [FoldoutGroup("Selection")]
    [SerializeField] private Button closeButton;

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

    public event Action Confirmed;
    public event Action CloseRequested;
    public event Action PerkPointsChanged;

    /// <summary>
    /// Initializes perk configuration and draws the first panel state.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
        RefreshView();
    }

    /// <summary>
    /// Rebuilds panel state whenever the perks panel becomes active.
    /// </summary>
    private void OnEnable()
    {
        EnsureInitialized();
        RefreshView();
    }

    /// <summary>
    /// Collects runtime data, wires callbacks, and restores the current equipped selection once.
    /// </summary>
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

    /// <summary>
    /// Connects the confirm and close buttons to the panel actions.
    /// </summary>
    private void BindButtons()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelection);
            confirmButton.onClick.AddListener(ConfirmSelection);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleCloseRequested);
            closeButton.onClick.AddListener(HandleCloseRequested);
        }
    }

    /// <summary>
    /// Builds the configured perk list from explicit references or Resources and deduplicates by perk id.
    /// </summary>
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
        HideoutRuntimeSession.SyncPerkTierUnlocks(configuredPerks);
    }

    /// <summary>
    /// Adds one valid perk definition into the configured runtime list when it has not already been seen.
    /// </summary>
    private void TryAddConfiguredPerk(HideoutPerkDefinition perkDefinition, HashSet<string> addedPerkIds)
    {
        if (perkDefinition == null || addedPerkIds == null || string.IsNullOrWhiteSpace(perkDefinition.PerkId))
            return;

        if (!addedPerkIds.Add(perkDefinition.PerkId))
            return;

        configuredPerks.Add(perkDefinition);
    }

    /// <summary>
    /// Hides the template item instances so only runtime clones remain visible.
    /// </summary>
    private void PrepareTemplates()
    {
        if (availablePerkItemPrefab != null)
            availablePerkItemPrefab.gameObject.SetActive(false);

        if (selectedPerkItemPrefab != null)
            selectedPerkItemPrefab.gameObject.SetActive(false);
    }

    /// <summary>
    /// Applies fallback tier titles when the scene text fields are still empty.
    /// </summary>
    private void ConfigureTierTitles()
    {
        ConfigureTierTitle(tierOneSection, "Tier I");
        ConfigureTierTitle(tierTwoSection, "Tier II");
        ConfigureTierTitle(tierThreeSection, "Tier III");
    }

    /// <summary>
    /// Caches the default background color for each tier section before lock tinting is applied.
    /// </summary>
    private void CacheTierBackgroundDefaults()
    {
        CacheTierBackgroundDefault(tierOneSection);
        CacheTierBackgroundDefault(tierTwoSection);
        CacheTierBackgroundDefault(tierThreeSection);
    }

    /// <summary>
    /// Stores a tier section's untinted background color for later lock-state restoration.
    /// </summary>
    private static void CacheTierBackgroundDefault(TierSectionReferences tierSection)
    {
        if (tierSection == null || tierSection.backgroundGraphic == null)
            return;

        tierSection.defaultBackgroundColor = tierSection.backgroundGraphic.color;
        tierSection.hasCachedBackgroundColor = true;
    }

    /// <summary>
    /// Fills a tier title with a fallback string when no custom localized text has been authored yet.
    /// </summary>
    private static void ConfigureTierTitle(TierSectionReferences tierSection, string fallbackTitle)
    {
        if (tierSection == null || tierSection.titleText == null)
            return;

        if (string.IsNullOrWhiteSpace(tierSection.titleText.text))
            tierSection.titleText.text = fallbackTitle;
    }

    /// <summary>
    /// Synchronizes the working equipped list from the runtime session, restoring from save when runtime data is still empty.
    /// </summary>
    private void SyncWorkingSelectionFromConfirmed()
    {
        workingEquippedPerks.Clear();
        PlayerPerkRuntimeLoadout confirmedLoadout = HideoutPerkLoadoutPersistence.GetOrRestoreRuntimeLoadout(configuredPerks);
        IReadOnlyList<HideoutPerkDefinition> confirmedPerks = confirmedLoadout.EquippedPerks;
        for (int i = 0; i < confirmedPerks.Count; i++)
            TryAddWorkingEquippedPerk(ResolveConfiguredPerk(confirmedPerks[i]), enforceCapacity: true);
    }

    /// <summary>
    /// Keeps the current selection pointing at a configured perk whenever possible.
    /// </summary>
    private void EnsureValidSelection()
    {
        if (selectedPerk != null && configuredPerks.Contains(selectedPerk))
            return;

        selectedPerk = configuredPerks.Count > 0 ? configuredPerks[0] : null;
    }

    /// <summary>
    /// Rebuilds perk selection, details, tier locks, and all configured perk-point labels.
    /// </summary>
    public void RefreshView()
    {
        if (!initialized)
            EnsureInitialized();

        EnsureValidSelection();

        if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            titleText.text = "Perks";

        HideoutResourceTextUtility.SetTexts(
            perkPointsText,
            perkPointsTexts,
            HideoutResourceTextUtility.FormatPerkPoints(HideoutRuntimeSession.PerkPoints));
        RefreshTierLocks();
        RefreshSelectedPerkDetails();
        RebuildTierLists();
        RebuildSelectedPerksList();
        RefreshConfirmButton();

        bool showEmptyState = configuredPerks.Count == 0;
        string emptyMessage = showEmptyState ? "No perks configured." : string.Empty;
        SetOptionalTextState(emptyStateText, showEmptyState, emptyMessage);
    }

    /// <summary>
    /// Updates padlocks and tier background tinting based on the player's current unlock requirements.
    /// </summary>
    private void RefreshTierLocks()
    {
        SetTierLockState(tierOneSection, false);
        SetTierLockState(tierTwoSection, !IsTierRequirementFulfilled(HideoutPerkTier.TierII));
        SetTierLockState(tierThreeSection, !IsTierRequirementFulfilled(HideoutPerkTier.TierIII));
    }

    /// <summary>
    /// Refreshes the currently selected perk detail panel.
    /// </summary>
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

    /// <summary>
    /// Rebuilds the available perk item views for every tier section.
    /// </summary>
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

    /// <summary>
    /// Rebuilds the equipped perk strip using the current working selection.
    /// </summary>
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

    /// <summary>
    /// Clears runtime clones from one tier content root while preserving the hidden template object.
    /// </summary>
    private void ClearTierSection(TierSectionReferences tierSection)
    {
        if (tierSection == null)
            return;

        Transform preservedTemplate = ResolvePreservedTemplate(
            tierSection.contentRoot,
            availablePerkItemPrefab != null ? availablePerkItemPrefab.transform : null);
        ClearGeneratedChildren(tierSection.contentRoot, preservedTemplate);
    }

    /// <summary>
    /// Resolves the content transform that corresponds to the supplied perk tier.
    /// </summary>
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

    /// <summary>
    /// Sets the active selected perk for the detail panel.
    /// </summary>
    private void SelectPerk(HideoutPerkDefinition perkDefinition)
    {
        selectedPerk = perkDefinition;
        RefreshView();
    }

    /// <summary>
    /// Unlocks a perk when the player can afford it and the tier requirement has been met.
    /// </summary>
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

        PerkPointsChanged?.Invoke();
        RefreshView();
    }

    /// <summary>
    /// Equips one unlocked perk, then commits the new selection to runtime and save data immediately.
    /// </summary>
    private void EquipPerk(HideoutPerkDefinition perkDefinition)
    {
        if (perkDefinition == null || !HideoutRuntimeSession.IsPerkUnlocked(perkDefinition))
            return;

        if (!TryAddWorkingEquippedPerk(perkDefinition, enforceCapacity: true))
            return;

        selectedPerk = perkDefinition;
        CommitWorkingSelection();
    }

    /// <summary>
    /// Unequips one perk from the working selection and persists the updated runtime loadout immediately.
    /// </summary>
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
        CommitWorkingSelection();
    }

    /// <summary>
    /// Tries to add a perk into the working equipped list while respecting duplicates and optional capacity limits.
    /// </summary>
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

    /// <summary>
    /// Resolves an arbitrary perk instance back to the configured scene/runtime instance by stable perk id.
    /// </summary>
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

    /// <summary>
    /// Returns whether the supplied perk is already present in the working equipped list.
    /// </summary>
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

    /// <summary>
    /// Commits the working selection and then notifies listeners that the player confirmed the panel.
    /// </summary>
    private void ConfirmSelection()
    {
        CommitWorkingSelection();
        Confirmed?.Invoke();
    }

    /// <summary>
    /// Clears every equipped perk from both runtime and save data when the panel is forcibly closed or reset.
    /// </summary>
    public void ClearWorkingSelectionAndRuntime()
    {
        if (!initialized)
            EnsureInitialized();

        workingEquippedPerks.Clear();
        selectedPerk = configuredPerks.Count > 0 ? configuredPerks[0] : null;
        CommitWorkingSelection();
    }

    /// <summary>
    /// Raises the panel close request so the hideout controller can play the proper fade transition.
    /// </summary>
    private void HandleCloseRequested()
    {
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Applies the current working perk selection to runtime state, persists it, and redraws the panel.
    /// </summary>
    private void CommitWorkingSelection()
    {
        PlayerPerkRuntimeLoadout loadout = new();
        loadout.SetPerks(workingEquippedPerks);
        PlayerPerkRuntimeSession.SetEquippedPerks(loadout);
        HideoutPerkLoadoutPersistence.SaveEquippedPerks(workingEquippedPerks);
        RefreshView();
    }

    /// <summary>
    /// Keeps the confirm button usable because equip persistence now occurs immediately.
    /// </summary>
    private void RefreshConfirmButton()
    {
        if (confirmButton != null)
            confirmButton.interactable = true;
    }

    /// <summary>
    /// Reports whether the player has met the unlock requirement for the supplied perk tier.
    /// </summary>
    private bool IsTierRequirementFulfilled(HideoutPerkTier perkTier)
    {
        return perkTier switch
        {
            HideoutPerkTier.TierI => true,
            HideoutPerkTier.TierII => HideoutRuntimeSession.IsPerkTierUnlocked(HideoutPerkTier.TierII),
            HideoutPerkTier.TierIII => HideoutRuntimeSession.IsPerkTierUnlocked(HideoutPerkTier.TierIII),
            _ => false
        };
    }

    /// <summary>
    /// Builds the combined description and effect text block shown in the perk details area.
    /// </summary>
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

    /// <summary>
    /// Converts the tier enum into the roman numeral text used by the UI.
    /// </summary>
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

    /// <summary>
    /// Sorts perks by tier first and then alphabetically by perk name.
    /// </summary>
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

    /// <summary>
    /// Resolves the hidden template child that should survive list-clearing rebuilds.
    /// </summary>
    private static Transform ResolvePreservedTemplate(RectTransform contentRoot, Transform templateTransform)
    {
        if (contentRoot == null || templateTransform == null)
            return null;

        return templateTransform.parent == contentRoot ? templateTransform : null;
    }

    /// <summary>
    /// Destroys generated item views under a content root while preserving an optional template transform.
    /// </summary>
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

    /// <summary>
    /// Shows or hides an optional text field and updates its visible value.
    /// </summary>
    private static void SetOptionalTextState(TMP_Text textField, bool visible, string value)
    {
        if (textField == null)
            return;

        textField.gameObject.SetActive(visible);
        if (visible)
            textField.text = value ?? string.Empty;
    }

    /// <summary>
    /// Assigns plain string content to a text field when it exists.
    /// </summary>
    private static void SetText(TMP_Text textField, string value)
    {
        if (textField != null)
            textField.text = value ?? string.Empty;
    }

    /// <summary>
    /// Applies the locked or unlocked visual state for one tier section.
    /// </summary>
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

    /// <summary>
    /// Sets an image sprite and hides the image component when no sprite is available.
    /// </summary>
    private static void SetImage(Image imageField, Sprite sprite)
    {
        if (imageField == null)
            return;

        imageField.sprite = sprite;
        imageField.enabled = sprite != null;
    }

    /// <summary>
    /// Resolves the configured global UI text bundle used by perk detail strings.
    /// </summary>
    private static EquipmentContextUiSettings ResolveUiSettings()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.EquipmentContextUi
            : new EquipmentContextUiSettings();
    }

    /// <summary>
    /// Toggles a GameObject only when the reference exists.
    /// </summary>
    private static void SetActive(GameObject target, bool visible)
    {
        if (target != null)
            target.SetActive(visible);
    }
}

}
