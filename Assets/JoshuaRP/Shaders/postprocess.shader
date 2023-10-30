Shader "JoshuaRP/postprocess"
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

            sampler2D _lightRt;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }


            fixed4 frag (v2f i) : SV_TARGET
            {
                return tex2D(_lightRt, i.uv);
            }
            ENDCG
        }
    }
}