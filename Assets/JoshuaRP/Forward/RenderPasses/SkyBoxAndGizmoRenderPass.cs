

namespace UnityEngine.Rendering.JsRP
{

public class SkyBoxAndGizmoRenderPass : RendererPass
{
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiling.Profiler.BeginSample("SkyBoxAndGizmoRenderPass");
        context.DrawSkybox(renderingData.cameraData.camera);
#if UNITY_EDITOR
        if (UnityEditor.Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(renderingData.cameraData.camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(renderingData.cameraData.camera, GizmoSubset.PostImageEffects);
        }
#endif
        context.Submit();
        Profiling.Profiler.EndSample();
    }
}

}