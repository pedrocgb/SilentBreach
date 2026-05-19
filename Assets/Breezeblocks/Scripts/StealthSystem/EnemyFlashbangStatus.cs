using Breezeblocks.WeaponSystem;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Flashbang Status")]
public class EnemyFlashbangStatus : MonoBehaviour
{
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

    [FoldoutGroup("References")]
    [SerializeField] private AIPath aiPath;

    [FoldoutGroup("References")]
    [SerializeField] private Rigidbody2D movementBody;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float FlashbangTimeRemaining => Mathf.Max(0f, flashbangEndTime - Time.time);

    private float flashbangEndTime = float.NegativeInfinity;
    private float flashbangRecoveryStartTime = float.NegativeInfinity;
    private float aimlessRotationSpeed;
    private bool cachedCanMove;
    private bool cachedCanMoveValid;

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

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        ClearFlashbangState();
    }

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
        {
            Vector2 facingDirection = new(
                Mathf.Cos(Time.time * aimlessRotationSpeed * Mathf.Deg2Rad),
                Mathf.Sin(Time.time * aimlessRotationSpeed * Mathf.Deg2Rad));
            enemyMovementController.SetFacingPoint((Vector2)transform.position + facingDirection);
        }
    }

    public void ApplyFlashbang(float duration, float recoveryThreshold, float blindedAimlessRotationSpeed)
    {
        duration = Mathf.Max(0.01f, duration);
        recoveryThreshold = Mathf.Clamp(recoveryThreshold, 0f, duration);

        enabled = true;
        CacheReferences();
        flashbangEndTime = Mathf.Max(flashbangEndTime, Time.time + duration);
        flashbangRecoveryStartTime = Mathf.Max(flashbangRecoveryStartTime, Time.time + recoveryThreshold);
        aimlessRotationSpeed = Mathf.Max(0f, blindedAimlessRotationSpeed);

        aiHearing?.SetExternalSensitivityMultiplier(0f);
        enemyVisionAI?.SetExternalPerceptionMultiplier(0f);
        enemyCombatantAI?.SetFlashbanged(true, aimlessRotationSpeed);
        enemyMeleeCombatantAI?.SetFlashbanged(true);
    }

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

        if (aiPath == null)
            aiPath = GetComponent<AIPath>();

        if (movementBody == null)
            movementBody = GetComponent<Rigidbody2D>();
    }

    private float ResolvePerceptionMultiplier()
    {
        if (Time.time <= flashbangRecoveryStartTime)
            return 0f;

        if (flashbangEndTime <= flashbangRecoveryStartTime)
            return 1f;

        return Mathf.InverseLerp(flashbangRecoveryStartTime, flashbangEndTime, Time.time);
    }

    private void ClearFlashbangState()
    {
        bool hadActiveState =
            flashbangEndTime > float.NegativeInfinity ||
            flashbangRecoveryStartTime > float.NegativeInfinity ||
            cachedCanMoveValid ||
            aimlessRotationSpeed > 0f;
        if (!hadActiveState)
            return;

        if (aiPath != null && cachedCanMoveValid)
            aiPath.canMove = cachedCanMove;

        cachedCanMoveValid = false;
        flashbangEndTime = float.NegativeInfinity;
        flashbangRecoveryStartTime = float.NegativeInfinity;
        aimlessRotationSpeed = 0f;

        aiHearing?.SetExternalSensitivityMultiplier(1f);
        enemyVisionAI?.SetExternalPerceptionMultiplier(1f);
        enemyCombatantAI?.SetFlashbanged(false, 0f);
        enemyMeleeCombatantAI?.SetFlashbanged(false);
    }
}
