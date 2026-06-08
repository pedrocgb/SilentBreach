using Breezeblocks.WeaponSystem;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Flashbang Status")]
public class EnemyFlashbangStatus : MonoBehaviour
{
    private const float AimlessLookAngleRange = 180f;
    private const float MinimumAimlessTurnInterval = 0.12f;
    private const float MaximumAimlessTurnInterval = 0.35f;

    [FoldoutGroup("References")]
    [SerializeField] private AIHearing aiHearing;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyVisionAI enemyVisionAI;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyCombatantAI enemyCombatantAI;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMeleeCombatantAI enemyMeleeCombatantAI;

    [FoldoutGroup("References"), ShowInInspector, ReadOnly]
    private AIPath aiPath;

    [FoldoutGroup("References"), ShowInInspector, ReadOnly]
    private Rigidbody2D movementBody;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float FlashbangTimeRemaining => Mathf.Max(0f, flashbangEndTime - Time.time);

    private float flashbangEndTime = float.NegativeInfinity;
    private float flashbangRecoveryStartTime = float.NegativeInfinity;
    private float aimlessRotationSpeed;
    private float nextAimlessLookTurnTime = float.NegativeInfinity;
    private Vector2 currentAimlessLookDirection = Vector2.up;
    private bool cachedCanMove;
    private bool cachedCanMoveValid;

    /// <summary>
    /// Ensures the target actor has a flashbang status component and returns it.
    /// </summary>
    public static EnemyFlashbangStatus EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        EnemyFlashbangStatus status = actorRoot.GetComponent<EnemyFlashbangStatus>();
        if (status == null)
            status = actorRoot.AddComponent<EnemyFlashbangStatus>();

