using Sirenix.OdinInspector;
using UnityEngine;
using Breezeblocks.Missions;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Combat/Actor Health")]
public class ActorHealth : MonoBehaviour
{
    private float maxHealth = 100f;

    private bool isInvincible;
    private bool externalInvincibleOverride;
    private bool isDead;
    private bool isIncapacitated;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentHealth { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float MaxHealth => maxHealth;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAlive => !isDead;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDead => isDead;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsIncapacitated => isIncapacitated;

    public event System.Action<ActorDamageContext> Damaged;
    public event System.Action<ActorDamageContext> Died;
    public event System.Action<ActorDamageContext> Incapacitated;
    public event System.Action Recovered;

    private void Awake()
    {
        RestoreHealth();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RestoreHealth()
    {
        CurrentHealth = maxHealth;
        isDead = false;
        isIncapacitated = false;
    }

    public void ApplySettings(ActorHealthSettings settings, bool restoreFullHealth = false)
    {
        if (settings == null)
            return;

        maxHealth = Mathf.Max(0f, settings.MaxHealth);
        isInvincible = settings.IsInvincible;

        if (!Application.isPlaying || restoreFullHealth)
        {
            RestoreHealth();
            return;
        }

        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
    }

    public ActorDamageOutcome ApplyDamage(float damage)
    {
        return ApplyDamage(damage, new ActorDamageContext(null, isLethal: true));
    }

    public ActorDamageOutcome ApplyDamage(float damage, ActorDamageContext context)
    {
        if (damage <= 0f || isInvincible || externalInvincibleOverride || isDead)
            return ActorDamageOutcome.None;

        if (isIncapacitated)
        {
            if (!context.IsLethal)
                return ActorDamageOutcome.None;

            Die(context);
            return ActorDamageOutcome.Killed;
        }

        float nextHealth = Mathf.Max(0f, CurrentHealth - damage);
        if (nextHealth > 0f)
        {
            CurrentHealth = nextHealth;
            Damaged?.Invoke(context);
            return ActorDamageOutcome.Damaged;
        }

        CurrentHealth = 0f;
        if (context.IsLethal)
        {
            Die(context);
            return ActorDamageOutcome.Killed;
        }

        Incapacitate(context);
        return ActorDamageOutcome.Incapacitated;
    }

    public void RecoverFromIncapacitation(float restoredHealthFraction = 1f)
    {
        if (!isIncapacitated || isDead)
            return;

        isIncapacitated = false;
        float restoredHealth = Mathf.Max(1f, maxHealth * Mathf.Clamp01(restoredHealthFraction));
        CurrentHealth = Mathf.Clamp(restoredHealth, 0f, maxHealth);
        Recovered?.Invoke();
    }

    public void Die()
    {
        Die(new ActorDamageContext(null, isLethal: true));
    }

    public void SetConsoleInvincibleOverride(bool enabled)
    {
        externalInvincibleOverride = enabled;
    }

    private void Incapacitate(ActorDamageContext context)
    {
        if (isDead || isIncapacitated)
            return;

        isIncapacitated = true;
        Incapacitated?.Invoke(context);
        MissionRuntimeEvents.RaiseActorIncapacitated(this, context.InstigatorRoot);
    }

    private void Die(ActorDamageContext context)
    {
        if (isDead)
            return;

        CurrentHealth = 0f;
        isDead = true;
        isIncapacitated = false;
        Died?.Invoke(context);
        MissionRuntimeEvents.RaiseActorKilled(this, context.InstigatorRoot);
    }
}
}
