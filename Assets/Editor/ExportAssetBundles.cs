using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;


public static class AssetBundleBuilder
{
    static string GetOutputDir()
    {
        string assetBundleDirectory = Path.Combine(Application.streamingAssetsPath, "Shaders");
        if(!Directory.Exists(assetBundleDirectory))
		{
            Directory.CreateDirectory(assetBundleDirectory);
		}
        return assetBundleDirectory;
    }
    [MenuItem("Assets/Test AssetBundle")]
    static void TestBuild()
    {

        string assetBundleDirectory = GetOutputDir();

        AssetBundleBuild[] buildMap = new AssetBundleBuild[1];
        buildMap[0].assetBundleName = "test shaders";
        buildMap[0].assetNames = new string[]
        {
            "Assets/JoshuaRP/Forward/Shaders/AssetBundleTest/Color.shader",
            "Assets/JoshuaRP/Forward/Shaders/AssetBundleTest/Dependency.hlsl"
        };

		AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            assetBundleDirectory, buildMap,
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows);

    }

    [MenuItem("Assets/Test Load AssetBundle")]
    static void TestLoad()
    {
        string assetBundleDirectory = GetOutputDir();
        string filePath = Path.Combine(assetBundleDirectory, "test shaders");
            //Load "animals" AssetBundle
        AssetBundle assetBundle = AssetBundle.LoadFromFile(filePath);
        Object[] assets = assetBundle.LoadAllAssets();
        foreach (Object asset in assets)
        {
            Debug.Log(asset.name);
            Debug.Log(asset.ToString());
            Shader shader = asset as Shader;
            Material mat = new Material(shader);
            mat.EnableKeyword("RED");
            GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<Renderer>().material = mat;
        }
    }

    [MenuItem("Assets/Test AssetBundle2")]
    static void TestBuild2()
    {
        string outPath = GetOutputDir();


        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        string[] shaders = Directory.GetFiles("Assets/JoshuaRP/Forward/Shaders/", "*.shader", 
            SearchOption.AllDirectories);
        string[] hlsls = Directory.GetFiles("Assets/JoshuaRP/Forward/Shaders/", "*.hlsl", 
            SearchOption.AllDirectories);

        foreach (string item in hlsls)
        {
            var bundle = new AssetBundleBuild();
            bundle.assetBundleName = Path.GetFileNameWithoutExtension(item);//设置包名
            bundle.assetNames = new string[] {
                Path.GetDirectoryName(item) + "/" + bundle.assetBundleName + ".hlsl"
            };
            builds.Add(bundle);
            Debug.Log(bundle.assetBundleName);
            Debug.Log(bundle.assetNames[0]);
        }

        foreach (string item in shaders)
        {
            var bundle = new AssetBundleBuild();
            bundle.assetBundleName = Path.GetFileNameWithoutExtension(item);//设置包名
            bundle.assetNames = new string[] {
                Path.GetDirectoryName(item) + "/" + bundle.assetBundleName + ".shader"
            };
            builds.Add(bundle);
            Debug.Log(bundle.assetBundleName);
            Debug.Log(bundle.assetNames[0]);
        }

        //构建Assetbundle
        BuildPipeline.BuildAssetBundles(outPath, builds.ToArray(), 
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows);
        //刷新
        AssetDatabase.Refresh();
    }
}
