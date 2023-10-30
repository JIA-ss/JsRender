using System.Collections.Generic;

namespace UnityEngine.Rendering.JsRP
{

    public class PostProcessingPass : RendererPass
    {
        PostProcessDataAsset postProcessData = null;
        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var pipilineAsset = GraphicsSettings.currentRenderPipeline as ForwardRenderPipelineAsset;
            postProcessData = pipilineAsset.m_postProcessData;
            if (postProcessData != null)
            {
                if (postProcessData.blur)
                {
                    BlurSetup(context, ref renderingData);
                }
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (postProcessData == null)
            {
                return;
            }

            if (postProcessData.blur)
            {
                BlurExecute(context, ref renderingData);
            }
        }

        public override void FrameCleanup(ScriptableRenderContext context)
        {
            if (postProcessData == null)
            {
                return;
            }

            if (postProcessData.blur)
            {
                BlurFrameCleanup(context);
            }
        }

        #region blur
        RTHandler blurRT1 = new RTHandler("blurRT1");
        RTHandler blurRT2 = new RTHandler("blurRT2");
        private void BlurSetup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            const string BlurSetupTag = "BlurSetup";
            var blurSetting = postProcessData.blurSetting;
            int RTWidth = (int)((float)renderingData.cameraData.camera.pixelWidth / blurSetting.downscaling);
            int RTHeight = (int)((float)renderingData.cameraData.camera.pixelHeight / blurSetting.downscaling);

            Profiling.Profiler.BeginSample(BlurSetupTag);
            CommandBuffer cmd = CommandBufferPool.Get(BlurSetupTag);
            cmd.GetTemporaryRT(blurRT1.id, new RenderTextureDescriptor(RTWidth, RTHeight));
            cmd.GetTemporaryRT(blurRT2.id, new RenderTextureDescriptor(RTWidth, RTHeight));
            cmd.SetGlobalVector("_TargetBlurRt_TexelSize", new Vector4(1.0f / (float)RTWidth, 1.0f / (float)RTHeight, RTWidth, RTHeight));
            cmd.Blit(m_ColorAttachments[0], blurRT1.id);
            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);

            Profiling.Profiler.EndSample();
        }
        private void BlurExecute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            const string BlurExecuteTag = "BlurExecute";
            Profiling.Profiler.BeginSample(BlurExecuteTag);
            CommandBuffer cmd = CommandBufferPool.Get(BlurExecuteTag);


            Shader blur = Shader.Find("JsRP/Forward/Gaussian");
            Material mat = new Material(blur);
            mat.SetFloat("_BlurRadius", postProcessData.blurSetting.blurRadius);

            switch (postProcessData.blurSetting.kernal)
            {
                case PostProcessDataAsset.BlurSetting.GaussKernal.e3x3:
                    mat.EnableKeyword("GAUSSIAN3x3");
                    break;
                case PostProcessDataAsset.BlurSetting.GaussKernal.e5x5:
                    mat.EnableKeyword("GAUSSIAN5x5");
                    break;
                case PostProcessDataAsset.BlurSetting.GaussKernal.e9x9:
                    mat.EnableKeyword("GAUSSIAN9x9");
                    break;
                case PostProcessDataAsset.BlurSetting.GaussKernal.eCunstom:
                    mat.EnableKeyword("GAUSSIANCUSTOM");
                    break;
            }

            for (int i = 0; i < postProcessData.blurSetting.iteration; i++)
            {
                cmd.Blit(blurRT1.id, blurRT2.id, mat, 1);
                cmd.Blit(blurRT2.id, blurRT1.id, mat, 2);
            }

            cmd.Blit(blurRT1.id, m_ColorAttachments[0]);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();

            CommandBufferPool.Release(cmd);
            Profiling.Profiler.EndSample();
        }
        private void BlurFrameCleanup(ScriptableRenderContext context)
        {
            const string BlurCleanupTag = "BlurCleanup";
            Profiling.Profiler.BeginSample(BlurCleanupTag);
            CommandBuffer cmd = CommandBufferPool.Get(BlurCleanupTag);
            cmd.ReleaseTemporaryRT(blurRT1.id);
            cmd.ReleaseTemporaryRT(blurRT2.id);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            Profiling.Profiler.EndSample();
            CommandBufferPool.Release(cmd);
        }
        #endregion
    }
}
