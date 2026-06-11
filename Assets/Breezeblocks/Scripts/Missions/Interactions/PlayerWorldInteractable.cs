using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

public abstract class PlayerWorldInteractable : MonoBehaviour
{
    [FoldoutGroup("Interact")]
    [SerializeField] private string interactionDisplayName;

    [FoldoutGroup("Interact")]
    [SerializeField] private bool interactionEnabled = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInteractionEnabled => interactionEnabled;

    public virtual string InteractionDisplayName => string.IsNullOrWhiteSpace(interactionDisplayName) ? name : interactionDisplayName;
    public virtual Vector3 InteractionPosition => transform.position;
    public static System.Collections.Generic.IReadOnlyList<PlayerWorldInteractable> ActiveInteractables => PlayerWorldInteractableRegistry.ActiveInteractables;

    public event System.Action<PlayerWorldInteractable> InteractionPresentationChanged;

    /// <summary>
    /// Registers the interactable so players can discover it at runtime.
    /// </summary>
    protected virtual void OnEnable()
    {
        PlayerWorldInteractableRegistry.Register(this);
    }

    /// <summary>
    /// Removes the interactable from the runtime registry when it is no longer usable.
    /// </summary>
    protected virtual void OnDisable()
    {
        PlayerWorldInteractableRegistry.Unregister(this);
    }

    /// <summary>
    /// Normalizes author-facing interaction labels in the inspector.
    /// </summary>
    protected virtual void OnValidate()
    {
        interactionDisplayName = interactionDisplayName != null ? interactionDisplayName.Trim() : string.Empty;
    }

    /// <summary>
    /// Returns whether the specified interactor can currently use this interactable.
    /// </summary>
    public virtual bool CanInteract(GameObject interactorRoot)
    {
        return interactionEnabled && isActiveAndEnabled;
    }

    /// <summary>
    /// Attempts to interact only when the interactable currently accepts the interactor.
    /// </summary>
    public bool TryInteract(GameObject interactorRoot)
    {
        return CanInteract(interactorRoot) && Interact(interactorRoot);
    }

    /// <summary>
    /// Requests any listening UI to refresh labels or other presentation tied to this interactable.
    /// </summary>
    public void RefreshInteractionPresentation()
    {
        InteractionPresentationChanged?.Invoke(this);
    }

    /// <summary>
    /// Executes the concrete interactable behavior for the supplied interactor.
    /// </summary>
    protected abstract bool Interact(GameObject interactorRoot);
}

}
