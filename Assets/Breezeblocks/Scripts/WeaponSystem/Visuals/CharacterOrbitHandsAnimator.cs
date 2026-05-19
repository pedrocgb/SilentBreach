using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Visuals/Character Orbit Hands Animator")]
public class CharacterOrbitHandsAnimator : MonoBehaviour
{
    private const float MinDirectionSqr = 0.0001f;
    private const float MinTransitionDuration = 0.0001f;
    private const string AutoLeftHandName = "Auto Left Hand";
    private const string AutoRightHandName = "Auto Right Hand";
    private const string HeldItemVisualName = "Held Item Visual";

    [FoldoutGroup("References")]
    [SerializeField] private Transform bodyAnchor;

    [FoldoutGroup("References")]
    [SerializeField] private Transform leftHand;

    [FoldoutGroup("References")]
    [SerializeField] private Transform rightHand;

    [FoldoutGroup("References")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [FoldoutGroup("References")]
    [SerializeField] private SpriteRenderer leftHandRenderer;

    [FoldoutGroup("References")]
    [SerializeField] private SpriteRenderer rightHandRenderer;

    [FoldoutGroup("References")]
    [SerializeField] private SpriteRenderer heldItemRenderer;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerEquipmentController playerEquipmentController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerUtilityController playerUtilityController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerVisionLight playerVisionLight;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyCombatantAI enemyCombatantAI;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMeleeCombatantAI enemyMeleeCombatantAI;

    [FoldoutGroup("Rig"), MinValue(0f)]
    [SerializeField] private float sideOffset = 0.55f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    [SerializeField] private float locomotionSwingAmplitude = 0.28f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    [SerializeField] private float holdDistance = 0.52f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    [SerializeField] private float holdHandSeparation = 0.12f;

    [FoldoutGroup("Rig"), MinValue(0f)]
    [SerializeField] private float heldItemScale = 0.75f;

    [FoldoutGroup("Rig")]
    [SerializeField] private float heldItemRotationOffset;

    [FoldoutGroup("Rig"), MinValue(0f)]
    [SerializeField] private float autoCreatedHandScale = 0.7f;

    [FoldoutGroup("Motion"), MinValue(0f)]
    [SerializeField] private float swingCyclesPerSpeedUnit = 1.35f;

    [FoldoutGroup("Motion"), MinValue(0f)]
    [SerializeField] private float minimumMoveSpeedForSwing = 0.05f;

    [FoldoutGroup("Setup")]
    [SerializeField] private bool autoCreateMissingVisualRig = true;

    [FoldoutGroup("Debug")]
    [SerializeField] private bool debugDraw;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public EquipmentItemData DisplayedItem => displayedItem;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(0f, 1f)]
    public float HoldBlend => holdBlend;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentFacingDirection => currentFacingDirection;

    public Transform HeldItemTransform => heldItemRenderer != null ? heldItemRenderer.transform : null;

    private EquipmentItemData displayedItem;
    private EquipmentItemData observedEnemyItem;
    private Vector2 currentFacingDirection = Vector2.right;
    private float holdBlend;
    private float transitionStartBlend;
    private float transitionTargetBlend;
    private float transitionStartedAt = float.NegativeInfinity;
    private float transitionDuration;
    private float swingPhase;
    private float throwableChargeVisualProgress;
    private bool clearDisplayedItemOnTransitionComplete;
    private bool subscribedToPlayerEquipment;

    public static CharacterOrbitHandsAnimator EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        CharacterOrbitHandsAnimator animator = actorRoot.GetComponent<CharacterOrbitHandsAnimator>();
        if (animator == null)
            animator = actorRoot.AddComponent<CharacterOrbitHandsAnimator>();

