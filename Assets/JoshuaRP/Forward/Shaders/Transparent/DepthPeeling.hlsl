#ifndef DEPTH_PEELING_HLSL
#define DEPTH_PEELING_HLSL

#include "UnityCG.cginc"
#include "../ForwardCommon.hlsl"

struct DepthPeelingVertVaring
{
    float4 positionOS:  POSITION;
    float3 normalOS:  NORMAL;
    float2 uv:      TEXCOORD0;
};

struct DepthPeelingFragVaring
{
    float4 positionCS:  SV_POSITION;
    float3 normalWS:  NORMAL;
    float2 uv:      TEXCOORD0;
    float3 view: TEXCOORD1;
    float3 worldPos: TEXCOORD2;
    float3 positionOS: TEXCOORD3;
};

sampler2D _DepthPeelingLEqualDepth;
float4 _ScreenSize;


DepthPeelingFragVaring DepthPeelingVert(DepthPeelingVertVaring input)
{
    DepthPeelingFragVaring output;
    output.positionCS = UnityObjectToClipPos(input.positionOS);
    output.uv = input.uv;
    output.normalWS = UnityObjectToWorldNormal(input.normalOS);
    output.view = normalize(UnityWorldSpaceViewDir(input.positionOS));
    output.worldPos = mul(unity_ObjectToWorld, input.positionOS).xyz;
    output.positionOS = input.positionOS.xyz;
    return output;
}
sampler2D _MainTex;
float4 _Color;
float _Metallic;
float _Roughness;
float4 DepthPeelingFrag(DepthPeelingFragVaring input) : SV_TARGET
{
    float depth = input.positionCS.z;
    float2 depthUV = input.positionCS.xy * _ScreenSize.zw;
    float minDepth = tex2D(_DepthPeelingLEqualDepth, depthUV).r;
#if UNITY_REVERSED_Z
    if (depth < minDepth + 0.0001) { discard; }
#else
    if (depth > minDepth - 0.0001) { discard; }
#endif
    float4 color = tex2D(_MainTex, input.uv);
    float3 normal = normalize(input.normalWS);
    float3 L = _MainLightDirection;
    float3 V = normalize(input.view);

    float inShadow = 0.0; // ShadowReceiver(float4(input.worldPos, 1.0));
    float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor *(1.0 - inShadow);
    return float4(pbrcolor, 1.0) *_Color;
}

#endif