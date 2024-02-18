#ifndef COSTUM_LIGHTING_HLSL
#define COSTUM_LIGHTING_HLSL

#define BUILTIN_TARGET_API
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"


#define MAX_VISIBLE_LIGHTS 256
real4 unity_LightData;
real4 unity_LightIndices[2];

float4 _MainLightPosition;
float4 _MainLightColor;
half4 _AdditionalLightsCount;

CBUFFER_START(AdditionalLights1)
float4 _AdditionalLightsPosition[MAX_VISIBLE_LIGHTS];
float4 _AdditionalLightsColor[MAX_VISIBLE_LIGHTS];  // fix precision problem.
CBUFFER_END

CBUFFER_START(AdditionalLights2)
half4 _AdditionalLightsAttenuation[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsFalloffParam[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsSpotDir[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsOcclusionProbes[MAX_VISIBLE_LIGHTS];
CBUFFER_END

struct Light
{
    half3   direction;
    half3   color;
    half    distanceAttenuation;
    half    shadowAttenuation;
    half    specularIntensity;
};

struct LightingData
{
    half3 positionWS;
    half3 normalWS;
    half3 viewDirectionWS;
    half3 diffuse;
    half3 specular;

    half roughness;
    half roughness2;
    half roughness2MinusOne;
    half normalizationTerm;     // roughness * 4.0 + 2.0
};

// #define FLT_MIN  1.175494351e-38 // Minimum normalized positive floating-point number
// #define HALF_MIN 6.103515625e-5

Light GetMainLight()
{
    Light light;
    light.direction = _MainLightPosition.xyz;
    light.distanceAttenuation = unity_LightData.z;
    light.shadowAttenuation = 1.0;
    light.color = _MainLightColor.rgb;
    light.specularIntensity = 1.0;
    return light;
}

LightingData InitializeLightingData(half3 albedo, half3 normalWS, float3 positionWS, float3 viewDirectionWS, float2 screenUv, half metallic, half smoothness)
{
    LightingData lightingData;
    lightingData.positionWS = positionWS;
    lightingData.normalWS = normalWS;
    lightingData.viewDirectionWS = viewDirectionWS;

    half oneMinusReflectivity = (1-0.04)*(1-metallic);
    half reflectivity = 1-oneMinusReflectivity;

    lightingData.diffuse = albedo * oneMinusReflectivity;
    lightingData.specular = lerp(0.04, albedo, metallic);
    
    lightingData.roughness = max((1-smoothness)*(1-smoothness), HALF_MIN);
    lightingData.roughness2 = lightingData.roughness * lightingData.roughness;

    lightingData.normalizationTerm = lightingData.roughness * 4.0h + 2.0h;
    lightingData.roughness2MinusOne = lightingData.roughness2 - 1.0h;
    
    return lightingData;
}

half3 DirectBDRF(LightingData lightingData, half3 lightDirectionWS, half specularIntensity)
{

    float3 halfDir = SafeNormalize(float3(lightDirectionWS)+float3(lightingData.viewDirectionWS));

    float NoH = saturate(dot(lightingData.normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // BRDFspec = (D * V * F) / 4.0
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155

    // Final BRDFspec = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2 * (LoH^2 * (roughness + 0.5) * 4.0)
    // We further optimize a few light invariant terms
    // brdfData.normalizationTerm = (roughness + 0.5) * 4.0 rewritten as roughness * 4.0 + 2.0 to a fit a MAD.
    float d = NoH * NoH * lightingData.roughness2MinusOne + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = lightingData.roughness2 * rcp((d * d) * max(0.1h, LoH2) * lightingData.normalizationTerm);

    half3 color = specularTerm * lightingData.specular * specularIntensity + lightingData.diffuse;
    return color;
}

// UE4 light attenuation curve
/**
 * Returns a radial attenuation factor for a point light.
 * WorldLightVector is the vector from the position being shaded to the light, divided by the radius of the light.
 */
half RadialAttenuationMask(half3 WorldLightVector)
{
    float NormalizeDistanceSquared = dot(WorldLightVector, WorldLightVector);
    return half(1.0) - clamp(NormalizeDistanceSquared, half(0), half(0.9999));
}

half RadialAttenuation(half3 WorldLightVector, half FalloffExponent)
{
    // UE3 (fast, but now we not use the default of 2 which looks quite bad):
    return pow(RadialAttenuationMask(WorldLightVector), FalloffExponent);
}

half AngleAttenuation(half3 spotDirection, half3 lightDirection, half2 spotAttenuation)
{
    // Spot Attenuation with a linear falloff can be defined as
    // (SdotL - cosOuterAngle) / (cosInnerAngle - cosOuterAngle)
    // This can be rewritten as
    // invAngleRange = 1.0 / (cosInnerAngle - cosOuterAngle)
    // SdotL * invAngleRange + (-cosOuterAngle * invAngleRange)
    // SdotL * spotAttenuation.x + spotAttenuation.y

    // If we precompute the terms in a MAD instruction
    half SdotL = dot(spotDirection, lightDirection);
    half atten = saturate(SdotL * spotAttenuation.x + spotAttenuation.y);
    return atten * atten;
}

float square(float x) { return x * x; }

Light GetAdditionalPerObjectLight(int lightIndex, float3 positionWS)
{

    float4 lightPositionWS = _AdditionalLightsPosition[lightIndex];
    half3 color = _AdditionalLightsColor[lightIndex].rgb;     // fix precision problem.
    half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[lightIndex];
    half4 falloffParams = _AdditionalLightsFalloffParam[lightIndex];
    half4 spotDirection = _AdditionalLightsSpotDir[lightIndex];

    // Directional lights store direction in lightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    half3 lightVector = lightPositionWS.xyz - positionWS * lightPositionWS.w;
    half distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);
    // Sphere falloff (technically just 1/d2 but this avoids inf)

	half distanceAttenuation = 1.0;

    half sourceRadiusSqr = falloffParams.z;

    if (falloffParams.x < 0.5) {
	    distanceAttenuation = RadialAttenuation(lightVector * distanceAndSpotAttenuation.x, falloffParams.y);
    } else {

        distanceAttenuation = rcp(max(distanceSqr, sourceRadiusSqr));
        half LightRadiusMask = square(saturate(1 - square(distanceSqr * distanceAndSpotAttenuation.x * distanceAndSpotAttenuation.x)));
	    distanceAttenuation *= LightRadiusMask;
    }
    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));

    half angleAttenuation = AngleAttenuation(spotDirection.xyz, lightDirection, distanceAndSpotAttenuation.zw);
    half attenuation = distanceAttenuation * angleAttenuation;

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0; //AdditionalLightRealtimeShadow(lightIndex, positionWS);
    light.color = color;
    light.specularIntensity = falloffParams.w;
    return light;
}

half3 Lighting(LightingData lightingData, Light light)
{
    half NdotL = saturate(dot(lightingData.normalWS, light.direction));
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * NdotL);
    return DirectBDRF(lightingData, light.direction, light.specularIntensity) * radiance;
}


#endif