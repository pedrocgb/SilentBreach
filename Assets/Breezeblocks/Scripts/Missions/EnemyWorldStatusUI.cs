using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Enemy World Status UI")]
public class EnemyWorldStatusUI : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private EnemyVisionAI enemyVisionAI;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private ActorIncapacitationController incapacitationController;

    [FoldoutGroup("References")]
    [SerializeField] private ActorHealth actorHealth;

    [FoldoutGroup("Visibility UI")]
    [SerializeField] private GameObject visibilityRoot;

    [FoldoutGroup("Visibility UI")]
    [SerializeField] private Image visibilityFillImage;

    [FoldoutGroup("Alert UI")]
    [SerializeField] private GameObject alertRoot;

    [FoldoutGroup("Suspicious UI")]
    [SerializeField] private GameObject suspiciousRoot;

    [FoldoutGroup("Incapacitated UI")]
    [SerializeField] private GameObject incapacitatedRoot;

    private void Reset()
    {
        CacheReferences();
        Refresh();
    }

    private void Awake()
    {
        CacheReferences();
        Refresh();
    }

    private void OnEnable()
    {
        CacheReferences();
        if (incapacitationController != null)
            incapacitationController.IncapacitationStateChanged += HandleIncapacitationChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (incapacitationController != null)
            incapacitationController.IncapacitationStateChanged -= HandleIncapacitationChanged;
    }

    private void Update()
    {
        Refresh();
    }

    private void HandleIncapacitationChanged(bool isIncapacitated)
    {
        Refresh();
    }

    private void CacheReferences()
    {
        if (enemyVisionAI == null)
            enemyVisionAI = GetComponentInParent<EnemyVisionAI>();

        if (enemyMovementController == null)
            enemyMovementController = GetComponentInParent<EnemyMovementController>();

        if (incapacitationController == null)
            incapacitationController = GetComponentInParent<ActorIncapacitationController>();

        if (actorHealth == null)
            actorHealth = GetComponentInParent<ActorHealth>();
    }

    private void Refresh()
    {
        bool isDead = actorHealth != null && actorHealth.IsDead;
        if (isDead)
        {
            gameObject.SetActive(false);
            return;
        }

        bool isIncapacitated = incapacitationController != null && incapacitationController.IsIncapacitated;
        EnemyState currentState = enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Disabled;
        bool isAlert = !isIncapacitated &&
                       (currentState == EnemyState.Detected ||
                        currentState == EnemyState.Alert ||
                        currentState == EnemyState.Fleeing ||
                        (enemyVisionAI != null && enemyVisionAI.CurrentDetectionValue >= 0.999f));
        bool isSuspicious = !isIncapacitated &&
                            !isAlert &&
                            enemyMovementController != null &&
                            (currentState == EnemyState.Suspicious ||
                             currentState == EnemyState.Searching);

        float detectionValue = enemyVisionAI != null ? Mathf.Clamp01(enemyVisionAI.CurrentDetectionValue) : 0f;
        bool showVisibility = !isIncapacitated && !isAlert && !isSuspicious && detectionValue > 0f;

        if (visibilityRoot != null)
            visibilityRoot.SetActive(showVisibility);

        if (visibilityFillImage != null)
            visibilityFillImage.fillAmount = detectionValue;

        if (alertRoot != null)
            alertRoot.SetActive(isAlert);

        if (suspiciousRoot != null)
            suspiciousRoot.SetActive(isSuspicious);

        if (incapacitatedRoot != null)
            incapacitatedRoot.SetActive(isIncapacitated);
    }
}

}
