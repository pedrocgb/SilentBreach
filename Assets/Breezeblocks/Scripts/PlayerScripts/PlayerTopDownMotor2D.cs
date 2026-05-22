using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
[AddComponentMenu("Breezeblocks/Player Top Down Motor 2D")]
public class PlayerTopDownMotor2D : MonoBehaviour
{
    private const int SpeedLevelsCount = 10;
    private const float MinInputSqr = 0.0001f;
    private const float MinScrollDelta = 0.01f;
    private const bool DefaultSprintToggleMode = false;

    [FoldoutGroup("Rewired"), MinValue(0)]
    [Tooltip("Rewired Player id to read inputs from.")]
    [SerializeField] private int rewiredPlayerId;

    [FoldoutGroup("Rewired"), Tooltip("Movement axis action name.")]
    [SerializeField] private string moveHorizontalAction = "Move Horizontal";

    [FoldoutGroup("Rewired"), Tooltip("Movement axis action name.")]
    [SerializeField] private string moveVerticalAction = "Move Vertical";

    [FoldoutGroup("Rewired"), Tooltip("Button action used while held to sprint (walk max speed).")]
    [SerializeField] private string sprintAction = "Sprint";

    [FoldoutGroup("Rewired"), Tooltip("Button action used to toggle speed level between 1 and 10.")]
    [SerializeField] private string toggleMinMaxSpeedAction = "Toggle Speed MinMax";

    [FoldoutGroup("Rewired"), Tooltip("Axis action used for mouse wheel speed stepping.")]
    [SerializeField] private string mouseWheelAxisAction = "Mouse Wheel";

    private float[] walkSpeedLevels = new float[SpeedLevelsCount]
    {
        1.0f, 1.2f, 1.4f, 1.6f, 1.8f, 2.0f, 2.2f, 2.4f, 2.6f, 2.8f
    };

    private int selectedSpeedLevel = 1;

    private float acceleration = 28f;

    private float deceleration = 34f;

    private float sprintSpeedMultiplier = 1.5f;

    private bool normalizeInput = true;

    [FoldoutGroup("Physics"), Tooltip("Optional override. If empty, uses Rigidbody2D on this object.")]
    [SerializeField] private Rigidbody2D movementBody;

    private bool forceZeroGravity = true;

    private bool freezeRotationZ = true;

    [FoldoutGroup("UI"), Tooltip("Image fillAmount will represent current effective speed in runtime.")]
    [SerializeField] private Image velocityFillImage;

