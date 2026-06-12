using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Cut Wire/Power Shutdown Effect")]
public sealed class CutWirePowerShutdownEffect : MonoBehaviour
{
    [FoldoutGroup("Power Shutdown"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> lightsToDisable = new();

    [FoldoutGroup("Power Shutdown"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<LightSwitchInteractable> switchesToDisable = new();

    [FoldoutGroup("Alert"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<EnemyMovementController> enemiesToAlert = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasApplied => hasApplied;

    private bool hasApplied;

    /// <summary>
    /// Permanently disables configured power objects and alerts only explicitly configured enemies.
    /// </summary>
    public void Apply()
    {
        if (hasApplied)
            return;

        hasApplied = true;
        for (int i = 0; i < switchesToDisable.Count; i++)
        {
            if (switchesToDisable[i] != null)
                switchesToDisable[i].SetPowerDisabled(true);
        }

        for (int i = 0; i < lightsToDisable.Count; i++)
        {
            if (lightsToDisable[i] != null)
                lightsToDisable[i].SetActive(false);
        }

        for (int i = 0; i < enemiesToAlert.Count; i++)
        {
            if (enemiesToAlert[i] != null)
                enemiesToAlert[i].EnterAlertState(force: true);
        }
    }
}

}
