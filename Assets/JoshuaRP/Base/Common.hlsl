#ifndef JSRP_COMMON
#define JSRP_COMMON

struct Light
{
    half3   direction;
    float3   color;     // fix precision problem.
    half    distanceAttenuation;
    half    shadowAttenuation;
    half    specularIntensity;
};


struct GBufferData
{
    half3 albedo;
    half3 normalWS;
    half metallic;
    half smoothness;
    half alpha;
};

struct BRDFData
{
    half3 diffuse;
    half3 specular;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;

    // We save some light invariant BRDF terms so we don't have to recompute
    // them in the light loop. Take a look at DirectBRDF function for detailed explaination.
    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne;    // roughness^2 - 1.0
};

struct LightingData
{
    float3  positionWS;
    half3   normalWS;
    half3   viewDirectionWS;
    float4  shadowCoord;
    half3   bakedGI;

    half occlusion;
    half alpha;
    half3 emission;

    half3 diffuse;
    half3 specular;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;

    // We save some light invariant BRDF terms so we don't have to recompute
    // them in the light loop. Take a look at DirectBRDF function for detailed explaination.
    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne; 
};

LightingData InitializeLightingData(GBufferData gbufferData, float3 positionWS, float3 viewDirectionWS, float2 screenUv)
{
    LightingData lightingData;

    lightingData.positionWS = positionWS;
    lightingData.normalWS = gbufferData.normalWS;
    lightingData.viewDirectionWS = viewDirectionWS;
    lightingData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    lightingData.bakedGI = UniversalSampleGI(positionWS, gbufferData.normalWS);

#ifdef _TILE_BASED_LIGHTCULLING
    lightingData.occlusion = GetAmbientOcclusion(screenUv);
#else
    lightingData.occlusion = 1;
#endif

    lightingData.alpha = gbufferData.alpha;
    lightingData.emission = 0;

    half oneMinusReflectivity = OneMinusReflectivityMetallic(gbufferData.metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;

    lightingData.diffuse = gbufferData.albedo * oneMinusReflectivity;
    lightingData.specular = lerp(kDieletricSpec.rgb, gbufferData.albedo, gbufferData.metallic);


    lightingData.grazingTerm = saturate(gbufferData.smoothness + reflectivity);
    lightingData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(gbufferData.smoothness);
    lightingData.roughness = max(PerceptualRoughnessToRoughness(lightingData.perceptualRoughness), HALF_MIN);
    lightingData.roughness2 = lightingData.roughness * lightingData.roughness;

    lightingData.normalizationTerm = lightingData.roughness * 4.0h + 2.0h;
    lightingData.roughness2MinusOne = lightingData.roughness2 - 1.0h;

#ifdef _ALPHAPREMULTIPLY_ON
    lightingData.diffuse *= gbufferData.alpha;
#endif

    return lightingData;
}

#endif