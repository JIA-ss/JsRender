using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.Profiling;

public class JoshuaRenderPipeline_bak : RenderPipeline
{
    public RenderTexture gdepth;
    public RenderTexture[] gbuffers = new RenderTexture[4];
    RenderTargetIdentifier gdepthID;
    RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[4];

    RenderTexture lightRt;
    RenderTargetIdentifier lightRtId;

    public Material postProcessMat = null;
    public JoshuaRenderPipeline_bak()
    {
        gdepth  = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
        gbuffers[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        gbuffers[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
        gbuffers[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        gbuffers[3] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

        for(int i=0; i<4; i++)
        {
            gbufferID[i] = gbuffers[i];
        }
        lightRt = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        lightRtId = lightRt;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // main camera
        Camera camera = cameras[0];

        SetGlobalShader(camera);
        GbufferPass(context, camera);
        LightPass(context, camera);
        PostProcessPass(context, camera);
        DrawSkyBoxAndGizmo(context, camera);
    }

    void LightPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "lightpass";

        Material mat = new Material(Shader.Find("JoshuaRP/lightpass"));
        cmd.Blit(gbufferID[0], lightRtId, mat);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
    }

    void PostProcessPass(ScriptableRenderContext context, Camera camera)
    {
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "postprocess";
        if (!postProcessMat)
        {
            postProcessMat = new Material(Shader.Find("JoshuaRP/postprocess"));
        }
        cmd.Blit(lightRt, BuiltinRenderTextureType.CameraTarget, postProcessMat);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
    }

    void GbufferPass(ScriptableRenderContext context, Camera camera)
    {
        Profiler.BeginSample("gbufferDraw");

        context.SetupCameraProperties(camera);
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "gbuffer";

        //clear render target
        cmd.SetRenderTarget(gbufferID, gdepth);
        cmd.ClearRenderTarget(true, true, Color.clear);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        //culling
        camera.TryGetCullingParameters(out var cullingParameters);
        var cullingResults = context.Cull(ref cullingParameters);

        //config settings
        ShaderTagId shaderTagId = new ShaderTagId("gbuffer");
        SortingSettings sortingSettings = new SortingSettings(camera);
        DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;

        //draw
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
        context.Submit();

        Profiler.EndSample();
    }

    void DrawSkyBoxAndGizmo(ScriptableRenderContext context, Camera camera)
    {
        Profiler.BeginSample("skyboxAndGizmoDraw");
        context.DrawSkybox(camera);
#if UNITY_EDITOR
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
#endif
        context.Submit();
        Profiler.EndSample();
    }

    void SetGlobalShader(Camera camera)
    {
        Shader.SetGlobalTexture("_gdepth", gdepth);
        for(int i=0; i<4; i++)
        {
            Shader.SetGlobalTexture("_GT"+i, gbuffers[i]);
        }
        Shader.SetGlobalTexture("_lightRt", lightRt);

        // 设置相机矩阵
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        Matrix4x4 vpMatrix = projMatrix * viewMatrix;
        Matrix4x4 vpMatrixInv = vpMatrix.inverse;
        Shader.SetGlobalMatrix("_vpMatrix", vpMatrix);
        Shader.SetGlobalMatrix("_vpMatrixInv", vpMatrixInv);
    }
}
