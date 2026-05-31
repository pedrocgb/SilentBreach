using System;
using Breezeblocks.HideoutSystem;
using DG.Tweening;
using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Focus Controller")]
public class PlayerFocusController : MonoBehaviour
{
    private const bool DefaultFocusToggleMode = false;
    private const float MinimumFocusAmount = 0.0001f;

    [FoldoutGroup("Rewired"), MinValue(0)]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired")]
    [SerializeField] private string focusAction = "Focus";

    [FoldoutGroup("References")]
    [SerializeField] private Volume targetVolume;

    [FoldoutGroup("UI")]
    [SerializeField] private Image focusFillImage;

    [FoldoutGroup("Focus"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float maxFocusSeconds = 6f;

    [FoldoutGroup("Focus")]
    [SerializeField] private bool regenerate = true;

    [FoldoutGroup("Focus"), ShowIf(nameof(regenerate)), MinValue(0f)]
    [SerializeField] private float regenerationPerSecond = 1.25f;

    [FoldoutGroup("Focus"), ShowIf(nameof(regenerate)), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float regenerationDelayAfterUse = 1.25f;

    [FoldoutGroup("Focus Effect"), Range(-100f, 100f)]
    [SerializeField] private float focusSaturation = -100f;

    [FoldoutGroup("Focus Effect"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float focusTransitionDuration = 0.22f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsFocusActive => isFocusActive;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float CurrentFocusSeconds { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentFocusNormalized => ResolveMaxFocusSeconds() <= 0f ? 0f : Mathf.Clamp01(CurrentFocusSeconds / ResolveMaxFocusSeconds());

    public event Action<float> FocusSpent;

    private Player rewiredPlayer;
    private VolumeProfile runtimeProfile;
    private ColorAdjustments colorAdjustments;
    private Tween saturationTween;
    private float baseSaturation;
    private float nextRegenerationAllowedTime;
    private bool inputBlocked;
    private bool isFocusActive;
    private bool focusToggleState;
    private float perkMaxFocusFlatBonus;
    private bool hasPerkRegenerationOverride;
    private bool perkRegenerationEnabled;
    private float perkRegenerationPerSecond;

    private void Reset()
    {
        if (targetVolume == null)
            targetVolume = FindFirstObjectByType<Volume>();
    }

    private void Awake()
    {
        if (targetVolume == null)
            targetVolume = FindFirstObjectByType<Volume>();

        ResolveRewiredPlayer();
        CacheVolumeOverride();
        CurrentFocusSeconds = ResolveMaxFocusSeconds();
        ApplyUi();
        ApplySaturationImmediate(baseSaturation);
        FocusRevealTarget.SetGlobalFocusVisible(false);
    }

    private void OnDisable()
    {
        saturationTween?.Kill();
        saturationTween = null;
        focusToggleState = false;
        SetFocusActive(false, force: true);
        ApplySaturationImmediate(baseSaturation);
    }

    private void OnValidate()
    {
        maxFocusSeconds = Mathf.Max(0f, maxFocusSeconds);
        regenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
        regenerationDelayAfterUse = Mathf.Max(0f, regenerationDelayAfterUse);
        focusTransitionDuration = Mathf.Max(0f, focusTransitionDuration);
        focusSaturation = Mathf.Clamp(focusSaturation, -100f, 100f);
    }

    private void Update()
    {
        if (colorAdjustments == null)
        {
            if (targetVolume == null)
                targetVolume = FindFirstObjectByType<Volume>();

            CacheVolumeOverride();
        }

        if (GameplayConsoleCheatState.FocusMode)
        {
            float currentMaxFocus = ResolveMaxFocusSeconds();
            if (CurrentFocusSeconds < currentMaxFocus)
                CurrentFocusSeconds = Mathf.Max(0f, currentMaxFocus);
        }

        bool focusRequested = ResolveFocusRequested();
        if (inputBlocked)
        {
            focusRequested = false;
            focusToggleState = false;
        }

        if (focusRequested && CurrentFocusSeconds > MinimumFocusAmount)
        {
            SetFocusActive(true);
            if (!GameplayConsoleCheatState.FocusMode)
            {
                float previousFocusSeconds = CurrentFocusSeconds;
                CurrentFocusSeconds = Mathf.Max(0f, CurrentFocusSeconds - Time.deltaTime);
                float spentFocus = Mathf.Max(0f, previousFocusSeconds - CurrentFocusSeconds);
                if (spentFocus > 0f)
                    FocusSpent?.Invoke(spentFocus);
            }

            nextRegenerationAllowedTime = Time.time + regenerationDelayAfterUse;

            if (CurrentFocusSeconds <= MinimumFocusAmount)
            {
                CurrentFocusSeconds = 0f;
                focusToggleState = false;
                SetFocusActive(false);
            }
        }
        else
        {
            if (isFocusActive)
            {
                SetFocusActive(false);
                nextRegenerationAllowedTime = Time.time + regenerationDelayAfterUse;
            }

            TryRegenerateFocus(focusRequested);
        }

        ApplyUi();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RestoreFullFocus()
    {
        CurrentFocusSeconds = ResolveMaxFocusSeconds();
        nextRegenerationAllowedTime = 0f;
        ApplyUi();
    }

    public void ApplyPerkModifiers(PlayerPerkModifierSet modifiers, bool restoreToFull = false)
    {
        perkMaxFocusFlatBonus = modifiers != null ? Mathf.Max(0f, modifiers.MaxFocusFlatBonus) : 0f;
        hasPerkRegenerationOverride = modifiers != null && modifiers.HasFocusRegenerationOverride;
        perkRegenerationEnabled = modifiers != null && modifiers.FocusRegenerationEnabled;
        perkRegenerationPerSecond = modifiers != null ? Mathf.Max(0f, modifiers.FocusRegenerationPerSecond) : 0f;

        if (!Application.isPlaying || restoreToFull)
        {
            RestoreFullFocus();
            return;
        }

        CurrentFocusSeconds = Mathf.Clamp(CurrentFocusSeconds, 0f, ResolveMaxFocusSeconds());
        ApplyUi();
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        if (!blocked)
            return;

        focusToggleState = false;
        SetFocusActive(false);
    }

    private bool ResolveFocusRequested()
    {
        if (rewiredPlayer == null && !ResolveRewiredPlayer())
            return false;

        bool focusToggleMode = ReadFocusToggleMode();
        if (focusToggleMode && rewiredPlayer.GetButtonDown(focusAction))
            focusToggleState = !focusToggleState;

        bool requested = focusToggleMode ? focusToggleState : rewiredPlayer.GetButton(focusAction);
        if (!focusToggleMode)
            focusToggleState = false;

        return requested;
    }

    private void TryRegenerateFocus(bool focusRequested)
    {
        float currentMaxFocus = ResolveMaxFocusSeconds();
        if (!ResolveFocusRegenerationEnabled() ||
            focusRequested ||
            inputBlocked ||
            CurrentFocusSeconds >= currentMaxFocus ||
            Time.time < nextRegenerationAllowedTime)
        {
            return;
        }

        CurrentFocusSeconds = Mathf.Min(currentMaxFocus, CurrentFocusSeconds + (ResolveFocusRegenerationPerSecond() * Time.deltaTime));
    }

    private void SetFocusActive(bool active, bool force = false)
    {
        if (!force && isFocusActive == active)
            return;

        isFocusActive = active;
        FocusRevealTarget.SetGlobalFocusVisible(active);
        AnimateSaturation(active ? focusSaturation : baseSaturation, immediate: force);
    }

    private void CacheVolumeOverride()
    {
        if (targetVolume == null)
            return;

        runtimeProfile = targetVolume.profile;
        if (runtimeProfile == null)
            return;

        if (!runtimeProfile.TryGet(out colorAdjustments))
            colorAdjustments = runtimeProfile.Add<ColorAdjustments>(true);

        if (colorAdjustments == null)
            return;

        baseSaturation = colorAdjustments.saturation.value;
    }

    private void AnimateSaturation(float targetValue, bool immediate = false)
    {
        if (colorAdjustments == null)
            return;

        saturationTween?.Kill();
        saturationTween = null;

        if (immediate || focusTransitionDuration <= 0f)
        {
            ApplySaturationImmediate(targetValue);
            return;
        }

        colorAdjustments.active = true;
        colorAdjustments.saturation.overrideState = true;
        saturationTween = DOTween.To(
                () => colorAdjustments.saturation.value,
                value => colorAdjustments.saturation.value = Mathf.Clamp(value, -100f, 100f),
                Mathf.Clamp(targetValue, -100f, 100f),
                focusTransitionDuration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .OnComplete(() => saturationTween = null);
    }

    private void ApplySaturationImmediate(float value)
    {
        if (colorAdjustments == null)
            return;

        colorAdjustments.active = true;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = Mathf.Clamp(value, -100f, 100f);
    }

    private void ApplyUi()
    {
        if (focusFillImage != null)
            focusFillImage.fillAmount = CurrentFocusNormalized;
    }

    private float ResolveMaxFocusSeconds()
    {
        return Mathf.Max(0f, maxFocusSeconds + perkMaxFocusFlatBonus);
    }

    private bool ResolveFocusRegenerationEnabled()
    {
        return hasPerkRegenerationOverride ? perkRegenerationEnabled : regenerate;
    }

    private float ResolveFocusRegenerationPerSecond()
    {
        return hasPerkRegenerationOverride
            ? Mathf.Max(0f, perkRegenerationPerSecond)
            : Mathf.Max(0f, regenerationPerSecond);
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
        return rewiredPlayer != null;
    }

    private static bool ReadFocusToggleMode()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.FocusToggleEnabled
            : DefaultFocusToggleMode;
    }
}
