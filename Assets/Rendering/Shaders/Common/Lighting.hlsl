#ifndef COSTUM_LIGHTING_HLSL
#define COSTUM_LIGHTING_HLSL


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
    half3   normalWS;
    half3   viewDirectionWS;
    half3 diffuse;
    half3 specular;

    half roughness2;
    half roughness2MinusOne;
    half normalizationTerm;     // roughness * 4.0 + 2.0
};

#define FLT_MIN  1.175494351e-38 // Minimum normalized positive floating-point number

float3 SafeNormalize(float3 inVec)
{
    float dp3 = max(FLT_MIN, dot(inVec, inVec));
    return inVec * rsqrt(dp3);
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

half3 Lighting(LightingData lightingData, Light light)
{
    half NdotL = saturate(dot(lightingData.normalWS, light.direction));
    half3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * NdotL);
    return DirectBDRF(lightingData, light.direction, light.specularIntensity);// * radiance;
}


#endif