using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

public enum CutWireDifficulty
{
    Easy,
    Medium,
    Hard
}

public enum CutWireColor
{
    White,
    Black,
    Yellow,
    Blue,
    Red,
    Green
}

[Serializable]
public sealed class CutWireSlotDefinition
{
    [HorizontalGroup("Wire")]
    [SerializeField] private CutWireColor color;

    [HorizontalGroup("Wire")]
    [SerializeField] private bool mustBeCut;

    public CutWireColor Color => color;
    public bool MustBeCut => mustBeCut;

    /// <summary>
    /// Updates the authored wire color while preserving its required-cut state.
    /// </summary>
    public void SetColor(CutWireColor value)
    {
        color = value;
    }

    /// <summary>
    /// Updates whether this wire belongs to the successful solution.
    /// </summary>
    public void SetMustBeCut(bool value)
    {
        mustBeCut = value;
    }
}

[CreateAssetMenu(fileName = "CutWireMinigameDefinition", menuName = "Breezeblocks/Missions/Cut Wire Minigame Definition")]
public sealed class CutWireMinigameDefinition : ScriptableObject
{
    private const int MaximumWireCount = 6;

    [FoldoutGroup("Fuse Box")]
    [SerializeField] private string fuseBoxName = "Fuse Box";

    [FoldoutGroup("Fuse Box"), AssetsOnly, PreviewField(80f)]
    [SerializeField] private Sprite companyLogo;

    [FoldoutGroup("Solution")]
    [SerializeField] private CutWireDifficulty difficulty = CutWireDifficulty.Medium;

    [FoldoutGroup("Solution"), TableList(AlwaysExpanded = true, ShowIndexLabels = true)]
    [SerializeField] private List<CutWireSlotDefinition> wires = new();

    public string FuseBoxName => string.IsNullOrWhiteSpace(fuseBoxName) ? name : fuseBoxName.Trim();
    public Sprite CompanyLogo => companyLogo;
    public CutWireDifficulty Difficulty => difficulty;
    public int WireCount => ResolveWireCount(difficulty);
    public IReadOnlyList<CutWireSlotDefinition> Wires => wires;

    /// <summary>
    /// Returns the configured wire at an active slot index.
    /// </summary>
    public bool TryGetWire(int index, out CutWireSlotDefinition wire)
    {
        wire = index >= 0 && index < WireCount && index < wires.Count ? wires[index] : null;
        return wire != null;
    }

    /// <summary>
    /// Returns whether every required wire has been cut in the supplied persistent state.
    /// </summary>
    public bool AreAllRequiredWiresCut(IReadOnlyList<bool> cutStates)
    {
        bool hasRequiredWire = false;
        for (int i = 0; i < WireCount; i++)
        {
            if (!TryGetWire(i, out CutWireSlotDefinition wire) || !wire.MustBeCut)
                continue;

            hasRequiredWire = true;
            if (cutStates == null || i >= cutStates.Count || !cutStates[i])
                return false;
        }

        return hasRequiredWire;
    }

    /// <summary>
    /// Clamps authored values and maintains a unique wire entry for every active difficulty slot.
    /// </summary>
    private void OnValidate()
    {
        fuseBoxName = fuseBoxName != null ? fuseBoxName.Trim() : string.Empty;
        wires ??= new List<CutWireSlotDefinition>();

        int targetCount = ResolveWireCount(difficulty);
        while (wires.Count < targetCount)
            wires.Add(new CutWireSlotDefinition());

        while (wires.Count > targetCount)
            wires.RemoveAt(wires.Count - 1);

        bool[] usedColors = new bool[MaximumWireCount];
        bool hasRequiredWire = false;
        for (int i = 0; i < wires.Count; i++)
        {
            wires[i] ??= new CutWireSlotDefinition();
            int colorIndex = (int)wires[i].Color;
            if (colorIndex < 0 || colorIndex >= MaximumWireCount || usedColors[colorIndex])
            {
                colorIndex = FindFirstUnusedColor(usedColors);
                wires[i].SetColor((CutWireColor)colorIndex);
            }

            usedColors[colorIndex] = true;
            hasRequiredWire |= wires[i].MustBeCut;
        }

        if (!hasRequiredWire && wires.Count > 0)
            wires[0].SetMustBeCut(true);
    }

    /// <summary>
    /// Converts a difficulty preset into its active wire count.
    /// </summary>
    private static int ResolveWireCount(CutWireDifficulty value)
    {
        return value switch
        {
            CutWireDifficulty.Easy => 2,
            CutWireDifficulty.Medium => 4,
            _ => MaximumWireCount
        };
    }

    /// <summary>
    /// Finds the first wire color not already used by the current preset.
    /// </summary>
    private static int FindFirstUnusedColor(IReadOnlyList<bool> usedColors)
    {
        for (int i = 0; i < MaximumWireCount; i++)
        {
            if (!usedColors[i])
                return i;
        }

        return 0;
    }
}

}
