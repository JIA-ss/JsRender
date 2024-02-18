using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
public class BaseRenderPipeline : RenderPipeline
{
    public static class PerFrameBuffer
    {
        public static int _GlossyEnvironmentColor = Shader.PropertyToID("_GlossyEnvironmentColor");
        public static int _SubtractiveShadowColor = Shader.PropertyToID("_SubtractiveShadowColor");

        public static int _Time = Shader.PropertyToID("_Time");
        public static int _SinTime = Shader.PropertyToID("_SinTime");
        public static int _CosTime = Shader.PropertyToID("_CosTime");
        public static int unity_DeltaTime = Shader.PropertyToID("unity_DeltaTime");
        public static int _TimeParameters = Shader.PropertyToID("_TimeParameters");
    }
    static public BaseRenderer Renderer { get => m_Renderer; }
    static protected BaseRenderer m_Renderer = null;
    protected const string k_ClearCameraTag = "Clear Screen";

    // Amount of Lights that can be shaded per object (in the for loop in the shader)
    public static int maxPerObjectLights
    {
        // No support to bitfield mask and int[] in gles2. Can't index fast more than 4 lights.
        // Check Lighting.hlsl for more details.
        get => (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2) ? 4 : 8;
    }
    static List<Vector4> m_ShadowBiasData = new List<Vector4>();
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        if (cameras == null || cameras.Length == 0)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_ClearCameraTag);
            cmd.ClearRenderTarget(true, true, Color.black);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            context.Submit();
            return;
        }

        BeginFrameRendering(context, cameras);
        {
            UpdateCameraTemporaryData(cameras);
            GraphicsSettings.lightsUseLinearIntensity = (QualitySettings.activeColorSpace == ColorSpace.Linear);

            SetupPerFrameShaderConstants();
            SortCameras(cameras);
            RenderCameras(context, cameras);
        }
        EndFrameRendering(context, cameras);
    }

    public static void UpdateCameraTemporaryData(Camera[] cameras)
    {
        // todo: taa or motion vector
    }

    public static BaseRenderPipelineAsset asset { get => GraphicsSettings.currentRenderPipeline as BaseRenderPipelineAsset; }
    
    static void SetupPerFrameShaderConstants()
    {
        // When glossy reflections are OFF in the shader we set a constant color to use as indirect specular
        SphericalHarmonicsL2 ambientSH = RenderSettings.ambientProbe;
        Color linearGlossyEnvColor = new Color(ambientSH[0, 0], ambientSH[1, 0], ambientSH[2, 0]) * RenderSettings.reflectionIntensity;
        Color glossyEnvColor = CoreUtils.ConvertLinearToActiveColorSpace(linearGlossyEnvColor);
        Shader.SetGlobalVector(PerFrameBuffer._GlossyEnvironmentColor, glossyEnvColor);

        // Used when subtractive mode is selected
        Shader.SetGlobalVector(PerFrameBuffer._SubtractiveShadowColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.subtractiveShadowColor));
    }

    static void SortCameras(Camera[] cameras)
    {
        if (cameras.Length > 1)
        {
            Array.Sort(cameras, (camera1, camera2) => { return (int) camera1.depth - (int) camera2.depth; });
        }
    }

    void RenderCameras(ScriptableRenderContext context, Camera[] cameras)
    {
        for (int i = 0; i < cameras.Length; ++i)
        {
            var camera = cameras[i];
            if (IsGameCamera(camera))
            {
                RenderCameraStack(context, camera);
            }
            else
            {
                BeginCameraRendering(context, camera);
                UpdateVolumeFramework(camera, null);
                RenderSingleCamera(context, camera);
                EndCameraRendering(context, camera);
            }
        }
    }
    
    public static bool IsGameCamera(Camera camera)
    {
        if (camera == null)
            throw new ArgumentNullException("camera");

        return camera.cameraType == CameraType.Game || camera.cameraType == CameraType.VR;
    }
    
    static void RenderCameraStack(ScriptableRenderContext context, Camera baseCamera)
    {
        baseCamera.TryGetComponent<AdditionalCameraData>(out var baseCameraAdditionalData);

        // Overlay cameras will be rendered stacked while rendering base cameras
        if (baseCameraAdditionalData != null && baseCameraAdditionalData.renderType == CameraRenderType.Overlay)
            return;

        // renderer contains a stack if it has additional data and the renderer supports stacking
        var renderer = baseCameraAdditionalData?.renderer;
        bool supportsCameraStacking = renderer != null && renderer.supportedRenderingFeatures.cameraStacking;
        List<Camera> cameraStack = (supportsCameraStacking) ? baseCameraAdditionalData?.cameraStack : null;

        bool anyPostProcessingEnabled = baseCameraAdditionalData != null;

        // We need to know the last active camera in the stack to be able to resolve
        // rendering to screen when rendering it. The last camera in the stack is not
        // necessarily the last active one as it users might disable it.
        int lastActiveOverlayCameraIndex = GetFinalValidCameraIndexInCameraStack(cameraStack, baseCameraAdditionalData);


        bool isStackedRendering = lastActiveOverlayCameraIndex != -1;

        BeginCameraRendering(context, baseCamera);
        UpdateVolumeFramework(baseCamera, baseCameraAdditionalData);
        InitializeCameraData(baseCamera, baseCameraAdditionalData, !isStackedRendering, out var baseCameraData);
        RenderSingleCamera(context, baseCameraData, anyPostProcessingEnabled);
        EndCameraRendering(context, baseCamera);

        if (!isStackedRendering)
            return;

        for (int i = 0; i < cameraStack.Count; ++i)
        {
            var currCamera = cameraStack[i];

            if (!currCamera.isActiveAndEnabled)
                continue;

            currCamera.TryGetComponent<AdditionalCameraData>(out var currCameraData);
            // Camera is overlay and enabled
            if (currCameraData != null)
            {
                // Copy base settings from base camera data and initialize initialize remaining specific settings for this camera type.
                CameraData overlayCameraData = baseCameraData;
                bool lastCamera = i == lastActiveOverlayCameraIndex;

                BeginCameraRendering(context, currCamera);

                UpdateVolumeFramework(currCamera, currCameraData);
                InitializeAdditionalCameraData(currCamera, currCameraData, lastCamera, ref overlayCameraData);
                RenderSingleCamera(context, overlayCameraData, anyPostProcessingEnabled);
                EndCameraRendering(context, currCamera);
            }
        }
    }

    static int GetFinalValidCameraIndexInCameraStack(List<Camera> cameraStack, AdditionalCameraData baseCameraAdditionalData)
    {
        // We need to know the last active camera in the stack to be able to resolve
        // rendering to screen when rendering it. The last camera in the stack is not
        // necessarily the last active one as it users might disable it.
        int lastActiveOverlayCameraIndex = -1;
        if (cameraStack != null && cameraStack.Count > 0)
        {
            var baseCameraRendererType = baseCameraAdditionalData?.renderer.GetType();

            for (int i = 0; i < cameraStack.Count; ++i)
            {
                Camera currCamera = cameraStack[i];

                if (currCamera != null && currCamera.isActiveAndEnabled)
                {
                    currCamera.TryGetComponent<AdditionalCameraData>(out var data);

                    if (data == null || data.renderType != CameraRenderType.Overlay)
                    {
                        Debug.LogWarning(string.Format("Stack can only contain Overlay cameras. {0} will skip rendering.", currCamera.name));
                        continue;
                    }
                    
                    lastActiveOverlayCameraIndex = i;
                }
            }
        }
        return lastActiveOverlayCameraIndex;
    }
    
    static void UpdateVolumeFramework(Camera camera, AdditionalCameraData additionalCameraData)
    {
        // Default values when there's no additional camera data available
        LayerMask layerMask = 1; // "Default"
        Transform trigger = camera.transform;

        if (additionalCameraData != null)
        {
            layerMask = additionalCameraData.volumeLayerMask;
            trigger = additionalCameraData.volumeTrigger != null
                ? additionalCameraData.volumeTrigger
                : trigger;
        }
        else if (camera.cameraType == CameraType.SceneView)
        {
            // Try to mirror the MainCamera volume layer mask for the scene view - do not mirror the target
            var mainCamera = Camera.main;
            AdditionalCameraData mainAdditionalCameraData = null;

            if (mainCamera != null && mainCamera.TryGetComponent(out mainAdditionalCameraData))
                layerMask = mainAdditionalCameraData.volumeLayerMask;

            trigger = mainAdditionalCameraData != null && mainAdditionalCameraData.volumeTrigger != null ? mainAdditionalCameraData.volumeTrigger : trigger;
        }

        VolumeManager.instance.Update(trigger, layerMask);
    }
    
    static void InitializeCameraData(Camera camera, AdditionalCameraData additionalCameraData, bool resolveFinalTarget, out CameraData cameraData)
    {
        cameraData = new CameraData();
        InitializeStackedCameraData(camera, additionalCameraData, ref cameraData);
        InitializeAdditionalCameraData(camera, additionalCameraData, resolveFinalTarget, ref cameraData);
    }
    
    private static void InitScreenSize(int screenHeight)
    {
        // if (QualitySettings.resolutionScalingFixedDPIFactor >= 1 && screenHeight > 0)
        // {
        //     int minSize = Math.Min(Screen.height, Screen.width);
        //     float scale = (float)screenHeight / (float)minSize;
        //     float dpiScale = (Screen.dpi * scale) / 1000;
        //     QualitySettings.resolutionScalingFixedDPIFactor = dpiScale;
        // }
    }
    
    static bool HasAlphaChannel(GraphicsFormat format)
    {
        switch (format)
        {
            // Formats with an Alpha channel
            case GraphicsFormat.R8G8B8A8_SRGB:
            case GraphicsFormat.R8G8B8A8_UNorm:
            case GraphicsFormat.R8G8B8A8_SNorm:
            case GraphicsFormat.R8G8B8A8_UInt:
            case GraphicsFormat.R8G8B8A8_SInt:
            case GraphicsFormat.R16G16B16A16_UNorm:
            case GraphicsFormat.R16G16B16A16_SNorm:
            case GraphicsFormat.R16G16B16A16_UInt:
            case GraphicsFormat.R16G16B16A16_SInt:
            case GraphicsFormat.R32G32B32A32_UInt:
            case GraphicsFormat.R32G32B32A32_SInt:
            case GraphicsFormat.R32G32B32A32_SFloat:
            case GraphicsFormat.B8G8R8A8_SRGB:
            case GraphicsFormat.B8G8R8A8_UNorm:
            case GraphicsFormat.B8G8R8A8_SNorm:
            case GraphicsFormat.B8G8R8A8_UInt:
            case GraphicsFormat.B8G8R8A8_SInt:
            case GraphicsFormat.A2B10G10R10_UNormPack32:
            case GraphicsFormat.A2B10G10R10_UIntPack32:
            case GraphicsFormat.A2B10G10R10_SIntPack32:
            case GraphicsFormat.A2R10G10B10_UNormPack32:
            case GraphicsFormat.A2R10G10B10_UIntPack32:
            case GraphicsFormat.A2R10G10B10_SIntPack32:
                return true;

            // All other formats do not have an Alpha channel
            default:
                return false;
        }
    }
    
    static bool CameraNeedsAlpha(Camera camera)
    {
        if (camera.targetTexture != null)
        {
            return HasAlphaChannel(camera.targetTexture.graphicsFormat);
        }
        else
        {
            return false;
        }
    }
    
    static GraphicsFormat GetSupportHdrFormat(bool needsAlpha, HdrFormat hdrFormat)
    {
        GraphicsFormat currentHdrFormat;

        if (!needsAlpha && SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render) && hdrFormat == HdrFormat.R11G11B10)
            currentHdrFormat = GraphicsFormat.B10G11R11_UFloatPack32;
        else if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Linear | FormatUsage.Render))
            currentHdrFormat = GraphicsFormat.R16G16B16A16_SFloat;
        else
            currentHdrFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR); // This might actually be a LDR format on old devices.

        return currentHdrFormat;
    }
    static RenderTextureDescriptor CreateRenderTextureDescriptor(Camera camera, float renderScale, 
        bool isHdrEnabled, HdrFormat hdrFormat, int msaaSamples, bool needsAlpha)
    {
        RenderTextureDescriptor desc;
        GraphicsFormat renderTextureFormatDefault = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
        needsAlpha |= CameraNeedsAlpha(camera);
        GraphicsFormat graphicsFormat = isHdrEnabled ? GetSupportHdrFormat(needsAlpha, hdrFormat) : renderTextureFormatDefault;

        if (camera.targetTexture == null)
        {
            desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight);
            desc.width = (int)((float)desc.width * renderScale);
            desc.height = (int)((float)desc.height * renderScale);
        }
        else
        {
            desc = camera.targetTexture.descriptor;
        }

        if (camera.targetTexture != null)
        {
            desc.graphicsFormat = graphicsFormat;
            desc.depthBufferBits = camera.targetTexture.descriptor.depthBufferBits;
            desc.msaaSamples = camera.targetTexture.descriptor.msaaSamples;
            desc.sRGB = camera.targetTexture.descriptor.sRGB;
        }
        else
        {
            desc.graphicsFormat = graphicsFormat;
            desc.depthBufferBits = 32;
            desc.msaaSamples = msaaSamples;
            desc.sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear);
        }

        desc.enableRandomWrite = false;
        desc.bindMS = false;
        desc.useDynamicScale = camera.allowDynamicResolution;
        return desc;
    }
    
    /// <summary>
    /// Initialize camera data settings common for all cameras in the stack. Overlay cameras will inherit
    /// settings from base camera.
    /// </summary>
    /// <param name="baseCamera">Base camera to inherit settings from.</param>
    /// <param name="baseAdditionalCameraData">Component that contains additional base camera data.</param>
    /// <param name="cameraData">Camera data to initialize setttings.</param>
    static void InitializeStackedCameraData(Camera baseCamera, AdditionalCameraData baseAdditionalCameraData, ref CameraData cameraData)
    {
        var settings = asset;
        cameraData.targetTexture = baseCamera.targetTexture;
        cameraData.cameraType = baseCamera.cameraType;
        cameraData.isSceneViewCamera = cameraData.cameraType == CameraType.SceneView;

        baseCamera.depthTextureMode |= DepthTextureMode.Depth;
        // baseCamera.depthTextureMode |= DepthTextureMode.MotionVectors;

        InitScreenSize(settings.screenHeight);

        bool isSceneViewCamera = cameraData.isSceneViewCamera;


        ///////////////////////////////////////////////////////////////////
        // Environment and Post-processing settings                       /
        ///////////////////////////////////////////////////////////////////
        if (isSceneViewCamera)
        {
            cameraData.volumeLayerMask = 1; // "Default"
            cameraData.volumeTrigger = null;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
        }
        else if (baseAdditionalCameraData != null)
        {
            cameraData.volumeLayerMask = baseAdditionalCameraData.volumeLayerMask;
            cameraData.volumeTrigger = baseAdditionalCameraData.volumeTrigger == null ? baseCamera.transform : baseAdditionalCameraData.volumeTrigger;
            cameraData.antialiasing = baseAdditionalCameraData.antialiasing;
            cameraData.antialiasingQuality = baseAdditionalCameraData.antialiasingQuality;
        }
        else
        {
            cameraData.volumeLayerMask = 1; // "Default"
            cameraData.volumeTrigger = null;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
        }


        ///////////////////////////////////////////////////////////////////
        // Settings that control output of the camera                     /
        ///////////////////////////////////////////////////////////////////
        int msaaSamples = 1;
        if (baseCamera.allowMSAA && (int)settings.msaaQuality > 1)
            msaaSamples = (baseCamera.targetTexture != null) ? baseCamera.targetTexture.antiAliasing : (int)settings.msaaQuality;
        cameraData.isHdrEnabled = baseCamera.allowHDR && settings.supportsHDR;

        Rect cameraRect = baseCamera.rect;
        cameraData.pixelRect = baseCamera.pixelRect;
        cameraData.pixelWidth = baseCamera.pixelWidth;
        cameraData.pixelHeight = baseCamera.pixelHeight;
        cameraData.aspectRatio = (float)cameraData.pixelWidth / (float)cameraData.pixelHeight;
        cameraData.isDefaultViewport = (!(Math.Abs(cameraRect.x) > 0.0f || Math.Abs(cameraRect.y) > 0.0f ||
            Math.Abs(cameraRect.width) < 1.0f || Math.Abs(cameraRect.height) < 1.0f));

        // If XR is enabled, use XR renderScale.
        // Discard variations lesser than kRenderScaleThreshold.
        // Scale is only enabled for gameview.
        const float kRenderScaleThreshold = 0.05f;
        float usedRenderScale = XRGraphics.enabled ? XRGraphics.eyeTextureResolutionScale : settings.renderScale;
        cameraData.renderScale = (Mathf.Abs(1.0f - usedRenderScale) < kRenderScaleThreshold) ? 1.0f : usedRenderScale;
        var commonOpaqueFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.QuantizedFrontToBack;
        var noFrontToBackOpaqueFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;
        bool hasHSRGPU = SystemInfo.hasHiddenSurfaceRemovalOnGPU;
        bool canSkipFrontToBackSorting = (baseCamera.opaqueSortMode == OpaqueSortMode.Default && hasHSRGPU) || baseCamera.opaqueSortMode == OpaqueSortMode.NoDistanceSort;

        cameraData.defaultOpaqueSortFlags = canSkipFrontToBackSorting ? noFrontToBackOpaqueFlags : commonOpaqueFlags;
        cameraData.captureActions = CameraCaptureBridge.GetCaptureActions(baseCamera);

        bool needsAlphaChannel = Graphics.preserveFramebufferAlpha;
        cameraData.cameraTargetDescriptor = CreateRenderTextureDescriptor(baseCamera, cameraData.renderScale, 
            cameraData.isHdrEnabled, settings.hdrFormat, msaaSamples, needsAlphaChannel);
    }
    static void InitializeAdditionalCameraData(Camera camera, AdditionalCameraData additionalCameraData, bool resolveFinalTarget, ref CameraData cameraData)
    {
        var settings = asset;
        cameraData.camera = camera;
        
        // max shadow distance
        cameraData.maxShadowDistance = Mathf.Min(settings.shadowDistance, camera.farClipPlane);
        if (additionalCameraData != null && additionalCameraData.overrideShadowDistance)
            cameraData.maxShadowDistance = additionalCameraData.shadowDistance;
        cameraData.maxShadowDistance = cameraData.maxShadowDistance >= camera.nearClipPlane ? cameraData.maxShadowDistance : 0.0f;


        bool isSceneViewCamera = cameraData.isSceneViewCamera;
        if (isSceneViewCamera)
        {
            cameraData.renderType = CameraRenderType.Base;
            cameraData.clearDepth = true;
            cameraData.postProcessEnabled = CoreUtils.ArePostProcessesEnabled(camera);
            cameraData.renderer = Renderer;
        }
        else if (additionalCameraData != null)
        {
            cameraData.renderType = additionalCameraData.renderType;
            cameraData.clearDepth = (additionalCameraData.renderType != CameraRenderType.Base) ? additionalCameraData.clearDepth : true;
            cameraData.postProcessEnabled = additionalCameraData.renderPostProcessing;
            cameraData.maxShadowDistance = (additionalCameraData.renderShadows) ? cameraData.maxShadowDistance : 0.0f;
            cameraData.renderer = Renderer;
        }
        else
        {
            cameraData.renderType = CameraRenderType.Base;
            cameraData.clearDepth = true;
            cameraData.postProcessEnabled = settings.enablePostProcessing;
            cameraData.renderer = Renderer;
        }

        cameraData.resolveFinalTarget = resolveFinalTarget;

        Matrix4x4 projectionMatrix = camera.projectionMatrix;

        // Overlay cameras inherit viewport from base.
        // If the viewport is different between them we might need to patch the projection to adjust aspect ratio
        // matrix to prevent squishing when rendering objects in overlay cameras.
        if (cameraData.renderType == CameraRenderType.Overlay && !camera.orthographic && cameraData.pixelRect != camera.pixelRect)
        {
            // m00 = (cotangent / aspect), therefore m00 * aspect gives us cotangent.
            float cotangent = camera.projectionMatrix.m00 * camera.aspect;

            // Get new m00 by dividing by base camera aspectRatio.
            float newCotangent = cotangent / cameraData.aspectRatio;
            projectionMatrix.m00 = newCotangent;
        }

        cameraData.SetViewAndProjectionMatrix(camera, projectionMatrix);
    }
    
    public static void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
    {
        AdditionalCameraData additionalCameraData = null;
        if (IsGameCamera(camera))
            camera.gameObject.TryGetComponent(out additionalCameraData);

        if (additionalCameraData != null && additionalCameraData.renderType != CameraRenderType.Base)
        {
            Debug.LogWarning("Only Base cameras can be rendered with standalone RenderSingleCamera. Camera will be skipped.");
            return;
        }

        InitializeCameraData(camera, additionalCameraData, true, out var cameraData);
        RenderSingleCamera(context, cameraData, cameraData.postProcessEnabled);
    }

    static void RenderSingleCamera(ScriptableRenderContext context, CameraData cameraData, bool anyPostProcessingEnabled)
    {
        Camera camera = cameraData.camera;
        var renderer = cameraData.renderer;
        if (renderer == null)
        {
            Debug.LogWarning(string.Format("Trying to render {0} with an invalid renderer. Camera rendering will be skipped.", camera.name));
            return;
        }

        if (!camera.TryGetCullingParameters(false, out var cullingParameters))
            return;
        
        bool isSceneViewCamera = cameraData.isSceneViewCamera;

        ProfilingSampler sampler = new ProfilingSampler(camera.name);
        CommandBuffer cmd = CommandBufferPool.Get(sampler.name);
        using (new ProfilingScope(cmd, sampler))
        {
            renderer.Clear(cameraData.renderType);
            renderer.SetupCullingParameters(ref cullingParameters, ref cameraData);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

#if UNITY_EDITOR
            // Emit scene view UI
            if (isSceneViewCamera)
            {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
            }
#endif

            var cullResults = context.Cull(ref cullingParameters);
            InitializeRenderingData(asset, ref cameraData, ref cullResults, anyPostProcessingEnabled, out var renderingData);
            
            renderer.Setup(context, ref renderingData);
            renderer.Execute(context, ref renderingData);
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
        
    }
    
    static void InitializeRenderingData(BaseRenderPipelineAsset settings, ref CameraData cameraData, ref CullingResults cullResults,
        bool anyPostProcessingEnabled, out RenderingData renderingData)
    {
        UnityEngine.Profiling.Profiler.BeginSample("Sorting Lights");
        var visibleLights = ProcessVisibleLights(cullResults.visibleLights, cullResults.visibleOffscreenVertexLights, settings.maxVisibleAdditionalLight);
        UnityEngine.Profiling.Profiler.EndSample();

        int mainLightIndex = GetMainLightIndex(settings, visibleLights);
        bool mainLightCastShadows = false;
        bool additionalLightsCastShadows = false;

        if (cameraData.maxShadowDistance > 0.0f)
        {
            mainLightCastShadows = (mainLightIndex != -1 && visibleLights[mainLightIndex].light != null &&
                                    visibleLights[mainLightIndex].light.shadows != LightShadows.None);

            // If additional lights are shaded per-pixel they cannot cast shadows
            if (settings.additionalLightsRenderingMode == LightRenderingMode.PerPixel)
            {
                for (int i = 0; i < visibleLights.Length; ++i)
                {
                    if (i == mainLightIndex)
                        continue;

                    Light light = visibleLights[i].light;

                    // UniversalRP doesn't support additional directional lights or point light shadows yet
                    if (visibleLights[i].lightType == LightType.Spot && light != null && light.shadows != LightShadows.None)
                    {
                        additionalLightsCastShadows = true;
                        break;
                    }
                }
            }
        }

        renderingData.cullResults = cullResults;
        renderingData.cameraData = cameraData;
        InitializeLightData(settings, cullResults, visibleLights, LightIndexRemapping, mainLightIndex, out renderingData.lightData);
        InitializeShadowData(settings, ref cameraData, visibleLights, mainLightCastShadows, additionalLightsCastShadows && !renderingData.lightData.shadeAdditionalLightsPerVertex, out renderingData.shadowData);
        // InitializeDecalData(settings, out renderingData.decalData);
        // InitializePostProcessingData(settings, out renderingData.postProcessingData);
        renderingData.perObjectData = GetPerObjectLightFlags(renderingData.lightData.additionalLightsCount);
        renderingData.postProcessingEnabled = anyPostProcessingEnabled;
    }
    
    static int[] LightSplitIndices = new int[(int)LightPriorityType.Baked + 1];
    static VisibleLight[] VisibleLightsCache = null;
    public static int[] LightIndexRemapping = null;
    public static int maxVisibleAdditionalLights = 256;

    
    static LightPriorityType GetLightPriority(Light light)
    {
        switch (light.type)
        {
            case LightType.Directional:
                return LightPriorityType.Directional;
            case LightType.Point:
                return LightPriorityType.Point;
            case LightType.Spot:
                return LightPriorityType.Spot;
            case LightType.Area:
                return LightPriorityType.Area;
            default:
                Debug.Assert(false);
                return LightPriorityType.Baked;
        }
    }
    static void RequireLightsCache(int maxCount)
    {
        if(LightIndexRemapping == null || LightIndexRemapping.Length < maxCount)
        {
            var lightsCount = Mathf.Min(256, ((maxCount + 63) / 64) * 64);
            LightIndexRemapping = new int[lightsCount];
        }
        if(VisibleLightsCache == null || VisibleLightsCache.Length < maxCount)
        {
            var lightsCount = Mathf.Min(256, ((maxCount + 63) / 64) * 64);
            VisibleLightsCache = new VisibleLight[lightsCount];
        }
    }
    
    // Main Light is always a directional light
    static int GetMainLightIndex(BaseRenderPipelineAsset settings, VisibleLightArray visibleLights)
    {
        int totalVisibleLights = visibleLights.Length;

        if (totalVisibleLights == 0 || settings.mainLightRenderingMode != LightRenderingMode.PerPixel)
            return -1;

        Light sunLight = RenderSettings.sun;
        int brightestDirectionalLightIndex = -1;
        float brightestLightIntensity = 0.0f;

        VisibleLight currVisibleLight = visibleLights[0];
        Light currLight = currVisibleLight.light;

        // Particle system lights have the light property as null. We sort lights so all particles lights
        // come last. Therefore, if first light is particle light then all lights are particle lights.
        // In this case we either have no main light or already found it.
        if (currLight == null)
            return -1;

        if (currLight == sunLight)
            return 0;

        // In case no shadow light is present we will return the brightest directional light
        if (currVisibleLight.lightType == LightType.Directional && currLight.intensity > brightestLightIntensity)
        {
            brightestLightIntensity = currLight.intensity;
            brightestDirectionalLightIndex = 0;
        }


        return brightestDirectionalLightIndex;
    }
    
    static VisibleLightArray ProcessVisibleLights(NativeArray<VisibleLight> visibleLights, NativeArray<VisibleLight> visibleOffscreenVertexLights, int maxVisibleAddLights)
    {
        if(visibleLights.Length == 0)
            return new VisibleLightArray(VisibleLightsCache, 0, 0);

        RequireLightsCache(visibleLights.Length + visibleOffscreenVertexLights.Length);

        var lightsCount = visibleLights.Length;
        NativeArray<VisibleLight>.Copy(visibleLights, 0, VisibleLightsCache, 0, lightsCount);

        for (int i = 0; i < lightsCount; ++i)
            LightIndexRemapping[i] = -1;


        for (int i = 0; i < LightSplitIndices.Length; i++)
            LightSplitIndices[i] = 0;

        for (int i = 0; i < lightsCount; ++i)
        {
            var l = VisibleLightsCache[i];
            LightSplitIndices[(int)GetLightPriority(l.light)]++;
        }

        int bakedCount = LightSplitIndices[LightSplitIndices.Length - 1];

        int LightSplitStartOffset = 0;
        for (int i = 0; i < LightSplitIndices.Length; ++i)
        {
            var count = LightSplitIndices[i];
            LightSplitIndices[i] = LightSplitStartOffset;
            LightSplitStartOffset += count;
        }


        for (int i = 0; i < lightsCount; ++i)
        {
            var l = visibleLights[i];
            var priority = GetLightPriority(l.light);
            int index = LightSplitIndices[(int)priority];
            LightSplitIndices[(int)priority]++;
            VisibleLightsCache[index] = l;
            LightIndexRemapping[index] = i;
        }

        return new VisibleLightArray(VisibleLightsCache, lightsCount, lightsCount - bakedCount);
    }
    
    
    static void InitializeLightData(BaseRenderPipelineAsset settings, CullingResults cullResults, VisibleLightArray visibleLights, int[] lightIndexRemapping, int mainLightIndex, out LightData lightData)
    {
        int maxPerObjectAdditionalLights = maxPerObjectLights;
        int maxVisibleAdditionalLights = settings.maxVisibleAdditionalLight;
        lightData = new LightData();
        lightData.mainLightIndex = mainLightIndex;

        if (settings.additionalLightsRenderingMode != LightRenderingMode.Disabled)
        {
            lightData.additionalLightsCount =
                Math.Min((mainLightIndex != -1) ? visibleLights.Length - 1 : visibleLights.Length, maxVisibleAdditionalLights);
            lightData.maxPerObjectAdditionalLightsCount = Math.Min(settings.additionalLightsPerObjectLimit, maxPerObjectAdditionalLights);
        }
        else
        {
            lightData.additionalLightsCount = 0;
            lightData.maxPerObjectAdditionalLightsCount = 0;
        }

        lightData.shadeAdditionalLightsPerVertex = settings.additionalLightsRenderingMode == LightRenderingMode.PerVertex;
        lightData.visibleLights = visibleLights;
        lightData.visibleLightRemapping = lightIndexRemapping;
        lightData.supportsMixedLighting = settings.supportsMixedLighting;

        lightData.visibleReflectionProbes = cullResults.visibleReflectionProbes;
        lightData.reflectionProbesCount = cullResults.visibleReflectionProbes.Length;
    }
    
    static void InitializeShadowData(BaseRenderPipelineAsset settings, ref CameraData cameraData, VisibleLightArray visibleLights, bool mainLightCastShadows, bool additionalLightsCastShadows, out ShadowData shadowData)
    {
        m_ShadowBiasData.Clear();

        for (int i = 0; i < visibleLights.Length; ++i)
        {
            Light light = visibleLights[i].light;
            m_ShadowBiasData.Add(new Vector4(light.shadowBias, light.shadowNormalBias, 0.0f, 0.0f));
        }

        shadowData = new ShadowData();
        shadowData.bias = m_ShadowBiasData;
        shadowData.supportsMainLightShadows = SystemInfo.supportsShadows && settings.supportsMainLightShadows && mainLightCastShadows;

        // We no longer use screen space shadows in URP.
        // This change allows us to have particles & transparent objects receive shadows.
        shadowData.requiresScreenSpaceShadowResolve = false;// shadowData.supportsMainLightShadows && supportsScreenSpaceShadows && settings.shadowCascadeOption != ShadowCascadesOption.NoCascades;

        var shadowCascadeOption = settings.shadowCascadeOption;

        var additionalCameraData = cameraData.camera.GetComponent<AdditionalCameraData>();
        var cascade2Split = settings.cascade2Split;
        var cascade4Split = settings.cascade4Split;
        if (additionalCameraData != null && additionalCameraData.overrideShadowCascade) {
            shadowCascadeOption = additionalCameraData.shadowCascadeOption;
            cascade2Split = additionalCameraData.cascade2Split;
            cascade4Split = additionalCameraData.cascade4Split;
        }

        int shadowCascadesCount;
        switch (shadowCascadeOption)
        {
            case ShadowCascadesOption.FourCascades:
                shadowCascadesCount = 4;
                break;

            case ShadowCascadesOption.TwoCascades:
                shadowCascadesCount = 2;
                break;

            default:
                shadowCascadesCount = 1;
                break;
        }


        shadowData.mainLightShadowCascadesCount = shadowCascadesCount;//(shadowData.requiresScreenSpaceShadowResolve) ? shadowCascadesCount : 1;


        shadowData.mainLightShadowmapWidth = (int)settings.mainLightShadowmapResolution;
        shadowData.mainLightShadowmapHeight = (int)settings.mainLightShadowmapResolution;

        if (additionalCameraData != null && additionalCameraData.overrideShadowResolutionFactor) {
            shadowData.mainLightShadowmapWidth = Mathf.Max(256, (int)((float)(shadowData.mainLightShadowmapWidth) * additionalCameraData.shadowResolutionFactor));
            shadowData.mainLightShadowmapHeight = Mathf.Max(256, (int)((float)(shadowData.mainLightShadowmapHeight) * additionalCameraData.shadowResolutionFactor));
        }


        switch (shadowData.mainLightShadowCascadesCount)
        {
            case 1:
                shadowData.mainLightShadowCascadesSplit = new Vector3(1.0f, 0.0f, 0.0f);
                break;

            case 2:
                shadowData.mainLightShadowCascadesSplit = new Vector3(cascade2Split, 1.0f, 0.0f);
                break;

            default:
                shadowData.mainLightShadowCascadesSplit = cascade4Split;
                break;
        }

        shadowData.supportsAdditionalLightShadows = SystemInfo.supportsShadows && settings.supportsAdditionalLightShadows && additionalLightsCastShadows;
        shadowData.additionalLightsShadowmapWidth = shadowData.additionalLightsShadowmapHeight = settings.additionalLightsShadowmapResolution;
        shadowData.supportsSoftShadows = settings.supportsSoftShadows && (shadowData.supportsMainLightShadows || shadowData.supportsAdditionalLightShadows);


        shadowData.shadowmapDepthBufferBits = 16;
    }
    
    static PerObjectData GetPerObjectLightFlags(int additionalLightsCount)
    {
        var configuration = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.LightData | PerObjectData.OcclusionProbe;

        if (additionalLightsCount > 0)
        {
            configuration |= PerObjectData.LightData;
            configuration |= PerObjectData.LightIndices;
        }

        return configuration;
    }
}
