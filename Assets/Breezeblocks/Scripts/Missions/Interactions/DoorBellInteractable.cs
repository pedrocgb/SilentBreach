using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Door Bell Interactable")]
public sealed class DoorBellInteractable : PlayerWorldInteractable
{
    private const string DefaultInteractionLabel = "Campainha";

    [FoldoutGroup("Door Bell")]
    [SerializeField] private string interactionLabel = DefaultInteractionLabel;

    [FoldoutGroup("Door Bell")]
    [SerializeField] private Transform sfxOrigin;

    [FoldoutGroup("SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet ringSfx = new();

    [FoldoutGroup("SFX"), Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [SerializeField] private float noiseAmount = 0.35f;

    [FoldoutGroup("Noise")]
    [SerializeField] private NoiseType noiseType = NoiseType.Common;

    [FoldoutGroup("Noise")]
    [SerializeField] private bool extremeNoise;

    [FoldoutGroup("Affected Enemies"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<EnemyMovementController> affectedEnemies = new();

    public override string InteractionDisplayName => string.IsNullOrWhiteSpace(interactionLabel)
        ? DefaultInteractionLabel
        : interactionLabel;

    private readonly List<AIHearing> affectedHearingListeners = new();

    /// <summary>
    /// Builds the targeted hearing-listener cache before the bell can be used.
    /// </summary>
    private void Awake()
    {
        RebuildAffectedHearingListeners();
    }

    /// <summary>
    /// Rebuilds affected listeners before registering this interactable.
    /// </summary>
    protected override void OnEnable()
    {
        RebuildAffectedHearingListeners();
        base.OnEnable();
    }

    /// <summary>
    /// Validates bell feedback and removes missing enemy references while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        interactionLabel = string.IsNullOrWhiteSpace(interactionLabel)
            ? DefaultInteractionLabel
            : interactionLabel.Trim();
        ringSfx ??= new AudioClipSet();
        ringSfx.Validate();
        sfxVolume = Mathf.Clamp01(sfxVolume);
        noiseAmount = Mathf.Max(0f, noiseAmount);
        affectedEnemies.RemoveAll(enemy => enemy == null);
    }

    /// <summary>
    /// Rings the bell, plays world SFX, and sends noise only to configured enemies.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        RebuildAffectedHearingListeners();
        Vector3 origin = sfxOrigin != null ? sfxOrigin.position : transform.position;
        if (noiseAmount > 0f && affectedHearingListeners.Count > 0)
        {
            NoiseEvent noiseEvent = new((Vector2)origin, noiseAmount, noiseType, gameObject, extremeNoise);
            NoiseManager.EmitNoiseToListeners(noiseEvent, affectedHearingListeners);
        }

        if (ringSfx != null && ringSfx.HasAnyClip)
            WorldSfxManager.Instance?.PlayClipSetAt(origin, ringSfx, noiseType, sfxVolume);

        return true;
    }

    /// <summary>
    /// Resolves and de-duplicates AI hearing components from the configured enemy list.
    /// </summary>
    private void RebuildAffectedHearingListeners()
    {
        affectedHearingListeners.Clear();
        for (int i = 0; i < affectedEnemies.Count; i++)
        {
            EnemyMovementController enemy = affectedEnemies[i];
            if (enemy == null || !enemy.TryGetComponent(out AIHearing hearing) || hearing == null)
                continue;

            if (!affectedHearingListeners.Contains(hearing))
                affectedHearingListeners.Add(hearing);
        }
    }
}

}
