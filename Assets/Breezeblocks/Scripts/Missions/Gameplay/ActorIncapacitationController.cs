using System;
using System.Collections;
using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(ActorHealth))]
[AddComponentMenu("Breezeblocks/Missions/Actor Incapacitation Controller")]
public class ActorIncapacitationController : MonoBehaviour
{
    private static readonly List<ActorIncapacitationController> ActiveControllersInternal = new();

    private ActorHealth actorHealth;

    [FoldoutGroup("Wake Up"), MinValue(0f), Range(0.01f, 1f)]
    [SerializeField] private float restoredHealthFractionOnWake = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsIncapacitated => actorHealth != null && actorHealth.IsIncapacitated;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDead => actorHealth != null && actorHealth.IsDead;

    public static IReadOnlyList<ActorIncapacitationController> ActiveControllers => ActiveControllersInternal;

    public event Action<bool> IncapacitationStateChanged;

    private Coroutine wakeUpRoutine;

    /// <summary>
    /// Ensures an actor has an incapacitation controller for body interactions and wake-up timing.
    /// </summary>
    public static ActorIncapacitationController EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        ActorIncapacitationController controller = actorRoot.GetComponent<ActorIncapacitationController>();
        if (controller == null)
            controller = actorRoot.AddComponent<ActorIncapacitationController>();

        controller.CacheReferences();
        return controller;
    }

    /// <summary>
    /// Refreshes cached references when the component is reset in the editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches health reference before subscribing to runtime state changes.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Registers this controller and subscribes to actor health events.
    /// </summary>
    private void OnEnable()
    {
        if (!ActiveControllersInternal.Contains(this))
            ActiveControllersInternal.Add(this);

        if (actorHealth == null)
            actorHealth = GetComponent<ActorHealth>();

        if (actorHealth != null)
        {
            actorHealth.Incapacitated += HandleIncapacitated;
            actorHealth.Recovered += HandleRecovered;
            actorHealth.Died += HandleDied;
        }
    }

    /// <summary>
    /// Unregisters this controller and unsubscribes from actor health events.
    /// </summary>
    private void OnDisable()
    {
        ActiveControllersInternal.Remove(this);

        if (actorHealth != null)
        {
            actorHealth.Incapacitated -= HandleIncapacitated;
            actorHealth.Recovered -= HandleRecovered;
            actorHealth.Died -= HandleDied;
        }
    }

    /// <summary>
    /// Clamps wake-up settings and refreshes cached references while editing.
    /// </summary>
    private void OnValidate()
    {
        restoredHealthFractionOnWake = Mathf.Clamp01(restoredHealthFractionOnWake);
        CacheReferences();
    }

    /// <summary>
    /// Immediately recovers this actor from incapacitation using the configured restored health fraction.
    /// </summary>
    public void WakeUpNow()
    {
        if (actorHealth == null || !actorHealth.IsIncapacitated)
            return;

        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        actorHealth.RecoverFromIncapacitation(restoredHealthFractionOnWake);
    }

    /// <summary>
    /// Starts the configured incapacitation wake-up timer and notifies body interaction listeners.
    /// </summary>
    private void HandleIncapacitated(ActorDamageContext context)
    {
        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        IncapacitationStateChanged?.Invoke(true);

        float wakeDelay = GlobalSettings.Instance != null ? GlobalSettings.Instance.IncapacitatedWakeUpDelay : 0f;
        if (wakeDelay > 0f)
            wakeUpRoutine = StartCoroutine(WakeUpRoutine(wakeDelay));
    }

    /// <summary>
    /// Clears incapacitation wake-up state and notifies listeners that this body recovered.
    /// </summary>
    private void HandleRecovered()
    {
        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        if (actorHealth != null && actorHealth.IsDead)
            return;

        IncapacitationStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Freezes the actor in a dead-body state while keeping the corpse present in the scene.
    /// </summary>
    private void HandleDied(ActorDamageContext context)
    {
        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        IncapacitationStateChanged?.Invoke(false);
    }

    /// <summary>
    /// Waits for the incapacitation wake delay before recovering this actor.
    /// </summary>
    private IEnumerator WakeUpRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        wakeUpRoutine = null;
        actorHealth?.RecoverFromIncapacitation(restoredHealthFractionOnWake);
    }

    /// <summary>
    /// Caches the required same-object ActorHealth reference.
    /// </summary>
    private void CacheReferences()
    {
        if (actorHealth == null)
            actorHealth = GetComponent<ActorHealth>();
    }
}

}
