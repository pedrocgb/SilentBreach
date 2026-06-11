using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

public enum LockpickDifficulty
{
    VeryEasy,
    Easy,
    Medium,
    Hard,
    VeryHard
}

[CreateAssetMenu(fileName = "LockpickMinigameDefinition", menuName = "Breezeblocks/Missions/Lockpick Minigame Definition")]
public sealed class LockpickMinigameDefinition : ScriptableObject
{
    private const int MinimumTumblerCount = 1;
    private const float MinimumDepth = 1f;
    private const float MinimumWindowDuration = 0.02f;
    private const float MinimumPushSpeed = 0.01f;
    private const float MinimumShakeRampStart = 0f;
    private const float MaximumShakeRampStart = 1f;

    [FoldoutGroup("Difficulty")]
    [SerializeField] private LockpickDifficulty difficulty = LockpickDifficulty.Medium;

    [FoldoutGroup("Gameplay"), MinValue(MinimumTumblerCount)]
    [SerializeField] private int tumblerCount = 5;

    [FoldoutGroup("Gameplay"), MinValue(MinimumDepth), SuffixLabel("px", true)]
    [SerializeField] private float hotspotMinDepth = 68f;

    [FoldoutGroup("Gameplay"), MinValue(MinimumDepth), SuffixLabel("px", true)]
    [SerializeField] private float hotspotMaxDepth = 150f;

    [FoldoutGroup("Gameplay"), MinValue(MinimumDepth), SuffixLabel("px", true)]
    [SerializeField] private float maxPushDepth = 205f;

    [FoldoutGroup("Gameplay"), MinValue(MinimumPushSpeed), SuffixLabel("px/s", true)]
    [SerializeField] private float pushSpeed = 165f;

    [FoldoutGroup("Gameplay"), MinValue(MinimumWindowDuration), SuffixLabel("s", true)]
    [SerializeField] private float hotspotWindowDuration = 0.22f;

    [FoldoutGroup("Gameplay"), Range(MinimumShakeRampStart, MaximumShakeRampStart), SuffixLabel("x hotspot", true)]
    [SerializeField] private float hotspotShakeRampStartNormalizedDepth = 0.8f;

    public LockpickDifficulty Difficulty => difficulty;
    public int TumblerCount => Mathf.Max(MinimumTumblerCount, tumblerCount);
    public float HotspotMinDepth => Mathf.Max(MinimumDepth, hotspotMinDepth);
    public float HotspotMaxDepth => Mathf.Max(HotspotMinDepth, hotspotMaxDepth);
    public float MaxPushDepth => Mathf.Max(HotspotMaxDepth, maxPushDepth);
    public float PushSpeed => Mathf.Max(MinimumPushSpeed, pushSpeed);
    public float HotspotWindowDuration => Mathf.Max(MinimumWindowDuration, hotspotWindowDuration);
    public float HotspotShakeRampStartNormalizedDepth => Mathf.Clamp(hotspotShakeRampStartNormalizedDepth, MinimumShakeRampStart, MaximumShakeRampStart);

    /// <summary>
    /// Clamps authored lockpicking values into safe runtime ranges.
    /// </summary>
    private void OnValidate()
    {
        tumblerCount = Mathf.Max(MinimumTumblerCount, tumblerCount);
        hotspotMinDepth = Mathf.Max(MinimumDepth, hotspotMinDepth);
        hotspotMaxDepth = Mathf.Max(hotspotMinDepth, hotspotMaxDepth);
        maxPushDepth = Mathf.Max(hotspotMaxDepth, maxPushDepth);
        pushSpeed = Mathf.Max(MinimumPushSpeed, pushSpeed);
        hotspotWindowDuration = Mathf.Max(MinimumWindowDuration, hotspotWindowDuration);
        hotspotShakeRampStartNormalizedDepth = Mathf.Clamp(hotspotShakeRampStartNormalizedDepth, MinimumShakeRampStart, MaximumShakeRampStart);
    }
}

}
