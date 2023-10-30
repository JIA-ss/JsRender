#ifndef SHADOW_HLSL
#define SHADOW_HLSL

#include "UnityCG.cginc"
#include "Lighting.hlsl"


struct ShadowCasterVertVaring
{
    float4 positionOS : POSITION;
    float3 normalOS :  NORMAL;
};

struct ShadowCasterFragVaring
{
    float4 positionCS : SV_POSITION;
};


float _CascadeCount;
float4 _CascadeSplit;
float4 _CascadeSplitPreSum;
float4x4 _CascadeWorldToMainLightShadowmapMatrix[4];
float4 _CascadeCullingSpheres[4];
float4 _CascadeCullingSpheresRad2;

half ComputeCascadeIndex(float3 positionWS)
{
    float3 fromCenter0 = positionWS - _CascadeCullingSpheres[0].xyz;
    float3 fromCenter1 = positionWS - _CascadeCullingSpheres[1].xyz;
    float3 fromCenter2 = positionWS - _CascadeCullingSpheres[2].xyz;
    float3 fromCenter3 = positionWS - _CascadeCullingSpheres[3].xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

    half4 weights = half4(distances2 < _CascadeCullingSpheresRad2);
    weights.yzw = saturate(weights.yzw - weights.xyz);

    return 4 - dot(weights, half4(4, 3, 2, 1));
}

ShadowCasterFragVaring ShadowCasterVS (ShadowCasterVertVaring input)
{
    ShadowCasterFragVaring output;
    float4 positionWS = mul(unity_ObjectToWorld, input.positionOS);

    float3 normalWS = normalize(UnityObjectToWorldNormal(input.normalOS));

    // apply shadow depth bias
    positionWS -= float4(_MainLightDirection, 0.0) * _MainLightShadowBias.x;
    // apply shadow normal bias
    float NdotL = dot(_MainLightDirection, normalWS);
    float sign = NdotL >= 0.0 ? -1.0 : 1.0;
    positionWS += sign * (1.0 - saturate(NdotL)) * _MainLightShadowBias.y * float4(normalWS, 0.0f);

    output.positionCS = UnityWorldToClipPos(positionWS);
    return output;
}

float4 ShadowCasterFS(ShadowCasterFragVaring input) : SV_TARGET
{
    float depth = input.positionCS.z;
#if UNITY_REVERSED_Z
    depth = 1.0 - depth;
#endif
    return float4(depth, depth * depth, 0.0, 0.0);
}

float CalPerCentage_VSM(float depth, float depth2, float z)
{
    if (depth >= 0.9999)
    {
        return 0.0;
    }

    float miu = depth;
    float sigma2 = abs(depth2 - miu * miu);
    float P_x_greater_t = sigma2 / ( sigma2 + (z - miu) * (z - miu));
    if (z >= miu)
    {
        return 1.0 - P_x_greater_t;
    }
    return 0.0;
}

float PCF_VSM(float3 uvz, half cascadeIndex = 0)
{
    uvz.x = uvz.x * 0.5;
    uvz.y = uvz.y * 0.5;

    float2 border = 0.0;
    float2 offset[4] = {
        border, float2(0.5 + border.x * 2, border.y), float2(border.x, 0.5 + border.y * 2), float2(0.5 + 2 * border.x, 0.5 + 2 * border.y)
    };

    uvz.xy = uvz.xy + offset[cascadeIndex];


    float2 v1 = tex2D(_ShadowMap, uvz.xy + 0.25 * float2(-_ShadowMap_TexelSize.x, _ShadowMap_TexelSize.y)).xy;
    float2 v2 = tex2D(_ShadowMap, uvz.xy + 0.25 * float2(-_ShadowMap_TexelSize.x, -_ShadowMap_TexelSize.y)).xy;
    float2 v3 = tex2D(_ShadowMap, uvz.xy + 0.25 * float2(_ShadowMap_TexelSize.x, _ShadowMap_TexelSize.y)).xy;
    float2 v4 = tex2D(_ShadowMap, uvz.xy + 0.25 * float2(_ShadowMap_TexelSize.x, -_ShadowMap_TexelSize.y)).xy;

    float P_x_greater_t = 0.0;
    P_x_greater_t += CalPerCentage_VSM(v1.x, v1.y, uvz.z);
    P_x_greater_t += CalPerCentage_VSM(v2.x, v2.y, uvz.z);
    P_x_greater_t += CalPerCentage_VSM(v3.x, v3.y, uvz.z);
    P_x_greater_t += CalPerCentage_VSM(v4.x, v4.y, uvz.z);

    return P_x_greater_t * 0.25;
}

float ShadowReceiver(float4 positionWS)
{

    half cascadeIndex = ComputeCascadeIndex(positionWS.xyz / positionWS.w);
    float3 centerDistance = _CascadeCullingSpheres[cascadeIndex].xyz - positionWS.xyz / positionWS.w;
    if (dot(centerDistance, centerDistance) >= _CascadeCullingSpheres[cascadeIndex].w * _CascadeCullingSpheres[cascadeIndex].w)
    {
        return 0.0;
    }

    float4 uvz = mul(_CascadeWorldToMainLightShadowmapMatrix[cascadeIndex], positionWS);
    // if (uvz.x <= 1e-2 || uvz.x >= 1 - 1e-2 || uvz. y <= 1e-2 || uvz.y >= 1 - 1e-2)
    // {
    //     return 0.0;
    // }

#if UNITY_REVERSED_Z
    uvz.z = 1.0 - uvz.z;
#endif

    return PCF_VSM(uvz, cascadeIndex);

    // PCF 4x4
    float P_x_greater_t = 0.0;
    float2 uv_lb = uvz.xy - _ShadowMap_TexelSize.xy;
    float2 uv_rt = uvz.xy + _ShadowMap_TexelSize.xy;
    float2 uv_lt = uvz.xy + float2(-_ShadowMap_TexelSize.x, _ShadowMap_TexelSize.y);
    float2 uv_rb = uvz.xy + float2(_ShadowMap_TexelSize.x, -_ShadowMap_TexelSize.y);

    P_x_greater_t += PCF_VSM(float3(uv_lb, uvz.z));
    P_x_greater_t += PCF_VSM(float3(uv_rt, uvz.z));
    P_x_greater_t += PCF_VSM(float3(uv_lt, uvz.z));
    P_x_greater_t += PCF_VSM(float3(uv_rb, uvz.z));

    return P_x_greater_t * 0.25;
}


float4 ShadowReceiverDebug(float4 positionWS)
{

    half cascadeIndex = ComputeCascadeIndex(positionWS.xyz / positionWS.w);

    float4 uvz = mul(_CascadeWorldToMainLightShadowmapMatrix[cascadeIndex], positionWS);
    if (uvz.x <= 1e-2 || uvz.x >= 1 - 1e-2 || uvz. y <= 1e-2 || uvz.y >= 1 - 1e-2)
    {
        return 0.0;
    }

#if UNITY_REVERSED_Z
    uvz.z = 1.0 - uvz.z;
#endif



    uvz.x = uvz.x * 0.5;
    uvz.y = uvz.y * 0.5;

    float2 offset[4] = {
        float2(0.0, 0.0), float2(0.5, 0), float2(0, 0.5), float2(0.5, 0.5)
    };

    uvz.xy = uvz.xy + offset[cascadeIndex] + float2(4,4) * _ShadowMap_TexelSize.xy;

    return float4(uvz.x, uvz.y, cascadeIndex, 1);
}

#endif