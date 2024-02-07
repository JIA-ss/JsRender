Shader "CustomRP/CommonOpaque"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white"{}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.5
        _Roughness("Roughness", Range(0.0, 1.0)) = 0.3
        _AO("AO", Range(0.0, 1.0)) = 0.2
    }
    SubShader
    {
        Pass
        {
            Tags { "RenderType" = "Opaque" "LightMode" = "PreZPass"}
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #include "Assets/Rendering/Shaders/Forward/RenderPasses/PreZPass.hlsl"
            #pragma vertex PrepassVertex
            #pragma fragment PrepassFragment
            ENDHLSL
        }
        Pass
        {
            Tags { "RenderType"="Opaque" "LightMode"="DrawObjectPassOpaque"}
            ZWrite On
            ZTest Equal
            HLSLPROGRAM
            #include "Assets/Rendering/Shaders/Forward/RenderPasses/DrawObjectPassOpaque.hlsl"
            #pragma vertex DrawOpaqueVertex
            #pragma fragment DrawOpaqueFragment
            ENDHLSL
        }
    }
}
