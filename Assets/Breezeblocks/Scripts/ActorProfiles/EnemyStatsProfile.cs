using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Breezeblocks/Actor Profiles/Enemy Stats Profile", fileName = "Enemy Stats Profile")]
public class EnemyStatsProfile : ScriptableObject
{
    [TabGroup("Profile", "Identity"), InlineProperty, HideLabel]
    public MissionActorIdentitySettings Identity = new();

    [TabGroup("Profile", "Health"), InlineProperty, HideLabel]
    public ActorHealthSettings Health = new();

    [TabGroup("Profile", "Core"), InlineProperty, HideLabel]
    public ActorStaggerSettings Stagger = new();

    [TabGroup("Profile", "Movement"), InlineProperty, HideLabel]
    public EnemyMovementSettings Movement = new();

    [TabGroup("Profile", "Perception"), InlineProperty, HideLabel]
    public EnemyVisionSettings Vision = new();

    [TabGroup("Profile", "Perception"), InlineProperty, HideLabel]
    public EnemyHearingSettings Hearing = new();

    [TabGroup("Profile", "Room Awareness"), InlineProperty, HideLabel]
    public EnemyRoomAwarenessSettings RoomAwareness = new();

    [TabGroup("Profile", "Room Awareness"), InlineProperty, HideLabel]
    public EnemyConfusedReactionSettings ConfusedReaction = new();

    [TabGroup("Profile", "Door Bell"), InlineProperty, HideLabel]
    public EnemyDoorBellReactionSettings DoorBellReaction = new();

    [TabGroup("Profile", "Sleep"), InlineProperty, HideLabel]
    public EnemySleepSettings Sleep = new();

    [TabGroup("Profile", "Combat"), LabelText("Is Combatant")]
    public bool IsCombatant = true;

    [TabGroup("Profile", "Combat"), InlineProperty, HideLabel]
    [ShowIf(nameof(IsCombatant))]
    public EnemyCombatSettings Combat = new();

    [TabGroup("Profile", "Combat"), InlineProperty, HideLabel]
    [ShowIf(nameof(IsCombatant))]
    public EnemyMeleeSettings Melee = new();

    [TabGroup("Profile", "Hands"), InlineProperty, HideLabel]
    public CharacterOrbitHandsSettings Hands = new();

    [TabGroup("Profile", "Audio"), LabelText("Footstep SFX"), InlineProperty]
    public ActorFootstepSfxSettings Footsteps = new();

    /// <summary>
    /// Ensures nested settings exist and clamps their values while editing the profile asset.
    /// </summary>
    private void OnValidate()
    {
        Identity ??= new MissionActorIdentitySettings();
        Health ??= new ActorHealthSettings();
        Stagger ??= new ActorStaggerSettings();
        Movement ??= new EnemyMovementSettings();
        Vision ??= new EnemyVisionSettings();
        Hearing ??= new EnemyHearingSettings();
        RoomAwareness ??= new EnemyRoomAwarenessSettings();
        ConfusedReaction ??= new EnemyConfusedReactionSettings();
        DoorBellReaction ??= new EnemyDoorBellReactionSettings();
        Sleep ??= new EnemySleepSettings();
        Combat ??= new EnemyCombatSettings();
        Melee ??= new EnemyMeleeSettings();
        Hands ??= new CharacterOrbitHandsSettings();
        Footsteps ??= new ActorFootstepSfxSettings();

        Identity.Validate();
        Health.Validate();
        RoomAwareness.Validate();
        ConfusedReaction.Validate();
        DoorBellReaction.Validate();
        Sleep.Validate();
        Hands.Validate();
        Footsteps.Validate();
    }
}
