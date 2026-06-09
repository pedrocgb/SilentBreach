using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Mission Status Entry UI")]
public class MissionStatusEntryUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private TMP_Text statusText;

    [FoldoutGroup("References")]
    [SerializeField] private CanvasGroup canvasGroup;

    private Tween activeTween;
    private string rawText = string.Empty;
    private bool struckThrough;

    private void Reset()
    {
        if (statusText == null)
            statusText = GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (statusText == null)
            statusText = GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void OnDisable()
    {
        activeTween?.Kill();
        activeTween = null;
    }

    public void PrepareForDisplay()
    {
        activeTween?.Kill();
        activeTween = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (transform is RectTransform rectTransform)
            rectTransform.localScale = Vector3.one;
    }

    public void SetText(string text, bool useStrikethrough)
    {
        rawText = text ?? string.Empty;
        struckThrough = useStrikethrough;

        if (statusText == null)
            return;

        statusText.text = struckThrough ? $"<s>{rawText}</s>" : rawText;
    }

    public Tween PlayFadeIn(float duration)
    {
        if (canvasGroup == null)
            return null;

        activeTween?.Kill();
        activeTween = canvasGroup
            .DOFade(1f, Mathf.Max(0f, duration))
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnComplete(() => activeTween = null);

        return activeTween;
    }
}
}
