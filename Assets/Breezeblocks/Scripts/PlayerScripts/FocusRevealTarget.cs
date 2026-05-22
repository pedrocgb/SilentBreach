using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Focus Reveal Target")]
public class FocusRevealTarget : MonoBehaviour
{
    private static readonly List<FocusRevealTarget> ActiveTargetsInternal = new();
    private static bool globalFocusVisible;

    [FoldoutGroup("Reveal"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<SpriteRenderer> revealRenderers = new();

    [FoldoutGroup("Reveal"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> revealObjects = new();

    public static bool GlobalFocusVisible => globalFocusVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        globalFocusVisible = false;
        ActiveTargetsInternal.Clear();
    }

    private void OnEnable()
    {
        if (!ActiveTargetsInternal.Contains(this))
            ActiveTargetsInternal.Add(this);

        ApplyVisibility(globalFocusVisible);
    }

    private void OnDisable()
    {
        ActiveTargetsInternal.Remove(this);
        ApplyVisibility(false);
    }

    public static void SetGlobalFocusVisible(bool visible)
    {
        globalFocusVisible = visible;

        for (int i = 0; i < ActiveTargetsInternal.Count; i++)
        {
            if (ActiveTargetsInternal[i] != null)
                ActiveTargetsInternal[i].ApplyVisibility(visible);
        }
    }

    private void ApplyVisibility(bool visible)
    {
        for (int i = 0; i < revealRenderers.Count; i++)
        {
            if (revealRenderers[i] != null)
                revealRenderers[i].enabled = visible;
        }

        for (int i = 0; i < revealObjects.Count; i++)
        {
            if (revealObjects[i] != null)
                revealObjects[i].SetActive(visible);
        }
    }
}
