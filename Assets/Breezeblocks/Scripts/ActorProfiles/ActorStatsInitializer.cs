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

    private void Awake()
    {
        ApplyProfiles();
    }

    [ContextMenu("Apply Profiles")]
    public void ApplyProfiles()
    {
        if (playerProfile != null)
            ApplyPlayerProfile();

        if (enemyProfile != null)
            ApplyEnemyProfile();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ApplyProfiles();
    }

    private void ApplyPlayerProfile()
    {
        if (TryGetComponent(out ActorHealth health))
            health.ApplySettings(playerProfile.Health);

        if (TryGetComponent(out ActorStaggerController staggerController))
            staggerController.ApplySettings(playerProfile.Stagger);

        if (TryGetComponent(out PlayerTopDownMotor2D playerMotor))
            playerMotor.ApplySettings(playerProfile.Movement);

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
    }

    private void ApplyEnemyProfile()
    {
        MissionActorIdentity.EnsureOn(gameObject);
        ActorIncapacitationController.EnsureOn(gameObject);

        if (TryGetComponent(out ActorHealth health))
            health.ApplySettings(enemyProfile.Health);

        if (TryGetComponent(out ActorStaggerController staggerController))
            staggerController.ApplySettings(enemyProfile.Stagger);

        if (TryGetComponent(out EnemyMovementController movementController))
            movementController.ApplySettings(enemyProfile.Movement);

        if (TryGetComponent(out EnemyVisionAI visionAI))
            visionAI.ApplySettings(enemyProfile.Vision);

        if (TryGetComponent(out AIHearing hearing))
            hearing.ApplySettings(enemyProfile.Hearing);

        if (TryGetComponent(out EnemyCombatantAI combatantAI))
            combatantAI.ApplySettings(enemyProfile.Combat);

        EnemyMeleeCombatantAI meleeCombatantAI = GetEnemyMeleeCombatant();
        meleeCombatantAI?.ApplySettings(enemyProfile.Melee);
    }

    private EnemyMeleeCombatantAI GetEnemyMeleeCombatant()
    {
        if (enemyProfile == null || enemyProfile.Melee == null)
            return TryGetComponent(out EnemyMeleeCombatantAI existingMeleeCombatant) ? existingMeleeCombatant : null;

        if (enemyProfile.Melee.StartingWeapon == null)
            return TryGetComponent(out EnemyMeleeCombatantAI existingMeleeCombatant) ? existingMeleeCombatant : null;

        if (TryGetComponent(out EnemyMeleeCombatantAI meleeCombatantAI))
            return meleeCombatantAI;

        return gameObject.AddComponent<EnemyMeleeCombatantAI>();
    }
}
