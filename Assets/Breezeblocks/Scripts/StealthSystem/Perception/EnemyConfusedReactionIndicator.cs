using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Confused Reaction Indicator")]
public sealed class EnemyConfusedReactionIndicator : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private GameObject indicatorRoot;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform indicatorGraphic;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPlaying => activeSequence != null && activeSequence.IsActive();

    private Sequence activeSequence;
    private float poseHoldDuration = 0.2f;
    private Vector2 firstLocalPosition = new(1f, 0f);
    private float firstLocalRotationZ = 30f;
    private Vector2 secondLocalPosition = new(-1f, 0f);
    private float secondLocalRotationZ = -30f;

    /// <summary>
    /// Hides the indicator when the component is first added in the editor.
    /// </summary>
    private void Reset()
    {
        HideImmediate();
    }

    /// <summary>
    /// Clamps authored timing values when edited in the inspector.
    /// </summary>
    private void OnValidate()
    {
        ClampSettings();
    }

    /// <summary>
    /// Stops any active tween and hides the indicator when the component disables.
    /// </summary>
    private void OnDisable()
    {
        HideImmediate();
    }

    /// <summary>
    /// Plays the confused indicator animation for the requested duration.
    /// </summary>
    public void Play(float duration)
    {
        if (indicatorRoot == null || indicatorGraphic == null)
            return;

        float clampedDuration = Mathf.Max(0.02f, duration);
        HideImmediate();
        indicatorRoot.SetActive(true);

        activeSequence = DOTween.Sequence()
            .SetUpdate(true)
            .AppendCallback(ApplyFirstPose)
            .AppendInterval(poseHoldDuration)
            .AppendCallback(ApplySecondPose)
            .AppendInterval(poseHoldDuration)
            .SetLoops(-1, LoopType.Restart);

        DOVirtual.DelayedCall(clampedDuration, HideImmediate, true)
            .SetTarget(this)
            .SetUpdate(true);
    }

    /// <summary>
    /// Applies profile-authored confused reaction animation settings.
    /// </summary>
    public void ApplySettings(EnemyConfusedReactionSettings settings)
    {
        if (settings == null)
            return;

        poseHoldDuration = settings.PoseHoldDuration;
        firstLocalPosition = settings.FirstLocalPosition;
        firstLocalRotationZ = settings.FirstLocalRotationZ;
        secondLocalPosition = settings.SecondLocalPosition;
        secondLocalRotationZ = settings.SecondLocalRotationZ;
        ClampSettings();
    }

    /// <summary>
    /// Stops the current animation immediately and hides the indicator root.
    /// </summary>
    public void HideImmediate()
    {
        DOTween.Kill(this);

        if (activeSequence != null)
        {
            activeSequence.Kill();
            activeSequence = null;
        }

        if (indicatorGraphic != null)
            indicatorGraphic.SetLocalPositionAndRotation(firstLocalPosition, Quaternion.Euler(0f, 0f, firstLocalRotationZ));

        if (indicatorRoot != null)
            indicatorRoot.SetActive(false);
    }

    /// <summary>
    /// Applies the first snapped pose of the confusion animation.
    /// </summary>
    private void ApplyFirstPose()
    {
        if (indicatorGraphic == null)
            return;

        indicatorGraphic.SetLocalPositionAndRotation(firstLocalPosition, Quaternion.Euler(0f, 0f, firstLocalRotationZ));
    }

    /// <summary>
    /// Applies the second snapped pose of the confusion animation.
    /// </summary>
    private void ApplySecondPose()
    {
        if (indicatorGraphic == null)
            return;

        indicatorGraphic.SetLocalPositionAndRotation(secondLocalPosition, Quaternion.Euler(0f, 0f, secondLocalRotationZ));
    }

    /// <summary>
    /// Clamps profile-applied animation values to safe runtime ranges.
    /// </summary>
    private void ClampSettings()
    {
        poseHoldDuration = Mathf.Max(0.02f, poseHoldDuration);
    }
}
