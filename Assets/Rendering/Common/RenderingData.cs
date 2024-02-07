using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;


public struct VisibleLightArray
{
    // Access large elements from NativeArray is slower than accessing from Managed Array
    public VisibleLight[] Lights;
    public int Length;
    public int NativeLength;

    public VisibleLightArray(VisibleLight[] lights, int lightLength, int lightCount)
    {
        Lights = lights;
        Length = lightCount;
        NativeLength = lightLength;
    }
    public VisibleLight this[int index] => Lights[index];
}

enum LightPriorityType
{
    Directional,
    Point,
    Spot,
    Area,
    Disc,
    Baked // Always last
}

public struct RenderingData
{
    public CameraData cameraData;
    public LightData lightData;
    public CullingResults cullResults;
    public PerObjectData perObjectData;
    public bool postProcessingEnabled;
    public ShadowData shadowData;
    public bool Init(ScriptableRenderContext context, Camera camera)
    {
        return cameraData.Init(context, camera) && lightData.Init(ref cameraData.cullingResults);
    }
}

public struct CameraData
{
    public Camera camera;
    public ScriptableCullingParameters cullingParameters;

    public RenderTexture targetTexture;
    public CullingResults cullingResults;
    public CameraType cameraType;
    public CameraRenderType renderType;
    public bool isSceneViewCamera;
    
    // volume
    public int volumeLayerMask;
    public Transform volumeTrigger;
    public AntialiasingMode antialiasing;
    public AntialiasingQuality antialiasingQuality;

    public bool isHdrEnabled;
    public Rect pixelRect;
    public int pixelWidth;
    public int pixelHeight;
    public float aspectRatio; // pixelWidth / pixelHeight
    public bool isDefaultViewport;
    public float renderScale;
    public SortingCriteria defaultOpaqueSortFlags;
    public IEnumerator<Action<RenderTargetIdentifier, CommandBuffer>> captureActions;
    public RenderTextureDescriptor cameraTargetDescriptor;
    public float maxShadowDistance;
    public bool clearDepth;
    public bool postProcessEnabled;
    public BaseRenderer renderer;
    public bool resolveFinalTarget;
    public Matrix4x4 m_ViewMatrix;
    public Matrix4x4 m_ProjectionMatrix;
    public bool Init(ScriptableRenderContext context, Camera camera)
    {
        this.camera = camera;
        context.SetupCameraProperties(camera);
        if (!camera.TryGetCullingParameters(out cullingParameters))
        {
            return false;
        }
        cullingResults = context.Cull(ref cullingParameters);
        return true;
    }
    
    public void SetViewAndProjectionMatrix(Camera camera, Matrix4x4 projectionMatrix)
    {
        m_ViewMatrix = camera.worldToCameraMatrix;
        m_ProjectionMatrix = projectionMatrix;
    }
}

public struct LightData
{
    public Light mainLight;
    public Vector3 lightDirection;
    public int mainLightIndex;
    public int additionalLightsCount;
    public bool shadeAdditionalLightsPerVertex;
    public VisibleLightArray visibleLights;
    public int[] visibleLightRemapping;
    public bool supportsMixedLighting;
    public NativeArray<VisibleReflectionProbe> visibleReflectionProbes;
    public int reflectionProbesCount;
    public int maxPerObjectAdditionalLightsCount;
    public float mainLightIntensity;
    public bool Init(ref CullingResults cullingResults)
    {
        mainLightIndex = -1;
        for (int lightIndex = 0; lightIndex < cullingResults.visibleLights.Length; lightIndex++)
        {
            var visibleLight = cullingResults.visibleLights[lightIndex];
            var light = visibleLight.light;
            if (light == RenderSettings.sun)
            {
                mainLightIndex = lightIndex;
                mainLightIntensity = light.intensity;
                mainLight = cullingResults.visibleLights[lightIndex].light;
                lightDirection = -cullingResults.visibleLights[lightIndex].localToWorldMatrix.GetColumn(2);
                lightDirection = lightDirection.normalized;
                return true;
            }
        }
        return false;
    }
}

public struct ShadowData
{
    public Matrix4x4 viewMatrix;
    public Matrix4x4 projectionMatrix;
    public List<Vector4> bias;
    public bool supportsMainLightShadows;
    public int mainLightShadowCascadesCount;
    public int mainLightShadowmapWidth;
    public int mainLightShadowmapHeight;
    public bool supportsAdditionalLightShadows;
    public int additionalLightsShadowmapWidth;
    public int additionalLightsShadowmapHeight;
    public bool supportsSoftShadows;
    public int shadowmapDepthBufferBits;
    public Vector3 mainLightShadowCascadesSplit;
    public bool requiresScreenSpaceShadowResolve;
}


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