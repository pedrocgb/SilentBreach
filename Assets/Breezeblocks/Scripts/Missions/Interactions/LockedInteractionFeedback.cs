using System;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

/// <summary>
/// Describes a temporary prompt animation requested by an interactable.
/// </summary>
public readonly struct InteractionPromptFeedback
{
    public readonly string Label;
    public readonly Color FlashColor;
    public readonly float Duration;
    public readonly float Strength;
    public readonly int Vibrato;

    /// <summary>
    /// Creates a UI feedback payload for the active interaction prompt.
    /// </summary>
    public InteractionPromptFeedback(string label, Color flashColor, float duration, float strength, int vibrato)
    {
        Label = label;
        FlashColor = flashColor;
        Duration = Mathf.Max(0f, duration);
        Strength = Mathf.Max(0f, strength);
        Vibrato = Mathf.Max(1, vibrato);
    }
}

/// <summary>
/// Shared settings for locked interactables that should stay visible but deny use without lockpicks.
/// </summary>
[Serializable]
public sealed class LockedInteractionFeedbackSettings
{
    private const string DefaultAttemptLabel = "Abrir";
    private const string DefaultLockedLabel = "Trancado";

    [FoldoutGroup("Text")]
    [SerializeField] private string attemptLabel = DefaultAttemptLabel;

    [FoldoutGroup("Text")]
    [SerializeField] private string lockedLabel = DefaultLockedLabel;

    [FoldoutGroup("Prompt Animation")]
    [SerializeField] private Color flashColor = new(1f, 0.08f, 0.05f, 1f);

    [FoldoutGroup("Prompt Animation"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float animationDuration = 0.28f;

    [FoldoutGroup("Prompt Animation"), LabelText("Move Distance"), MinValue(0f)]
    [SerializeField] private float shakeStrength = 8f;

    [FoldoutGroup("Prompt Animation"), LabelText("Move Cycles"), MinValue(1)]
    [SerializeField] private int shakeVibrato = 16;

    [FoldoutGroup("Audio"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet lockedSfx = new();

    [FoldoutGroup("Audio"), Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [SerializeField] private float noiseAmount = 0.2f;

    [FoldoutGroup("Noise")]
    [SerializeField] private NoiseType noiseType = NoiseType.Common;

    [FoldoutGroup("Noise")]
    [SerializeField] private bool extremeNoise;

    public string AttemptLabel => string.IsNullOrWhiteSpace(attemptLabel) ? DefaultAttemptLabel : attemptLabel;
    public string LockedLabel => string.IsNullOrWhiteSpace(lockedLabel) ? DefaultLockedLabel : lockedLabel;

    /// <summary>
    /// Normalizes author-facing feedback values after inspector edits.
    /// </summary>
    public void Validate()
    {
        attemptLabel = string.IsNullOrWhiteSpace(attemptLabel) ? DefaultAttemptLabel : attemptLabel.Trim();
        lockedLabel = string.IsNullOrWhiteSpace(lockedLabel) ? DefaultLockedLabel : lockedLabel.Trim();
        animationDuration = Mathf.Max(0f, animationDuration);
        shakeStrength = Mathf.Max(0f, shakeStrength);
        shakeVibrato = Mathf.Max(1, shakeVibrato);
        lockedSfx ??= new AudioClipSet();
        lockedSfx.Validate();
        sfxVolume = Mathf.Clamp01(sfxVolume);
        noiseAmount = Mathf.Max(0f, noiseAmount);
    }

    /// <summary>
    /// Builds the prompt animation payload used when interaction is denied by a locked object.
    /// </summary>
    public InteractionPromptFeedback CreatePromptFeedback()
    {
        return new InteractionPromptFeedback(LockedLabel, flashColor, animationDuration, shakeStrength, shakeVibrato);
    }

    /// <summary>
    /// Emits the locked-use SFX and noise from the supplied world position.
    /// </summary>
    public void PlayWorldFeedback(Vector3 position, GameObject source)
    {
        if (noiseAmount > 0f)
            NoiseManager.EmitNoise(position, noiseAmount, noiseType, source, extremeNoise);

        WorldSfxManager.Instance?.PlayClipSetAt(position, lockedSfx, noiseType, sfxVolume);
    }
}

}
