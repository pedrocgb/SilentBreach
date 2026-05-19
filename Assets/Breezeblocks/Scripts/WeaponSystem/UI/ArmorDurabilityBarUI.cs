using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/UI/Armor Durability Bar UI")]
public class ArmorDurabilityBarUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private ArmorLoadout armorLoadout;

    [FoldoutGroup("References"), Required]
    [SerializeField] private Image fillImage;

    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text valueText;

    [FoldoutGroup("Format")]
    [SerializeField] private string durabilityFormat = "{0:0}/{1:0}";

    [FoldoutGroup("Format")]
    [SerializeField] private string noArmorText = "--/--";

    [FoldoutGroup("Behavior")]
    [SerializeField] private bool hideWhenNoArmor;

    private ArmorLoadout subscribedArmorLoadout;

    private void Awake()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (armorLoadout == null)
            armorLoadout = FindFirstObjectByType<ArmorLoadout>();
    }

    private void Subscribe()
    {
        if (subscribedArmorLoadout == armorLoadout || armorLoadout == null)
            return;

        Unsubscribe();
        armorLoadout.ArmorChanged += Refresh;
        subscribedArmorLoadout = armorLoadout;
    }

    private void Unsubscribe()
    {
        if (subscribedArmorLoadout == null)
            return;

        subscribedArmorLoadout.ArmorChanged -= Refresh;
        subscribedArmorLoadout = null;
    }

    private void Refresh()
    {
        float maxArmor = armorLoadout != null ? armorLoadout.MaxArmorValue : 0f;
        float currentArmor = armorLoadout != null ? armorLoadout.CurrentArmorValue : 0f;
        bool hasArmor = maxArmor > 0f;

        if (fillImage != null)
        {
            fillImage.fillAmount = hasArmor ? Mathf.Clamp01(currentArmor / Mathf.Max(0.0001f, maxArmor)) : 0f;
            if (hideWhenNoArmor)
                fillImage.gameObject.SetActive(hasArmor);
        }

        if (valueText != null)
        {
            valueText.text = hasArmor
                ? string.Format(durabilityFormat, currentArmor, maxArmor)
                : noArmorText;

            if (hideWhenNoArmor)
                valueText.gameObject.SetActive(hasArmor);
        }
    }
}
}
