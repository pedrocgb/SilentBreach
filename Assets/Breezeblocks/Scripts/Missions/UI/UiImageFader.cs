using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
[AddComponentMenu("Breezeblocks/Missions/UI Image Fader")]
public class UiImageFader : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private Image targetImage;

    [FoldoutGroup("Tween"), MinValue(0f)]
    [SerializeField] private float defaultFadeDuration = 0.35f;

    [FoldoutGroup("Tween")]
    [SerializeField] private Ease fadeEase = Ease.InOutSine;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float CurrentAlpha => targetImage != null ? targetImage.color.a : 0f;

    private Tween activeTween;

    private void Reset()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }

    private void OnDisable()
    {
        activeTween?.Kill();
        activeTween = null;
    }

    public Tween FadeIn(float duration = -1f)
    {
        return FadeTo(1f, duration);
    }

    public Tween FadeOut(float duration = -1f)
    {
        return FadeTo(0f, duration);
    }

    public Tween FadeTo(float targetAlpha, float duration = -1f)
    {
        if (targetImage == null)
            return null;

        activeTween?.Kill();
        float resolvedDuration = duration >= 0f ? duration : defaultFadeDuration;
        targetAlpha = Mathf.Clamp01(targetAlpha);

        activeTween = targetImage.DOFade(targetAlpha, resolvedDuration)
            .SetEase(fadeEase)
            .SetUpdate(true);

        return activeTween;
    }

    public void SetAlphaImmediate(float alpha)
    {
        if (targetImage == null)
            return;

        activeTween?.Kill();
        Color color = targetImage.color;
        color.a = Mathf.Clamp01(alpha);
        targetImage.color = color;
    }
}

}
