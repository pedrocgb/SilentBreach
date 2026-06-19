using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Alarm/Alarm Controller")]
public sealed class AlarmController : MonoBehaviour
{
    [FoldoutGroup("Siren Emitters"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<AlarmSirenEmitter> sirenEmitters = new();

    [FoldoutGroup("Lights"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> lightsToEnableOnAlarm = new();

    [FoldoutGroup("Lights"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> lightsToDisableOnAlarm = new();

    [FoldoutGroup("Enemies"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<EnemyMovementController> enemiesToAlert = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsTriggered => isTriggered;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDisarmed => isDisarmed;

    private bool isTriggered;
    private bool isDisarmed;

    /// <summary>
    /// Removes null scene references while editing.
    /// </summary>
    private void OnValidate()
    {
        sirenEmitters.RemoveAll(emitter => emitter == null);
        lightsToEnableOnAlarm.RemoveAll(lightObject => lightObject == null);
        lightsToDisableOnAlarm.RemoveAll(lightObject => lightObject == null);
        enemiesToAlert.RemoveAll(enemy => enemy == null);
    }

    /// <summary>
    /// Starts the alarm once, applying sirens, light state, and enemy alert reactions.
    /// </summary>
    public void TriggerAlarm()
    {
        if (isDisarmed || isTriggered)
            return;

        isTriggered = true;
        ApplySirenState(true);
        ApplyAlarmLightState(true);
        AlertConfiguredEnemies();
    }

    /// <summary>
    /// Permanently disarms the alarm and stops active siren and alarm light feedback.
    /// </summary>
    public void DisarmAlarm()
    {
        if (isDisarmed)
            return;

        bool wasTriggered = isTriggered;
        isDisarmed = true;
        isTriggered = false;
        if (!wasTriggered)
            return;

        ApplySirenState(false);
        ApplyAlarmLightState(false);
    }

    /// <summary>
    /// Restores runtime alarm state for testing without changing configured scene references.
    /// </summary>
    [Button(ButtonSizes.Small)]
    public void ResetAlarmRuntime()
    {
        isTriggered = false;
        isDisarmed = false;
        ApplySirenState(false);
        ApplyAlarmLightState(false);
    }

    /// <summary>
    /// Starts or stops every configured siren emitter.
    /// </summary>
    private void ApplySirenState(bool active)
    {
        for (int i = 0; i < sirenEmitters.Count; i++)
            sirenEmitters[i]?.SetAlarmActive(active);
    }

    /// <summary>
    /// Applies or reverses the configured alarm light changes.
    /// </summary>
    private void ApplyAlarmLightState(bool active)
    {
        for (int i = 0; i < lightsToEnableOnAlarm.Count; i++)
        {
            if (lightsToEnableOnAlarm[i] != null)
                lightsToEnableOnAlarm[i].SetActive(active);
        }

        for (int i = 0; i < lightsToDisableOnAlarm.Count; i++)
        {
            if (lightsToDisableOnAlarm[i] != null)
                lightsToDisableOnAlarm[i].SetActive(!active);
        }
    }

    /// <summary>
    /// Forces configured enemies into alert state when the alarm first triggers.
    /// </summary>
    private void AlertConfiguredEnemies()
    {
        for (int i = 0; i < enemiesToAlert.Count; i++)
            enemiesToAlert[i]?.EnterAlertState(force: true);
    }
}

}
