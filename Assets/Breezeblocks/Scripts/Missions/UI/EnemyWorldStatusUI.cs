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

    /// <summary>
    /// Caches parent references and refreshes the initial runtime widget state.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        EnsureVisibilityFillConfiguration();
        Refresh();
    }

    /// <summary>
    /// Resolves scene references and applies the initial world-status UI state.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        EnsureVisibilityFillConfiguration();
        Refresh();
    }

    /// <summary>
    /// Subscribes to incapacitation changes and refreshes the world-status UI when enabled.
    /// </summary>
    private void OnEnable()
    {
        CacheReferences();
        EnsureVisibilityFillConfiguration();
        if (incapacitationController != null)
            incapacitationController.IncapacitationStateChanged += HandleIncapacitationChanged;

        Refresh();
    }

    /// <summary>
    /// Unsubscribes from incapacitation change notifications when disabled.
    /// </summary>
    private void OnDisable()
    {
        if (incapacitationController != null)
            incapacitationController.IncapacitationStateChanged -= HandleIncapacitationChanged;
    }

    /// <summary>
    /// Refreshes the UI continuously so the detection meter tracks the live vision value.
    /// </summary>
    private void Update()
    {
        Refresh();
    }

    /// <summary>
    /// Refreshes the world-status UI when the owning actor enters or leaves incapacitation.
    /// </summary>
    private void HandleIncapacitationChanged(bool isIncapacitated)
    {
        Refresh();
    }

    /// <summary>
    /// Resolves parent-owned components that provide the enemy state displayed by this UI.
    /// </summary>
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

    /// <summary>
    /// Configures the visibility meter image so `fillAmount` always drives a vertical bar at runtime.
    /// </summary>
    private void EnsureVisibilityFillConfiguration()
    {
        if (visibilityFillImage == null)
            return;

        visibilityFillImage.type = Image.Type.Filled;
        visibilityFillImage.fillMethod = Image.FillMethod.Vertical;
    }

    /// <summary>
    /// Refreshes world-status widgets without disabling the owning enemy object when dead.
    /// </summary>
    private void Refresh()
    {
        bool isDead = actorHealth != null && actorHealth.IsDead;
        if (isDead)
        {
            SetRootActive(visibilityRoot, false);
            SetRootActive(alertRoot, false);
            SetRootActive(suspiciousRoot, false);
            SetRootActive(incapacitatedRoot, false);

            return;
        }

        bool isIncapacitated = incapacitationController != null && incapacitationController.IsIncapacitated;
        EnemyState currentState = enemyMovementController != null ? enemyMovementController.CurrentState : EnemyState.Disabled;
        bool isAlert = !isIncapacitated &&
                       (currentState == EnemyState.Detected ||
                        currentState == EnemyState.Alert ||
                        currentState == EnemyState.Fleeing ||
                        (enemyVisionAI != null && enemyVisionAI.CurrentDetectionValue >= 0.999f));
        float detectionValue = enemyVisionAI != null ? Mathf.Clamp01(enemyVisionAI.CurrentDetectionValue) : 0f;
        bool showVisibility = !isIncapacitated && !isAlert && detectionValue > 0f;
        bool isSuspicious = !isIncapacitated &&
                            !isAlert &&
                            !showVisibility &&
                            enemyMovementController != null &&
                            (currentState == EnemyState.Suspicious ||
                             currentState == EnemyState.Searching);

        if (visibilityFillImage != null)
            visibilityFillImage.fillAmount = detectionValue;

        SetRootActive(visibilityRoot, showVisibility);
        SetRootActive(alertRoot, isAlert);
        SetRootActive(suspiciousRoot, isSuspicious);
        SetRootActive(incapacitatedRoot, isIncapacitated);
    }

    /// <summary>
    /// Applies a root active state only when it actually changes to avoid redundant SetActive calls.
    /// </summary>
    private static void SetRootActive(GameObject root, bool active)
    {
        if (root == null || root.activeSelf == active)
            return;

        root.SetActive(active);
    }
}

}
