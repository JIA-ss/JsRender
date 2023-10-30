#define PI 3.14159265359

// D
float D_GGX(float NdotH, float roughness)
{
    float a2     = roughness * roughness;
    float NdotH2 = NdotH * NdotH;

    float nom   = a2;
    float denom = NdotH2 * (a2 - 1.0) + 1.0;
    denom = PI * denom * denom;

    return nom / denom;
}

// F
float3 SchlickFresnel(float HdotV, float3 F0)
{
    float m = clamp(1-HdotV, 0, 1);
    float m5 = m * m * m * m * m;
    return F0 + (1.0 - F0) * m5;
}

// G
float G_GGX(float Ndot_V_or_L, float k)
{
    return Ndot_V_or_L / (Ndot_V_or_L * (1.0 - k) + k);
}

float3 PBR(float3 N, float3 V, float3 L, float3 albedo, float3 radiance, float roughness, float metallic)
{
    roughness = max(roughness, 0.05);

    float3 H = normalize(L+V);
    float NdotL = max(dot(N, L), 0);
    float NdotV = max(dot(N, V), 0);
    float NdotH = max(dot(N, H), 0);
    float HdotV = max(dot(H, V), 0);

    float D = D_GGX(NdotH, roughness);

    float3 F0 = lerp(float3(0.04,0.04,0.04), albedo, metallic);
    float F = SchlickFresnel(HdotV, F0);

    float k = (roughness + 1) * (roughness + 1) / 8.0;
    float G = G_GGX(NdotV, k) * G_GGX(NdotL, k);

    float3 k_s = F;
    float3 k_d = (1.0 - k_s) * max(1.0 - metallic, 0.0);

    float3 f_diffuse = albedo / PI;
    float3 f_specular = (D * F * G) / (4.0 * NdotV * NdotL + 0.0001);

    return (k_d * f_diffuse + f_specular) * radiance * NdotL;
}