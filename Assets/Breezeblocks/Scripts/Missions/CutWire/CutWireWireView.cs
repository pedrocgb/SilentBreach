using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("Breezeblocks/Missions/Cut Wire/Wire View")]
public sealed class CutWireWireView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [FoldoutGroup("References")]
    [SerializeField] private Image wireImage;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject hoverObject;

    [FoldoutGroup("Sprites"), AssetsOnly]
    [SerializeField] private Sprite intactSprite;

    [FoldoutGroup("Sprites"), AssetsOnly]
    [SerializeField] private Sprite cutSprite;

    [FoldoutGroup("Colors")]
    [SerializeField] private Color white = Color.white;

    [FoldoutGroup("Colors")]
    [SerializeField] private Color black = Color.black;

    [FoldoutGroup("Colors")]
    [SerializeField] private Color yellow = Color.yellow;

    [FoldoutGroup("Colors")]
    [SerializeField] private Color blue = Color.blue;

    [FoldoutGroup("Colors")]
    [SerializeField] private Color red = Color.red;

    [FoldoutGroup("Colors")]
    [SerializeField] private Color green = Color.green;

    public event Action<CutWireWireView> CutRequested;

    public int WireIndex { get; private set; }
    public bool IsCut { get; private set; }

    private Button button;

    /// <summary>
    /// Caches the mandatory same-object button before runtime configuration.
    /// </summary>
    private void Awake()
    {
        button = GetComponent<Button>();
        SetHovered(false);
    }

    /// <summary>
    /// Registers the wire click callback while this slot is active.
    /// </summary>
    private void OnEnable()
    {
        button ??= GetComponent<Button>();
        button.onClick.AddListener(HandleClicked);
    }

    /// <summary>
    /// Removes the wire click callback and hover feedback while hidden.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);

        SetHovered(false);
    }

    /// <summary>
    /// Configures this reusable slot for one active wire.
    /// </summary>
    public void Configure(int index, CutWireColor color, bool isCut)
    {
        WireIndex = index;
        gameObject.SetActive(true);

        if (wireImage != null)
            wireImage.color = ResolveColor(color);

        SetCut(isCut);
    }

    /// <summary>
    /// Shows or hides this slot based on current difficulty.
    /// </summary>
    public void SetRuntimeVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Applies intact or cut presentation and prevents cutting the same wire twice.
    /// </summary>
    public void SetCut(bool cut)
    {
        IsCut = cut;
        if (wireImage != null)
        {
            Sprite targetSprite = cut ? cutSprite : intactSprite;
            if (targetSprite != null)
                wireImage.sprite = targetSprite;
        }

        if (button != null)
            button.interactable = !cut;

        if (cut)
            SetHovered(false);
    }

    /// <summary>
    /// Enables or disables clicking without changing cut presentation.
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        if (button != null)
            button.interactable = enabled && !IsCut;
    }

    /// <summary>
    /// Shows hover feedback when pointer enters an intact wire.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(!IsCut);
    }

    /// <summary>
    /// Hides hover feedback when pointer leaves this wire.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
    }

    /// <summary>
    /// Publishes a cut request for the active minigame controller.
    /// </summary>
    private void HandleClicked()
    {
        if (!IsCut)
            CutRequested?.Invoke(this);
    }

    /// <summary>
    /// Applies optional hover presentation.
    /// </summary>
    private void SetHovered(bool hovered)
    {
        if (hoverObject != null)
            hoverObject.SetActive(hovered);
    }

    /// <summary>
    /// Resolves the designer-tunable tint belonging to a wire color.
    /// </summary>
    private Color ResolveColor(CutWireColor color)
    {
        return color switch
        {
            CutWireColor.Black => black,
            CutWireColor.Yellow => yellow,
            CutWireColor.Blue => blue,
            CutWireColor.Red => red,
            CutWireColor.Green => green,
            _ => white
        };
    }
}

}
