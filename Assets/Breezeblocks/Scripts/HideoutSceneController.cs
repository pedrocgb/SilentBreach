using System;
using System.Collections.Generic;
using Breezeblocks;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.HideoutSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Hideout/Hideout Scene Controller")]
public sealed class HideoutSceneController : MonoBehaviour
{
    private enum HideoutView
    {
        MainMenu,
        Jobs,
        Fence,
        Perks,
        Contacts
    }

    private enum DetailSelectionSource
    {
        None,
        ShopOffer,
        Equipment
    }

    [Serializable]
    private sealed class HeaderReferences
    {
        public TMP_Text titleText;
        public TMP_Text cashText;
        public TMP_Text influenceText;
        public TMP_Text messageText;
    }

    [Serializable]
    private sealed class MainMenuReferences
    {
        public GameObject root;
    }

    [Serializable]
    private sealed class JobsPanelReferences
    {
        public GameObject root;
        public RectTransform jobListContent;
        public HideoutJobListItemUI jobListItemPrefab;
        public TMP_Text emptyStateText;
        public TMP_Text jobNameText;
        public TMP_Text jobLevelText;
        public TMP_Text jobDescriptionText;
        public TMP_Text jobRewardText;
        public TMP_Text jobObjectivesText;
        public TMP_Text jobFailureText;
        public TMP_Text jobFixerText;
        public Image jobImage;
        public Button proceedButton;
    }

    [Serializable]
    private sealed class FencePanelReferences
    {
        public GameObject root;
        public TMP_Text shopTitleText;
        public TMP_Text shopDescriptionText;
        public Image shopImage;
        public RectTransform shopListContent;
        public HideoutFenceOfferItemUI shopOfferItemPrefab;
        public TMP_Text emptyStateText;
        public TMP_Text detailTitleText;
        public TMP_Text detailSourceText;
        public TMP_Text detailDescriptionText;
        public TMP_Text detailStatsText;
        public TMP_Text detailValueText;
        public Image detailImage;
        public TMP_Text sellValueText;
        public Button sellButton;
        public Button startQuestButton;

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<PlayerEquipmentSlotViewUI> slotViews = new();
    }

    [Serializable]
    private sealed class PlaceholderPanelReferences
    {
        public GameObject root;
        public TMP_Text titleText;
        public TMP_Text bodyText;
    }

    private sealed class PreparedFenceOffer
    {
        public HideoutFenceOfferDefinition Definition;
        public EquipmentItemData Item;
        public ProjectileData Projectile;
        public int Price;
        public int InitialQuantity;
        public int RemainingQuantity;
    }

    private sealed class LoadoutSlotState
    {
        public EquipmentSlotType SlotType;
        public EquipmentItemData Item;
        public ProjectileData Projectile;
        public int LoadedAmmo;
        public int ReserveAmmo;
        public int PurchasePrice;
        public PreparedFenceOffer SourceOffer;

        public bool HasItem => Item != null;
    }

    [FoldoutGroup("Defaults"), MinValue(0)]
    [SerializeField] private int startingCash = 1800;

    [FoldoutGroup("Defaults"), MinValue(0)]
    [SerializeField] private int startingInfluencePoints = 3;

    [FoldoutGroup("Defaults")]
    [SerializeField] private string resourcesSearchPath = string.Empty;

    [FoldoutGroup("References")]
    [SerializeField] private HeaderReferences header = new();

    [FoldoutGroup("References")]
    [SerializeField] private MainMenuReferences mainMenu = new();

    [FoldoutGroup("References")]
    [SerializeField] private JobsPanelReferences jobsPanel = new();

    [FoldoutGroup("References")]
    [SerializeField] private FencePanelReferences fencePanel = new();

    [FoldoutGroup("References")]
    [SerializeField] private PlaceholderPanelReferences perksPanel = new();

    [FoldoutGroup("References")]
    [SerializeField] private PlaceholderPanelReferences contactsPanel = new();

    private readonly List<HideoutJobDefinition> availableJobs = new();
    private readonly List<PreparedFenceOffer> activeFenceOffers = new();
    private readonly Dictionary<EquipmentSlotType, LoadoutSlotState> loadoutSlots = new();
    private readonly Dictionary<EquipmentSlotType, PlayerEquipmentSlotViewUI> equipmentSlotViews = new();

    private HideoutView currentView;
    private HideoutJobDefinition selectedJob;
    private PreparedFenceOffer selectedOffer;
    private EquipmentSlotType selectedEquipmentSlot = EquipmentSlotType.None;
    private DetailSelectionSource detailSelectionSource = DetailSelectionSource.None;
    private int totalConfiguredJobs;

    private void Awake()
    {
        HideoutRuntimeSession.EnsureInitialized(startingCash, startingInfluencePoints);
        InitializeLoadoutSlots();
        CacheSlotViews();
        PrepareTemplates();
        BindSlotEvents();
        ConfigurePlaceholderPanels();
        LoadAvailableJobs();

        selectedJob = ResolveInitialSelectedJob();

        RebuildJobList();
        RefreshJobDetails();
        RefreshCurrencyTexts();
        RefreshEquipmentSlots();
        RefreshDetailPanel();
        ShowView(HideoutView.MainMenu);

        if (HideoutRuntimeSession.TryConsumePendingHideoutMessage(out string pendingMessage))
            SetMessage(pendingMessage);
        else
        {
            SetMessage(availableJobs.Count > 0
                ? "Hideout scene is now fully manual. Wire your own buttons and layout in the editor."
                : totalConfiguredJobs > 0
                    ? "No jobs are currently available."
                    : "No hideout jobs were found in Resources. Create a Hideout Job asset to populate this screen.");
        }
    }

