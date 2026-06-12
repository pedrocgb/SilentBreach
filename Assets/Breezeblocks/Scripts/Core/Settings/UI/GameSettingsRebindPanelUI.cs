using System;
using System.Collections;
using System.Collections.Generic;
using Rewired;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Settings.UI
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Settings/Game Settings Rebind Panel UI")]
public sealed class GameSettingsRebindPanelUI : MonoBehaviour
{
    private readonly struct BindingTarget
    {
        public readonly ControllerMap Map;
        public readonly ActionElementMap ElementMap;

        /// <summary>
        /// Stores one combined keyboard-or-mouse binding target.
        /// </summary>
        public BindingTarget(ControllerMap map, ActionElementMap elementMap)
        {
            Map = map;
            ElementMap = elementMap;
        }
    }

    [FoldoutGroup("Panel")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("Panel")]
    [SerializeField] private Button closeButton;

    [FoldoutGroup("Panel")]
    [SerializeField] private Button restoreDefaultsButton;

    [FoldoutGroup("Rows")]
    [SerializeField] private RectTransform content;

    [FoldoutGroup("Rows"), AssetsOnly]
    [SerializeField] private GameSettingsRebindActionRowUI rowPrefab;

    [FoldoutGroup("Prompt")]
    [SerializeField] private GameObject promptRoot;

    [FoldoutGroup("Prompt")]
    [SerializeField] private TMP_Text promptText;

    [FoldoutGroup("Prompt")]
    [SerializeField] private string listeningText = "Press a key";

    [FoldoutGroup("Feedback")]
    [SerializeField] private GameObject feedbackRoot;

    [FoldoutGroup("Feedback")]
    [SerializeField] private TMP_Text feedbackText;

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId = GameSettingsRuntime.DefaultRewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string actionCategory = "Default";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string mapCategory = "Default";

    [FoldoutGroup("Rewired")]
    [SerializeField] private string mapLayout = "Default";

    [FoldoutGroup("Rewired"), MinValue(1f), SuffixLabel("s", true)]
    [SerializeField] private float listeningTimeout = 8f;

    private readonly List<GameSettingsRebindActionRowUI> rows = new();
    private readonly List<BindingTarget> bindingTargets = new();
    private readonly Dictionary<string, string> previousElementOwners = new(StringComparer.Ordinal);
    private readonly InputMapper keyboardMapper = new();
    private readonly InputMapper mouseMapper = new();

    private Player player;
    private ControllerMap replacementMap;
    private int replacementElementMapId = -1;
    private GameSettingsRebindActionRowUI activeRow;
    private Coroutine populateRowsRoutine;
    private bool listening;

    /// <summary>
    /// Registers panel controls and configures reusable Rewired input mappers.
    /// </summary>
    private void OnEnable()
    {
        ConfigureMappers();

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (restoreDefaultsButton != null)
            restoreDefaultsButton.onClick.AddListener(RestoreDefaults);
    }

    /// <summary>
    /// Stops active listening and unregisters panel controls.
    /// </summary>
    private void OnDisable()
    {
        StopPopulateRowsRoutine();
        StopListening();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (restoreDefaultsButton != null)
            restoreDefaultsButton.onClick.RemoveListener(RestoreDefaults);
    }

    /// <summary>
    /// Polls Escape through Rewired so it cancels but can never become a binding.
    /// </summary>
    private void Update()
    {
        if (!listening || !ReInput.isReady || ReInput.controllers.Keyboard == null)
            return;

        ControllerPollingInfo key = ReInput.controllers.Keyboard.PollForFirstKeyDown();
        if (key.keyboardKey == KeyCode.Escape)
            CancelListening();
    }

