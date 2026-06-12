using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Settings.UI
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("Breezeblocks/Settings/Settings Frame Rate Button UI")]
public sealed class GameSettingsFrameRateButtonUI : MonoBehaviour
{
    [FoldoutGroup("Setting"), EnumToggleButtons]
    [SerializeField] private GameFrameRateLimit frameRateLimit = GameFrameRateLimit.Unlimited;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject selectedVisual;

    [FoldoutGroup("Optional Explanation")]
    [SerializeField] private TMP_Text explanationText;

    private Button button;

    public event Action<GameSettingsFrameRateButtonUI> Selected;

    public GameFrameRateLimit FrameRateLimit => frameRateLimit;
    public TMP_Text ExplanationText => explanationText;

    /// <summary>
    /// Caches the mandatory same-object button.
    /// </summary>
    private void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>
    /// Registers the frame-rate click listener.
    /// </summary>
    private void OnEnable()
    {
        button ??= GetComponent<Button>();
        button.onClick.AddListener(HandleClicked);
    }

    /// <summary>
    /// Removes the frame-rate click listener.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    /// <summary>
    /// Updates selected feedback without altering the saved preference.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectedVisual != null)
            selectedVisual.SetActive(selected);
    }

    /// <summary>
    /// Notifies the settings panel that this frame-rate option was selected.
    /// </summary>
    private void HandleClicked()
    {
        Selected?.Invoke(this);
    }
}

}
