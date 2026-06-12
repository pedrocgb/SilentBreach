using Rewired;

namespace Breezeblocks.Settings
{

/// <summary>
/// Captures, restores, and reapplies player Rewired controller maps.
/// </summary>
public static class GameSettingsRewiredMapService
{
    private static bool mapsApplied;

    /// <summary>
    /// Clears application state after Rewired or play-mode state resets.
    /// </summary>
    public static void Reset()
    {
        mapsApplied = false;
    }

    /// <summary>
    /// Restores project-default keyboard and mouse maps.
    /// </summary>
    public static void RestoreDefaults(GameSettingsSaveData settings, int rewiredPlayerId)
    {
        if (!TryGetPlayer(rewiredPlayerId, out Player player))
            return;

        player.controllers.maps.LoadDefaultMaps(ControllerType.Keyboard);
        player.controllers.maps.LoadDefaultMaps(ControllerType.Mouse);
        settings.RewiredControllerMaps.Clear();
        mapsApplied = true;
    }

    /// <summary>
    /// Captures every user-assignable keyboard and mouse map.
    /// </summary>
    public static void Capture(GameSettingsSaveData settings, int rewiredPlayerId)
    {
        if (settings == null || !TryGetPlayer(rewiredPlayerId, out Player player))
            return;

        settings.RewiredControllerMaps.Clear();
        Capture(settings, player, ControllerType.Keyboard, 0);
        Capture(settings, player, ControllerType.Mouse, 0);
        mapsApplied = true;
    }

    /// <summary>
    /// Applies saved maps once per Rewired initialization.
    /// </summary>
    public static void TryApply(GameSettingsSaveData settings, int rewiredPlayerId)
    {
        if (mapsApplied ||
            settings?.RewiredControllerMaps == null ||
            settings.RewiredControllerMaps.Count <= 0 ||
            !TryGetPlayer(rewiredPlayerId, out Player player))
        {
            return;
        }

        for (int i = 0; i < settings.RewiredControllerMaps.Count; i++)
        {
            RewiredControllerMapSaveData savedMap = settings.RewiredControllerMaps[i];
            if (savedMap == null || string.IsNullOrWhiteSpace(savedMap.MapJson))
                continue;

            ControllerType controllerType = (ControllerType)savedMap.ControllerType;
            Controller controller = player.controllers.GetController(controllerType, savedMap.ControllerId);
            ControllerMap controllerMap = ControllerMap.CreateFromJson(controllerType, savedMap.MapJson);
            if (controller == null || controllerMap == null)
                continue;

            player.controllers.maps.RemoveMap(
                controllerType,
                savedMap.ControllerId,
                controllerMap.categoryId,
                controllerMap.layoutId);
            player.controllers.maps.AddMap(controller, controllerMap);
        }

        mapsApplied = true;
    }

    /// <summary>
    /// Captures all user-assignable maps for one controller.
    /// </summary>
    private static void Capture(GameSettingsSaveData settings, Player player, ControllerType controllerType, int controllerId)
    {
        ControllerMapSaveData[] maps = player.controllers.maps.GetMapSaveData(controllerType, controllerId, true);
        if (maps == null)
            return;

        for (int i = 0; i < maps.Length; i++)
        {
            ControllerMap map = maps[i].map;
            if (map == null)
                continue;

            settings.RewiredControllerMaps.Add(new RewiredControllerMapSaveData
            {
                ControllerType = (int)controllerType,
                ControllerId = controllerId,
                MapJson = map.ToJsonString()
            });
        }
    }

    /// <summary>
    /// Resolves the configured Rewired player when the input manager is ready.
    /// </summary>
    private static bool TryGetPlayer(int rewiredPlayerId, out Player player)
    {
        player = ReInput.isReady ? ReInput.players.GetPlayer(rewiredPlayerId) : null;
        return player != null;
    }
}

}