    private void OnDestroy()
    {
        UnbindSlotEvents();
    }

    public void ShowMainMenu()
    {
        ShowView(HideoutView.MainMenu);
    }

    public void ShowJobsView()
    {
        ShowView(HideoutView.Jobs);
    }

    public void ShowPerksView()
    {
        ShowView(HideoutView.Perks);
    }

    public void ShowContactsView()
    {
        ShowView(HideoutView.Contacts);
    }

    public void OpenSelectedJobFence()
    {
        if (selectedJob == null)
        {
            SetMessage("Select a job before heading to the fence.");
            return;
        }

        ResetPreparedLoadout();
        GenerateFenceInventory(selectedJob);
        HideoutRuntimeSession.SetCurrentJob(selectedJob);
        SelectInitialFenceDetail();
        ShowView(HideoutView.Fence);
        RefreshFenceView();
        SetMessage($"The fence laid out a fresh spread for {selectedJob.JobTitle}.");
    }

    public void BackOutOfFence()
    {
        int refund = RefundAllPurchasedItems();
        ShowView(HideoutView.Jobs);
        SetMessage(refund > 0
            ? $"Fence closed. Refunded ${refund} and cleared the prep loadout."
            : "Fence closed. No purchases needed to be refunded.");
    }

    public void SellSelectedEquipment()
    {
        LoadoutSlotState slotState = GetSlotState(selectedEquipmentSlot);
        if (slotState == null || !slotState.HasItem)
        {
            SetMessage("Select an equipped item before selling.");
            return;
        }

        int refund = slotState.PurchasePrice;
        string itemName = slotState.Item.DisplayName;
        PreparedFenceOffer restoredOffer = RestockPurchasedOffer(slotState);
        ClearSlot(slotState);
        HideoutRuntimeSession.AddCash(refund);

        if (restoredOffer != null)
            SelectOffer(restoredOffer);
        else
            SelectEquipmentSlot(selectedEquipmentSlot);

        SetMessage($"Sold {itemName} for ${refund}.");
    }

    public void StartQuest()
    {
        if (selectedJob == null || !SceneLoadUtility.CanLoadScene(selectedJob.MissionSceneBuildIndex, selectedJob.MissionSceneName))
        {
            SetMessage("This job does not have a quest scene configured yet.");
            return;
        }

        PlayerEquipmentRuntimeSession.SetPendingQuestLoadout(BuildRuntimeLoadout());
        HideoutRuntimeSession.SetCurrentJob(selectedJob);
        Time.timeScale = 1f;
        SceneLoadUtility.TryLoadScene(selectedJob.MissionSceneBuildIndex, selectedJob.MissionSceneName);
    }

    private void InitializeLoadoutSlots()
    {
        loadoutSlots.Clear();
        loadoutSlots[EquipmentSlotType.Primary] = new LoadoutSlotState { SlotType = EquipmentSlotType.Primary };
        loadoutSlots[EquipmentSlotType.Secondary] = new LoadoutSlotState { SlotType = EquipmentSlotType.Secondary };
        loadoutSlots[EquipmentSlotType.Belt] = new LoadoutSlotState { SlotType = EquipmentSlotType.Belt };
        loadoutSlots[EquipmentSlotType.Armor] = new LoadoutSlotState { SlotType = EquipmentSlotType.Armor };
    }

    private void CacheSlotViews()
    {
        equipmentSlotViews.Clear();

        for (int i = 0; i < fencePanel.slotViews.Count; i++)
        {
            PlayerEquipmentSlotViewUI slotView = fencePanel.slotViews[i];
            if (slotView == null || slotView.SlotType == EquipmentSlotType.None)
                continue;

            equipmentSlotViews[slotView.SlotType] = slotView;
        }
    }

    private void BindSlotEvents()
    {
        foreach (PlayerEquipmentSlotViewUI slotView in equipmentSlotViews.Values)
        {
            if (slotView == null)
                continue;

            slotView.SetDragAndDropEnabled(slotView.SlotType != EquipmentSlotType.None);
            slotView.Clicked -= HandleSlotClicked;
            slotView.DropReceived -= HandleSlotDrop;
            slotView.Clicked += HandleSlotClicked;
            slotView.DropReceived += HandleSlotDrop;
        }
    }

    private void UnbindSlotEvents()
    {
        foreach (PlayerEquipmentSlotViewUI slotView in equipmentSlotViews.Values)
        {
            if (slotView == null)
                continue;

            slotView.Clicked -= HandleSlotClicked;
            slotView.DropReceived -= HandleSlotDrop;
        }
    }

    private void PrepareTemplates()
    {
        if (jobsPanel.jobListItemPrefab != null)
            jobsPanel.jobListItemPrefab.gameObject.SetActive(false);

        if (fencePanel.shopOfferItemPrefab != null)
            fencePanel.shopOfferItemPrefab.gameObject.SetActive(false);
    }

    private void ConfigurePlaceholderPanels()
    {
        ConfigurePlaceholderPanel(perksPanel, "Perks");
        ConfigurePlaceholderPanel(contactsPanel, "Contacts");
    }

