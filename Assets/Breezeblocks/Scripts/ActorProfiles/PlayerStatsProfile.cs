using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Breezeblocks/Actor Profiles/Player Stats Profile", fileName = "Player Stats Profile")]
public class PlayerStatsProfile : ScriptableObject
{
    [FoldoutGroup("Core"), InlineProperty, HideLabel]
    public ActorHealthSettings Health = new();

    [FoldoutGroup("Core"), InlineProperty, HideLabel]
    public ActorStaggerSettings Stagger = new();

    [FoldoutGroup("Movement"), InlineProperty, HideLabel]
    public PlayerMovementSettings Movement = new();

    [FoldoutGroup("Noise"), InlineProperty, HideLabel]
    public PlayerNoiseSettings Noise = new();

    [FoldoutGroup("Noise"), InlineProperty, HideLabel]
    public PlayerNoiseEmitterSettings NoiseEmitter = new();

    [FoldoutGroup("Visibility"), InlineProperty, HideLabel]
    public PlayerVisibilitySettings Visibility = new();

    [FoldoutGroup("Vision"), InlineProperty, HideLabel]
    public PlayerVisionLightSettings VisionLight = new();

    [FoldoutGroup("Stamina"), InlineProperty, HideLabel]
    public PlayerStaminaSettings Stamina = new();
}
