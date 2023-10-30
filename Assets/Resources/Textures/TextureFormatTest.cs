using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class DisplayInfo : EditorWindow
{
    static void getTargetFormats(string texture)
    {
        List<string> platforms = new List<string> {"Windows", "Android", "IOS"};
        TextureImporter ti = (TextureImporter)TextureImporter.GetAtPath(texture);
        foreach (string platformString in platforms)
        {
            Debug.Log(platformString + " get format: " + texture);
            if (ti.GetPlatformTextureSettings(platformString, out var platformMaxTextureSize, out var platformTextureFmt, out var platformCompressionQuality, out var platformAllowsAlphaSplit))
            {
                Debug.Log("override android format: " + platformTextureFmt.ToString());
            }
            else
            {
                Debug.Log("default android format: " + ti.GetAutomaticFormat(platformString).ToString());
            }
        }
    }

    [MenuItem("PlatformSettings/GetSettingsForAndroid")]
    static void GetAndroidSettings()
    {
        string platformString = "Windows";
        int platformMaxTextureSize = 0;
        TextureImporterFormat platformTextureFmt;
        int platformCompressionQuality = 0;
        bool platformAllowsAlphaSplit = false;

        string texture = "Assets/Resources/Textures/window.png";
        getTargetFormats(texture);
    }
}
