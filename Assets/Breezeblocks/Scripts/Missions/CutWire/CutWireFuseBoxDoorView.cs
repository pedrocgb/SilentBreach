using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
[AddComponentMenu("Breezeblocks/Missions/Cut Wire/Fuse Box Door View")]
public sealed class CutWireFuseBoxDoorView : MonoBehaviour
{
    private const float MinimumDuration = 0f;

    [FoldoutGroup("References")]
    [SerializeField] private RectTransform doorPivot;

    [FoldoutGroup("Rotation")]
    [SerializeField] private float closedYAngle;

    [FoldoutGroup("Rotation")]
    [SerializeField] private float openYAngle = 200f;

    [FoldoutGroup("Rotation")]
    [SerializeField] private float pivotZero = 90f;

    [FoldoutGroup("Animation"), MinValue(MinimumDuration), SuffixLabel("s", true)]
    [SerializeField] private float animationDuration = 0.5f;

    [FoldoutGroup("Animation")]
    [SerializeField] private Ease animationEase = Ease.InOutSine;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsOpen => isOpen;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAnimating => isAnimating;

    public event Action Opened;
    public event Action<bool> HeaderVisibilityChanged;

    private Button button;
    private Tween rotationTween;
    private Vector3 baseLocalEulerAngles;
    private float currentYAngle;
    private bool isOpen;
    private bool isAnimating;
    private bool interactionEnabled = true;
    private bool headerVisible = true;

    /// <summary>
    /// Caches the same-object button and establishes the authored closed-door presentation.
    /// </summary>
    private void Awake()
    {
        button = GetComponent<Button>();
        ResolveDoorPivot();
        CacheBaseRotation();
        ResetClosedImmediate();
    }

    /// <summary>
    /// Registers the click callback whenever this reusable door view is active.
    /// </summary>
    private void OnEnable()
    {
        button ??= GetComponent<Button>();
        button.onClick.AddListener(HandleClicked);
        RefreshButtonInteraction();
    }

    /// <summary>
    /// Removes callbacks and stops any animation owned by this view while disabled.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);

        KillRotationTween();
    }

    /// <summary>
    /// Clamps authored animation values without changing the editor-time door presentation.
    /// </summary>
    private void OnValidate()
    {
        animationDuration = Mathf.Max(MinimumDuration, animationDuration);
        pivotZero = Mathf.Clamp(pivotZero, Mathf.Min(closedYAngle, openYAngle), Mathf.Max(closedYAngle, openYAngle));
    }

    /// <summary>
    /// Restores the closed visual state immediately before a new cut-wire session begins.
    /// </summary>
    public void ResetClosedImmediate()
    {
        KillRotationTween();
        ResolveDoorPivot();
        CacheBaseRotation();
        isOpen = false;
        isAnimating = false;
        ApplyYAngle(closedYAngle);
        SetHeaderVisible(true);
        RefreshButtonInteraction();
    }

    /// <summary>
    /// Allows the session controller to enable or disable opening the fuse-box door.
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        RefreshButtonInteraction();
    }

    /// <summary>
    /// Opens the fuse-box door when it is closed and available for interaction.
    /// </summary>
    public bool TryOpen()
    {
        if (!interactionEnabled || isOpen || isAnimating)
            return false;

        AnimateTo(openYAngle, opening: true, () => Opened?.Invoke());
        return true;
    }

    /// <summary>
    /// Reverses the fuse-box door animation and invokes completion after it is fully closed.
    /// </summary>
    public void Close(Action completed)
    {
        if (!isOpen && !isAnimating)
        {
            ResetClosedImmediate();
            completed?.Invoke();
            return;
        }

        AnimateTo(closedYAngle, opening: false, completed);
    }

    /// <summary>
    /// Starts one unscaled UI rotation animation toward the requested authored angle.
    /// </summary>
    private void AnimateTo(float targetYAngle, bool opening, Action completed)
    {
        KillRotationTween();
        isAnimating = true;
        interactionEnabled = false;
        RefreshButtonInteraction();

        if (doorPivot == null || animationDuration <= MinimumDuration)
        {
            CompleteAnimation(targetYAngle, opening, completed);
            return;
        }

        rotationTween = DOVirtual
            .Float(currentYAngle, targetYAngle, animationDuration, ApplyYAngle)
            .SetEase(animationEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                rotationTween = null;
                CompleteAnimation(targetYAngle, opening, completed);
            });
    }

    /// <summary>
    /// Finalizes an open or close animation and publishes its completion callback.
    /// </summary>
    private void CompleteAnimation(float targetYAngle, bool opening, Action completed)
    {
        ApplyYAngle(targetYAngle);
        isOpen = opening;
        isAnimating = false;
        RefreshButtonInteraction();
        completed?.Invoke();
    }

    /// <summary>
    /// Applies an exact Y rotation while preserving the pivot's authored X and Z rotation.
    /// </summary>
    private void ApplyYAngle(float yAngle)
    {
        currentYAngle = yAngle;
        if (doorPivot != null)
            doorPivot.localRotation = Quaternion.Euler(baseLocalEulerAngles.x, yAngle, baseLocalEulerAngles.z);

        SetHeaderVisible(yAngle < pivotZero);
    }

    /// <summary>
    /// Publishes header visibility only when crossing the configured pivot threshold.
    /// </summary>
    private void SetHeaderVisible(bool visible)
    {
        if (headerVisible == visible)
            return;

        headerVisible = visible;
        HeaderVisibilityChanged?.Invoke(visible);
    }

    /// <summary>
    /// Requests the opening animation when the player clicks the door image.
    /// </summary>
    private void HandleClicked()
    {
        TryOpen();
    }

    /// <summary>
    /// Resolves the optional external pivot or falls back to this UI object's RectTransform.
    /// </summary>
    private void ResolveDoorPivot()
    {
        if (doorPivot == null)
            doorPivot = transform as RectTransform;
    }

    /// <summary>
    /// Caches the authored non-Y rotation axes used throughout door animation.
    /// </summary>
    private void CacheBaseRotation()
    {
        if (doorPivot != null)
            baseLocalEulerAngles = doorPivot.localEulerAngles;
    }

    /// <summary>
    /// Keeps the door button available only while the closed door may be opened.
    /// </summary>
    private void RefreshButtonInteraction()
    {
        if (button != null)
            button.interactable = interactionEnabled && !isOpen && !isAnimating;
    }

    /// <summary>
    /// Stops and clears the currently owned rotation tween.
    /// </summary>
    private void KillRotationTween()
    {
        rotationTween?.Kill();
        rotationTween = null;
    }
}

}
