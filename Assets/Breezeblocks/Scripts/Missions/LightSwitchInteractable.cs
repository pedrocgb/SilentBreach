using System;
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
    public event Action<LightSwitchInteractable, bool> LightStateChanged;

    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("Lights"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> controlledLights = new();

    [FoldoutGroup("Lights")]
    [SerializeField] private bool startEnabled = true;

    [FoldoutGroup("SFX"), InlineProperty, LabelText("Toggle SFX")]
    [SerializeField] private AudioClipSet toggleSfx = new();

    [FoldoutGroup("SFX")]
    [SerializeField] private NoiseType toggleSfxType = NoiseType.Common;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsOn => isOn;

    public IReadOnlyList<GameObject> ControlledLights => controlledLights;

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
        return SetLightState(!isOn, playSfx: true, interactorRoot);
    }

    public bool SetLightState(bool enabled, bool playSfx = true, GameObject interactorRoot = null)
    {
        if (isOn == enabled)
            return false;

        isOn = enabled;
        ApplyLightState();

        if (playSfx)
            PlayToggleSfx();

        LightStateChanged?.Invoke(this, isOn);
        return true;
    }

    private void ApplyLightState()
    {
        for (int i = 0; i < controlledLights.Count; i++)
        {
            if (controlledLights[i] != null)
                controlledLights[i].SetActive(isOn);
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
