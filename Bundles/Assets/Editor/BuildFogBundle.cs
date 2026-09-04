using System.IO;
using UnityEditor;
using UnityEngine;

public static class BuildFogBundle
{
    [MenuItem("OverlayMap/Build Fog Bundles")]
    public static void Build()
    {
        BuildFor(BuildTarget.StandaloneLinux64, "AssetBundles/linux");
        BuildFor(BuildTarget.StandaloneWindows64, "AssetBundles/windows");
    }

    private static void BuildFor(BuildTarget target, string dir)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
        {
            Debug.LogWarning("OverlayMapFog: build support for " + target + " is not installed, skipping");
            return;
        }

        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = "overlaymapfog",
            assetNames = new[] { "Assets/FogMultiply.shader" }
        };

        Directory.CreateDirectory(dir);
        BuildPipeline.BuildAssetBundles(dir, new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle
            | BuildAssetBundleOptions.ChunkBasedCompression,
            target);
        Debug.Log("OverlayMapFog: built " + Path.GetFullPath(Path.Combine(dir, "overlaymapfog")));
    }
}
