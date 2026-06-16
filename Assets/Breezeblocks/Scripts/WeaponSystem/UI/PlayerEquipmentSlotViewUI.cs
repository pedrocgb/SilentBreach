using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breezeblocks.WeaponSystem
{

public enum PlayerEquipmentSlotViewType
{
    Equipment,
    Armor
}

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/UI/Equipment Slot View")]
public class PlayerEquipmentSlotViewUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [FoldoutGroup("Slot"), LabelText("Slot Type")]
    [SerializeField] private PlayerEquipmentSlotViewType slotViewType = PlayerEquipmentSlotViewType.Equipment;

    [FoldoutGroup("References")]
    [SerializeField] private Image iconImage;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text itemNameText;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text slotLabelText;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text hotkeyLabelText;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject selectedHighlight;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject hoverHighlight;

    [FoldoutGroup("References")]
    [SerializeField] private GameObject emptyStateRoot;

    [FoldoutGroup("Visuals")]
    [SerializeField] private Color filledColor = Color.white;

    [FoldoutGroup("Visuals")]
    [SerializeField] private Color emptyColor = new(1f, 1f, 1f, 0.35f);

    [FoldoutGroup("Visuals")]
    [SerializeField] private string emptyDisplayName = "Empty";

    [FoldoutGroup("Visuals")]
    [SerializeField] private Sprite fallbackFilledIcon;

    public EquipmentSlotType SlotType => ResolveSlotType();
    public PlayerEquipmentSlotViewType SlotViewType => slotViewType;
    public bool IsDragAndDropEnabled => dragAndDropEnabled;
    public bool HasItem => displayedItem != null;
    public event Action<PlayerEquipmentSlotViewUI> PointerEntered;
    public event Action<PlayerEquipmentSlotViewUI> PointerExited;
    public event Action<PlayerEquipmentSlotViewUI> Clicked;
    public event Action<PlayerEquipmentSlotViewUI> DragStarted;
    public event Action<PlayerEquipmentSlotViewUI> DragEnded;
    public event Action<PlayerEquipmentSlotViewUI, PlayerEquipmentSlotViewUI> DropReceived;

    private Sprite runtimeFallbackFilledIcon;
    private EquipmentItemData displayedItem;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private RectTransform dragPreviewRectTransform;
    private Image dragPreviewImage;
    private bool isDragging;
    private bool dragAndDropEnabled;
    private bool dropHandledThisDrag;
    private EquipmentSlotType runtimeSlotType;

    /// <summary>
    /// Caches required runtime references before the slot view is used.
    /// </summary>
    private void Awake()
    {
        if (iconImage != null)
            runtimeFallbackFilledIcon = iconImage.sprite;

        rectTransform = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheRootCanvas();
    }

    /// <summary>
    /// Restores drag state when the slot view is disabled mid-drag.
    /// </summary>
    private void OnDisable()
    {
        if (isDragging)
            RestoreAfterDrag();
    }

    /// <summary>
    /// Enables or disables drag and drop interactions for this slot view.
    /// </summary>
    public void SetDragAndDropEnabled(bool enabled)
    {
        dragAndDropEnabled = enabled;
    }

    /// <summary>
    /// Binds this slot view to a specific runtime slot when it is created through code.
    /// </summary>
    public void ConfigureRuntimeView(
        EquipmentSlotType configuredSlotType,
        Image configuredIconImage,
        TMP_Text configuredItemNameText,
        TMP_Text configuredSlotLabelText,
        TMP_Text configuredHotkeyLabelText,
        GameObject configuredSelectedHighlight,
        GameObject configuredHoverHighlight,
        GameObject configuredEmptyStateRoot,
        Sprite configuredFallbackFilledIcon = null)
    {
        runtimeSlotType = configuredSlotType;
        slotViewType = configuredSlotType == EquipmentSlotType.Armor
            ? PlayerEquipmentSlotViewType.Armor
            : PlayerEquipmentSlotViewType.Equipment;
        iconImage = configuredIconImage;
        itemNameText = configuredItemNameText;
        slotLabelText = configuredSlotLabelText;
        hotkeyLabelText = configuredHotkeyLabelText;
        selectedHighlight = configuredSelectedHighlight;
        hoverHighlight = configuredHoverHighlight;
        emptyStateRoot = configuredEmptyStateRoot;

        if (configuredFallbackFilledIcon != null)
            fallbackFilledIcon = configuredFallbackFilledIcon;

        runtimeFallbackFilledIcon = iconImage != null ? iconImage.sprite : null;
        rectTransform = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheRootCanvas();
    }

    /// <summary>
    /// Refreshes the slot visual state from the supplied equipment data and selection flags.
    /// </summary>
    public void Refresh(EquipmentItemData item, bool isSelected, string slotLabel, string hotkeyLabel)
    {
        displayedItem = item;

        if (slotLabelText != null)
            slotLabelText.text = slotLabel;

        if (hotkeyLabelText != null)
            hotkeyLabelText.text = hotkeyLabel;

        if (itemNameText != null)
            itemNameText.text = item != null ? item.DisplayName : emptyDisplayName;

        if (iconImage != null)
        {
            Sprite iconSprite = item != null ? item.Icon : null;
            if (item != null && iconSprite == null)
                iconSprite = fallbackFilledIcon != null ? fallbackFilledIcon : runtimeFallbackFilledIcon;

            iconImage.sprite = iconSprite;
            iconImage.enabled = true;
            iconImage.color = item != null ? filledColor : new Color(emptyColor.r, emptyColor.g, emptyColor.b, 0f);
        }

        if (selectedHighlight != null)
            selectedHighlight.SetActive(isSelected);

        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(item == null);

        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);
    }

    /// <summary>
    /// Shows hover state and notifies listeners when the pointer enters this slot.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverHighlight != null)
            hoverHighlight.SetActive(true);

        PointerEntered?.Invoke(this);
    }

    /// <summary>
    /// Clears hover state and notifies listeners when the pointer exits this slot.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);

        PointerExited?.Invoke(this);
    }

    /// <summary>
    /// Notifies listeners that this slot was clicked.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(this);
    }

    /// <summary>
    /// Starts drag feedback for the current item when this slot can be dragged.
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanStartDrag())
            return;

        CacheRootCanvas();
        if (rectTransform == null || rootCanvas == null)
            return;

        isDragging = true;
        dropHandledThisDrag = false;
        CreateDragPreview();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.45f;
        }

        if (hoverHighlight != null)
            hoverHighlight.SetActive(false);

        OnDrag(eventData);
        DragStarted?.Invoke(this);
    }

    /// <summary>
    /// Moves the drag preview to follow the current pointer position.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragPreviewRectTransform == null || rootCanvas == null)
            return;

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            dragPreviewRectTransform.anchoredPosition = localPoint;
    }

    /// <summary>
    /// Finalizes a drag operation and attempts to drop the item onto another slot view.
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        TryHandleDropFromPointer(eventData);
        RestoreAfterDrag();
        DragEnded?.Invoke(this);
    }

    /// <summary>
    /// Receives a dropped slot view and forwards the swap request to listeners.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (!dragAndDropEnabled || eventData.pointerDrag == null)
            return;

        PlayerEquipmentSlotViewUI sourceSlotView = eventData.pointerDrag.GetComponent<PlayerEquipmentSlotViewUI>();
        if (sourceSlotView == null || sourceSlotView == this || !sourceSlotView.IsDragAndDropEnabled || !sourceSlotView.HasItem)
            return;

        sourceSlotView.dropHandledThisDrag = true;
        DropReceived?.Invoke(this, sourceSlotView);
    }

    /// <summary>
    /// Returns whether this slot can currently begin a drag operation.
    /// </summary>
    private bool CanStartDrag()
    {
        return dragAndDropEnabled && displayedItem != null;
    }

    /// <summary>
    /// Resolves the effective runtime slot used by external controllers.
    /// </summary>
    private EquipmentSlotType ResolveSlotType()
    {
        if (runtimeSlotType != EquipmentSlotType.None)
            return runtimeSlotType;

        if (slotViewType == PlayerEquipmentSlotViewType.Armor)
            return EquipmentSlotType.Armor;

        return ResolveIndexedHandSlot(ResolveHandSlotSiblingIndex());
    }

    /// <summary>
    /// Counts how many equipment views appear before this one under the same parent.
    /// </summary>
    private int ResolveHandSlotSiblingIndex()
    {
        Transform parentTransform = transform.parent;
        if (parentTransform == null)
            return 0;

        int handSlotIndex = 0;
        for (int i = 0; i < parentTransform.childCount; i++)
        {
            Transform child = parentTransform.GetChild(i);
            if (child == transform)
                break;

            PlayerEquipmentSlotViewUI siblingSlotView = child.GetComponent<PlayerEquipmentSlotViewUI>();
            if (siblingSlotView == null || siblingSlotView.SlotViewType == PlayerEquipmentSlotViewType.Armor)
                continue;

            handSlotIndex++;
        }

        return handSlotIndex;
    }

    /// <summary>
    /// Maps a hand-slot index to the matching gameplay slot identifier.
    /// </summary>
    private static EquipmentSlotType ResolveIndexedHandSlot(int handSlotIndex)
    {
        return handSlotIndex switch
        {
            0 => EquipmentSlotType.Primary,
            1 => EquipmentSlotType.Secondary,
            2 => EquipmentSlotType.Belt,
            _ => EquipmentSlotType.None
        };
    }

    /// <summary>
    /// Caches the root canvas used to host the temporary drag preview.
    /// </summary>
    private void CacheRootCanvas()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
    }

    /// <summary>
    /// Clears drag-only state and restores the slot view after a drag ends.
    /// </summary>
    private void RestoreAfterDrag()
    {
        isDragging = false;
        dropHandledThisDrag = false;
        DestroyDragPreview();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }
    }

    /// <summary>
    /// Builds the temporary drag-preview image shown while an item is being dragged.
    /// </summary>
    private void CreateDragPreview()
    {
        DestroyDragPreview();
        if (rootCanvas == null || iconImage == null || iconImage.sprite == null)
            return;

        GameObject previewObject = new($"{name} Drag Preview", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        previewObject.transform.SetParent(rootCanvas.transform, false);

        dragPreviewRectTransform = previewObject.GetComponent<RectTransform>();
        Vector2 previewSize = iconImage.rectTransform.rect.size;
        if (previewSize.x <= 0f || previewSize.y <= 0f)
            previewSize = rectTransform != null ? rectTransform.rect.size : new Vector2(64f, 64f);

        dragPreviewRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        dragPreviewRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        dragPreviewRectTransform.pivot = new Vector2(0.5f, 0.5f);
        dragPreviewRectTransform.sizeDelta = previewSize;
        dragPreviewRectTransform.localScale = Vector3.one;
        dragPreviewRectTransform.SetAsLastSibling();

        CanvasGroup previewCanvasGroup = previewObject.GetComponent<CanvasGroup>();
        previewCanvasGroup.blocksRaycasts = false;
        previewCanvasGroup.interactable = false;
        previewCanvasGroup.alpha = 0.95f;

        dragPreviewImage = previewObject.GetComponent<Image>();
        dragPreviewImage.sprite = iconImage.sprite;
        dragPreviewImage.color = iconImage.color;
        dragPreviewImage.material = iconImage.material;
        dragPreviewImage.preserveAspect = iconImage.preserveAspect;
        dragPreviewImage.raycastTarget = false;
    }

    /// <summary>
    /// Attempts to resolve a drop target from the current pointer state when drag ends.
    /// </summary>
    private void TryHandleDropFromPointer(PointerEventData eventData)
    {
        if (dropHandledThisDrag || eventData == null)
            return;

        PlayerEquipmentSlotViewUI targetSlotView = FindDropTargetSlotView(eventData);
        if (targetSlotView == null)
            return;

        dropHandledThisDrag = true;
        targetSlotView.DropReceived?.Invoke(targetSlotView, this);
    }

    /// <summary>
    /// Finds the slot view currently under the pointer that can accept a drag drop.
    /// </summary>
    private PlayerEquipmentSlotViewUI FindDropTargetSlotView(PointerEventData eventData)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return null;

        List<RaycastResult> raycastResults = new();
        eventSystem.RaycastAll(eventData, raycastResults);
        for (int i = 0; i < raycastResults.Count; i++)
        {
            GameObject hitObject = raycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            PlayerEquipmentSlotViewUI slotView = hitObject.GetComponentInParent<PlayerEquipmentSlotViewUI>();
            if (slotView == null || slotView == this || !slotView.IsDragAndDropEnabled)
                continue;

            return slotView;
        }

        GameObject hoveredObject = eventData.pointerCurrentRaycast.gameObject;
        if (hoveredObject == null)
            hoveredObject = eventData.pointerEnter;

        if (hoveredObject == null)
            return null;

        PlayerEquipmentSlotViewUI hoveredSlotView = hoveredObject.GetComponentInParent<PlayerEquipmentSlotViewUI>();
        return hoveredSlotView != null && hoveredSlotView != this && hoveredSlotView.IsDragAndDropEnabled
            ? hoveredSlotView
            : null;
    }

    /// <summary>
    /// Destroys the temporary drag-preview object if one exists.
    /// </summary>
    private void DestroyDragPreview()
    {
        if (dragPreviewRectTransform != null)
            Destroy(dragPreviewRectTransform.gameObject);

        dragPreviewRectTransform = null;
        dragPreviewImage = null;
    }
}
}
