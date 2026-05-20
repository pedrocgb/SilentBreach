using System;
using Breezeblocks.WeaponSystem;
using UnityEngine;

namespace Breezeblocks.Missions
{

public enum ActorDamageOutcome
{
    None,
    Damaged,
    Killed,
    Incapacitated
}

public readonly struct ActorDamageContext
{
    public ActorDamageContext(GameObject instigatorRoot, bool isLethal)
    {
        InstigatorRoot = instigatorRoot;
        IsLethal = isLethal;
    }

    public GameObject InstigatorRoot { get; }
    public bool IsLethal { get; }
}

public readonly struct MissionActorEvent
{
    public MissionActorEvent(ActorHealth actorHealth, MissionActorIdentity identity, GameObject instigatorRoot, bool wasLethal)
    {
        ActorHealth = actorHealth;
        Identity = identity;
        InstigatorRoot = instigatorRoot;
        WasLethal = wasLethal;
    }

    public ActorHealth ActorHealth { get; }
    public MissionActorIdentity Identity { get; }
    public GameObject InstigatorRoot { get; }
    public bool WasLethal { get; }
}

public readonly struct MissionPickupEvent
{
    public MissionPickupEvent(GameObject pickerRoot, PickableItemWorld pickableItem)
    {
        PickerRoot = pickerRoot;
        PickableItem = pickableItem;
    }

    public GameObject PickerRoot { get; }
    public PickableItemWorld PickableItem { get; }
    public string ItemId => PickableItem != null ? PickableItem.ItemId : string.Empty;
}

public readonly struct EnemyStateChangedEvent
{
    public EnemyStateChangedEvent(EnemyMovementController controller, EnemyState previousState, EnemyState newState)
    {
        Controller = controller;
        PreviousState = previousState;
        NewState = newState;
    }

    public EnemyMovementController Controller { get; }
    public EnemyState PreviousState { get; }
    public EnemyState NewState { get; }
}

public static class MissionRuntimeEvents
{
    public static event Action<MissionActorEvent> ActorKilled;
    public static event Action<MissionActorEvent> ActorIncapacitated;
    public static event Action<MissionPickupEvent> ItemPickedUp;
    public static event Action<EnemyStateChangedEvent> EnemyStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        ActorKilled = null;
        ActorIncapacitated = null;
        ItemPickedUp = null;
        EnemyStateChanged = null;
    }

    public static void RaiseActorKilled(ActorHealth actorHealth, GameObject instigatorRoot)
    {
        ActorKilled?.Invoke(new MissionActorEvent(
            actorHealth,
            actorHealth != null ? actorHealth.GetComponent<MissionActorIdentity>() ?? actorHealth.GetComponentInParent<MissionActorIdentity>() : null,
            instigatorRoot,
            wasLethal: true));
    }

    public static void RaiseActorIncapacitated(ActorHealth actorHealth, GameObject instigatorRoot)
    {
        ActorIncapacitated?.Invoke(new MissionActorEvent(
            actorHealth,
            actorHealth != null ? actorHealth.GetComponent<MissionActorIdentity>() ?? actorHealth.GetComponentInParent<MissionActorIdentity>() : null,
            instigatorRoot,
            wasLethal: false));
    }

    public static void RaiseItemPickedUp(GameObject pickerRoot, PickableItemWorld pickableItem)
    {
        ItemPickedUp?.Invoke(new MissionPickupEvent(pickerRoot, pickableItem));
    }

    public static void RaiseEnemyStateChanged(EnemyMovementController controller, EnemyState previousState, EnemyState newState)
    {
        EnemyStateChanged?.Invoke(new EnemyStateChangedEvent(controller, previousState, newState));
    }
}

}