    /// <summary>
    /// Opens the panel and builds one row for every user-assignable action.
    /// </summary>
    public void Open()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        HidePrompt();
        HideFeedback();
        StopPopulateRowsRoutine();
        populateRowsRoutine = StartCoroutine(PopulateRowsWhenReady());
    }

    /// <summary>
    /// Cancels remapping, resets transient UI, and closes the assigned panel.
    /// </summary>
    public void Close()
    {
        StopPopulateRowsRoutine();
        CancelListening();
        HideFeedback();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Starts replacing or adding one primary or secondary action binding.
    /// </summary>
    public void BeginRebind(GameSettingsRebindActionRowUI row, int bindingIndex)
    {
        if (row == null || !ResolvePlayer())
        {
            ShowFeedback("Rewired player is not available.");
            return;
        }

        StopListening();
        CollectBindingTargets(row.ActionId, row.ActionRange);
        replacementMap = bindingIndex >= 0 && bindingIndex < bindingTargets.Count
            ? bindingTargets[bindingIndex].Map
            : null;
        replacementElementMapId = bindingIndex >= 0 && bindingIndex < bindingTargets.Count
            ? bindingTargets[bindingIndex].ElementMap.id
            : -1;
        activeRow = row;
        CapturePreviousElementOwners();
        StartCoroutine(StartListeningDelayed());
    }

    /// <summary>
    /// Restores project-default keyboard and mouse maps and saves them immediately.
    /// </summary>
    public void RestoreDefaults()
    {
        if (!ResolvePlayer())
        {
            ShowFeedback("Rewired player is not available.");
            return;
        }

        StopListening();
        player.controllers.maps.LoadDefaultMaps(ControllerType.Keyboard);
        player.controllers.maps.LoadDefaultMaps(ControllerType.Mouse);
        GameSettingsRuntime.SaveCurrentRewiredMaps();
        RefreshRows();
        ShowFeedback("All bindings restored to defaults.");
    }

    /// <summary>
    /// Configures mapper options and event listeners exactly once per enable cycle.
    /// </summary>
    private void ConfigureMappers()
    {
        ConfigureMapper(keyboardMapper);
        ConfigureMapper(mouseMapper);
        mouseMapper.options.ignoreMouseXAxis = true;
        mouseMapper.options.ignoreMouseYAxis = true;
    }

    /// <summary>
    /// Configures one keyboard-or-mouse mapper.
    /// </summary>
    private void ConfigureMapper(InputMapper mapper)
    {
        mapper.RemoveAllEventListeners();
        mapper.options.timeout = Mathf.Max(1f, listeningTimeout);
        mapper.options.allowButtonsOnFullAxisAssignment = false;
        mapper.options.isElementAllowedCallback = IsElementAllowed;
        mapper.InputMappedEvent += HandleInputMapped;
        mapper.ConflictFoundEvent += HandleConflictFound;
        mapper.StoppedEvent += HandleMapperStopped;
        mapper.TimedOutEvent += HandleMapperTimedOut;
    }

    /// <summary>
    /// Resolves the configured Rewired player when available.
    /// </summary>
    private bool ResolvePlayer()
    {
        if (!ReInput.isReady)
        {
            player = null;
            return false;
        }

        player ??= ReInput.players.GetPlayer(rewiredPlayerId);
        return player != null;
    }

    /// <summary>
    /// Waits briefly for Rewired initialization before populating the keybinding list.
    /// </summary>
    private IEnumerator PopulateRowsWhenReady()
    {
        const float timeoutSeconds = 2f;
        float elapsed = 0f;
        while (!ResolvePlayer() && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        populateRowsRoutine = null;
        if (!ResolvePlayer())
        {
            ShowFeedback("Rewired player is not available.");
            yield break;
        }

        int createdRowCount = BuildRows();
        RefreshRows();
        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (createdRowCount <= 0)
            ShowFeedback($"No user-assignable actions were found in category '{actionCategory}'.");
    }

    /// <summary>
    /// Stops a pending keybinding-list population request.
    /// </summary>
    private void StopPopulateRowsRoutine()
    {
        if (populateRowsRoutine == null)
            return;

        StopCoroutine(populateRowsRoutine);
        populateRowsRoutine = null;
    }

    /// <summary>
    /// Creates runtime action rows from the configured Rewired action category.
    /// </summary>
    private int BuildRows()
    {
        ClearRows();
        if (content == null)
        {
            ShowFeedback("Keybinding row content is not assigned.");
            return 0;
        }

        if (rowPrefab == null)
        {
            ShowFeedback("Keybinding row prefab is not assigned.");
            return 0;
        }

        InputCategory category = ReInput.mapping.GetActionCategory(actionCategory);
        if (category == null)
        {
            ShowFeedback($"Rewired action category '{actionCategory}' does not exist.");
            return 0;
        }

        foreach (InputAction action in ReInput.mapping.UserAssignableActionsInCategory(category.id, true))
        {
            if (action == null)
                continue;

            if (action.type == InputActionType.Axis)
            {
                CreateRow(action, AxisRange.Positive, ResolveAxisLabel(action, AxisRange.Positive));
                CreateRow(action, AxisRange.Negative, ResolveAxisLabel(action, AxisRange.Negative));
            }
            else
            {
                CreateRow(action, AxisRange.Positive, ResolveActionLabel(action));
            }
        }

        return rows.Count;
    }

    /// <summary>
    /// Instantiates and configures one action row under the wired content root.
    /// </summary>
    private void CreateRow(InputAction action, AxisRange range, string label)
    {
        GameSettingsRebindActionRowUI row = Instantiate(rowPrefab, content);
        row.Initialize(this, action.id, range, label);
        rows.Add(row);
    }

    /// <summary>
    /// Destroys only rows previously instantiated by this controller.
    /// </summary>
    private void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
                Destroy(rows[i].gameObject);
        }

        rows.Clear();
    }

    /// <summary>
    /// Redraws primary and secondary labels from current combined keyboard and mouse maps.
    /// </summary>
    private void RefreshRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            GameSettingsRebindActionRowUI row = rows[i];
            if (row == null)
                continue;

            CollectBindingTargets(row.ActionId, row.ActionRange);
            string primary = bindingTargets.Count > 0 ? bindingTargets[0].ElementMap.elementIdentifierName : string.Empty;
            string secondary = bindingTargets.Count > 1 ? bindingTargets[1].ElementMap.elementIdentifierName : string.Empty;
            row.SetBindingLabels(primary, secondary);
        }
    }

    /// <summary>
    /// Collects compatible bindings from keyboard first and mouse second.
    /// </summary>
    private void CollectBindingTargets(int actionId, AxisRange range)
    {
        bindingTargets.Clear();
        if (!ResolvePlayer())
            return;

        CollectBindingTargets(player.controllers.maps.GetMap(ControllerType.Keyboard, 0, mapCategory, mapLayout), actionId, range);
        CollectBindingTargets(player.controllers.maps.GetMap(ControllerType.Mouse, 0, mapCategory, mapLayout), actionId, range);
    }

    /// <summary>
    /// Collects compatible bindings from one controller map.
    /// </summary>
    private void CollectBindingTargets(ControllerMap map, int actionId, AxisRange range)
    {
        if (map == null)
            return;

        foreach (ActionElementMap elementMap in map.ElementMapsWithAction(actionId))
        {
            if (elementMap.ShowInField(range))
                bindingTargets.Add(new BindingTarget(map, elementMap));
        }
    }

    /// <summary>
    /// Delays mapper startup so the UI submit click cannot become the new binding.
    /// </summary>
    private IEnumerator StartListeningDelayed()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        if (activeRow == null || !ResolvePlayer())
            yield break;

        ControllerMap keyboardMap = player.controllers.maps.GetMap(ControllerType.Keyboard, 0, mapCategory, mapLayout);
        ControllerMap mouseMap = player.controllers.maps.GetMap(ControllerType.Mouse, 0, mapCategory, mapLayout);
        if (keyboardMap == null || mouseMap == null)
        {
            ShowFeedback("Keyboard or mouse map is missing.");
            yield break;
        }

        listening = true;
        ShowPrompt();
        keyboardMapper.Start(BuildContext(keyboardMap));
        mouseMapper.Start(BuildContext(mouseMap));
    }

    /// <summary>
    /// Builds a mapper context for the active action and one controller map.
    /// </summary>
    private InputMapper.Context BuildContext(ControllerMap controllerMap)
    {
        return new InputMapper.Context
        {
            actionId = activeRow.ActionId,
            actionRange = activeRow.ActionRange,
            controllerMap = controllerMap,
            actionElementMapToReplace = controllerMap == replacementMap
                ? controllerMap.GetElementMap(replacementElementMapId)
                : null
        };
    }

    /// <summary>
    /// Rejects Escape so it remains exclusively reserved for cancellation.
    /// </summary>
    private static bool IsElementAllowed(ControllerPollingInfo pollingInfo)
    {
        return pollingInfo.keyboardKey != KeyCode.Escape;
    }

    /// <summary>
    /// Automatically replaces conflicting user bindings as requested.
    /// </summary>
    private void HandleConflictFound(InputMapper.ConflictFoundEventData data)
    {
        data.responseCallback(InputMapper.ConflictResponse.Replace);
    }

    /// <summary>
    /// Finalizes a successful map, handles cross-device replacement, and saves it.
    /// </summary>
    private void HandleInputMapped(InputMapper.InputMappedEventData data)
    {
        if (!listening)
            return;

        listening = false;
        if (replacementMap != null &&
            data.actionElementMap.controllerMap != replacementMap &&
            replacementMap.ContainsElementMap(replacementElementMapId))
        {
            replacementMap.DeleteElementMap(replacementElementMapId);
        }

        string elementName = data.actionElementMap.elementIdentifierName;
        string previousOwnerKey = BuildElementOwnerKey(elementName);
        StopListening();
        GameSettingsRuntime.SaveCurrentRewiredMaps();
        RefreshRows();

        if (previousElementOwners.TryGetValue(previousOwnerKey, out string previousOwner) &&
            activeRow != null &&
            !string.Equals(previousOwner, ResolveActionLabel(activeRow.ActionId), StringComparison.Ordinal))
        {
            ShowFeedback($"{previousOwner} is now unbound from {elementName}.");
        }
        else
        {
            ShowFeedback($"Bound {elementName}.");
        }
    }

    /// <summary>
    /// Resets prompt state after either mapper stops.
    /// </summary>
    private void HandleMapperStopped(InputMapper.StoppedEventData data)
    {
        if (keyboardMapper.status == InputMapper.Status.Idle && mouseMapper.status == InputMapper.Status.Idle)
        {
            listening = false;
            HidePrompt();
        }
    }

    /// <summary>
    /// Shows timeout feedback when no valid binding was selected.
    /// </summary>
    private void HandleMapperTimedOut(InputMapper.TimedOutEventData data)
    {
        if (!listening)
            return;

        CancelListening();
        ShowFeedback("Binding request timed out.");
    }

    /// <summary>
    /// Cancels the active request without changing any bindings.
    /// </summary>
    private void CancelListening()
    {
        bool wasListening = listening;
        StopListening();
        if (wasListening)
            ShowFeedback("Binding canceled.");
    }

    /// <summary>
    /// Stops both mappers and clears the active replacement target.
    /// </summary>
    private void StopListening()
    {
        listening = false;
        keyboardMapper.Stop();
        mouseMapper.Stop();
        replacementMap = null;
        replacementElementMapId = -1;
        HidePrompt();
    }

    /// <summary>
    /// Captures action ownership before a conflict replacement for useful feedback.
    /// </summary>
    private void CapturePreviousElementOwners()
    {
        previousElementOwners.Clear();
        CapturePreviousElementOwners(player.controllers.maps.GetMap(ControllerType.Keyboard, 0, mapCategory, mapLayout));
        CapturePreviousElementOwners(player.controllers.maps.GetMap(ControllerType.Mouse, 0, mapCategory, mapLayout));
    }

    /// <summary>
    /// Captures element ownership from one controller map.
    /// </summary>
    private void CapturePreviousElementOwners(ControllerMap map)
    {
        if (map == null)
            return;

        foreach (ActionElementMap elementMap in map.AllMaps)
        {
            string key = BuildElementOwnerKey(elementMap.elementIdentifierName);
            previousElementOwners[key] = ResolveActionLabel(elementMap.actionId);
        }
    }

    /// <summary>
    /// Builds a stable lookup key for a displayed controller element.
    /// </summary>
    private static string BuildElementOwnerKey(string elementIdentifierName)
    {
        return elementIdentifierName ?? string.Empty;
    }

    /// <summary>
    /// Resolves an action label by id.
    /// </summary>
    private static string ResolveActionLabel(int actionId)
    {
        InputAction action = ReInput.mapping.GetAction(actionId);
        return ResolveActionLabel(action);
    }

    /// <summary>
    /// Resolves a readable action label.
    /// </summary>
    private static string ResolveActionLabel(InputAction action)
    {
        if (action == null)
            return "Unknown Action";

        return string.IsNullOrWhiteSpace(action.descriptiveName) ? action.name : action.descriptiveName;
    }

    /// <summary>
    /// Resolves a readable positive or negative axis label.
    /// </summary>
    private static string ResolveAxisLabel(InputAction action, AxisRange range)
    {
        if (action == null)
            return "Unknown Action";

        string label = range == AxisRange.Negative ? action.negativeDescriptiveName : action.positiveDescriptiveName;
        if (!string.IsNullOrWhiteSpace(label))
            return label;

        return $"{ResolveActionLabel(action)} {(range == AxisRange.Negative ? "-" : "+")}";
    }

    /// <summary>
    /// Displays the listening prompt.
    /// </summary>
    private void ShowPrompt()
    {
        if (promptText != null)
            promptText.text = listeningText ?? string.Empty;

        if (promptRoot != null)
            promptRoot.SetActive(true);
    }

    /// <summary>
    /// Hides the listening prompt.
    /// </summary>
    private void HidePrompt()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    /// <summary>
    /// Displays conflict, cancellation, or completion feedback.
    /// </summary>
    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message ?? string.Empty;

        if (feedbackRoot != null)
            feedbackRoot.SetActive(true);
    }

    /// <summary>
    /// Hides the optional feedback window.
    /// </summary>
    private void HideFeedback()
    {
        if (feedbackRoot != null)
            feedbackRoot.SetActive(false);
    }
}

}
