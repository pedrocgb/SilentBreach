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

    [FoldoutGroup("Lights"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> controlledLights = new();

    [FoldoutGroup("Lights")]
    [SerializeField] private bool startEnabled = true;

    [FoldoutGroup("Enemy Lookaround")]
    [SerializeField] private LightSwitchLookaroundPreset enemyLookaroundPreset = LightSwitchLookaroundPreset.RightLookaround;

    [FoldoutGroup("SFX"), InlineProperty, LabelText("Toggle SFX")]
    [SerializeField] private AudioClipSet toggleSfx = new();

    [FoldoutGroup("SFX")]
    [SerializeField] private NoiseType toggleSfxType = NoiseType.Common;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsOn => isOn;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPowerDisabled => isPowerDisabled;

    public IReadOnlyList<GameObject> ControlledLights => controlledLights;
    public LightSwitchLookaroundPreset EnemyLookaroundPreset => enemyLookaroundPreset;
    public WorldStateChangeSource LastStateChangeSource => lastStateChangeSource;
    public GameObject LastStateChangeActor => lastStateChangeActor;

    private bool isOn;
    private bool isPowerDisabled;
    private WorldSfxManager worldSfxManager;
    private WorldStateChangeSource lastStateChangeSource = WorldStateChangeSource.System;
    private GameObject lastStateChangeActor;

    /// <summary>
    /// Applies the authored starting state unless an external power shutdown already disabled this switch.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        isOn = !isPowerDisabled && startEnabled;
        lastStateChangeSource = WorldStateChangeSource.System;
        lastStateChangeActor = null;
        ApplyLightState();
    }

    /// <summary>
    /// Validates reusable switch audio settings while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        toggleSfx ??= new AudioClipSet();
        toggleSfx.Validate();
    }

    /// <summary>
    /// Returns whether this switch has power and currently accepts player interaction.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        return !isPowerDisabled && base.CanInteract(interactorRoot);
    }

    /// <summary>
    /// Toggles powered lights when the player interacts with this switch.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        return SetLightState(!isOn, playSfx: true, interactorRoot);
    }

    /// <summary>
    /// Applies a requested light state unless permanent power loss prevents turning lights on.
    /// </summary>
    public bool SetLightState(bool enabled, bool playSfx = true, GameObject interactorRoot = null)
    {
        if (enabled && isPowerDisabled)
            return false;

        if (isOn == enabled)
            return false;

        isOn = enabled;
        lastStateChangeSource = ResolveStateChangeSource(interactorRoot);
        lastStateChangeActor = interactorRoot;
        ApplyLightState();

        if (playSfx)
            PlayToggleSfx();

        LightStateChanged?.Invoke(this, isOn);
        return true;
    }

    /// <summary>
    /// Permanently disables or restores switch power, forcing controlled lights off when disabled.
    /// </summary>
    public void SetPowerDisabled(bool disabled)
    {
        if (isPowerDisabled == disabled && (!disabled || !isOn))
            return;

        bool powerStateChanged = isPowerDisabled != disabled;
        isPowerDisabled = disabled;
        bool stateChanged = disabled && isOn;
        if (disabled)
        {
            isOn = false;
            lastStateChangeSource = WorldStateChangeSource.System;
            lastStateChangeActor = null;
            ApplyLightState();
        }

        if (disabled && (powerStateChanged || stateChanged))
            LightStateChanged?.Invoke(this, false);

        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Activates or deactivates every scene light controlled by this switch.
    /// </summary>
    private void ApplyLightState()
    {
        for (int i = 0; i < controlledLights.Count; i++)
        {
            if (controlledLights[i] != null)
                controlledLights[i].SetActive(isOn);
        }
    }

    /// <summary>
    /// Plays the configured world-space switch feedback when available.
    /// </summary>
    private void PlayToggleSfx()
    {
        if (toggleSfx == null || !toggleSfx.HasAnyClip)
            return;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        worldSfxManager?.PlayClipSetAt(transform.position, toggleSfx, toggleSfxType);
    }

    /// <summary>
    /// Classifies the actor that requested a light-switch state change for room-awareness filtering.
    /// </summary>
    private static WorldStateChangeSource ResolveStateChangeSource(GameObject actorRoot)
    {
        if (actorRoot == null)
            return WorldStateChangeSource.System;

        return actorRoot.GetComponentInParent<EnemyMovementController>() != null
            ? WorldStateChangeSource.Enemy
            : WorldStateChangeSource.Player;
    }
}

}
