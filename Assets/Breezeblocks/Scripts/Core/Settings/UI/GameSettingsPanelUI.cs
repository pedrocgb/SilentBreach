using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Settings.UI
{

[Serializable]
public sealed class SettingsDropdownReference
{
    public TMP_Dropdown Dropdown;
    public TMP_Text ExplanationText;
}

[Serializable]
public sealed class SettingsSliderReference
{
    public Slider Slider;
    public TMP_Text ValueText;
    public TMP_Text ExplanationText;
}

[Serializable]
public sealed class SettingsToggleReference
{
    public Toggle Toggle;
    public TMP_Text ExplanationText;
}

[Serializable]
public sealed class SettingsButtonReference
{
    public Button Button;
    public TMP_Text ExplanationText;
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Settings/Game Settings Panel UI")]
public sealed class GameSettingsPanelUI : MonoBehaviour
{
    private const float MinimumVolumePercent = 0f;
    private const float MaximumVolumePercent = 100f;

    private static readonly List<string> LanguageCodes = new() { GameSettingsSaveData.DefaultLanguageCode };
    private static readonly List<string> LanguageLabels = new() { "Português (Brasil)" };

    [FoldoutGroup("Panel")]
    [Tooltip("Panel controlled by Open and Close. This may be separate from the GameObject containing this script.")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("Panel")]
    [Tooltip("Optional button that opens the settings panel.")]
    [SerializeField] private Button openButton;

    [FoldoutGroup("Panel")]
    [Tooltip("Optional button that closes the settings panel.")]
    [SerializeField] private Button closeButton;

    [FoldoutGroup("Panel")]
    [MinValue(0f), SuffixLabel("s", true)]
    [Tooltip("Unscaled duration used when opening or closing the settings panel.")]
    [SerializeField] private float fadeDuration = 0.2f;

    [FoldoutGroup("Panel")]
    [SerializeField] private Ease fadeEase = Ease.OutQuad;

    [FoldoutGroup("Panel")]
    [SerializeField] private List<GameSettingsTabButtonUI> tabs = new();

    [FoldoutGroup("Display")]
    [SerializeField] private SettingsDropdownReference resolution = new();

    [FoldoutGroup("Display")]
    [SerializeField] private SettingsDropdownReference monitor = new();

    [FoldoutGroup("Display")]
    [SerializeField] private SettingsToggleReference vSync = new();

    [FoldoutGroup("Display")]
    [SerializeField] private SettingsSliderReference brightness = new();

    [FoldoutGroup("Display")]
    [SerializeField] private SettingsToggleReference screenshake = new();

    [FoldoutGroup("Display")]
    [SerializeField] private List<GameSettingsFrameRateButtonUI> frameRateButtons = new();

    [FoldoutGroup("Audio")]
    [SerializeField] private SettingsSliderReference masterVolume = new();

    [FoldoutGroup("Audio")]
    [SerializeField] private SettingsSliderReference musicVolume = new();

    [FoldoutGroup("Audio")]
    [SerializeField] private SettingsSliderReference sfxVolume = new();

    [FoldoutGroup("Audio")]
    [SerializeField] private SettingsSliderReference uiVolume = new();

    [FoldoutGroup("Audio")]
    [SerializeField] private SettingsSliderReference ambientVolume = new();

    [FoldoutGroup("Controls")]
    [SerializeField] private SettingsButtonReference openKeyRebinding = new();

    [FoldoutGroup("Controls")]
    [SerializeField] private GameSettingsRebindPanelUI rebindPanel;

    [FoldoutGroup("Controls")]
    [SerializeField] private SettingsSliderReference mouseSensitivity = new();

    [FoldoutGroup("Controls")]
    [SerializeField] private SettingsSliderReference aimSensitivity = new();

    [FoldoutGroup("Controls")]
    [SerializeField] private SettingsToggleReference toggleAim = new();

    [FoldoutGroup("Controls")]
    [SerializeField] private SettingsToggleReference toggleSprint = new();

    [FoldoutGroup("Controls")]
    [SerializeField] private SettingsToggleReference toggleDragBody = new();

    [FoldoutGroup("Gameplay")]
    [SerializeField] private SettingsDropdownReference language = new();

    [FoldoutGroup("Gameplay")]
    [SerializeField] private SettingsButtonReference restoreDefaults = new();

    private readonly List<DisplayInfo> displays = new();
    private readonly List<Resolution> resolutions = new();
    private readonly HashSet<long> resolutionKeys = new();
    private readonly List<string> optionLabels = new();
    private CanvasGroup panelCanvasGroup;
    private Tween panelFadeTween;
    private bool listenersRegistered;

    /// <summary>
    /// Caches the CanvasGroup owned by the externally assigned panel root.
    /// </summary>
    private void Awake()
    {
        ResolvePanelCanvasGroup();
        ConfigureVolumeSliders();
    }

    /// <summary>
    /// Registers UI listeners and refreshes controls from persisted settings.
    /// </summary>
    private void OnEnable()
    {
        ResolvePanelCanvasGroup();
        ConfigureVolumeSliders();
        GameSettingsRuntime.EnsureInitialized();
        RegisterListeners();
        RefreshFromSavedSettings();
    }

    /// <summary>
    /// Removes UI listeners when the controller is disabled.
    /// </summary>
    private void OnDisable()
    {
        KillPanelFade();
        UnregisterListeners();
    }

    /// <summary>
    /// Opens the assigned panel and refreshes every displayed value.
    /// </summary>
    public void Open()
    {
        if (panelRoot == null)
            return;

        ResolvePanelCanvasGroup();
        KillPanelFade();
        panelRoot.SetActive(true);
        RefreshFromSavedSettings();

        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        if (fadeDuration <= 0f)
        {
            CompletePanelOpen();
            return;
        }

        panelFadeTween = panelCanvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(fadeEase)
            .SetUpdate(true)
            .OnComplete(CompletePanelOpen);
    }

    /// <summary>
    /// Fades out and closes the assigned panel without changing saved preferences.
    /// </summary>
    public void Close()
    {
        if (panelRoot == null)
            return;

        ResolvePanelCanvasGroup();
        KillPanelFade();

        if (panelCanvasGroup == null || fadeDuration <= 0f)
        {
            CompletePanelClose();
            return;
        }

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        panelFadeTween = panelCanvasGroup
            .DOFade(0f, fadeDuration)
            .SetEase(fadeEase)
            .SetUpdate(true)
            .OnComplete(CompletePanelClose);
    }

    /// <summary>
    /// Resolves the optional CanvasGroup from the panel root without requiring another serialized reference.
    /// </summary>
    private void ResolvePanelCanvasGroup()
    {
        panelCanvasGroup = panelRoot != null ? panelRoot.GetComponent<CanvasGroup>() : null;
    }

    /// <summary>
    /// Restores the fully open visual and interaction state after a fade completes.
    /// </summary>
    private void CompletePanelOpen()
    {
        panelFadeTween = null;
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// Restores the closed visual state before deactivating the panel root.
    /// </summary>
    private void CompletePanelClose()
    {
        panelFadeTween = null;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Stops an interrupted panel transition before another transition starts.
    /// </summary>
    private void KillPanelFade()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;
    }

    /// <summary>
    /// Configures every audio slider to emit the zero-to-one-hundred percentages expected by the AudioMixer integration.
    /// </summary>
    private void ConfigureVolumeSliders()
    {
        ConfigureVolumeSlider(masterVolume);
        ConfigureVolumeSlider(musicVolume);
        ConfigureVolumeSlider(sfxVolume);
        ConfigureVolumeSlider(uiVolume);
        ConfigureVolumeSlider(ambientVolume);
    }

    /// <summary>
    /// Configures one optional audio slider without requiring its range to be maintained in the scene.
    /// </summary>
    private static void ConfigureVolumeSlider(SettingsSliderReference reference)
    {
        Slider slider = reference?.Slider;
        if (slider == null)
            return;

        slider.minValue = MinimumVolumePercent;
        slider.maxValue = MaximumVolumePercent;
        slider.wholeNumbers = false;
    }

    /// <summary>
    /// Rebuilds dynamic options and updates controls without invoking save callbacks.
    /// </summary>
    public void RefreshFromSavedSettings()
    {
        GameSettingsSaveData settings = GameSettingsRuntime.Current;
        RefreshMonitorOptions(settings);
        RefreshResolutionOptions(settings);
        RefreshLanguageOptions(settings);

        SetToggleWithoutNotify(vSync, settings.VSyncEnabled);
        SetSliderWithoutNotify(brightness, settings.Brightness, "0.00");
        SetToggleWithoutNotify(screenshake, settings.ScreenshakeEnabled);
        SetSliderWithoutNotify(masterVolume, settings.MasterVolume, "0");
        SetSliderWithoutNotify(musicVolume, settings.MusicVolume, "0");
        SetSliderWithoutNotify(sfxVolume, settings.SfxVolume, "0");
        SetSliderWithoutNotify(uiVolume, settings.UiVolume, "0");
        SetSliderWithoutNotify(ambientVolume, settings.AmbientVolume, "0");
        SetSliderWithoutNotify(mouseSensitivity, settings.MouseSensitivity, "0.00");
        SetSliderWithoutNotify(aimSensitivity, settings.AimSensitivity, "0.00");
        SetToggleWithoutNotify(toggleAim, settings.ToggleAim);
        SetToggleWithoutNotify(toggleSprint, settings.ToggleSprint);
        SetToggleWithoutNotify(toggleDragBody, settings.ToggleDragBody);
        RefreshFrameRateButtons(settings.FrameRateLimit);

        if (tabs.Count > 0)
            SelectTab(tabs[0]);
    }

    /// <summary>
    /// Registers all wired control callbacks once.
    /// </summary>
    private void RegisterListeners()
    {
        if (listenersRegistered)
            return;

        listenersRegistered = true;
        GameSettingsRuntime.SettingsChanged += HandleRuntimeSettingsChanged;

        if (openButton != null)
            openButton.onClick.AddListener(Open);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        AddDropdownListener(monitor, HandleMonitorChanged);
        AddDropdownListener(resolution, HandleResolutionChanged);
        AddToggleListener(vSync, GameSettingsRuntime.SetVSync);
        AddSliderListener(brightness, HandleBrightnessChanged);
        AddToggleListener(screenshake, GameSettingsRuntime.SetScreenshake);
        AddSliderListener(masterVolume, HandleMasterVolumeChanged);
        AddSliderListener(musicVolume, HandleMusicVolumeChanged);
        AddSliderListener(sfxVolume, HandleSfxVolumeChanged);
        AddSliderListener(uiVolume, HandleUiVolumeChanged);
        AddSliderListener(ambientVolume, HandleAmbientVolumeChanged);
        AddSliderListener(mouseSensitivity, HandleMouseSensitivityChanged);
        AddSliderListener(aimSensitivity, HandleAimSensitivityChanged);
        AddToggleListener(toggleAim, GameSettingsRuntime.SetToggleAim);
        AddToggleListener(toggleSprint, GameSettingsRuntime.SetToggleSprint);
        AddToggleListener(toggleDragBody, GameSettingsRuntime.SetToggleDragBody);
        AddDropdownListener(language, HandleLanguageChanged);

        if (openKeyRebinding?.Button != null)
            openKeyRebinding.Button.onClick.AddListener(HandleOpenKeyRebinding);

        if (restoreDefaults?.Button != null)
            restoreDefaults.Button.onClick.AddListener(HandleRestoreDefaults);

        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null)
                tabs[i].Selected += SelectTab;
        }

        for (int i = 0; i < frameRateButtons.Count; i++)
        {
            if (frameRateButtons[i] != null)
                frameRateButtons[i].Selected += HandleFrameRateSelected;
        }
    }

    /// <summary>
    /// Removes all callbacks previously registered by this controller.
    /// </summary>
    private void UnregisterListeners()
    {
        if (!listenersRegistered)
            return;

        listenersRegistered = false;
        GameSettingsRuntime.SettingsChanged -= HandleRuntimeSettingsChanged;

        if (openButton != null)
            openButton.onClick.RemoveListener(Open);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        RemoveDropdownListener(monitor, HandleMonitorChanged);
        RemoveDropdownListener(resolution, HandleResolutionChanged);
        RemoveToggleListener(vSync, GameSettingsRuntime.SetVSync);
        RemoveSliderListener(brightness, HandleBrightnessChanged);
        RemoveToggleListener(screenshake, GameSettingsRuntime.SetScreenshake);
        RemoveSliderListener(masterVolume, HandleMasterVolumeChanged);
        RemoveSliderListener(musicVolume, HandleMusicVolumeChanged);
        RemoveSliderListener(sfxVolume, HandleSfxVolumeChanged);
        RemoveSliderListener(uiVolume, HandleUiVolumeChanged);
        RemoveSliderListener(ambientVolume, HandleAmbientVolumeChanged);
        RemoveSliderListener(mouseSensitivity, HandleMouseSensitivityChanged);
        RemoveSliderListener(aimSensitivity, HandleAimSensitivityChanged);
        RemoveToggleListener(toggleAim, GameSettingsRuntime.SetToggleAim);
        RemoveToggleListener(toggleSprint, GameSettingsRuntime.SetToggleSprint);
        RemoveToggleListener(toggleDragBody, GameSettingsRuntime.SetToggleDragBody);
        RemoveDropdownListener(language, HandleLanguageChanged);

        if (openKeyRebinding?.Button != null)
            openKeyRebinding.Button.onClick.RemoveListener(HandleOpenKeyRebinding);

        if (restoreDefaults?.Button != null)
            restoreDefaults.Button.onClick.RemoveListener(HandleRestoreDefaults);

        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null)
                tabs[i].Selected -= SelectTab;
        }

        for (int i = 0; i < frameRateButtons.Count; i++)
        {
            if (frameRateButtons[i] != null)
                frameRateButtons[i].Selected -= HandleFrameRateSelected;
        }
    }

    /// <summary>
    /// Rebuilds connected-monitor options and enables selection only when useful.
    /// </summary>
    private void RefreshMonitorOptions(GameSettingsSaveData settings)
    {
        TMP_Dropdown dropdown = monitor?.Dropdown;
        if (dropdown == null)
            return;

        GameSettingsRuntime.GetDisplayLayout(displays);
        optionLabels.Clear();
        for (int i = 0; i < displays.Count; i++)
        {
            DisplayInfo display = displays[i];
            optionLabels.Add(string.IsNullOrWhiteSpace(display.name)
                ? $"Monitor {i + 1} ({display.width}x{display.height})"
                : $"{display.name} ({display.width}x{display.height})");
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(optionLabels);
        dropdown.interactable = displays.Count > 1;
        dropdown.SetValueWithoutNotify(displays.Count > 0 ? Mathf.Clamp(settings.MonitorIndex, 0, displays.Count - 1) : 0);
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// Rebuilds unique resolution options for the currently selected monitor.
    /// </summary>
    private void RefreshResolutionOptions(GameSettingsSaveData settings)
    {
        TMP_Dropdown dropdown = resolution?.Dropdown;
        if (dropdown == null)
            return;

        GameSettingsRuntime.GetResolutionsForMonitor(settings.MonitorIndex, resolutions);
        resolutions.Sort(CompareResolutions);
        resolutionKeys.Clear();
        optionLabels.Clear();

        int selectedIndex = 0;
        int writeIndex = 0;
        for (int i = 0; i < resolutions.Count; i++)
        {
            Resolution candidate = resolutions[i];
            long key = ((long)candidate.width << 32) | (uint)candidate.height;
            if (!resolutionKeys.Add(key))
                continue;

            resolutions[writeIndex] = candidate;
            optionLabels.Add($"{candidate.width} x {candidate.height}");
            if (candidate.width == settings.ResolutionWidth && candidate.height == settings.ResolutionHeight)
                selectedIndex = writeIndex;

            writeIndex++;
        }

        if (writeIndex < resolutions.Count)
            resolutions.RemoveRange(writeIndex, resolutions.Count - writeIndex);

        dropdown.ClearOptions();
        dropdown.AddOptions(optionLabels);
        dropdown.interactable = resolutions.Count > 1;
        dropdown.SetValueWithoutNotify(resolutions.Count > 0 ? Mathf.Clamp(selectedIndex, 0, resolutions.Count - 1) : 0);
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// Rebuilds language options from currently supported language identifiers.
    /// </summary>
    private void RefreshLanguageOptions(GameSettingsSaveData settings)
    {
        TMP_Dropdown dropdown = language?.Dropdown;
        if (dropdown == null)
            return;

        int selectedIndex = Mathf.Max(0, LanguageCodes.IndexOf(settings.LanguageCode));
        dropdown.ClearOptions();
        dropdown.AddOptions(LanguageLabels);
        dropdown.SetValueWithoutNotify(selectedIndex);
        dropdown.RefreshShownValue();
    }

    /// <summary>
    /// Selects one settings tab and hides every other tab.
    /// </summary>
    private void SelectTab(GameSettingsTabButtonUI selectedTab)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] != null)
                tabs[i].SetSelected(tabs[i] == selectedTab);
        }
    }

    /// <summary>
    /// Handles a selected monitor and rebuilds its available resolutions.
    /// </summary>
    private void HandleMonitorChanged(int index)
    {
        GameSettingsRuntime.SetMonitor(index);
        RefreshResolutionOptions(GameSettingsRuntime.Current);
    }

    /// <summary>
    /// Handles a selected resolution.
    /// </summary>
    private void HandleResolutionChanged(int index)
    {
        if (index < 0 || index >= resolutions.Count)
            return;

        Resolution selected = resolutions[index];
        GameSettingsRuntime.SetResolution(selected.width, selected.height);
    }

    /// <summary>
    /// Applies and displays a brightness slider change.
    /// </summary>
    private void HandleBrightnessChanged(float value)
    {
        UpdateSliderValueText(brightness, value, "0.00");
        GameSettingsRuntime.SetBrightness(value);
    }

    /// <summary>
    /// Applies and displays a master-volume slider change.
    /// </summary>
    private void HandleMasterVolumeChanged(float value)
    {
        UpdateSliderValueText(masterVolume, value, "0");
        GameSettingsRuntime.SetMasterVolume(value);
    }

    /// <summary>
    /// Applies and displays a music-volume slider change.
    /// </summary>
    private void HandleMusicVolumeChanged(float value)
    {
        UpdateSliderValueText(musicVolume, value, "0");
        GameSettingsRuntime.SetMusicVolume(value);
    }

    /// <summary>
    /// Applies and displays a sound-effects-volume slider change.
    /// </summary>
    private void HandleSfxVolumeChanged(float value)
    {
        UpdateSliderValueText(sfxVolume, value, "0");
        GameSettingsRuntime.SetSfxVolume(value);
    }

    /// <summary>
    /// Applies and displays a UI-volume slider change.
    /// </summary>
    private void HandleUiVolumeChanged(float value)
    {
        UpdateSliderValueText(uiVolume, value, "0");
        GameSettingsRuntime.SetUiVolume(value);
    }

    /// <summary>
    /// Applies and displays an ambient-volume slider change.
    /// </summary>
    private void HandleAmbientVolumeChanged(float value)
    {
        UpdateSliderValueText(ambientVolume, value, "0");
        GameSettingsRuntime.SetAmbientVolume(value);
    }

    /// <summary>
    /// Applies and displays a mouse-sensitivity slider change.
    /// </summary>
    private void HandleMouseSensitivityChanged(float value)
    {
        UpdateSliderValueText(mouseSensitivity, value, "0.00");
        GameSettingsRuntime.SetMouseSensitivity(value);
    }

    /// <summary>
    /// Applies and displays an aim-sensitivity slider change.
    /// </summary>
    private void HandleAimSensitivityChanged(float value)
    {
        UpdateSliderValueText(aimSensitivity, value, "0.00");
        GameSettingsRuntime.SetAimSensitivity(value);
    }

    /// <summary>
    /// Applies a selected language identifier.
    /// </summary>
    private void HandleLanguageChanged(int index)
    {
        if (index >= 0 && index < LanguageCodes.Count)
            GameSettingsRuntime.SetLanguage(LanguageCodes[index]);
    }

    /// <summary>
    /// Opens the separately wired key-rebinding panel.
    /// </summary>
    private void HandleOpenKeyRebinding()
    {
        if (rebindPanel != null)
            rebindPanel.Open();
    }

    /// <summary>
    /// Restores every setting to defaults and refreshes the menu.
    /// </summary>
    private void HandleRestoreDefaults()
    {
        GameSettingsRuntime.RestoreDefaults();
        RefreshFromSavedSettings();
    }

    /// <summary>
    /// Applies the selected frame-rate option.
    /// </summary>
    private void HandleFrameRateSelected(GameSettingsFrameRateButtonUI selectedButton)
    {
        if (selectedButton == null)
            return;

        GameSettingsRuntime.SetFrameRateLimit(selectedButton.FrameRateLimit);
        RefreshFrameRateButtons((int)selectedButton.FrameRateLimit);
    }

    /// <summary>
    /// Refreshes visual state after settings are changed outside this panel.
    /// </summary>
    private void HandleRuntimeSettingsChanged()
    {
        RefreshFrameRateButtons(GameSettingsRuntime.Current.FrameRateLimit);
    }

    /// <summary>
    /// Updates selected feedback on all frame-rate buttons.
    /// </summary>
    private void RefreshFrameRateButtons(int selectedFrameRate)
    {
        for (int i = 0; i < frameRateButtons.Count; i++)
        {
            GameSettingsFrameRateButtonUI button = frameRateButtons[i];
            if (button != null)
                button.SetSelected((int)button.FrameRateLimit == selectedFrameRate);
        }
    }

    /// <summary>
    /// Sets a toggle value without invoking its save callback.
    /// </summary>
    private static void SetToggleWithoutNotify(SettingsToggleReference reference, bool value)
    {
        reference?.Toggle?.SetIsOnWithoutNotify(value);
    }

    /// <summary>
    /// Sets a slider value and optional display text without invoking its save callback.
    /// </summary>
    private static void SetSliderWithoutNotify(SettingsSliderReference reference, float value, string format)
    {
        if (reference?.Slider != null)
            reference.Slider.SetValueWithoutNotify(value);

        UpdateSliderValueText(reference, value, format);
    }

    /// <summary>
    /// Updates an optional slider value label.
    /// </summary>
    private static void UpdateSliderValueText(SettingsSliderReference reference, float value, string format)
    {
        if (reference?.ValueText != null)
            reference.ValueText.text = value.ToString(format);
    }

    /// <summary>
    /// Adds a dropdown listener when its control is wired.
    /// </summary>
    private static void AddDropdownListener(SettingsDropdownReference reference, UnityEngine.Events.UnityAction<int> callback)
    {
        reference?.Dropdown?.onValueChanged.AddListener(callback);
    }

    /// <summary>
    /// Removes a dropdown listener when its control is wired.
    /// </summary>
    private static void RemoveDropdownListener(SettingsDropdownReference reference, UnityEngine.Events.UnityAction<int> callback)
    {
        reference?.Dropdown?.onValueChanged.RemoveListener(callback);
    }

    /// <summary>
    /// Adds a slider listener when its control is wired.
    /// </summary>
    private static void AddSliderListener(SettingsSliderReference reference, UnityEngine.Events.UnityAction<float> callback)
    {
        reference?.Slider?.onValueChanged.AddListener(callback);
    }

    /// <summary>
    /// Removes a slider listener when its control is wired.
    /// </summary>
    private static void RemoveSliderListener(SettingsSliderReference reference, UnityEngine.Events.UnityAction<float> callback)
    {
        reference?.Slider?.onValueChanged.RemoveListener(callback);
    }

    /// <summary>
    /// Adds a toggle listener when its control is wired.
    /// </summary>
    private static void AddToggleListener(SettingsToggleReference reference, UnityEngine.Events.UnityAction<bool> callback)
    {
        reference?.Toggle?.onValueChanged.AddListener(callback);
    }

    /// <summary>
    /// Removes a toggle listener when its control is wired.
    /// </summary>
    private static void RemoveToggleListener(SettingsToggleReference reference, UnityEngine.Events.UnityAction<bool> callback)
    {
        reference?.Toggle?.onValueChanged.RemoveListener(callback);
    }

    /// <summary>
    /// Orders resolutions by pixel area, then width and height.
    /// </summary>
    private static int CompareResolutions(Resolution left, Resolution right)
    {
        long leftArea = (long)left.width * left.height;
        long rightArea = (long)right.width * right.height;
        int areaComparison = leftArea.CompareTo(rightArea);
        if (areaComparison != 0)
            return areaComparison;

        int widthComparison = left.width.CompareTo(right.width);
        return widthComparison != 0 ? widthComparison : left.height.CompareTo(right.height);
    }
}

}
