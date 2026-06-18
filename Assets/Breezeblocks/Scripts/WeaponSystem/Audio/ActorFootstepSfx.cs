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

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private Rigidbody2D movementBody;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerTopDownMotor2D playerMotor;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private EnemyMovementController enemyMovementController;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentObservedSpeed => ResolveCurrentSpeed();

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float StepTimerRemaining => Mathf.Max(0f, nextStepTime - Time.time);

    private float nextStepTime;
    private WorldSfxManager worldSfxManager;
    private AudioClipSet footstepSfx = new();
    private NoiseType footstepSoundType = NoiseType.Common;
    private float minSpeedThreshold = 0.2f;
    private float speedForFastestStep = 5f;
    private float slowStepInterval = 0.5f;
    private float fastStepInterval = 0.22f;
    private float minimumVolumeMultiplier = 0.7f;
    private float maximumVolumeMultiplier = 1f;

    /// <summary>
    /// Caches local references when the component is reset in the editor.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
        emitOrigin = transform;
    }

    /// <summary>
    /// Caches runtime references before footstep playback begins.
    /// </summary>
    private void Awake()
    {
        CacheReferences();

        if (emitOrigin == null)
            emitOrigin = transform;

        footstepSfx ??= new AudioClipSet();
        ClampSettings();
    }

    /// <summary>
    /// Keeps profile-applied values safe while editing component references.
    /// </summary>
    private void OnValidate()
    {
        CacheReferences();
        ClampSettings();
    }

    /// <summary>
    /// Emits footstep SFX at an interval based on the actor's current movement speed.
    /// </summary>
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

    /// <summary>
    /// Applies profile-authored footstep SFX settings shared by player and enemy actors.
    /// </summary>
    public void ApplySettings(ActorFootstepSfxSettings settings)
    {
        if (settings == null)
            return;

        footstepSfx = settings.FootstepSfx ?? new AudioClipSet();
        footstepSoundType = settings.FootstepSoundType;
        minSpeedThreshold = settings.MinSpeedThreshold;
        speedForFastestStep = settings.SpeedForFastestStep;
        slowStepInterval = settings.SlowStepInterval;
        fastStepInterval = settings.FastStepInterval;
        minimumVolumeMultiplier = settings.MinimumVolumeMultiplier;
        maximumVolumeMultiplier = settings.MaximumVolumeMultiplier;
        ClampSettings();
    }

    /// <summary>
    /// Caches same-object movement components used to resolve current actor speed.
    /// </summary>
    private void CacheReferences()
    {
        movementBody = GetComponent<Rigidbody2D>();
        playerMotor = GetComponent<PlayerTopDownMotor2D>();
        enemyMovementController = GetComponent<EnemyMovementController>();
    }

    /// <summary>
    /// Clamps profile-applied footstep values to safe runtime ranges.
    /// </summary>
    private void ClampSettings()
    {
        footstepSfx ??= new AudioClipSet();
        footstepSfx.Validate();
        minSpeedThreshold = Mathf.Max(0f, minSpeedThreshold);
        speedForFastestStep = Mathf.Max(MinimumSpeed, speedForFastestStep);
        slowStepInterval = Mathf.Max(0.01f, slowStepInterval);
        fastStepInterval = Mathf.Max(0.01f, fastStepInterval);
        maximumVolumeMultiplier = Mathf.Max(0f, maximumVolumeMultiplier);
        minimumVolumeMultiplier = Mathf.Clamp(minimumVolumeMultiplier, 0f, maximumVolumeMultiplier);
    }

    /// <summary>
    /// Resolves speed from the active player, enemy, or Rigidbody2D movement source.
    /// </summary>
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
