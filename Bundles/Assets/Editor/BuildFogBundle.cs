using System.IO;
using UnityEditor;

public static class BuildFogBundle
{
    public static void Build()
    {
        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = "overlaymapfog",
            assetNames = new[] { "Assets/FogMultiply.shader" }
        };

        const string dir = "AssetBundles";
        Directory.CreateDirectory(dir);
        BuildPipeline.BuildAssetBundles(dir, new[] { build },
            BuildAssetBundleOptions.ForceRebuildAssetBundle
            | BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneLinux64);
    }
}
