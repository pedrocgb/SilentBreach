using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Combat/Actor Health")]
public class ActorHealth : MonoBehaviour
{
    private float maxHealth = 100f;

    private bool isInvincible;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public float CurrentHealth { get; private set; }

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsAlive => CurrentHealth > 0f;

    private void Awake()
    {
        RestoreHealth();
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RestoreHealth()
    {
        CurrentHealth = maxHealth;
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

    public void ApplyDamage(float damage)
    {
        if (damage <= 0f || isInvincible)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

        if (CurrentHealth <= 0f)
        {
            Die();
        }
    }

    public void Die()
    {
        CurrentHealth = 0f;
        gameObject.SetActive(false);
    }
}
}
