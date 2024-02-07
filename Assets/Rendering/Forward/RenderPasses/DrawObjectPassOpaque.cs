using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DrawObjectPassOpaque : RenderPassBase
{
    public override string GetProfilerTag() { return "DrawObjectPassOpaque"; }
    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        base.Setup(context, ref renderingData);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.SetRenderTarget(ShaderID._CameraColorAttachment, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, ShaderID._CameraDepthAttachment, RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare);
        cmd.ClearRenderTarget(false, false, Color.clear);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get( GetProfilerTag() + "Execute");

        var drawSettings = new DrawingSettings(new ShaderTagId(GetProfilerTag()), new SortingSettings(renderingData.cameraData.camera));
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);
        // cmd.Blit(ShaderID._CameraDepthAttachment, ShaderID._DepthTexture);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
    }
}
