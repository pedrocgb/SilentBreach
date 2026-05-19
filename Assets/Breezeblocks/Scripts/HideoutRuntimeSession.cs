using UnityEngine;

namespace Breezeblocks.HideoutSystem
{

public static class HideoutRuntimeSession
{
    private static bool initialized;
    private static int cash;
    private static int influencePoints;
    private static HideoutJobDefinition currentJob;

    public static bool IsInitialized => initialized;
    public static int Cash => cash;
    public static int InfluencePoints => influencePoints;
    public static HideoutJobDefinition CurrentJob => currentJob;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        initialized = false;
        cash = 0;
        influencePoints = 0;
        currentJob = null;
    }

    public static void EnsureInitialized(int startingCash, int startingInfluencePoints)
    {
        if (initialized)
            return;

        initialized = true;
        cash = Mathf.Max(0, startingCash);
        influencePoints = Mathf.Max(0, startingInfluencePoints);
        currentJob = null;
    }

    public static bool TrySpendCash(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (cash < amount)
            return false;

        cash -= amount;
        return true;
    }

    public static void AddCash(int amount)
    {
        cash += Mathf.Max(0, amount);
    }

    public static void SetCurrentJob(HideoutJobDefinition jobDefinition)
    {
        currentJob = jobDefinition;
    }
}

}
