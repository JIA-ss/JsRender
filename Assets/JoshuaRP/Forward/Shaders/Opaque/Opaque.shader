Shader "JsRP/Forward/Opaque"
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
            Tags {"LightMode" = "JsRP PrePass"}
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"
            #pragma vertex PrepassVS
            #pragma fragment PrepassFS
            ENDHLSL
        }

        Pass
        {
            Tags {"LightMode" = "JsRP Opaque"}
            HLSLPROGRAM
            #include "Assets/JoshuaRP/Forward/Shaders/ForwardCommon.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex:  POSITION;
                float2 uv:      TEXCOORD0;
                float3 normal:  NORMAL;
            };

            struct v2f
            {
                float2 uv:      TEXCOORD0;
                float4 vertex:  SV_POSITION;
                float3 normal:  NORMAL;
                float3 view: TEXCOORD1;
                float3 worldPos: TEXCOORD2;
                float3 positionOS: TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Metallic;
            float _Roughness;
            float _AO;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = UnityWorldSpaceViewDir(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.positionOS = v.vertex.xyz;
                return o;
            }

            float4 frag (v2f i) : SV_TARGET
            {
                float4 color = tex2D(_MainTex, i.uv);
                float3 normal = normalize(i.normal);
                float3 L = _MainLightDirection;
                float3 V = normalize(i.view);
                float NdotL = saturate(dot(normal, L));
                float diffcoef = 0.8;
                float3 pbrcolor = MetallicWorkflowPBR(color, _Metallic, _Roughness, normal, V, L) * _MainLightColor;
                float4 finalColor = float4(pbrcolor, 1.0);
                float inShadow = ShadowReceiver(float4(i.worldPos, 1.0));
                // return ShadowReceiverDebug(float4(i.worldPos, 1.0));
                // return 1.0 - inShadow;
                // return finalColor;
                return finalColor * (1.0 - inShadow);
            }
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


    }
}