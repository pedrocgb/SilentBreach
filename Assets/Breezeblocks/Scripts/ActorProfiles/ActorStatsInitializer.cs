using Breezeblocks.WeaponSystem;
using Breezeblocks.Missions;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
[AddComponentMenu("Breezeblocks/Actor Profiles/Actor Stats Initializer")]
public class ActorStatsInitializer : MonoBehaviour
{
    [SerializeField] private PlayerStatsProfile playerProfile;
    [SerializeField] private EnemyStatsProfile enemyProfile;

    public PlayerStatsProfile PlayerProfile => playerProfile;
    public EnemyStatsProfile EnemyProfile => enemyProfile;

    /// <summary>
    /// Applies any configured actor profile before the actor's normal startup logic runs.
    /// </summary>
    private void Awake()
    {
        ApplyProfiles();
    }

    /// <summary>
    /// Applies the assigned player or enemy profile to the matching components on this actor.
    /// </summary>
    [ContextMenu("Apply Profiles")]
    public void ApplyProfiles()
    {
        if (playerProfile != null)
            ApplyPlayerProfile();

        if (enemyProfile != null)
            ApplyEnemyProfile();
    }

    /// <summary>
    /// Refreshes editor-time profile values without changing runtime instances during play mode.
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ApplyProfiles();
    }

    /// <summary>
    /// Pushes the player profile settings into every supported player component on this actor.
    /// </summary>
    private void ApplyPlayerProfile()
    {
        if (TryGetComponent(out ActorHealth health))
            health.ApplySettings(playerProfile.Health);

        if (TryGetComponent(out ActorIncapacitationController incapacitationController))
            incapacitationController.ApplySettings(playerProfile.Health);

        if (TryGetComponent(out ActorStaggerController staggerController))
            staggerController.ApplySettings(playerProfile.Stagger);

        if (TryGetComponent(out PlayerStaggerFeedback staggerFeedback))
            staggerFeedback.ApplySettings(playerProfile.StaggerFeedback);

        if (TryGetComponent(out PlayerTopDownMotor2D playerMotor))
        {
            playerMotor.ApplyControls(playerProfile.Controls);
            playerMotor.ApplySettings(playerProfile.Movement);
        }

        if (TryGetComponent(out PlayerNoise playerNoise))
            playerNoise.ApplySettings(playerProfile.Noise);

        if (TryGetComponent(out PlayerNoiseEmitter noiseEmitter))
            noiseEmitter.ApplySettings(playerProfile.NoiseEmitter);

        if (TryGetComponent(out PlayerVisibility visibility))
            visibility.ApplySettings(playerProfile.Visibility);

        PlayerVisionLight visionLight = GetComponentInChildren<PlayerVisionLight>(true);
        visionLight?.ApplySettings(playerProfile.VisionLight);

        if (TryGetComponent(out PlayerStaminaController staminaController))
            staminaController.ApplySettings(playerProfile.Stamina);

        if (TryGetComponent(out PlayerEquipmentController equipmentController))
        {
            equipmentController.ApplyControls(playerProfile.Controls);
            equipmentController.ApplySettings(playerProfile.Equipment);
            equipmentController.ApplyUnarmedAimSettings(playerProfile.VisionLight.UnarmedAimRotationSpeed, playerProfile.VisionLight.UnarmedAimPanDistance);
        }

        if (TryGetComponent(out PlayerFocusController focusController))
        {
            focusController.ApplyControls(playerProfile.Controls);
            focusController.ApplySettings(playerProfile.Focus);
        }

        if (TryGetComponent(out PlayerWeaponController weaponController))
        {
            weaponController.ApplyControls(playerProfile.Controls);
            weaponController.ApplySettings(playerProfile.Weapon);
        }

        if (TryGetComponent(out PlayerMeleeController meleeController))
            meleeController.ApplyControls(playerProfile.Controls);

        if (TryGetComponent(out PlayerUtilityController utilityController))
            utilityController.ApplyControls(playerProfile.Controls);

        if (TryGetComponent(out PlayerPickupInteractor pickupInteractor))
        {
            pickupInteractor.ApplyControls(playerProfile.Controls);
            pickupInteractor.ApplySettings(playerProfile.Interaction);
        }

        if (TryGetComponent(out PlayerBodyDragController bodyDragController))
            bodyDragController.ApplySettings(playerProfile.BodyDrag);

        if (TryGetComponent(out CharacterOrbitHandsAnimator handsAnimator))
            handsAnimator.ApplySettings(playerProfile.Hands);

        if (TryGetComponent(out ActorFootstepSfx footstepSfx))
            footstepSfx.ApplySettings(playerProfile.Footsteps);
    }

    /// <summary>
    /// Ensures enemy helper components exist and pushes profile settings into supported enemy systems.
    /// </summary>
    private void ApplyEnemyProfile()
    {
        MissionActorIdentity identity = MissionActorIdentity.EnsureOn(gameObject);
        identity?.ApplySettings(enemyProfile.Identity);

        ActorIncapacitationController.EnsureOn(gameObject);
        if (TryGetComponent(out ActorIncapacitationController incapacitationController))
            incapacitationController.ApplySettings(enemyProfile.Health);

        EnemyAiSensesDebugGizmos.EnsureOn(gameObject);
        EnemyRoomAwareness roomAwareness = EnemyRoomAwareness.EnsureOn(gameObject);
        roomAwareness?.ApplySettings(enemyProfile.RoomAwareness);

        if (TryGetComponent(out ActorHealth health))
            health.ApplySettings(enemyProfile.Health);

        if (TryGetComponent(out ActorStaggerController staggerController))
            staggerController.ApplySettings(enemyProfile.Stagger);

        if (TryGetComponent(out EnemyMovementController movementController))
        {
            movementController.ApplySettings(enemyProfile.Movement);
            movementController.ApplyDoorBellReactionSettings(enemyProfile.DoorBellReaction);
        }

        if (TryGetComponent(out EnemyVisionAI visionAI))
            visionAI.ApplySettings(enemyProfile.Vision);

        if (TryGetComponent(out AIHearing hearing))
            hearing.ApplySettings(enemyProfile.Hearing);

        if (TryGetComponent(out EnemyConfusedReactionIndicator confusedReactionIndicator))
            confusedReactionIndicator.ApplySettings(enemyProfile.ConfusedReaction);

        if (TryGetComponent(out EnemySleepController sleepController))
            sleepController.ApplySettings(enemyProfile.Sleep);

        if (TryGetComponent(out ActorFootstepSfx footstepSfx))
            footstepSfx.ApplySettings(enemyProfile.Footsteps);

        if (TryGetComponent(out CharacterOrbitHandsAnimator handsAnimator))
            handsAnimator.ApplySettings(enemyProfile.Hands);

        if (TryGetComponent(out EnemyCombatantAI combatantAI))
        {
            combatantAI.enabled = enemyProfile.IsCombatant;
            if (enemyProfile.IsCombatant)
                combatantAI.ApplySettings(enemyProfile.Combat);
        }

        if (enemyProfile.IsCombatant)
        {
            EnemyMeleeCombatantAI meleeCombatantAI = GetEnemyMeleeCombatant();
            if (meleeCombatantAI != null)
            {
                meleeCombatantAI.enabled = true;
                meleeCombatantAI.ApplySettings(enemyProfile.Melee);
            }
        }
        else if (TryGetComponent(out EnemyMeleeCombatantAI existingMeleeCombatantAI))
        {
            existingMeleeCombatantAI.enabled = false;
        }
    }

    /// <summary>
    /// Gets or creates the melee combat component only when the enemy profile requires a starting melee weapon.
    /// </summary>
    private EnemyMeleeCombatantAI GetEnemyMeleeCombatant()
    {
        if (enemyProfile == null || !enemyProfile.IsCombatant || enemyProfile.Melee == null)
            return TryGetComponent(out EnemyMeleeCombatantAI existingMeleeCombatant) ? existingMeleeCombatant : null;

        if (enemyProfile.Melee.StartingWeapon == null)
            return TryGetComponent(out EnemyMeleeCombatantAI existingMeleeCombatant) ? existingMeleeCombatant : null;

        if (TryGetComponent(out EnemyMeleeCombatantAI meleeCombatantAI))
            return meleeCombatantAI;

        return gameObject.AddComponent<EnemyMeleeCombatantAI>();
    }
}
