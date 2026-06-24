using System;
using System.Collections.Generic;
using System.Text;
using Breezeblocks.HideoutSystem;
using Breezeblocks.Input;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Gameplay Console Controller")]
public class GameplayConsoleController : MonoBehaviour
{
    private const string CreateEquipmentCommandPrefix = "create_equipment_";
    private const string AddPerkCommandName = "add_perk";
    private const string DefaultToggleConsoleAction = "ToggleConsole";

    [Serializable]
    private sealed class ConsoleCommandHelpEntry
    {
        [HideInInspector]
        public string commandId;

        [ReadOnly]
        public string commandSyntax;

        [MultiLineProperty(3)]
        public string description;
    }

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string toggleConsoleAction = DefaultToggleConsoleAction;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string commandHistoryVerticalAction = "Move Vertical";

    [FoldoutGroup("Rewired"), Range(0.01f, 1f)]
    [SerializeField] private float commandHistoryAxisThreshold = 0.5f;

    [FoldoutGroup("Legacy"), HideInInspector]
    [SerializeField] private KeyCode keyboardToggleConsoleFallbackKey = KeyCode.BackQuote;

    [FoldoutGroup("References")]
    [SerializeField] private Transform playerRoot;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController playerEquipmentController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerPickupInteractor playerPickupInteractor;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerFocusController playerFocusController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerStaminaController playerStaminaController;

    [FoldoutGroup("References")]
    [SerializeField] private ActorHealth playerHealth;

    [FoldoutGroup("References")]
    [SerializeField] private Light2D globalLight2D;

    [FoldoutGroup("UI")]
    [SerializeField] private bool showSystemTime = true;

    [FoldoutGroup("UI")]
    [SerializeField] private bool pauseGameplayWhileOpen;

    [FoldoutGroup("UI"), MinValue(8)]
    [SerializeField] private int maxLogEntries = 128;

    [FoldoutGroup("Help"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
    [SerializeField] private List<ConsoleCommandHelpEntry> commandHelpEntries = new();

    [FoldoutGroup("UI/Optional References")]
    [SerializeField] private Canvas consoleCanvas;

    [FoldoutGroup("UI/Optional References")]
    [SerializeField] private RectTransform consoleRoot;

    [FoldoutGroup("UI/Optional References")]
    [SerializeField] private TMP_Text logText;

    [FoldoutGroup("UI/Optional References")]
    [SerializeField] private TMP_Text systemTimeText;

    [FoldoutGroup("UI/Optional References")]
    [SerializeField] private TMP_InputField commandInputField;

    [FoldoutGroup("UI/Optional References")]
    [SerializeField] private ScrollRect logScrollRect;

    [FoldoutGroup("UI/Optional References")]
    [SerializeField] private Scrollbar logScrollbar;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsConsoleVisible => consoleVisible;

    private readonly List<string> logEntries = new();
    private readonly StringBuilder logBuilder = new();
    private readonly List<Collider2D> cachedGhostModeColliders = new();
    private readonly List<bool> cachedGhostModeColliderStates = new();
    private readonly List<string> commandHistory = new();

    private IPlayerInputReader inputReader;
    private float cachedTimeScaleBeforeConsole = 1f;
    private bool consoleVisible;
    private EquipmentItemRuntimeCatalog equipmentCatalog;
    private bool defaultGlobalLightStateCached;
    private bool defaultGlobalLightEnabled;
    private bool ghostModeColliderCacheValid;
    private bool commandHistoryAxisEngaged;
    private int commandHistoryCursor = -1;

    // Executes the EnsureOn routine.
    public static GameplayConsoleController EnsureOn(GameObject host)
    {
        if (host == null)
            return null;

        GameplayConsoleController controller = host.GetComponent<GameplayConsoleController>();
        return controller != null ? controller : host.AddComponent<GameplayConsoleController>();
    }

    // Executes the Reset routine.
    private void Reset()
    {
        CacheReferences();
        EnsureCommandHelpEntries();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        CacheReferences();
        EnsureCommandHelpEntries();
        CacheGhostModeColliders();
        EnsureEventSystemExists();
        EnsureConsoleUi();
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
        SetConsoleVisible(false, force: true);
        ApplyCheatState();
        AppendLog("Console ready.");
    }

    // Executes the OnEnable routine.
    private void OnEnable()
    {
        GameplayConsoleCheatState.StateChanged += HandleCheatStateChanged;
        ApplyCheatState();
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        GameplayConsoleCheatState.StateChanged -= HandleCheatStateChanged;
        SetConsoleVisible(false, force: true);
    }

    // Executes the Update routine.
    private void Update()
    {
        if (inputReader == null)
            inputReader = new RewiredPlayerInputReader(rewiredPlayerId);

        if (!inputReader.IsReady)
            return;

        if (ResolveToggleConsoleRequested())
        {
            if (consoleVisible)
                SetConsoleVisible(false);
            else if (CanOpenConsole())
                SetConsoleVisible(true);
        }

        if (!consoleVisible)
            return;

        RefreshSystemTimeLabel();
        HandleCommandHistoryInput();
    }

    // Executes the OnValidate routine.
    private void OnValidate()
    {
        commandHistoryAxisThreshold = Mathf.Clamp(commandHistoryAxisThreshold, 0.01f, 1f);
        EnsureCommandHelpEntries();
    }

    // Executes the CacheReferences routine.
    private void CacheReferences()
    {
        if (playerMotor == null)
            playerMotor = FindFirstObjectByType<PlayerTopDownMotor2D>();

        if (playerRoot == null && playerMotor != null)
            playerRoot = playerMotor.transform.root;

        if (playerVisionLight == null)
            playerVisionLight = FindFirstObjectByType<PlayerVisionLight>();

        if (playerEquipmentController == null)
            playerEquipmentController = FindFirstObjectByType<PlayerEquipmentController>();

        if (playerWeaponController == null)
            playerWeaponController = FindFirstObjectByType<PlayerWeaponController>();

        if (playerUtilityController == null)
            playerUtilityController = FindFirstObjectByType<PlayerUtilityController>();

        if (playerMeleeController == null)
            playerMeleeController = FindFirstObjectByType<PlayerMeleeController>();

        if (playerPickupInteractor == null)
            playerPickupInteractor = FindFirstObjectByType<PlayerPickupInteractor>();

        if (playerFocusController == null)
            playerFocusController = FindFirstObjectByType<PlayerFocusController>();

        if (playerStaminaController == null)
            playerStaminaController = FindFirstObjectByType<PlayerStaminaController>();

        if (playerHealth == null)
        {
            if (playerRoot != null)
                playerHealth = playerRoot.GetComponent<ActorHealth>() ?? playerRoot.GetComponentInChildren<ActorHealth>(true);
            else if (playerMotor != null)
                playerHealth = playerMotor.GetComponent<ActorHealth>();
        }

        if (playerRoot == null && playerEquipmentController != null)
            playerRoot = playerEquipmentController.transform.root;

        if (globalLight2D == null)
            globalLight2D = FindGlobalLight2D();

        CacheDefaultGlobalLightState();
    }

    // Executes the EnsureConsoleUi routine.
    private void EnsureConsoleUi()
    {
        if (consoleCanvas != null &&
            consoleRoot != null &&
            logText != null &&
            systemTimeText != null &&
            commandInputField != null &&
            logScrollRect != null &&
            logScrollbar != null)
        {
            HookInputFieldEvents();
            RefreshSystemTimeLabel();
            return;
        }

        BuildRuntimeUi();
    }

    // Executes the BuildRuntimeUi routine.
    private void BuildRuntimeUi()
    {
        GameObject canvasObject = new("Gameplay Console Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        consoleCanvas = canvasObject.GetComponent<Canvas>();
        consoleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        consoleCanvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        consoleRoot = CreateRectTransform("Console Root", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image rootImage = consoleRoot.gameObject.AddComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.45f);

        RectTransform panel = CreateRectTransform(
            "Panel",
            consoleRoot,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-540f, -310f),
            new Vector2(540f, 310f));
        panel.pivot = new Vector2(0.5f, 0.5f);
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.08f, 0.11f, 0.98f);

        RectTransform header = CreateRectTransform(
            "Header",
            panel,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -48f),
            new Vector2(0f, 0f));
        Image headerImage = header.gameObject.AddComponent<Image>();
        headerImage.color = new Color(0.11f, 0.14f, 0.18f, 1f);

        TMP_Text titleText = CreateText(
            "Title",
            header,
            "Gameplay Console",
            28,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            new Color(0.92f, 0.94f, 0.98f, 1f),
            new Vector2(18f, 0f),
            new Vector2(-220f, 0f));
        titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);

