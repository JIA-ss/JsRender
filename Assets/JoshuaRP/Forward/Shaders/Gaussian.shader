Shader "JsRP/Forward/Gaussian"
{

    Properties
    {
        _MainTex("Texture", 2D) = "white"{}
    }

    HLSLINCLUDE
    #include "UnityCG.cginc"
    struct appdata
    {
        float4 vertex:  POSITION;
        float2 uv:      TEXCOORD0;
    };

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert(appdata i)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(i.vertex);
        o.uv = i.uv;
        return o;
    }
    ENDHLSL

    SubShader
    {
        Pass
        {
            Name "GaussianOnPass"
            Tags {"LightMode"="Gaussian"}
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/Gaussian.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile GAUSSIAN5x5 GAUSSIAN3x3 GAUSSIAN9x9
            float4 frag(v2f i) :SV_TARGET
            {
                return FragBlurOnePass(i.uv);
            }

            ENDHLSL
        }

        Pass
        {
            Name "GaussianV"
            Tags {"LightMode" = "Gaussian"}
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #include "Assets/JoshuaRP/Forward/Shaders/Gaussian.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile GAUSSIAN3x3 GAUSSIAN5x5 GAUSSIAN9x9 GAUSSIANCUSTOM
            float4 frag(v2f i) :SV_TARGET
            {
                return FragBlurV(i.uv);
            }

            ENDHLSL
        }

        Pass
        {
            Name "GaussianV"
            Tags {"LightMode" = "Gaussian"}
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #include "Assets/JoshuaRP/Forward/Shaders/Gaussian.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile GAUSSIAN3x3 GAUSSIAN5x5 GAUSSIAN9x9 GAUSSIANCUSTOM
            float4 frag(v2f i) :SV_TARGET
            {
                return FragBlurH(i.uv);
            }

            ENDHLSL
        }

    }
}