using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.HideoutSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Hideout/Job List Item UI")]
public sealed class HideoutJobListItemUI : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private Color normalColor = new(0.13f, 0.17f, 0.21f, 0.90f);
    [SerializeField] private Color selectedColor = new(0.18f, 0.31f, 0.39f, 1f);

    private void Reset()
    {
        selectButton = GetComponent<Button>();
        backgroundImage = GetComponent<Image>();
    }

    public void Bind(string title, string subtitle, bool isSelected, Action onSelected)
    {
        if (selectButton == null)
            selectButton = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (titleText != null)
            titleText.text = title ?? string.Empty;

        if (subtitleText != null)
            subtitleText.text = subtitle ?? string.Empty;

        if (backgroundImage != null)
            backgroundImage.color = isSelected ? selectedColor : normalColor;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke());
        }
    }
}

}
