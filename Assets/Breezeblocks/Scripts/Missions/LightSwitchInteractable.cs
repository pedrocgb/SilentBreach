using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Light Switch Interactable")]
public class LightSwitchInteractable : PlayerWorldInteractable
{
    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("Lights"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<Light2D> controlledLights = new();

    [FoldoutGroup("Lights")]
    [SerializeField] private bool startEnabled = true;

    [FoldoutGroup("SFX"), InlineProperty, LabelText("Toggle SFX")]
    [SerializeField] private AudioClipSet toggleSfx = new();

    [FoldoutGroup("SFX")]
    [SerializeField] private NoiseType toggleSfxType = NoiseType.Common;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsOn => isOn;

    private bool isOn;

    protected override void OnEnable()
    {
        base.OnEnable();
        isOn = startEnabled;
        ApplyLightState();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        toggleSfx ??= new AudioClipSet();
        toggleSfx.Validate();
    }

    protected override bool Interact(GameObject interactorRoot)
    {
        isOn = !isOn;
        ApplyLightState();
        PlayToggleSfx();
        return true;
    }

    private void ApplyLightState()
    {
        for (int i = 0; i < controlledLights.Count; i++)
        {
            if (controlledLights[i] != null)
                controlledLights[i].enabled = isOn;
        }
    }

    private void PlayToggleSfx()
    {
        if (toggleSfx == null || !toggleSfx.HasAnyClip)
            return;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        worldSfxManager?.PlayClipSetAt(transform.position, toggleSfx, toggleSfxType);
    }
}

}
