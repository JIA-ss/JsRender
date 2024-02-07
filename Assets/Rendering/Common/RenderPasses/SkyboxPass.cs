using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SkyboxPass : RenderPassBase
{
    public override string GetProfilerTag()
    {
        return "SkyboxPass";
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        base.Setup(context, ref renderingData);
    }
    
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.SetRenderTarget(ShaderID._CameraColorAttachment, ShaderID._CameraDepthAttachment, 0, CubemapFace.Unknown, -1);
        cmd.ClearRenderTarget(false, false, Color.clear);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        context.DrawSkybox(renderingData.cameraData.camera);
#if UNITY_EDITOR
        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(renderingData.cameraData.camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(renderingData.cameraData.camera, GizmoSubset.PostImageEffects);
        }
#endif
    }
}