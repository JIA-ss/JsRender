Shader "JsRP/Forward/Transparent"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white"{}
        _Color("Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.5 
        _Roughness("Roughness", Range(0.0, 1.0)) = 0.6
    }
    SubShader
    {
        Pass
        {
            Name "AlphaBlend"
            Tags {"LightMode"="AlphaBlend" "RenderQueue"="Transparent"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma fragment frag

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 view : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
            };
            #include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Metallic;
            float _Roughness;
            float4 _Color;

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.view = normalize(UnityWorldSpaceViewDir(v.vertex));
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f input) : SV_Target
            {

                float4 color = tex2D(_MainTex, input.uv);
                float3 normal = normalize(input.normalWS);
                float3 L = _MainLightDirection;
                float3 V = normalize(input.view);

                float inShadow = 0.0; // ShadowReceiver(float4(input.worldPos, 1.0));
                float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor * (1.0 - inShadow);

                return float4(pbrcolor, 1.0) *_Color;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthPeeling"
            Tags {"LightMode"="DepthPeeling" "RenderQueue"="Transparent"}
            Cull Off
            ZWrite On
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/Transparent/DepthPeeling.hlsl"
            #pragma vertex DepthPeelingVert
            #pragma fragment DepthPeelingFrag
            ENDHLSL
        }

        Pass
        {
            Name "WeightedSum"
            Tags {"LightMode"="WeightedSum" "RenderQueue"="Transparent"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #include "UnityCG.cginc"
            #include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"
            #pragma vertex vert
            #pragma fragment frag

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

            fixed4 frag(v2f input) : SV_Target
            {
                float3 c0 = tex2D(_OpaqueColor, int2(input.pos.xy)).rgb;

                float4 color = tex2D(_MainTex, input.uv);
                float3 normal = normalize(input.normalWS);
                float3 L = _MainLightDirection;
                float3 V = normalize(input.view);
                float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor;
                // pbrcolor = pbrcolor * _Color.a;
                float4 ci = float4(pbrcolor, 1.0) * _Color;
                return float4(ci.rgb - ci.a * c0.rgb, 1.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "WeightedAverageAccum"
            Tags {"LightMode" = "WeightedAverageAccum"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One
            HLSLPROGRAM
            #include "WeightedAverage.hlsl"
            #pragma vertex AccumVS
            #pragma fragment AccumFS
            ENDHLSL
        }

        Pass
        {
            Name "WeightedAverageFinal"
            Tags {"LightMode" = "WeightedAverageFinal"}
            Cull Off
            ZWrite Off
            ZTest Always
            Blend OneMinusSrcAlpha SrcAlpha
            HLSLPROGRAM
            #include "WeightedAverage.hlsl"
            #pragma vertex FinalVS
            #pragma fragment FinalFS
            ENDHLSL
        }

        Pass
        {
            Name "DepthWeightedAccum"
            Tags {"LightMode" = "DepthWeightedAccum"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One, Zero OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex AccumVS
            #pragma fragment DepthWeightedAccumFS

            #pragma multi_compile DEPTH_WEIGHTED_FUNC0 DEPTH_WEIGHTED_FUNC1 DEPTH_WEIGHTED_FUNC2 DEPTH_WEIGHTED_FUNC3 DEPTH_WEIGHTED_FUNC4
            #include "DepthWeighted.hlsl"

            ENDHLSL
        }
        Pass
        {
            Name "DepthWeightedFinal"
            Tags {"LightMode" = "DepthWeightedFinal"}
            Cull Off
            ZWrite Off
            ZTest Always
            Blend OneMinusSrcAlpha SrcAlpha
            HLSLPROGRAM
            #pragma vertex FinalVS
            #pragma fragment DepthWeightedFinalFS

            #include "DepthWeighted.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags {"LightMode" = "ShadowCaster"}
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"

            #pragma vertex ShadowCasterVS
            #pragma fragment ShadowCasterFS

            ENDHLSL
        }


        Pass
        {
            Name "ThreePassWeightedPass1"
            Tags {"LightMode"="ThreePassWeightedPass1" "RenderQueue"="Transparent"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One, One One
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/Transparent/ThreePassWeighted.hlsl"
            #pragma vertex vert
            #pragma fragment pass1
            ENDHLSL
        }

        Pass
        {
            Name "ThreePassWeightedPass2"
            Tags {"LightMode"="ThreePassWeightedPass2" "RenderQueue"="Transparent"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend Zero SrcColor, Zero SrcColor
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/Transparent/ThreePassWeighted.hlsl"
            #pragma vertex vert
            #pragma fragment pass2
            ENDHLSL
        }

        Pass
        {
            Name "ThreePassWeightedPass3"
            Tags {"LightMode"="ThreePassWeightedPass3" "RenderQueue"="Transparent"}
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One One
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/Transparent/ThreePassWeighted.hlsl"
            #pragma vertex vert
            #pragma fragment pass3
            ENDHLSL
        }
    }
}