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

    // Executes the EnsureOn routine.
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

    // Executes the Reset routine.
    private void Reset()
    {
        CacheReferences();
    }

    // Executes the Awake routine.
    private void Awake()
    {
        CacheReferences();
    }

    // Executes the OnEnable routine.
    private void OnEnable()
    {
        if (appliedOnce)
            ApplyRuntimePerks();
    }

    // Executes the Start routine.
    private void Start()
    {
        ApplyRuntimePerks();
    }

    // Executes the Update routine.
    private void Update()
    {
        if (!activeModifiers.RevealArmedAgentsDuringFocus || Time.unscaledTime < nextRevealSyncTime)
            return;

        SyncArmedRevealTargets();
    }

    // Executes the OnDisable routine.
    private void OnDisable()
    {
        ClearArmedRevealTargets();
    }

    // Executes the ApplyRuntimePerks routine.
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

    // Executes the CacheReferences routine.
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

    // Executes the SyncArmedRevealTargets routine.
    private void SyncArmedRevealTargets()
    {
        ClearArmedRevealTargets();

        PlayerSceneReferenceUtility.CollectComponentsInLoadedScenes(armedAgentsBuffer, includeInactive: false);
        for (int i = 0; i < armedAgentsBuffer.Count; i++)
        {
            EnemyCombatantAI armedAgent = armedAgentsBuffer[i];
            if (armedAgent == null)
                continue;

            FocusRevealTarget revealTarget = armedAgent.GetComponent<FocusRevealTarget>();
            if (revealTarget == null)
                revealTarget = armedAgent.GetComponentInChildren<FocusRevealTarget>(true);

            if (revealTarget == null)
                continue;

            revealTarget.SetRevealTintOverride(armedAgentRevealTint);
            tintedRevealTargets.Add(revealTarget);
        }

        nextRevealSyncTime = Time.unscaledTime + RevealSyncInterval;
        armedAgentsBuffer.Clear();
    }

    // Executes the ClearArmedRevealTargets routine.
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
