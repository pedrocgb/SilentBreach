using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

[Serializable]
public sealed class PlayerBadgeVisualDefinition
{
    [SerializeField] private string displayName;

    [PreviewField(96, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite sprite;

    public string DisplayName => displayName ?? string.Empty;
    public Sprite Sprite => sprite;

    /// <summary>
    /// Creates a badge visual definition with its expected default name.
    /// </summary>
    public PlayerBadgeVisualDefinition(string defaultDisplayName)
    {
        displayName = defaultDisplayName;
    }

    /// <summary>
    /// Removes unwanted whitespace from the configured badge name.
    /// </summary>
    public void Sanitize()
    {
        displayName = displayName != null ? displayName.Trim() : string.Empty;
    }
}

[CreateAssetMenu(fileName = "PlayerProgression", menuName = "Breezeblocks/Hideout/Player Progression")]
public sealed class PlayerProgressionDefinition : ScriptableObject
{
    [FoldoutGroup("Badges")]
    [SerializeField] private PlayerBadgeVisualDefinition amateur = new("Amador");

    [FoldoutGroup("Badges")]
    [SerializeField] private PlayerBadgeVisualDefinition operatorBadge = new("Operador");

    [FoldoutGroup("Badges")]
    [SerializeField] private PlayerBadgeVisualDefinition specialist = new("Especialista");

    [FoldoutGroup("Badges")]
    [SerializeField] private PlayerBadgeVisualDefinition boss = new("Chefão");

    /// <summary>
    /// Returns the configured visual definition for a progression badge.
    /// </summary>
    public PlayerBadgeVisualDefinition GetBadge(PlayerBadgeId badgeId)
    {
        return badgeId switch
        {
            PlayerBadgeId.Amador => amateur,
            PlayerBadgeId.Operador => operatorBadge,
            PlayerBadgeId.Especialista => specialist,
            PlayerBadgeId.Chefao => boss,
            _ => amateur
        };
    }

    /// <summary>
    /// Returns the configured badge visual represented by the supplied level.
    /// </summary>
    public PlayerBadgeVisualDefinition GetBadgeForLevel(int level)
    {
        return GetBadge(PlayerProgressionRules.GetBadgeId(level));
    }

    /// <summary>
    /// Normalizes all author-facing badge names inside the inspector.
    /// </summary>
    private void OnValidate()
    {
        amateur?.Sanitize();
        operatorBadge?.Sanitize();
        specialist?.Sanitize();
        boss?.Sanitize();
    }
}

}
