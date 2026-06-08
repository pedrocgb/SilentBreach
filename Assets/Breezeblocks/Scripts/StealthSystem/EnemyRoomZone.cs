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
    [SerializeField] private Collider2D roomCollider;

    [FoldoutGroup("References")]
    [SerializeField] private LightSwitchInteractable lightSwitch;

    [FoldoutGroup("Lights"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> roomLights = new();

    [FoldoutGroup("Doors"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<DoorInteractable> connectedDoors = new();

    [FoldoutGroup("Look Around"), SuffixLabel("deg", true)]
    [SerializeField] private float lookAroundMinAngle = -70f;

    [FoldoutGroup("Look Around"), SuffixLabel("deg", true)]
    [SerializeField] private float lookAroundMaxAngle = 70f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool AreLightsOn => areLightsOn;

    public event Action<EnemyRoomZone, bool> LightStateChanged;

    public LightSwitchInteractable LightSwitch => lightSwitch;
    public Vector2 SwitchPosition => lightSwitch != null ? (Vector2)lightSwitch.transform.position : (Vector2)transform.position;
    public float LookAroundMinAngle => lookAroundMinAngle;
    public float LookAroundMaxAngle => lookAroundMaxAngle;
    public static IReadOnlyList<EnemyRoomZone> ActiveZones => ActiveZonesInternal;

    private bool areLightsOn = true;

    private void Reset()
    {
        roomCollider = GetComponent<Collider2D>();
    }

    private void Awake()
    {
        CacheReferences();
        SanitizeConfiguration();
        areLightsOn = ComputeAreLightsOn();
    }

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

    private void OnDisable()
    {
        if (lightSwitch != null)
            lightSwitch.LightStateChanged -= HandleLightSwitchStateChanged;

        ActiveZonesInternal.Remove(this);
    }

    private void OnValidate()
    {
        CacheReferences();
        SanitizeConfiguration();

        if (lookAroundMaxAngle < lookAroundMinAngle)
            lookAroundMaxAngle = lookAroundMinAngle;
    }

    private void Update()
    {
        RefreshLightState(notifyListeners: true);
    }

    public bool ContainsPoint(Vector2 worldPoint)
    {
        return roomCollider != null && roomCollider.enabled && roomCollider.OverlapPoint(worldPoint);
    }

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

    private void CacheReferences()
    {
        if (roomCollider == null)
            roomCollider = GetComponent<Collider2D>();
    }

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

    private void HandleLightSwitchStateChanged(LightSwitchInteractable source, bool lightsOn)
    {
        if (source != lightSwitch)
            return;

        RefreshLightState(notifyListeners: true, fallbackValue: lightsOn);
    }

    private void RefreshLightState(bool notifyListeners, bool? fallbackValue = null)
    {
        bool nextState = fallbackValue ?? ComputeAreLightsOn();
        if (areLightsOn == nextState)
            return;

        areLightsOn = nextState;
        if (notifyListeners)
            LightStateChanged?.Invoke(this, areLightsOn);
    }

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

    private IReadOnlyList<GameObject> ResolveLights()
    {
        if (roomLights != null && roomLights.Count > 0)
            return roomLights;

        return lightSwitch != null ? lightSwitch.ControlledLights : null;
    }

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

    private float GetBoundsArea()
    {
        if (roomCollider == null)
            return float.PositiveInfinity;

        Vector3 size = roomCollider.bounds.size;
        return Mathf.Abs(size.x * size.y);
    }

    private bool IsDoorConnectedToRoom(DoorInteractable door)
    {
        if (door == null || roomCollider == null)
            return false;

        if (roomCollider.OverlapPoint(door.AwarenessSamplePosition))
            return true;

        return roomCollider.bounds.Intersects(door.AwarenessBounds);
    }
}
