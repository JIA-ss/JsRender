#ifndef WEIGHTEDAVERAGE_HLSL
#define WEIGHTEDAVERAGE_HLSL

#include "UnityCG.cginc"
#include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"

sampler2D _MainTex;
float _Metallic;
float _Roughness;
float4 _Color;

sampler2D _WeightedAverageAccumTexture;
sampler2D _WeightedAverageCountTexture;

struct WeightedAverageVertVaring
{
    float4 positionOS:  POSITION;
    float3 normalOS:  NORMAL;
    float2 uv:      TEXCOORD0;
};

struct WeightedAverageFragVaring
{
    float4 positionCS:  SV_POSITION;
    float3 normalWS:  NORMAL;
    float2 uv:      TEXCOORD0;
    float3 positionWS: TEXCOORD1;
    float3 positionVS: TEXCOORD2;
    float3 view: TEXCOORD3;
};

WeightedAverageFragVaring AccumVS(WeightedAverageVertVaring input)
{
    WeightedAverageFragVaring output;
    output.positionCS = UnityObjectToClipPos(input.positionOS);
    output.normalWS = UnityObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
    output.positionVS = UnityObjectToViewPos(input.positionOS);
    output.view = normalize(UnityWorldSpaceViewDir(output.positionWS));
    return output;
}

void AccumFS(WeightedAverageFragVaring input, out float4 accumColor :SV_TARGET0, out float4 accumCount :SV_TARGET1 )
{
    float4 color = tex2D(_MainTex, input.uv);
    float3 normal = normalize(input.normalWS);
    float3 L = _MainLightDirection;
    float3 V = normalize(input.view);

    float inShadow = 0.0; // ShadowReceiver(float4(input.positionWS, 1.0));
    float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor * (1.0 - inShadow);
    pbrcolor = pbrcolor * _Color.a;
    accumColor = float4(pbrcolor, 1.0) *_Color;
    accumCount = int4(1.0, 0, 0, 0);
}

WeightedAverageFragVaring FinalVS(WeightedAverageVertVaring input)
{
    return AccumVS(input);
}

float4 FinalFS(WeightedAverageFragVaring i) : SV_TARGET
{
    float4 accum = tex2D(_WeightedAverageAccumTexture, i.uv);
    float3 accumCi = accum.rgb;
    float accumAlpha = accum.a;
    if (accumAlpha < 0.0001)
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }
    float n = tex2D(_WeightedAverageCountTexture, i.uv).r;
    float SrcAlpha = pow(max(0.0, 1.0 - accumAlpha / n), n);
    float3 SrcColor = accumCi / max(accumAlpha, 0.0001);
    return float4(SrcColor, SrcAlpha);
}

#endif