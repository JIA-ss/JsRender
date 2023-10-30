using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.JsRP
{
    public class ForwardRenderPipelineAsset : RenderPipelineAsset
    {

#if UNITY_EDITOR

        internal class CreateForwardRenderPipelineAssetAction : UnityEditor.ProjectWindowCallback.EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                ForwardRenderPipelineAsset instance = CreateInstance<ForwardRenderPipelineAsset>();
                AssetDatabase.CreateAsset(instance, pathName);
            }
        }

        [MenuItem("Assets/JsRP/ForwardRenderPipelineAsset")]
        static void CreateJoshuaRenderPipelineAsset()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateForwardRenderPipelineAssetAction>(), "ForwardRenderPipelineAsset.asset", null, null);
        }
#endif

        protected override RenderPipeline CreatePipeline()
        {
            return new ForwardRenderPipeline(this);
        }

        public enum OITType
        {
            kNone,
            kDepthPeeling,
            kWeightedSum,
            kWeightedAverage,
            kDepthWeighted,
            kThreePassWeighted,
        }

        public OITType m_OITType = OITType.kDepthPeeling;

        [Range(0, 4)]
        public int m_depthWeightedFuncIdx = 0;


        public PostProcessDataAsset m_postProcessData;

    }
}