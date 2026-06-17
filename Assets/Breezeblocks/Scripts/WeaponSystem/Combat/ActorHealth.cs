using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Breezeblocks.Missions;
using Pathfinding;

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
    private bool isSleeping;

        [FoldoutGroup("State Presentation")]
    [FoldoutGroup("State Presentation/Visuals")]
    [SerializeField] private SpriteRenderer stateSpriteRenderer;

    [FoldoutGroup("State Presentation/Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite incapacitatedSprite;

    [FoldoutGroup("State Presentation/Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite deadSprite;

    [FoldoutGroup("State Presentation/Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite sleepingSprite;

    [FoldoutGroup("State Presentation/Disable On Incapacitated Or Dead"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<MonoBehaviour> additionalBehavioursToDisable = new();

    [FoldoutGroup("State Presentation/Hide On Death"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> gameObjectsToHideOnDeath = new();

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

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsSleeping => isSleeping;

    public event System.Action<ActorDamageContext> Damaged;
    public event System.Action<ActorDamageContext> Died;
    public event System.Action<ActorDamageContext> Incapacitated;
    public event System.Action Recovered;
    public event System.Action<bool> SleepingStateChanged;

    private readonly List<MonoBehaviour> runtimeBehavioursToDisable = new();
    private readonly Dictionary<MonoBehaviour, bool> cachedEnabledStates = new();
    private Rigidbody2D movementBody;
    private Sprite defaultSprite;
    private bool defaultSpriteCached;

    /// <summary>
    /// Caches presentation references and initializes health before gameplay begins.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
        CacheAutoDisableBehaviours();
        CacheDefaultSprite();
        RestoreHealth();
    }

    /// <summary>
    /// Keeps state-presentation authoring lists clean while editing.
    /// </summary>
    private void OnValidate()
    {
        additionalBehavioursToDisable ??= new List<MonoBehaviour>();
        gameObjectsToHideOnDeath ??= new List<GameObject>();
        CacheReferences();
        CacheAutoDisableBehaviours();
        CacheDefaultSprite();
        RemoveNullDisableEntries();
        RemoveNullDeathHideEntries();
    }

    /// <summary>
    /// Restores this actor to full health and resets all body-state presentation.
    /// </summary>
    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RestoreHealth()
    {
        CurrentHealth = maxHealth;
        isDead = false;
        isIncapacitated = false;
        isSleeping = false;
        ApplyStatePresentation();
    }

    /// <summary>
    /// Applies health settings loaded from an actor profile.
    /// </summary>
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

    /// <summary>
    /// Applies lethal damage using an empty damage context.
    /// </summary>
    public ActorDamageOutcome ApplyDamage(float damage)
    {
        return ApplyDamage(damage, new ActorDamageContext(null, isLethal: true));
    }

    /// <summary>
    /// Applies damage, resolving whether it damages, kills, or incapacitates this actor.
    /// </summary>
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

    /// <summary>
    /// Recovers an incapacitated actor and restores the runtime presentation it had before being downed.
    /// </summary>
    public void RecoverFromIncapacitation(float restoredHealthFraction = 1f)
    {
        if (!isIncapacitated || isDead)
            return;

        isIncapacitated = false;
        float restoredHealth = Mathf.Max(1f, maxHealth * Mathf.Clamp01(restoredHealthFraction));
        CurrentHealth = Mathf.Clamp(restoredHealth, 0f, maxHealth);
        ApplyStatePresentation();
        Recovered?.Invoke();
    }

    /// <summary>
    /// Kills this actor using a default lethal damage context.
    /// </summary>
    public void Die()
    {
        Die(new ActorDamageContext(null, isLethal: true));
    }

    /// <summary>
    /// Enables or disables external invincibility used by console/debug systems.
    /// </summary>
    public void SetConsoleInvincibleOverride(bool enabled)
    {
        externalInvincibleOverride = enabled;
    }

    /// <summary>
    /// Applies sleeping presentation without changing health, death, or incapacitation state.
    /// </summary>
    public void SetSleeping(bool sleeping)
    {
        if (isDead || isIncapacitated)
            sleeping = false;

        if (isSleeping == sleeping)
            return;

        isSleeping = sleeping;
        ApplyStatePresentation();
        SleepingStateChanged?.Invoke(isSleeping);
    }

    /// <summary>
    /// Enters incapacitated state and raises mission/runtime notifications.
    /// </summary>
    private void Incapacitate(ActorDamageContext context)
    {
        if (isDead || isIncapacitated)
            return;

        isIncapacitated = true;
        isSleeping = false;
        ApplyStatePresentation();
        Incapacitated?.Invoke(context);
        MissionRuntimeEvents.RaiseActorIncapacitated(this, context.InstigatorRoot);
    }

    /// <summary>
    /// Enters dead state and raises mission/runtime notifications.
    /// </summary>
    private void Die(ActorDamageContext context)
    {
        if (isDead)
            return;

        CurrentHealth = 0f;
        isDead = true;
        isIncapacitated = false;
        isSleeping = false;
        ApplyStatePresentation();
        Died?.Invoke(context);
        MissionRuntimeEvents.RaiseActorKilled(this, context.InstigatorRoot);
    }

    /// <summary>
    /// Caches same-object and child references used by body-state presentation.
    /// </summary>
    private void CacheReferences()
    {
        movementBody = GetComponent<Rigidbody2D>();

        if (stateSpriteRenderer == null)
            stateSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    /// <summary>
    /// Builds the runtime disable list from known actor systems plus designer-specified behaviours.
    /// </summary>
    private void CacheAutoDisableBehaviours()
    {
        additionalBehavioursToDisable ??= new List<MonoBehaviour>();
        RemoveNullDisableEntries();
        runtimeBehavioursToDisable.Clear();
        AddDisableBehaviourIfPresent(GetComponent<EnemyMovementController>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyVisionAI>());
        AddDisableBehaviourIfPresent(GetComponent<AIHearing>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyCombatantAI>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyMeleeCombatantAI>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyFlashbangStatus>());
        AddDisableBehaviourIfPresent(GetComponent<EnemySleepController>());
        AddDisableBehaviourIfPresent(GetComponent<ActorStaggerController>());
        AddDisableBehaviourIfPresent(GetComponent<AIPath>());
        AddDisableBehaviourIfPresent(GetComponent<AIDestinationSetter>());
        AddDisableBehaviourIfPresent(GetComponent<Seeker>());

        for (int i = 0; i < additionalBehavioursToDisable.Count; i++)
            AddDisableBehaviourIfPresent(additionalBehavioursToDisable[i]);
    }

    /// <summary>
    /// Adds a behaviour to the runtime disable list when it can be safely controlled by health state.
    /// </summary>
    private void AddDisableBehaviourIfPresent(MonoBehaviour behaviour)
    {
        if (behaviour == null ||
            behaviour == this ||
            behaviour is ActorIncapacitationController ||
            runtimeBehavioursToDisable.Contains(behaviour))
        {
            return;
        }

        runtimeBehavioursToDisable.Add(behaviour);
    }

    /// <summary>
    /// Removes invalid entries from the designer-authored disable list.
    /// </summary>
    private void RemoveNullDisableEntries()
    {
        additionalBehavioursToDisable.RemoveAll(behaviour =>
            behaviour == null ||
            behaviour == this ||
            behaviour is ActorIncapacitationController);
    }

    /// <summary>
    /// Removes invalid entries from the designer-authored death hiding list.
    /// </summary>
    private void RemoveNullDeathHideEntries()
    {
        gameObjectsToHideOnDeath ??= new List<GameObject>();
        gameObjectsToHideOnDeath.RemoveAll(entry => entry == null || entry == gameObject);
    }

    /// <summary>
    /// Captures the default sprite once so recovery can restore normal presentation.
    /// </summary>
    private void CacheDefaultSprite()
    {
        if (defaultSpriteCached || stateSpriteRenderer == null)
            return;

        defaultSprite = stateSpriteRenderer.sprite;
        defaultSpriteCached = true;
    }

    /// <summary>
    /// Applies the current health/sleep presentation to visuals, motion, and controlled behaviours.
    /// </summary>
    private void ApplyStatePresentation()
    {
        CacheDefaultSprite();
        bool bodyUnavailable = isDead || isIncapacitated;

        if (bodyUnavailable)
        {
            DisableRuntimeBehaviours();
            StopMotion();
        }
        else
        {
            RestoreRuntimeBehaviours();
            if (isSleeping)
                StopMotion();
        }

        ApplySpriteState();
        ApplyDeathObjectVisibility();
    }

    /// <summary>
    /// Disables configured runtime behaviours while an actor is incapacitated or dead.
    /// </summary>
    private void DisableRuntimeBehaviours()
    {
        CacheAutoDisableBehaviours();

        for (int i = 0; i < runtimeBehavioursToDisable.Count; i++)
        {
            MonoBehaviour behaviour = runtimeBehavioursToDisable[i];
            if (behaviour == null || behaviour == this)
                continue;

            if (!cachedEnabledStates.ContainsKey(behaviour))
                cachedEnabledStates[behaviour] = behaviour.enabled;

            behaviour.enabled = false;
        }
    }

    /// <summary>
    /// Restores behaviours to the enabled state cached before incapacitation or death presentation.
    /// </summary>
    private void RestoreRuntimeBehaviours()
    {
        foreach (KeyValuePair<MonoBehaviour, bool> pair in cachedEnabledStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        cachedEnabledStates.Clear();
    }

    /// <summary>
    /// Stops physical motion when actor state should be body/static presentation.
    /// </summary>
    private void StopMotion()
    {
        if (movementBody == null)
            return;

        movementBody.linearVelocity = Vector2.zero;
        movementBody.angularVelocity = 0f;
    }

    /// <summary>
    /// Applies the highest-priority sprite for dead, incapacitated, sleeping, or default state.
    /// </summary>
    private void ApplySpriteState()
    {
        if (stateSpriteRenderer == null)
            return;

        if (isDead && deadSprite != null)
        {
            stateSpriteRenderer.sprite = deadSprite;
            return;
        }

        if (isIncapacitated && incapacitatedSprite != null)
        {
            stateSpriteRenderer.sprite = incapacitatedSprite;
            return;
        }

        if (isSleeping && sleepingSprite != null)
        {
            stateSpriteRenderer.sprite = sleepingSprite;
            return;
        }

        if (defaultSpriteCached)
            stateSpriteRenderer.sprite = defaultSprite;
    }

    /// <summary>
    /// Shows or hides death-only configured objects based on current death state.
    /// </summary>
    private void ApplyDeathObjectVisibility()
    {
        gameObjectsToHideOnDeath ??= new List<GameObject>();
        for (int i = 0; i < gameObjectsToHideOnDeath.Count; i++)
        {
            GameObject target = gameObjectsToHideOnDeath[i];
            if (target != null)
                target.SetActive(!isDead);
        }
    }
}
}
