using System.Collections.Generic;
namespace UnityEngine.Rendering.JsRP
{

public class ShadowCasterPass : RendererPass
{

    struct CascadeSliceData
    {
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;
        public Matrix4x4 shadowTransform;
        public Vector4 cullingSphere;
        public int offsetX;
        public int offsetY;
        public int resolution;
    };

    public Matrix4x4 _MainLightView;
    public Matrix4x4 _MainLightProj;
    public Matrix4x4 _WorldToMainLightShadowmapMatrix;
    public ShadowSplitData _ShadowSplitData;
    public int _ShadowMapHandler;
    public int _ShadowMapBlurHandler;
    public RenderTargetIdentifier _ShadowMapIdentifier;
    public RenderTargetIdentifier _ShadowMapBlurIdentifier;
    public Light m_MainLight;
    const string SHADOWMAP_NAME = "_ShadowMap";
    const int SHADOWMAP_RESOLUTION = 2048;
    const float SHADOW_NEARPLANE = 0.2f;

    const int CASCADE_NUM = 4;
    Vector3 CASCADE_SPLIT = new Vector3(0.02f, 0.1f, 0.5f);

    CascadeSliceData[] _CascadeSliceData = new CascadeSliceData[CASCADE_NUM];
    ShadowSplitData[] _CascadeSplitData = new ShadowSplitData[CASCADE_NUM];

    bool m_valid = true;
    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!GetLightMatrix(ref renderingData))
        {
            Debug.Log("GetLightMatrix failed");
            m_valid = false;
            return;
        }
        m_valid = true;

        Profiling.Profiler.BeginSample("shadow depth setup");
        CommandBuffer cmd = new CommandBuffer() { name = "shadow depth setup" };
        {
            SetupResources(cmd, ref renderingData);
            SetupShader(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
        }
        cmd.Clear();
        Profiling.Profiler.EndSample();
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        const string shadowDepthTag = "JsRP ShadowCaster";

        if (!m_valid || renderingData.lightData.mainLightIndex == -1)
        {
            return;
        }

        CommandBuffer cmd = new CommandBuffer() { name = shadowDepthTag };
        {
            Profiling.Profiler.BeginSample(shadowDepthTag);
            ShadowDrawingSettings drawingSettings = new ShadowDrawingSettings(renderingData.cameraData.cullingResults, renderingData.lightData.mainLightIndex);
            {
                // cmd.EnableShaderKeyword("GAUSSIAN5x5");
                cmd.SetRenderTarget(_ShadowMapIdentifier);
                float colorClearValue = SystemInfo.usesReversedZBuffer ? 1.0f : 0.0f;
                cmd.ClearRenderTarget(true, true, new Color(colorClearValue,colorClearValue,0,0), 1.0f);

                var resolution = GetShadowMapResolution(m_MainLight);

                for (int i = 0; i < CASCADE_NUM; i++)
                {
                    cmd.SetViewport(new Rect(_CascadeSliceData[i].offsetX, _CascadeSliceData[i].offsetY, _CascadeSliceData[i].resolution, _CascadeSliceData[i].resolution));
                    cmd.EnableScissorRect(new Rect(_CascadeSliceData[i].offsetX + 4, _CascadeSliceData[i].offsetY + 4, _CascadeSliceData[i].resolution - 8, _CascadeSliceData[i].resolution - 8));
                    cmd.SetViewProjectionMatrices(_CascadeSliceData[i].viewMatrix, _CascadeSliceData[i].projectionMatrix);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    context.DrawShadows(ref drawingSettings);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }


                cmd.DisableScissorRect();
                context.ExecuteCommandBuffer(cmd);
            }

            context.Submit();
            Profiling.Profiler.EndSample();
        }


        Profiling.Profiler.BeginSample("blur shadowmap");
        // blur shadowmap
        cmd.Clear();
        Shader blur = Shader.Find("JsRP/Forward/Gaussian");
        Material mat = new Material(blur);
        cmd.Blit(_ShadowMapIdentifier, _ShadowMapBlurIdentifier, mat, 0);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
        cmd.Clear();
        cmd.CopyTexture(_ShadowMapBlurIdentifier, _ShadowMapIdentifier);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();
        Profiling.Profiler.EndSample();


        cmd.Clear();
        cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
        context.ExecuteCommandBuffer(cmd);
        context.Submit();




    }

