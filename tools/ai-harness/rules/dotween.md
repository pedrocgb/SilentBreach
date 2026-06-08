# DOTween Rules

The project uses DOTween for tweening and animation.

---

## Core Rules

- Prefer DOTween for simple, code-driven animations when it is appropriate.
- Do not automatically use DOTween for every animation.
- Analyze whether DOTween is the best approach based on performance cost and maintainability.
- The agent must explain why DOTween was used or why it was intentionally not used.

---

## Decision Criteria

Before choosing DOTween, analyze:

- Performance cost.
- Animation complexity.
- Whether the animation is UI, transform, material, camera, or gameplay-related.
- Whether the animation needs to be reusable.
- Whether the animation should be handled by Unity Animator instead.
- Whether the animation happens frequently or only occasionally.
- Whether the animation affects physics or gameplay movement.

---

## Preferred DOTween Use Cases

Prefer DOTween for:

- UI transitions.
- Menu animations.
- Fade in / fade out.
- Scale punch effects.
- Simple movement animations.
- Camera shake.
- Object highlight effects.
- Small feedback animations.
- Temporary visual effects.
- Smooth value interpolation.

---

## Avoid DOTween When

Avoid DOTween when:

- The animation is a complex character animation.
- The animation needs animation clips, blend trees, or Animator state machines.
- The animation runs constantly on many objects and may create performance overhead.
- The animation is better handled by physics.
- The animation affects gameplay movement that should be controlled by Rigidbody2D.
- A simple direct value assignment is enough.
- A pooled or reusable manual system would be more performant.

---

## Performance Rules

- Avoid creating tweens every frame.
- Avoid unnecessary allocations.
- Cache reusable tweens when appropriate.
- Kill tweens when the object is disabled or destroyed.
- Avoid leaving active tweens on destroyed objects.
- Use `SetAutoKill(false)` only when the tween is intentionally reused.
- Use `SetUpdate(true)` only for animations that must ignore `Time.timeScale`.
- Avoid tweening physics objects directly through Transform when Rigidbody2D movement is required.
- For Rigidbody2D objects, prefer Rigidbody2D-compatible movement or tweening methods only when it does not conflict with physics.

---

## Required Cleanup

Any component that creates DOTween tweens must clean them up properly.

Use `OnDisable` or `OnDestroy` when needed.

Example:

```csharp
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class UIElementTween : MonoBehaviour
{
    [TitleGroup("Tween Settings")]
    [SerializeField, MinValue(0f)]
    private float fadeDuration = 0.25f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;

    /// <summary>
    /// Caches the CanvasGroup from this GameObject before the tween is used.
    /// </summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Fades the UI element in using DOTween.
    /// </summary>
    public void FadeIn()
    {
        fadeTween?.Kill();
        fadeTween = canvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// Stops the active tween when this object is disabled.
    /// </summary>
    private void OnDisable()
    {
        fadeTween?.Kill();
        fadeTween = null;
    }
}
```

---

## Same-Object Components

Same-object DOTween-related components must not be serialized.

Do not do this:

```csharp
[SerializeField]
private CanvasGroup canvasGroup;
```

When `CanvasGroup` is expected to be on the same GameObject.

Do this instead:

```csharp
private CanvasGroup canvasGroup;

/// <summary>
/// Caches the CanvasGroup from this GameObject.
/// </summary>
private void Awake()
{
    canvasGroup = GetComponent<CanvasGroup>();
}
```