    private static void ConfigurePlaceholderPanel(PlaceholderPanelReferences panel, string title)
    {
        if (panel == null)
            return;

        if (panel.titleText != null && string.IsNullOrWhiteSpace(panel.titleText.text))
            panel.titleText.text = title;

        if (panel.bodyText != null && string.IsNullOrWhiteSpace(panel.bodyText.text))
            panel.bodyText.text = "Coming soon";
    }

    private void LoadAvailableJobs()
    {
        availableJobs.Clear();
        HideoutJobDefinition[] jobs = Resources.LoadAll<HideoutJobDefinition>(resourcesSearchPath ?? string.Empty);
        totalConfiguredJobs = 0;
        HashSet<string> lockedJobIds = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < jobs.Length; i++)
        {
            HideoutJobDefinition job = jobs[i];
            if (job == null)
                continue;

            totalConfiguredJobs++;
            if (job.UnlockJobs == null)
                continue;

            for (int unlockIndex = 0; unlockIndex < job.UnlockJobs.Count; unlockIndex++)
            {
                HideoutJobDefinition unlockJob = job.UnlockJobs[unlockIndex];
                if (unlockJob == null || string.IsNullOrWhiteSpace(unlockJob.JobId))
                    continue;

                lockedJobIds.Add(unlockJob.JobId);
            }
        }

        Array.Sort(jobs, (left, right) => string.Compare(left != null ? left.JobTitle : string.Empty, right != null ? right.JobTitle : string.Empty, StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < jobs.Length; i++)
        {
            HideoutJobDefinition job = jobs[i];
            if (job == null || HideoutRuntimeSession.IsJobCompleted(job))
                continue;

            bool isBaseJob = !lockedJobIds.Contains(job.JobId);
            if (!isBaseJob && !HideoutRuntimeSession.IsJobUnlocked(job))
                continue;

            availableJobs.Add(job);
        }
    }

    private HideoutJobDefinition ResolveInitialSelectedJob()
    {
        HideoutJobDefinition runtimeJob = HideoutRuntimeSession.CurrentJob;
        if (runtimeJob != null && availableJobs.Contains(runtimeJob))
            return runtimeJob;

        return availableJobs.Count > 0 ? availableJobs[0] : null;
    }

    private void RebuildJobList()
    {
        Transform preservedTemplate = ResolvePreservedTemplate(jobsPanel.jobListContent, jobsPanel.jobListItemPrefab != null ? jobsPanel.jobListItemPrefab.transform : null);
        ClearGeneratedChildren(jobsPanel.jobListContent, preservedTemplate);

        if (availableJobs.Count == 0)
        {
            SetOptionalTextState(jobsPanel.emptyStateText, true, "No jobs configured.");
            return;
        }

        if (jobsPanel.jobListItemPrefab == null || jobsPanel.jobListContent == null)
        {
            SetOptionalTextState(jobsPanel.emptyStateText, true, "Assign a job list item prefab and content root.");
            return;
        }

        SetOptionalTextState(jobsPanel.emptyStateText, false, null);

        for (int i = 0; i < availableJobs.Count; i++)
        {
            HideoutJobDefinition job = availableJobs[i];
            HideoutJobListItemUI itemView = Instantiate(jobsPanel.jobListItemPrefab, jobsPanel.jobListContent);
            itemView.gameObject.name = $"{job.JobTitle} Row";
            itemView.gameObject.SetActive(true);
            itemView.Bind(
                job.JobTitle,
                BuildJobSubtitle(job),
                job == selectedJob,
                () => SelectJob(job));
        }
    }

    private void RebuildFenceOfferList()
    {
        Transform preservedTemplate = ResolvePreservedTemplate(fencePanel.shopListContent, fencePanel.shopOfferItemPrefab != null ? fencePanel.shopOfferItemPrefab.transform : null);
        ClearGeneratedChildren(fencePanel.shopListContent, preservedTemplate);

        if (activeFenceOffers.Count == 0)
        {
            SetOptionalTextState(fencePanel.emptyStateText, true, "This fence has nothing on the table for this contract.");
            return;
        }

        if (fencePanel.shopOfferItemPrefab == null || fencePanel.shopListContent == null)
        {
            SetOptionalTextState(fencePanel.emptyStateText, true, "Assign a fence offer prefab and content root.");
            return;
        }

        SetOptionalTextState(fencePanel.emptyStateText, false, null);

        for (int i = 0; i < activeFenceOffers.Count; i++)
        {
            PreparedFenceOffer offer = activeFenceOffers[i];
            HideoutFenceOfferItemUI itemView = Instantiate(fencePanel.shopOfferItemPrefab, fencePanel.shopListContent);
            itemView.gameObject.name = $"{offer.Item.DisplayName} Offer";
            itemView.gameObject.SetActive(true);
            itemView.Bind(
                offer.Item.DisplayName,
                $"${offer.Price} | Qty {offer.RemainingQuantity}",
                FormatAllowedSlots(offer.Item.AllowedSlots),
                offer.Item.Icon,
                detailSelectionSource == DetailSelectionSource.ShopOffer && selectedOffer == offer,
                offer.RemainingQuantity > 0,
                () => SelectOffer(offer),
                () => TryBuyOffer(offer));
        }
    }

