﻿struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float2 uv : TEXCOORD0;
    UNITY_FOG_COORDS(1)
    float4 vertex : SV_POSITION;
};

sampler2D _MainTex;
float4 _MainTex_ST;

v2f vert (appdata v)
{
    v2f o;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    UNITY_TRANSFER_FOG(o,o.vertex);
    return o;
}

fixed4 frag (v2f i) : SV_Target
{
#if defined(RED)
    return fixed4(1, 0, 0, 1);
#elif defined(GREEN)
    return fixed4(0, 1, 0, 1);
#elif defined(BLUE)
    return fixed4(0, 0, 1, 1);
#else
    return fixed4(0.1, 0.2, 0.3, 1);;
#endif
}