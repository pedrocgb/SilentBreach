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

    [FoldoutGroup("References")]
    [SerializeField] private Transform dragOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("References")]
    [SerializeField] private AudioClipSet dragMovementSfx;

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

    [FoldoutGroup("Drag"), MinValue(0f)]
    [SerializeField] private float dragDistance = 0.7f;

    [FoldoutGroup("Drag"), MinValue(0f)]
    [SerializeField] private float dragVerticalOffset = 0f;

    [FoldoutGroup("Drag"), MinValue(MinimumDragFollowSpeed)]
    [SerializeField] private float dragFollowSpeed = 5f;

    [FoldoutGroup("Drag"), MinValue(0f)]
    [SerializeField] private float movingBodyThreshold = 0.05f;

    [FoldoutGroup("Drag Noise"), MinValue(MinimumDragNoiseInterval)]
    [SerializeField] private float dragNoiseInterval = 0.28f;

    [FoldoutGroup("Drag Noise"), MinValue(0f)]
    [SerializeField] private float dragNoiseIntensity = 0.35f;

    [FoldoutGroup("Drag Noise")]
    [SerializeField] private NoiseType dragNoiseType = NoiseType.Common;

    [FoldoutGroup("Drag Noise")]
    [SerializeField] private bool dragNoiseExtreme;

    [FoldoutGroup("Drag Noise"), MinValue(0f)]
    [SerializeField] private float dragSfxVolumeMultiplier = 1f;

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
        CacheReferences();
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
        playerMotor?.SetExternalSpeedMultiplier(ResolveDragSpeedMultiplier());
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
        playerMotor?.SetExternalSpeedMultiplier(1f);
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
    /// Resolves the dragged body follow point behind the player's current movement direction.
    /// </summary>
    private Vector2 ResolveDraggedBodyTargetPosition()
    {
        Transform originTransform = dragOrigin != null ? dragOrigin : transform;
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
    /// Resolves the drag slowdown multiplier from global settings.
    /// </summary>
    private static float ResolveDragSpeedMultiplier()
    {
        if (GlobalSettings.Instance == null)
            return 1f;

        float slowPercent = Mathf.Clamp(GlobalSettings.Instance.DragSlowPercentage, 0f, 100f);
        return Mathf.Clamp01(1f - (slowPercent / 100f));
    }
}
