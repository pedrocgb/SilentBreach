using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Focus Reveal Target")]
public class FocusRevealTarget : MonoBehaviour
{
    private sealed class TintTarget
    {
        public SpriteRenderer SpriteRenderer;
        public Graphic Graphic;
        public Color DefaultColor;
    }

    private static readonly List<FocusRevealTarget> ActiveTargetsInternal = new();
    private static bool globalFocusVisible;

    [FoldoutGroup("Reveal"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> revealObjects = new();

    private readonly List<TintTarget> tintTargets = new();
    private bool hasRevealTintOverride;
    private Color revealTintOverride = Color.white;

    public static bool GlobalFocusVisible => globalFocusVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    // Executes the ResetStatics routine.
    private static void ResetStatics()
    {
        globalFocusVisible = false;
        ActiveTargetsInternal.Clear();
    }

    // Executes the OnEnable routine.
    private void OnEnable()
    {
        if (!ActiveTargetsInternal.Contains(this))
            ActiveTargetsInternal.Add(this);

        CacheTintTargets();
        ApplyRevealTint();
        ApplyVisibility(globalFocusVisible);
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        ActiveTargetsInternal.Remove(this);
        ApplyVisibility(false);
        ClearRevealTintOverride();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        CacheTintTargets();
        ApplyRevealTint();
    }

    // Executes the SetGlobalFocusVisible routine.
    public static void SetGlobalFocusVisible(bool visible)
    {
        globalFocusVisible = visible;

        for (int i = 0; i < ActiveTargetsInternal.Count; i++)
        {
            if (ActiveTargetsInternal[i] != null)
                ActiveTargetsInternal[i].ApplyVisibility(visible);
        }
    }

    // Executes the ResetRuntimeState routine.
    public static void ResetRuntimeState()
    {
        globalFocusVisible = false;

        for (int i = ActiveTargetsInternal.Count - 1; i >= 0; i--)
        {
            FocusRevealTarget target = ActiveTargetsInternal[i];
            if (target == null)
            {
                ActiveTargetsInternal.RemoveAt(i);
                continue;
            }

            target.ClearRevealTintOverride();
            target.ApplyVisibility(false);
        }
    }

    // Executes the SetRevealTintOverride routine.
    public void SetRevealTintOverride(Color tintColor)
    {
        hasRevealTintOverride = true;
        revealTintOverride = tintColor;
        ApplyRevealTint();
    }

    // Executes the ClearRevealTintOverride routine.
    public void ClearRevealTintOverride()
    {
        hasRevealTintOverride = false;
        ApplyRevealTint();
    }

    // Executes the ApplyVisibility routine.
    private void ApplyVisibility(bool visible)
    {
        for (int i = 0; i < revealObjects.Count; i++)
        {
            GameObject revealObject = revealObjects[i];
            if (revealObject == null)
                continue;

            revealObject.SetActive(visible);
        }
    }

    // Executes the CacheTintTargets routine.
    private void CacheTintTargets()
    {
        tintTargets.Clear();
        HashSet<int> cachedInstanceIds = new();

        for (int i = 0; i < revealObjects.Count; i++)
        {
            GameObject revealObject = revealObjects[i];
            if (revealObject == null)
                continue;

            SpriteRenderer[] childRenderers = revealObject.GetComponentsInChildren<SpriteRenderer>(true);
            for (int childIndex = 0; childIndex < childRenderers.Length; childIndex++)
                TryAddTintTarget(childRenderers[childIndex], cachedInstanceIds);

            Graphic[] childGraphics = revealObject.GetComponentsInChildren<Graphic>(true);
            for (int childIndex = 0; childIndex < childGraphics.Length; childIndex++)
                TryAddTintTarget(childGraphics[childIndex], cachedInstanceIds);
        }
    }

    // Executes the ApplyRevealTint routine.
    private void ApplyRevealTint()
    {
        for (int i = 0; i < tintTargets.Count; i++)
        {
            TintTarget tintTarget = tintTargets[i];
            Color targetColor = hasRevealTintOverride ? revealTintOverride : tintTarget.DefaultColor;

            if (tintTarget.SpriteRenderer != null)
                tintTarget.SpriteRenderer.color = targetColor;

            if (tintTarget.Graphic != null)
                tintTarget.Graphic.color = targetColor;
        }
    }

    // Executes the TryAddTintTarget routine.
    private void TryAddTintTarget(SpriteRenderer renderer, HashSet<int> cachedInstanceIds)
    {
        if (renderer == null || cachedInstanceIds == null || !cachedInstanceIds.Add(renderer.GetInstanceID()))
            return;

        tintTargets.Add(new TintTarget
        {
            SpriteRenderer = renderer,
            DefaultColor = renderer.color
        });
    }

    // Executes the TryAddTintTarget routine.
    private void TryAddTintTarget(Graphic graphic, HashSet<int> cachedInstanceIds)
    {
        if (graphic == null || cachedInstanceIds == null || !cachedInstanceIds.Add(graphic.GetInstanceID()))
            return;

        tintTargets.Add(new TintTarget
        {
            Graphic = graphic,
            DefaultColor = graphic.color
        });
    }
}
