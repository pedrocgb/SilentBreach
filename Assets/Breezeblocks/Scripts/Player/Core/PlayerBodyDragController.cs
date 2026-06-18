using System.Collections;
using Breezeblocks.Missions;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerTopDownMotor2D))]
[RequireComponent(typeof(PlayerEquipmentController))]
[AddComponentMenu("Breezeblocks/Player/Player Body Drag Controller")]
public sealed class PlayerBodyDragController : MonoBehaviour
{
    private const float MinimumDragFollowSpeed = 0.01f;
    private const float MinimumDragNoiseInterval = 0.02f;
    private const float MinimumMovementThreshold = 0.0001f;
    private const float DragStartFacingDotThreshold = 0.99f;

    [FoldoutGroup("References")]
    [SerializeField] private Transform dragOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private Transform draggedBodyHoldPoint;

    private WorldSfxManager worldSfxManager;
    private AudioClipSet dragMovementSfx = new();

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerEquipmentController playerEquipmentController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerPickupInteractor playerPickupInteractor;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerNoiseEmitter playerNoiseEmitter;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerFocusController playerFocusController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ActorStaggerController actorStaggerController;

    private float dragDistance = 0.7f;
    private float dragVerticalOffset;
    private float dragFollowSpeed = 5f;
    private float movingBodyThreshold = 0.05f;
    private float dragNoiseInterval = 0.28f;
    private float dragNoiseIntensity = 0.35f;
    private NoiseType dragNoiseType = NoiseType.Common;
    private bool dragNoiseExtreme;
    private float dragSfxVolumeMultiplier = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDragging => activeBody != null && isDragging;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsPendingDragStart => pendingDragStart;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public DragBodyInteractable ActiveBody => activeBody;

    private DragBodyInteractable activeBody;
    private Coroutine dragStartRoutine;
    private bool pendingDragStart;
    private bool isDragging;
    private float nextDragFeedbackTime = float.NegativeInfinity;

