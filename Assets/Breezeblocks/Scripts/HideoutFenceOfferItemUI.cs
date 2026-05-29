using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.HideoutSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Hideout/Fence Offer Item UI")]
public sealed class HideoutFenceOfferItemUI : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text slotsText;
    [SerializeField] private TMP_Text buyButtonText;
    [SerializeField] private Color normalColor = new(0.13f, 0.17f, 0.21f, 0.90f);
    [SerializeField] private Color selectedColor = new(0.18f, 0.31f, 0.39f, 1f);
    [SerializeField] private Color soldOutColor = new(0.17f, 0.17f, 0.18f, 0.88f);

    private void Reset()
    {
        selectButton = GetComponent<Button>();
        backgroundImage = GetComponent<Image>();
    }

    public void Bind(
        string itemName,
        string priceLabel,
        string slotsLabel,
        Sprite icon,
        bool isSelected,
        bool canBuy,
        Action onSelected,
        Action onBought)
    {
        if (selectButton == null)
            selectButton = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (nameText != null)
            nameText.text = itemName ?? string.Empty;

        if (priceText != null)
            priceText.text = priceLabel ?? string.Empty;

        if (slotsText != null)
            slotsText.text = slotsLabel ?? string.Empty;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (buyButtonText != null)
            buyButtonText.text = canBuy ? "Comprar" : "Esgotado";

        if (backgroundImage != null)
            backgroundImage.color = isSelected ? selectedColor : (canBuy ? normalColor : soldOutColor);

        if (highlightObject != null)
            highlightObject.SetActive(isSelected);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke());
        }

        if (buyButton != null)
        {
            buyButton.interactable = canBuy;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBought?.Invoke());
        }
    }
}

}
