using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

public enum InputCodeButtonKind
{
    Number,
    Delete,
    Confirm
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("Breezeblocks/Missions/Input Code/Button View")]
public sealed class InputCodeButtonView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [FoldoutGroup("Button")]
    [SerializeField] private InputCodeButtonKind buttonKind;

    [FoldoutGroup("Button"), ShowIf(nameof(IsNumberButton)), Range(0, 9)]
    [SerializeField] private int digit;

    [FoldoutGroup("Highlight")]
    [SerializeField] private Image highlightImage;

    [FoldoutGroup("Highlight"), AssetsOnly]
    [SerializeField] private Material hoverMaterial;

    public event Action<InputCodeButtonView> Clicked;

    public InputCodeButtonKind ButtonKind => buttonKind;
    public int Digit => Mathf.Clamp(digit, 0, 9);

    private readonly UIImageMaterialHoverFeedback hoverFeedback = new();
    private Button button;

    /// <summary>
    /// Caches the same-object button and binds material hover feedback.
    /// </summary>
    private void Awake()
    {
        button = GetComponent<Button>();
        BindHoverFeedback();
    }

    /// <summary>
    /// Subscribes to button clicks while this key is visible.
    /// </summary>
    private void OnEnable()
    {
        button ??= GetComponent<Button>();
        BindHoverFeedback();
        button.onClick.AddListener(HandleClicked);
    }

    /// <summary>
    /// Removes click callbacks and clears hover feedback while hidden.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);

        hoverFeedback.SetHighlighted(false);
    }

    /// <summary>
    /// Clamps digit values and syncs hover material while editing.
    /// </summary>
    private void OnValidate()
    {
        digit = Mathf.Clamp(digit, 0, 9);
        hoverFeedback.SetHighlightMaterial(hoverMaterial);
    }

    /// <summary>
    /// Applies or removes material hover feedback.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        hoverFeedback.SetHighlighted(highlighted);
    }

    /// <summary>
    /// Enables or disables this keypad button through its cached Button component.
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.interactable = enabled;

        if (!enabled)
            SetHighlighted(false);
    }

    /// <summary>
    /// Handles pointer-enter hover feedback for enabled keys.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHighlighted(true);
    }

    /// <summary>
    /// Handles pointer-exit hover feedback for enabled keys.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHighlighted(false);
    }

    /// <summary>
    /// Emits a click request to the owning input-code controller.
    /// </summary>
    private void HandleClicked()
    {
        Clicked?.Invoke(this);
    }

    /// <summary>
    /// Returns whether Odin should expose the digit field.
    /// </summary>
    private bool IsNumberButton()
    {
        return buttonKind == InputCodeButtonKind.Number;
    }

    /// <summary>
    /// Binds hover feedback to the configured image, falling back to this button's graphic.
    /// </summary>
    private void BindHoverFeedback()
    {
        if (highlightImage == null && button != null)
            highlightImage = button.targetGraphic as Image;

        hoverFeedback.Bind(highlightImage, hoverMaterial);
    }
}

}
