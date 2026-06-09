using Breezeblocks.Input;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

/// <summary>
/// Centralizes common runtime dependency resolution used across weapon controllers and weapon actors.
/// </summary>
public static class WeaponRuntimeUtility
{
    /// <summary>
    /// Resolves player aim camera from explicit reference first, then from loaded scene helpers.
    /// </summary>
    public static PlayerAimCamera2D ResolveAimCamera(PlayerAimCamera2D currentCamera, GameObject owner)
    {
        if (currentCamera != null)
            return currentCamera;

        if (Camera.main != null)
        {
            PlayerAimCamera2D mainCameraAim = Camera.main.GetComponent<PlayerAimCamera2D>();
            if (mainCameraAim != null)
                return mainCameraAim;
        }

        return owner != null
            ? PlayerSceneReferenceUtility.FindPlayerAimCamera(owner)
            : null;
    }

    /// <summary>
    /// Resolves shared global object pooler without duplicating singleton fallback code in every weapon script.
    /// </summary>
    public static GlobalObjectPooler ResolveGlobalObjectPooler(GlobalObjectPooler currentPooler)
    {
        return currentPooler != null ? currentPooler : GlobalObjectPooler.Instance;
    }

    /// <summary>
    /// Resolves shared world SFX manager without duplicating singleton fallback code in every weapon script.
    /// </summary>
    public static WorldSfxManager ResolveWorldSfxManager(WorldSfxManager currentManager)
    {
        return currentManager != null ? currentManager : WorldSfxManager.Instance;
    }

    /// <summary>
    /// Ensures gameplay controller has reusable Rewired-backed input reader.
    /// </summary>
    public static IPlayerInputReader EnsureInputReader(IPlayerInputReader currentReader, int rewiredPlayerId)
    {
        return currentReader ?? new RewiredPlayerInputReader(rewiredPlayerId);
    }

    /// <summary>
    /// Ensures gameplay controller shares one Rewired-backed reader for both player and pointer access.
    /// </summary>
    public static void EnsureCombinedInputReaders(ref IPlayerInputReader inputReader, ref IPointerInputReader pointerInputReader, int rewiredPlayerId)
    {
        if (inputReader is RewiredPlayerInputReader rewiredReader)
        {
            pointerInputReader ??= rewiredReader;
            return;
        }

        if (pointerInputReader is RewiredPlayerInputReader pointerRewiredReader)
        {
            inputReader = pointerRewiredReader;
            return;
        }

        RewiredPlayerInputReader newReader = new(rewiredPlayerId);
        inputReader = newReader;
        pointerInputReader = newReader;
    }

    /// <summary>
    /// Emits player noise only when noise component exists, keeping controller call sites small.
    /// </summary>
    public static void EmitNoise(PlayerNoise playerNoise, float amount, float duration, NoiseType noiseType, bool isExtremeNoise)
    {
        if (playerNoise != null)
            playerNoise.AddNoiseSpike(amount, duration, noiseType, isExtremeNoise);
    }
}

}
