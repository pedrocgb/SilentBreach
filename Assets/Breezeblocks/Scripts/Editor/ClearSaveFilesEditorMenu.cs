#if UNITY_EDITOR
using Breezeblocks.HideoutSystem;
using UnityEditor;
using UnityEngine;

namespace Breezeblocks.EditorTools
{

public static class ClearSaveFilesEditorMenu
{
    private const string MenuPath = "Tools/Breezeblocks/Clear Save Files";

    /// <summary>
    /// Confirms and deletes every file owned by the game's save system.
    /// </summary>
    [MenuItem(MenuPath)]
    private static void ClearSaveFiles()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Clear Save Files",
            "Delete all Silent Breach save files? This cannot be undone.",
            "Delete Save Files",
            "Cancel");
        if (!confirmed)
            return;

        int deletedFileCount = HideoutSaveSystem.DeleteAllSaveFiles();
        Debug.Log($"Cleared {deletedFileCount} Silent Breach save file(s) from {Application.persistentDataPath}.");
    }

    /// <summary>
    /// Prevents save deletion while Play Mode may still write runtime progress.
    /// </summary>
    [MenuItem(MenuPath, true)]
    private static bool ValidateClearSaveFiles()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}

}
#endif
