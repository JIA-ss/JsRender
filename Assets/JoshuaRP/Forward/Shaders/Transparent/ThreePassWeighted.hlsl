#ifndef THREE_PASS_WEIGHTED_HLSL
#define THREE_PASS_WEIGHTED_HLSL

#include "UnityCG.cginc"
#include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"


struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float3 view : TEXCOORD2;
};

sampler2D _MainTex;
sampler2D _OpaqueColor;
float4 _MainTex_ST;
float4 _Color;
float _Metallic;
float _Roughness;

v2f vert(appdata_base v)
{
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
    o.normalWS = UnityObjectToWorldNormal(v.normal);
    o.view = normalize(UnityWorldSpaceViewDir(v.vertex));
    return o;
}

void pass1(v2f input, out float4 colorMulAlpha :SV_TARGET0, out float4 oneOverAlpha :SV_TARGET1 )
{
    float4 color = tex2D(_MainTex, input.uv);
    float3 normal = normalize(input.normalWS);
    float3 L = _MainLightDirection;
    float3 V = normalize(input.view);
    float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor;
    // pbrcolor = pbrcolor * _Color.a;
    float4 ci = float4(pbrcolor, 1.0) * _Color;

    colorMulAlpha = float4(ci.rgb * ci.a, ci.a);
    oneOverAlpha = float4(1.0 / ci.a, 1.0 / ci.a, 1.0 / ci.a, ci.a);
}

float4 pass2(v2f input) : SV_TARGET
{

    float4 color = tex2D(_MainTex, input.uv);
    float3 normal = normalize(input.normalWS);
    float3 L = _MainLightDirection;
    float3 V = normalize(input.view);
    float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor;
    // pbrcolor = pbrcolor * _Color.a;
    float4 ci = float4(pbrcolor, 1.0) * _Color;

    return ci;
}

sampler2D _ColorRGBA1;
sampler2D _ColorRGBA2;
sampler2D _ColorRGBA3;

float4 pass3(v2f input) : SV_TARGET
{
    float3 d0 = tex2D(_OpaqueColor, input.uv).rgb;

    float4 rgba1 = tex2D(_ColorRGBA1, input.uv);
    float4 rgba2 = tex2D(_ColorRGBA2, input.uv);
    float4 rgba3 = tex2D(_ColorRGBA3, input.uv);

    if (rgba3.a == 1)
    {
        rgba3 = 0.0;
    }

    float3 rgb1 = rgba1.rgb;
    float3 rgb2 = rgba2.rgb;
    float3 rgb3 = rgba3.rgb;

    return float4((rgb1 - d0 * rgba1.a) + (d0 * rgba3.a) + (d0 * rgb2 * rgba3.a), 1.0);
}


#endif
