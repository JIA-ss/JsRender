using UnityEngine;
using System.Collections.Generic;
namespace UnityEngine.Rendering.JsRP
{

public class ForwardTransparentPass : RendererPass
{

    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var pipilineAsset = GraphicsSettings.currentRenderPipeline as ForwardRenderPipelineAsset;
        var oit = pipilineAsset.m_OITType;

        switch (oit)
        {
        case ForwardRenderPipelineAsset.OITType.kDepthPeeling:
            DepthPeelingSetup(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kWeightedAverage:
            WeightedAverageSetup(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kWeightedSum:
            WeightedSumSetup(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kDepthWeighted:
            DepthWeightedSetup(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kThreePassWeighted:
            ThreeWeightedPassSetup(context, ref renderingData);
            break;
        default:
            break;
        }

    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var pipilineAsset = GraphicsSettings.currentRenderPipeline as ForwardRenderPipelineAsset;
        var oit = pipilineAsset.m_OITType;

        switch (oit)
        {
        case ForwardRenderPipelineAsset.OITType.kDepthPeeling:
            DepthPeelingExecute(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kNone:
            AlphaBlendExecute(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kWeightedSum:
            WeightedSumExecute(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kWeightedAverage:
            WeightedAverageExecute(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kDepthWeighted:
            DepthWeightedExecute(context, ref renderingData);
            break;
        case ForwardRenderPipelineAsset.OITType.kThreePassWeighted:
            ThreeWeightedPassExecute(context, ref renderingData);
            break;
        default:
            break;
        }

    }

    public override void FrameCleanup(ScriptableRenderContext context)
    {
        var pipilineAsset = GraphicsSettings.currentRenderPipeline as ForwardRenderPipelineAsset;
        var oit = pipilineAsset.m_OITType;

        switch (oit)
        {
        case ForwardRenderPipelineAsset.OITType.kDepthPeeling:
            DepthPeelingCleanup(context);
            break;
        case ForwardRenderPipelineAsset.OITType.kWeightedAverage:
            WeightedAverageCleanup(context);
            break;
        case ForwardRenderPipelineAsset.OITType.kDepthWeighted:
            DepthWeightedCleanup(context);
            break;
        case ForwardRenderPipelineAsset.OITType.kThreePassWeighted:
            ThreeWeightedPassCleanup(context);
            break;
        default:
            break;
        }

    }

#region DepthPeeling

    RTHandler[] m_DepthPeelingColorLayers = new RTHandler[10];
    RTHandler[] m_DepthPeelingDepthAttachments = new RTHandler[2];
    void DepthPeelingSetup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "DepthPeelingSetup" };

        RenderTextureDescriptor colorLayerDesc = new RenderTextureDescriptor(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
        colorLayerDesc.colorFormat = RenderTextureFormat.ARGBFloat;
        colorLayerDesc.depthBufferBits = 0;
        colorLayerDesc.msaaSamples = 1;
        colorLayerDesc.sRGB = false;
        colorLayerDesc.useMipMap = false;
        colorLayerDesc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
        for (int i = 0; i < m_DepthPeelingColorLayers.Length; i++)
        {
            m_DepthPeelingColorLayers[i] = new RTHandler("DepthPeelingColor" + i.ToString());
            cmd.GetTemporaryRT(m_DepthPeelingColorLayers[i].id, colorLayerDesc);
        }

        m_DepthPeelingDepthAttachments[0] = new RTHandler("DepthPeelingDepth0");
        m_DepthPeelingDepthAttachments[1] = new RTHandler("DepthPeelingDepth1");

        cmd.GetTemporaryRT(m_DepthPeelingDepthAttachments[0].id, renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Default, 1, false);
        cmd.GetTemporaryRT(m_DepthPeelingDepthAttachments[1].id, renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight, 24, FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Default, 1, false);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        cmd.CopyTexture(m_DepthAttachment, m_DepthPeelingDepthAttachments[0].identifier);
        // context.SetupCameraProperties(renderingData.cameraData.camera);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
    }

    void DepthPeelingExecute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "DepthPeelingSetup" };
        var screenSize = new Vector4(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight, 1.0f / renderingData.cameraData.camera.pixelWidth, 1.0f / renderingData.cameraData.camera.pixelHeight);
        cmd.SetGlobalVector("_ScreenSize", screenSize);
        context.SetupCameraProperties(renderingData.cameraData.camera);
        int attachmentIdx = 0;
        for (int i = 0; i < m_DepthPeelingColorLayers.Length; i++)
        {
            int lastAttachmentIdx = attachmentIdx;
            if (++attachmentIdx > 1)
            {
                attachmentIdx = 0;
            }

            cmd.SetRenderTarget(m_ColorAttachments[0], m_DepthPeelingDepthAttachments[attachmentIdx].identifier);
            cmd.ClearRenderTarget(true, false, Color.clear, 0.0f);
            cmd.SetGlobalTexture("_DepthPeelingLEqualDepth", m_DepthPeelingDepthAttachments[lastAttachmentIdx].identifier);
            context.ExecuteCommandBuffer(cmd);


            var drawSettings = new DrawingSettings(new ShaderTagId("DepthPeeling"), new SortingSettings(renderingData.cameraData.camera));
            var filterSettings = new FilteringSettings(RenderQueueRange.transparent);
            context.DrawRenderers(renderingData.cameraData.cullingResults, ref drawSettings, ref filterSettings);
            context.Submit();
            cmd.Clear();
        }
    }

    void DepthPeelingCleanup(ScriptableRenderContext context)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "DepthPeelingClean" };
        foreach (var layer in m_DepthPeelingColorLayers)
        {
            cmd.ReleaseTemporaryRT(layer.id);
        }
        cmd.ReleaseTemporaryRT(m_DepthPeelingDepthAttachments[0].id);
        cmd.ReleaseTemporaryRT(m_DepthPeelingDepthAttachments[1].id);
    }
#endregion

    void AlphaBlendExecute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        const string transparentTag = "AlphaBlend";
        Profiling.Profiler.BeginSample(transparentTag);


        CommandBuffer cmd = new CommandBuffer() { name = transparentTag };
        cmd.SetRenderTarget(m_ColorAttachments[0], m_DepthAttachment);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        Camera camera = renderingData.cameraData.camera;
        var drawSettings = new DrawingSettings(new ShaderTagId(transparentTag), new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent });
        var filterSettings = new FilteringSettings(RenderQueueRange.transparent);
        context.DrawRenderers(renderingData.cameraData.cullingResults, ref drawSettings, ref filterSettings);

        context.Submit();
        Profiling.Profiler.EndSample();
    }

