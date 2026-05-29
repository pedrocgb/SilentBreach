using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Breezeblocks;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

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
        Contacts,
        Settings
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
    private sealed class SellButtonReference
    {
        public EquipmentSlotType slotType = EquipmentSlotType.None;
        public Button button;
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

        [Title("Context")]
        public PlayerEquipmentPanelUI contextPanel;

        [Title("Actions")]
        public Button sellButton;
        public Button startQuestButton;

        [Title("Equipment")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<PlayerEquipmentSlotViewUI> slotViews = new();

        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<SellButtonReference> slotSellButtons = new();
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

    [FoldoutGroup("Jobs"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<HideoutJobDefinition> startingJobs = new();

    [FoldoutGroup("Jobs")]
    [SerializeField] private string resourcesSearchPath = string.Empty;

    [FoldoutGroup("Transitions"), MinValue(0f)]
    [SerializeField] private float panelFadeDuration = 0.22f;

    [FoldoutGroup("Transitions")]
    [SerializeField] private Ease panelFadeEase = Ease.InOutSine;

    [FoldoutGroup("Transitions"), MinValue(0f)]
    [SerializeField] private float sceneFadeDuration = 0.35f;

    [FoldoutGroup("Transitions")]
    [SerializeField] private Ease sceneFadeEase = Ease.InOutSine;

    [FoldoutGroup("Transitions")]
    [SerializeField] private CanvasGroup sceneFadeCanvasGroup;

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

    [FoldoutGroup("References")]
    [SerializeField] private PlaceholderPanelReferences settingsPanel = new();

    private readonly List<HideoutJobDefinition> availableJobs = new();
    private readonly List<PreparedFenceOffer> activeFenceOffers = new();
    private readonly Dictionary<EquipmentSlotType, LoadoutSlotState> loadoutSlots = new();
    private readonly Dictionary<EquipmentSlotType, PlayerEquipmentSlotViewUI> equipmentSlotViews = new();
    private readonly Dictionary<EquipmentSlotType, Button> equipmentSellButtons = new();
    private readonly Dictionary<HideoutView, GameObject> resolvedRoots = new();
    private readonly Dictionary<HideoutView, CanvasGroup> resolvedCanvasGroups = new();

    private HideoutView currentView;
    private HideoutJobDefinition selectedJob;
    private HideoutJobDefinition preparedJob;
    private PreparedFenceOffer selectedOffer;
    private EquipmentSlotType selectedEquipmentSlot = EquipmentSlotType.None;
    private DetailSelectionSource detailSelectionSource = DetailSelectionSource.None;
    private int totalConfiguredJobs;
    private bool hasPreparedFence;
    private bool viewInitialized;
    private bool isSceneTransitioning;
    private bool isTearingDown;
    private Coroutine panelTransitionRoutine;
    private Tween sceneFadeTween;

    private void Awake()
    {
        isTearingDown = false;
        HideoutRuntimeSession.EnsureInitialized(startingCash, startingInfluencePoints);
        InitializeLoadoutSlots();
        ResolvePanelRoots();
        ResolvePanelCanvasGroups();
        CacheSlotViews();
        CacheSellButtons();
        PrepareTemplates();
        PrepareSceneFade();
        BindSlotEvents();
        BindButtons();
        ConfigurePlaceholderPanels();
        LoadAvailableJobs();

        selectedJob = ResolveInitialSelectedJob();

        RebuildJobList();
        RefreshJobDetails();
        RefreshCurrencyTexts();
        RefreshFenceView();
        SetViewImmediate(HideoutView.MainMenu);

        if (HideoutRuntimeSession.TryConsumePendingHideoutMessage(out string pendingMessage))
        {
            SetMessage(pendingMessage);
        }
        else if (availableJobs.Count > 0)
        {
            SetMessage("Choose a job to inspect the contract details.");
        }
        else if (totalConfiguredJobs > 0)
        {
            SetMessage("No jobs are currently available.");
        }
        else
        {
            SetMessage("Assign at least one starting job to populate the hideout.");
        }
    }

    private void OnDisable()
    {
        BeginTeardown();
    }

    private void OnDestroy()
    {
        BeginTeardown();
    }

    private void BeginTeardown()
    {
        if (isTearingDown)
            return;

        isTearingDown = true;
        if (sceneFadeCanvasGroup != null)
            DOTween.Kill(sceneFadeCanvasGroup);

        foreach (CanvasGroup canvasGroup in resolvedCanvasGroups.Values)
        {
            if (canvasGroup != null)
                DOTween.Kill(canvasGroup);
        }

        sceneFadeTween?.Kill();
        sceneFadeTween = null;

        if (panelTransitionRoutine != null)
        {
            StopCoroutine(panelTransitionRoutine);
            panelTransitionRoutine = null;
        }

        UnbindSlotEvents();
    }

    public void ShowMainMenu()
    {
        RequestView(HideoutView.MainMenu);
    }

    public void ShowJobsView()
    {
        RequestView(HideoutView.Jobs);
    }

    public void ShowPerksView()
    {
        RequestView(HideoutView.Perks);
    }

    public void ShowContactsView()
    {
        RequestView(HideoutView.Contacts);
    }

    public void ShowSettingsView()
    {
        RequestView(HideoutView.Settings);
    }

    public void OpenSelectedJobFence()
    {
        if (selectedJob == null)
        {
            SetMessage("Select a job before heading to the fence.");
            return;
        }

        if (!hasPreparedFence || preparedJob != selectedJob)
        {
            if (hasPreparedFence && preparedJob != null && preparedJob != selectedJob)
                RefundAllPurchasedItems(refreshUi: false);

            ResetPreparedLoadout();
            GenerateFenceInventory(selectedJob);
            preparedJob = selectedJob;
            hasPreparedFence = true;
            SetMessage($"The fence prepared a fresh spread for {selectedJob.JobTitle}.");
        }

        HideoutRuntimeSession.SetCurrentJob(selectedJob);
        EnsureValidFenceSelection();
        RefreshFenceView();
        RequestView(HideoutView.Fence);
    }

    public void BackOutOfFence()
    {
        RequestView(HideoutView.Jobs);
    }

    public void SellSelectedEquipment()
    {
        if (selectedEquipmentSlot == EquipmentSlotType.None)
        {
            SetMessage("Select an equipped item before selling.");
            return;
        }

        SellEquipmentInSlot(selectedEquipmentSlot);
    }

    public void StartQuest()
    {
        if (isSceneTransitioning)
            return;

        if (selectedJob == null || !SceneLoadUtility.CanLoadScene(selectedJob.MissionSceneBuildIndex, selectedJob.MissionSceneName))
        {
            SetMessage("This job does not have a scene configured yet.");
            return;
        }

        PlayerEquipmentRuntimeSession.SetPendingQuestLoadout(BuildRuntimeLoadout());
        HideoutRuntimeSession.SetCurrentJob(selectedJob);
        StartCoroutine(StartQuestRoutine(selectedJob));
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator StartQuestRoutine(HideoutJobDefinition job)
    {
        isSceneTransitioning = true;
        SetAllPanelsInteractable(false);

        yield return FadeScreen(1f, sceneFadeDuration);

        Time.timeScale = 1f;
        bool loaded = SceneLoadUtility.TryLoadScene(job.MissionSceneBuildIndex, job.MissionSceneName);
        if (!loaded)
        {
            yield return FadeScreen(0f, sceneFadeDuration);
            SetAllPanelsInteractable(true);
            SetMessage("The selected job scene could not be loaded.");
            isSceneTransitioning = false;
        }
    }

    private void InitializeLoadoutSlots()
    {
        loadoutSlots.Clear();
        loadoutSlots[EquipmentSlotType.Primary] = new LoadoutSlotState { SlotType = EquipmentSlotType.Primary };
        loadoutSlots[EquipmentSlotType.Secondary] = new LoadoutSlotState { SlotType = EquipmentSlotType.Secondary };
        loadoutSlots[EquipmentSlotType.Belt] = new LoadoutSlotState { SlotType = EquipmentSlotType.Belt };
        loadoutSlots[EquipmentSlotType.Armor] = new LoadoutSlotState { SlotType = EquipmentSlotType.Armor };
    }

    private void ResolvePanelRoots()
    {
        resolvedRoots.Clear();
        resolvedRoots[HideoutView.MainMenu] = ResolveConfiguredPanelRoot(mainMenu.root, "Main Menu Panel");
        resolvedRoots[HideoutView.Jobs] = ResolveConfiguredPanelRoot(jobsPanel.root, "Selected Job", "Available Jobs Panel");
        resolvedRoots[HideoutView.Fence] = ResolveConfiguredPanelRoot(fencePanel.root, "Fence Panel");
        resolvedRoots[HideoutView.Perks] = ResolveConfiguredPanelRoot(perksPanel.root, "Perks Panel");
        resolvedRoots[HideoutView.Contacts] = ResolveConfiguredPanelRoot(contactsPanel.root, "Contacts Panel");
        resolvedRoots[HideoutView.Settings] = ResolveConfiguredPanelRoot(settingsPanel.root, "Settings Panel");
    }

    private void ResolvePanelCanvasGroups()
    {
        resolvedCanvasGroups.Clear();

        foreach (KeyValuePair<HideoutView, GameObject> pair in resolvedRoots)
        {
            GameObject root = pair.Value;
            if (root == null)
                continue;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = root.AddComponent<CanvasGroup>();

            resolvedCanvasGroups[pair.Key] = canvasGroup;
        }
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

    private void CacheSellButtons()
    {
        equipmentSellButtons.Clear();

        for (int i = 0; i < fencePanel.slotSellButtons.Count; i++)
        {
            SellButtonReference reference = fencePanel.slotSellButtons[i];
            if (reference == null || reference.button == null || reference.slotType == EquipmentSlotType.None)
                continue;

            equipmentSellButtons[reference.slotType] = reference.button;
        }
    }

    private void BindSlotEvents()
    {
        foreach (PlayerEquipmentSlotViewUI slotView in equipmentSlotViews.Values)
        {
            if (slotView == null)
                continue;

            slotView.SetDragAndDropEnabled(slotView.SlotType.IsHandSlot());
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

    private void BindButtons()
    {
        BindButton(jobsPanel.proceedButton, OpenSelectedJobFence);
        BindButton(fencePanel.sellButton, SellSelectedEquipment);
        BindButton(fencePanel.startQuestButton, StartQuest);

        foreach (KeyValuePair<EquipmentSlotType, Button> pair in equipmentSellButtons)
        {
            EquipmentSlotType slotType = pair.Key;
            BindButton(pair.Value, () => SellEquipmentInSlot(slotType));
        }
    }

    private static void BindButton(Button button, UnityAction action)
    {
        if (button == null || action == null)
            return;

        if (button.onClick.GetPersistentEventCount() > 0)
            return;

        button.onClick.AddListener(action);
    }

    private void PrepareTemplates()
    {
        if (jobsPanel.jobListItemPrefab != null)
            jobsPanel.jobListItemPrefab.gameObject.SetActive(false);

        if (fencePanel.shopOfferItemPrefab != null)
            fencePanel.shopOfferItemPrefab.gameObject.SetActive(false);

        if (fencePanel.contextPanel != null)
            fencePanel.contextPanel.SetVisible(true);
    }

    private void PrepareSceneFade()
    {
        if (sceneFadeCanvasGroup == null)
            return;

        sceneFadeCanvasGroup.alpha = 0f;
        sceneFadeCanvasGroup.interactable = false;
        sceneFadeCanvasGroup.blocksRaycasts = false;
    }

    private void ConfigurePlaceholderPanels()
    {
        ConfigurePlaceholderPanel(perksPanel, "Perks");
        ConfigurePlaceholderPanel(contactsPanel, "Contacts");
        ConfigurePlaceholderPanel(settingsPanel, "Settings");
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
        List<HideoutJobDefinition> configuredJobs = CollectConfiguredJobs(out HashSet<string> startingJobIds);
        totalConfiguredJobs = configuredJobs.Count;

        for (int i = 0; i < configuredJobs.Count; i++)
        {
            HideoutJobDefinition job = configuredJobs[i];
            if (job == null || HideoutRuntimeSession.IsJobCompleted(job))
                continue;

            if (!startingJobIds.Contains(job.JobId) && !HideoutRuntimeSession.IsJobUnlocked(job))
                continue;

            availableJobs.Add(job);
        }

        availableJobs.Sort((left, right) =>
            string.Compare(left != null ? left.JobTitle : string.Empty, right != null ? right.JobTitle : string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private List<HideoutJobDefinition> CollectConfiguredJobs(out HashSet<string> startingJobIds)
    {
        startingJobIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<HideoutJobDefinition> visitedJobs = new();
        List<HideoutJobDefinition> configuredJobs = new();

        for (int i = 0; i < startingJobs.Count; i++)
        {
            HideoutJobDefinition job = startingJobs[i];
            if (job == null)
                continue;

            startingJobIds.Add(job.JobId);
            CollectJobRecursive(job, visitedJobs, configuredJobs);
        }

        if (configuredJobs.Count > 0)
            return configuredJobs;

        HideoutJobDefinition[] resourceJobs = Resources.LoadAll<HideoutJobDefinition>(resourcesSearchPath ?? string.Empty);
        HashSet<string> lockedJobIds = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < resourceJobs.Length; i++)
        {
            HideoutJobDefinition job = resourceJobs[i];
            if (job == null || !visitedJobs.Add(job))
                continue;

            configuredJobs.Add(job);

            IReadOnlyList<HideoutJobDefinition> unlockJobs = job.UnlockJobs;
            for (int unlockIndex = 0; unlockIndex < unlockJobs.Count; unlockIndex++)
            {
                HideoutJobDefinition unlockJob = unlockJobs[unlockIndex];
                if (unlockJob == null || string.IsNullOrWhiteSpace(unlockJob.JobId))
                    continue;

                lockedJobIds.Add(unlockJob.JobId);
            }
        }

        for (int i = 0; i < configuredJobs.Count; i++)
        {
            HideoutJobDefinition job = configuredJobs[i];
            if (job == null || lockedJobIds.Contains(job.JobId))
                continue;

            startingJobIds.Add(job.JobId);
        }

        return configuredJobs;
    }

    private static void CollectJobRecursive(HideoutJobDefinition job, HashSet<HideoutJobDefinition> visitedJobs, List<HideoutJobDefinition> configuredJobs)
    {
        if (job == null || visitedJobs == null || configuredJobs == null || !visitedJobs.Add(job))
            return;

        configuredJobs.Add(job);
        IReadOnlyList<HideoutJobDefinition> unlockJobs = job.UnlockJobs;
        for (int i = 0; i < unlockJobs.Count; i++)
            CollectJobRecursive(unlockJobs[i], visitedJobs, configuredJobs);
    }

    private HideoutJobDefinition ResolveInitialSelectedJob()
    {
        if (selectedJob != null && availableJobs.Contains(selectedJob))
            return selectedJob;

        HideoutJobDefinition runtimeJob = HideoutRuntimeSession.CurrentJob;
        if (runtimeJob != null && availableJobs.Contains(runtimeJob))
            return runtimeJob;

        return availableJobs.Count > 0 ? availableJobs[0] : null;
    }

    private void RebuildJobList()
    {
        if (isTearingDown)
            return;

        Transform preservedTemplate = ResolvePreservedTemplate(
            jobsPanel.jobListContent,
            jobsPanel.jobListItemPrefab != null ? jobsPanel.jobListItemPrefab.transform : null);
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
        if (isTearingDown)
            return;

        Transform preservedTemplate = ResolvePreservedTemplate(
            fencePanel.shopListContent,
            fencePanel.shopOfferItemPrefab != null ? fencePanel.shopOfferItemPrefab.transform : null);
        ClearGeneratedChildren(fencePanel.shopListContent, preservedTemplate);

        if (activeFenceOffers.Count == 0)
        {
            SetOptionalTextState(fencePanel.emptyStateText, true, "This fence has nothing available for this job.");
            return;
        }

        if (fencePanel.shopOfferItemPrefab == null || fencePanel.shopListContent == null)
        {
            SetOptionalTextState(fencePanel.emptyStateText, true, "Assign a fence item prefab and content root.");
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
                $"R${offer.Price} | Qty {offer.RemainingQuantity}",
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
        if (isTearingDown)
            return;

        if (selectedJob == null)
        {
            SetText(jobsPanel.jobNameText, "No Job Selected");
            SetText(jobsPanel.jobLevelText, string.Empty);
            SetText(jobsPanel.jobDescriptionText, "Select a contract to inspect it.");
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
        SetText(jobsPanel.jobLevelText, ResolveJobLevelText(selectedJob.JobLevel));
        SetText(jobsPanel.jobDescriptionText, selectedJob.JobDescription);
        SetText(jobsPanel.jobRewardText, selectedJob.RewardText);
        SetText(jobsPanel.jobObjectivesText, selectedJob.ObjectivesText);
        SetText(jobsPanel.jobFailureText, selectedJob.TermsOfFailureText);
        SetText(jobsPanel.jobFixerText, selectedJob.FixerName);
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

        activeFenceOffers.Sort((left, right) =>
            string.Compare(left.Item != null ? left.Item.DisplayName : string.Empty, right.Item != null ? right.Item.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshFenceView()
    {
        if (isTearingDown)
            return;

        if (fencePanel.contextPanel != null)
            fencePanel.contextPanel.SetVisible(true);

        SetText(fencePanel.shopTitleText, selectedJob != null ? selectedJob.ShopTitle : "The Fence");
        SetText(fencePanel.shopDescriptionText, selectedJob != null ? selectedJob.ShopDescription : string.Empty);
        SetImage(fencePanel.shopImage, selectedJob != null ? selectedJob.ShopImage : null);
        RefreshCurrencyTexts();
        RebuildFenceOfferList();
        RefreshEquipmentSlots();
        RefreshFenceDetail();

        if (fencePanel.startQuestButton != null)
        {
            fencePanel.startQuestButton.interactable =
                selectedJob != null &&
                SceneLoadUtility.CanLoadScene(selectedJob.MissionSceneBuildIndex, selectedJob.MissionSceneName) &&
                !isSceneTransitioning;
        }
    }

    private void RefreshEquipmentSlots()
    {
        if (isTearingDown)
            return;

        RefreshSingleSlot(EquipmentSlotType.Primary);
        RefreshSingleSlot(EquipmentSlotType.Secondary);
        RefreshSingleSlot(EquipmentSlotType.Belt);
        RefreshSingleSlot(EquipmentSlotType.Armor);
        RefreshSellButtons();
    }

    private void RefreshSingleSlot(EquipmentSlotType slotType)
    {
        if (!equipmentSlotViews.TryGetValue(slotType, out PlayerEquipmentSlotViewUI slotView) || slotView == null)
            return;

        LoadoutSlotState slotState = GetSlotState(slotType);
        bool isSelected = detailSelectionSource == DetailSelectionSource.Equipment && selectedEquipmentSlot == slotType;
        slotView.Refresh(
            slotState != null ? slotState.Item : null,
            isSelected,
            ResolveSlotDisplayName(slotType),
            ResolveHotkeyLabel(slotType));
    }

    private void RefreshSellButtons()
    {
        foreach (KeyValuePair<EquipmentSlotType, Button> pair in equipmentSellButtons)
        {
            LoadoutSlotState slotState = GetSlotState(pair.Key);
            if (pair.Value != null)
                pair.Value.interactable = slotState != null && slotState.HasItem;
        }
    }

    private void RefreshFenceDetail()
    {
        if (isTearingDown)
            return;

        bool canSell = false;

        if (detailSelectionSource == DetailSelectionSource.ShopOffer && selectedOffer != null)
        {
            ShowFenceContext(
                selectedOffer.Item,
                ResolvePreferredSlotForContext(selectedOffer.Item),
                selectedOffer.Projectile,
                ResolveLoadedAmmo(selectedOffer.Item),
                ResolveReserveAmmo(selectedOffer.Item));
        }
        else if (detailSelectionSource == DetailSelectionSource.Equipment)
        {
            LoadoutSlotState slotState = GetSlotState(selectedEquipmentSlot);
            if (slotState != null && slotState.HasItem)
            {
                canSell = true;
                ShowFenceContext(
                    slotState.Item,
                    slotState.SlotType,
                    slotState.Projectile,
                    slotState.LoadedAmmo,
                    slotState.ReserveAmmo);
            }
            else
            {
                detailSelectionSource = DetailSelectionSource.None;
                selectedEquipmentSlot = EquipmentSlotType.None;
                ShowEmptyFenceContext();
            }
        }
        else
        {
            ShowEmptyFenceContext();
        }

        if (fencePanel.sellButton != null)
            fencePanel.sellButton.interactable = canSell;
    }

    private void ShowFenceContext(
        EquipmentItemData item,
        EquipmentSlotType slotType,
        ProjectileData projectile,
        int loadedAmmo,
        int reserveAmmo)
    {
        if (fencePanel.contextPanel == null)
            return;

        if (item == null)
        {
            fencePanel.contextPanel.ShowNoSelectionContext();
            return;
        }

        fencePanel.contextPanel.ShowContextForItem(item, slotType, projectile, loadedAmmo, reserveAmmo);
    }

    private void ShowEmptyFenceContext()
    {
        if (fencePanel.contextPanel != null)
            fencePanel.contextPanel.ShowNoSelectionContext();
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

    private void EnsureValidFenceSelection()
    {
        if (detailSelectionSource == DetailSelectionSource.ShopOffer &&
            selectedOffer != null &&
            activeFenceOffers.Contains(selectedOffer))
        {
            return;
        }

        if (detailSelectionSource == DetailSelectionSource.Equipment)
        {
            LoadoutSlotState slotState = GetSlotState(selectedEquipmentSlot);
            if (slotState != null && slotState.HasItem)
                return;
        }

        SelectInitialFenceDetail();
    }

    private void SelectOffer(PreparedFenceOffer offer)
    {
        selectedOffer = offer;
        selectedEquipmentSlot = EquipmentSlotType.None;
        detailSelectionSource = offer != null ? DetailSelectionSource.ShopOffer : DetailSelectionSource.None;

        if (!isTearingDown && currentView == HideoutView.Fence)
            RefreshFenceView();
    }

    private void SelectEquipmentSlot(EquipmentSlotType slotType)
    {
        selectedEquipmentSlot = slotType;
        selectedOffer = null;
        detailSelectionSource = slotType != EquipmentSlotType.None ? DetailSelectionSource.Equipment : DetailSelectionSource.None;

        if (!isTearingDown && currentView == HideoutView.Fence)
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
            SetMessage("You do not have enough cash for this item.");
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
        SetMessage($"Bought {offer.Item.DisplayName} for the {ResolveSlotDisplayName(slotType)} slot.");
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
            ? $"No free compatible slot is available for {offer.Item.DisplayName}."
            : $"{offer.Item.DisplayName} cannot be equipped in the hideout loadout.";
        return false;
    }

    private void SellEquipmentInSlot(EquipmentSlotType slotType)
    {
        LoadoutSlotState slotState = GetSlotState(slotType);
        if (slotState == null || !slotState.HasItem)
        {
            SetMessage("There is no equipment in that slot to sell.");
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
            EnsureValidFenceSelection();

        RefreshFenceView();
        SetMessage($"Sold {itemName} for ${refund}.");
    }

    private int RefundAllPurchasedItems(bool refreshUi)
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

        if (refreshUi)
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
        SetMessage($"Moved equipment to the {ResolveSlotDisplayName(targetSlotView.SlotType)} slot.");
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
            message = $"{fromSlot.Item.DisplayName} cannot go into the {ResolveSlotDisplayName(toSlotType)} slot.";
            return false;
        }

        if (toSlot.HasItem && !toSlot.Item.SupportsSlot(fromSlotType))
        {
            message = $"{toSlot.Item.DisplayName} cannot swap into the {ResolveSlotDisplayName(fromSlotType)} slot.";
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

    private void AssignSlot(
        LoadoutSlotState slotState,
        EquipmentItemData item,
        ProjectileData projectile,
        int loadedAmmo,
        int reserveAmmo,
        int purchasePrice,
        PreparedFenceOffer sourceOffer = null)
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

    private static EquipmentSlotType ResolvePreferredSlotForContext(EquipmentItemData item)
    {
        if (item == null)
            return EquipmentSlotType.None;

        if (item.SupportsSlot(EquipmentSlotType.Primary))
            return EquipmentSlotType.Primary;

        if (item.SupportsSlot(EquipmentSlotType.Secondary))
            return EquipmentSlotType.Secondary;

        if (item.SupportsSlot(EquipmentSlotType.Belt))
            return EquipmentSlotType.Belt;

        if (item.SupportsSlot(EquipmentSlotType.Armor))
            return EquipmentSlotType.Armor;

        return EquipmentSlotType.None;
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

    private void RequestView(HideoutView targetView)
    {
        if (isTearingDown)
            return;

        if (GetViewRoot(targetView) == null)
        {
            SetMessage($"{ResolveViewDisplayName(targetView)} panel is not assigned.");
            return;
        }

        if (!viewInitialized)
        {
            SetViewImmediate(targetView);
            return;
        }

        if (panelTransitionRoutine != null || targetView == currentView)
        {
            RefreshViewContent(targetView);
            return;
        }

        panelTransitionRoutine = StartCoroutine(TransitionToViewRoutine(targetView));
    }

    private IEnumerator TransitionToViewRoutine(HideoutView targetView)
    {
        RefreshViewContent(targetView);

        GameObject currentRoot = GetViewRoot(currentView);
        CanvasGroup currentCanvasGroup = GetViewCanvasGroup(currentView);
        GameObject targetRoot = GetViewRoot(targetView);
        CanvasGroup targetCanvasGroup = GetViewCanvasGroup(targetView);

        if (currentRoot == targetRoot)
        {
            SetViewImmediate(targetView);
            panelTransitionRoutine = null;
            yield break;
        }

        if (currentRoot != null && currentCanvasGroup != null)
        {
            currentCanvasGroup.interactable = false;
            currentCanvasGroup.blocksRaycasts = false;

            if (panelFadeDuration > 0f)
            {
                Tween fadeOutTween = currentCanvasGroup
                    .DOFade(0f, panelFadeDuration)
                    .SetEase(panelFadeEase)
                    .SetUpdate(true);
                yield return fadeOutTween.WaitForCompletion();
            }
            else
            {
                currentCanvasGroup.alpha = 0f;
            }
        }

        if (currentRoot != null)
            currentRoot.SetActive(false);

        currentView = targetView;
        UpdateHeaderTitle(targetView);

        if (targetRoot != null)
        {
            targetRoot.SetActive(true);

            if (targetCanvasGroup != null)
            {
                targetCanvasGroup.alpha = 0f;
                targetCanvasGroup.interactable = true;
                targetCanvasGroup.blocksRaycasts = true;

                if (panelFadeDuration > 0f)
                {
                    Tween fadeInTween = targetCanvasGroup
                        .DOFade(1f, panelFadeDuration)
                        .SetEase(panelFadeEase)
                        .SetUpdate(true);
                    yield return fadeInTween.WaitForCompletion();
                }
                else
                {
                    targetCanvasGroup.alpha = 1f;
                }
            }
        }

        panelTransitionRoutine = null;
    }

    private void SetViewImmediate(HideoutView view)
    {
        RefreshViewContent(view);

        foreach (KeyValuePair<HideoutView, GameObject> pair in resolvedRoots)
        {
            bool isTarget = pair.Key == view;
            if (pair.Value != null)
                pair.Value.SetActive(isTarget);

            if (!resolvedCanvasGroups.TryGetValue(pair.Key, out CanvasGroup canvasGroup) || canvasGroup == null)
                continue;

            canvasGroup.alpha = isTarget ? 1f : 0f;
            canvasGroup.interactable = isTarget;
            canvasGroup.blocksRaycasts = isTarget;
        }

        currentView = view;
        viewInitialized = true;
        UpdateHeaderTitle(view);
    }

    private void RefreshViewContent(HideoutView view)
    {
        if (isTearingDown)
            return;

        if (view == HideoutView.Jobs)
            RefreshJobDetails();

        if (view == HideoutView.Fence)
            RefreshFenceView();
    }

    private void UpdateHeaderTitle(HideoutView view)
    {
        string headerTitle = view switch
        {
            HideoutView.MainMenu => "Hideout",
            HideoutView.Jobs => "Jobs",
            HideoutView.Fence => selectedJob != null ? $"{selectedJob.JobTitle} | Fence" : "Fence",
            HideoutView.Perks => "Perks",
            HideoutView.Contacts => "Contacts",
            HideoutView.Settings => "Settings",
            _ => "Hideout"
        };

        SetText(header.titleText, headerTitle);
    }

    private GameObject GetViewRoot(HideoutView view)
    {
        resolvedRoots.TryGetValue(view, out GameObject root);
        return root;
    }

    private CanvasGroup GetViewCanvasGroup(HideoutView view)
    {
        resolvedCanvasGroups.TryGetValue(view, out CanvasGroup canvasGroup);
        return canvasGroup;
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        if (isTearingDown || sceneFadeCanvasGroup == null)
            yield break;

        sceneFadeTween?.Kill();
        sceneFadeCanvasGroup.blocksRaycasts = targetAlpha > 0.001f;
        sceneFadeCanvasGroup.interactable = false;

        if (duration <= 0f)
        {
            sceneFadeCanvasGroup.alpha = targetAlpha;
            yield break;
        }

        sceneFadeTween = sceneFadeCanvasGroup
            .DOFade(targetAlpha, duration)
            .SetEase(sceneFadeEase)
            .SetUpdate(true);
        yield return sceneFadeTween.WaitForCompletion();
        sceneFadeTween = null;
    }

    private void SetAllPanelsInteractable(bool interactable)
    {
        foreach (CanvasGroup canvasGroup in resolvedCanvasGroups.Values)
        {
            if (canvasGroup == null)
                continue;

            canvasGroup.interactable = interactable && canvasGroup.alpha > 0.001f;
            canvasGroup.blocksRaycasts = interactable && canvasGroup.alpha > 0.001f;
        }
    }

    private static GameObject ResolveConfiguredPanelRoot(GameObject configuredRoot, params string[] fallbackNames)
    {
        if (configuredRoot != null)
            return configuredRoot;

        if (fallbackNames == null)
            return null;

        for (int i = 0; i < fallbackNames.Length; i++)
        {
            GameObject sceneObject = FindSceneObjectByName(fallbackNames[i]);
            if (sceneObject != null)
                return sceneObject;
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            GameObject match = FindGameObjectRecursive(rootObjects[i].transform, targetName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static GameObject FindGameObjectRecursive(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject childMatch = FindGameObjectRecursive(root.GetChild(i), targetName);
            if (childMatch != null)
                return childMatch;
        }

        return null;
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

    private static void SetImage(Image imageField, Sprite sprite)
    {
        if (imageField == null)
            return;

        imageField.sprite = sprite;
        imageField.enabled = sprite != null;
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

    private static EquipmentContextUiSettings ResolveUiSettings()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.EquipmentContextUi
            : new EquipmentContextUiSettings();
    }

    private static string ResolveSlotDisplayName(EquipmentSlotType slotType)
    {
        return ResolveUiSettings().GetSlotDisplayName(slotType);
    }

    private static string ResolveJobLevelText(HideoutJobLevel jobLevel)
    {
        return ResolveUiSettings().GetJobLevelText(jobLevel);
    }

    private static string ResolveViewDisplayName(HideoutView view)
    {
        return view switch
        {
            HideoutView.MainMenu => "Main menu",
            HideoutView.Jobs => "Jobs",
            HideoutView.Fence => "Fence",
            HideoutView.Perks => "Perks",
            HideoutView.Contacts => "Contacts",
            HideoutView.Settings => "Settings",
            _ => "Requested"
        };
    }

    private static string FormatAllowedSlots(EquipmentSlotMask slotMask)
    {
        EquipmentContextUiSettings uiSettings = ResolveUiSettings();
        List<string> slotNames = new();

        if ((slotMask & EquipmentSlotMask.Primary) != 0)
            slotNames.Add(uiSettings.GetSlotDisplayName(EquipmentSlotType.Primary));

        if ((slotMask & EquipmentSlotMask.Secondary) != 0)
            slotNames.Add(uiSettings.GetSlotDisplayName(EquipmentSlotType.Secondary));

        if ((slotMask & EquipmentSlotMask.Belt) != 0)
            slotNames.Add(uiSettings.GetSlotDisplayName(EquipmentSlotType.Belt));

        if ((slotMask & EquipmentSlotMask.Armor) != 0)
            slotNames.Add(uiSettings.GetSlotDisplayName(EquipmentSlotType.Armor));

        return slotNames.Count > 0 ? string.Join(", ", slotNames) : "None";
    }
}

}
