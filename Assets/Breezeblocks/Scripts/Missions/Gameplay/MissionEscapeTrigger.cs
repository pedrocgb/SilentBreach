using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Breezeblocks/Missions/Mission Escape Trigger")]
public class MissionEscapeTrigger : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private GameplayMissionController gameplayMissionController;

    [FoldoutGroup("References")]
    [SerializeField] private Collider2D triggerCollider;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsEscapeEnabled => triggerCollider != null && triggerCollider.enabled;

    private void Reset()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    public void Bind(GameplayMissionController controller)
    {
        gameplayMissionController = controller;
    }

    public void SetEscapeEnabled(bool enabled)
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
            triggerCollider.enabled = enabled;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (gameplayMissionController == null || triggerCollider == null || !triggerCollider.enabled || other == null)
            return;

        gameplayMissionController.TryHandleEscapeTrigger(other.transform.root.gameObject);
    }
}

}
