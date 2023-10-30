
namespace UnityEngine.Rendering.JsRP
{

public class ForwardOpaquePass : RendererPass
{
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        const string opaqueTag = "JsRP Opaque";
        Profiling.Profiler.BeginSample(opaqueTag);
        CommandBuffer cmd = CommandBufferPool.Get(opaqueTag);
        cmd.SetRenderTarget(m_ColorAttachments[0], m_DepthAttachment);
        cmd.ClearRenderTarget(true, true, Color.clear);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);

        var drawSettings = new DrawingSettings(new ShaderTagId(opaqueTag), new SortingSettings(renderingData.cameraData.camera));
        var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
        context.DrawRenderers(renderingData.cameraData.cullingResults, ref drawSettings, ref filterSettings);

        context.Submit();
        Profiling.Profiler.EndSample();
    }
}


}