    /// <summary>
    /// Caches default references while editing.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches same-object references used by drag runtime.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);
    }

    /// <summary>
    /// Clears drag state when the controller is disabled.
    /// </summary>
    private void OnDisable()
    {
        CancelPendingDragStart();
        ReleaseDragState();
    }

    /// <summary>
    /// Clamps authoring values and refreshes cached references while editing.
    /// </summary>
    private void OnValidate()
    {
        dragDistance = Mathf.Max(0f, dragDistance);
        dragFollowSpeed = Mathf.Max(MinimumDragFollowSpeed, dragFollowSpeed);
        movingBodyThreshold = Mathf.Max(0f, movingBodyThreshold);
        dragNoiseInterval = Mathf.Max(MinimumDragNoiseInterval, dragNoiseInterval);
        dragNoiseIntensity = Mathf.Max(0f, dragNoiseIntensity);
        dragSfxVolumeMultiplier = Mathf.Max(0f, dragSfxVolumeMultiplier);
        dragMovementSfx ??= new AudioClipSet();
        CacheReferences();
    }

    /// <summary>
    /// Applies profile-authored body drag movement, noise, and SFX settings.
    /// </summary>
    public void ApplySettings(PlayerBodyDragSettings settings)
    {
        if (settings == null)
            return;

        dragDistance = Mathf.Max(0f, settings.DragDistance);
        dragVerticalOffset = settings.DragVerticalOffset;
        dragFollowSpeed = Mathf.Max(MinimumDragFollowSpeed, settings.DragFollowSpeed);
        movingBodyThreshold = Mathf.Max(0f, settings.MovingBodyThreshold);
        dragMovementSfx = settings.DragMovementSfx ?? new AudioClipSet();
        dragNoiseInterval = Mathf.Max(MinimumDragNoiseInterval, settings.DragNoiseInterval);
        dragNoiseIntensity = Mathf.Max(0f, settings.DragNoiseIntensity);
        dragNoiseType = settings.DragNoiseType;
        dragNoiseExtreme = settings.DragNoiseExtreme;
        dragSfxVolumeMultiplier = Mathf.Max(0f, settings.DragSfxVolumeMultiplier);
    }

    /// <summary>
    /// Maintains drag validity and runtime feedback while dragging.
    /// </summary>
    private void Update()
    {
        if (activeBody == null)
            return;

        if (!IsBodyStillDraggable(activeBody))
        {
            StopDragging(activeBody);
            return;
        }

        if (!isDragging)
            return;

        EmitDragFeedbackIfMoving();
    }

    /// <summary>
    /// Moves the dragged body toward the follow anchor during physics updates.
    /// </summary>
    private void FixedUpdate()
    {
        if (!isDragging || activeBody == null)
            return;

        Vector2 targetPosition = ResolveDraggedBodyTargetPosition();
        MoveDraggedBody(activeBody, targetPosition);
    }

    /// <summary>
    /// Returns whether the supplied body may start dragging right now.
    /// </summary>
    public bool CanStartDragging(DragBodyInteractable body)
    {
        return body != null &&
               IsBodyStillDraggable(body) &&
               (activeBody == null || activeBody == body);
    }

    /// <summary>
    /// Attempts to begin dragging a body and holsters current equipment first when needed.
    /// </summary>
    public bool TryBeginDragging(DragBodyInteractable body)
    {
        if (!CanStartDragging(body))
            return false;

        if (activeBody == body && (pendingDragStart || isDragging))
            return true;

        if (activeBody != null && activeBody != body)
            return false;

        activeBody = body;
        if (dragStartRoutine != null)
            StopCoroutine(dragStartRoutine);

        dragStartRoutine = StartCoroutine(BeginDragRoutine(body));
        return true;
    }

    /// <summary>
    /// Returns whether the supplied body is currently pending drag start or already being dragged.
    /// </summary>
    public bool IsManagingDrag(DragBodyInteractable body)
    {
        return body != null && body == activeBody && (pendingDragStart || isDragging);
    }

    /// <summary>
    /// Keeps an active held drag alive while the interact button remains pressed.
    /// </summary>
    public void MaintainHeldDrag(DragBodyInteractable body, float deltaTime)
    {
        if (body == null || body != activeBody)
            return;

        if (isDragging)
            ApplyDraggingRuntimeState();
    }

    /// <summary>
    /// Stops dragging when the interact button is released or the body becomes invalid.
    /// </summary>
    public void StopDragging(DragBodyInteractable body)
    {
        if (body == null || body != activeBody)
            return;

        CancelPendingDragStart();
        ReleaseDragState();
    }

    /// <summary>
    /// Caches same-object and adjacent references needed by drag logic.
    /// </summary>
    private void CacheReferences()
    {
        playerMotor ??= GetComponent<PlayerTopDownMotor2D>();
        playerEquipmentController ??= GetComponent<PlayerEquipmentController>();
        playerWeaponController ??= GetComponent<PlayerWeaponController>();
        playerUtilityController ??= GetComponent<PlayerUtilityController>();
        playerMeleeController ??= GetComponent<PlayerMeleeController>();
        playerPickupInteractor ??= GetComponent<PlayerPickupInteractor>();
        playerNoiseEmitter ??= GetComponent<PlayerNoiseEmitter>();
        playerFocusController ??= GetComponent<PlayerFocusController>();
        actorStaggerController ??= GetComponent<ActorStaggerController>();

        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>(true);

        if (dragOrigin == null)
            dragOrigin = transform;
    }

    /// <summary>
    /// Waits for holster completion when needed, then transitions into active dragging.
    /// </summary>
    private IEnumerator BeginDragRoutine(DragBodyInteractable body)
    {
        pendingDragStart = true;
        isDragging = false;
        ApplyHolsterLockState();

        if (playerEquipmentController != null && playerEquipmentController.CurrentHeldItem != null)
        {
            while (playerEquipmentController != null && !playerEquipmentController.CanHolsterCurrentHeldItem())
                yield return null;

            if (playerEquipmentController == null || activeBody != body)
            {
                dragStartRoutine = null;
                yield break;
            }

            if (!playerEquipmentController.BeginHolsterCurrentHeldItem())
            {
                ReleaseDragState();
                dragStartRoutine = null;
                yield break;
            }

            while (playerEquipmentController != null &&
                   (playerEquipmentController.IsSwitchingEquipment || playerEquipmentController.CurrentHeldItem != null))
            {
                if (activeBody != body)
                {
                    dragStartRoutine = null;
                    yield break;
                }

                yield return null;
            }
        }

        if (activeBody != body || !IsBodyStillDraggable(body))
        {
            ReleaseDragState();
            dragStartRoutine = null;
            yield break;
        }

        yield return RotateTowardsBodyBeforeDrag(body);

        if (activeBody != body || !IsBodyStillDraggable(body))
        {
            ReleaseDragState();
            dragStartRoutine = null;
            yield break;
        }

        pendingDragStart = false;
        isDragging = true;
        nextDragFeedbackTime = Time.time;
        ApplyDraggingRuntimeState();
        dragStartRoutine = null;
    }

    /// <summary>
    /// Applies full player lock while current equipment is being holstered for dragging.
    /// </summary>
    private void ApplyHolsterLockState()
    {
        playerMotor?.SetInputBlocked(true);
        playerVisionLight?.SetMouseLookSuppressed(true);
        playerWeaponController?.SetInputBlocked(true);
        playerUtilityController?.SetInputBlocked(true);
        playerMeleeController?.SetInputBlocked(true);
        playerPickupInteractor?.SetInputBlocked(true);
        playerEquipmentController?.SetDragInputBlocked(true);
        playerFocusController?.SetInputBlocked(true);
    }

    /// <summary>
    /// Applies dragging movement slowdown and action restrictions after holster completes.
    /// </summary>
    private void ApplyDraggingRuntimeState()
    {
        playerMotor?.SetInputBlocked(false);
        playerMotor?.SetExternalSpeedOverride(true, ResolveDragMoveSpeed(), lockSpeedSelection: true);
        playerMotor?.SetSprintBlocked(true);
        playerVisionLight?.SetMouseLookSuppressed(true);
        playerWeaponController?.SetInputBlocked(true);
        playerUtilityController?.SetInputBlocked(true);
        playerMeleeController?.SetInputBlocked(true);
        playerPickupInteractor?.SetInputBlocked(true);
        playerEquipmentController?.SetDragInputBlocked(true);
        playerFocusController?.SetInputBlocked(true);
    }

    /// <summary>
    /// Clears pending or active drag state and restores normal player controls.
    /// </summary>
    private void ReleaseDragState()
    {
        pendingDragStart = false;
        isDragging = false;
        activeBody = null;
        nextDragFeedbackTime = float.NegativeInfinity;

        playerMotor?.SetInputBlocked(false);
        playerMotor?.SetExternalSpeedOverride(false, 0f, lockSpeedSelection: false);
        playerMotor?.SetSprintBlocked(false);
        playerVisionLight?.SetMouseLookSuppressed(false);
        playerWeaponController?.SetInputBlocked(false);
        playerUtilityController?.SetInputBlocked(false);
        playerMeleeController?.SetInputBlocked(false);
        playerPickupInteractor?.SetInputBlocked(false);
        playerEquipmentController?.SetDragInputBlocked(false);
        playerFocusController?.SetInputBlocked(false);
    }

    /// <summary>
    /// Cancels the pending drag-start coroutine if one is active.
    /// </summary>
    private void CancelPendingDragStart()
    {
        if (dragStartRoutine == null)
            return;

        StopCoroutine(dragStartRoutine);
        dragStartRoutine = null;
        pendingDragStart = false;
    }

    /// <summary>
    /// Returns whether the body still satisfies dead or incapacitated dragging rules.
    /// </summary>
    private static bool IsBodyStillDraggable(DragBodyInteractable body)
    {
        return body != null &&
               body.isActiveAndEnabled &&
               body.ActorHealth != null &&
               (body.ActorHealth.IsDead || body.ActorHealth.IsIncapacitated);
    }

    /// <summary>
    /// Rotates the player toward the dragged body before the drag fully starts.
    /// </summary>
    private IEnumerator RotateTowardsBodyBeforeDrag(DragBodyInteractable body)
    {
        if (body == null || playerVisionLight == null)
            yield break;

        while (activeBody == body && IsBodyStillDraggable(body))
        {
            if (!TryResolveDirectionToBody(body, out Vector2 directionToBody) || IsFacingBodyForDrag(directionToBody))
                yield break;

            playerVisionLight.ApplyExternalDirection(directionToBody, ResolveDragRotationSpeed(), Time.deltaTime);
            yield return null;
        }
    }

    /// <summary>
    /// Returns whether the player is already facing the body closely enough to begin dragging.
    /// </summary>
    private bool IsFacingBodyForDrag(Vector2 directionToBody)
    {
        if (directionToBody.sqrMagnitude <= MinimumMovementThreshold)
            return true;

        Vector2 currentFacing = playerVisionLight != null && playerVisionLight.FacingDirection.sqrMagnitude > MinimumMovementThreshold
            ? playerVisionLight.FacingDirection.normalized
            : (Vector2)transform.up;
        return Vector2.Dot(currentFacing, directionToBody.normalized) >= DragStartFacingDotThreshold;
    }

    /// <summary>
    /// Resolves normalized world direction from the player to the dragged body.
    /// </summary>
    private bool TryResolveDirectionToBody(DragBodyInteractable body, out Vector2 directionToBody)
    {
        directionToBody = Vector2.zero;
        if (body == null)
            return false;

        directionToBody = body.DragAnchorPosition - (Vector2)transform.position;
        if (directionToBody.sqrMagnitude <= MinimumMovementThreshold)
            return false;

        directionToBody.Normalize();
        return true;
    }

    /// <summary>
    /// Resolves the dragged body follow point from the explicit player hold point when configured.
    /// </summary>
    private Vector2 ResolveDraggedBodyTargetPosition()
    {
        if (draggedBodyHoldPoint != null)
            return draggedBodyHoldPoint.position;

        Transform originTransform = dragOrigin != null ? dragOrigin : transform;
        if (originTransform != transform)
            return (Vector2)originTransform.position + (Vector2.up * dragVerticalOffset);

        Vector2 movementDirection = playerMotor != null && playerMotor.HasMovementInput
            ? playerMotor.MoveInput.normalized
            : playerMotor != null && playerMotor.LastMoveDirection.sqrMagnitude > MinimumMovementThreshold
                ? playerMotor.LastMoveDirection.normalized
                : Vector2.down;

        Vector2 offset = (-movementDirection * dragDistance) + (Vector2.up * dragVerticalOffset);
        return (Vector2)originTransform.position + offset;
    }

    /// <summary>
    /// Moves the dragged body toward the resolved follow point with rigidbody-safe motion.
    /// </summary>
    private void MoveDraggedBody(DragBodyInteractable body, Vector2 targetPosition)
    {
        Rigidbody2D bodyRigidbody = body.BodyRigidbody;
        if (bodyRigidbody != null)
        {
            Vector2 nextPosition = Vector2.MoveTowards(bodyRigidbody.position, targetPosition, dragFollowSpeed * Time.fixedDeltaTime);
            bodyRigidbody.linearVelocity = Vector2.zero;
            bodyRigidbody.angularVelocity = 0f;
            bodyRigidbody.MovePosition(nextPosition);
            return;
        }

        Vector2 currentPosition = body.transform.position;
        body.transform.position = Vector2.MoveTowards(currentPosition, targetPosition, dragFollowSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Emits drag noise and movement SFX only while the player is actively moving a body.
    /// </summary>
    private void EmitDragFeedbackIfMoving()
    {
        if (playerMotor == null || !playerMotor.HasMovementInput || playerMotor.CurrentPlanarSpeed <= movingBodyThreshold)
            return;

        if (Time.time < nextDragFeedbackTime)
            return;

        if (dragNoiseIntensity > 0f)
            playerNoiseEmitter?.EmitNoise(dragNoiseIntensity, dragNoiseType, dragNoiseExtreme);

        worldSfxManager = WeaponRuntimeUtility.ResolveWorldSfxManager(worldSfxManager);
        if (worldSfxManager != null && dragMovementSfx != null)
            worldSfxManager.PlayClipSetAt(transform.position, dragMovementSfx, dragNoiseType, dragSfxVolumeMultiplier);

        nextDragFeedbackTime = Time.time + dragNoiseInterval;
    }

    /// <summary>
    /// Resolves the drag-time player rotation speed using the current look smoothing and stagger modifiers.
    /// </summary>
    private float ResolveDragRotationSpeed()
    {
        float baseRotationSpeed = playerVisionLight != null ? playerVisionLight.RotationSmoothing : 0f;
        float staggerMultiplier = actorStaggerController != null ? actorStaggerController.TurnSpeedMultiplier : 1f;
        return Mathf.Max(0f, baseRotationSpeed * staggerMultiplier);
    }

    /// <summary>
    /// Resolves drag movement speed to the player's slowest walk level.
    /// </summary>
    private float ResolveDragMoveSpeed()
    {
        return playerMotor != null ? playerMotor.MinWalkSpeed : 0f;
    }
}
