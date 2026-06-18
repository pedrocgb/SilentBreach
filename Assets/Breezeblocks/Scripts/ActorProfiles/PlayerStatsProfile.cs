using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Breezeblocks/Actor Profiles/Player Stats Profile", fileName = "Player Stats Profile")]
public class PlayerStatsProfile : ScriptableObject
{
    [TabGroup("Profile", "Controls"), InlineProperty, HideLabel]
    public PlayerControlsSettings Controls = new();

    [TabGroup("Profile", "Health"), InlineProperty, HideLabel]
    public ActorHealthSettings Health = new();

    [TabGroup("Profile", "Core"), InlineProperty, HideLabel]
    public ActorStaggerSettings Stagger = new();

    [TabGroup("Profile", "Feedback"), InlineProperty, HideLabel]
    public PlayerStaggerFeedbackSettings StaggerFeedback = new();

    [TabGroup("Profile", "Movement"), InlineProperty, HideLabel]
    public PlayerMovementSettings Movement = new();

    [TabGroup("Profile", "Noise"), InlineProperty, HideLabel]
    public PlayerNoiseSettings Noise = new();

    [TabGroup("Profile", "Noise"), InlineProperty, HideLabel]
    public PlayerNoiseEmitterSettings NoiseEmitter = new();

    [TabGroup("Profile", "Visibility"), InlineProperty, HideLabel]
    public PlayerVisibilitySettings Visibility = new();

    [TabGroup("Profile", "Vision"), InlineProperty, HideLabel]
    public PlayerVisionLightSettings VisionLight = new();

    [TabGroup("Profile", "Stamina"), InlineProperty, HideLabel]
    public PlayerStaminaSettings Stamina = new();

    [TabGroup("Profile", "Focus"), InlineProperty, HideLabel]
    public PlayerFocusSettings Focus = new();

    [TabGroup("Profile", "Equipment"), InlineProperty, HideLabel]
    public PlayerEquipmentSettings Equipment = new();

    [TabGroup("Profile", "Weapon"), InlineProperty, HideLabel]
    public PlayerWeaponControllerSettings Weapon = new();

    [TabGroup("Profile", "Interaction"), InlineProperty, HideLabel]
    public PlayerInteractionSettings Interaction = new();

    [TabGroup("Profile", "Body Drag"), InlineProperty, HideLabel]
    public PlayerBodyDragSettings BodyDrag = new();

    [TabGroup("Profile", "Hands"), InlineProperty, HideLabel]
    public CharacterOrbitHandsSettings Hands = new();

    [TabGroup("Profile", "Audio"), LabelText("Footstep SFX"), InlineProperty]
    public ActorFootstepSfxSettings Footsteps = new();

    /// <summary>
    /// Ensures nested shared settings exist and clamps their values while editing the profile asset.
    /// </summary>
    private void OnValidate()
    {
        Controls ??= new PlayerControlsSettings();
        Health ??= new ActorHealthSettings();
        Stagger ??= new ActorStaggerSettings();
        StaggerFeedback ??= new PlayerStaggerFeedbackSettings();
        Movement ??= new PlayerMovementSettings();
        Noise ??= new PlayerNoiseSettings();
        NoiseEmitter ??= new PlayerNoiseEmitterSettings();
        Visibility ??= new PlayerVisibilitySettings();
        VisionLight ??= new PlayerVisionLightSettings();
        Stamina ??= new PlayerStaminaSettings();
        Focus ??= new PlayerFocusSettings();
        Equipment ??= new PlayerEquipmentSettings();
        Weapon ??= new PlayerWeaponControllerSettings();
        Interaction ??= new PlayerInteractionSettings();
        BodyDrag ??= new PlayerBodyDragSettings();
        Hands ??= new CharacterOrbitHandsSettings();
        Footsteps ??= new ActorFootstepSfxSettings();

        Controls.Validate();
        Health.Validate();
        StaggerFeedback.Validate();
        Stamina.Validate();
        Focus.Validate();
        Equipment.Validate();
        Weapon.Validate();
        Interaction.Validate();
        BodyDrag.Validate();
        Hands.Validate();
        Footsteps.Validate();
    }
}
