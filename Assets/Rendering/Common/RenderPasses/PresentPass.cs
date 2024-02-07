using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PresentPass : RenderPassBase
{
    public override string GetProfilerTag()
    {
        return "PresentPass";
    }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        base.Setup(context, ref renderingData);
    }
    
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        cmd.ClearRenderTarget(false, false, Color.clear);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(GetProfilerTag() + "Execute");
        cmd.Blit(ShaderID._CameraColorAttachment, BuiltinRenderTextureType.CameraTarget);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
}