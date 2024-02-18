#ifndef DWAW_OBJECT_PASS_OPAQUE_HLSL
#define DWAW_OBJECT_PASS_OPAQUE_HLSL


#include "Assets/Rendering/Shaders/Common/Lighting.hlsl"
#include "UnityCG.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;
float _Metallic;
float _Roughness;
float _AO;

struct DrawOpaqueFragmentVaring
{
    float4 vertex:  SV_POSITION;
    float3 normal:  NORMAL;
    float2 uv:      TEXCOORD0;
    float3 worldPos: TEXCOORD1;
    float3 positionOS: TEXCOORD2;
};

DrawOpaqueFragmentVaring DrawOpaqueVertex(appdata_base v)
{
    DrawOpaqueFragmentVaring o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
    o.normal = UnityObjectToWorldNormal(v.normal);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.positionOS = v.vertex.xyz;
    return o;
}

float4 DrawOpaqueFragment(DrawOpaqueFragmentVaring i) : SV_TARGET
{

    float4 color = tex2D(_MainTex, i.uv);
    float3 normal = normalize(i.normal);
    float3 L = normalize(_MainLightPosition);
    float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

    LightingData lightingData = InitializeLightingData(color, i.normal, i.worldPos, V, i.uv, _Metallic, _Roughness);
    Light light;
    light.color = _MainLightColor;
    light.direction = L;
    light.distanceAttenuation = 1.0;
    light.specularIntensity = 1.0;
    light.shadowAttenuation = 1.0;
    color = float4(Lighting(lightingData, light),1.0);

    for (uint lightIndex = 0u; lightIndex < _AdditionalLightsCount.x; ++lightIndex)
    {
        light = GetAdditionalPerObjectLight(lightIndex, i.worldPos);
        color += float4(Lighting(lightingData, light),1.0);
    }
    return color;
    
    // float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L, _MainLightColor);
    // float4 finalColor = float4(pbrcolor, 1.0);
    // float inShadow = 0.0; // ShadowReceiver(float4(i.worldPos, 1.0));
    // // return ShadowReceiverDebug(float4(i.worldPos, 1.0));
    // // return 1.0 - inShadow;
    // // return finalColor;
    // return finalColor * (1.0 - inShadow);
    // return _MainLightColor;
}

#endif