    private void SelectJob(HideoutJobDefinition job)
    {
        selectedJob = job;
        RefreshJobDetails();
        RebuildJobList();
    }

    private void RefreshJobDetails()
    {
        EquipmentContextUiSettings uiSettings = GlobalSettings.Instance != null
            ? GlobalSettings.Instance.EquipmentContextUi
            : new EquipmentContextUiSettings();

        if (selectedJob == null)
        {
            SetText(jobsPanel.jobNameText, "No Job Selected");
            SetText(jobsPanel.jobLevelText, string.Empty);
            SetText(jobsPanel.jobDescriptionText, "Select a contract to see the full brief.");
            SetText(jobsPanel.jobRewardText, string.Empty);
            SetText(jobsPanel.jobObjectivesText, string.Empty);
            SetText(jobsPanel.jobFailureText, string.Empty);
            SetText(jobsPanel.jobFixerText, string.Empty);
            SetImage(jobsPanel.jobImage, null);

            if (jobsPanel.proceedButton != null)
                jobsPanel.proceedButton.interactable = false;

            return;
        }

        SetText(jobsPanel.jobNameText, selectedJob.JobTitle);
        SetText(
            jobsPanel.jobLevelText,
            $"{uiSettings.JobLevelPrefix}{uiSettings.GetJobLevelText(selectedJob.JobLevel)}");
        SetText(jobsPanel.jobDescriptionText, selectedJob.JobDescription);
        SetText(jobsPanel.jobRewardText, selectedJob.RewardText);
        SetText(jobsPanel.jobObjectivesText, selectedJob.ObjectivesText);
        SetText(jobsPanel.jobFailureText, selectedJob.TermsOfFailureText);
        SetText(jobsPanel.jobFixerText, string.IsNullOrWhiteSpace(selectedJob.FixerName) ? string.Empty : $"Fixer: {selectedJob.FixerName}");
        SetImage(jobsPanel.jobImage, selectedJob.JobImage);

        if (jobsPanel.proceedButton != null)
            jobsPanel.proceedButton.interactable = true;
    }

