using System.Collections;
using Breezeblocks.HideoutSystem;
using Breezeblocks.Input;
using Breezeblocks.Settings;
using Breezeblocks.Settings.UI;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/UI/Gameplay Pause Menu UI")]
public sealed class GameplayPauseMenuUI : MonoBehaviour
{
    private const string DefaultPauseAction = "Pause";

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId = GameSettingsRuntime.DefaultRewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string pauseAction = DefaultPauseAction;

    [FoldoutGroup("Panel")]
    [SerializeField] private GameObject panelRoot;

    [FoldoutGroup("Panel"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float panelFadeDuration = 0.18f;

    [FoldoutGroup("Panel")]
    [SerializeField] private Ease panelFadeEase = Ease.OutQuad;

    [FoldoutGroup("Panel/Buttons")]
    [SerializeField] private Button closeButton;

    [FoldoutGroup("Panel/Buttons")]
    [SerializeField] private Button settingsButton;

    [FoldoutGroup("Panel/Buttons")]
    [SerializeField] private Button quitButton;

    [FoldoutGroup("Quit Prompt")]
    [SerializeField] private GameObject quitPromptRoot;

    [FoldoutGroup("Quit Prompt/Buttons")]
    [SerializeField] private Button quitConfirmButton;

    [FoldoutGroup("Quit Prompt/Buttons")]
    [SerializeField] private Button quitCloseButton;

    [FoldoutGroup("Settings")]
    [SerializeField] private GameSettingsPanelUI settingsPanelUi;

    [FoldoutGroup("Scene Loading")]
    [SerializeField] private UiImageFader sceneFadeFader;

    [FoldoutGroup("Scene Loading"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float sceneFadeDuration = 0.35f;

    [FoldoutGroup("Scene Loading"), LabelText("Hideout Scene Build Index"), MinValue(-1)]
    [SerializeField] private int hideoutSceneBuildIndex;

    [FoldoutGroup("Scene Loading"), LabelText("Hideout Scene Fallback Name")]
    [SerializeField] private string hideoutSceneName = "Hideout";

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPaused => isPaused;

    private IPlayerInputReader inputReader;
    private CanvasGroup panelCanvasGroup;
    private CanvasGroup quitPromptCanvasGroup;
    private Tween panelFadeTween;
    private Tween quitPromptFadeTween;
    private bool isPaused;
    private bool sceneTransitionInProgress;
    private CursorLockMode cachedCursorLockMode;

    /// <summary>
    /// Caches panel references, prepares closed UI state, and registers button callbacks.
    /// </summary>
    private void Awake()
    {
        ResolveReferences();
        inputReader = new RewiredPlayerInputReader(rewiredPlayerId);
        RegisterButtonCallbacks();
        SetPanelStateImmediate(false);
        SetQuitPromptStateImmediate(false);
    }

    /// <summary>
    /// Refreshes references whenever this controller becomes active.
    /// </summary>
    private void OnEnable()
    {
        ResolveReferences();
        RegisterButtonCallbacks();
    }

    /// <summary>
    /// Restores runtime pause state and removes callbacks when disabled.
    /// </summary>
    private void OnDisable()
    {
        UnregisterButtonCallbacks();
        KillTweens();

        if (isPaused && !sceneTransitionInProgress)
            RestoreGameplayStateAfterPause();
    }

    /// <summary>
    /// Polls the configured Rewired pause action.
    /// </summary>
    private void Update()
    {
        if (sceneTransitionInProgress)
            return;

        if (isPaused)
            ApplyPausedTimeScale();

        inputReader ??= new RewiredPlayerInputReader(rewiredPlayerId);
        if (inputReader == null || !inputReader.IsReady)
            return;

        if (inputReader.GetButtonDown(pauseAction))
            TogglePause();
    }

    /// <summary>
    /// Opens or closes the pause menu depending on current state.
    /// </summary>
    public void TogglePause()
    {
        if (isPaused)
        {
            ClosePauseMenu();
            return;
        }

        OpenPauseMenu();
    }

    /// <summary>
    /// Opens the pause menu if no other system already paused time.
    /// </summary>
    public void OpenPauseMenu()
    {
        if (sceneTransitionInProgress || isPaused || panelRoot == null)
            return;

        ResolveReferences();
        isPaused = true;
        cachedCursorLockMode = Cursor.lockState;
        ApplyPausedTimeScale();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetQuitPromptStateImmediate(false);
        FadePanel(true);
    }

    /// <summary>
    /// Closes the pause menu and resumes gameplay.
    /// </summary>
    public void ClosePauseMenu()
    {
        if (!isPaused || sceneTransitionInProgress)
            return;

        settingsPanelUi?.Close();
        FadeQuitPrompt(false);
        RestoreGameplayStateAfterPause();
        FadePanel(false);
    }

    /// <summary>
    /// Opens the shared game settings panel while the pause menu remains active.
    /// </summary>
    private void OpenSettingsPanel()
    {
        settingsPanelUi?.Open();
    }

    /// <summary>
    /// Opens the quit confirmation prompt.
    /// </summary>
    private void OpenQuitPrompt()
    {
        FadeQuitPrompt(true);
    }

    /// <summary>
    /// Closes the quit confirmation prompt.
    /// </summary>
    private void CloseQuitPrompt()
    {
        FadeQuitPrompt(false);
    }

    /// <summary>
    /// Saves runtime data, fades to black, and loads the configured hideout scene.
    /// </summary>
    private void ConfirmQuitToHideout()
    {
        if (sceneTransitionInProgress)
            return;

        sceneTransitionInProgress = true;
        StartCoroutine(QuitToHideoutRoutine());
    }

    /// <summary>
    /// Performs the unpaused quit transition after confirmation.
    /// </summary>
    private IEnumerator QuitToHideoutRoutine()
    {
        settingsPanelUi?.Close();
        SetQuitPromptStateImmediate(false);
        ApplyUnpausedTimeScale();
        PersistBeforeQuit();

        Tween fadeTween = sceneFadeFader != null ? sceneFadeFader.FadeIn(sceneFadeDuration) : null;
        if (fadeTween != null)
            yield return fadeTween.WaitForCompletion();

        HideoutRuntimeSession.ClearActiveMissionJob();
        if (SceneLoadUtility.TryLoadScene(hideoutSceneBuildIndex, hideoutSceneName))
            yield break;

        sceneTransitionInProgress = false;
        isPaused = false;
        Tween fadeOutTween = sceneFadeFader != null ? sceneFadeFader.FadeOut(sceneFadeDuration) : null;
        if (fadeOutTween != null)
            yield return fadeOutTween.WaitForCompletion();

        OpenPauseMenu();
    }

    /// <summary>
    /// Forces current save systems to flush their latest data before scene loading.
    /// </summary>
    private static void PersistBeforeQuit()
    {
        GameSettingsRuntime.EnsureInitialized();
        if (GameSettingsRuntime.IsInitialized)
            GameSettingsRuntime.SaveCurrentRewiredMaps();

        HideoutSaveSnapshot snapshot = HideoutSaveSystem.TryLoad(out HideoutSaveSnapshot loadedSnapshot)
            ? loadedSnapshot
            : new HideoutSaveSnapshot();
        HideoutSaveSystem.Save(snapshot);
    }

    /// <summary>
    /// Restores time, cursor, and player controls after pause closes.
    /// </summary>
    private void RestoreGameplayStateAfterPause()
    {
        isPaused = false;
        ApplyUnpausedTimeScale();
        Cursor.visible = false;
        Cursor.lockState = cachedCursorLockMode;
    }

    /// <summary>
    /// Forces gameplay time to stop while the pause menu is active.
    /// </summary>
    private static void ApplyPausedTimeScale()
    {
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Restores normal gameplay time after closing pause or loading away.
    /// </summary>
    private static void ApplyUnpausedTimeScale()
    {
        Time.timeScale = 1f;
    }

    /// <summary>
    /// Fades the main pause panel in or out.
    /// </summary>
    private void FadePanel(bool visible)
    {
        if (panelRoot == null)
            return;

        ResolvePanelCanvasGroups();
        panelFadeTween?.Kill();
        panelRoot.SetActive(true);

        if (panelCanvasGroup == null || panelFadeDuration <= 0f)
        {
            SetPanelStateImmediate(visible);
            return;
        }

        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
        if (visible)
            panelCanvasGroup.alpha = 0f;

        panelFadeTween = panelCanvasGroup
            .DOFade(visible ? 1f : 0f, panelFadeDuration)
            .SetEase(panelFadeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                panelFadeTween = null;
                SetPanelStateImmediate(visible);
            });
    }

    /// <summary>
    /// Fades the quit confirmation prompt in or out.
    /// </summary>
    private void FadeQuitPrompt(bool visible)
    {
        if (quitPromptRoot == null)
            return;

        ResolvePanelCanvasGroups();
        quitPromptFadeTween?.Kill();
        quitPromptRoot.SetActive(true);

        if (quitPromptCanvasGroup == null || panelFadeDuration <= 0f)
        {
            SetQuitPromptStateImmediate(visible);
            return;
        }

        quitPromptCanvasGroup.interactable = false;
        quitPromptCanvasGroup.blocksRaycasts = false;
        if (visible)
            quitPromptCanvasGroup.alpha = 0f;

        quitPromptFadeTween = quitPromptCanvasGroup
            .DOFade(visible ? 1f : 0f, panelFadeDuration)
            .SetEase(panelFadeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                quitPromptFadeTween = null;
                SetQuitPromptStateImmediate(visible);
            });
    }

    /// <summary>
    /// Applies the main panel state without animation.
    /// </summary>
    private void SetPanelStateImmediate(bool visible)
    {
        ResolvePanelCanvasGroups();
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }

        if (panelRoot != null)
            panelRoot.SetActive(visible);
    }

    /// <summary>
    /// Applies the quit prompt state without animation.
    /// </summary>
    private void SetQuitPromptStateImmediate(bool visible)
    {
        ResolvePanelCanvasGroups();
        if (quitPromptCanvasGroup != null)
        {
            quitPromptCanvasGroup.alpha = visible ? 1f : 0f;
            quitPromptCanvasGroup.interactable = visible;
            quitPromptCanvasGroup.blocksRaycasts = visible;
        }

        if (quitPromptRoot != null)
            quitPromptRoot.SetActive(visible);
    }

    /// <summary>
    /// Resolves optional scene references used by pause behavior.
    /// </summary>
    private void ResolveReferences()
    {
        ResolvePanelCanvasGroups();
    }

    /// <summary>
    /// Resolves CanvasGroups from their configured panel roots.
    /// </summary>
    private void ResolvePanelCanvasGroups()
    {
        panelCanvasGroup = panelRoot != null ? panelRoot.GetComponent<CanvasGroup>() : null;
        quitPromptCanvasGroup = quitPromptRoot != null ? quitPromptRoot.GetComponent<CanvasGroup>() : null;
    }

    /// <summary>
    /// Registers all configured button callbacks.
    /// </summary>
    private void RegisterButtonCallbacks()
    {
        UnregisterButtonCallbacks();
        closeButton?.onClick.AddListener(ClosePauseMenu);
        settingsButton?.onClick.AddListener(OpenSettingsPanel);
        quitButton?.onClick.AddListener(OpenQuitPrompt);
        quitConfirmButton?.onClick.AddListener(ConfirmQuitToHideout);
        quitCloseButton?.onClick.AddListener(CloseQuitPrompt);
    }

    /// <summary>
    /// Removes all configured button callbacks.
    /// </summary>
    private void UnregisterButtonCallbacks()
    {
        closeButton?.onClick.RemoveListener(ClosePauseMenu);
        settingsButton?.onClick.RemoveListener(OpenSettingsPanel);
        quitButton?.onClick.RemoveListener(OpenQuitPrompt);
        quitConfirmButton?.onClick.RemoveListener(ConfirmQuitToHideout);
        quitCloseButton?.onClick.RemoveListener(CloseQuitPrompt);
    }

    /// <summary>
    /// Stops active UI tweens owned by this controller.
    /// </summary>
    private void KillTweens()
    {
        panelFadeTween?.Kill();
        panelFadeTween = null;
        quitPromptFadeTween?.Kill();
        quitPromptFadeTween = null;
    }
}

}
