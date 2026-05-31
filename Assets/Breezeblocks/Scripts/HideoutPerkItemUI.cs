using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Breezeblocks.HideoutSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Hideout/Perk Item UI")]
public sealed class HideoutPerkItemUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject padlockObject;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button equipButton;
    [SerializeField] private GameObject selectionObject;
    [SerializeField] private GameObject equippedObject;
    [SerializeField] private Color normalColor = new(0.13f, 0.17f, 0.21f, 0.90f);
    [SerializeField] private Color lockedColor = new(0.18f, 0.16f, 0.18f, 0.92f);
    [SerializeField] private Color equippedColor = new(0.16f, 0.24f, 0.19f, 0.95f);
    [SerializeField] private Color selectedColor = new(0.18f, 0.31f, 0.39f, 1f);

    private Action doubleClickAction;

    private void Reset()
    {
        selectButton = GetComponent<Button>();
        backgroundImage = GetComponent<Image>();
    }

    public void Bind(
        HideoutPerkDefinition perkDefinition,
        bool isSelected,
        bool isUnlocked,
        bool isEquipped,
        bool showBuyButton,
        bool canBuy,
        bool showEquipButton,
        bool canEquip,
        Action onPrimaryClicked,
        Action onBought,
        Action onEquipped,
        Action onDoubleClicked = null)
    {
        if (selectButton == null)
            selectButton = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (titleText != null)
            titleText.text = perkDefinition != null ? perkDefinition.PerkName : string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = perkDefinition != null ? perkDefinition.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        SetActive(padlockObject, !isUnlocked);
        SetActive(selectionObject, isSelected);
        SetActive(equippedObject, isEquipped);

        if (backgroundImage != null)
        {
            backgroundImage.color = isSelected
                ? selectedColor
                : isEquipped
                    ? equippedColor
                    : isUnlocked
                        ? normalColor
                        : lockedColor;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onPrimaryClicked?.Invoke());
        }

        if (buyButton != null)
        {
            buyButton.gameObject.SetActive(showBuyButton);
            buyButton.interactable = canBuy;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBought?.Invoke());
        }

        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(showEquipButton);
            equipButton.interactable = canEquip;
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(() => onEquipped?.Invoke());
        }

        doubleClickAction = onDoubleClicked;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.clickCount < 2 || doubleClickAction == null)
            return;

        if (WasActionButtonPressed(eventData, buyButton) || WasActionButtonPressed(eventData, equipButton))
            return;

        doubleClickAction.Invoke();
    }

    private static bool WasActionButtonPressed(PointerEventData eventData, Button button)
    {
        if (eventData == null || button == null)
            return false;

        GameObject pressedObject = eventData.pointerPress;
        if (pressedObject == null)
            pressedObject = eventData.pointerCurrentRaycast.gameObject;

        return pressedObject != null && pressedObject.transform.IsChildOf(button.transform);
    }

    private static void SetActive(GameObject target, bool visible)
    {
        if (target != null)
            target.SetActive(visible);
    }
}

}