    public override void FrameCleanup(ScriptableRenderContext context)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "shadow depth cleanup" };
        cmd.ReleaseTemporaryRT(Shader.PropertyToID(SHADOWMAP_NAME));
        cmd.ReleaseTemporaryRT(Shader.PropertyToID(SHADOWMAP_NAME + "_Blur"));
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    bool GetLightMatrix(ref RenderingData renderingData)
    {
        if (renderingData.lightData.mainLightIndex == -1)
        {
            m_MainLight = null;
            return false;
        }
        _MainLightView = Matrix4x4.identity;
        _MainLightProj = Matrix4x4.identity;

        Light mainLight = renderingData.cameraData.cullingResults.visibleLights[renderingData.lightData.mainLightIndex].light;

        var shadowResolution = GetShadowMapResolution(mainLight);
        shadowResolution >>= CASCADE_NUM > 1 ? 1 : 0;
        for (int i = 0; i < CASCADE_NUM; i++)
        {
            if (!renderingData.cameraData.cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                renderingData.lightData.mainLightIndex,i,CASCADE_NUM, CASCADE_SPLIT,
                shadowResolution,
                mainLight.shadowNearPlane,
                out _CascadeSliceData[i].viewMatrix,out _CascadeSliceData[i].projectionMatrix, out _CascadeSplitData[i]))
            {
                m_MainLight = null;
                return false;
            }
            _CascadeSliceData[i].shadowTransform = GetWorldToShadowMapSpaceMatrix(_CascadeSliceData[i].projectionMatrix, _CascadeSliceData[i].viewMatrix);
            _CascadeSliceData[i].offsetX = (i % 2) * shadowResolution;
            _CascadeSliceData[i].offsetY = (i / 2) * shadowResolution;
            _CascadeSliceData[i].resolution = shadowResolution;
            _CascadeSliceData[i].cullingSphere = _CascadeSplitData[i].cullingSphere;
        }



        if (!renderingData.cameraData.cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                renderingData.lightData.mainLightIndex,0,4, new Vector3(0.4f, 0.3f, 0.5f),
                GetShadowMapResolution(mainLight),
                mainLight.shadowNearPlane,
                out _MainLightView,out _MainLightProj,out _ShadowSplitData))
        {
            m_MainLight = null;
            return false;
        }
        // _MainLightView = mainLight.transform.worldToLocalMatrix;
        // _MainLightProj = Matrix4x4.Ortho(-3.0f, 3.0f, -3.0f, 3.0f, 0.1f, 10.0f);

        // _MainLightProj = GL.GetGPUProjectionMatrix(_MainLightProj, true);
        _WorldToMainLightShadowmapMatrix = GetWorldToShadowMapSpaceMatrix(_MainLightProj, _MainLightView);

        if (!renderingData.cameraData.cullingResults.GetShadowCasterBounds(renderingData.lightData.mainLightIndex, out var bounds))
        {
            m_MainLight = null;
            return false;
        }

        m_MainLight = mainLight;
        // var viewMatrix = Matrix4x4.TRS(mainLight.transform.position, mainLight.transform.rotation, Vector3.one).inverse;
        // if (SystemInfo.usesReversedZBuffer)
        // {
        //     viewMatrix.m20 = -viewMatrix.m20;
        //     viewMatrix.m21 = -viewMatrix.m21;
        //     viewMatrix.m22 = -viewMatrix.m22;
        //     viewMatrix.m23 = -viewMatrix.m23;
        // }

        return true;
    }

    void SetupShader(CommandBuffer cmd, ref RenderingData renderingData)
    {

        Light mainLight = renderingData.cameraData.cullingResults.visibleLights[renderingData.lightData.mainLightIndex].light;
        Vector3 lightDirection = -renderingData.cameraData.cullingResults.visibleLights[renderingData.lightData.mainLightIndex].localToWorldMatrix.GetColumn(2);
        lightDirection = lightDirection.normalized;

        // cmd.SetGlobalMatrix("_MainLightView", _MainLightView);
        // cmd.SetGlobalMatrix("_MainLightProj", _MainLightProj);

        cmd.SetGlobalFloat("_CascadeCount", CASCADE_NUM);
        cmd.SetGlobalVector("_CascadeSplit", CASCADE_SPLIT);
        cmd.SetGlobalVector("_CascadeSplitPreSum", new Vector4(CASCADE_SPLIT.x, CASCADE_SPLIT.x + CASCADE_SPLIT.y, CASCADE_SPLIT.x + CASCADE_SPLIT.y + CASCADE_SPLIT.z, 1.0f));

        List<Matrix4x4> shadowMatrices = new List<Matrix4x4>();
        for (int i = 0; i < CASCADE_NUM; i++)
        {
            shadowMatrices.Add(_CascadeSliceData[i].shadowTransform);
        }
        cmd.SetGlobalMatrixArray("_CascadeWorldToMainLightShadowmapMatrix", shadowMatrices.ToArray());

        List<Vector4> cullingSpheres = new List<Vector4>();
        for (int i = 0; i < CASCADE_NUM; i++)
        {
            cullingSpheres.Add(_CascadeSliceData[i].cullingSphere);
        }
        cmd.SetGlobalVectorArray("_CascadeCullingSpheres", cullingSpheres.ToArray());

        cmd.SetGlobalVector("_CascadeCullingSpheresRad2", new Vector4(
            _CascadeSliceData[0].cullingSphere.w * _CascadeSliceData[0].cullingSphere.w,
            _CascadeSliceData[1].cullingSphere.w * _CascadeSliceData[1].cullingSphere.w,
            _CascadeSliceData[2].cullingSphere.w * _CascadeSliceData[2].cullingSphere.w,
            _CascadeSliceData[3].cullingSphere.w * _CascadeSliceData[3].cullingSphere.w));


        cmd.SetGlobalMatrix("_WorldToMainLightShadowmapMatrix", _WorldToMainLightShadowmapMatrix);
        cmd.SetGlobalColor("_MainLightColor", mainLight.color);
        cmd.SetGlobalFloat("_MainLightIntensity", mainLight.intensity);
        cmd.SetGlobalVector("_MainLightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));
        cmd.SetGlobalTexture(_ShadowMapHandler, _ShadowMapIdentifier);
        cmd.SetGlobalTexture("_TargetBlurRt", _ShadowMapBlurIdentifier);
        cmd.SetGlobalVector("_MainLightShadowBias", new Vector4(mainLight.shadowBias, mainLight.shadowNormalBias, 0.0f, 0.0f));
        cmd.EnableShaderKeyword("GAUSSIAN5x5");
    }

    void SetupResources(CommandBuffer cmd, ref RenderingData renderingData)
    {
        _ShadowMapHandler = Shader.PropertyToID(SHADOWMAP_NAME);
        _ShadowMapIdentifier = new RenderTargetIdentifier(_ShadowMapHandler);
        _ShadowMapBlurHandler = Shader.PropertyToID(SHADOWMAP_NAME + "_Blur");
        _ShadowMapBlurIdentifier = new RenderTargetIdentifier(_ShadowMapBlurHandler);

        int resolution = GetShadowMapResolution(renderingData.cameraData.cullingResults.visibleLights[renderingData.lightData.mainLightIndex].light);
        renderingData.rtList.rts[0].identifier = _ShadowMapIdentifier;
        renderingData.rtList.rts[0].descriptor = new RenderTextureDescriptor(resolution, resolution, RenderTextureFormat.RGFloat, 16)
        {
            dimension = TextureDimension.Tex2D
        };
        cmd.GetTemporaryRT(_ShadowMapHandler, renderingData.rtList.rts[0].descriptor);
        cmd.GetTemporaryRT(_ShadowMapBlurHandler, renderingData.rtList.rts[0].descriptor);

        cmd.SetGlobalTexture("_TargetBlurRt", _ShadowMapIdentifier);
        cmd.SetGlobalVector("_TargetBlurRt_TexelSize", new Vector4(1.0f / resolution, 1.0f / resolution, resolution, resolution));
        cmd.SetGlobalVector("_ShadowMap_TexelSize", new Vector4(1.0f / resolution, 1.0f / resolution, resolution, resolution));
    }

    public static Matrix4x4 GetWorldToShadowMapSpaceMatrix(Matrix4x4 proj, Matrix4x4 view)
    {
        //检查平台是否zBuffer反转,一般情况下，z轴方向是朝屏幕内，即近小远大。但是在zBuffer反转的情况下，z轴是朝屏幕外，即近大远小。
        if (SystemInfo.usesReversedZBuffer)
        {
            proj.m20 = -proj.m20;
            proj.m21 = -proj.m21;
            proj.m22 = -proj.m22;
            proj.m23 = -proj.m23;
        }

        // uv_depth = xyz * 0.5 + 0.5. 
        // 即将xy从(-1,1)映射到(0,1),z从(-1,1)或(1,-1)映射到(0,1)或(1,0)
        Matrix4x4 worldToShadow = proj * view;
        var textureScaleAndBias = Matrix4x4.identity;
        textureScaleAndBias.m00 = 0.5f;
        textureScaleAndBias.m11 = 0.5f;
        textureScaleAndBias.m22 = 0.5f;
        textureScaleAndBias.m03 = 0.5f;
        textureScaleAndBias.m23 = 0.5f;
        textureScaleAndBias.m13 = 0.5f;

        return textureScaleAndBias * worldToShadow;
    }

    private static int GetShadowMapResolution(Light light){
        switch(light.shadowResolution){
            case LightShadowResolution.VeryHigh:
            return 2048;
            case LightShadowResolution.High:
            return 1024;
            case LightShadowResolution.Medium:
            return 512;
            case LightShadowResolution.Low:
            return 256;
        }
        return 256;
    }
}


}