    [FoldoutGroup("UI"), Tooltip("If true, fill is based on level (1-10). If false, based on absolute speed.")]
    [SerializeField] private bool fillByLevel = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 MoveInput { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 CurrentVelocity => movementBody != null ? movementBody.linearVelocity : Vector2.zero;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentPlanarSpeed => CurrentVelocity.magnitude;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Vector2 LastMoveDirection { get; private set; } = Vector2.right;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSprinting { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool SprintRequested => sprintRequested;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsInputBlocked => inputBlocked;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasMovementInput => MoveInput.sqrMagnitude > MinInputSqr;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, PropertyRange(1, SpeedLevelsCount)]
    public int EffectiveSpeedLevel => IsSprinting ? SpeedLevelsCount : selectedSpeedLevel;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentTargetSpeed { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float MinWalkSpeed => walkSpeedLevels[0];

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float MaxWalkSpeed => walkSpeedLevels[SpeedLevelsCount - 1];

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float MaxSprintSpeed => MaxWalkSpeed * sprintSpeedMultiplier;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentMotionRatio => CurrentTargetSpeed <= 0f
        ? 0f
        : Mathf.Clamp01(CurrentPlanarSpeed / CurrentTargetSpeed);

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool HasExternalSpeedOverride => hasExternalSpeedOverride;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSpeedSelectionLocked => speedSelectionLockedExternally || hasExternalSpeedOverride;

    private Player _player;
    private Vector2 _targetVelocity;
    private bool _sprintToggleState;
    private bool hasExternalSpeedOverride;
    private float externalSpeedOverride;
    private bool speedSelectionLockedExternally;
    private bool sprintBlockedExternally;
    private bool inputBlocked;
    private bool sprintRequested;

    private void Reset()
    {
        movementBody = GetComponent<Rigidbody2D>();
        EnsureCoverUser();
        EnsureSpeedArrays();
        ApplyPhysicsDefaults();
    }

    private void Awake()
    {
        if (movementBody == null)
            movementBody = GetComponent<Rigidbody2D>();

        EnsureCoverUser();
        EnsureSpeedArrays();
        ApplyPhysicsDefaults();
        ResolveRewiredPlayer();
        RefreshUi();
    }

    private void OnValidate()
    {
        EnsureSpeedArrays();

        selectedSpeedLevel = Mathf.Clamp(selectedSpeedLevel, 1, SpeedLevelsCount);
        acceleration = Mathf.Max(0f, acceleration);
        deceleration = Mathf.Max(0f, deceleration);
        sprintSpeedMultiplier = Mathf.Max(0f, sprintSpeedMultiplier);

        ClampNonNegative(walkSpeedLevels);

        if (!Application.isPlaying && movementBody != null)
        {
            ApplyPhysicsDefaults();
            RefreshUi();
        }
    }

    private void Update()
    {
        if (_player == null && !ResolveRewiredPlayer())
            return;

        if (inputBlocked)
        {
            MoveInput = Vector2.zero;
            IsSprinting = false;
            sprintRequested = false;
            CurrentTargetSpeed = 0f;
            _targetVelocity = Vector2.zero;
            RefreshUi();
            return;
        }

        HandleSpeedLevelInput();

        float moveX = _player.GetAxis(moveHorizontalAction);
        float moveY = _player.GetAxis(moveVerticalAction);
        Vector2 move = new Vector2(moveX, moveY);

        if (normalizeInput && move.sqrMagnitude > 1f)
            move.Normalize();

        MoveInput = move;

        UpdateSprintState();

        int speedLevelIndex = EffectiveSpeedLevel - 1;
        CurrentTargetSpeed = GetTargetSpeed(speedLevelIndex);
        _targetVelocity = move * CurrentTargetSpeed;

        if (move.sqrMagnitude > MinInputSqr)
            LastMoveDirection = move.normalized;

        RefreshUi();
    }

    private void FixedUpdate()
    {
        if (movementBody == null)
            return;

        float changeRate = _targetVelocity.sqrMagnitude > MinInputSqr ? acceleration : deceleration;
        Vector2 nextVelocity = Vector2.MoveTowards(movementBody.linearVelocity, _targetVelocity, changeRate * Time.fixedDeltaTime);
        movementBody.linearVelocity = nextVelocity;
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void SetSpeedLevelToMin()
    {
        selectedSpeedLevel = 1;
        RefreshUi();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void SetSpeedLevelToMax()
    {
        selectedSpeedLevel = SpeedLevelsCount;
        RefreshUi();
    }

    public void SetExternalSpeedOverride(bool enabled, float speed, bool lockSpeedSelection)
    {
        hasExternalSpeedOverride = enabled;
        externalSpeedOverride = Mathf.Max(0f, speed);
        speedSelectionLockedExternally = enabled && lockSpeedSelection;

        if (enabled)
            _sprintToggleState = false;

        RefreshUi();
    }

    public void SetInputBlocked(bool blocked)
    {
        inputBlocked = blocked;
        if (!blocked)
            return;

        _sprintToggleState = false;
        MoveInput = Vector2.zero;
        IsSprinting = false;
        sprintRequested = false;
        CurrentTargetSpeed = 0f;
        _targetVelocity = Vector2.zero;

        if (movementBody != null)
            movementBody.linearVelocity = Vector2.zero;

        RefreshUi();
    }

    public void ApplySettings(PlayerMovementSettings settings)
    {
        if (settings == null)
            return;

        if (movementBody == null)
            movementBody = GetComponent<Rigidbody2D>();

        walkSpeedLevels = ActorProfileDataUtility.CloneFloatArray(settings.WalkSpeedLevels);
        EnsureSpeedArrays();

        selectedSpeedLevel = Mathf.Clamp(settings.SelectedSpeedLevel, 1, SpeedLevelsCount);
        acceleration = Mathf.Max(0f, settings.Acceleration);
        deceleration = Mathf.Max(0f, settings.Deceleration);
        sprintSpeedMultiplier = Mathf.Max(0f, settings.SprintSpeedMultiplier);
        normalizeInput = settings.NormalizeInput;
        forceZeroGravity = settings.ForceZeroGravity;
        freezeRotationZ = settings.FreezeRotationZ;

        ClampNonNegative(walkSpeedLevels);
        ApplyPhysicsDefaults();
        RefreshUi();
    }

    public void SetSprintBlocked(bool blocked)
    {
        sprintBlockedExternally = blocked;
        if (blocked)
            _sprintToggleState = false;
    }

    private void HandleSpeedLevelInput()
    {
        if (IsSpeedSelectionLocked)
            return;

        float scroll = _player.GetAxis(mouseWheelAxisAction);
        if (scroll > MinScrollDelta)
            selectedSpeedLevel = Mathf.Min(SpeedLevelsCount, selectedSpeedLevel + 1);
        else if (scroll < -MinScrollDelta)
            selectedSpeedLevel = Mathf.Max(1, selectedSpeedLevel - 1);

        if (_player.GetButtonDown(toggleMinMaxSpeedAction))
        {
            selectedSpeedLevel = selectedSpeedLevel <= 1 ? SpeedLevelsCount : 1;
        }
    }

    private float GetTargetSpeed(int levelIndex)
    {
        if (hasExternalSpeedOverride)
            return externalSpeedOverride;

        if (IsSprinting)
            return walkSpeedLevels[SpeedLevelsCount - 1] * sprintSpeedMultiplier;

        return walkSpeedLevels[levelIndex];
    }

    private void UpdateSprintState()
    {
        bool sprintToggleMode = ReadSprintToggleMode();

        if (sprintToggleMode && _player.GetButtonDown(sprintAction))
            _sprintToggleState = !_sprintToggleState;

        bool requestedByInput = sprintToggleMode ? _sprintToggleState : _player.GetButton(sprintAction);
        sprintRequested = requestedByInput;
        bool resolvedSprintRequested = requestedByInput;

        if (sprintBlockedExternally || hasExternalSpeedOverride)
        {
            resolvedSprintRequested = false;
            _sprintToggleState = false;
        }

        if (resolvedSprintRequested && selectedSpeedLevel < SpeedLevelsCount)
            selectedSpeedLevel = SpeedLevelsCount;

        IsSprinting = resolvedSprintRequested;

        if (!sprintToggleMode)
            _sprintToggleState = false;
    }

    private static bool ReadSprintToggleMode()
    {
        return GlobalSettings.Instance != null
            ? GlobalSettings.Instance.SprintToggleEnabled
            : DefaultSprintToggleMode;
    }

    private bool ResolveRewiredPlayer()
    {
        if (!ReInput.isReady)
            return false;

        _player = ReInput.players.GetPlayer(rewiredPlayerId);
        return _player != null;
    }

    private void ApplyPhysicsDefaults()
    {
        if (movementBody == null)
            return;

        if (forceZeroGravity)
            movementBody.gravityScale = 0f;

        if (freezeRotationZ)
            movementBody.freezeRotation = true;
    }

    private void EnsureSpeedArrays()
    {
        walkSpeedLevels = EnsureArraySize(walkSpeedLevels, SpeedLevelsCount, 1f, 0.2f);
    }

    private static float[] EnsureArraySize(float[] source, int size, float startValue, float step)
    {
        if (source != null && source.Length == size)
            return source;

        float[] resized = new float[size];
        for (int i = 0; i < size; i++)
        {
            if (source != null && i < source.Length)
                resized[i] = source[i];
            else
                resized[i] = startValue + (step * i);
        }

        return resized;
    }

    private static void ClampNonNegative(float[] values)
    {
        if (values == null)
            return;

        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Max(0f, values[i]);
    }

    private void EnsureCoverUser()
    {
        if (GetComponent<CoverUser2D>() == null)
            gameObject.AddComponent<CoverUser2D>();
    }

    private void RefreshUi()
    {
        if (velocityFillImage != null)
            velocityFillImage.fillAmount = CalculateVelocityFill();
    }

    private float CalculateVelocityFill()
    {
        float minSpeed = walkSpeedLevels[0];
        float maxSpeed = MaxSprintSpeed;
        if (maxSpeed <= minSpeed)
            return 0f;

        if (hasExternalSpeedOverride)
            return Mathf.InverseLerp(minSpeed, maxSpeed, externalSpeedOverride);

        if (fillByLevel)
            return EffectiveSpeedLevel / (float)SpeedLevelsCount;

        return Mathf.InverseLerp(minSpeed, maxSpeed, CurrentTargetSpeed);
    }
}