        status.CacheReferences();
        return status;
    }

    /// <summary>
    /// Refreshes cached references when the component is reset.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches runtime references used by flashbang behavior.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Clears flashbang runtime state when the component is disabled.
    /// </summary>
    private void OnDisable()
    {
        ClearFlashbangState();
    }

    /// <summary>
    /// Updates the active flashbang effect and suppresses movement while blinded.
    /// </summary>
    private void Update()
    {
        if (flashbangEndTime <= float.NegativeInfinity)
            return;

        if (Time.time >= flashbangEndTime)
        {
            ClearFlashbangState();
            return;
        }

        float perceptionMultiplier = ResolvePerceptionMultiplier();
        aiHearing?.SetExternalSensitivityMultiplier(perceptionMultiplier);
        enemyVisionAI?.SetExternalPerceptionMultiplier(perceptionMultiplier);
        enemyCombatantAI?.SetFlashbanged(true, aimlessRotationSpeed);
        enemyMeleeCombatantAI?.SetFlashbanged(true);

        if (aiPath != null)
        {
            if (!cachedCanMoveValid)
            {
                cachedCanMove = aiPath.canMove;
                cachedCanMoveValid = true;
            }

            aiPath.canMove = false;
        }

        if (movementBody != null)
        {
            movementBody.linearVelocity = Vector2.zero;
            movementBody.angularVelocity = 0f;
        }

        if (enemyMovementController != null)
            UpdateAimlessLookAround();
    }

    /// <summary>
    /// Applies a flashbang effect for the provided duration and recovery timing.
    /// </summary>
    public void ApplyFlashbang(float duration, float recoveryThreshold, float blindedAimlessRotationSpeed)
    {
        duration = Mathf.Max(0.01f, duration);
        recoveryThreshold = Mathf.Clamp(recoveryThreshold, 0f, duration);

        enabled = true;
        CacheReferences();
        flashbangEndTime = Mathf.Max(flashbangEndTime, Time.time + duration);
        flashbangRecoveryStartTime = Mathf.Max(flashbangRecoveryStartTime, Time.time + recoveryThreshold);
        aimlessRotationSpeed = Mathf.Max(0f, blindedAimlessRotationSpeed);
        nextAimlessLookTurnTime = float.NegativeInfinity;
        currentAimlessLookDirection = enemyMovementController != null &&
                                      enemyMovementController.CurrentFacingDirection.sqrMagnitude > 0.0001f
            ? enemyMovementController.CurrentFacingDirection.normalized
            : (Vector2)transform.up;

        aiHearing?.SetExternalSensitivityMultiplier(0f);
        enemyVisionAI?.SetExternalPerceptionMultiplier(0f);
        enemyCombatantAI?.SetFlashbanged(true, aimlessRotationSpeed);
        enemyMeleeCombatantAI?.SetFlashbanged(true);
    }

    /// <summary>
    /// Caches same-object and sibling references used by the flashbang effect.
    /// </summary>
    private void CacheReferences()
    {
        if (aiHearing == null)
            aiHearing = GetComponent<AIHearing>();

        if (enemyVisionAI == null)
            enemyVisionAI = GetComponent<EnemyVisionAI>();

        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();

        if (enemyMeleeCombatantAI == null)
            enemyMeleeCombatantAI = GetComponent<EnemyMeleeCombatantAI>();

        aiPath = GetComponent<AIPath>();
        movementBody = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Resolves the current perception multiplier during flashbang recovery.
    /// </summary>
    private float ResolvePerceptionMultiplier()
    {
        if (Time.time <= flashbangRecoveryStartTime)
            return 0f;

        if (flashbangEndTime <= flashbangRecoveryStartTime)
            return 1f;

        return Mathf.InverseLerp(flashbangRecoveryStartTime, flashbangEndTime, Time.time);
    }

    /// <summary>
    /// Applies an aimless look-around override while the flashbang is active.
    /// </summary>
    private void UpdateAimlessLookAround()
    {
        if (enemyMovementController == null)
            return;

        enemyMovementController.SetExternalTurnSpeedOverride(true, aimlessRotationSpeed);

        if (Time.time >= nextAimlessLookTurnTime)
        {
            Vector2 basis = currentAimlessLookDirection.sqrMagnitude > 0.0001f
                ? currentAimlessLookDirection.normalized
                : enemyMovementController.CurrentFacingDirection.sqrMagnitude > 0.0001f
                    ? enemyMovementController.CurrentFacingDirection.normalized
                    : (Vector2)transform.up;

            float angleOffset = Random.Range(-AimlessLookAngleRange * 0.5f, AimlessLookAngleRange * 0.5f);
            currentAimlessLookDirection = Rotate(basis, angleOffset);
            nextAimlessLookTurnTime = Time.time + ResolveAimlessLookTurnInterval();
        }

        enemyMovementController.SetExternalFacingDirection(currentAimlessLookDirection);
    }

    /// <summary>
    /// Resolves the interval between aimless look changes while flashbanged.
    /// </summary>
    private float ResolveAimlessLookTurnInterval()
    {
        if (aimlessRotationSpeed <= 0f)
            return 0.2f;

        float intervalFromSpeed = 90f / aimlessRotationSpeed;
        return Mathf.Clamp(intervalFromSpeed, MinimumAimlessTurnInterval, MaximumAimlessTurnInterval);
    }

    /// <summary>
    /// Rotates a direction vector by the supplied angle in degrees.
    /// </summary>
    private static Vector2 Rotate(Vector2 direction, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            (direction.x * cos) - (direction.y * sin),
            (direction.x * sin) + (direction.y * cos)).normalized;
    }

    /// <summary>
    /// Clears all active flashbang runtime state and restores cached movement flags.
    /// </summary>
    private void ClearFlashbangState()
    {
        bool hadActiveState =
            flashbangEndTime > float.NegativeInfinity ||
            flashbangRecoveryStartTime > float.NegativeInfinity ||
            cachedCanMoveValid ||
            aimlessRotationSpeed > 0f ||
            nextAimlessLookTurnTime > float.NegativeInfinity;
        if (!hadActiveState)
            return;

        flashbangEndTime = float.NegativeInfinity;
        flashbangRecoveryStartTime = float.NegativeInfinity;
        aimlessRotationSpeed = 0f;
        nextAimlessLookTurnTime = float.NegativeInfinity;
        currentAimlessLookDirection = Vector2.up;

        aiHearing?.SetExternalSensitivityMultiplier(1f);
        enemyVisionAI?.SetExternalPerceptionMultiplier(1f);
        enemyCombatantAI?.SetFlashbanged(false, 0f);
        enemyMeleeCombatantAI?.SetFlashbanged(false);

        if (enemyMovementController != null)
        {
            enemyMovementController.SetExternalTurnSpeedOverride(false, 0f);
            enemyMovementController.ClearExternalFacingOverride();
        }

        if (aiPath != null && cachedCanMoveValid)
            aiPath.canMove = cachedCanMove;

        cachedCanMove = false;
        cachedCanMoveValid = false;
    }
}
