namespace UnityEngine.Rendering.JsRP
{

public class ForwardPrePass : RendererPass
{

    const string prepassTag = "JsRP PrePass";

    public int m_ColorHandler;
    public int m_DepthHandler;
    public RenderTargetIdentifier m_ColorIdentifier;
    public RenderTargetIdentifier m_DepthIdentifier;

    public RenderTargetIdentifier colorRenderTarget { get { return m_ColorIdentifier; } }
    public RenderTargetIdentifier depthRenderTarget { get { return m_DepthIdentifier; } }

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "ForwardPrePass Setup"};
        Profiling.Profiler.BeginSample(prepassTag + " Setup");
        {
            SetupResources(cmd, ref renderingData);
            cmd.SetGlobalVector("_CameraNearFar", new Vector4(renderingData.cameraData.camera.nearClipPlane, renderingData.cameraData.camera.farClipPlane, 0, 0));
            context.ExecuteCommandBuffer(cmd);
        }
        Profiling.Profiler.EndSample();
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {

        CommandBuffer cmd = new CommandBuffer() { name = "ForwardPrePass Execute"};

        Profiling.Profiler.BeginSample(prepassTag + " Execute");
        {
            var drawSettings = new DrawingSettings(new ShaderTagId(prepassTag), new SortingSettings(renderingData.cameraData.camera));
            var filterSettings = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(renderingData.cameraData.cullingResults, ref drawSettings, ref filterSettings);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
        }
        Profiling.Profiler.EndSample();
    }

    void SetupResources(CommandBuffer cmd, ref RenderingData renderingData)
    {
        m_ColorHandler = Shader.PropertyToID("_CameraColorAttachment");
        m_DepthHandler = Shader.PropertyToID("_CameraDepthAttachment");
        m_ColorIdentifier = new RenderTargetIdentifier(m_ColorHandler);
        m_DepthIdentifier = new RenderTargetIdentifier(m_DepthHandler);

        cmd.GetTemporaryRT(m_ColorHandler, renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Default, 1, false);
        cmd.GetTemporaryRT(m_DepthHandler, renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Default, 1, false);

        cmd.SetGlobalTexture(m_ColorHandler, m_ColorIdentifier);
        cmd.SetGlobalTexture(m_DepthHandler, m_DepthIdentifier);

        cmd.SetRenderTarget(m_ColorIdentifier, m_DepthIdentifier);
        cmd.ClearRenderTarget(true, true, Color.clear, 1.0f);
    }
}


}