        systemTimeText = CreateText(
            "System Time",
            header,
            string.Empty,
            22,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineRight,
            new Color(0.72f, 0.8f, 0.91f, 1f),
            new Vector2(240f, 0f),
            new Vector2(-18f, 0f));
        systemTimeText.rectTransform.anchorMin = new Vector2(1f, 0f);
        systemTimeText.rectTransform.anchorMax = new Vector2(1f, 1f);
        systemTimeText.rectTransform.pivot = new Vector2(1f, 0.5f);

        RectTransform logArea = CreateRectTransform(
            "Log Area",
            panel,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(18f, 84f),
            new Vector2(-18f, -64f));

        logScrollRect = logArea.gameObject.AddComponent<ScrollRect>();
        logScrollRect.horizontal = false;
        logScrollRect.vertical = true;
        logScrollRect.movementType = ScrollRect.MovementType.Clamped;
        logScrollRect.scrollSensitivity = 24f;

        RectTransform viewport = CreateRectTransform(
            "Viewport",
            logArea,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(-18f, 0f));
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0.02f, 0.03f, 0.05f, 0.78f);
        Mask viewportMask = viewport.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        RectTransform logTextRect = CreateRectTransform(
            "Log Text",
            viewport,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(10f, -10f),
            new Vector2(-10f, -10f));
        logTextRect.pivot = new Vector2(0.5f, 1f);

        logText = logTextRect.gameObject.AddComponent<TextMeshProUGUI>();
        ApplyTextDefaults(logText, 22, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.9f, 0.92f, 0.95f, 1f));
        logText.enableWordWrapping = true;
        logText.overflowMode = TextOverflowModes.Overflow;

        ContentSizeFitter logContentFitter = logTextRect.gameObject.AddComponent<ContentSizeFitter>();
        logContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        logContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        logScrollbar = CreateScrollbar(logArea);
        logScrollRect.viewport = viewport;
        logScrollRect.content = logTextRect;
        logScrollRect.verticalScrollbar = logScrollbar;
        logScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        RectTransform inputBackground = CreateRectTransform(
            "Input Background",
            panel,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(18f, 18f),
            new Vector2(-18f, 62f));
        Image inputBackgroundImage = inputBackground.gameObject.AddComponent<Image>();
        inputBackgroundImage.color = new Color(0.13f, 0.16f, 0.2f, 1f);

        commandInputField = inputBackground.gameObject.AddComponent<TMP_InputField>();
        commandInputField.lineType = TMP_InputField.LineType.SingleLine;
        commandInputField.richText = false;
        commandInputField.resetOnDeActivation = false;

        RectTransform textViewport = CreateRectTransform(
            "Text Viewport",
            inputBackground,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(12f, 8f),
            new Vector2(-12f, -8f));

        RectTransform placeholderRect = CreateRectTransform(
            "Placeholder",
            textViewport,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        TMP_Text placeholderText = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
        ApplyTextDefaults(placeholderText, 22, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, new Color(0.52f, 0.58f, 0.65f, 1f));
        placeholderText.text = "Type a console code and press Enter";

        RectTransform inputTextRect = CreateRectTransform(
            "Text",
            textViewport,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        TMP_Text inputText = inputTextRect.gameObject.AddComponent<TextMeshProUGUI>();
        ApplyTextDefaults(inputText, 22, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, new Color(0.95f, 0.96f, 0.98f, 1f));

        commandInputField.textViewport = textViewport;
        commandInputField.placeholder = placeholderText;
        commandInputField.textComponent = inputText;

        HookInputFieldEvents();
        RefreshSystemTimeLabel();
    }

    // Executes the HookInputFieldEvents routine.
    private void HookInputFieldEvents()
    {
        if (commandInputField == null)
            return;

        commandInputField.onSubmit.RemoveListener(HandleSubmittedCommand);
        commandInputField.onSubmit.AddListener(HandleSubmittedCommand);
    }

    // Executes the HandleSubmittedCommand routine.
    private void HandleSubmittedCommand(string submittedText)
    {
        if (!consoleVisible)
            return;

        AddCommandToHistory(submittedText);
        ExecuteCommand(submittedText);
        if (commandInputField == null)
            return;

        commandInputField.text = string.Empty;
        commandInputField.ActivateInputField();
        commandInputField.Select();
    }

    // Executes the ExecuteCommand routine.
    private void ExecuteCommand(string rawCommand)
    {
        string trimmedCommand = rawCommand != null ? rawCommand.Trim() : string.Empty;
        if (string.IsNullOrEmpty(trimmedCommand))
            return;

        AppendLog($"> {trimmedCommand}");

        if (trimmedCommand.StartsWith(CreateEquipmentCommandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            GameplayConsoleCheatState.RegisterCommandUse("create_equipment");
            string requestedEquipmentName = trimmedCommand.Substring(CreateEquipmentCommandPrefix.Length).Trim();
            HandleCreateEquipmentCommand(requestedEquipmentName);
            return;
        }

        int firstSpaceIndex = trimmedCommand.IndexOf(' ');
        string commandName = firstSpaceIndex >= 0 ? trimmedCommand.Substring(0, firstSpaceIndex) : trimmedCommand;
        string argument = firstSpaceIndex >= 0 ? trimmedCommand.Substring(firstSpaceIndex + 1).Trim() : string.Empty;

        switch (commandName.ToLowerInvariant())
        {
            case "help":
                PrintHelp();
                break;

            case "god_mode":
                HandleBooleanCheatCommand(argument, "god_mode", GameplayConsoleCheatState.SetGodMode);
                break;

            case "invisible":
                HandleBooleanCheatCommand(argument, "invisible", GameplayConsoleCheatState.SetInvisible);
                break;

            case "lightfooted":
                HandleBooleanCheatCommand(argument, "lightfooted", GameplayConsoleCheatState.SetLightfooted);
                break;

            case "restart_level":
                GameplayConsoleCheatState.RegisterCommandUse("restart_level");
                RestartCurrentLevel();
                break;

            case "noclip":
                HandleBooleanCheatCommand(argument, "noclip", GameplayConsoleCheatState.SetInfiniteReserveAmmo);
                break;

            case "focus_mode":
                HandleBooleanCheatCommand(argument, "focus_mode", GameplayConsoleCheatState.SetFocusMode);
                break;

            case "athlete_mode":
                HandleBooleanCheatCommand(argument, "athlete_mode", GameplayConsoleCheatState.SetAthleteMode);
                break;

            case "ghost_mode":
                HandleBooleanCheatCommand(argument, "ghost_mode", GameplayConsoleCheatState.SetGhostMode);
                break;

            case "medusa_mode":
                HandleBooleanCheatCommand(argument, "medusa_mode", GameplayConsoleCheatState.SetMedusaMode);
                break;

            case "let_there_be_light":
                HandleBooleanCheatCommand(argument, "let_there_be_light", GameplayConsoleCheatState.SetLetThereBeLight);
                break;

            case AddPerkCommandName:
                GameplayConsoleCheatState.RegisterCommandUse(AddPerkCommandName);
                HandleAddPerkCommand(argument);
                break;

            case "no_failures":
                HandleBooleanCheatCommand(argument, "no_failures", GameplayConsoleCheatState.SetNoFailures);
                break;

            case "win_game":
                GameplayConsoleCheatState.RegisterCommandUse("win_game");
                HandleWinGameCommand();
                break;

            case "instant_lockpicking":
                HandleBooleanCheatCommand(argument, "instant_lockpicking", GameplayConsoleCheatState.SetInstantLockpicking);
                break;

            case "infinite_lockpicks":
                HandleBooleanCheatCommand(argument, "infinite_lockpicks", GameplayConsoleCheatState.SetInfiniteLockpicks);
                break;

            case "unlock_all":
                GameplayConsoleCheatState.RegisterCommandUse("unlock_all");
                HandleUnlockAllCommand();
                break;

            case "show_active_cheats":
                GameplayConsoleCheatState.RegisterCommandUse("show_active_cheats");
                HandleShowActiveCheatsCommand();
                break;

            default:
                AppendLog($"Unknown code: {trimmedCommand}");
                break;
        }
    }

    // Executes the HandleCreateEquipmentCommand routine.
    private void HandleCreateEquipmentCommand(string requestedEquipmentName)
    {
        if (string.IsNullOrWhiteSpace(requestedEquipmentName))
        {
            AppendLog("Usage: create_equipment_EQUIPMENTNAME");
            return;
        }

        EquipmentItemData item = ResolveEquipmentByDisplayName(requestedEquipmentName);
        if (item == null)
        {
            AppendLog($"Equipment not found: {requestedEquipmentName}");
            return;
        }

        if (playerEquipmentController == null)
        {
            AppendLog("PlayerEquipmentController is not available in this scene.");
            return;
        }

        bool reopenConsoleAfterCommand = consoleVisible && pauseGameplayWhileOpen;
        if (reopenConsoleAfterCommand)
            SetConsoleVisible(false);

        playerEquipmentController.ForceStoreEquipmentFromConsole(item, (success, message) =>
        {
            AppendLog(message);
            if (reopenConsoleAfterCommand && this != null && isActiveAndEnabled)
                SetConsoleVisible(true);
        });
    }

    // Executes the HandleBooleanCheatCommand routine.
    private void HandleBooleanCheatCommand(string argument, string commandName, Action<bool> setter)
    {
        if (!TryParseBooleanArgument(argument, out bool enabled))
        {
            AppendLog($"Usage: {commandName} true|false");
            return;
        }

        GameplayConsoleCheatState.RegisterCommandUse(commandName);
        setter?.Invoke(enabled);
        ApplyCheatState();
        AppendLog($"{commandName} set to {enabled.ToString().ToLowerInvariant()}.");
    }

    // Executes the HandleWinGameCommand routine.
    private void HandleWinGameCommand()
    {
        GameplayMissionController missionController = GetComponent<GameplayMissionController>() ?? FindFirstObjectByType<GameplayMissionController>();
        if (missionController == null)
        {
            AppendLog("GameplayMissionController is not available in this scene.");
            return;
        }

        bool completed = missionController.CompleteAllObjectivesFromConsole();
        AppendLog(completed ? "All objectives completed. Go to exit zone." : "Could not complete objectives.");
    }

    // Executes the HandleUnlockAllCommand routine.
    private void HandleUnlockAllCommand()
    {
        DoorLockState[] doorLocks = FindSceneObjectsIncludingInactive<DoorLockState>();
        int unlockedCount = 0;
        for (int i = 0; i < doorLocks.Length; i++)
        {
            DoorLockState doorLock = doorLocks[i];
            if (doorLock == null || !doorLock.IsLocked)
                continue;

            doorLock.SetLocked(false);
            unlockedCount++;
        }

        AppendLog($"Unlocked doors: {unlockedCount}.");
    }

    // Executes the HandleShowActiveCheatsCommand routine.
    private void HandleShowActiveCheatsCommand()
    {
        AppendLog($"Active cheats: {GameplayConsoleCheatState.BuildActiveCheatsReport()}");
        AppendLog($"Used commands: {GameplayConsoleCheatState.BuildUsedCommandsReport()}");
    }

    // Executes the HandleAddPerkCommand routine.
    private void HandleAddPerkCommand(string requestedPerkName)
    {
        if (string.IsNullOrWhiteSpace(requestedPerkName))
        {
            AppendLog("Usage: add_perk PERKNAME");
            return;
        }

        HideoutPerkDefinition perkDefinition = ResolvePerkByName(requestedPerkName);
        if (perkDefinition == null)
        {
            AppendLog($"Perk not found: {requestedPerkName}");
            return;
        }

        PlayerPerkRuntimeLoadout runtimeLoadout = PlayerPerkRuntimeSession.PeekEquippedPerks();
        if (runtimeLoadout.Contains(perkDefinition))
        {
            AppendLog($"{perkDefinition.PerkName} already equipped.");
            return;
        }

        List<HideoutPerkDefinition> updatedPerks = new(runtimeLoadout.Count + 1);
        IReadOnlyList<HideoutPerkDefinition> equippedPerks = runtimeLoadout.EquippedPerks;
        for (int i = 0; i < equippedPerks.Count; i++)
        {
            HideoutPerkDefinition equippedPerk = equippedPerks[i];
            if (equippedPerk != null)
                updatedPerks.Add(equippedPerk);
        }

        updatedPerks.Add(perkDefinition);
        PlayerPerkRuntimeSession.SetEquippedPerks(updatedPerks);
        ApplyRuntimePerkEffectsForConsole();
        RefreshEquippedPerkPanelsForConsole();
        AppendLog($"{perkDefinition.PerkName} equipped through console.");
    }

    // Executes the RestartCurrentLevel routine.
    private void RestartCurrentLevel()
    {
        AppendLog("Restarting current level.");
        SetConsoleVisible(false);
        PlayerEquipmentRuntimeSession.RestorePreparedQuestLoadoutForReplay();
        PlayerPerkRuntimeSession.RestorePreparedEquippedPerks();
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    // Executes the PrintHelp routine.
    private void PrintHelp()
    {
        EnsureCommandHelpEntries();
        AppendLog("Available console commands:");
        for (int i = 0; i < commandHelpEntries.Count; i++)
        {
            ConsoleCommandHelpEntry entry = commandHelpEntries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.commandSyntax))
                continue;

            string description = string.IsNullOrWhiteSpace(entry.description)
                ? "No description set."
                : entry.description.Trim();
            AppendLog($"{entry.commandSyntax} - {description}");
        }
    }

    // Executes the ApplyCheatState routine.
    private void ApplyCheatState()
    {
        CacheReferences();
        EnsureGhostModeColliderCache();

        if (playerHealth != null)
            playerHealth.SetConsoleInvincibleOverride(GameplayConsoleCheatState.GodMode);

        playerVisionLight?.SetMedusaVisionOverride(GameplayConsoleCheatState.MedusaMode);

        if (GameplayConsoleCheatState.InfiniteReserveAmmo)
            playerWeaponController?.EnsureConsoleAmmoReserveBuffer();

        if (GameplayConsoleCheatState.FocusMode)
            playerFocusController?.RestoreFullFocus();

        if (GameplayConsoleCheatState.AthleteMode)
            playerStaminaController?.RestoreStamina();

        ApplyGhostMode();

        if (GameplayConsoleCheatState.Invisible)
        {
            EnemyVisionAI[] visionControllers = FindSceneObjectsIncludingInactive<EnemyVisionAI>();
            for (int i = 0; i < visionControllers.Length; i++)
                visionControllers[i]?.ClearVisualDetectionForConsoleCheat();
        }

        if (GameplayConsoleCheatState.Lightfooted)
        {
            AIHearing[] hearingControllers = FindSceneObjectsIncludingInactive<AIHearing>();
            for (int i = 0; i < hearingControllers.Length; i++)
                hearingControllers[i]?.ClearAccumulatedDetectionForConsoleCheat();
        }

        ApplyGlobalLightOverride();
    }

    // Executes the ApplyGhostMode routine.
    private void ApplyGhostMode()
    {
        if (cachedGhostModeColliders.Count == 0)
            return;

        for (int i = 0; i < cachedGhostModeColliders.Count; i++)
        {
            Collider2D collider = cachedGhostModeColliders[i];
            if (collider == null)
                continue;

            bool originalEnabled = i < cachedGhostModeColliderStates.Count ? cachedGhostModeColliderStates[i] : collider.enabled;
            collider.enabled = GameplayConsoleCheatState.GhostMode ? false : originalEnabled;
        }
    }

    // Executes the CacheGhostModeColliders routine.
    private void CacheGhostModeColliders()
    {
        cachedGhostModeColliders.Clear();
        cachedGhostModeColliderStates.Clear();

        Transform root = playerRoot != null ? playerRoot : playerMotor != null ? playerMotor.transform : null;
        if (root == null)
            return;

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || collider.isTrigger)
                continue;

            cachedGhostModeColliders.Add(collider);
            cachedGhostModeColliderStates.Add(collider.enabled);
        }

        ghostModeColliderCacheValid = true;
    }

    // Executes the EnsureGhostModeColliderCache routine.
    private void EnsureGhostModeColliderCache()
    {
        if (ghostModeColliderCacheValid && cachedGhostModeColliders.Count > 0)
            return;

        CacheGhostModeColliders();
    }

    // Executes the HandleCheatStateChanged routine.
    private void HandleCheatStateChanged()
    {
        ApplyCheatState();
    }

    // Executes the CanOpenConsole routine.
    private bool CanOpenConsole()
    {
        if (playerHealth != null && (!playerHealth.IsAlive || playerHealth.IsIncapacitated))
            return false;

        return (playerMotor == null || !playerMotor.IsInputBlocked) &&
               (playerWeaponController == null || !playerWeaponController.IsInputBlocked) &&
               (playerUtilityController == null || !playerUtilityController.IsInputBlocked) &&
               (playerMeleeController == null || !playerMeleeController.IsInputBlocked) &&
               (playerPickupInteractor == null || !playerPickupInteractor.IsInputBlocked) &&
               (playerFocusController == null || !playerFocusController.IsInputBlocked) &&
               (playerEquipmentController == null || !playerEquipmentController.IsInputBlocked);
    }

    // Executes the SetConsoleVisible routine.
    private void SetConsoleVisible(bool visible, bool force = false)
    {
        if (!force && consoleVisible == visible)
            return;

        bool wasVisible = consoleVisible;
        consoleVisible = visible;

        if (consoleRoot != null)
            consoleRoot.gameObject.SetActive(visible);

        if (visible)
        {
            commandHistoryCursor = commandHistory.Count;
            commandHistoryAxisEngaged = false;
            if (pauseGameplayWhileOpen)
            {
                cachedTimeScaleBeforeConsole = Time.timeScale > 0f ? Time.timeScale : cachedTimeScaleBeforeConsole;
                Time.timeScale = 0f;
            }

            SetPlayerInputBlocked(true);
            RefreshSystemTimeLabel();
            if (commandInputField != null)
            {
                commandInputField.text = string.Empty;
                commandInputField.ActivateInputField();
                commandInputField.Select();
            }

            return;
        }

        if (pauseGameplayWhileOpen && wasVisible)
            Time.timeScale = Mathf.Approximately(cachedTimeScaleBeforeConsole, 0f) ? 1f : cachedTimeScaleBeforeConsole;

        if (wasVisible)
        {
            SetPlayerInputBlocked(false);
            if (commandInputField != null)
                commandInputField.DeactivateInputField();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // Executes the SetPlayerInputBlocked routine.
    private void SetPlayerInputBlocked(bool blocked)
    {
        playerEquipmentController?.SetInputBlocked(blocked);
        playerMotor?.SetInputBlocked(blocked);
        playerVisionLight?.SetInputBlocked(blocked);
        playerWeaponController?.SetInputBlocked(blocked);
        playerUtilityController?.SetInputBlocked(blocked);
        playerMeleeController?.SetInputBlocked(blocked);
        playerPickupInteractor?.SetInputBlocked(blocked);
        playerFocusController?.SetInputBlocked(blocked);
    }

    // Executes the AddCommandToHistory routine.
    private void AddCommandToHistory(string submittedText)
    {
        string trimmedCommand = submittedText != null ? submittedText.Trim() : string.Empty;
        if (string.IsNullOrEmpty(trimmedCommand))
            return;

        if (commandHistory.Count == 0 || !string.Equals(commandHistory[commandHistory.Count - 1], trimmedCommand, StringComparison.Ordinal))
            commandHistory.Add(trimmedCommand);

        commandHistoryCursor = commandHistory.Count;
    }

    // Executes the HandleCommandHistoryInput routine.
    private void HandleCommandHistoryInput()
    {
        if (inputReader == null || commandInputField == null || commandHistory.Count == 0)
            return;

        float axis = inputReader.GetAxis(commandHistoryVerticalAction);
        if (Mathf.Abs(axis) < 0.01f)
        {
            commandHistoryAxisEngaged = false;
            return;
        }

        if (commandHistoryAxisEngaged || Mathf.Abs(axis) < commandHistoryAxisThreshold)
            return;

        commandHistoryAxisEngaged = true;
        MoveCommandHistory(axis > 0f ? -1 : 1);
    }

    // Executes the MoveCommandHistory routine.
    private void MoveCommandHistory(int direction)
    {
        if (commandInputField == null || commandHistory.Count == 0)
            return;

        commandHistoryCursor = Mathf.Clamp(commandHistoryCursor + direction, 0, commandHistory.Count);
        string commandText = commandHistoryCursor >= 0 && commandHistoryCursor < commandHistory.Count
            ? commandHistory[commandHistoryCursor]
            : string.Empty;

        commandInputField.SetTextWithoutNotify(commandText);
        commandInputField.caretPosition = commandText.Length;
        commandInputField.MoveTextEnd(false);
        commandInputField.ActivateInputField();
        commandInputField.Select();
    }

    // Executes the AppendLog routine.
    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (maxLogEntries > 0 && logEntries.Count >= maxLogEntries)
            logEntries.RemoveAt(0);

        logEntries.Add(FormatLogEntry(message));

        logBuilder.Clear();
        for (int i = 0; i < logEntries.Count; i++)
        {
            if (i > 0)
                logBuilder.AppendLine();

            logBuilder.Append(logEntries[i]);
        }

        if (logText != null)
            logText.text = logBuilder.ToString();

        Canvas.ForceUpdateCanvases();
        if (logScrollRect != null)
            logScrollRect.verticalNormalizedPosition = 0f;
    }

    // Executes the FormatLogEntry routine.
    private string FormatLogEntry(string message)
    {
        return showSystemTime
            ? $"[{DateTime.Now:HH:mm:ss}] {message}"
            : message;
    }

    // Executes the RefreshSystemTimeLabel routine.
    private void RefreshSystemTimeLabel()
    {
        if (systemTimeText == null)
            return;

        systemTimeText.gameObject.SetActive(showSystemTime);
        if (showSystemTime)
            systemTimeText.text = DateTime.Now.ToString("HH:mm:ss");
    }

    // Executes the ResolveEquipmentByDisplayName routine.
    private EquipmentItemData ResolveEquipmentByDisplayName(string requestedName)
    {
        string normalizedName = EquipmentItemRuntimeCatalog.NormalizeLookupKey(requestedName);
        if (string.IsNullOrEmpty(normalizedName))
            return null;

        if (equipmentCatalog == null)
            equipmentCatalog = EquipmentItemRuntimeCatalog.Load();

        EquipmentItemData resolvedItem = equipmentCatalog != null ? equipmentCatalog.FindByDisplayName(normalizedName) : null;
        if (resolvedItem != null)
            return resolvedItem;

        EquipmentItemData[] loadedItems = Resources.FindObjectsOfTypeAll<EquipmentItemData>();
        for (int i = 0; i < loadedItems.Length; i++)
        {
            EquipmentItemData candidate = loadedItems[i];
            if (candidate == null)
                continue;

            if (string.Equals(EquipmentItemRuntimeCatalog.NormalizeLookupKey(candidate.DisplayName), normalizedName, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    // Executes the ResolvePerkByName routine.
    private HideoutPerkDefinition ResolvePerkByName(string requestedName)
    {
        string normalizedName = NormalizePerkLookupKey(requestedName);
        if (string.IsNullOrEmpty(normalizedName))
            return null;

        HideoutPerkDefinition[] resourcePerks = Resources.LoadAll<HideoutPerkDefinition>(string.Empty);
        HideoutPerkDefinition resolvedPerk = ResolvePerkByName(resourcePerks, normalizedName);
        if (resolvedPerk != null)
            return resolvedPerk;

        HideoutPerkDefinition[] loadedPerks = Resources.FindObjectsOfTypeAll<HideoutPerkDefinition>();
        return ResolvePerkByName(loadedPerks, normalizedName);
    }

    // Executes the ResolvePerkByName routine.
    private static HideoutPerkDefinition ResolvePerkByName(IReadOnlyList<HideoutPerkDefinition> candidates, string normalizedName)
    {
        if (candidates == null || string.IsNullOrEmpty(normalizedName))
            return null;

        for (int i = 0; i < candidates.Count; i++)
        {
            HideoutPerkDefinition candidate = candidates[i];
            if (candidate == null)
                continue;

            if (string.Equals(NormalizePerkLookupKey(candidate.PerkName), normalizedName, StringComparison.Ordinal) ||
                string.Equals(NormalizePerkLookupKey(candidate.name), normalizedName, StringComparison.Ordinal) ||
                string.Equals(NormalizePerkLookupKey(candidate.PerkId), normalizedName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    // Executes the NormalizePerkLookupKey routine.
    private static string NormalizePerkLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = value.Trim().ToLowerInvariant().ToCharArray();
        StringBuilder normalized = new(buffer.Length);
        for (int i = 0; i < buffer.Length; i++)
        {
            if (char.IsLetterOrDigit(buffer[i]))
                normalized.Append(buffer[i]);
        }

        return normalized.ToString();
    }

    // Executes the ApplyRuntimePerkEffectsForConsole routine.
    private void ApplyRuntimePerkEffectsForConsole()
    {
        CacheReferences();
        if (playerRoot == null)
            return;

        PlayerPerkEffectController perkEffectController = PlayerPerkEffectController.EnsureOn(playerRoot.gameObject);
        perkEffectController?.ApplyRuntimePerks();
    }

    // Executes the RefreshEquippedPerkPanelsForConsole routine.
    private void RefreshEquippedPerkPanelsForConsole()
    {
        PlayerEquipmentPanelUI[] perkPanels = FindSceneObjectsIncludingInactive<PlayerEquipmentPanelUI>();
        for (int i = 0; i < perkPanels.Length; i++)
        {
            PlayerEquipmentPanelUI perkPanel = perkPanels[i];
            if (perkPanel == null)
                continue;

            perkPanel.RefreshEquippedPerksFromRuntime();
            if (perkPanel.IsVisible)
                perkPanel.SetVisible(true);
        }
    }

    // Executes the TryParseBooleanArgument routine.
    private static bool TryParseBooleanArgument(string argument, out bool enabled)
    {
        switch ((argument ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "on":
            case "yes":
                enabled = true;
                return true;

            case "false":
            case "0":
            case "off":
            case "no":
                enabled = false;
                return true;

            default:
                enabled = false;
                return false;
        }
    }

    // Executes the EnsureEventSystemExists routine.
    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(transform.root, false);
    }

    // Executes the ResolveToggleConsoleRequested routine.
    private bool ResolveToggleConsoleRequested()
    {
        return inputReader != null && inputReader.GetButtonDown(toggleConsoleAction);
    }

    // Executes the EnsureCommandHelpEntries routine.
    private void EnsureCommandHelpEntries()
    {
        commandHelpEntries ??= new List<ConsoleCommandHelpEntry>();

        EnsureCommandHelpEntry(
            "help",
            "help",
            "Lists all available console commands and what they do.");
        EnsureCommandHelpEntry(
            "create_equipment",
            "create_equipment_EQUIPMENTNAME",
            "Creates equipment by its display name and stores it in the player's equipment slots.");
        EnsureCommandHelpEntry(
            "god_mode",
            "god_mode true|false",
            "Turns player invincibility on or off.");
        EnsureCommandHelpEntry(
            "invisible",
            "invisible true|false",
            "Disables enemy vision detection against the player.");
        EnsureCommandHelpEntry(
            "lightfooted",
            "lightfooted true|false",
            "Disables enemy hearing reactions.");
        EnsureCommandHelpEntry(
            "restart_level",
            "restart_level",
            "Reloads the current level.");
        EnsureCommandHelpEntry(
            "noclip",
            "noclip true|false",
            "Gives infinite reserve ammo and throwable uses.");
        EnsureCommandHelpEntry(
            "focus_mode",
            "focus_mode true|false",
            "Restores focus to full and prevents focus from draining.");
        EnsureCommandHelpEntry(
            "athlete_mode",
            "athlete_mode true|false",
            "Restores stamina to full and prevents stamina from draining.");
        EnsureCommandHelpEntry(
            "ghost_mode",
            "ghost_mode true|false",
            "Disables the player's solid colliders so the player can pass through obstacles.");
        EnsureCommandHelpEntry(
            "medusa_mode",
            "medusa_mode true|false",
            "Turns the player vision cone into a 360 degree view or restores the default cone.");
        EnsureCommandHelpEntry(
            "let_there_be_light",
            "let_there_be_light true|false",
            "Enables or disables the _GLOBALLIGHT Light2D.");
        EnsureCommandHelpEntry(
            AddPerkCommandName,
            "add_perk PERKNAME",
            "Equips a perk by ScriptableObject perk name without respecting the normal perk slot cap.");
        EnsureCommandHelpEntry(
            "no_failures",
            "no_failures true|false",
            "Prevents job failure rules from ending the mission.");
        EnsureCommandHelpEntry(
            "win_game",
            "win_game",
            "Completes all mission objectives but still requires entering the exit zone.");
        EnsureCommandHelpEntry(
            "instant_lockpicking",
            "instant_lockpicking true|false",
            "Makes any started lockpicking attempt succeed immediately without lockpicks.");
        EnsureCommandHelpEntry(
            "infinite_lockpicks",
            "infinite_lockpicks true|false",
            "Lets lockpicking start without lockpicks and prevents lockpick use consumption.");
        EnsureCommandHelpEntry(
            "unlock_all",
            "unlock_all",
            "Unlocks every locked door in the current scene.");
        EnsureCommandHelpEntry(
            "show_active_cheats",
            "show_active_cheats",
            "Lists active toggle cheats and command use counts.");
    }

    // Executes the EnsureCommandHelpEntry routine.
    private void EnsureCommandHelpEntry(string commandId, string commandSyntax, string defaultDescription)
    {
        ConsoleCommandHelpEntry existingEntry = null;
        for (int i = 0; i < commandHelpEntries.Count; i++)
        {
            ConsoleCommandHelpEntry entry = commandHelpEntries[i];
            if (entry == null)
                continue;

            if (!string.Equals(entry.commandId, commandId, StringComparison.OrdinalIgnoreCase))
                continue;

            existingEntry = entry;
            break;
        }

        if (existingEntry == null)
        {
            commandHelpEntries.Add(new ConsoleCommandHelpEntry
            {
                commandId = commandId,
                commandSyntax = commandSyntax,
                description = defaultDescription
            });
            return;
        }

        existingEntry.commandId = commandId;
        existingEntry.commandSyntax = commandSyntax;
        if (string.IsNullOrWhiteSpace(existingEntry.description))
            existingEntry.description = defaultDescription;
    }

    // Executes the ApplyGlobalLightOverride routine.
    private void ApplyGlobalLightOverride()
    {
        if (globalLight2D == null)
            globalLight2D = FindGlobalLight2D();

        CacheDefaultGlobalLightState();
        if (globalLight2D == null)
            return;

        if (GameplayConsoleCheatState.LetThereBeLightOverrideInitialized)
        {
            globalLight2D.enabled = GameplayConsoleCheatState.LetThereBeLight;
            return;
        }

        if (defaultGlobalLightStateCached)
            globalLight2D.enabled = defaultGlobalLightEnabled;
    }

    // Executes the CacheDefaultGlobalLightState routine.
    private void CacheDefaultGlobalLightState()
    {
        if (defaultGlobalLightStateCached || globalLight2D == null)
            return;

        defaultGlobalLightEnabled = globalLight2D.enabled;
        defaultGlobalLightStateCached = true;
    }

    // Executes the FindGlobalLight2D routine.
    private static Light2D FindGlobalLight2D()
    {
        Light2D[] lights = FindSceneObjectsIncludingInactive<Light2D>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light2D candidate = lights[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.gameObject.name, "_GLOBALLIGHT", StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    // Executes the CreateRectTransform routine.
    private static RectTransform CreateRectTransform(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject child = new(objectName, typeof(RectTransform));
        RectTransform rectTransform = child.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        return rectTransform;
    }

    // Executes the CreateText routine.
    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        string textValue,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        RectTransform rectTransform = CreateRectTransform(objectName, parent, Vector2.zero, Vector2.one, offsetMin, offsetMax);
        TMP_Text text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        ApplyTextDefaults(text, fontSize, fontStyle, alignment, color);
        text.text = textValue;
        return text;
    }

    // Executes the ApplyTextDefaults routine.
    private static void ApplyTextDefaults(TMP_Text text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        if (text == null)
            return;

        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
    }

    // Executes the CreateScrollbar routine.
    private Scrollbar CreateScrollbar(Transform parent)
    {
        RectTransform scrollbarRect = CreateRectTransform(
            "Scrollbar",
            parent,
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-12f, 0f),
            new Vector2(0f, 0f));
        Image backgroundImage = scrollbarRect.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);

        RectTransform slidingArea = CreateRectTransform(
            "Sliding Area",
            scrollbarRect,
            Vector2.zero,
            Vector2.one,
            new Vector2(2f, 2f),
            new Vector2(-2f, -2f));

        RectTransform handleRect = CreateRectTransform(
            "Handle",
            slidingArea,
            new Vector2(0f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(0f, -42f),
            new Vector2(0f, 42f));
        Image handleImage = handleRect.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.56f, 0.7f, 0.88f, 1f);

        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        return scrollbar;
    }

    // Executes the FindSceneObjectsIncludingInactive routine.
    private static T[] FindSceneObjectsIncludingInactive<T>() where T : UnityEngine.Object
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        List<T> sceneObjects = new(candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate is Component component)
            {
                if (!component.gameObject.scene.IsValid())
                    continue;

                sceneObjects.Add(candidate);
                continue;
            }

            if (candidate is GameObject gameObject)
            {
                if (!gameObject.scene.IsValid())
                    continue;

                sceneObjects.Add(candidate);
            }
        }

        return sceneObjects.ToArray();
    }
}

}
