using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PreZPass : RenderPassBase
{
    private RenderTextureDescriptor m_DepthDescriptor;
    private int kDepthBufferBits = 32;
    public override string GetProfilerTag() { return "PreZPass"; }
    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get( GetProfilerTag() + "Setup");
        m_DepthDescriptor = renderingData.cameraData.cameraTargetDescriptor; //RenderingUtils.GetGlobalDepthAttachmentDescriptor(renderingData);
        m_DepthDescriptor.depthBufferBits = kDepthBufferBits;
        m_DepthDescriptor.colorFormat = RenderTextureFormat.Depth;
        m_DepthDescriptor.autoGenerateMips = false;
        m_DepthDescriptor.sRGB = false;
        m_DepthDescriptor.bindMS = false;
        m_DepthDescriptor.msaaSamples = 1;
        
        cmd.GetTemporaryRT(ShaderID._CameraColorAttachment, renderingData.cameraData.cameraTargetDescriptor);
        cmd.GetTemporaryRT(ShaderID._CameraDepthAttachment, m_DepthDescriptor);
        // cmd.GetTemporaryRT(ShaderID._DepthTexture, desc);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.SetRenderTarget(ShaderID._CameraDepthAttachment);
        cmd.ClearRenderTarget(true, true, Color.clear, SystemInfo.usesReversedZBuffer ? 1.0f : 0.0f);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get( GetProfilerTag() + "Execute");

        var drawSettings = new DrawingSettings(new ShaderTagId(GetProfilerTag()), new SortingSettings(renderingData.cameraData.camera));
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings);

        cmd.Clear();
        // cmd.Blit(ShaderID._CameraDepthAttachment, ShaderID._DepthTexture);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(ShaderID._CameraColorAttachment);
        cmd.ReleaseTemporaryRT(ShaderID._CameraDepthAttachment);
    }
}
