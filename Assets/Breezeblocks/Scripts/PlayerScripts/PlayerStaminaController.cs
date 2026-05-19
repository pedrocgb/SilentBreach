using Breezeblocks.WeaponSystem;
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

    private void Reset()
    {
        playerMotor = GetComponent<PlayerTopDownMotor2D>();
        playerWeaponController = GetComponent<PlayerWeaponController>();
        playerUtilityController = GetComponent<PlayerUtilityController>();
        actorStaggerController = GetComponent<ActorStaggerController>();
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
    }

    private void OnValidate()
    {
        maxStamina = Mathf.Max(0f, maxStamina);
        sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond);
        regenerationPerSecond = Mathf.Max(0f, regenerationPerSecond);
        regenerationDelayAfterSpend = Mathf.Max(0f, regenerationDelayAfterSpend);
        staggerStaminaLossPercent = Mathf.Clamp(staggerStaminaLossPercent, 0f, 100f);
        movementThreshold = Mathf.Max(0f, movementThreshold);
    }

    private void Update()
    {
        bool consumedStaminaThisFrame = DrainSprintStamina();

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
        RefreshUi();
    }

    public void SpendStamina(float amount)
    {
        if (amount <= 0f || maxStamina <= 0f)
            return;

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        nextRegenerationAllowedTime = Time.time + regenerationDelayAfterSpend;
        IsRegenerating = false;
        RefreshUi();
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
}
