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

    [FoldoutGroup("References")]
    [SerializeField] private PlayerStaminaController playerStaminaController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerFocusController playerFocusController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerNoise playerNoise;

    [FoldoutGroup("References")]
    [SerializeField] private ArmorLoadout armorLoadout;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [FoldoutGroup("References")]
    [SerializeField] private PlayerMeleeController playerMeleeController;

    [FoldoutGroup("Visuals")]
    [SerializeField] private Color armedAgentRevealTint = new(1f, 0.22f, 0.22f, 1f);

    private readonly List<FocusRevealTarget> tintedRevealTargets = new();
    private PlayerPerkModifierSet activeModifiers = new();
    private float nextRevealSyncTime;
    private bool appliedOnce;

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

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (appliedOnce)
            ApplyRuntimePerks();
    }

    private void Start()
    {
        ApplyRuntimePerks();
    }

    private void Update()
    {
        if (!activeModifiers.RevealArmedAgentsDuringFocus || Time.unscaledTime < nextRevealSyncTime)
            return;

        SyncArmedRevealTargets();
    }

    private void OnDisable()
    {
        ClearArmedRevealTargets();
    }

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

    private void SyncArmedRevealTargets()
    {
        ClearArmedRevealTargets();

        EnemyCombatantAI[] armedAgents = FindObjectsByType<EnemyCombatantAI>(FindObjectsSortMode.None);
        for (int i = 0; i < armedAgents.Length; i++)
        {
            EnemyCombatantAI armedAgent = armedAgents[i];
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
    }

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
