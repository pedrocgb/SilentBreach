using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

/// <summary>
/// Stores mutable firearm ammo state separately from the player weapon controller flow logic.
/// </summary>
public sealed class FirearmRuntimeState
{
    /// <summary>
    /// Gets currently loaded rounds in active firearm magazine or chamber.
    /// </summary>
    public int LoadedAmmo { get; private set; }

    /// <summary>
    /// Gets currently available reserve rounds for active firearm.
    /// </summary>
    public int ReserveAmmo { get; private set; }

    /// <summary>
    /// Clears all mutable ammo state when no firearm is equipped.
    /// </summary>
    public void Clear()
    {
        LoadedAmmo = 0;
        ReserveAmmo = 0;
    }

    /// <summary>
    /// Initializes ammo state for newly equipped firearm using requested values or firearm defaults.
    /// </summary>
    public void Initialize(FirearmData firearm, int requestedLoadedAmmo, int requestedReserveAmmo, bool infiniteReserveAmmo)
    {
        int ammoCapacity = firearm != null ? firearm.AmmoCapacity : 0;
        int defaultLoadedAmmo = ammoCapacity;
        int defaultReserveAmmo = firearm != null ? firearm.DefaultReserveAmmo : 0;

        LoadedAmmo = Mathf.Clamp(requestedLoadedAmmo < 0 ? defaultLoadedAmmo : requestedLoadedAmmo, 0, ammoCapacity);
        ReserveAmmo = Mathf.Max(0, requestedReserveAmmo < 0 ? defaultReserveAmmo : requestedReserveAmmo);
        EnsureReserveBuffer(firearm, infiniteReserveAmmo);
    }

    /// <summary>
    /// Adds reserve rounds and reports whether mutable state changed.
    /// </summary>
    public bool AddReserveAmmo(int amount)
    {
        if (amount <= 0)
            return false;

        ReserveAmmo += amount;
        return true;
    }

    /// <summary>
    /// Keeps reserve ammo above one magazine when infinite-ammo cheat is active.
    /// </summary>
    public bool EnsureReserveBuffer(FirearmData firearm, bool infiniteReserveAmmo)
    {
        if (!infiniteReserveAmmo || firearm == null)
            return false;

        int bufferedReserveAmmo = Mathf.Max(ReserveAmmo, Mathf.Max(1, firearm.AmmoCapacity));
        if (bufferedReserveAmmo == ReserveAmmo)
            return false;

        ReserveAmmo = bufferedReserveAmmo;
        return true;
    }

    /// <summary>
    /// Tries to consume one loaded round before firing.
    /// </summary>
    public bool TryConsumeRound()
    {
        if (LoadedAmmo <= 0)
            return false;

        LoadedAmmo--;
        return true;
    }

    /// <summary>
    /// Transfers as many rounds as possible into magazine reload target and returns amount moved.
    /// </summary>
    public int TransferMagazineRounds(int ammoCapacity, bool infiniteReserveAmmo)
    {
        int missingRounds = Mathf.Max(0, ammoCapacity - LoadedAmmo);
        int roundsToTransfer = infiniteReserveAmmo
            ? missingRounds
            : Mathf.Min(missingRounds, ReserveAmmo);
        if (roundsToTransfer <= 0)
            return 0;

        LoadedAmmo += roundsToTransfer;
        if (!infiniteReserveAmmo)
            ReserveAmmo -= roundsToTransfer;

        return roundsToTransfer;
    }

    /// <summary>
    /// Loads one round for bullet-by-bullet reloads and reports whether state changed.
    /// </summary>
    public bool TryLoadSingleRound(int ammoCapacity, bool infiniteReserveAmmo)
    {
        if (LoadedAmmo >= ammoCapacity)
            return false;

        if (!infiniteReserveAmmo && ReserveAmmo <= 0)
            return false;

        LoadedAmmo++;
        if (!infiniteReserveAmmo)
            ReserveAmmo--;

        return true;
    }
}

}
