using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
public abstract class RenderPassBase
{
    public RenderTargetIdentifier[] colorAttachments = new RenderTargetIdentifier[]{BuiltinRenderTextureType.CameraTarget};
    public RenderTargetIdentifier depthAttachment = BuiltinRenderTextureType.CameraTarget;
    public abstract string GetProfilerTag();
    public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);
    public virtual void Setup(ScriptableRenderContext context, ref RenderingData renderingData) { }
    public virtual void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) { }
    public virtual void FrameCleanup(CommandBuffer cmd) { }
    public virtual void OnFinishCameraStackRendering(CommandBuffer cmd) {}
    public void SetupWithTag(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiler.BeginSample(GetProfilerTag() + " Setup");
        Setup(context, ref renderingData);
        Profiler.EndSample();
    }

    public void ConfigureWithTag(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        Profiler.BeginSample(GetProfilerTag() + "Configure");
        Configure(cmd, cameraTextureDescriptor);
        Profiler.EndSample();
    }
    public void ExecuteWithTag(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiler.BeginSample(GetProfilerTag() + " Execute");
        Execute(context, ref renderingData);
        Profiler.EndSample();
    }
    public void FrameCleanupWithTag(CommandBuffer cmd)
    {
        Profiler.BeginSample(GetProfilerTag() + " FrameCleanup");
        FrameCleanup(cmd);
        Profiler.EndSample();
    }
}
