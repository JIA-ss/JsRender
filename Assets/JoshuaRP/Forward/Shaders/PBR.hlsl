#ifndef PBR_HLSL
#define PBR_HLSL

#include "Declare.hlsl"


float3 F_lambert(float3 albedo)
{
    return albedo * INV_PI;
}

float NDF_GGX(float roughness, float3 normal, float3 subsurfaceNormal)
{
    float alpha = roughness * roughness;
    float alpha2 = alpha * alpha;
    float NDotH = dot(normal, subsurfaceNormal);
    float NDotH2 = NDotH * NDotH;

    float molecular = alpha2 * INV_PI;

    float denominator = NDotH2 * (alpha2 - 1.0) + 1.0;
    denominator = denominator * denominator;

    return molecular / denominator;
}

float3 Fresnel(float3 F0, float3 view, float3 subsurfaceNormal)
{
    float VDotH = max(dot(view, subsurfaceNormal), 0.0);
    return F0 + (1.0 - F0) * exp2(-5.55473 * VDotH * VDotH - 6.98316 * VDotH);
}

float G_sub_SchlickGGX(float3 normal, float3 view, float k)
{
    float NDotV = max(dot(normal, view), 0.0);
    return NDotV / (NDotV * (1 - k) + k);
}

float GeometryFunction(float3 normal, float3 view, float3 lightDir, float roughness)
{
    float k = (roughness + 1) * (roughness + 1) * 0.125;
    // float k = roughness * roughness * 0.5; // for ibl
    float G1 = G_sub_SchlickGGX(normal, view, k);
    float G2 = G_sub_SchlickGGX(normal, lightDir, k);
    return G1 * G2;
}

float3 MetallicWorkflowPBR(float3 alebdo, float metallic, float roughness, float3 normal, float3 view, float3 lightDir)
{
    float3 subsurfaceNormal = normalize(view + lightDir);
    float3 F0 = lerp(0.04, alebdo, metallic);
    float3 F = Fresnel(F0, view, subsurfaceNormal);
    float NDF = NDF_GGX(roughness, normal, subsurfaceNormal);
    float G = GeometryFunction(normal, view, lightDir, roughness);
    float3 diffuse = F_lambert(alebdo) * (1.0 - F) * (1.0 - metallic);
    float3 specular = F * NDF * G * 0.25 / (max(dot(view, normal), 0.0) * max(dot(lightDir, normal), 0.0) + 0.00001);
    float3 c = diffuse + specular;
    return c;// / (c + 1.0);
}

#endif