using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ForwardRenderPipelineAsset : BaseRenderPipelineAsset
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

    [MenuItem("Assets/RenderPipelineAsset/ForwardRenderPipelineAsset")]
    static void CreateJoshuaRenderPipelineAsset()
    {
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateForwardRenderPipelineAssetAction>(), "ForwardRenderPipelineAsset.asset", null, null);
    }
#endif
    protected override RenderPipeline CreatePipeline()
    {
        return new ForwardRenderPipeline(this, CreateRenderPassesByConfig());
    }
}