using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Stealth/Noise Manager")]
public sealed class NoiseManager : MonoBehaviour
{
    private static readonly List<AIHearing> Listeners = new();
    private static NoiseManager _instance;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public static int ListenerCount => Listeners.Count;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int TotalNoiseEventsEmitted => _totalNoiseEventsEmitted;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public NoiseEvent LastNoiseEvent => _lastNoiseEvent;

    [SerializeField, HideInInspector] private int _totalNoiseEventsEmitted;
    [SerializeField, HideInInspector] private Vector2 _lastNoisePosition;
    [SerializeField, HideInInspector] private float _lastNoiseIntensity;
    [SerializeField, HideInInspector] private NoiseType _lastNoiseType;
    [SerializeField, HideInInspector] private GameObject _lastNoiseSource;
    [SerializeField, HideInInspector] private float _lastNoiseTime;

    private NoiseEvent _lastNoiseEvent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Listeners.Clear();
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public static void RegisterListener(AIHearing listener)
    {
        if (listener == null || Listeners.Contains(listener))
            return;

        Listeners.Add(listener);
    }

    public static void UnregisterListener(AIHearing listener)
    {
        if (listener == null)
            return;

        Listeners.Remove(listener);
    }

    public static void EmitNoise(Vector2 position, float intensity, NoiseType noiseType, GameObject source = null, bool isExtremeNoise = false)
    {
        EmitNoise(new NoiseEvent(position, intensity, noiseType, source, isExtremeNoise));
    }

    public static void EmitNoise(NoiseEvent noiseEvent)
    {
        if (noiseEvent.Intensity <= 0f)
            return;

        for (int i = Listeners.Count - 1; i >= 0; i--)
        {
            AIHearing listener = Listeners[i];
            if (listener == null)
            {
                Listeners.RemoveAt(i);
                continue;
            }

            listener.ReceiveNoise(noiseEvent);
        }

        if (_instance != null)
            _instance.RecordNoise(noiseEvent);
    }

    private void RecordNoise(NoiseEvent noiseEvent)
    {
        _totalNoiseEventsEmitted++;
        _lastNoiseEvent = noiseEvent;
        _lastNoisePosition = noiseEvent.Position;
        _lastNoiseIntensity = noiseEvent.Intensity;
        _lastNoiseType = noiseEvent.NoiseType;
        _lastNoiseSource = noiseEvent.Source;
        _lastNoiseTime = noiseEvent.TimeCreated;
    }
}
