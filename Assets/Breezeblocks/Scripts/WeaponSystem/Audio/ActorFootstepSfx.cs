using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Audio/Actor Footstep SFX")]
public class ActorFootstepSfx : MonoBehaviour
{
    private const float MinimumSpeed = 0.01f;

    [FoldoutGroup("References")]
    [SerializeField] private Transform emitOrigin;

    [FoldoutGroup("References")]
    [SerializeField] private Rigidbody2D movementBody;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("References")]
    [SerializeField] private EnemyMovementController enemyMovementController;

    [FoldoutGroup("References")]
    [SerializeField] private WorldSfxManager worldSfxManager;

    [FoldoutGroup("SFX"), InlineProperty, HideLabel]
    [SerializeField] private AudioClipSet footstepSfx = new();

    [FoldoutGroup("SFX"), EnumToggleButtons]
    [SerializeField] private NoiseType footstepSoundType = NoiseType.Common;

    [FoldoutGroup("Timing"), MinValue(0f)]
    [SerializeField] private float minSpeedThreshold = 0.2f;

    [FoldoutGroup("Timing"), MinValue(MinimumSpeed)]
    [SerializeField] private float speedForFastestStep = 5f;

    [FoldoutGroup("Timing"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float slowStepInterval = 0.5f;

    [FoldoutGroup("Timing"), MinValue(0.01f), SuffixLabel("s", true)]
    [SerializeField] private float fastStepInterval = 0.22f;

    [FoldoutGroup("Mix"), Range(0f, 1f)]
    [SerializeField] private float minimumVolumeMultiplier = 0.7f;

    [FoldoutGroup("Mix"), Range(0f, 2f)]
    [SerializeField] private float maximumVolumeMultiplier = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentObservedSpeed => ResolveCurrentSpeed();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float StepTimerRemaining => Mathf.Max(0f, nextStepTime - Time.time);

    private float nextStepTime;

    private void Reset()
    {
        movementBody = GetComponent<Rigidbody2D>();
        playerMotor = GetComponent<PlayerTopDownMotor2D>();
        enemyMovementController = GetComponent<EnemyMovementController>();
        emitOrigin = transform;
    }

    private void Awake()
    {
        if (movementBody == null)
            movementBody = GetComponent<Rigidbody2D>();

        if (playerMotor == null)
            playerMotor = GetComponent<PlayerTopDownMotor2D>();

        if (enemyMovementController == null)
            enemyMovementController = GetComponent<EnemyMovementController>();

        if (emitOrigin == null)
            emitOrigin = transform;

        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        footstepSfx ??= new AudioClipSet();
        footstepSfx.Validate();
    }

    private void OnValidate()
    {
        minSpeedThreshold = Mathf.Max(0f, minSpeedThreshold);
        speedForFastestStep = Mathf.Max(MinimumSpeed, speedForFastestStep);
        slowStepInterval = Mathf.Max(0.01f, slowStepInterval);
        fastStepInterval = Mathf.Max(0.01f, fastStepInterval);
        maximumVolumeMultiplier = Mathf.Max(0f, maximumVolumeMultiplier);
        minimumVolumeMultiplier = Mathf.Clamp(minimumVolumeMultiplier, 0f, maximumVolumeMultiplier);

        footstepSfx ??= new AudioClipSet();
        footstepSfx.Validate();
    }

    private void Update()
    {
        if (worldSfxManager == null)
            worldSfxManager = WorldSfxManager.Instance;

        float currentSpeed = ResolveCurrentSpeed();
        if (currentSpeed < minSpeedThreshold || footstepSfx == null || !footstepSfx.HasAnyClip)
        {
            nextStepTime = Time.time;
            return;
        }

        if (Time.time < nextStepTime)
            return;

        float speedRatio = Mathf.Clamp01(currentSpeed / Mathf.Max(MinimumSpeed, speedForFastestStep));
        float interval = Mathf.Lerp(slowStepInterval, fastStepInterval, speedRatio);
        float volumeMultiplier = Mathf.Lerp(minimumVolumeMultiplier, maximumVolumeMultiplier, speedRatio);

        Vector3 position = emitOrigin != null ? emitOrigin.position : transform.position;
        worldSfxManager?.PlayClipSetAt(position, footstepSfx, footstepSoundType, volumeMultiplier);
        nextStepTime = Time.time + interval;
    }

    private float ResolveCurrentSpeed()
    {
        if (playerMotor != null)
            return playerMotor.CurrentPlanarSpeed;

        if (enemyMovementController != null)
            return enemyMovementController.CurrentMovementSpeed;

        if (movementBody != null)
            return movementBody.linearVelocity.magnitude;

        return 0f;
    }
}
}
