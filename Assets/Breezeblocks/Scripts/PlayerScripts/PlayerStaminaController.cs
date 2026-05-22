using Breezeblocks.WeaponSystem;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Player/Player Stamina Controller")]
public class PlayerStaminaController : MonoBehaviour
{
    private const float MinimumThreshold = 0.0001f;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("References")]
    [SerializeField] private ActorStaggerController actorStaggerController;

    [FoldoutGroup("UI")]
    [SerializeField] private Image staminaFillImage;

    [FoldoutGroup("UI")]
    [SerializeField] private TMP_Text staminaText;

    [FoldoutGroup("UI")]
    [SerializeField] private string staminaTextFormat = "{0:0}/{1:0}";

    [FoldoutGroup("UI")]
    [SerializeField] private RectTransform staminaFeedbackRoot;

    [FoldoutGroup("UI"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float insufficientStaminaShakeDuration = 0.2f;

    [FoldoutGroup("UI"), MinValue(0f)]
    [SerializeField] private float insufficientStaminaShakeStrength = 18f;

    [FoldoutGroup("UI"), MinValue(1)]
    [SerializeField] private int insufficientStaminaShakeVibrato = 18;

    private float maxStamina = 100f;

    private float sprintDrainPerSecond = 20f;

    private float regenerationPerSecond = 32f;

    private float regenerationDelayAfterSpend = 1f;

    private float staggerStaminaLossPercent = 12f;

    private float movementThreshold = 0.05f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentStamina { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, ProgressBar(0f, 1f)]
    public float CurrentStaminaNormalized => maxStamina <= 0f ? 0f : Mathf.Clamp01(CurrentStamina / maxStamina);

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsRegenerating { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSprintBlocked => CurrentStamina <= MinimumThreshold;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float RegenerationDelayRemaining => Mathf.Max(0f, nextRegenerationAllowedTime - Time.time);

    private float nextRegenerationAllowedTime;
    private Tween insufficientStaminaShakeTween;
    private Vector2 staminaFeedbackDefaultAnchoredPosition;
    private bool sprintInsufficientFeedbackActive;

    private void Reset()
    {
        playerMotor = GetComponent<PlayerTopDownMotor2D>();
        playerWeaponController = GetComponent<PlayerWeaponController>();
        playerUtilityController = GetComponent<PlayerUtilityController>();
        actorStaggerController = GetComponent<ActorStaggerController>();
        CacheFeedbackRoot();
    }

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

    private void OnEnable()
    {
        if (actorStaggerController != null)
            actorStaggerController.StaggerApplied += HandleStaggerApplied;

        RefreshUi();
    }

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

    private void Update()
    {
        if (GameplayConsoleCheatState.AthleteMode && CurrentStamina < maxStamina)
            CurrentStamina = maxStamina;

        bool consumedStaminaThisFrame = DrainSprintStamina();
        UpdateSprintInsufficientFeedback();

        bool canRegenerate = !consumedStaminaThisFrame && CanRegenerate();
        IsRegenerating = canRegenerate;
        if (canRegenerate)
            CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + (regenerationPerSecond * Time.deltaTime));

        if (playerMotor != null)
            playerMotor.SetSprintBlocked(IsSprintBlocked);

        RefreshUi();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RestoreStamina()
    {
        CurrentStamina = maxStamina;
        nextRegenerationAllowedTime = 0f;
        IsRegenerating = false;
        sprintInsufficientFeedbackActive = false;
        RefreshUi();
    }

    public void SpendStamina(float amount)
    {
        if (GameplayConsoleCheatState.AthleteMode)
        {
            CurrentStamina = maxStamina;
            nextRegenerationAllowedTime = 0f;
            IsRegenerating = false;
            sprintInsufficientFeedbackActive = false;
            RefreshUi();
            return;
        }

        if (amount <= 0f || maxStamina <= 0f)
            return;

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        nextRegenerationAllowedTime = Time.time + regenerationDelayAfterSpend;
        IsRegenerating = false;
        if (CurrentStamina > MinimumThreshold)
            sprintInsufficientFeedbackActive = false;
        RefreshUi();
    }

    public bool HasStamina(float amount)
    {
        if (amount <= 0f)
            return true;

        return CurrentStamina + MinimumThreshold >= amount;
    }

    public bool TrySpendStamina(float amount, bool playFeedbackOnFailure = true)
    {
        if (GameplayConsoleCheatState.AthleteMode)
        {
            CurrentStamina = maxStamina;
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

        if (!Application.isPlaying || restoreToFull)
        {
            RestoreStamina();
            return;
        }

        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, maxStamina);
        RefreshUi();
    }

    private bool DrainSprintStamina()
    {
        if (playerMotor == null || !playerMotor.IsSprinting || !IsMoving())
            return false;

        float drain = sprintDrainPerSecond * Time.deltaTime;
        if (drain <= 0f)
            return false;

        SpendStamina(drain);
        return true;
    }

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

    private bool CanRegenerate()
    {
        if (CurrentStamina >= maxStamina || maxStamina <= 0f)
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

    private bool IsMoving()
    {
        if (playerMotor == null)
            return false;

        return playerMotor.HasMovementInput || playerMotor.CurrentPlanarSpeed > movementThreshold;
    }

    private void HandleStaggerApplied(float duration)
    {
        if (staggerStaminaLossPercent <= 0f || maxStamina <= 0f)
            return;

        SpendStamina(maxStamina * (staggerStaminaLossPercent / 100f));
    }

    private void RefreshUi()
    {
        if (staminaFillImage != null)
            staminaFillImage.fillAmount = CurrentStaminaNormalized;

        if (staminaText != null)
            staminaText.text = string.Format(staminaTextFormat, CurrentStamina, maxStamina);
    }

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

    private void ResetFeedbackRootPosition()
    {
        if (staminaFeedbackRoot != null)
            staminaFeedbackRoot.anchoredPosition = staminaFeedbackDefaultAnchoredPosition;
    }
}
