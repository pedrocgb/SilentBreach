using System;
using Breezeblocks.HideoutSystem;
using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Stamina Controller")]
[RequireComponent(typeof(PlayerTopDownMotor2D))]
public class PlayerStaminaController : MonoBehaviour
{
    private const float MinimumThreshold = 0.0001f;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorStaggerController actorStaggerController;

    [FoldoutGroup("UI")]
    [SerializeField] private Image staminaFillImage;

    [FoldoutGroup("UI")]
    [SerializeField] private TMP_Text staminaText;

    [FoldoutGroup("UI")]
    [SerializeField] private RectTransform staminaFeedbackRoot;

    private float maxStamina = 100f;

    private float sprintDrainPerSecond = 20f;

    private float regenerationPerSecond = 32f;

    private float regenerationDelayAfterSpend = 1f;

    private float staggerStaminaLossPercent = 12f;

    private float movementThreshold = 0.05f;
    private string staminaTextFormat = "{0:0}/{1:0}";
    private float insufficientStaminaShakeDuration = 0.2f;
    private float insufficientStaminaShakeStrength = 18f;
    private int insufficientStaminaShakeVibrato = 18;
    private float perkMaxStaminaFlatBonus;
    private float perkSprintDrainMultiplier = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentStamina { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentStaminaNormalized => ResolveMaxStamina() <= 0f ? 0f : Mathf.Clamp01(CurrentStamina / ResolveMaxStamina());

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsRegenerating { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSprintBlocked => CurrentStamina <= MinimumThreshold;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float RegenerationDelayRemaining => Mathf.Max(0f, nextRegenerationAllowedTime - Time.time);

    public event Action<float> StaminaSpent;

    private float nextRegenerationAllowedTime;
    private Tween insufficientStaminaShakeTween;
    private Vector2 staminaFeedbackDefaultAnchoredPosition;
    private bool sprintInsufficientFeedbackActive;

    // Executes the Reset routine.
    private void Reset()
    {
        playerMotor = GetComponent<PlayerTopDownMotor2D>();
        playerWeaponController = GetComponent<PlayerWeaponController>();
        playerUtilityController = GetComponent<PlayerUtilityController>();
        actorStaggerController = GetComponent<ActorStaggerController>();
        CacheFeedbackRoot();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (playerWeaponController == null)
            playerWeaponController = GetComponent<PlayerWeaponController>();

        if (playerUtilityController == null)
            playerUtilityController = GetComponent<PlayerUtilityController>();

        if (actorStaggerController == null)
            actorStaggerController = GetComponent<ActorStaggerController>();

        CacheFeedbackRoot();
        RestoreStamina();
    }

    // Executes the OnEnable routine.
    private void OnEnable()
    {
        if (actorStaggerController != null)
            actorStaggerController.StaggerApplied += HandleStaggerApplied;

        RefreshUi();
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        if (playerMotor != null)
            playerMotor.SetSprintBlocked(false);

        if (actorStaggerController != null)
            actorStaggerController.StaggerApplied -= HandleStaggerApplied;

        insufficientStaminaShakeTween?.Kill();
        insufficientStaminaShakeTween = null;
        sprintInsufficientFeedbackActive = false;
        ResetFeedbackRootPosition();
    }

    // Executes the OnValidate routine.
    private void OnValidate()
    {
        maxStamina = Mathf.Max(0f, maxStamina);
        sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond);
        regenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
        regenerationDelayAfterSpend = Mathf.Max(0f, regenerationDelayAfterSpend);
        staggerStaminaLossPercent = Mathf.Clamp(staggerStaminaLossPercent, 0f, 100f);
        movementThreshold = Mathf.Max(0f, movementThreshold);
        insufficientStaminaShakeDuration = Mathf.Max(0f, insufficientStaminaShakeDuration);
        insufficientStaminaShakeStrength = Mathf.Max(0f, insufficientStaminaShakeStrength);
        insufficientStaminaShakeVibrato = Mathf.Max(1, insufficientStaminaShakeVibrato);
        CacheFeedbackRoot();
    }

    // Executes the Update routine.
    private void Update()
    {
        float currentMaxStamina = ResolveMaxStamina();
        if (GameplayConsoleCheatState.AthleteMode && CurrentStamina < currentMaxStamina)
            CurrentStamina = currentMaxStamina;

        bool consumedStaminaThisFrame = DrainSprintStamina();
        UpdateSprintInsufficientFeedback();

        bool canRegenerate = !consumedStaminaThisFrame && CanRegenerate();
        IsRegenerating = canRegenerate;
        if (canRegenerate)
            CurrentStamina = Mathf.Min(currentMaxStamina, CurrentStamina + (regenerationPerSecond * Time.deltaTime));

        if (playerMotor != null)
            playerMotor.SetSprintBlocked(IsSprintBlocked);

        RefreshUi();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    // Executes the RestoreStamina routine.
    public void RestoreStamina()
    {
        CurrentStamina = ResolveMaxStamina();
        nextRegenerationAllowedTime = 0f;
        IsRegenerating = false;
        sprintInsufficientFeedbackActive = false;
        RefreshUi();
    }

    // Executes the SpendStamina routine.
    public void SpendStamina(float amount)
    {
        if (GameplayConsoleCheatState.AthleteMode)
        {
            CurrentStamina = ResolveMaxStamina();
            nextRegenerationAllowedTime = 0f;
            IsRegenerating = false;
            sprintInsufficientFeedbackActive = false;
            RefreshUi();
            return;
        }

        if (amount <= 0f || ResolveMaxStamina() <= 0f)
            return;

        float clampedAmount = Mathf.Max(0f, amount);
        float actualSpent = Mathf.Min(CurrentStamina, clampedAmount);
        CurrentStamina = Mathf.Max(0f, CurrentStamina - clampedAmount);
        nextRegenerationAllowedTime = Time.time + regenerationDelayAfterSpend;
        IsRegenerating = false;
        if (CurrentStamina > MinimumThreshold)
            sprintInsufficientFeedbackActive = false;
        if (actualSpent > 0f)
            StaminaSpent?.Invoke(actualSpent);
        RefreshUi();
    }

    // Executes the HasStamina routine.
    public bool HasStamina(float amount)
    {
        if (amount <= 0f)
            return true;

        return CurrentStamina + MinimumThreshold >= amount;
    }

    // Executes the TrySpendStamina routine.
    public bool TrySpendStamina(float amount, bool playFeedbackOnFailure = true)
    {
        if (GameplayConsoleCheatState.AthleteMode)
        {
            CurrentStamina = ResolveMaxStamina();
            nextRegenerationAllowedTime = 0f;
            IsRegenerating = false;
            sprintInsufficientFeedbackActive = false;
            RefreshUi();
            return true;
        }

        if (!HasStamina(amount))
        {
            if (playFeedbackOnFailure)
                PlayInsufficientStaminaFeedback();

            return false;
        }

        SpendStamina(amount);
        return true;
    }

    // Executes the PlayInsufficientStaminaFeedback routine.
    public void PlayInsufficientStaminaFeedback()
    {
        if (staminaFeedbackRoot == null || insufficientStaminaShakeDuration <= 0f || insufficientStaminaShakeStrength <= 0f)
            return;

        insufficientStaminaShakeTween?.Kill();
        ResetFeedbackRootPosition();

        insufficientStaminaShakeTween = staminaFeedbackRoot.DOShakeAnchorPos(
                insufficientStaminaShakeDuration,
                new Vector2(insufficientStaminaShakeStrength, 0f),
                insufficientStaminaShakeVibrato,
                90f,
                snapping: false,
                fadeOut: true)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                insufficientStaminaShakeTween = null;
                ResetFeedbackRootPosition();
            });
    }

