using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class BaseRenderer
{
    public class RenderingFeatures
    {
        /// <summary>
        /// This setting controls if the camera editor should display the camera stack category.
        /// Renderers that don't support camera stacking will only render camera of type CameraRenderType.Base
        /// <see cref="CameraRenderType"/>
        /// <seealso cref="UniversalAdditionalCameraData.cameraStack"/>
        /// </summary>
        public bool cameraStacking { get; set; } = false;
    }

    protected const string k_SetCameraRenderStateTag = "Set Camera Data";
    protected const string k_SetRenderTarget = "Set RenderTarget";
    protected const string k_ReleaseResourcesTag = "Release Resources";
    protected const string k_SetupLightConstants = "Setup Light Constants";
    
    /// <summary>
    /// Supported rendering features by this renderer.
    /// <see cref="SupportedRenderingFeatures"/>
    /// </summary>
    public RenderingFeatures supportedRenderingFeatures { get; set; } = new RenderingFeatures();
    public RenderTexture targetTexture;
    protected RenderingData m_RenderingData = new RenderingData();
    protected List<RenderPassBase> m_RenderPasses = new List<RenderPassBase>();
    public abstract void Setup(ScriptableRenderContext context, ref RenderingData renderingData);

    public virtual void ExecuteRenderPass(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ref CameraData cameraData = ref renderingData.cameraData;
        Camera camera = cameraData.camera;
        foreach (RenderPassBase renderPass in m_RenderPasses)
        {
            CommandBuffer cmd = CommandBufferPool.Get(renderPass.GetProfilerTag() + k_SetRenderTarget);
            renderPass.ConfigureWithTag(cmd, cameraData.cameraTargetDescriptor);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            renderPass.ExecuteWithTag(context, ref renderingData);
        }
    }
    public virtual void FinishRendering(CommandBuffer cmd)
    {
    }
    public virtual void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(k_SetupLightConstants);
        VisibleLight mainLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];
        Vector4 dir = -mainLight.localToWorldMatrix.GetColumn(2);
        dir = -mainLight.light.gameObject.transform.forward;
        cmd.SetGlobalVector(ShaderID._MainLightPosition, new Vector4(dir.x, dir.y, dir.z, 0.0f));
        cmd.SetGlobalVector(ShaderID._MainLightColor, mainLight.finalColor);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
    protected virtual void ClearRenderingState(CommandBuffer cmd)
    {
        // Reset per-camera shader keywords. They are enabled depending on which render passes are executed.
        cmd.DisableShaderKeyword(ShaderKeywordStrings.MainLightShadows);
        cmd.DisableShaderKeyword(ShaderKeywordStrings.MainLightShadowCascades);
        cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightsVertex);
        cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightsPixel);
        cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightShadows);
        cmd.DisableShaderKeyword(ShaderKeywordStrings.SoftShadows);
        cmd.DisableShaderKeyword(ShaderKeywordStrings.MixedLightingSubtractive);
        cmd.DisableShaderKeyword(ShaderKeywordStrings.LinearToSRGBConversion);
    }
    
    public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        ref CameraData cameraData = ref renderingData.cameraData;
        Camera camera = cameraData.camera;

        CommandBuffer cmd = CommandBufferPool.Get(k_SetCameraRenderStateTag);

        // Cache the time for after the call to `SetupCameraProperties` and set the time variables in shader
        // For now we set the time variables per camera, as we plan to remove `SetupCameraProperties`.
        // Setting the time per frame would take API changes to pass the variable to each camera render.
        // Once `SetupCameraProperties` is gone, the variable should be set higher in the call-stack.
#if UNITY_EDITOR
        float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
#else
        float time = Time.time;
