using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.Rendering.JsRP
{

public class ForwardRenderer
{
    RenderingData m_RenderingData;
    ForwardPrePass m_ForwardPrePass = new ForwardPrePass();
    ShadowCasterPass m_ShadowCasterPass = new ShadowCasterPass();
    ForwardOpaquePass m_ForwardOpaquePass = new ForwardOpaquePass();
    SkyBoxAndGizmoRenderPass m_SkyBoxAndGizmoRenderPass = new SkyBoxAndGizmoRenderPass();
    ForwardTransparentPass m_ForwardTransparentPass = new ForwardTransparentPass();
    PostProcessingPass m_PostProcessingPass = new PostProcessingPass();
    FinalPass m_FinalPass = new FinalPass();

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        if (!InitRenderingData(context, camera))
        {
            return;
        }

        Clear(context, camera);

        m_ForwardPrePass.Setup(context, ref m_RenderingData);
        m_ForwardPrePass.Execute(context, ref m_RenderingData);

        m_ShadowCasterPass.Setup(context, ref m_RenderingData);
        m_ShadowCasterPass.Execute(context, ref m_RenderingData);

        m_ForwardOpaquePass.Setup(context, ref m_RenderingData);
        m_ForwardOpaquePass.ConfigureTarget(m_ForwardPrePass.m_ColorIdentifier, m_ForwardPrePass.m_DepthIdentifier);
        m_ForwardOpaquePass.Execute(context, ref m_RenderingData);

        m_SkyBoxAndGizmoRenderPass.Setup(context, ref m_RenderingData);
        m_SkyBoxAndGizmoRenderPass.Execute(context, ref m_RenderingData);


        m_ForwardTransparentPass.Setup(context, ref m_RenderingData);
        m_ForwardTransparentPass.ConfigureTarget(m_ForwardPrePass.m_ColorIdentifier, m_ForwardPrePass.m_DepthIdentifier);
        m_ForwardTransparentPass.Execute(context, ref m_RenderingData);

        m_PostProcessingPass.ConfigureTarget(m_ForwardPrePass.m_ColorIdentifier, m_ForwardPrePass.m_DepthIdentifier);
        m_PostProcessingPass.Setup(context, ref m_RenderingData);
        m_PostProcessingPass.Execute(context, ref m_RenderingData);

        m_FinalPass.Setup(context, ref m_RenderingData);
        // m_FinalPass.ConfigureTarget(m_ShadowCasterPass._ShadowMapIdentifier, m_ForwardPrePass.m_DepthIdentifier);
        m_FinalPass.ConfigureTarget(m_ForwardPrePass.m_ColorIdentifier, m_ForwardPrePass.m_DepthIdentifier);
        m_FinalPass.Execute(context, ref m_RenderingData);
        FrameCleanup(context);
    }

    bool InitRenderingData(ScriptableRenderContext context, Camera camera)
    {
        m_RenderingData.cameraData.camera = camera;

        context.SetupCameraProperties(camera);
        if (!camera.TryGetCullingParameters(out m_RenderingData.cameraData.cullingParameters))
        {
            return false;
        }
        m_RenderingData.cameraData.cullingResults = context.Cull(ref m_RenderingData.cameraData.cullingParameters);

        m_RenderingData.rtList.rts = new RTInfo[3];


        InitLightData(ref m_RenderingData.cameraData.cullingResults);
        return true;
    }

    void InitLightData(ref CullingResults cullingResults)
    {
        m_RenderingData.lightData.mainLightIndex = -1;
        for (int lightIndex = 0; lightIndex < cullingResults.visibleLights.Length; lightIndex++)
        {
            var visibleLight = cullingResults.visibleLights[lightIndex];
            var light = visibleLight.light;
            if (light == RenderSettings.sun)
            {
                m_RenderingData.lightData.mainLightIndex = lightIndex;
                m_RenderingData.lightData.mainLightIntensity = light.intensity;
            }
        }
    }

    void Clear(ScriptableRenderContext context, Camera camera)
    {

    }

    void FrameCleanup(ScriptableRenderContext context)
    {
        m_ForwardOpaquePass.FrameCleanup(context);
        m_SkyBoxAndGizmoRenderPass.FrameCleanup(context);
        m_ForwardTransparentPass.FrameCleanup(context);
        m_PostProcessingPass.FrameCleanup(context);
    }
}

}