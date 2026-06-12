using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Settings.UI
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("Breezeblocks/Settings/Settings Tab Button UI")]
public sealed class GameSettingsTabButtonUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private GameObject contentRoot;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject selectedVisual;

    [FoldoutGroup("Optional Explanation")]
    [SerializeField] private TMP_Text explanationText;

    private Button button;

    public event Action<GameSettingsTabButtonUI> Selected;

    public TMP_Text ExplanationText => explanationText;

    /// <summary>
    /// Caches the mandatory same-object button.
    /// </summary>
    private void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>
    /// Registers the tab click listener.
    /// </summary>
    private void OnEnable()
    {
        button ??= GetComponent<Button>();
        button.onClick.AddListener(HandleClicked);
    }

    /// <summary>
    /// Removes the tab click listener.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    /// <summary>
    /// Displays this tab's content and selected feedback.
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (contentRoot != null)
            contentRoot.SetActive(selected);

        if (selectedVisual != null)
            selectedVisual.SetActive(selected);
    }

    /// <summary>
    /// Notifies the settings panel that this tab was selected.
    /// </summary>
    private void HandleClicked()
    {
        Selected?.Invoke(this);
    }
}

}
