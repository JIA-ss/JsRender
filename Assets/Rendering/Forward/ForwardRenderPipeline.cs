using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

public class ForwardRenderPipeline : BaseRenderPipeline
{
    public ForwardRenderPipeline(ForwardRenderPipelineAsset asset, List<RenderPassBase> renderPasses)
    {
        m_Renderer = new ForwardRenderer(renderPasses);
    }
}