    // Executes the ApplySettings routine.
    public void ApplySettings(PlayerStaminaSettings settings, bool restoreToFull = false)
    {
        if (settings == null)
            return;

        maxStamina = Mathf.Max(0f, settings.MaxStamina);
        sprintDrainPerSecond = Mathf.Max(0f, settings.SprintDrainPerSecond);
        regenerationPerSecond = Mathf.Max(0f, settings.RegenerationPerSecond);
        regenerationDelayAfterSpend = Mathf.Max(0f, settings.RegenerationDelayAfterSpend);
        staggerStaminaLossPercent = Mathf.Clamp(settings.StaggerStaminaLossPercent, 0f, 100f);
        movementThreshold = Mathf.Max(0f, settings.MovementThreshold);
        staminaTextFormat = string.IsNullOrWhiteSpace(settings.StaminaTextFormat) ? "{0:0}/{1:0}" : settings.StaminaTextFormat;
        insufficientStaminaShakeDuration = Mathf.Max(0f, settings.InsufficientStaminaShakeDuration);
        insufficientStaminaShakeStrength = Mathf.Max(0f, settings.InsufficientStaminaShakeStrength);
        insufficientStaminaShakeVibrato = Mathf.Max(1, settings.InsufficientStaminaShakeVibrato);

        if (!Application.isPlaying || restoreToFull)
        {
            RestoreStamina();
            return;
        }

        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, ResolveMaxStamina());
        RefreshUi();
    }

    // Executes the ApplyPerkModifiers routine.
    public void ApplyPerkModifiers(PlayerPerkModifierSet modifiers, bool restoreToFull = false)
    {
        perkMaxStaminaFlatBonus = modifiers != null ? Mathf.Max(0f, modifiers.MaxStaminaFlatBonus) : 0f;
        perkSprintDrainMultiplier = modifiers != null ? Mathf.Max(0f, modifiers.SprintStaminaDrainMultiplier) : 1f;

        if (!Application.isPlaying || restoreToFull)
        {
            RestoreStamina();
            return;
        }

        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, ResolveMaxStamina());
        RefreshUi();
    }

    // Executes the DrainSprintStamina routine.
    private bool DrainSprintStamina()
    {
        if (playerMotor == null || !playerMotor.IsSprinting || !IsMoving())
            return false;

        float drain = ResolveSprintDrainPerSecond() * Time.deltaTime;
        if (drain <= 0f)
            return false;

        SpendStamina(drain);
        return true;
    }

    // Executes the UpdateSprintInsufficientFeedback routine.
    private void UpdateSprintInsufficientFeedback()
    {
        bool shouldPlayFeedback = playerMotor != null &&
                                  playerMotor.SprintRequested &&
                                  !playerMotor.IsInputBlocked &&
                                  IsMoving() &&
                                  IsSprintBlocked;

        if (shouldPlayFeedback && !sprintInsufficientFeedbackActive)
            PlayInsufficientStaminaFeedback();

        sprintInsufficientFeedbackActive = shouldPlayFeedback;
    }

    // Executes the CanRegenerate routine.
    private bool CanRegenerate()
    {
        float currentMaxStamina = ResolveMaxStamina();
        if (CurrentStamina >= currentMaxStamina || currentMaxStamina <= 0f)
            return false;

        if (Time.time < nextRegenerationAllowedTime)
            return false;

        if (actorStaggerController != null && actorStaggerController.IsStaggered)
            return false;

        if (playerMotor != null)
        {
            if (playerMotor.IsSprinting)
                return false;
        }

        if (playerWeaponController != null && playerWeaponController.IsAiming)
            return false;

        if (playerUtilityController != null && playerUtilityController.IsAiming)
            return false;

        return true;
    }

    // Executes the IsMoving routine.
    private bool IsMoving()
    {
        if (playerMotor == null)
            return false;

        return playerMotor.HasMovementInput || playerMotor.CurrentPlanarSpeed > movementThreshold;
    }

    // Executes the HandleStaggerApplied routine.
    private void HandleStaggerApplied(float duration)
    {
        float currentMaxStamina = ResolveMaxStamina();
        if (staggerStaminaLossPercent <= 0f || currentMaxStamina <= 0f)
            return;

        SpendStamina(currentMaxStamina * (staggerStaminaLossPercent / 100f));
    }

    // Executes the RefreshUi routine.
    private void RefreshUi()
    {
        float currentMaxStamina = ResolveMaxStamina();
        if (staminaFillImage != null)
            staminaFillImage.fillAmount = CurrentStaminaNormalized;

        if (staminaText != null)
            staminaText.text = string.Format(staminaTextFormat, CurrentStamina, currentMaxStamina);
    }

    // Executes the ResolveMaxStamina routine.
    private float ResolveMaxStamina()
    {
        return Mathf.Max(0f, maxStamina + perkMaxStaminaFlatBonus);
    }

    // Executes the ResolveSprintDrainPerSecond routine.
    private float ResolveSprintDrainPerSecond()
    {
        return Mathf.Max(0f, sprintDrainPerSecond * perkSprintDrainMultiplier);
    }

    // Executes the CacheFeedbackRoot routine.
    private void CacheFeedbackRoot()
    {
        if (staminaFeedbackRoot == null)
        {
            if (staminaFillImage != null)
                staminaFeedbackRoot = staminaFillImage.rectTransform;
            else if (staminaText != null)
                staminaFeedbackRoot = staminaText.rectTransform;
        }

        if (staminaFeedbackRoot != null)
            staminaFeedbackDefaultAnchoredPosition = staminaFeedbackRoot.anchoredPosition;
    }

    // Executes the ResetFeedbackRootPosition routine.
    private void ResetFeedbackRootPosition()
    {
        if (staminaFeedbackRoot != null)
            staminaFeedbackRoot.anchoredPosition = staminaFeedbackDefaultAnchoredPosition;
    }
}
