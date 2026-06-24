using System;
using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Breezeblocks.Missions
{

public enum ObjectiveActivationMode
{
    Instant,
    Hold
}

public enum ObjectiveActivationGate
{
    None,
    Lockpicking,
    CutWire,
    InputCode
}

[Serializable]
public sealed class ObjectiveActivationUnityEvent : UnityEvent
{
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Breezeblocks/Missions/Objective Activation Interactable")]
public sealed class ObjectiveActivationInteractable : PlayerWorldInteractable,
    IPlayerHoldInteractable,
    ILockpickSessionTarget,
    ICutWireSessionTarget,
    IInputCodeSessionTarget
{
    private const string DefaultInteractionLabel = "Ativar";
    private const string DefaultLockedInteractionLabel = "Desbloquear";
    private const float MinimumHoldDuration = 0.01f;

    [FoldoutGroup("Objective")]
    [SerializeField] private string objectiveId;

    [FoldoutGroup("Objective")]
    [SerializeField] private string interactionLabel = DefaultInteractionLabel;

    [FoldoutGroup("Activation")]
    [SerializeField] private ObjectiveActivationMode activationMode = ObjectiveActivationMode.Instant;

    [FoldoutGroup("Activation"), ShowIf(nameof(UsesHoldActivation)), MinValue(MinimumHoldDuration), SuffixLabel("s", true)]
    [SerializeField] private float holdDuration = 2f;

    [FoldoutGroup("Gate")]
    [SerializeField] private ObjectiveActivationGate activationGate = ObjectiveActivationGate.None;

    [FoldoutGroup("Gate")]
    [SerializeField] private string lockedInteractionLabel = DefaultLockedInteractionLabel;

    [FoldoutGroup("Gate"), ShowIf(nameof(UsesLockpickingGate)), AssetsOnly]
    [SerializeField] private LockpickMinigameDefinition lockpickDefinition;

    [FoldoutGroup("Gate"), ShowIf(nameof(UsesCutWireGate)), AssetsOnly]
    [SerializeField] private CutWireMinigameDefinition cutWireDefinition;

    [FoldoutGroup("Gate"), ShowIf(nameof(UsesInputCodeGate)), AssetsOnly]
    [SerializeField] private InputCodeMinigameDefinition inputCodeDefinition;

    [FoldoutGroup("Activation SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet activationSfx = new();

    [FoldoutGroup("Activation SFX"), Range(0f, 1f)]
    [SerializeField] private float activationSfxVolume = 1f;

    [FoldoutGroup("Activation Noise"), MinValue(0f)]
    [SerializeField] private float activationNoiseAmount = 0.2f;

    [FoldoutGroup("Activation Noise")]
    [SerializeField] private NoiseType activationNoiseType = NoiseType.Common;

    [FoldoutGroup("Activation Noise")]
    [SerializeField] private bool activationExtremeNoise;

    [FoldoutGroup("Events")]
    [SerializeField] private ObjectiveActivationUnityEvent onActivated = new();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsActivated => isActivated;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsGateSolved => gateSolved;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public int RemainingAttempts
    {
        get
        {
            EnsureInputCodeAttemptsInitialized();
            return inputCodeRemainingAttempts;
        }
    }

    public LockpickMinigameDefinition Definition => lockpickDefinition;
    CutWireMinigameDefinition ICutWireSessionTarget.Definition => cutWireDefinition;
    InputCodeMinigameDefinition IInputCodeSessionTarget.Definition => inputCodeDefinition;
    public IReadOnlyList<bool> CutStates => cutWireStates;

    public override string InteractionDisplayName => ResolveInteractionLabel(null);

    private readonly PlayerMinigameControlLock holdControlLock = new();
    private bool isActivated;
    private bool gateSolved;
    private bool holdActive;
    private GameObject holdInteractorRoot;
    private float holdElapsedTime;
    private bool[] cutWireStates = Array.Empty<bool>();
    private int inputCodeRemainingAttempts;
    private bool inputCodeAttemptsInitialized;
    private Collider2D interactionZoneCollider;

    /// <summary>
    /// Initializes gate state before runtime interaction begins.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        ResetGateRuntimeState();
    }

    /// <summary>
    /// Clears active hold state if this object is disabled.
    /// </summary>
    protected override void OnDisable()
    {
        CancelHold();
        base.OnDisable();
    }

    /// <summary>
    /// Normalizes authored ids, labels, and feedback settings.
    /// </summary>
    protected override void OnValidate()
    {
        base.OnValidate();
        CacheReferences();
        objectiveId = objectiveId != null ? objectiveId.Trim() : string.Empty;
        interactionLabel = string.IsNullOrWhiteSpace(interactionLabel) ? DefaultInteractionLabel : interactionLabel.Trim();
        lockedInteractionLabel = string.IsNullOrWhiteSpace(lockedInteractionLabel) ? DefaultLockedInteractionLabel : lockedInteractionLabel.Trim();
        holdDuration = Mathf.Max(MinimumHoldDuration, holdDuration);
        activationSfx ??= new AudioClipSet();
        activationSfx.Validate();
        activationSfxVolume = Mathf.Clamp01(activationSfxVolume);
        activationNoiseAmount = Mathf.Max(0f, activationNoiseAmount);
    }

    /// <summary>
    /// Returns the closest point on the activation zone so large trigger colliders can prompt correctly.
    /// </summary>
    public override Vector3 GetClosestInteractionPosition(Vector3 origin)
    {
        CacheReferences();
        if (interactionZoneCollider == null || !interactionZoneCollider.enabled)
            return base.GetClosestInteractionPosition(origin);

        return interactionZoneCollider.ClosestPoint(origin);
    }

    /// <summary>
    /// Returns whether this objective object can currently be used by the player.
    /// </summary>
    public override bool CanInteract(GameObject interactorRoot)
    {
        if (isActivated || !base.CanInteract(interactorRoot))
            return false;

        if (IsGateSatisfied())
            return true;

        return CanStartGateMinigame(interactorRoot);
    }

    /// <summary>
    /// Resolves prompt label based on activation and gate state.
    /// </summary>
    public override string GetInteractionDisplayName(GameObject interactorRoot)
    {
        return ResolveInteractionLabel(interactorRoot);
    }

    /// <summary>
    /// Routes instant interactions through the shared activation flow.
    /// </summary>
    protected override bool Interact(GameObject interactorRoot)
    {
        return TryActivateOrStartGate(interactorRoot);
    }

    /// <summary>
    /// Starts minigame gate, instant activation, or timed activation depending on current state.
    /// </summary>
    public bool TryBeginHold(GameObject interactorRoot)
    {
        if (!CanInteract(interactorRoot))
            return false;

        if (!IsGateSatisfied())
        {
            TryStartGateMinigame(interactorRoot);
            return false;
        }

        if (activationMode == ObjectiveActivationMode.Instant)
        {
            Activate(interactorRoot);
            return false;
        }

        if (holdActive)
            return true;

        holdActive = true;
        holdInteractorRoot = interactorRoot;
        holdElapsedTime = 0f;
        holdControlLock.Bind(interactorRoot);
        holdControlLock.SetBlocked(true);
        ObjectiveHoldProgressUI.ShowActive(holdDuration);
        return true;
    }

    /// <summary>
    /// Returns whether a timed activation is still active for this interactor.
    /// </summary>
    public bool IsHoldActive(GameObject interactorRoot)
    {
        return holdActive && interactorRoot != null && holdInteractorRoot == interactorRoot && !isActivated;
    }

    /// <summary>
    /// Advances held activation progress while the player keeps interacting.
    /// </summary>
    public void TickHold(GameObject interactorRoot, float deltaTime)
    {
        if (!IsHoldActive(interactorRoot))
            return;

        holdElapsedTime = Mathf.Min(holdDuration, holdElapsedTime + Mathf.Max(0f, deltaTime));
        ObjectiveHoldProgressUI.UpdateActive(holdElapsedTime, holdDuration);
        if (holdElapsedTime < holdDuration)
            return;

        Activate(interactorRoot);
        CancelHold();
    }

    /// <summary>
    /// Cancels held activation when the player releases interaction early.
    /// </summary>
    public void EndHold(GameObject interactorRoot)
    {
        if (holdInteractorRoot != interactorRoot)
            return;

        CancelHold();
    }

    /// <summary>
    /// Marks lockpicking gate solved after the lockpick minigame succeeds.
    /// </summary>
    public void NotifyUnlocked(GameObject interactorRoot)
    {
        if (activationGate != ObjectiveActivationGate.Lockpicking)
            return;

        gateSolved = true;
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Stores one cut-wire progress bit while cut-wire gate is open.
    /// </summary>
    public void NotifyWireCut(int wireIndex)
    {
        EnsureCutWireStateCapacity();
        if (wireIndex >= 0 && wireIndex < cutWireStates.Length)
            cutWireStates[wireIndex] = true;
    }

    /// <summary>
    /// Marks cut-wire or input-code gate solved after minigame success.
    /// </summary>
    public void NotifySucceeded(GameObject interactorRoot)
    {
        if (activationGate != ObjectiveActivationGate.CutWire &&
            activationGate != ObjectiveActivationGate.InputCode)
        {
            return;
        }

        gateSolved = true;
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Keeps gate unsolved after failed minigame attempts.
    /// </summary>
    public void NotifyFailed(GameObject interactorRoot)
    {
        if (activationGate == ObjectiveActivationGate.CutWire)
            EnsureCutWireStateCapacity(clearExisting: true);

        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Consumes one failed input-code attempt for this activation gate.
    /// </summary>
    public int ConsumeFailedAttempt(GameObject interactorRoot)
    {
        EnsureInputCodeAttemptsInitialized();
        inputCodeRemainingAttempts = Mathf.Max(0, inputCodeRemainingAttempts - 1);
        return inputCodeRemainingAttempts;
    }

    /// <summary>
    /// Restores this object to unactivated and locked test state.
    /// </summary>
    [Button(ButtonSizes.Small)]
    public void ResetRuntimeState()
    {
        isActivated = false;
        CancelHold();
        ResetGateRuntimeState();
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Attempts instant activation or starts the configured minigame gate.
    /// </summary>
    private bool TryActivateOrStartGate(GameObject interactorRoot)
    {
        if (!IsGateSatisfied())
            return TryStartGateMinigame(interactorRoot);

        if (activationMode == ObjectiveActivationMode.Hold)
            return false;

        Activate(interactorRoot);
        return true;
    }

    /// <summary>
    /// Applies activation once, emits feedback, and raises mission progress.
    /// </summary>
    private void Activate(GameObject interactorRoot)
    {
        if (isActivated)
            return;

        isActivated = true;
        EmitActivationFeedback(interactorRoot);
        onActivated?.Invoke();
        MissionRuntimeEvents.RaiseObjectiveObjectActivated(ResolveObjectiveId(), gameObject, interactorRoot);
        RefreshInteractionPresentation();
    }

    /// <summary>
    /// Starts the configured gate minigame when one is required.
    /// </summary>
    private bool TryStartGateMinigame(GameObject interactorRoot)
    {
        switch (activationGate)
        {
            case ObjectiveActivationGate.Lockpicking:
                if (!PlayerLockpickInventoryUtility.HasAnyLockpickUses(interactorRoot))
                {
                    RequestInteractionFeedback(LockpickMinigameController.CreateNoLockpickPromptFeedback());
                    LockpickMinigameController.PlayNoLockpickWorldFeedback(InteractionPosition, gameObject);
                    return false;
                }

                return lockpickDefinition != null && LockpickMinigameController.TryBeginActiveSession(interactorRoot, this);

            case ObjectiveActivationGate.CutWire:
                EnsureCutWireStateCapacity();
                return cutWireDefinition != null && CutWireController.TryBeginActiveSession(interactorRoot, this);

            case ObjectiveActivationGate.InputCode:
                EnsureInputCodeAttemptsInitialized();
                return inputCodeDefinition != null &&
                       inputCodeRemainingAttempts > 0 &&
                       InputCodeController.TryBeginActiveSession(interactorRoot, this);

            default:
                return false;
        }
    }

    /// <summary>
    /// Returns whether the configured gate is already solved or absent.
    /// </summary>
    private bool IsGateSatisfied()
    {
        return activationGate == ObjectiveActivationGate.None || gateSolved;
    }

    /// <summary>
    /// Returns whether the configured gate has enough runtime support to start.
    /// </summary>
    private bool CanStartGateMinigame(GameObject interactorRoot)
    {
        return activationGate switch
        {
            ObjectiveActivationGate.Lockpicking => lockpickDefinition != null &&
                                                   (!PlayerLockpickInventoryUtility.HasAnyLockpickUses(interactorRoot) ||
                                                    LockpickMinigameController.HasRegisteredInstance),
            ObjectiveActivationGate.CutWire => cutWireDefinition != null && CutWireController.HasRegisteredInstance,
            ObjectiveActivationGate.InputCode => inputCodeDefinition != null && RemainingAttempts > 0 && InputCodeController.HasRegisteredInstance,
            _ => false
        };
    }

    /// <summary>
    /// Emits authored activation SFX and AI noise.
    /// </summary>
    private void EmitActivationFeedback(GameObject interactorRoot)
    {
        Vector3 position = InteractionPosition;
        if (activationSfx != null && activationSfx.HasAnyClip)
            WorldSfxManager.Instance?.PlayClipSetAt(position, activationSfx, activationNoiseType, activationSfxVolume);

        if (activationNoiseAmount > 0f)
            NoiseManager.EmitNoise((Vector2)position, activationNoiseAmount, activationNoiseType, gameObject, activationExtremeNoise);
    }

    /// <summary>
    /// Cancels active held activation and restores player controls.
    /// </summary>
    private void CancelHold()
    {
        if (!holdActive && holdInteractorRoot == null)
            return;

        holdActive = false;
        holdElapsedTime = 0f;
        ObjectiveHoldProgressUI.HideActive();
        holdControlLock.SetBlocked(false);
        holdControlLock.Clear();
        holdInteractorRoot = null;
    }

    /// <summary>
    /// Resolves player-facing prompt label from current gate and activation state.
    /// </summary>
    private string ResolveInteractionLabel(GameObject interactorRoot)
    {
        if (!IsGateSatisfied())
        {
            if (activationGate == ObjectiveActivationGate.Lockpicking &&
                !PlayerLockpickInventoryUtility.HasAnyLockpickUses(interactorRoot))
            {
                return LockpickMinigameController.ResolveNoLockpickDisplayName(false);
            }

            return lockedInteractionLabel;
        }

        return interactionLabel;
    }

    /// <summary>
    /// Resolves stable objective id used by job objective matching.
    /// </summary>
    private string ResolveObjectiveId()
    {
        return string.IsNullOrWhiteSpace(objectiveId) ? name : objectiveId;
    }

    /// <summary>
    /// Caches the same-object collider used as the activation prompt zone.
    /// </summary>
    private void CacheReferences()
    {
        if (interactionZoneCollider == null)
            interactionZoneCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Restores minigame-gate runtime state from authored setup.
    /// </summary>
    private void ResetGateRuntimeState()
    {
        gateSolved = activationGate == ObjectiveActivationGate.None;
        EnsureCutWireStateCapacity(clearExisting: true);
        inputCodeAttemptsInitialized = false;
        EnsureInputCodeAttemptsInitialized();
    }

    /// <summary>
    /// Resizes cut-wire progress storage to match current definition.
    /// </summary>
    private void EnsureCutWireStateCapacity(bool clearExisting = false)
    {
        int targetCount = cutWireDefinition != null ? cutWireDefinition.WireCount : 0;
        if (!clearExisting && cutWireStates != null && cutWireStates.Length == targetCount)
            return;

        bool[] nextStates = new bool[targetCount];
        if (!clearExisting && cutWireStates != null)
            Array.Copy(cutWireStates, nextStates, Mathf.Min(cutWireStates.Length, nextStates.Length));

        cutWireStates = nextStates;
    }

    /// <summary>
    /// Initializes input-code attempt count from current definition once per runtime reset.
    /// </summary>
    private void EnsureInputCodeAttemptsInitialized()
    {
        if (inputCodeAttemptsInitialized)
            return;

        inputCodeRemainingAttempts = inputCodeDefinition != null ? inputCodeDefinition.MaxAttempts : 0;
        inputCodeAttemptsInitialized = true;
    }

    /// <summary>
    /// Returns whether this object uses held activation.
    /// </summary>
    private bool UsesHoldActivation()
    {
        return activationMode == ObjectiveActivationMode.Hold;
    }

    /// <summary>
    /// Returns whether this object is gated by lockpicking.
    /// </summary>
    private bool UsesLockpickingGate()
    {
        return activationGate == ObjectiveActivationGate.Lockpicking;
    }

    /// <summary>
    /// Returns whether this object is gated by cut-wire.
    /// </summary>
    private bool UsesCutWireGate()
    {
        return activationGate == ObjectiveActivationGate.CutWire;
    }

    /// <summary>
    /// Returns whether this object is gated by input-code.
    /// </summary>
    private bool UsesInputCodeGate()
    {
        return activationGate == ObjectiveActivationGate.InputCode;
    }
}

}
