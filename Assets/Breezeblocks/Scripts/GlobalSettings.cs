using System;
using Sirenix.OdinInspector;
using UnityEngine;

[AddComponentMenu("Breezeblocks/Global Settings")]
public class GlobalSettings : MonoBehaviour
{
    public static GlobalSettings Instance { get; private set; }

    [FoldoutGroup("Input Modes"), Tooltip("If true, sprint works as toggle (press once). If false, hold to sprint.")]
    [SerializeField] private bool sprintToggleEnabled = false;

    [FoldoutGroup("Input Modes"), Tooltip("If true, focus works as toggle (press once). If false, hold to focus.")]
    [SerializeField] private bool focusToggleEnabled = false;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [Tooltip("How long a firearm shot noise spike lasts.")]
    [SerializeField] private float shotNoiseDuration = 0.1f;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [Tooltip("How long an equip noise spike lasts.")]
    [SerializeField] private float equipNoiseDuration = 0.4f;

    [FoldoutGroup("Noise"), MinValue(0f)]
    [Tooltip("How long a holster noise spike lasts.")]
    [SerializeField] private float holsterNoiseDuration = 0.6f;

    [FoldoutGroup("Combat"), MinValue(0f), SuffixLabel("s", true)]
    [Tooltip("How long incapacitated actors stay down before waking up again.")]
    [SerializeField] private float incapacitatedWakeUpDelay = 60f;

    public bool SprintToggleEnabled => sprintToggleEnabled;
    public bool FocusToggleEnabled => focusToggleEnabled;
    public float ShotNoiseDuration => shotNoiseDuration;
    public float EquipNoiseDuration => equipNoiseDuration;
    public float HolsterNoiseDuration => holsterNoiseDuration;
    public float IncapacitatedWakeUpDelay => incapacitatedWakeUpDelay;

    public event Action SettingsChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        shotNoiseDuration = Mathf.Max(0f, shotNoiseDuration);
        equipNoiseDuration = Mathf.Max(0f, equipNoiseDuration);
        holsterNoiseDuration = Mathf.Max(0f, holsterNoiseDuration);
        incapacitatedWakeUpDelay = Mathf.Max(0f, incapacitatedWakeUpDelay);
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void ToggleSprintMode()
    {
        SetSprintToggleEnabled(!sprintToggleEnabled);
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void ToggleFocusMode()
    {
        SetFocusToggleEnabled(!focusToggleEnabled);
    }

    public void SetSprintToggleEnabled(bool enabled)
    {
        if (sprintToggleEnabled == enabled)
            return;

        sprintToggleEnabled = enabled;
        SettingsChanged?.Invoke();
    }

    public void SetFocusToggleEnabled(bool enabled)
    {
        if (focusToggleEnabled == enabled)
            return;

        focusToggleEnabled = enabled;
        SettingsChanged?.Invoke();
    }
}
