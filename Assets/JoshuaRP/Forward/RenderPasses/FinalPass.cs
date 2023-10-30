using UnityEngine.Rendering;
namespace UnityEngine.Rendering.JsRP
{

public class FinalPass : RendererPass
{
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {

        CommandBuffer cmd = new CommandBuffer() { name = "FinalPass Execute"};

        Profiling.Profiler.BeginSample("FinalPass Execute");
        {
            cmd.Blit(m_ColorAttachments[0], BuiltinRenderTextureType.CameraTarget);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
        }
        Profiling.Profiler.EndSample();
    }
}


}
