using UnityEngine;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ForwardRenderer : BaseRenderer
{
    public ForwardRenderer(List<RenderPassBase> renderPasses)
    {
        m_RenderPasses = renderPasses;
        
        int maxLights = BaseRenderPipeline.maxVisibleAdditionalLights;
        m_AdditionalLightPositions = new Vector4[maxLights];
        m_AdditionalLightColors = new Vector4[maxLights];
        m_AdditionalLightAttenuations = new Vector4[maxLights];
        m_AdditionalLightsFalloffParams = new Vector4[maxLights];
        m_AdditionalLightSpotDirections = new Vector4[maxLights];
        m_AdditionalLightOcclusionProbeChannels = new Vector4[maxLights];
    }
    public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        ref CameraData cameraData = ref renderingData.cameraData;
        RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

        foreach (RenderPassBase renderPass in m_RenderPasses)
        {
            renderPass.SetupWithTag(context, ref renderingData);
        }
    }

    // Holds light direction for directional lights or position for punctual lights.
    // When w is set to 1.0, it means it's a punctual light.
    Vector4 k_DefaultLightPosition = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
    Vector4 k_DefaultLightColor = Color.black;

    // Default light attenuation is setup in a particular way that it causes
    // directional lights to return 1.0 for both distance and angle attenuation
    Vector4 k_DefaultLightAttenuation = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
    Vector4 k_DefaultLightExponentFadeThreshold = new Vector4(2.0f, 0.8f, 0.0f, 0.0f);
    Vector4 k_DefaultLightSpotDirection = new Vector4(0.0f, 0.0f, 1.0f, 0.0f);
    Vector4 k_DefaultLightsProbeChannel = new Vector4(-1.0f, 1.0f, -1.0f, -1.0f);

    Vector4[] m_AdditionalLightPositions;
    Vector4[] m_AdditionalLightColors;
    Vector4[] m_AdditionalLightAttenuations;
    Vector4[] m_AdditionalLightsFalloffParams;
    Vector4[] m_AdditionalLightSpotDirections;
    Vector4[] m_AdditionalLightOcclusionProbeChannels;
    
    int SetupPerObjectLightIndices(CullingResults cullResults, ref LightData lightData)
    {
        if (lightData.maxPerObjectAdditionalLightsCount == 0)
            return lightData.maxPerObjectAdditionalLightsCount;

        var visibleLights = lightData.visibleLights;
        var perObjectLightIndexMap = cullResults.GetLightIndexMap(Allocator.Temp);
        int globalDirectionalLightsCount = 0;
        int additionalLightsCount = 0;

        var LightIndexRemapping = BaseRenderPipeline.LightIndexRemapping;
        var nativeLightsCount = visibleLights.NativeLength;
        for (int i = 0; i < nativeLightsCount; ++i)
        {
            perObjectLightIndexMap[i] = LightIndexRemapping[perObjectLightIndexMap[i]];
        }

        // Disable all directional lights from the perobject light indices
        // Pipeline handles main light globally and there's no support for additional directional lights atm.
        for (int i = 0; i < nativeLightsCount; ++i)
        {
            if (additionalLightsCount >= BaseRenderPipeline.maxVisibleAdditionalLights)
                break;

            if (i == lightData.mainLightIndex)
            {
                perObjectLightIndexMap[i] = -1;
                ++globalDirectionalLightsCount;
            }
            else
            {
                perObjectLightIndexMap[i] -= globalDirectionalLightsCount;
                ++additionalLightsCount;
            }
        }

        // Disable all remaining lights we cannot fit into the global light buffer.
        for (int i = globalDirectionalLightsCount + additionalLightsCount; i < perObjectLightIndexMap.Length; ++i)
            perObjectLightIndexMap[i] = -1;

        cullResults.SetLightIndexMap(perObjectLightIndexMap);
        
        perObjectLightIndexMap.Dispose();
        return additionalLightsCount;
    }
    
    void InitializeLightConstants(VisibleLight[] lights, int lightIndex, out Vector4 lightPos, out Vector4 lightColor, out Vector4 lightAttenuation, out Vector4 lightExponentFadeThreshold, out Vector4 lightSpotDir, out Vector4 lightOcclusionProbeChannel)
    {
        lightPos = k_DefaultLightPosition;
        lightColor = k_DefaultLightColor;
        lightAttenuation = k_DefaultLightAttenuation;
        lightExponentFadeThreshold = k_DefaultLightExponentFadeThreshold;
        lightSpotDir = k_DefaultLightSpotDirection;
        lightOcclusionProbeChannel = k_DefaultLightsProbeChannel;

        // When no lights are visible, main light will be set to -1.
        // In this case we initialize it to default values and return
        if (lightIndex < 0)
            return;

        VisibleLight lightData = lights[lightIndex];
        if (lightData.lightType == LightType.Directional)
        {
            Vector4 dir = -lightData.localToWorldMatrix.GetColumn(2);
            lightPos = new Vector4(dir.x, dir.y, dir.z, 0.0f);
        }
        else
        {
            Vector4 pos = lightData.localToWorldMatrix.GetColumn(3);
            lightPos = new Vector4(pos.x, pos.y, pos.z, 1.0f);
        }

        // VisibleLight.finalColor already returns color in active color space
        lightColor = lightData.finalColor;

        // Directional Light attenuation is initialize so distance attenuation always be 1.0
        if (lightData.lightType != LightType.Directional)
        {
            // Light attenuation in universal matches the unity vanilla one.
            // attenuation = 1.0 / distanceToLightSqr
            // We offer two different smoothing factors.
            // The smoothing factors make sure that the light intensity is zero at the light range limit.
            // The first smoothing factor is a linear fade starting at 80 % of the light range.
            // smoothFactor = (lightRangeSqr - distanceToLightSqr) / (lightRangeSqr - fadeStartDistanceSqr)
            // We rewrite smoothFactor to be able to pre compute the constant terms below and apply the smooth factor
            // with one MAD instruction
            // smoothFactor =  distanceSqr * (1.0 / (fadeDistanceSqr - lightRangeSqr)) + (-lightRangeSqr / (fadeDistanceSqr - lightRangeSqr)
            //                 distanceSqr *           oneOverFadeRangeSqr             +              lightRangeSqrOverFadeRangeSqr

            // The other smoothing factor matches the one used in the Unity lightmapper but is slower than the linear one.
            // smoothFactor = (1.0 - saturate((distanceSqr * 1.0 / lightrangeSqr)^2))^2
            float lightRangeSqr = lightData.range * lightData.range;
            float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
            float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
            float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
            float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
            float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, lightData.range * lightData.range);

            // On mobile and Nintendo Switch: Use the faster linear smoothing factor (SHADER_HINT_NICE_QUALITY).
            // On other devices: Use the smoothing factor that matches the GI.
            lightAttenuation.x = Application.isMobilePlatform || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Switch ? oneOverFadeRangeSqr : oneOverLightRangeSqr;
            lightAttenuation.y = lightRangeSqrOverFadeRangeSqr;
        }

        if (lightData.lightType == LightType.Spot)
        {
            Vector4 dir = lightData.localToWorldMatrix.GetColumn(2);
            lightSpotDir = new Vector4(-dir.x, -dir.y, -dir.z, 0.0f);

            // Spot Attenuation with a linear falloff can be defined as
            // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
            // This can be rewritten as
            // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
            // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
            // If we precompute the terms in a MAD instruction
            float cosOuterAngle = Mathf.Cos(Mathf.Deg2Rad * lightData.spotAngle * 0.5f);
            // We neeed to do a null check for particle lights
            // This should be changed in the future
            // Particle lights will use an inline function
            float cosInnerAngle;
            if (lightData.light != null)
                cosInnerAngle = Mathf.Cos(lightData.light.innerSpotAngle * Mathf.Deg2Rad * 0.5f);
            else
                cosInnerAngle = Mathf.Cos((2.0f * Mathf.Atan(Mathf.Tan(lightData.spotAngle * 0.5f * Mathf.Deg2Rad) * (64.0f - 18.0f) / 64.0f)) * 0.5f);
            float smoothAngleRange = Mathf.Max(0.001f, cosInnerAngle - cosOuterAngle);
            float invAngleRange = 1.0f / smoothAngleRange;
            float add = -cosOuterAngle * invAngleRange;
            lightAttenuation.z = invAngleRange;
            lightAttenuation.w = add;
        }

        Light light = lightData.light;

        // Set the occlusion probe channel.
        int occlusionProbeChannel = light != null ? light.bakingOutput.occlusionMaskChannel : -1;

        // If we have baked the light, the occlusion channel is the index we need to sample in 'unity_ProbesOcclusion'
        // If we have not baked the light, the occlusion channel is -1.
        // In case there is no occlusion channel is -1, we set it to zero, and then set the second value in the
        // input to one. We then, in the shader max with the second value for non-occluded lights.
        lightOcclusionProbeChannel.x = occlusionProbeChannel == -1 ? 0f : occlusionProbeChannel;
        lightOcclusionProbeChannel.y = occlusionProbeChannel == -1 ? 1f : 0f;
    }
    
    public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(k_SetupLightConstants);
        VisibleLight mainLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];
        Vector4 dir = -mainLight.light.gameObject.transform.forward.normalized;
        cmd.SetGlobalVector(ShaderID._MainLightPosition, new Vector4(dir.x, dir.y, dir.z, 0.0f));
        cmd.SetGlobalVector(ShaderID._MainLightColor, mainLight.finalColor);
        
        ref LightData lightData = ref renderingData.lightData;
        var cullResults = renderingData.cullResults;
        var lights = lightData.visibleLights.Lights;
        var lightsCount = lightData.visibleLights.Length;
        int maxAdditionalLightsCount = BaseRenderPipeline.asset.maxVisibleAdditionalLight;
        int additionalLightsCount = SetupPerObjectLightIndices(cullResults, ref lightData);
        if (additionalLightsCount > 0)
        {
            for (int i = 0, lightIter = 0; i < lightsCount && lightIter < maxAdditionalLightsCount; ++i)
            {
                VisibleLight light = lights[i];
                if (lightData.mainLightIndex != i)
                {
                    InitializeLightConstants(lights, i, out m_AdditionalLightPositions[lightIter],
                        out m_AdditionalLightColors[lightIter],
                        out m_AdditionalLightAttenuations[lightIter],
                        out m_AdditionalLightsFalloffParams[lightIter],
                        out m_AdditionalLightSpotDirections[lightIter],
                        out m_AdditionalLightOcclusionProbeChannels[lightIter]);
                    lightIter++;
                }
            }

            cmd.SetGlobalVectorArray(Shader.PropertyToID("_AdditionalLightsPosition"), m_AdditionalLightPositions);
            cmd.SetGlobalVectorArray(Shader.PropertyToID("_AdditionalLightsColor"), m_AdditionalLightColors);
            cmd.SetGlobalVectorArray(Shader.PropertyToID("_AdditionalLightsAttenuation"), m_AdditionalLightAttenuations);
            cmd.SetGlobalVectorArray(Shader.PropertyToID("_AdditionalLightsSpotDir"), m_AdditionalLightSpotDirections);
            cmd.SetGlobalVectorArray(Shader.PropertyToID("_AdditionalLightOcclusionProbeChannel"), m_AdditionalLightOcclusionProbeChannels);
        }
        
        cmd.SetGlobalVector(Shader.PropertyToID("_AdditionalLightsCount"), new Vector4(lightData.maxPerObjectAdditionalLightsCount,
            0.0f, 0.0f, 0.0f));
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void ExecuteRenderPass(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        base.ExecuteRenderPass(context, ref renderingData);
    }
}