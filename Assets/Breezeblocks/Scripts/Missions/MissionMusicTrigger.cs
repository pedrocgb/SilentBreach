using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Breezeblocks/Missions/Mission Music Trigger")]
public class MissionMusicTrigger : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private GameplayMissionController gameplayMissionController;

    [FoldoutGroup("References")]
    [SerializeField] private Collider2D triggerCollider;

    [FoldoutGroup("Music")]
    [SerializeField] private MissionMusicCue cue = MissionMusicCue.Lurking;

    [FoldoutGroup("Music")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;

    private void Reset()
    {
        CacheReferences();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        CacheReferences();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    public void Bind(GameplayMissionController controller)
    {
        gameplayMissionController = controller;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || gameplayMissionController == null || triggerCollider == null || !triggerCollider.enabled || other == null)
            return;

        bool handled = gameplayMissionController.TryHandleMusicTrigger(other.transform.root.gameObject, cue);
        if (!handled || !triggerOnce)
            return;

        hasTriggered = true;
        triggerCollider.enabled = false;
    }

    private void CacheReferences()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider2D>();

        if (gameplayMissionController == null)
            gameplayMissionController = FindFirstObjectByType<GameplayMissionController>();
    }
}

}