    private void GenerateFenceInventory(HideoutJobDefinition job)
    {
        activeFenceOffers.Clear();
        if (job == null || job.FenceOffers == null)
            return;

        HideoutFenceOfferDefinition fallbackDefinition = null;

        for (int i = 0; i < job.FenceOffers.Count; i++)
        {
            HideoutFenceOfferDefinition definition = job.FenceOffers[i];
            if (definition == null || definition.Item == null)
                continue;

            if (fallbackDefinition == null || definition.AvailabilityProbability > fallbackDefinition.AvailabilityProbability)
                fallbackDefinition = definition;

            if (UnityEngine.Random.value > definition.AvailabilityProbability)
                continue;

            int quantity = definition.MaxQuantity > 1
                ? UnityEngine.Random.Range(1, definition.MaxQuantity + 1)
                : 1;

            activeFenceOffers.Add(new PreparedFenceOffer
            {
                Definition = definition,
                Item = definition.Item,
                Projectile = definition.FirearmProjectile,
                Price = definition.Price,
                InitialQuantity = Mathf.Max(1, quantity),
                RemainingQuantity = Mathf.Max(1, quantity)
            });
        }

        if (activeFenceOffers.Count == 0 && fallbackDefinition != null)
        {
            activeFenceOffers.Add(new PreparedFenceOffer
            {
                Definition = fallbackDefinition,
                Item = fallbackDefinition.Item,
                Projectile = fallbackDefinition.FirearmProjectile,
                Price = fallbackDefinition.Price,
                InitialQuantity = 1,
                RemainingQuantity = 1
            });
        }

        activeFenceOffers.Sort((left, right) => string.Compare(left.Item != null ? left.Item.DisplayName : string.Empty, right.Item != null ? right.Item.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshFenceView()
    {
        SetText(fencePanel.shopTitleText, selectedJob != null ? selectedJob.ShopTitle : "The Fence");
        SetText(fencePanel.shopDescriptionText, selectedJob != null ? selectedJob.ShopDescription : string.Empty);
        SetImage(fencePanel.shopImage, selectedJob != null ? selectedJob.ShopImage : null);
        RefreshCurrencyTexts();
        RebuildFenceOfferList();
        RefreshEquipmentSlots();
        RefreshDetailPanel();

        if (fencePanel.startQuestButton != null)
            fencePanel.startQuestButton.interactable = selectedJob != null && SceneLoadUtility.CanLoadScene(selectedJob.MissionSceneBuildIndex, selectedJob.MissionSceneName);
    }

    private void SelectInitialFenceDetail()
    {
        selectedEquipmentSlot = EquipmentSlotType.None;
        detailSelectionSource = DetailSelectionSource.None;
        selectedOffer = null;

        if (activeFenceOffers.Count > 0)
        {
            SelectOffer(activeFenceOffers[0]);
            return;
        }

        foreach (KeyValuePair<EquipmentSlotType, LoadoutSlotState> pair in loadoutSlots)
        {
            if (pair.Value.HasItem)
            {
                SelectEquipmentSlot(pair.Key);
                return;
            }
        }
    }

    private void SelectOffer(PreparedFenceOffer offer)
    {
        selectedOffer = offer;
        selectedEquipmentSlot = EquipmentSlotType.None;
        detailSelectionSource = offer != null ? DetailSelectionSource.ShopOffer : DetailSelectionSource.None;

        if (currentView == HideoutView.Fence)
            RefreshFenceView();
    }

    private void SelectEquipmentSlot(EquipmentSlotType slotType)
    {
        selectedEquipmentSlot = slotType;
        selectedOffer = null;
        detailSelectionSource = slotType != EquipmentSlotType.None ? DetailSelectionSource.Equipment : DetailSelectionSource.None;

        if (currentView == HideoutView.Fence)
            RefreshFenceView();
    }

    private void TryBuyOffer(PreparedFenceOffer offer)
    {
        if (offer == null || offer.Item == null)
        {
            SetMessage("This offer is no longer available.");
            return;
        }

        if (offer.RemainingQuantity <= 0)
        {
            SetMessage($"{offer.Item.DisplayName} is sold out.");
            return;
        }

        if (!HideoutRuntimeSession.TrySpendCash(offer.Price))
        {
            SetMessage("Not enough cash for that purchase.");
            return;
        }

        if (!TryPlacePurchasedItem(offer, out EquipmentSlotType slotType, out string failureMessage))
        {
            HideoutRuntimeSession.AddCash(offer.Price);
            SetMessage(failureMessage);
            return;
        }

        offer.RemainingQuantity--;
        SelectEquipmentSlot(slotType);
        RefreshFenceView();
        SetMessage($"Bought {offer.Item.DisplayName} and placed it in the {FormatSlot(slotType)} slot.");
    }

    private bool TryPlacePurchasedItem(PreparedFenceOffer offer, out EquipmentSlotType slotType, out string failureMessage)
    {
        slotType = EquipmentSlotType.None;
        failureMessage = "No compatible slot is available.";

        if (offer == null || offer.Item == null)
            return false;

        List<EquipmentSlotType> compatibleSlots = ResolveCompatibleSlots(offer.Item);
        for (int i = 0; i < compatibleSlots.Count; i++)
        {
            EquipmentSlotType candidateSlotType = compatibleSlots[i];
            LoadoutSlotState candidateSlot = GetSlotState(candidateSlotType);
            if (candidateSlot == null || candidateSlot.HasItem)
                continue;

            AssignSlot(
                candidateSlot,
                offer.Item,
                ResolveProjectileForItem(offer.Item, offer.Projectile),
                ResolveLoadedAmmo(offer.Item),
                ResolveReserveAmmo(offer.Item),
                offer.Price,
                offer);
            slotType = candidateSlotType;
            failureMessage = string.Empty;
            return true;
        }

        failureMessage = compatibleSlots.Count > 0
            ? $"The compatible slot for {offer.Item.DisplayName} is already occupied."
            : $"{offer.Item.DisplayName} cannot be equipped into any prep slot.";
        return false;
    }

    private int RefundAllPurchasedItems()
    {
        int refund = 0;

        foreach (KeyValuePair<EquipmentSlotType, LoadoutSlotState> pair in loadoutSlots)
        {
            LoadoutSlotState slotState = pair.Value;
            if (slotState == null || !slotState.HasItem)
                continue;

            refund += slotState.PurchasePrice;
            RestockPurchasedOffer(slotState);
            ClearSlot(slotState);
        }

        if (refund > 0)
            HideoutRuntimeSession.AddCash(refund);

        detailSelectionSource = DetailSelectionSource.None;
        selectedOffer = null;
        selectedEquipmentSlot = EquipmentSlotType.None;
        RefreshFenceView();
        return refund;
    }

    private void ResetPreparedLoadout()
    {
        foreach (KeyValuePair<EquipmentSlotType, LoadoutSlotState> pair in loadoutSlots)
            ClearSlot(pair.Value);

        detailSelectionSource = DetailSelectionSource.None;
        selectedOffer = null;
        selectedEquipmentSlot = EquipmentSlotType.None;
    }

    private void RefreshEquipmentSlots()
    {
        RefreshSingleSlot(EquipmentSlotType.Primary);
        RefreshSingleSlot(EquipmentSlotType.Secondary);
        RefreshSingleSlot(EquipmentSlotType.Belt);
        RefreshSingleSlot(EquipmentSlotType.Armor);
    }

    private void RefreshSingleSlot(EquipmentSlotType slotType)
    {
        if (!equipmentSlotViews.TryGetValue(slotType, out PlayerEquipmentSlotViewUI slotView) || slotView == null)
            return;

        LoadoutSlotState slotState = GetSlotState(slotType);
        bool isSelected = detailSelectionSource == DetailSelectionSource.Equipment && selectedEquipmentSlot == slotType;
        slotView.Refresh(slotState != null ? slotState.Item : null, isSelected, FormatSlot(slotType), ResolveHotkeyLabel(slotType));
    }

    private void RefreshDetailPanel()
    {
        if (detailSelectionSource == DetailSelectionSource.ShopOffer && selectedOffer != null)
        {
            PopulateItemDetail(
                selectedOffer.Item,
                $"Fence Offer | {selectedOffer.RemainingQuantity} remaining",
                selectedOffer.Price,
                ResolveLoadedAmmo(selectedOffer.Item),
                ResolveReserveAmmo(selectedOffer.Item),
                canSell: false);
            return;
        }

        if (detailSelectionSource == DetailSelectionSource.Equipment)
        {
            LoadoutSlotState slotState = GetSlotState(selectedEquipmentSlot);
            if (slotState != null && slotState.HasItem)
            {
                PopulateItemDetail(
                    slotState.Item,
                    $"Prepared Loadout | {FormatSlot(slotState.SlotType)} slot",
                    slotState.PurchasePrice,
                    slotState.LoadedAmmo,
                    slotState.ReserveAmmo,
                    canSell: true);
                return;
            }
        }

        SetText(fencePanel.detailTitleText, "Select an item");
        SetText(fencePanel.detailSourceText, "Choose a fence offer or click an equipped item.");
        SetText(fencePanel.detailDescriptionText, string.Empty);
        SetText(fencePanel.detailStatsText, string.Empty);
        SetText(fencePanel.detailValueText, string.Empty);
        SetText(fencePanel.sellValueText, "Sell Value: $0");
        SetImage(fencePanel.detailImage, selectedJob != null ? selectedJob.ShopImage : null);

        if (fencePanel.sellButton != null)
            fencePanel.sellButton.interactable = false;
    }

    private void PopulateItemDetail(EquipmentItemData item, string sourceText, int value, int loadedAmmo, int reserveAmmo, bool canSell)
    {
        SetText(fencePanel.detailTitleText, item != null ? item.DisplayName : "Select an item");
        SetText(fencePanel.detailSourceText, sourceText);
        SetText(fencePanel.detailDescriptionText, item != null ? item.Description : string.Empty);
        SetText(fencePanel.detailStatsText, item != null ? BuildItemStats(item, loadedAmmo, reserveAmmo) : string.Empty);
        SetText(fencePanel.detailValueText, $"Value: ${Mathf.Max(0, value)}");
        SetText(fencePanel.sellValueText, $"Sell Value: ${Mathf.Max(0, value)}");
        SetImage(fencePanel.detailImage, item != null ? item.Icon : null);

        if (fencePanel.sellButton != null)
            fencePanel.sellButton.interactable = canSell && item != null;
    }

    private string BuildItemStats(EquipmentItemData item, int loadedAmmo, int reserveAmmo)
    {
        if (item == null)
            return string.Empty;

        if (item is FirearmData firearmData)
        {
            return
                $"Class: {firearmData.Class}\n" +
                $"Slots: {FormatAllowedSlots(firearmData.AllowedSlots)}\n" +
                $"Fire Modes: {firearmData.Modes.ToString().Replace(", ", " / ")}\n" +
                $"Bullets: {loadedAmmo}/{reserveAmmo}\n" +
                $"Reload: {(firearmData.ReloadStyle == ReloadType.BulletPerBullet ? "Per bullet" : "Magazine")} | {firearmData.ReloadTime:0.##}s";
        }

        if (item is ArmorData armorData)
        {
            return
                $"Armor Class: {armorData.ArmorClass}\n" +
                $"Armor Value: {armorData.ArmorValue:0.##}\n" +
                $"Rotation Penalty: {armorData.RotationPenalty:0.#}%\n" +
                $"Movement Noise: +{armorData.MovementNoiseModifierPercent:0.#}%";
        }

        if (item is MeleeWeaponData meleeWeaponData)
        {
            return
                $"Grip: {meleeWeaponData.GripType}\n" +
                $"Slots: {FormatAllowedSlots(meleeWeaponData.AllowedSlots)}\n" +
                $"Damage: {meleeWeaponData.Damage:0.#}\n" +
                $"Attack: {meleeWeaponData.AttackAnimationDuration:0.##}s total | {meleeWeaponData.AttackSwingDuration:0.##}s swing\n" +
                $"Reach: {meleeWeaponData.AttackReachDistance:0.##}";
        }

        if (item is UtilityItemData utilityItemData)
        {
            if (item is ThrowableUtilityData throwableData)
            {
                return
                    $"Type: {utilityItemData.UtilityTypeName}\n" +
                    $"Slots: {FormatAllowedSlots(utilityItemData.AllowedSlots)}\n" +
                    $"Uses: {reserveAmmo}/{throwableData.MaxUses}\n" +
                    $"Throw Distance: {throwableData.MinTravelDistance:0.##}-{throwableData.MaxTravelDistance:0.##}\n" +
                    $"Equip: {utilityItemData.EquipTime:0.##}s | Holster: {utilityItemData.HolsterTime:0.##}s";
            }

            return
                $"Type: {utilityItemData.UtilityTypeName}\n" +
                $"Slots: {FormatAllowedSlots(utilityItemData.AllowedSlots)}\n" +
                $"Equip: {utilityItemData.EquipTime:0.##}s\n" +
                $"Holster: {utilityItemData.HolsterTime:0.##}s";
        }

        return $"Slots: {FormatAllowedSlots(item.AllowedSlots)}";
    }

    private PlayerEquipmentRuntimeLoadout BuildRuntimeLoadout()
    {
        PlayerEquipmentRuntimeLoadout loadout = new()
        {
            ArmorItem = GetSlotState(EquipmentSlotType.Armor)?.Item as ArmorData,
            HeldSlot = ResolveStartingHeldSlot()
        };

        AppendLoadoutSlot(loadout, EquipmentSlotType.Primary);
        AppendLoadoutSlot(loadout, EquipmentSlotType.Secondary);
        AppendLoadoutSlot(loadout, EquipmentSlotType.Belt);
        return loadout;
    }

    private void AppendLoadoutSlot(PlayerEquipmentRuntimeLoadout loadout, EquipmentSlotType slotType)
    {
        LoadoutSlotState slotState = GetSlotState(slotType);
        if (loadout == null || slotState == null || !slotState.HasItem)
            return;

        loadout.SetSlot(slotState.SlotType, slotState.Item, slotState.Projectile, slotState.LoadedAmmo, slotState.ReserveAmmo);
    }

    private EquipmentSlotType ResolveStartingHeldSlot()
    {
        if (detailSelectionSource == DetailSelectionSource.Equipment && selectedEquipmentSlot.IsHandSlot())
        {
            LoadoutSlotState selectedSlot = GetSlotState(selectedEquipmentSlot);
            if (selectedSlot != null && selectedSlot.HasItem)
                return selectedEquipmentSlot;
        }

        if (GetSlotState(EquipmentSlotType.Primary)?.HasItem == true)
            return EquipmentSlotType.Primary;

        if (GetSlotState(EquipmentSlotType.Secondary)?.HasItem == true)
            return EquipmentSlotType.Secondary;

        if (GetSlotState(EquipmentSlotType.Belt)?.HasItem == true)
            return EquipmentSlotType.Belt;

        return EquipmentSlotType.None;
    }

    private void ShowView(HideoutView view)
    {
        currentView = view;

        SetActive(mainMenu.root, view == HideoutView.MainMenu);
        SetActive(jobsPanel.root, view == HideoutView.Jobs);
        SetActive(fencePanel.root, view == HideoutView.Fence);
        SetActive(perksPanel.root, view == HideoutView.Perks);
        SetActive(contactsPanel.root, view == HideoutView.Contacts);

        string headerTitle = view switch
        {
            HideoutView.MainMenu => "Thieves Guild Hideout",
            HideoutView.Jobs => "Available Jobs",
            HideoutView.Fence => selectedJob != null ? $"{selectedJob.JobTitle} | The Fence" : "The Fence",
            HideoutView.Perks => "Perks",
            HideoutView.Contacts => "Contacts",
            _ => "Thieves Guild Hideout"
        };

        SetText(header.titleText, headerTitle);

        if (view == HideoutView.Jobs)
            RefreshJobDetails();

        if (view == HideoutView.Fence)
            RefreshFenceView();
    }

    private void HandleSlotClicked(PlayerEquipmentSlotViewUI slotView)
    {
        if (slotView == null)
            return;

        SelectEquipmentSlot(slotView.SlotType);
    }

    private void HandleSlotDrop(PlayerEquipmentSlotViewUI targetSlotView, PlayerEquipmentSlotViewUI sourceSlotView)
    {
        if (targetSlotView == null || sourceSlotView == null)
            return;

        if (!TryMoveLoadoutItem(sourceSlotView.SlotType, targetSlotView.SlotType, out string message))
        {
            SetMessage(message);
            return;
        }

        SelectEquipmentSlot(targetSlotView.SlotType);
        RefreshFenceView();
        SetMessage($"Moved equipment into the {FormatSlot(targetSlotView.SlotType)} slot.");
    }

    private bool TryMoveLoadoutItem(EquipmentSlotType fromSlotType, EquipmentSlotType toSlotType, out string message)
    {
        message = "That move is not allowed.";

        LoadoutSlotState fromSlot = GetSlotState(fromSlotType);
        LoadoutSlotState toSlot = GetSlotState(toSlotType);
        if (fromSlot == null || toSlot == null || !fromSlot.HasItem)
        {
            message = "There is no item in the source slot.";
            return false;
        }

        if (!fromSlot.Item.SupportsSlot(toSlotType))
        {
            message = $"{fromSlot.Item.DisplayName} cannot go into the {FormatSlot(toSlotType)} slot.";
            return false;
        }

        if (toSlot.HasItem && !toSlot.Item.SupportsSlot(fromSlotType))
        {
            message = $"{toSlot.Item.DisplayName} cannot swap into the {FormatSlot(fromSlotType)} slot.";
            return false;
        }

        if (!toSlot.HasItem)
        {
            AssignSlot(toSlot, fromSlot.Item, fromSlot.Projectile, fromSlot.LoadedAmmo, fromSlot.ReserveAmmo, fromSlot.PurchasePrice, fromSlot.SourceOffer);
            ClearSlot(fromSlot);
            return true;
        }

        EquipmentItemData swapItem = toSlot.Item;
        ProjectileData swapProjectile = toSlot.Projectile;
        int swapLoadedAmmo = toSlot.LoadedAmmo;
        int swapReserveAmmo = toSlot.ReserveAmmo;
        int swapPrice = toSlot.PurchasePrice;
        PreparedFenceOffer swapSourceOffer = toSlot.SourceOffer;

        AssignSlot(toSlot, fromSlot.Item, fromSlot.Projectile, fromSlot.LoadedAmmo, fromSlot.ReserveAmmo, fromSlot.PurchasePrice, fromSlot.SourceOffer);
        AssignSlot(fromSlot, swapItem, swapProjectile, swapLoadedAmmo, swapReserveAmmo, swapPrice, swapSourceOffer);
        return true;
    }

    private LoadoutSlotState GetSlotState(EquipmentSlotType slotType)
    {
        loadoutSlots.TryGetValue(slotType, out LoadoutSlotState slotState);
        return slotState;
    }

    private void AssignSlot(LoadoutSlotState slotState, EquipmentItemData item, ProjectileData projectile, int loadedAmmo, int reserveAmmo, int purchasePrice, PreparedFenceOffer sourceOffer = null)
    {
        if (slotState == null)
            return;

        slotState.Item = item;
        slotState.Projectile = projectile;
        slotState.LoadedAmmo = Mathf.Max(0, loadedAmmo);
        slotState.ReserveAmmo = Mathf.Max(0, reserveAmmo);
        slotState.PurchasePrice = Mathf.Max(0, purchasePrice);
        slotState.SourceOffer = sourceOffer;
    }

    private void ClearSlot(LoadoutSlotState slotState)
    {
        AssignSlot(slotState, null, null, 0, 0, 0);
    }

    private static PreparedFenceOffer RestockPurchasedOffer(LoadoutSlotState slotState)
    {
        if (slotState?.SourceOffer == null)
            return null;

        PreparedFenceOffer sourceOffer = slotState.SourceOffer;
        int maxQuantity = Mathf.Max(1, sourceOffer.InitialQuantity);
        sourceOffer.RemainingQuantity = Mathf.Min(maxQuantity, sourceOffer.RemainingQuantity + 1);
        return sourceOffer;
    }

    private List<EquipmentSlotType> ResolveCompatibleSlots(EquipmentItemData item)
    {
        List<EquipmentSlotType> compatibleSlots = new();
        if (item == null)
            return compatibleSlots;

        TryAddCompatibleSlot(compatibleSlots, item, EquipmentSlotType.Primary);
        TryAddCompatibleSlot(compatibleSlots, item, EquipmentSlotType.Secondary);
        TryAddCompatibleSlot(compatibleSlots, item, EquipmentSlotType.Belt);
        TryAddCompatibleSlot(compatibleSlots, item, EquipmentSlotType.Armor);
        return compatibleSlots;
    }

    private static void TryAddCompatibleSlot(List<EquipmentSlotType> slots, EquipmentItemData item, EquipmentSlotType slotType)
    {
        if (slots == null || item == null)
            return;

        if (item.SupportsSlot(slotType))
            slots.Add(slotType);
    }

    private static ProjectileData ResolveProjectileForItem(EquipmentItemData item, ProjectileData preferredProjectile)
    {
        if (item is not FirearmData firearmData)
            return null;

        if (firearmData.SupportsProjectile(preferredProjectile))
            return preferredProjectile;

        return firearmData.CompatibleProjectiles.Count > 0 ? firearmData.CompatibleProjectiles[0] : null;
    }

    private static int ResolveLoadedAmmo(EquipmentItemData item)
    {
        return item is FirearmData firearmData ? firearmData.AmmoCapacity : 0;
    }

    private static int ResolveReserveAmmo(EquipmentItemData item)
    {
        return item switch
        {
            FirearmData firearmData => firearmData.DefaultReserveAmmo,
            ThrowableUtilityData throwableData => throwableData.MaxUses,
            _ => 0
        };
    }

    private void RefreshCurrencyTexts()
    {
        SetText(header.cashText, $"Cash\n${HideoutRuntimeSession.Cash}");
        SetText(header.influenceText, $"Influence\n{HideoutRuntimeSession.InfluencePoints}");
    }

    private void SetMessage(string message)
    {
        SetText(header.messageText, message);
    }

    private static string BuildJobSubtitle(HideoutJobDefinition job)
    {
        if (job == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(job.FixerName))
            return job.RewardSummaryText ?? string.Empty;

        if (string.IsNullOrWhiteSpace(job.RewardSummaryText))
            return job.FixerName;

        return $"{job.FixerName} | {job.RewardSummaryText}";
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

    private static string FormatAllowedSlots(EquipmentSlotMask slotMask)
    {
        List<string> slotNames = new();
        if ((slotMask & EquipmentSlotMask.Primary) != 0)
            slotNames.Add("Primary");

        if ((slotMask & EquipmentSlotMask.Secondary) != 0)
            slotNames.Add("Secondary");

        if ((slotMask & EquipmentSlotMask.Belt) != 0)
            slotNames.Add("Belt");

        if ((slotMask & EquipmentSlotMask.Armor) != 0)
            slotNames.Add("Armor");

        return slotNames.Count > 0 ? string.Join(", ", slotNames) : "None";
    }

    private static string FormatSlot(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Primary => "Primary",
            EquipmentSlotType.Secondary => "Secondary",
            EquipmentSlotType.Belt => "Belt",
            EquipmentSlotType.Armor => "Armor",
            _ => "None"
        };
    }

    private static string ResolveHotkeyLabel(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Primary => "1",
            EquipmentSlotType.Secondary => "2",
            EquipmentSlotType.Belt => "3",
            _ => string.Empty
        };
    }

    private static void SetText(TMP_Text textField, string value)
    {
        if (textField != null)
            textField.text = value ?? string.Empty;
    }

    private static void SetImage(Image imageField, Sprite sprite)
    {
        if (imageField == null)
            return;

        imageField.sprite = sprite;
        imageField.enabled = sprite != null;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }

    private static void SetOptionalTextState(TMP_Text textField, bool active, string value)
    {
        if (textField == null)
            return;

        if (value != null)
            textField.text = value;

        textField.gameObject.SetActive(active);
    }
}

}
