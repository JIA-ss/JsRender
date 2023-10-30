using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

namespace UnityEngine.Rendering.JsRP
{
    public class ForwardRenderPipeline : RenderPipeline
    {
        ForwardRenderer m_Renderer;
        public ForwardRenderPipeline(ForwardRenderPipelineAsset asset)
        {
            m_Renderer = new ForwardRenderer();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            m_Renderer.Render(context, cameras[0]);
        }
    }

}
