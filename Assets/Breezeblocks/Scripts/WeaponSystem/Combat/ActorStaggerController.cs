using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Combat/Actor Stagger Controller")]
public class ActorStaggerController : MonoBehaviour
{
    private bool enableStagger = true;

    private float staggeredMoveSpeed = 1.2f;

    private float turnSpeedReductionPercent = 40f;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsStaggered => remainingStaggerTime > 0f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float RemainingStaggerTime => remainingStaggerTime;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float PeakStaggerTime { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float TurnSpeedMultiplier => IsStaggered
        ? 1f - Mathf.Clamp01(turnSpeedReductionPercent / 100f)
        : 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float StaggeredMoveSpeed => staggeredMoveSpeed;

    public event Action<float> StaggerApplied;
    public event Action<bool> StaggerStateChanged;

    private float remainingStaggerTime;

    private void Reset()
    {
        playerMotor = GetComponent<PlayerTopDownMotor2D>();
        enemyMovementController = GetComponent<EnemyMovementController>();
    }

    private void Awake()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        ApplyMovementOverrides(false);
    }

    private void OnDisable()
    {
        remainingStaggerTime = 0f;
        PeakStaggerTime = 0f;
        ApplyMovementOverrides(false);
    }

    private void Update()
    {
        if (!IsStaggered)
            return;

        remainingStaggerTime = Mathf.Max(0f, remainingStaggerTime - Time.deltaTime);
        if (remainingStaggerTime > 0f)
            return;

        PeakStaggerTime = 0f;
        ApplyMovementOverrides(false);
        StaggerStateChanged?.Invoke(false);
    }

    public void ApplyStagger(float duration)
    {
        if (!enableStagger || duration <= 0f)
            return;

        bool wasStaggered = IsStaggered;
        remainingStaggerTime += duration;
        PeakStaggerTime = Mathf.Max(PeakStaggerTime, remainingStaggerTime);

        ApplyMovementOverrides(true);
        StaggerApplied?.Invoke(duration);

        if (!wasStaggered)
            StaggerStateChanged?.Invoke(true);
    }

    public void ClearStagger()
    {
        if (!IsStaggered)
            return;

        remainingStaggerTime = 0f;
        PeakStaggerTime = 0f;
        ApplyMovementOverrides(false);
        StaggerStateChanged?.Invoke(false);
    }

    public void ApplySettings(ActorStaggerSettings settings)
    {
        if (settings == null)
            return;

        enableStagger = settings.EnableStagger;
        staggeredMoveSpeed = Mathf.Max(0f, settings.StaggeredMoveSpeed);
        turnSpeedReductionPercent = Mathf.Clamp(settings.TurnSpeedReductionPercent, 0f, 100f);
        ApplyMovementOverrides(IsStaggered);
    }

    private void ApplyMovementOverrides(bool staggerActive)
    {
        if (playerMotor != null)
            playerMotor.SetExternalSpeedOverride(staggerActive, staggeredMoveSpeed, lockSpeedSelection: staggerActive);

        if (enemyMovementController != null)
            enemyMovementController.SetStaggerOverride(staggerActive, staggeredMoveSpeed, TurnSpeedMultiplier);
    }
}
}
