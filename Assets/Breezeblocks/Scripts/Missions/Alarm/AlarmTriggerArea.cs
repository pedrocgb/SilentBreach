using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Breezeblocks/Missions/Alarm/Alarm Trigger Area")]
public sealed class AlarmTriggerArea : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private AlarmController alarmController;

    private Collider2D triggerCollider;

    /// <summary>
    /// Caches and configures the same-object trigger collider when added in the editor.
    /// </summary>
    private void Reset()
    {
        CacheTriggerCollider();
        ConfigureTriggerCollider();
    }

    /// <summary>
    /// Caches and configures the same-object trigger collider before gameplay.
    /// </summary>
    private void Awake()
    {
        CacheTriggerCollider();
        ConfigureTriggerCollider();
    }

    /// <summary>
    /// Triggers the alarm when the player root enters this area.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (alarmController == null || other == null)
            return;

        GameObject root = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
        if (!IsPlayerRoot(root))
            return;

        alarmController.TriggerAlarm();
    }

    /// <summary>
    /// Caches the trigger collider from this GameObject.
    /// </summary>
    private void CacheTriggerCollider()
    {
        triggerCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Ensures the collider behaves as a trigger volume.
    /// </summary>
    private void ConfigureTriggerCollider()
    {
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    /// <summary>
    /// Returns whether the supplied root belongs to the player actor.
    /// </summary>
    private static bool IsPlayerRoot(GameObject root)
    {
        return root != null &&
               (root.GetComponent<PlayerTopDownMotor2D>() != null ||
                root.GetComponent<PlayerPickupInteractor>() != null);
    }
}

}
