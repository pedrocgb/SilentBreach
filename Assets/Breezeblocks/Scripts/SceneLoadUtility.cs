using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breezeblocks
{

public static class SceneLoadUtility
{
    public static bool HasSceneReference(int buildIndex, string fallbackSceneName)
    {
        return buildIndex >= 0 || !string.IsNullOrWhiteSpace(SanitizeSceneName(fallbackSceneName));
    }

    public static bool CanLoadScene(int buildIndex, string fallbackSceneName)
    {
        if (IsBuildSceneAvailable(buildIndex))
            return true;

        string sanitizedSceneName = SanitizeSceneName(fallbackSceneName);
        return !string.IsNullOrWhiteSpace(sanitizedSceneName) && Application.CanStreamedLevelBeLoaded(sanitizedSceneName);
    }

    public static bool IsBuildSceneAvailable(int buildIndex)
    {
        return buildIndex >= 0 && !string.IsNullOrWhiteSpace(SceneUtility.GetScenePathByBuildIndex(buildIndex));
    }

    public static string GetBuildSceneName(int buildIndex)
    {
        if (!IsBuildSceneAvailable(buildIndex))
            return string.Empty;

        return Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(buildIndex));
    }

    public static string SanitizeSceneName(string sceneNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(sceneNameOrPath))
            return string.Empty;

        string trimmed = sceneNameOrPath.Trim();
        if (trimmed.Contains("/") || trimmed.Contains("\\") || trimmed.EndsWith(".unity"))
            return Path.GetFileNameWithoutExtension(trimmed);

        return trimmed;
    }

    public static bool TryLoadScene(int buildIndex, string fallbackSceneName)
    {
        if (IsBuildSceneAvailable(buildIndex))
        {
            SceneManager.LoadScene(buildIndex);
            return true;
        }

        string sanitizedSceneName = SanitizeSceneName(fallbackSceneName);
        if (!string.IsNullOrWhiteSpace(sanitizedSceneName) && Application.CanStreamedLevelBeLoaded(sanitizedSceneName))
        {
            SceneManager.LoadScene(sanitizedSceneName);
            return true;
        }

        return false;
    }
}

}
