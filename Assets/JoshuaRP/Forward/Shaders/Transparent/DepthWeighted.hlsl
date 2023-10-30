#ifndef DEPTHWEIGHTED_HLSL
#define DEPTHWEIGHTED_HLSL

#include "WeightedAverage.hlsl"


float DepthWeighted(float z, float a)
{
    if (z <= 0.0)
    {
        z = -z;
    }
#if DEPTH_WEIGHTED_FUNC0
    return a * max(0.01, min(3000, 10 / (0.00001 + z * z * 0.04 + pow(z / 200, 6))));
#elif DEPTH_WEIGHTED_FUNC1
    return a * max(0.01, min(3000, 10 / (0.00001 + pow(0.1 * z, 3) + pow(z / 200, 6))));
#elif DEPTH_WEIGHTED_FUNC2
    return a * max(0.01, min(3000, 0.03 / (0.00001 + pow(z / 200, 4))));
#elif DEPTH_WEIGHTED_FUNC3
    return a * max(0.01, 3000 * (1 - (_CameraNearFar.x * _CameraNearFar.y / z - _CameraNearFar.y)/(_CameraNearFar.x - _CameraNearFar.y)));
#elif DEPTH_WEIGHTED_FUNC4
    return 1.0;
#endif
    return 1.0;
}


void DepthWeightedAccumFS(WeightedAverageFragVaring input, out float4 accumColor :SV_TARGET0, out float4 accumCount :SV_TARGET1 )
{
    float4 color = tex2D(_MainTex, input.uv);
    float3 normal = normalize(input.normalWS);
    float3 L = _MainLightDirection;
    float3 V = normalize(input.view);

    float inShadow = 0.0; // ShadowReceiver(float4(input.positionWS, 1.0));
    float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor * (1.0 - inShadow);
    pbrcolor = pbrcolor * _Color.a;

    float3 ci = pbrcolor * _Color.rgb;
    float ai = _Color.a;
    float wi = DepthWeighted(input.positionVS.z, ai);
    accumColor = float4(ci * wi, ai);
    accumCount = float4(ai * wi, 0.0, 0.0, 0.0);
}


float4 DepthWeightedFinalFS(WeightedAverageFragVaring i) : SV_TARGET
{
    float4 accum = tex2D(_WeightedAverageAccumTexture, i.uv);
    float r = accum.a;
    if (r <= 0.000000001)
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }
    accum.a = tex2D(_WeightedAverageCountTexture, i.uv).r;
    if (accum.a >= 5e4)
    {
        accum.a = 5e4;
    }
    float3 bias = float3(0.1, 0.1, 0.1) * 0;
    return float4(accum.rgb / max(accum.a, 1e-4) - bias, r);
}

#endif