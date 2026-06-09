using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[CreateAssetMenu(fileName = "FlashlightUtilityData", menuName = "Breezeblocks/Equipment/Flashlight Utility")]
public class FlashlightUtilityData : UtilityItemData
{
    [FoldoutGroup("Flashlight"), LabelText("Start Enabled When Equipped")]
    [SerializeField] private bool startEnabledWhenEquipped;

    [FoldoutGroup("Flashlight"), LabelText("Toggle SFX"), InlineProperty]
    [SerializeField] private AudioClipSet toggleSfx = new();

    [FoldoutGroup("Flashlight"), LabelText("Toggle SFX Type")]
    [SerializeField] private NoiseType toggleSfxType = NoiseType.Silent;

    [FoldoutGroup("Flashlight"), LabelText("Toggle Noise"), MinValue(0f)]
    [SerializeField] private float toggleNoise = 0.08f;

    [FoldoutGroup("Flashlight"), LabelText("Toggle Noise Duration"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float toggleNoiseDuration = 0.1f;

    [FoldoutGroup("Flashlight"), LabelText("Toggle Extreme Noise")]
    [SerializeField] private bool toggleExtremeNoise;

    public override string UtilityTypeName => "Flashlight";
    public bool StartEnabledWhenEquipped => startEnabledWhenEquipped;
    public AudioClipSet ToggleSfx => toggleSfx;
    public NoiseType ToggleSfxType => toggleSfxType;
    public float ToggleNoise => toggleNoise;
    public float ToggleNoiseDuration => toggleNoiseDuration;
    public bool ToggleExtremeNoise => toggleExtremeNoise;

    protected override void OnValidate()
    {
        base.OnValidate();
        toggleSfx ??= new AudioClipSet();
        toggleSfx.Validate();
        toggleNoise = Mathf.Max(0f, toggleNoise);
        toggleNoiseDuration = Mathf.Max(0f, toggleNoiseDuration);
    }
}
}
