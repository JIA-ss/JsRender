using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ForwardRenderer : BaseRenderer
{
    public ForwardRenderer(List<RenderPassBase> renderPasses)
    {
        m_RenderPasses = renderPasses;
    }
    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        ref CameraData cameraData = ref renderingData.cameraData;
        RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

        foreach (RenderPassBase renderPass in m_RenderPasses)
        {
            renderPass.SetupWithTag(context, ref renderingData);
        }
    }

    public override void ExecuteRenderPass(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        base.ExecuteRenderPass(context, ref renderingData);
    }
}