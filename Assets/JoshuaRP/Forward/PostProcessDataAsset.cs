using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.JsRP
{
    public class PostProcessDataAsset : ScriptableObject
    {
        [System.Serializable]
        public class BlurSetting
        {
            public enum GaussKernal
            {
                e3x3 = 0,
                e5x5,
                e9x9,
                eCunstom
            }
            public int iteration = 3;
            public float downscaling = 1.0f;
            public float blurRadius = 2.0f;
            public GaussKernal kernal = GaussKernal.e5x5;
        }

        public bool blur = false;
        [SerializeField]
        public BlurSetting blurSetting = new BlurSetting();


#if UNITY_EDITOR
        internal class CreatePostProcessDataAssetAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                PostProcessDataAsset instance = CreateInstance<PostProcessDataAsset>();
                AssetDatabase.CreateAsset(instance, pathName);
            }
        }

        [MenuItem("Assets/JsRP/PostProcessData")]
        static void CreatePostProcessData()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreatePostProcessDataAssetAction>(), "PostProcessDataAsset.asset", null, null);
        }
#endif
    }

}