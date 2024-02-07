#ifndef PRE_Z_PASS_HLSL
#define PRE_Z_PASS_HLSL

#include "UnityCG.cginc"
#include "Assets/Rendering/Shaders/Common/PBR.hlsl"

sampler2D _MainTex;
float4 _MainTex_ST;
float _Metallic;
float _Roughness;
float _AO;

struct DrawOpaqueFragmentVaring
{
    float2 uv:      TEXCOORD0;
    float4 vertex:  SV_POSITION;
    float3 normal:  NORMAL;
    float3 view: TEXCOORD1;
    float3 worldPos: TEXCOORD2;
    float3 positionOS: TEXCOORD3;
};

DrawOpaqueFragmentVaring DrawOpaqueVertex(appdata_base v)
{
    DrawOpaqueFragmentVaring o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
    o.normal = UnityObjectToWorldNormal(v.normal);
    o.view = UnityWorldSpaceViewDir(v.vertex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.positionOS = v.vertex.xyz;
    return o;
}

float4 DrawOpaqueFragment(DrawOpaqueFragmentVaring i) : SV_TARGET
{
    float4 color = tex2D(_MainTex, i.uv);
    float3 normal = normalize(i.normal);
    float3 L = normalize(_MainLightPosition);
    float3 V = normalize(i.view);

    float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L, _MainLightColor);
    float4 finalColor = float4(pbrcolor, 1.0);
    float inShadow = 0.0; // ShadowReceiver(float4(i.worldPos, 1.0));
    // return ShadowReceiverDebug(float4(i.worldPos, 1.0));
    // return 1.0 - inShadow;
    // return finalColor;
    return finalColor * (1.0 - inShadow);
    // return _MainLightColor;
}

#endif