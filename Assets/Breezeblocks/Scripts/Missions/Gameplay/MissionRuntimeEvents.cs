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
    /// <summary>
    /// Creates context describing who caused actor damage and whether it was lethal.
    /// </summary>
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
    /// <summary>
    /// Creates a mission actor event with identity and damage outcome metadata.
    /// </summary>
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
    /// <summary>
    /// Creates a mission pickup event for the item collected by the supplied picker.
    /// </summary>
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
    /// <summary>
    /// Creates an enemy-state transition event for mission failure checks.
    /// </summary>
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

public readonly struct EnemyVisualDetectionEvent
{
    /// <summary>
    /// Creates a visual-detection event after an enemy fully detects the player.
    /// </summary>
    public EnemyVisualDetectionEvent(EnemyVisionAI visionAI, EnemyMovementController controller)
    {
        VisionAI = visionAI;
        Controller = controller;
    }

    public EnemyVisionAI VisionAI { get; }
    public EnemyMovementController Controller { get; }
}

public readonly struct MissionObjectiveObjectEvent
{
    /// <summary>
    /// Creates an object-activation event for objective progress matching.
    /// </summary>
    public MissionObjectiveObjectEvent(string objectiveId, GameObject sourceObject, GameObject interactorRoot)
    {
        ObjectiveId = objectiveId ?? string.Empty;
        SourceObject = sourceObject;
        InteractorRoot = interactorRoot;
    }

    public string ObjectiveId { get; }
    public GameObject SourceObject { get; }
    public GameObject InteractorRoot { get; }
}

public readonly struct MissionKidnappingEvent
{
    /// <summary>
    /// Creates a kidnapping delivery event for the delivered incapacitated target.
    /// </summary>
    public MissionKidnappingEvent(ActorHealth actorHealth, MissionActorIdentity identity, GameObject deliveryZone)
    {
        ActorHealth = actorHealth;
        Identity = identity;
        DeliveryZone = deliveryZone;
    }

    public ActorHealth ActorHealth { get; }
    public MissionActorIdentity Identity { get; }
    public GameObject DeliveryZone { get; }
}

public static class MissionRuntimeEvents
{
    public static event Action<MissionActorEvent> ActorKilled;
    public static event Action<MissionActorEvent> ActorIncapacitated;
    public static event Action<MissionPickupEvent> ItemPickedUp;
    public static event Action<EnemyStateChangedEvent> EnemyStateChanged;
    public static event Action<EnemyVisualDetectionEvent> EnemyPlayerFullyDetected;
    public static event Action<MissionObjectiveObjectEvent> ObjectiveObjectActivated;
    public static event Action<MissionKidnappingEvent> KidnappingDelivered;

    /// <summary>
    /// Clears static event subscriptions when Unity resets runtime statics.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        ResetRuntimeState();
    }

    /// <summary>
    /// Clears all mission runtime event subscriptions.
    /// </summary>
    public static void ResetRuntimeState()
    {
        ActorKilled = null;
        ActorIncapacitated = null;
        ItemPickedUp = null;
        EnemyStateChanged = null;
        EnemyPlayerFullyDetected = null;
        ObjectiveObjectActivated = null;
        KidnappingDelivered = null;
    }

    /// <summary>
    /// Raises an actor-killed event with identity resolved from the actor hierarchy.
    /// </summary>
    public static void RaiseActorKilled(ActorHealth actorHealth, GameObject instigatorRoot)
    {
        ActorKilled?.Invoke(new MissionActorEvent(
            actorHealth,
            actorHealth != null ? actorHealth.GetComponent<MissionActorIdentity>() ?? actorHealth.GetComponentInParent<MissionActorIdentity>() : null,
            instigatorRoot,
            wasLethal: true));
    }

    /// <summary>
    /// Raises an actor-incapacitated event with identity resolved from the actor hierarchy.
    /// </summary>
    public static void RaiseActorIncapacitated(ActorHealth actorHealth, GameObject instigatorRoot)
    {
        ActorIncapacitated?.Invoke(new MissionActorEvent(
            actorHealth,
            actorHealth != null ? actorHealth.GetComponent<MissionActorIdentity>() ?? actorHealth.GetComponentInParent<MissionActorIdentity>() : null,
            instigatorRoot,
            wasLethal: false));
    }

    /// <summary>
    /// Raises an item-picked-up event for retrieve-objective progress.
    /// </summary>
    public static void RaiseItemPickedUp(GameObject pickerRoot, PickableItemWorld pickableItem)
    {
        ItemPickedUp?.Invoke(new MissionPickupEvent(pickerRoot, pickableItem));
    }

    /// <summary>
    /// Raises an enemy state-change event for alert-related mission failures.
    /// </summary>
    public static void RaiseEnemyStateChanged(EnemyMovementController controller, EnemyState previousState, EnemyState newState)
    {
        EnemyStateChanged?.Invoke(new EnemyStateChangedEvent(controller, previousState, newState));
    }

    /// <summary>
    /// Raises a full visual-detection event for detection-related mission failures.
    /// </summary>
    public static void RaiseEnemyPlayerFullyDetected(EnemyVisionAI visionAI, EnemyMovementController controller)
    {
        EnemyPlayerFullyDetected?.Invoke(new EnemyVisualDetectionEvent(visionAI, controller));
    }

    /// <summary>
    /// Raises an activated-object event for objective progress.
    /// </summary>
    public static void RaiseObjectiveObjectActivated(string objectiveId, GameObject sourceObject, GameObject interactorRoot)
    {
        ObjectiveObjectActivated?.Invoke(new MissionObjectiveObjectEvent(objectiveId, sourceObject, interactorRoot));
    }

    /// <summary>
    /// Raises a kidnapping delivery event after an incapacitated target reaches the delivery zone.
    /// </summary>
    public static void RaiseKidnappingDelivered(ActorHealth actorHealth, MissionActorIdentity identity, GameObject deliveryZone)
    {
        KidnappingDelivered?.Invoke(new MissionKidnappingEvent(actorHealth, identity, deliveryZone));
    }
}

}
