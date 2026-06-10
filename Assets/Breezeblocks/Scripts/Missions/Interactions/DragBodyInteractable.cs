using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(ActorHealth))]
[AddComponentMenu("Breezeblocks/Missions/Drag Body Interactable")]
public sealed class DragBodyInteractable : PlayerWorldInteractable, IPlayerHoldInteractable
{
    [FoldoutGroup("References")]
    [SerializeField] private Transform dragAnchor;

    [FoldoutGroup("References")]
    [SerializeField] private Rigidbody2D bodyRigidbody;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorHealth actorHealth;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDraggableBody => actorHealth != null && (actorHealth.IsDead || actorHealth.IsIncapacitated);

    /// <summary>
    /// Gets drag anchor position used by the player drag controller.
    /// </summary>
    public Vector2 DragAnchorPosition => dragAnchor != null ? (Vector2)dragAnchor.position : (Vector2)transform.position;

    /// <summary>
    /// Gets dragged body rigidbody when present.
    /// </summary>
    public Rigidbody2D BodyRigidbody => bodyRigidbody;

    /// <summary>
    /// Gets the authoritative body health state.
    /// </summary>
    public ActorHealth ActorHealth => actorHealth;

    /// <summary>
    /// Returns prompt position for body dragging.
    /// </summary>
    public override Vector3 InteractionPosition => dragAnchor != null ? dragAnchor.position : transform.position;

    /// <summary>
    /// Caches default same-object references while editing.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches same-object references required for dragging.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Refreshes cached authoring references while editing.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        CacheReferences();
    }

    /// <summary>
    /// Returns whether the body can currently be dragged by the supplied interactor.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        if (!base.CanInteract(interactorRoot) || !IsDraggableBody || interactorRoot == null)
            return false;

        PlayerBodyDragController dragController = interactorRoot.GetComponent<PlayerBodyDragController>();
        return dragController != null && dragController.CanStartDragging(this);
    }

    /// <summary>
    /// Drag bodies only through held interaction flow.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        return false;
    }

    /// <summary>
    /// Attempts to begin dragging this body through the interactor's drag controller.
    /// </summary>
    public bool TryBeginHold(GameObject interactorRoot)
    {
        if (!CanInteract(interactorRoot))
            return false;

        PlayerBodyDragController dragController = interactorRoot.GetComponent<PlayerBodyDragController>();
        return dragController != null && dragController.TryBeginDragging(this);
    }

    /// <summary>
    /// Returns whether this body is still being managed by the player's drag controller.
    /// </summary>
    public bool IsHoldActive(GameObject interactorRoot)
    {
        if (interactorRoot == null)
            return false;

        PlayerBodyDragController dragController = interactorRoot.GetComponent<PlayerBodyDragController>();
        return dragController != null && dragController.IsManagingDrag(this);
    }

    /// <summary>
    /// Keeps the held interaction alive while the player continues dragging.
    /// </summary>
    public void TickHold(GameObject interactorRoot, float deltaTime)
    {
        if (interactorRoot == null)
            return;

        PlayerBodyDragController dragController = interactorRoot.GetComponent<PlayerBodyDragController>();
        dragController?.MaintainHeldDrag(this, deltaTime);
    }

    /// <summary>
    /// Stops dragging when the active interaction ends.
    /// </summary>
    public void EndHold(GameObject interactorRoot)
    {
        if (interactorRoot == null)
            return;

        PlayerBodyDragController dragController = interactorRoot.GetComponent<PlayerBodyDragController>();
        dragController?.StopDragging(this);
    }

    /// <summary>
    /// Caches same-object references used by the interactable.
    /// </summary>
    private void CacheReferences()
    {
        actorHealth ??= GetComponent<ActorHealth>();
        bodyRigidbody ??= GetComponent<Rigidbody2D>();

        if (dragAnchor == null)
            dragAnchor = transform;
    }
}

}