#endif
        float deltaTime = Time.deltaTime;
        float smoothDeltaTime = Time.smoothDeltaTime;

        // Initialize Camera Render State
        ClearRenderingState(cmd);
        SetPerCameraShaderVariables(cmd, ref cameraData);
        SetMipmapBiasValues(cmd, cameraData);
        // SetShaderTimeValues(cmd, time, deltaTime, smoothDeltaTime);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        SetupLights(context, ref renderingData);
        
        {
            context.SetupCameraProperties(camera: camera,stereoSetup: false, eye: 0);
            SetCameraMatrices(cmd, ref cameraData, true);

            // Reset shader time variables as they were overridden in SetupCameraProperties. If we don't do it we might have a mismatch between shadows and main rendering
            SetShaderTimeValues(cmd, time, deltaTime, smoothDeltaTime);
            ExecuteRenderPass(context, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

        }
        
        InternalFinishRendering(context, cameraData.resolveFinalTarget);
        CommandBufferPool.Release(cmd);
    }
    
    public virtual void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters,
        ref CameraData cameraData)
    {
    }
    
    public void Clear(CameraRenderType cameraType)
    {
        m_ActiveColorAttachments[0] = BuiltinRenderTextureType.CameraTarget;
        for (int i = 1; i < m_ActiveColorAttachments.Length; ++i)
            m_ActiveColorAttachments[i] = 0;

        m_ActiveDepthAttachment = BuiltinRenderTextureType.CameraTarget;
        m_CameraColorTarget = BuiltinRenderTextureType.CameraTarget;
        m_CameraDepthTarget = BuiltinRenderTextureType.CameraTarget;
    }
    
    void SetPerCameraShaderVariables(CommandBuffer cmd, ref CameraData cameraData)
    {
        Camera camera = cameraData.camera;

        Rect pixelRect = cameraData.pixelRect;
        float scaledCameraWidth = (float)cameraData.cameraTargetDescriptor.width;
        float scaledCameraHeight = (float)cameraData.cameraTargetDescriptor.height;
        float cameraWidth = (float)pixelRect.width;
        float cameraHeight = (float)pixelRect.height;

        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;
        float invNear = Mathf.Approximately(near, 0.0f) ? 0.0f : 1.0f / near;
        float invFar = Mathf.Approximately(far, 0.0f) ? 0.0f : 1.0f / far;
        float isOrthographic = camera.orthographic ? 1.0f : 0.0f;

        // From http://www.humus.name/temp/Linearize%20depth.txt
        // But as depth component textures on OpenGL always return in 0..1 range (as in D3D), we have to use
        // the same constants for both D3D and OpenGL here.
        // OpenGL would be this:
        // zc0 = (1.0 - far / near) / 2.0;
        // zc1 = (1.0 + far / near) / 2.0;
        // D3D is this:
        float zc0 = 1.0f - far * invNear;
        float zc1 = far * invNear;

        Vector4 zBufferParams = new Vector4(zc0, zc1, zc0 * invFar, zc1 * invFar);

        if (SystemInfo.usesReversedZBuffer)
        {
            zBufferParams.y += zBufferParams.x;
            zBufferParams.x = -zBufferParams.x;
            zBufferParams.w += zBufferParams.z;
            zBufferParams.z = -zBufferParams.z;
        }

        // Projection flip sign logic is very deep in GfxDevice::SetInvertProjectionMatrix
        // For now we don't deal with _ProjectionParams.x and let SetupCameraProperties handle it.
        // We need to enable this when we remove SetupCameraProperties
        // float projectionFlipSign = ???
        // Vector4 projectionParams = new Vector4(projectionFlipSign, near, far, 1.0f * invFar);
        // cmd.SetGlobalVector(ShaderPropertyId.projectionParams, projectionParams);

        Vector4 orthoParams = new Vector4(camera.orthographicSize * cameraData.aspectRatio, camera.orthographicSize, 0.0f, isOrthographic);

        // Camera and Screen variables as described in https://docs.unity3d.com/Manual/SL-UnityShaderVariables.html
        cmd.SetGlobalVector(ShaderPropertyId.worldSpaceCameraPos, camera.transform.position);
        cmd.SetGlobalVector(ShaderPropertyId.screenParams, new Vector4(cameraWidth, cameraHeight, 1.0f + 1.0f / cameraWidth, 1.0f + 1.0f / cameraHeight));
        cmd.SetGlobalVector(ShaderPropertyId.scaledScreenParams, new Vector4(scaledCameraWidth, scaledCameraHeight, 1.0f + 1.0f / scaledCameraWidth, 1.0f + 1.0f / scaledCameraHeight));
        cmd.SetGlobalVector(ShaderPropertyId.zBufferParams, zBufferParams);
        cmd.SetGlobalVector(ShaderPropertyId.orthoParams, orthoParams);
    }
    
    void SetMipmapBiasValues(CommandBuffer cmd, CameraData cameraData)
    {
        float mipmapBias = 0;
        cmd.SetGlobalFloat(ShaderPropertyId.mipmapBias, mipmapBias);
    }
    
    void SetShaderTimeValues(CommandBuffer cmd, float time, float deltaTime, float smoothDeltaTime)
    {
        float timeEights = time / 8f;
        float timeFourth = time / 4f;
        float timeHalf = time / 2f;

        // Time values
        Vector4 timeVector = time * new Vector4(1f / 20f, 1f, 2f, 3f);
        Vector4 sinTimeVector = new Vector4(Mathf.Sin(timeEights), Mathf.Sin(timeFourth), Mathf.Sin(timeHalf), Mathf.Sin(time));
        Vector4 cosTimeVector = new Vector4(Mathf.Cos(timeEights), Mathf.Cos(timeFourth), Mathf.Cos(timeHalf), Mathf.Cos(time));
        Vector4 deltaTimeVector = new Vector4(deltaTime, 1f / deltaTime, smoothDeltaTime, 1f / smoothDeltaTime);
        Vector4 timeParametersVector = new Vector4(time, Mathf.Sin(time), Mathf.Cos(time), 0.0f);

        cmd.SetGlobalVector(BaseRenderPipeline.PerFrameBuffer._Time, timeVector);
        cmd.SetGlobalVector(BaseRenderPipeline.PerFrameBuffer._SinTime, sinTimeVector);
        cmd.SetGlobalVector(BaseRenderPipeline.PerFrameBuffer._CosTime, cosTimeVector);
        cmd.SetGlobalVector(BaseRenderPipeline.PerFrameBuffer.unity_DeltaTime, deltaTimeVector);
        cmd.SetGlobalVector(BaseRenderPipeline.PerFrameBuffer._TimeParameters, timeParametersVector);
    }
    
    public void SetCameraMatrices(CommandBuffer cmd, ref CameraData cameraData, bool setInverseMatrices)
    {
        Matrix4x4 viewMatrix = cameraData.m_ViewMatrix;
        Matrix4x4 projectionMatrix = cameraData.m_ProjectionMatrix;

        // TODO: Investigate why SetViewAndProjectionMatrices is causing y-flip / winding order issue
        // for now using cmd.SetViewProjecionMatrices
        //SetViewAndProjectionMatrices(cmd, viewMatrix, cameraData.GetDeviceProjectionMatrix(), setInverseMatrices);
        cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);

        if (setInverseMatrices)
        {
            Matrix4x4 inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.cameraToWorldMatrix, inverseViewMatrix);

            // cmd.SetGlobalMatrix(ShaderPropertyId.prevViewProjMatrix, cameraData.GetPrevViewProjMatrix());
            // cmd.SetGlobalMatrix(ShaderPropertyId.nonJitteredViewProjMatrix, cameraData.GetGPUNonJitteredProjectionMatrix() * viewMatrix);

            // UniversalRenderPipeline.CameraTemporaryData cameraTemporaryData = UniversalRenderPipeline.cameraTemporaryDataMap[cameraData.camera];
            // cmd.SetGlobalVector(ShaderPropertyId.currentJitter, cameraTemporaryData.currentJitter);
            // cmd.SetGlobalVector(ShaderPropertyId.prevJitter, cameraTemporaryData.prevJitter);

            Matrix4x4 viewAndProjectionMatrix = GL.GetGPUProjectionMatrix(cameraData.m_ProjectionMatrix, IsCameraProjectionMatrixFlipped()) * viewMatrix;
            Matrix4x4 inverseViewProjection = Matrix4x4.Inverse(viewAndProjectionMatrix);
            cmd.SetGlobalMatrix(ShaderPropertyId.inverseViewAndProjectionMatrix, inverseViewProjection);
        }

        // TODO: missing unity_CameraWorldClipPlanes[6], currently set by context.SetupCameraProperties
    }
    
    private bool IsCameraProjectionMatrixFlipped()
    {
        bool renderingToTexture = m_CameraColorTarget != BuiltinRenderTextureType.CameraTarget || targetTexture != null;
        return SystemInfo.graphicsUVStartsAtTop && renderingToTexture;
    }
    
    private void InternalFinishRendering(ScriptableRenderContext context, bool resolveFinalTarget)
    {
        CommandBuffer cmd = CommandBufferPool.Get(k_ReleaseResourcesTag);

        for (int i = 0; i < m_RenderPasses.Count; ++i)
            m_RenderPasses[i].FrameCleanup(cmd);

        // Happens when rendering the last camera in the camera stack.
        if (resolveFinalTarget)
        {
            for (int i = 0; i < m_RenderPasses.Count; ++i)
                m_RenderPasses[i].OnFinishCameraStackRendering(cmd);

            FinishRendering(cmd);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    protected ClearFlag GetCameraClearFlag(ref CameraData cameraData)
    {
        var cameraClearFlags = cameraData.camera.clearFlags;
        if (cameraData.renderType == CameraRenderType.Overlay)
            return (cameraData.clearDepth) ? ClearFlag.Depth : ClearFlag.None;

        // Always clear on first render pass in mobile as it's same perf of DontCare and avoid tile clearing issues.
        if (Application.isMobilePlatform)
            return ClearFlag.All;
        
        if ((cameraClearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null) ||
            cameraClearFlags == CameraClearFlags.Nothing)
            return ClearFlag.Depth;

        return ClearFlag.All;
    }
    
    protected static RenderTargetIdentifier[] m_ActiveColorAttachments = new RenderTargetIdentifier[]{0, 0, 0, 0, 0, 0, 0, 0 };
    protected static RenderTargetIdentifier m_ActiveDepthAttachment;
    protected RenderTargetIdentifier m_CameraColorTarget;
    protected RenderTargetIdentifier m_CameraDepthTarget;
}