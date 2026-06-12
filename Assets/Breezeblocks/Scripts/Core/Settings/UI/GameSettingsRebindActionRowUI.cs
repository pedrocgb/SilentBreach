using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Settings.UI
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Settings/Rebind Action Row UI")]
public sealed class GameSettingsRebindActionRowUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text actionLabel;

    [FoldoutGroup("References")]
    [SerializeField] private Button primaryButton;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text primaryBindingText;

    [FoldoutGroup("References")]
    [SerializeField] private Button secondaryButton;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text secondaryBindingText;

    [FoldoutGroup("Optional Explanation")]
    [SerializeField] private TMP_Text explanationText;

    private GameSettingsRebindPanelUI owner;
    private int actionId;
    private Rewired.AxisRange actionRange;

    public int ActionId => actionId;
    public Rewired.AxisRange ActionRange => actionRange;
    public TMP_Text ExplanationText => explanationText;

    /// <summary>
    /// Registers button listeners for this runtime-created row.
    /// </summary>
    private void OnEnable()
    {
        if (primaryButton != null)
            primaryButton.onClick.AddListener(HandlePrimaryClicked);

        if (secondaryButton != null)
            secondaryButton.onClick.AddListener(HandleSecondaryClicked);
    }

    /// <summary>
    /// Removes button listeners when the row is hidden or destroyed.
    /// </summary>
    private void OnDisable()
    {
        if (primaryButton != null)
            primaryButton.onClick.RemoveListener(HandlePrimaryClicked);

        if (secondaryButton != null)
            secondaryButton.onClick.RemoveListener(HandleSecondaryClicked);
    }

    /// <summary>
    /// Configures this row for one Rewired action and axis range.
    /// </summary>
    public void Initialize(GameSettingsRebindPanelUI panelOwner, int configuredActionId, Rewired.AxisRange configuredRange, string label)
    {
        owner = panelOwner;
        actionId = configuredActionId;
        actionRange = configuredRange;

        if (actionLabel != null)
            actionLabel.text = label ?? string.Empty;
    }

    /// <summary>
    /// Updates both binding labels without changing Rewired maps.
    /// </summary>
    public void SetBindingLabels(string primary, string secondary)
    {
        if (primaryBindingText != null)
            primaryBindingText.text = string.IsNullOrWhiteSpace(primary) ? "Unbound" : primary;

        if (secondaryBindingText != null)
            secondaryBindingText.text = string.IsNullOrWhiteSpace(secondary) ? "Unbound" : secondary;
    }

    /// <summary>
    /// Requests replacement or creation of the primary binding.
    /// </summary>
    private void HandlePrimaryClicked()
    {
        owner?.BeginRebind(this, 0);
    }

    /// <summary>
    /// Requests replacement or creation of the secondary binding.
    /// </summary>
    private void HandleSecondaryClicked()
    {
        owner?.BeginRebind(this, 1);
    }
}

}
