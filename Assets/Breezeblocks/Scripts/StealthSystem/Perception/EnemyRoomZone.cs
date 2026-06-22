using System;
using System.Collections.Generic;
using Breezeblocks.Missions;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Breezeblocks/Stealth/Enemy Room Zone")]
public class EnemyRoomZone : MonoBehaviour
{
    private static readonly List<EnemyRoomZone> ActiveZonesInternal = new();

    [FoldoutGroup("References")]
    [SerializeField] private LightSwitchInteractable lightSwitch;

    [FoldoutGroup("Lights"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> roomLights = new();

    [FoldoutGroup("Doors"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<DoorInteractable> connectedDoors = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool AreLightsOn => areLightsOn;

    public event Action<EnemyRoomZone, bool> LightStateChanged;

    public LightSwitchInteractable LightSwitch => lightSwitch;
    public Vector2 SwitchPosition => lightSwitch != null ? (Vector2)lightSwitch.transform.position : (Vector2)transform.position;
    public float LookAroundMinAngle
    {
        get
        {
            ResolveLookaroundAngles(out float minAngle, out _);
            return minAngle;
        }
    }

    public float LookAroundMaxAngle
    {
        get
        {
            ResolveLookaroundAngles(out _, out float maxAngle);
            return maxAngle;
        }
    }
    public static IReadOnlyList<EnemyRoomZone> ActiveZones => ActiveZonesInternal;

    private Collider2D roomCollider;
    private bool areLightsOn = true;

    /// <summary>
    /// Caches the mandatory room collider when this component is added in the editor.
    /// </summary>
    private void Reset()
    {
        roomCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Caches room references and initializes the current light state.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        SanitizeConfiguration();
        areLightsOn = ComputeAreLightsOn();
    }

    /// <summary>
    /// Registers this room and subscribes to its configured light switch.
    /// </summary>
    private void OnEnable()
    {
        CacheReferences();
        SanitizeConfiguration();
        if (!ActiveZonesInternal.Contains(this))
            ActiveZonesInternal.Add(this);

        if (lightSwitch != null)
            lightSwitch.LightStateChanged += HandleLightSwitchStateChanged;

        RefreshLightState(notifyListeners: false);
    }

    /// <summary>
    /// Unregisters this room and removes light-switch callbacks.
    /// </summary>
    private void OnDisable()
    {
        if (lightSwitch != null)
            lightSwitch.LightStateChanged -= HandleLightSwitchStateChanged;

        ActiveZonesInternal.Remove(this);
    }

    /// <summary>
    /// Refreshes external references and removes invalid authored entries while editing.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
        SanitizeConfiguration();
    }

    /// <summary>
    /// Detects direct light-object state changes not routed through the light switch.
    /// </summary>
    private void Update()
    {
        RefreshLightState(notifyListeners: true);
    }

    /// <summary>
    /// Returns whether the supplied world point lies inside this room collider.
    /// </summary>
    public bool ContainsPoint(Vector2 worldPoint)
    {
        return roomCollider != null && roomCollider.enabled && roomCollider.OverlapPoint(worldPoint);
    }

    /// <summary>
    /// Turns this room's lights on through its switch or direct light references.
    /// </summary>
    public bool TryTurnLightsOn(GameObject interactorRoot = null, bool playSfx = true)
    {
        if (lightSwitch != null)
            return lightSwitch.SetLightState(true, playSfx, interactorRoot);

        bool changed = !AreLightsOn;
        SetLightsActive(true);
        if (changed)
        {
            areLightsOn = true;
            LightStateChanged?.Invoke(this, true);
        }

        return changed;
    }

    /// <summary>
    /// Finds the smallest active room zone containing the supplied world point.
    /// </summary>
    public static EnemyRoomZone FindContainingPoint(Vector2 worldPoint)
    {
        EnemyRoomZone bestMatch = null;
        float bestArea = float.PositiveInfinity;

        for (int i = 0; i < ActiveZonesInternal.Count; i++)
        {
            EnemyRoomZone zone = ActiveZonesInternal[i];
            if (zone == null || !zone.isActiveAndEnabled || !zone.ContainsPoint(worldPoint))
                continue;

            float area = zone.GetBoundsArea();
            if (bestMatch == null || area < bestArea)
            {
                bestMatch = zone;
                bestArea = area;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Fills the supplied list with explicitly configured or spatially connected doors.
    /// </summary>
    public void GetConnectedDoors(List<DoorInteractable> results)
    {
        if (results == null)
            return;

        results.Clear();
        SanitizeConfiguration();

        if (connectedDoors != null && connectedDoors.Count > 0)
        {
            results.AddRange(connectedDoors);
            return;
        }

        IReadOnlyList<DoorInteractable> activeDoors = DoorInteractable.ActiveDoors;
        for (int i = 0; i < activeDoors.Count; i++)
        {
            DoorInteractable door = activeDoors[i];
            if (door == null || !door.isActiveAndEnabled || !IsDoorConnectedToRoom(door))
                continue;

            results.Add(door);
        }
    }

    /// <summary>
    /// Resolves the direction used as the basis for enemy lookaround angles at the switch.
    /// </summary>
    public Vector2 ResolveLookAroundBaseDirection(Vector2 fallbackOrigin)
    {
        Vector2 switchPosition = SwitchPosition;
        Vector2 roomCenter = roomCollider != null ? roomCollider.bounds.center : transform.position;
        Vector2 basis = roomCenter - switchPosition;

        if (basis.sqrMagnitude <= Mathf.Epsilon)
            basis = roomCenter - fallbackOrigin;

        if (basis.sqrMagnitude <= Mathf.Epsilon)
            basis = transform.up;

        return basis.normalized;
    }

    /// <summary>
    /// Resolves this room's enemy lookaround range from its assigned light-switch preset.
    /// </summary>
    private void ResolveLookaroundAngles(out float minAngle, out float maxAngle)
    {
        LightSwitchLookaroundPreset preset = lightSwitch != null
            ? lightSwitch.EnemyLookaroundPreset
            : LightSwitchLookaroundPreset.RightLookaround;

        if (GlobalSettings.Instance != null)
        {
            GlobalSettings.Instance.GetLightSwitchLookaroundAngles(preset, out minAngle, out maxAngle);
            return;
        }

        LightSwitchLookaroundPresetDefaults.Resolve(preset, out minAngle, out maxAngle);
    }

    /// <summary>
    /// Caches the same-object room collider.
    /// </summary>
    private void CacheReferences()
    {
        if (roomCollider == null)
            roomCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Initializes room collections and removes missing scene references.
    /// </summary>
    private void SanitizeConfiguration()
    {
        roomLights ??= new List<GameObject>();
        connectedDoors ??= new List<DoorInteractable>();

        for (int i = roomLights.Count - 1; i >= 0; i--)
        {
            if (roomLights[i] == null)
                roomLights.RemoveAt(i);
        }

        for (int i = connectedDoors.Count - 1; i >= 0; i--)
        {
            if (connectedDoors[i] == null)
                connectedDoors.RemoveAt(i);
        }
    }

    /// <summary>
    /// Refreshes room light state after its configured switch changes.
    /// </summary>
    private void HandleLightSwitchStateChanged(LightSwitchInteractable source, bool lightsOn)
    {
        if (source != lightSwitch)
            return;

        RefreshLightState(notifyListeners: true, fallbackValue: lightsOn);
    }

    /// <summary>
    /// Recomputes room light state and optionally notifies awareness listeners.
    /// </summary>
    private void RefreshLightState(bool notifyListeners, bool? fallbackValue = null)
    {
        bool nextState = fallbackValue ?? ComputeAreLightsOn();
        if (areLightsOn == nextState)
            return;

        areLightsOn = nextState;
        if (notifyListeners)
            LightStateChanged?.Invoke(this, areLightsOn);
    }

    /// <summary>
    /// Determines whether any valid light associated with this room is active.
    /// </summary>
    private bool ComputeAreLightsOn()
    {
        IReadOnlyList<GameObject> lights = ResolveLights();
        if (lights == null || lights.Count == 0)
            return true;

        bool hasAnyValidLightReference = false;
        for (int i = 0; i < lights.Count; i++)
        {
            GameObject lightObject = lights[i];
            if (lightObject == null)
                continue;

            hasAnyValidLightReference = true;
            if (lightObject.activeInHierarchy)
                return true;
        }

        return !hasAnyValidLightReference;
    }

    /// <summary>
    /// Returns explicit room lights or falls back to lights controlled by the switch.
    /// </summary>
    private IReadOnlyList<GameObject> ResolveLights()
    {
        if (roomLights != null && roomLights.Count > 0)
            return roomLights;

        return lightSwitch != null ? lightSwitch.ControlledLights : null;
    }

    /// <summary>
    /// Applies one active state to every light associated with this room.
    /// </summary>
    private void SetLightsActive(bool active)
    {
        IReadOnlyList<GameObject> lights = ResolveLights();
        if (lights == null)
            return;

        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i] != null)
                lights[i].SetActive(active);
        }
    }

    /// <summary>
    /// Returns room collider area for nested-room selection.
    /// </summary>
    private float GetBoundsArea()
    {
        if (roomCollider == null)
            return float.PositiveInfinity;

        Vector3 size = roomCollider.bounds.size;
        return Mathf.Abs(size.x * size.y);
    }

    /// <summary>
    /// Returns whether a door's awareness bounds overlap this room.
    /// </summary>
    private bool IsDoorConnectedToRoom(DoorInteractable door)
    {
        if (door == null || roomCollider == null)
            return false;

        if (roomCollider.OverlapPoint(door.AwarenessSamplePosition))
            return true;

        return roomCollider.bounds.Intersects(door.AwarenessBounds);
    }
}
