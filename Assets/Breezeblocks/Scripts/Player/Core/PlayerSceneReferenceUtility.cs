using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves scene references for player-facing scripts without relying on Unity's risky global find APIs.
/// </summary>
public static class PlayerSceneReferenceUtility
{
    /// <summary>
    /// Finds the first component of the requested type in loaded scenes.
    /// </summary>
    public static T FindFirstComponentInLoadedScenes<T>(bool includeInactive = true) where T : Component
    {
        return FindFirstComponentInLoadedScenes<T>(null, includeInactive);
    }

    /// <summary>
    /// Finds the first component of the requested type, preferring the caller's scene before scanning other loaded scenes.
    /// </summary>
    public static T FindFirstComponentInLoadedScenes<T>(GameObject preferredContext, bool includeInactive = true) where T : Component
    {
        if (preferredContext != null &&
            TryFindFirstComponentInScene(preferredContext.scene, includeInactive, out T preferredResult))
        {
            return preferredResult;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (preferredContext != null && scene == preferredContext.scene)
                continue;

            if (TryFindFirstComponentInScene(scene, includeInactive, out T result))
                return result;
        }

        return null;
    }

    /// <summary>
    /// Collects every component of the requested type from loaded scenes.
    /// </summary>
    public static void CollectComponentsInLoadedScenes<T>(List<T> results, bool includeInactive = true) where T : Component
    {
        if (results == null)
            return;

        results.Clear();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                T[] components = roots[rootIndex].GetComponentsInChildren<T>(includeInactive);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    T component = components[componentIndex];
                    if (component != null)
                        results.Add(component);
                }
            }
        }
    }

    /// <summary>
    /// Finds the first component of the requested type on the main camera hierarchy.
    /// </summary>
    public static T FindFirstComponentUnderMainCamera<T>(bool includeInactive = true) where T : Component
    {
        if (Camera.main == null)
            return null;

        T directMatch = Camera.main.GetComponent<T>();
        if (directMatch != null)
            return directMatch;

        return Camera.main.GetComponentInChildren<T>(includeInactive);
    }

    /// <summary>
    /// Finds the player aim camera by checking the main camera first and then loaded scenes.
    /// </summary>
    public static Breezeblocks.WeaponSystem.PlayerAimCamera2D FindPlayerAimCamera(GameObject preferredContext)
    {
        Breezeblocks.WeaponSystem.PlayerAimCamera2D mainCameraMatch =
            FindFirstComponentUnderMainCamera<Breezeblocks.WeaponSystem.PlayerAimCamera2D>();

        return mainCameraMatch != null
            ? mainCameraMatch
            : FindFirstComponentInLoadedScenes<Breezeblocks.WeaponSystem.PlayerAimCamera2D>(preferredContext);
    }

    /// <summary>
    /// Finds the first post-processing volume available to the player scripts.
    /// </summary>
    public static Volume FindPlayerVolume(GameObject preferredContext)
    {
        return FindFirstComponentInLoadedScenes<Volume>(preferredContext);
    }

    /// <summary>
    /// Attempts to find the first component of the requested type inside the provided scene.
    /// </summary>
    private static bool TryFindFirstComponentInScene<T>(Scene scene, bool includeInactive, out T result) where T : Component
    {
        result = null;
        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T component = roots[i].GetComponentInChildren<T>(includeInactive);
            if (component == null)
                continue;

            result = component;
            return true;
        }

        return false;
    }
}
