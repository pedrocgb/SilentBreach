using System.Collections.Generic;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Hideout/Player Perk Effect Controller")]
public sealed class PlayerPerkEffectController : MonoBehaviour
{
    private const float RevealSyncInterval = 1f;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerStaminaController playerStaminaController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerFocusController playerFocusController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerNoise playerNoise;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private ArmorLoadout armorLoadout;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("Cached References"), ShowInInspector, ReadOnly]
    private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("Visuals")]
    [SerializeField] private Color armedAgentRevealTint = new(1f, 0.22f, 0.22f, 1f);

    private readonly List<FocusRevealTarget> tintedRevealTargets = new();
    private readonly List<EnemyCombatantAI> armedAgentsBuffer = new();
    private PlayerPerkModifierSet activeModifiers = new();
    private float nextRevealSyncTime;
    private bool appliedOnce;

    /// <summary>
    /// Ensures a perk effect controller exists on the supplied actor root.
    /// </summary>
    public static PlayerPerkEffectController EnsureOn(GameObject actorRoot)
    {
        if (actorRoot == null)
            return null;

        PlayerPerkEffectController controller = actorRoot.GetComponent<PlayerPerkEffectController>();
        if (controller == null)
            controller = actorRoot.AddComponent<PlayerPerkEffectController>();

        controller.CacheReferences();
        return controller;
    }

    /// <summary>
    /// Refreshes cached same-object references when the component is added or reset.
    /// </summary>
    private void Reset()
    {
        CacheReferences();
    }

    /// <summary>
    /// Caches same-object references before runtime perk application begins.
    /// </summary>
    private void Awake()
    {
        CacheReferences();
    }

    /// <summary>
    /// Reapplies runtime perks when the controller is re-enabled after an earlier setup.
    /// </summary>
    private void OnEnable()
    {
        if (appliedOnce)
            ApplyRuntimePerks();
    }

    /// <summary>
    /// Applies the currently equipped runtime perks after startup dependencies are ready.
    /// </summary>
    private void Start()
    {
        ApplyRuntimePerks();
    }

    /// <summary>
    /// Periodically refreshes armed-agent reveal targets while the perk is active.
    /// </summary>
    private void Update()
    {
        if (!activeModifiers.RevealArmedAgentsDuringFocus || Time.unscaledTime < nextRevealSyncTime)
            return;

        SyncArmedRevealTargets();
    }

    /// <summary>
    /// Clears any temporary focus reveal tint overrides when the controller is disabled.
    /// </summary>
    private void OnDisable()
    {
        ClearArmedRevealTargets();
    }

    /// <summary>
    /// Rebuilds active perk modifiers from runtime loadout and applies them to dependent systems.
    /// </summary>
    public void ApplyRuntimePerks()
    {
        CacheReferences();
        PlayerPerkRuntimeLoadout perkLoadout = PlayerPerkRuntimeSession.PeekEquippedPerks();
        activeModifiers = PlayerPerkModifierSet.BuildFrom(perkLoadout.EquippedPerks);

        bool restoreResourcesToFull = !appliedOnce;
        playerStaminaController?.ApplyPerkModifiers(activeModifiers, restoreResourcesToFull);
        playerFocusController?.ApplyPerkModifiers(activeModifiers, restoreResourcesToFull);
        armorLoadout?.ApplyPerkModifiers(activeModifiers);
        playerNoise?.ApplyPerkModifiers(activeModifiers);
        playerWeaponController?.ApplyPerkModifiers(activeModifiers);
        playerMeleeController?.ApplyPerkModifiers(activeModifiers);

        if (activeModifiers.RevealArmedAgentsDuringFocus)
            SyncArmedRevealTargets();
        else
            ClearArmedRevealTargets();

        appliedOnce = true;
    }

    /// <summary>
    /// Caches same-object runtime components affected by passive perk modifiers.
    /// </summary>
    private void CacheReferences()
    {
        if (playerStaminaController == null)
            playerStaminaController = GetComponent<PlayerStaminaController>();

        if (playerFocusController == null)
            playerFocusController = GetComponent<PlayerFocusController>();

        if (playerNoise == null)
            playerNoise = GetComponent<PlayerNoise>();

        if (armorLoadout == null)
            armorLoadout = GetComponent<ArmorLoadout>();

        if (playerWeaponController == null)
            playerWeaponController = GetComponent<PlayerWeaponController>();

        if (playerMeleeController == null)
            playerMeleeController = GetComponent<PlayerMeleeController>();
    }

    /// <summary>
    /// Rebuilds reveal tint overrides so only armed enemy agents stay highlighted during focus.
    /// </summary>
    private void SyncArmedRevealTargets()
    {
        ClearArmedRevealTargets();

        PlayerSceneReferenceUtility.CollectComponentsInLoadedScenes(armedAgentsBuffer, includeInactive: false);
        for (int i = 0; i < armedAgentsBuffer.Count; i++)
        {
            EnemyCombatantAI armedAgent = armedAgentsBuffer[i];
            if (!TryResolveArmedRevealTarget(armedAgent, out FocusRevealTarget revealTarget))
                continue;

            revealTarget.SetRevealTintOverride(armedAgentRevealTint);
            tintedRevealTargets.Add(revealTarget);
        }

        nextRevealSyncTime = Time.unscaledTime + RevealSyncInterval;
        armedAgentsBuffer.Clear();
    }

    /// <summary>
    /// Resolves a reveal target only when the supplied combatant should count as armed for this perk.
    /// </summary>
    private static bool TryResolveArmedRevealTarget(EnemyCombatantAI armedAgent, out FocusRevealTarget revealTarget)
    {
        revealTarget = null;
        if (armedAgent == null || !armedAgent.HasConfiguredFirearmLoadout)
            return false;

        revealTarget = armedAgent.GetComponent<FocusRevealTarget>();
        if (revealTarget == null)
            revealTarget = armedAgent.GetComponentInChildren<FocusRevealTarget>(true);

        return revealTarget != null;
    }

    /// <summary>
    /// Clears armed-agent reveal tint overrides and resets refresh timing.
    /// </summary>
    private void ClearArmedRevealTargets()
    {
        for (int i = tintedRevealTargets.Count - 1; i >= 0; i--)
        {
            FocusRevealTarget revealTarget = tintedRevealTargets[i];
            if (revealTarget != null)
                revealTarget.ClearRevealTintOverride();
        }

        tintedRevealTargets.Clear();
        nextRevealSyncTime = 0f;
    }
}

}