        animator.ResolveReferences();
        animator.EnsureRig();
        return animator;
    }

    private void Reset()
    {
        ResolveReferences();
        EnsureRig();
        ApplyImmediateState();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureRig();
        ApplyImmediateState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureRig();
        SubscribeToPlayerEquipment();
        ApplyImmediateState();
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayerEquipment();
    }

    private void OnValidate()
    {
        sideOffset = Mathf.Max(0f, sideOffset);
        locomotionSwingAmplitude = Mathf.Max(0f, locomotionSwingAmplitude);
        holdDistance = Mathf.Max(0f, holdDistance);
        holdHandSeparation = Mathf.Max(0f, holdHandSeparation);
        heldItemScale = Mathf.Max(0f, heldItemScale);
        autoCreatedHandScale = Mathf.Max(0f, autoCreatedHandScale);
        swingCyclesPerSpeedUnit = Mathf.Max(0f, swingCyclesPerSpeedUnit);
        minimumMoveSpeedForSwing = Mathf.Max(0f, minimumMoveSpeedForSwing);

        ResolveReferences();
        EnsureRig();

        if (!Application.isPlaying)
            ApplyImmediateState();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        EnsureRig();
        SyncEnemyHeldItemState();
        UpdateHoldTransition();
        UpdateSwingPhase();
        ApplyPose();
    }

    private void ResolveReferences()
    {
        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (playerEquipmentController == null)
            playerEquipmentController = GetComponent<PlayerEquipmentController>();

        if (playerWeaponController == null)
            playerWeaponController = GetComponent<PlayerWeaponController>();

        if (playerUtilityController == null)
            playerUtilityController = GetComponent<PlayerUtilityController>();

        if (playerMeleeController == null)
            playerMeleeController = GetComponent<PlayerMeleeController>();

        if (playerVisionLight == null)
            playerVisionLight = GetComponentInChildren<PlayerVisionLight>(true);

        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (enemyCombatantAI == null)
            enemyCombatantAI = GetComponent<EnemyCombatantAI>();

        if (enemyMeleeCombatantAI == null)
            enemyMeleeCombatantAI = GetComponent<EnemyMeleeCombatantAI>();

        if (bodyAnchor == null)
            bodyAnchor = FindPreferredBodyAnchor();

        if (bodyRenderer == null && bodyAnchor != null)
            bodyRenderer = bodyAnchor.GetComponent<SpriteRenderer>();

        if (leftHand == null && bodyAnchor != null)
            leftHand = FindNamedChild(bodyAnchor, "left", "arm", "hand");

        if (rightHand == null && bodyAnchor != null)
            rightHand = FindNamedChild(bodyAnchor, "right", "arm", "hand");

        if (leftHandRenderer == null && leftHand != null)
            leftHandRenderer = leftHand.GetComponent<SpriteRenderer>();

        if (rightHandRenderer == null && rightHand != null)
            rightHandRenderer = rightHand.GetComponent<SpriteRenderer>();

        if (heldItemRenderer == null && bodyAnchor != null)
            heldItemRenderer = FindHeldItemRenderer(bodyAnchor);
    }

    private void EnsureRig()
    {
        if (!autoCreateMissingVisualRig || bodyAnchor == null)
            return;

        if (bodyRenderer == null)
            bodyRenderer = bodyAnchor.GetComponent<SpriteRenderer>();

        if (leftHand == null)
            leftHand = CreateHand(AutoLeftHandName, sideOffset);

        if (rightHand == null)
            rightHand = CreateHand(AutoRightHandName, -sideOffset);

        if (leftHand != null && leftHandRenderer == null)
            leftHandRenderer = leftHand.GetComponent<SpriteRenderer>();

        if (rightHand != null && rightHandRenderer == null)
            rightHandRenderer = rightHand.GetComponent<SpriteRenderer>();

        if (heldItemRenderer == null)
            heldItemRenderer = CreateHeldItemRenderer();

        SyncAutoCreatedHandVisual(leftHandRenderer);
        SyncAutoCreatedHandVisual(rightHandRenderer);
        SyncHeldItemRendererVisual();
    }

    private void SubscribeToPlayerEquipment()
    {
        if (subscribedToPlayerEquipment || playerEquipmentController == null)
            return;

        playerEquipmentController.HeldItemEquipping += HandlePlayerItemEquipping;
        playerEquipmentController.HeldItemHolstering += HandlePlayerItemHolstering;
        playerEquipmentController.EquipmentChanged += HandlePlayerEquipmentChanged;
        subscribedToPlayerEquipment = true;
    }

    private void UnsubscribeFromPlayerEquipment()
    {
        if (!subscribedToPlayerEquipment || playerEquipmentController == null)
            return;

        playerEquipmentController.HeldItemEquipping -= HandlePlayerItemEquipping;
        playerEquipmentController.HeldItemHolstering -= HandlePlayerItemHolstering;
        playerEquipmentController.EquipmentChanged -= HandlePlayerEquipmentChanged;
        subscribedToPlayerEquipment = false;
    }

    private void HandlePlayerItemEquipping(EquipmentItemData item, float duration)
    {
        BeginTransition(item, 1f, duration, clearItemWhenDone: false);
    }

    private void HandlePlayerItemHolstering(EquipmentItemData item, float duration)
    {
        BeginTransition(item, 0f, duration, clearItemWhenDone: true);
    }

    private void HandlePlayerEquipmentChanged()
    {
        EquipmentItemData equippedItem = ResolveImmediateHeldItem();
        if (equippedItem is not ThrowableUtilityData)
            throwableChargeVisualProgress = 0f;

        if (equippedItem != null)
        {
            displayedItem = equippedItem;
            ApplyHeldItemSprite();
            if (transitionTargetBlend >= 1f || transitionStartedAt <= float.NegativeInfinity)
            {
                holdBlend = 1f;
                transitionStartedAt = float.NegativeInfinity;
                transitionTargetBlend = 1f;
                clearDisplayedItemOnTransitionComplete = false;
            }
        }
        else if (transitionStartedAt <= float.NegativeInfinity)
        {
            holdBlend = 0f;
            transitionStartBlend = 0f;
            transitionTargetBlend = 0f;
            displayedItem = null;
            clearDisplayedItemOnTransitionComplete = false;
            ApplyHeldItemSprite();
        }
    }

    private void SyncEnemyHeldItemState()
    {
        if (enemyCombatantAI == null && enemyMeleeCombatantAI == null)
            return;

        EquipmentItemData currentEnemyItem = ResolveImmediateEnemyHeldItem();
        if (currentEnemyItem == observedEnemyItem)
            return;

        if (currentEnemyItem != null)
        {
            BeginTransition(currentEnemyItem, 1f, ResolveEquipDuration(currentEnemyItem), clearItemWhenDone: false);
        }
        else if (observedEnemyItem != null)
        {
            BeginTransition(observedEnemyItem, 0f, ResolveHolsterDuration(observedEnemyItem), clearItemWhenDone: true);
        }

        observedEnemyItem = currentEnemyItem;
    }

    private void ApplyImmediateState()
    {
        displayedItem = ResolveImmediateHeldItem();
        observedEnemyItem = ResolveImmediateEnemyHeldItem();
        holdBlend = displayedItem != null ? 1f : 0f;
        transitionStartBlend = holdBlend;
        transitionTargetBlend = holdBlend;
        transitionStartedAt = float.NegativeInfinity;
        throwableChargeVisualProgress = 0f;
        clearDisplayedItemOnTransitionComplete = false;
        currentFacingDirection = ResolveFacingDirection();
        ApplyHeldItemSprite();
        ApplyPose();
    }

    private void BeginTransition(EquipmentItemData item, float targetBlend, float duration, bool clearItemWhenDone)
    {
        displayedItem = item;
        transitionStartBlend = holdBlend;
        transitionTargetBlend = Mathf.Clamp01(targetBlend);
        transitionDuration = Mathf.Max(MinTransitionDuration, duration);
        transitionStartedAt = Time.time;
        clearDisplayedItemOnTransitionComplete = clearItemWhenDone;
        ApplyHeldItemSprite();
    }

    private void UpdateHoldTransition()
    {
        if (transitionStartedAt <= float.NegativeInfinity)
            return;

        float t = Mathf.Clamp01((Time.time - transitionStartedAt) / transitionDuration);
        holdBlend = Mathf.Lerp(transitionStartBlend, transitionTargetBlend, Mathf.SmoothStep(0f, 1f, t));
        if (t < 1f)
            return;

        transitionStartedAt = float.NegativeInfinity;
        holdBlend = transitionTargetBlend;
        if (clearDisplayedItemOnTransitionComplete && holdBlend <= 0f)
        {
            displayedItem = null;
            ApplyHeldItemSprite();
        }

        clearDisplayedItemOnTransitionComplete = false;
    }

    private void UpdateSwingPhase()
    {
        float movementSpeed = ResolveMovementSpeed(out _, out _);
        bool keepSwingWhileHoldingThrowable = displayedItem is ThrowableUtilityData;
        if (movementSpeed <= minimumMoveSpeedForSwing || (holdBlend >= 0.999f && !keepSwingWhileHoldingThrowable))
            return;

        swingPhase += Time.deltaTime * movementSpeed * swingCyclesPerSpeedUnit * Mathf.PI * 2f;
        if (swingPhase > Mathf.PI * 2f)
            swingPhase -= Mathf.PI * 2f;
    }

    private void ApplyPose()
    {
        if (bodyAnchor == null || leftHand == null || rightHand == null)
            return;

        currentFacingDirection = ResolveFacingDirection();
        if (currentFacingDirection.sqrMagnitude <= MinDirectionSqr)
            currentFacingDirection = Vector2.right;

        Vector2 localForward = bodyAnchor.InverseTransformDirection(currentFacingDirection);
        if (localForward.sqrMagnitude <= MinDirectionSqr)
            localForward = Vector2.right;

        localForward.Normalize();
        Vector2 localSide = new(-localForward.y, localForward.x);
        float moveSpeed = ResolveMovementSpeed(out float normalizedSpeed, out Vector2 movementDirection);
        float swingAmount = moveSpeed > minimumMoveSpeedForSwing
            ? Mathf.Sin(swingPhase) * Mathf.Clamp01(normalizedSpeed)
            : 0f;

        Vector3 unarmedLeft = (Vector3)(localSide * sideOffset) + (Vector3)(localForward * locomotionSwingAmplitude * swingAmount);
        Vector3 unarmedRight = (Vector3)(-localSide * sideOffset) + (Vector3)(localForward * -locomotionSwingAmplitude * swingAmount);
        float poseBlend = Mathf.Clamp01(holdBlend);

        if (displayedItem is MeleeWeaponData meleeWeaponData)
        {
            ApplyMeleePose(unarmedLeft, unarmedRight, localForward, localSide, poseBlend, meleeWeaponData);
            return;
        }

        if (displayedItem is FirearmData firearmData)
        {
            ApplyFirearmPose(unarmedLeft, unarmedRight, localForward, localSide, poseBlend, firearmData);
            return;
        }

        if (displayedItem is ThrowableUtilityData throwableData)
        {
            ApplyThrowablePose(unarmedLeft, unarmedRight, localForward, localSide, poseBlend, throwableData);
            return;
        }

        Vector3 holdCenter = (Vector3)(localForward * holdDistance);
        Vector3 holdOffset = (Vector3)(localSide * holdHandSeparation);
        Vector3 heldLeft = holdCenter + holdOffset;
        Vector3 heldRight = holdCenter - holdOffset;

        leftHand.localPosition = Vector3.Lerp(unarmedLeft, heldLeft, poseBlend);
        rightHand.localPosition = Vector3.Lerp(unarmedRight, heldRight, poseBlend);
        ApplyHeldItemVisual(holdCenter, localForward, poseBlend);
    }

    private void ApplyFirearmPose(
        Vector3 unarmedLeft,
        Vector3 unarmedRight,
        Vector2 localForward,
        Vector2 localSide,
        float poseBlend,
        FirearmData firearmData)
    {
        Vector3 holdCenter = (Vector3)(localForward * holdDistance);
        Vector3 holdOffset = (Vector3)(localSide * holdHandSeparation);
        Vector3 heldLeft = holdCenter + holdOffset;
        Vector3 heldRight = holdCenter - holdOffset;
        Vector3 heldItemCenter = holdCenter;

        if (firearmData != null && firearmData.GripType == FirearmGripType.TwoHanded)
        {
            Vector3 frontHandOffset = (Vector3)(localForward * firearmData.TwoHandedFrontHandForwardOffset);
            heldLeft += frontHandOffset;
            heldItemCenter += frontHandOffset * 0.5f;
        }

        leftHand.localPosition = Vector3.Lerp(unarmedLeft, heldLeft, poseBlend);
        rightHand.localPosition = Vector3.Lerp(unarmedRight, heldRight, poseBlend);
        ApplyHeldItemVisual(heldItemCenter, localForward, poseBlend);
    }

    private void ApplyThrowablePose(
        Vector3 unarmedLeft,
        Vector3 unarmedRight,
        Vector2 localForward,
        Vector2 localSide,
        float poseBlend,
        ThrowableUtilityData throwableData)
    {
        const float ThrowSwingPeakProgress = 0.35f;
        const float HeldItemReleaseProgress = 0.2f;

        float throwProgress = ResolveThrowableThrowProgress(throwableData);
        float clampedThrowProgress = Mathf.Clamp01(throwProgress);
        float chargeProgress = ResolveThrowableChargeVisualProgress(throwableData, clampedThrowProgress);
        float throwSwingWeight = clampedThrowProgress > 0f
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(clampedThrowProgress / ThrowSwingPeakProgress))
            : 0f;
        float throwRecoveryWeight = clampedThrowProgress > ThrowSwingPeakProgress
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((clampedThrowProgress - ThrowSwingPeakProgress) / (1f - ThrowSwingPeakProgress)))
            : 0f;

        Vector2 idleDirection = (-localSide * 0.98f) + (localForward * 0.08f);
        if (idleDirection.sqrMagnitude <= MinDirectionSqr)
            idleDirection = -localSide;

        idleDirection.Normalize();
        Vector2 chargeArcDirection = (localForward * 0.94f) + (-localSide * 0.38f);
        if (chargeArcDirection.sqrMagnitude <= MinDirectionSqr)
            chargeArcDirection = localForward;

        chargeArcDirection.Normalize();
        Vector2 preThrowDirection = Vector2.Lerp(idleDirection, chargeArcDirection, Mathf.SmoothStep(0f, 1f, chargeProgress));
        if (preThrowDirection.sqrMagnitude <= MinDirectionSqr)
            preThrowDirection = idleDirection;

        preThrowDirection.Normalize();
        Vector2 throwFrontDirection = (-localForward * 0.98f) + (-localSide * 0.18f);
        if (throwFrontDirection.sqrMagnitude <= MinDirectionSqr)
            throwFrontDirection = -localForward;

        throwFrontDirection.Normalize();
        Vector2 throwForwardDirection = Vector2.Lerp(preThrowDirection, throwFrontDirection, throwSwingWeight);
        if (throwForwardDirection.sqrMagnitude <= MinDirectionSqr)
            throwForwardDirection = throwFrontDirection;

        throwForwardDirection.Normalize();
        Vector2 throwDirection = Vector2.Lerp(throwForwardDirection, idleDirection, throwRecoveryWeight);
        if (throwDirection.sqrMagnitude <= MinDirectionSqr)
            throwDirection = idleDirection;

        throwDirection.Normalize();

        float idleRadius = Mathf.Max(0.01f, sideOffset * 0.92f);
        float chargeRadius = Mathf.Max(idleRadius, holdDistance + (sideOffset * 0.18f));
        float chargedRadius = Mathf.Lerp(idleRadius, chargeRadius, chargeProgress);
        float throwForwardRadius = holdDistance + (sideOffset * 0.32f);
        float currentRadius = Mathf.Lerp(chargedRadius, throwForwardRadius, throwSwingWeight);
        currentRadius = Mathf.Lerp(currentRadius, idleRadius, throwRecoveryWeight);

        Vector3 rightHandTarget = (Vector3)(throwDirection * currentRadius);
        leftHand.localPosition = unarmedLeft;
        rightHand.localPosition = Vector3.Lerp(unarmedRight, rightHandTarget, poseBlend);
        ApplyHeldItemVisual(rightHandTarget, throwDirection, poseBlend);

        if (heldItemRenderer != null && clampedThrowProgress >= HeldItemReleaseProgress)
        {
            SetRendererAlpha(heldItemRenderer, 0f);
            heldItemRenderer.enabled = false;
        }
    }

    private void ApplyMeleePose(
        Vector3 unarmedLeft,
        Vector3 unarmedRight,
        Vector2 localForward,
        Vector2 localSide,
        float poseBlend,
        MeleeWeaponData meleeWeaponData)
    {
        float attackWeight = ResolveMeleeAttackWeight(meleeWeaponData);

        if (meleeWeaponData.GripType == MeleeGripType.TwoHanded)
        {
            Vector2 idleDirection = (-localSide * 0.96f) + (localForward * 0.1f);
            if (idleDirection.sqrMagnitude <= MinDirectionSqr)
                idleDirection = localForward;

            idleDirection.Normalize();
            Vector2 swingDirection = Vector2.Lerp(idleDirection, localForward, attackWeight);
            if (swingDirection.sqrMagnitude <= MinDirectionSqr)
                swingDirection = idleDirection;

            swingDirection.Normalize();
            float idleRadius = Mathf.Max(0.01f, sideOffset * 0.72f);
            float attackRadius = Mathf.Max(idleRadius, holdDistance + (sideOffset * 0.5f));
            float currentRadius = Mathf.Lerp(idleRadius, attackRadius, attackWeight);
            Vector3 holdCenter = (Vector3)(swingDirection * currentRadius);
            Vector2 handNormal = new(-swingDirection.y, swingDirection.x);
            Vector3 handOffset = (Vector3)(handNormal * (holdHandSeparation * 0.85f));

            Vector3 heldLeft = holdCenter + handOffset;
            Vector3 heldRight = holdCenter - handOffset;
            leftHand.localPosition = Vector3.Lerp(unarmedLeft, heldLeft, poseBlend);
            rightHand.localPosition = Vector3.Lerp(unarmedRight, heldRight, poseBlend);
            ApplyHeldItemVisual(holdCenter, swingDirection, poseBlend);
            return;
        }

        Vector2 oneHandIdleDirection = (-localSide * 0.88f) + (localForward * 0.22f);
        if (oneHandIdleDirection.sqrMagnitude <= MinDirectionSqr)
            oneHandIdleDirection = localForward;

        oneHandIdleDirection.Normalize();
        Vector2 oneHandSwingDirection = Vector2.Lerp(oneHandIdleDirection, localForward, attackWeight);
        if (oneHandSwingDirection.sqrMagnitude <= MinDirectionSqr)
            oneHandSwingDirection = oneHandIdleDirection;

        oneHandSwingDirection.Normalize();
        float idleRadiusOneHanded = Mathf.Max(0.01f, sideOffset * 0.9f);
        float attackRadiusOneHanded = Mathf.Max(idleRadiusOneHanded, holdDistance + (sideOffset * 0.28f));
        float currentRadiusOneHanded = Mathf.Lerp(idleRadiusOneHanded, attackRadiusOneHanded, attackWeight);
        Vector3 rightHandTarget = (Vector3)(oneHandSwingDirection * currentRadiusOneHanded);
        Vector3 leftHandTarget = (Vector3)(localSide * (sideOffset * 0.74f)) + (Vector3)(localForward * (0.04f + (0.07f * attackWeight)));

        leftHand.localPosition = Vector3.Lerp(unarmedLeft, leftHandTarget, poseBlend);
        rightHand.localPosition = Vector3.Lerp(unarmedRight, rightHandTarget, poseBlend);
        ApplyHeldItemVisual(rightHandTarget, oneHandSwingDirection, poseBlend);
    }

    private void ApplyHeldItemVisual(Vector3 holdCenter, Vector2 localForward, float poseBlend)
    {
        if (heldItemRenderer == null)
            return;

        if (displayedItem == null || poseBlend <= 0f)
        {
            SetRendererAlpha(heldItemRenderer, 0f);
            heldItemRenderer.enabled = false;
            return;
        }

        heldItemRenderer.enabled = true;
        heldItemRenderer.transform.localPosition = holdCenter;
        float angle = Mathf.Atan2(localForward.y, localForward.x) * Mathf.Rad2Deg + heldItemRotationOffset;
        heldItemRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        heldItemRenderer.transform.localScale = Vector3.one * Mathf.Lerp(heldItemScale * 0.85f, heldItemScale, poseBlend);
        SetRendererAlpha(heldItemRenderer, poseBlend);
    }

    private EquipmentItemData ResolveImmediateHeldItem()
    {
        if (playerEquipmentController != null && playerEquipmentController.CurrentHeldItem != null)
            return playerEquipmentController.CurrentHeldItem;

        if (playerWeaponController != null && playerWeaponController.EquippedFirearm != null)
            return playerWeaponController.EquippedFirearm;

        if (playerUtilityController != null && playerUtilityController.EquippedUtility != null)
            return playerUtilityController.EquippedUtility;

        if (playerMeleeController != null && playerMeleeController.EquippedMeleeWeapon != null)
            return playerMeleeController.EquippedMeleeWeapon;

        if (enemyCombatantAI != null && enemyCombatantAI.EquippedFirearm != null)
            return enemyCombatantAI.EquippedFirearm;

        if (enemyMeleeCombatantAI != null && enemyMeleeCombatantAI.EquippedMeleeWeapon != null)
            return enemyMeleeCombatantAI.EquippedMeleeWeapon;

        return null;
    }

    private Vector2 ResolveFacingDirection()
    {
        if (displayedItem is ThrowableUtilityData ||
            (playerEquipmentController != null && playerEquipmentController.CurrentHeldItem is ThrowableUtilityData))
        {
            if (bodyAnchor != null)
            {
                Vector2 bodyForward = bodyAnchor.up;
                if (bodyForward.sqrMagnitude > MinDirectionSqr)
                    return bodyForward.normalized;
            }

            Vector2 transformForward = transform.up;
            if (transformForward.sqrMagnitude > MinDirectionSqr)
                return transformForward.normalized;

            return Vector2.up;
        }

        if (playerWeaponController != null &&
            playerWeaponController.EquippedFirearm != null &&
            playerWeaponController.CurrentAimDirection.sqrMagnitude > MinDirectionSqr)
            return playerWeaponController.CurrentAimDirection.normalized;

        if (enemyCombatantAI != null &&
            enemyCombatantAI.IsAiming &&
            enemyCombatantAI.CurrentAimDirection.sqrMagnitude > MinDirectionSqr)
            return enemyCombatantAI.CurrentAimDirection.normalized;

        if (playerUtilityController != null &&
            playerUtilityController.EquippedUtility != null &&
            playerUtilityController.FlashlightFacingDirection.sqrMagnitude > MinDirectionSqr)
            return playerUtilityController.FlashlightFacingDirection.normalized;

        if (playerVisionLight != null &&
            playerVisionLight.FacingDirection.sqrMagnitude > MinDirectionSqr)
            return playerVisionLight.FacingDirection.normalized;

        if (enemyMovementController != null && enemyMovementController.CurrentFacingDirection.sqrMagnitude > MinDirectionSqr)
            return enemyMovementController.CurrentFacingDirection.normalized;

        if (bodyAnchor != null)
        {
            Vector2 bodyForward = bodyAnchor.up;
            if (bodyForward.sqrMagnitude > MinDirectionSqr)
                return bodyForward.normalized;
        }

        Vector2 transformForward2 = transform.up;
        if (transformForward2.sqrMagnitude > MinDirectionSqr)
            return transformForward2.normalized;

        if (playerMotor != null && playerMotor.LastMoveDirection.sqrMagnitude > MinDirectionSqr)
            return playerMotor.LastMoveDirection.normalized;

        return Vector2.right;
    }

    private float ResolveMovementSpeed(out float normalizedSpeed, out Vector2 movementDirection)
    {
        movementDirection = Vector2.zero;
        normalizedSpeed = 0f;

        if (playerMotor != null)
        {
            float speed = playerMotor.CurrentPlanarSpeed;
            float speedCap = playerMotor.CurrentTargetSpeed > 0f ? playerMotor.CurrentTargetSpeed : playerMotor.MaxSprintSpeed;
            Vector2 velocity = playerMotor.CurrentVelocity;
            movementDirection = velocity.sqrMagnitude > MinDirectionSqr
                ? velocity.normalized
                : playerMotor.LastMoveDirection;
            normalizedSpeed = speedCap > 0f ? Mathf.Clamp01(speed / speedCap) : 0f;
            return speed;
        }

        if (enemyMovementController != null)
        {
            float speed = enemyMovementController.CurrentMovementSpeed;
            float speedCap = enemyMovementController.CurrentSpeedCap;
            movementDirection = enemyMovementController.CurrentFacingDirection;
            normalizedSpeed = speedCap > 0f ? Mathf.Clamp01(speed / speedCap) : Mathf.Clamp01(speed);
            return speed;
        }

        return 0f;
    }

    private float ResolveEquipDuration(EquipmentItemData item)
    {
        return item switch
        {
            FirearmData firearmData => firearmData.EquipTime,
            MeleeWeaponData meleeWeaponData => meleeWeaponData.EquipTime,
            UtilityItemData utilityItemData => utilityItemData.EquipTime,
            _ => 0f
        };
    }

    private float ResolveHolsterDuration(EquipmentItemData item)
    {
        return item switch
        {
            FirearmData firearmData => firearmData.HolsterTime,
            MeleeWeaponData meleeWeaponData => meleeWeaponData.HolsterTime,
            UtilityItemData utilityItemData => utilityItemData.HolsterTime,
            _ => 0f
        };
    }

    private EquipmentItemData ResolveImmediateEnemyHeldItem()
    {
        if (enemyCombatantAI != null && enemyCombatantAI.EquippedFirearm != null)
            return enemyCombatantAI.EquippedFirearm;

        if (enemyMeleeCombatantAI != null && enemyMeleeCombatantAI.EquippedMeleeWeapon != null)
            return enemyMeleeCombatantAI.EquippedMeleeWeapon;

        return null;
    }

    private float ResolveMeleeAttackProgress(MeleeWeaponData meleeWeaponData)
    {
        if (meleeWeaponData == null)
            return 0f;

        if (playerMeleeController != null &&
            playerMeleeController.EquippedMeleeWeapon == meleeWeaponData &&
            playerMeleeController.IsAttacking)
        {
            return Mathf.Clamp01(playerMeleeController.AttackProgress01);
        }

        if (enemyMeleeCombatantAI != null &&
            enemyMeleeCombatantAI.EquippedMeleeWeapon == meleeWeaponData &&
            enemyMeleeCombatantAI.IsAttacking)
        {
            return Mathf.Clamp01(enemyMeleeCombatantAI.AttackProgress01);
        }

        return 0f;
    }

    private float ResolveMeleeAttackWeight(MeleeWeaponData meleeWeaponData)
    {
        if (meleeWeaponData == null)
            return 0f;

        float attackProgress01 = ResolveMeleeAttackProgress(meleeWeaponData);
        if (attackProgress01 <= 0f)
            return 0f;

        float totalDuration = Mathf.Max(0.01f, meleeWeaponData.AttackAnimationDuration);
        float swingDuration = Mathf.Clamp(meleeWeaponData.AttackSwingDuration, 0.01f, totalDuration);
        float elapsed = Mathf.Clamp01(attackProgress01) * totalDuration;

        if (elapsed <= swingDuration)
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / swingDuration));

        float recoveryDuration = Mathf.Max(0.0001f, totalDuration - swingDuration);
        float recoveryProgress = Mathf.Clamp01((elapsed - swingDuration) / recoveryDuration);
        return Mathf.SmoothStep(1f, 0f, recoveryProgress);
    }

    private float ResolveThrowableChargeProgress(ThrowableUtilityData throwableData)
    {
        if (throwableData == null ||
            playerUtilityController == null ||
            playerUtilityController.EquippedThrowable != throwableData ||
            !playerUtilityController.IsChargingThrowable)
        {
            return 0f;
        }

        return Mathf.Clamp01(playerUtilityController.ThrowableChargeProgress01);
    }

    private float ResolveThrowableChargeVisualProgress(ThrowableUtilityData throwableData, float throwProgress)
    {
        if (throwableData == null ||
            playerUtilityController == null ||
            playerUtilityController.EquippedThrowable != throwableData)
        {
            throwableChargeVisualProgress = 0f;
            return 0f;
        }

        if (throwProgress > 0f || (playerUtilityController.IsBusy && !playerUtilityController.IsChargingThrowable))
            return Mathf.Clamp01(throwableChargeVisualProgress);

        float targetChargeProgress = ResolveThrowableChargeProgress(throwableData);
        float blendSpeed = targetChargeProgress >= throwableChargeVisualProgress ? 14f : 10f;
        throwableChargeVisualProgress = Mathf.Lerp(
            throwableChargeVisualProgress,
            targetChargeProgress,
            1f - Mathf.Exp(-blendSpeed * Time.deltaTime));

        if (Mathf.Abs(targetChargeProgress - throwableChargeVisualProgress) <= 0.001f)
            throwableChargeVisualProgress = targetChargeProgress;

        return Mathf.Clamp01(throwableChargeVisualProgress);
    }

    private float ResolveThrowableThrowProgress(ThrowableUtilityData throwableData)
    {
        if (throwableData == null ||
            playerUtilityController == null ||
            playerUtilityController.EquippedThrowable != throwableData)
        {
            return 0f;
        }

        return Mathf.Clamp01(playerUtilityController.ThrowableThrowProgress01);
    }

    private Transform FindPreferredBodyAnchor()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        Transform fallback = transform;
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];
            if (child == null || child == transform)
                continue;

            if (!child.name.Contains("Gfx", StringComparison.OrdinalIgnoreCase))
                continue;

            if (child.GetComponent<SpriteRenderer>() != null)
                return child;

            fallback = child;
        }

        if (fallback != transform)
            return fallback;

        SpriteRenderer childRenderer = GetComponentInChildren<SpriteRenderer>(true);
        return childRenderer != null ? childRenderer.transform : transform;
    }

    private static Transform FindNamedChild(Transform root, string requiredToken, string firstOptionalToken, string secondOptionalToken)
    {
        if (root == null)
            return null;

        Transform[] childTransforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];
            if (child == null || child == root)
                continue;

            string childName = child.name;
            if (!childName.Contains(requiredToken, StringComparison.OrdinalIgnoreCase))
                continue;

            if (childName.Contains(firstOptionalToken, StringComparison.OrdinalIgnoreCase) ||
                childName.Contains(secondOptionalToken, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private static SpriteRenderer FindHeldItemRenderer(Transform root)
    {
        if (root == null)
            return null;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.transform == root)
                continue;

            if (renderer.name.Contains(HeldItemVisualName, StringComparison.OrdinalIgnoreCase))
                return renderer;
        }

        return null;
    }

    private Transform CreateHand(string handName, float defaultX)
    {
        GameObject handObject = new(handName);
        handObject.transform.SetParent(bodyAnchor, false);
        handObject.transform.localPosition = new Vector3(defaultX, 0f, 0f);
        handObject.transform.localRotation = Quaternion.identity;
        handObject.transform.localScale = Vector3.one * autoCreatedHandScale;

        SpriteRenderer renderer = handObject.AddComponent<SpriteRenderer>();
        ApplyRendererStyle(renderer, useBodySprite: true, sortingOrderOffset: -1);
        return handObject.transform;
    }

    private SpriteRenderer CreateHeldItemRenderer()
    {
        GameObject visualObject = new(HeldItemVisualName);
        visualObject.transform.SetParent(bodyAnchor, false);
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = Vector3.one * heldItemScale;

        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        ApplyRendererStyle(renderer, useBodySprite: false, sortingOrderOffset: 1);
        renderer.enabled = false;
        renderer.sprite = null;
        return renderer;
    }

    private void SyncAutoCreatedHandVisual(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        if (renderer.gameObject.name != AutoLeftHandName && renderer.gameObject.name != AutoRightHandName)
            return;

        ApplyRendererStyle(renderer, useBodySprite: true, sortingOrderOffset: -1);
    }

    private void SyncHeldItemRendererVisual()
    {
        if (heldItemRenderer == null || heldItemRenderer.gameObject.name != HeldItemVisualName)
            return;

        ApplyRendererStyle(heldItemRenderer, useBodySprite: false, sortingOrderOffset: 1);
    }

    private void ApplyRendererStyle(SpriteRenderer renderer, bool useBodySprite, int sortingOrderOffset)
    {
        if (renderer == null)
            return;

        if (bodyRenderer != null)
        {
            renderer.sortingLayerID = bodyRenderer.sortingLayerID;
            renderer.sortingOrder = bodyRenderer.sortingOrder + sortingOrderOffset;
            renderer.sharedMaterial = bodyRenderer.sharedMaterial;
            if (useBodySprite)
            {
                renderer.sprite = bodyRenderer.sprite;
                renderer.color = bodyRenderer.color;
            }
            else
            {
                renderer.color = Color.white;
            }
        }

        renderer.maskInteraction = SpriteMaskInteraction.None;
    }

    private void ApplyHeldItemSprite()
    {
        if (heldItemRenderer == null)
            return;

        heldItemRenderer.sprite = displayedItem != null ? displayedItem.HeldVisualSprite : null;
        heldItemRenderer.enabled = displayedItem != null;
        if (displayedItem == null)
            SetRendererAlpha(heldItemRenderer, 0f);
    }

    private static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
            return;

        Color color = renderer.color;
        color.a = Mathf.Clamp01(alpha);
        renderer.color = color;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDraw || bodyAnchor == null)
            return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);
        Gizmos.DrawLine(bodyAnchor.position, bodyAnchor.position + (Vector3)(currentFacingDirection.normalized * holdDistance));
    }
}
}
