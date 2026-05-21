using System;
using System.Collections;
using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Pathfinding;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[RequireComponent(typeof(ActorHealth))]
[AddComponentMenu("Breezeblocks/Missions/Actor Incapacitation Controller")]
public class ActorIncapacitationController : MonoBehaviour
{
    [FoldoutGroup("References")]
    [SerializeField] private ActorHealth actorHealth;

    [FoldoutGroup("References")]
    [SerializeField] private Rigidbody2D movementBody;

    [FoldoutGroup("Disable On Incapacitated"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<MonoBehaviour> additionalBehavioursToDisable = new();

    [FoldoutGroup("Death"), ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<GameObject> gameObjectsToHideOnDeath = new();

    [FoldoutGroup("Visuals")]
    [SerializeField] private SpriteRenderer stateSpriteRenderer;

    [FoldoutGroup("Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite incapacitatedSprite;

    [FoldoutGroup("Visuals"), PreviewField(72, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite deadSprite;

    [FoldoutGroup("Wake Up"), MinValue(0f), Range(0.01f, 1f)]
    [SerializeField] private float restoredHealthFractionOnWake = 1f;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsIncapacitated => actorHealth != null && actorHealth.IsIncapacitated;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsDead => actorHealth != null && actorHealth.IsDead;

    public event Action<bool> IncapacitationStateChanged;

    private readonly List<MonoBehaviour> runtimeBehavioursToDisable = new();
    private readonly Dictionary<MonoBehaviour, bool> cachedEnabledStates = new();
    private Coroutine wakeUpRoutine;
    private Sprite defaultSprite;
    private bool defaultSpriteCached;

    public static ActorIncapacitationController EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        ActorIncapacitationController controller = actorRoot.GetComponent<ActorIncapacitationController>();
        if (controller == null)
            controller = actorRoot.AddComponent<ActorIncapacitationController>();

        controller.CacheReferences();
        controller.CacheAutoDisableBehaviours();
        return controller;
    }

    private void Reset()
    {
        CacheReferences();
        CacheAutoDisableBehaviours();
        CacheDefaultSprite();
    }

    private void Awake()
    {
        CacheReferences();
        CacheAutoDisableBehaviours();
        CacheDefaultSprite();
    }

    private void OnEnable()
    {
        if (actorHealth == null)
            actorHealth = GetComponent<ActorHealth>();

        if (actorHealth != null)
        {
            actorHealth.Incapacitated += HandleIncapacitated;
            actorHealth.Recovered += HandleRecovered;
            actorHealth.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (actorHealth != null)
        {
            actorHealth.Incapacitated -= HandleIncapacitated;
            actorHealth.Recovered -= HandleRecovered;
            actorHealth.Died -= HandleDied;
        }
    }

    private void OnValidate()
    {
        restoredHealthFractionOnWake = Mathf.Clamp01(restoredHealthFractionOnWake);
        CacheReferences();
        CacheAutoDisableBehaviours();
        CacheDefaultSprite();
        RemoveNullDisableEntries();
        RemoveNullDeathHideEntries();
    }

    public void WakeUpNow()
    {
        if (actorHealth == null || !actorHealth.IsIncapacitated)
            return;

        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        actorHealth.RecoverFromIncapacitation(restoredHealthFractionOnWake);
    }

    private void HandleIncapacitated(ActorDamageContext context)
    {
        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        DisableRuntimeBehaviours();
        StopMotion();
        ApplyVisualState();

        IncapacitationStateChanged?.Invoke(true);

        float wakeDelay = GlobalSettings.Instance != null ? GlobalSettings.Instance.IncapacitatedWakeUpDelay : 0f;
        if (wakeDelay > 0f)
            wakeUpRoutine = StartCoroutine(WakeUpRoutine(wakeDelay));
    }

    private void HandleRecovered()
    {
        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        if (actorHealth != null && actorHealth.IsDead)
            return;

        foreach (KeyValuePair<MonoBehaviour, bool> pair in cachedEnabledStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        cachedEnabledStates.Clear();
        ApplyVisualState();
        IncapacitationStateChanged?.Invoke(false);
    }

    private void HandleDied(ActorDamageContext context)
    {
        if (wakeUpRoutine != null)
        {
            StopCoroutine(wakeUpRoutine);
            wakeUpRoutine = null;
        }

        DisableRuntimeBehaviours();
        StopMotion();
        ApplyVisualState();
        HideDeathObjects();
        IncapacitationStateChanged?.Invoke(false);
    }

    private IEnumerator WakeUpRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        wakeUpRoutine = null;
        actorHealth?.RecoverFromIncapacitation(restoredHealthFractionOnWake);
    }

    private void CacheReferences()
    {
        if (actorHealth == null)
            actorHealth = GetComponent<ActorHealth>();

        if (movementBody == null)
            movementBody = GetComponent<Rigidbody2D>();

        if (stateSpriteRenderer == null)
            stateSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void CacheAutoDisableBehaviours()
    {
        CacheReferences();
        RemoveNullDisableEntries();

        runtimeBehavioursToDisable.Clear();
        AddDisableBehaviourIfPresent(GetComponent<EnemyMovementController>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyVisionAI>());
        AddDisableBehaviourIfPresent(GetComponent<AIHearing>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyCombatantAI>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyMeleeCombatantAI>());
        AddDisableBehaviourIfPresent(GetComponent<EnemyFlashbangStatus>());
        AddDisableBehaviourIfPresent(GetComponent<ActorStaggerController>());
        AddDisableBehaviourIfPresent(GetComponent<AIPath>());
        AddDisableBehaviourIfPresent(GetComponent<AIDestinationSetter>());
        AddDisableBehaviourIfPresent(GetComponent<Seeker>());

        for (int i = 0; i < additionalBehavioursToDisable.Count; i++)
            AddDisableBehaviourIfPresent(additionalBehavioursToDisable[i]);
    }

    private void AddDisableBehaviourIfPresent(MonoBehaviour behaviour)
    {
        if (behaviour == null || behaviour == this || behaviour == actorHealth || runtimeBehavioursToDisable.Contains(behaviour))
            return;

        runtimeBehavioursToDisable.Add(behaviour);
    }

    private void RemoveNullDisableEntries()
    {
        additionalBehavioursToDisable.RemoveAll(behaviour => behaviour == null || behaviour == this || behaviour == actorHealth);
    }

    private void RemoveNullDeathHideEntries()
    {
        gameObjectsToHideOnDeath.RemoveAll(entry => entry == null || entry == gameObject);
    }

    private void CacheDefaultSprite()
    {
        if (defaultSpriteCached || stateSpriteRenderer == null)
            return;

        defaultSprite = stateSpriteRenderer.sprite;
        defaultSpriteCached = true;
    }

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

    private void StopMotion()
    {
        if (movementBody == null)
            return;

        movementBody.linearVelocity = Vector2.zero;
        movementBody.angularVelocity = 0f;
    }

    private void ApplyVisualState()
    {
        CacheDefaultSprite();
        if (stateSpriteRenderer == null)
            return;

        if (actorHealth != null && actorHealth.IsDead && deadSprite != null)
        {
            stateSpriteRenderer.sprite = deadSprite;
            return;
        }

        if (actorHealth != null && actorHealth.IsIncapacitated && incapacitatedSprite != null)
        {
            stateSpriteRenderer.sprite = incapacitatedSprite;
            return;
        }

        if (defaultSpriteCached)
            stateSpriteRenderer.sprite = defaultSprite;
    }

    private void HideDeathObjects()
    {
        for (int i = 0; i < gameObjectsToHideOnDeath.Count; i++)
        {
            GameObject target = gameObjectsToHideOnDeath[i];
            if (target != null)
                target.SetActive(false);
        }
    }
}

}
