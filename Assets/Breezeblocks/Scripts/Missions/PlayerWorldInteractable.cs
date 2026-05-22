using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

public abstract class PlayerWorldInteractable : MonoBehaviour
{
    private static readonly List<PlayerWorldInteractable> ActiveInteractablesInternal = new();

    [FoldoutGroup("Interact")]
    [SerializeField] private string interactionDisplayName;

    [FoldoutGroup("Interact")]
    [SerializeField] private bool interactionEnabled = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInteractionEnabled => interactionEnabled;

    public virtual string InteractionDisplayName => string.IsNullOrWhiteSpace(interactionDisplayName) ? name : interactionDisplayName;
    public virtual Vector3 InteractionPosition => transform.position;
    public static IReadOnlyList<PlayerWorldInteractable> ActiveInteractables => ActiveInteractablesInternal;

    protected virtual void OnEnable()
    {
        if (!ActiveInteractablesInternal.Contains(this))
            ActiveInteractablesInternal.Add(this);
    }

    protected virtual void OnDisable()
    {
        ActiveInteractablesInternal.Remove(this);
    }

    protected virtual void OnValidate()
    {
        interactionDisplayName = interactionDisplayName != null ? interactionDisplayName.Trim() : string.Empty;
    }

    public virtual bool CanInteract(GameObject interactorRoot)
    {
        return interactionEnabled && isActiveAndEnabled;
    }

    public bool TryInteract(GameObject interactorRoot)
    {
        return CanInteract(interactorRoot) && Interact(interactorRoot);
    }

    protected abstract bool Interact(GameObject interactorRoot);
}

}