    RTHandler m_WeightedSumColorAttachment;
    void WeightedSumSetup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiling.Profiler.BeginSample("WeightedSumSetup");
        m_WeightedSumColorAttachment = new RTHandler("OpaqueColor");
        CommandBuffer cmd = new CommandBuffer() { name = "WeightedSumSetup" };
        cmd.GetTemporaryRT(m_WeightedSumColorAttachment.id, renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Default, 1, false);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        Profiling.Profiler.EndSample();
    }

    void WeightedSumExecute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        const string transparentTag = "WeightedSum";
        Profiling.Profiler.BeginSample(transparentTag);

        CommandBuffer cmd = new CommandBuffer() { name = transparentTag };
        cmd.CopyTexture(m_ColorAttachments[0], m_WeightedSumColorAttachment.identifier);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
        cmd.Clear();
        cmd.SetRenderTarget(m_ColorAttachments[0], m_DepthAttachment);
        cmd.SetGlobalTexture("_OpaqueColor", m_WeightedSumColorAttachment.identifier);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        Camera camera = renderingData.cameraData.camera;
        var drawSettings = new DrawingSettings(new ShaderTagId(transparentTag), new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent });
        var filterSettings = new FilteringSettings(RenderQueueRange.transparent);
        context.DrawRenderers(renderingData.cameraData.cullingResults, ref drawSettings, ref filterSettings);

        context.Submit();
        Profiling.Profiler.EndSample();
    }


    RTHandler m_WeightedAverageAccumTexture;
    RTHandler m_WeightedAverageCountTexture;
    void WeightedAverageSetup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiling.Profiler.BeginSample("WeightedAverageSetup");
        CommandBuffer cmd = new CommandBuffer() { name = "WeightedAverageSetup" };

        m_WeightedAverageAccumTexture = new RTHandler("_WeightedAverageAccumTexture");
        m_WeightedAverageCountTexture = new RTHandler("_WeightedAverageCountTexture");

        RenderTextureDescriptor desc = new RenderTextureDescriptor(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
        desc.colorFormat = RenderTextureFormat.ARGBFloat;
        desc.depthBufferBits = 0;
        desc.msaaSamples = 1;
        desc.sRGB = true;
        desc.useMipMap = false;
        desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
        cmd.GetTemporaryRT(m_WeightedAverageAccumTexture.id, desc);

        desc.colorFormat = RenderTextureFormat.RFloat;
        desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
        cmd.GetTemporaryRT(m_WeightedAverageCountTexture.id, desc);

        cmd.SetGlobalTexture(m_WeightedAverageAccumTexture.name, m_WeightedAverageAccumTexture.identifier);
        cmd.SetGlobalTexture(m_WeightedAverageCountTexture.name, m_WeightedAverageCountTexture.identifier);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
        Profiling.Profiler.EndSample();
    }
    void WeightedAverageExecute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiling.Profiler.BeginSample("WeightedAverageExecute");
        CommandBuffer cmd = new CommandBuffer() { name = "WeightedAverageExecute" };
        cmd.SetRenderTarget(
            new RenderTargetIdentifier[2]
            {
                m_WeightedAverageAccumTexture.identifier,
                m_WeightedAverageCountTexture.identifier
            }, m_DepthAttachment);

        cmd.ClearRenderTarget(false, true, new Color(0,0,0,0));
        context.ExecuteCommandBuffer(cmd);

        var accumDrawingSetting = new DrawingSettings(new ShaderTagId("WeightedAverageAccum"), new SortingSettings(renderingData.cameraData.camera));
        var filterSetting = new FilteringSettings(RenderQueueRange.transparent);
        context.DrawRenderers(renderingData.cameraData.cullingResults, ref accumDrawingSetting, ref filterSetting);
        context.Submit();


        cmd.Clear();
        Material mat = new Material(Shader.Find("JsRP/Forward/Transparent"));
        // cmd.SetRenderTarget(m_ColorAttachments[0], m_DepthAttachment);
        // cmd.ClearRenderTarget(false, false, Color.clear);
        // cmd.DrawMesh(RenderUtil.fullscreenMesh, Matrix4x4.identity, mat, 0, 4);
        cmd.Blit(m_WeightedAverageAccumTexture.identifier, m_ColorAttachments[0], mat, 4);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
        Profiling.Profiler.EndSample();
    }
    void WeightedAverageCleanup(ScriptableRenderContext context)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "WeightedAverageClean" };
        cmd.ReleaseTemporaryRT(m_WeightedAverageAccumTexture.id);
        cmd.ReleaseTemporaryRT(m_WeightedAverageCountTexture.id);
    }



    void DepthWeightedSetup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        WeightedAverageSetup(context, ref renderingData);
        var pipilineAsset = GraphicsSettings.currentRenderPipeline as ForwardRenderPipelineAsset;
        CommandBuffer cmd = new CommandBuffer() { name = "enable depth weighted function" };
        for (int weighted_funcId = 0; weighted_funcId <= 4; weighted_funcId++)
        {
            if (weighted_funcId == pipilineAsset.m_depthWeightedFuncIdx)
            {
                cmd.EnableShaderKeyword("DEPTH_WEIGHTED_FUNC" + weighted_funcId.ToString());
            }
            else
            {
                cmd.DisableShaderKeyword("DEPTH_WEIGHTED_FUNC" + weighted_funcId.ToString());
            }
        }
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
    }

    void DepthWeightedExecute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiling.Profiler.BeginSample("DepthWeightedExecute");
        CommandBuffer cmd = new CommandBuffer() { name = "DepthWeightedExecute" };
        cmd.SetRenderTarget(
            new RenderTargetIdentifier[2]
            {
                m_WeightedAverageAccumTexture.identifier,
                m_WeightedAverageCountTexture.identifier
            }, m_DepthAttachment);
        cmd.ClearRenderTarget(false, true, new Color(0,0,0,1));
        context.ExecuteCommandBuffer(cmd);

        var accumDrawingSetting = new DrawingSettings(new ShaderTagId("DepthWeightedAccum"), new SortingSettings(renderingData.cameraData.camera));
        var filterSetting = new FilteringSettings(RenderQueueRange.transparent);
        context.DrawRenderers(renderingData.cameraData.cullingResults, ref accumDrawingSetting, ref filterSetting);
        context.Submit();


        cmd.Clear();
        Material mat = new Material(Shader.Find("JsRP/Forward/Transparent"));
        // cmd.SetRenderTarget(m_ColorAttachments[0], m_DepthAttachment);
        // cmd.ClearRenderTarget(false, false, Color.clear);
        // cmd.DrawMesh(RenderUtil.fullscreenMesh, Matrix4x4.identity, mat, 0, 4);
        cmd.Blit(m_WeightedAverageAccumTexture.identifier, m_ColorAttachments[0], mat, 6);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
        Profiling.Profiler.EndSample();
    }

    void DepthWeightedCleanup(ScriptableRenderContext context)
    {
        WeightedAverageCleanup(context);
    }


    RTHandler _ColorRGBA1;
    RTHandler _ColorRGBA2;
    RTHandler _ColorRGBA3;
    RTHandler _OpaqueColor;
    void ThreeWeightedPassSetup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiling.Profiler.BeginSample("ThreeWeightedPassSetup");
        {
            CommandBuffer cmd = new CommandBuffer() { name = "WeightedAverageSetup" };

            _ColorRGBA1 = new RTHandler("_ColorRGBA1");
            _ColorRGBA2 = new RTHandler("_ColorRGBA2");
            _ColorRGBA3 = new RTHandler("_ColorRGBA3");
            _OpaqueColor = new RTHandler("_OpaqueColor");

            RenderTextureDescriptor desc = new RenderTextureDescriptor(renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight);
            desc.colorFormat = RenderTextureFormat.ARGBFloat;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.sRGB = true;
            desc.useMipMap = false;
            desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            cmd.GetTemporaryRT(_ColorRGBA1.id, desc);
            cmd.GetTemporaryRT(_ColorRGBA2.id, desc);
            cmd.GetTemporaryRT(_ColorRGBA3.id, desc);
            cmd.GetTemporaryRT(_OpaqueColor.id, renderingData.cameraData.camera.pixelWidth, renderingData.cameraData.camera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Default, 1, false);

            cmd.SetGlobalTexture(_ColorRGBA1.name, _ColorRGBA1.identifier);
            cmd.SetGlobalTexture(_ColorRGBA2.name, _ColorRGBA2.identifier);
            cmd.SetGlobalTexture(_ColorRGBA3.name, _ColorRGBA3.identifier);
            cmd.SetGlobalTexture(_OpaqueColor.name, _OpaqueColor.identifier);

            context.ExecuteCommandBuffer(cmd);
            context.Submit();
        }
        Profiling.Profiler.EndSample();
    }

    void ThreeWeightedPassExecute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Profiling.Profiler.BeginSample("ThreeWeightedPassExecute");
        CommandBuffer cmd = new CommandBuffer() { name = "ThreeWeightedPassExecute" };
        cmd.SetRenderTarget(
            new RenderTargetIdentifier[2]
            {
                _ColorRGBA1.identifier,
                _ColorRGBA2.identifier
            }, m_DepthAttachment);

        cmd.ClearRenderTarget(false, true, new Color(0,0,0,0));
        context.ExecuteCommandBuffer(cmd);

        var pass1DrawingSetting = new DrawingSettings(new ShaderTagId("ThreePassWeightedPass1"), new SortingSettings(renderingData.cameraData.camera));
        var filterSetting = new FilteringSettings(RenderQueueRange.transparent);
        context.DrawRenderers(renderingData.cameraData.cullingResults, ref pass1DrawingSetting, ref filterSetting);
        context.Submit();


        cmd.Clear();
        cmd.SetRenderTarget(_ColorRGBA3.identifier, m_DepthAttachment);
        cmd.ClearRenderTarget(false, true, new Color(1.0f,1.0f,1.0f,1.0f));
        context.ExecuteCommandBuffer(cmd);

        var pass2DrawingSetting = new DrawingSettings(new ShaderTagId("ThreePassWeightedPass2"), new SortingSettings(renderingData.cameraData.camera));
        context.DrawRenderers(renderingData.cameraData.cullingResults, ref pass2DrawingSetting, ref filterSetting);
        context.Submit();

        cmd.Clear();
        cmd.CopyTexture(m_ColorAttachments[0], _OpaqueColor.identifier);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();


        cmd.Clear();
        Material mat = new Material(Shader.Find("JsRP/Forward/Transparent"));
        // cmd.SetRenderTarget(m_ColorAttachments[0], m_DepthAttachment);
        // cmd.ClearRenderTarget(false, true, Color.clear);
        // cmd.DrawMesh(RenderUtil.fullscreenMesh, Matrix4x4.identity, mat, 0, 10);
        cmd.Blit(_OpaqueColor.identifier, m_ColorAttachments[0], mat, 10);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
        Profiling.Profiler.EndSample();
    }

    void ThreeWeightedPassCleanup(ScriptableRenderContext context)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "ThreeWeightedPassCleanup" };
        cmd.ReleaseTemporaryRT(_ColorRGBA1.id);
        cmd.ReleaseTemporaryRT(_ColorRGBA2.id);
        cmd.ReleaseTemporaryRT(_ColorRGBA3.id);
        cmd.ReleaseTemporaryRT(_OpaqueColor.id);
    }
}


}
