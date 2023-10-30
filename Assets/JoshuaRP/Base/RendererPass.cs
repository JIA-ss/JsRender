namespace UnityEngine.Rendering.JsRP
{

public abstract class RendererPass
{
    public virtual void Setup(ScriptableRenderContext context, ref RenderingData renderingData) {}
    public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);
    public virtual void FrameCleanup(ScriptableRenderContext context) {}
    public virtual void ConfigureTarget(RenderTargetIdentifier[] colors, RenderTargetIdentifier depth)
    {
        m_ColorAttachments = colors;
        m_DepthAttachment = depth;
    }
    public virtual void ConfigureTarget(RenderTargetIdentifier color, RenderTargetIdentifier depth)
    {
        m_ColorAttachments = new RenderTargetIdentifier[] { color };
        m_DepthAttachment = depth;
    }

    protected RenderTargetIdentifier[] m_ColorAttachments;
    protected RenderTargetIdentifier m_DepthAttachment;
}

}