#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

public sealed class EquipmentItemRuntimeCatalogBuilder : IPreprocessBuildWithReport
{
    private const string ResourcesFolderPath = "Assets/Breezeblocks/Resources";
    private const string CatalogAssetPath = ResourcesFolderPath + "/EquipmentItemRuntimeCatalog.asset";

    private static readonly string[] SearchFilters =
    {
        "t:FirearmData",
        "t:MeleeWeaponData",
        "t:UtilityItemData",
        "t:ThrowableUtilityData",
        "t:FlashlightUtilityData",
        "t:ArmorData"
    };

    public int callbackOrder => 0;

    [InitializeOnLoadMethod]
    private static void ScheduleCatalogRefresh()
    {
        EditorApplication.delayCall -= RefreshCatalogOnDelay;
        EditorApplication.delayCall += RefreshCatalogOnDelay;
    }

    [MenuItem("Tools/Breezeblocks/Rebuild Equipment Runtime Catalog")]
    private static void RebuildFromMenu()
    {
        EnsureCatalogAssetUpToDate(logResult: true);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        EnsureCatalogAssetUpToDate(logResult: false);
    }

    private static void RefreshCatalogOnDelay()
    {
        EnsureCatalogAssetUpToDate(logResult: false);
    }

    private static void EnsureCatalogAssetUpToDate(bool logResult)
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            return;

        EnsureFolder("Assets/Breezeblocks");
        EnsureFolder(ResourcesFolderPath);

        List<EquipmentItemData> equipmentItems = CollectEquipmentItems();
        EquipmentItemRuntimeCatalog catalog = AssetDatabase.LoadAssetAtPath<EquipmentItemRuntimeCatalog>(CatalogAssetPath);
        bool created = false;
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EquipmentItemRuntimeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            created = true;
        }

        bool changed = catalog.ReplaceItemsForEditor(equipmentItems);
        if (created || changed)
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        if (logResult)
        {
            Debug.Log(
                $"Equipment runtime catalog {(created ? "created" : changed ? "updated" : "checked")} with {equipmentItems.Count} items.",
                catalog);
        }
    }

    private static List<EquipmentItemData> CollectEquipmentItems()
    {
        HashSet<string> uniqueGuids = new();
        List<EquipmentItemData> items = new();
        for (int filterIndex = 0; filterIndex < SearchFilters.Length; filterIndex++)
        {
            string[] guids = AssetDatabase.FindAssets(SearchFilters[filterIndex]);
            for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
            {
                string guid = guids[guidIndex];
                if (!uniqueGuids.Add(guid))
                    continue;

                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                EquipmentItemData asset = AssetDatabase.LoadAssetAtPath<EquipmentItemData>(assetPath);
                if (asset != null)
                    items.Add(asset);
            }
        }

        items.Sort((left, right) =>
        {
            if (left == null && right == null)
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            int displayNameComparison = string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            if (displayNameComparison != 0)
                return displayNameComparison;

            return string.Compare(left.name, right.name, System.StringComparison.OrdinalIgnoreCase);
        });
        return items;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        int lastSlashIndex = folderPath.LastIndexOf('/');
        if (lastSlashIndex <= 0)
            return;

        string parentFolder = folderPath.Substring(0, lastSlashIndex);
        string folderName = folderPath.Substring(lastSlashIndex + 1);
        EnsureFolder(parentFolder);

        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder(parentFolder, folderName);
    }
}

}
#endif
