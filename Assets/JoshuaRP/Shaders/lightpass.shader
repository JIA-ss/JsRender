Shader "JoshuaRP/lightpass"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white"{}
    }
    SubShader
    {
        Cull Off ZWrite On ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "BRDF.cginc"

            struct appdata
            {
                float4 vertex:  POSITION;
                float2 uv:      TEXCOORD0;
            };

            struct v2f
            {
                float2 uv:      TEXCOORD0;
                float4 vertex:  SV_POSITION;
            };


            float4x4 _vpMatrix;
            float4x4 _vpMatrixInv;
            sampler2D _gdepth;
            sampler2D _GT0;
            sampler2D _GT1;
            sampler2D _GT2;
            sampler2D _GT3;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }


            fixed4 frag (v2f i, out float depthOut : SV_Depth) : SV_TARGET
            {
                float2 uv = i.uv;
                float4 GT2 = tex2D(_GT2, uv);
                float4 GT3 = tex2D(_GT3, uv);

                float3 albedo = tex2D(_GT0, uv).rgb;
                float3 normal = tex2D(_GT1, uv).rgb * 2 - 1;

                float2 motionVec = GT2.rg;
                float roughness = GT2.b;
                float metallic = GT2.a;
                float3 emission = GT3.rgb;
                float occlusion = GT3.a;

                float d = UNITY_SAMPLE_DEPTH(tex2D(_gdepth, uv));
                float d_lin = Linear01Depth(d);
                depthOut = d;

                // 反投影重建世界坐标
                float4 ndcPos = float4(uv*2-1, d, 1);
                float4 worldPos = mul(_vpMatrixInv, ndcPos);
                worldPos /= worldPos.w;

                // 计算参数
                float3 color = float3(0, 0, 0);
                float3 N = normalize(normal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
                float3 radiance = float3(1, 1, 1); //_LightColor0.rgb;

                float3 direct = PBR(N, V, L, albedo, radiance, roughness, metallic);

                return float4(direct, 1);
            }
            ENDCG
        }
    }
}