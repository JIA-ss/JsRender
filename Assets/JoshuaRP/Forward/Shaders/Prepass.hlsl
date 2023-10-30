#ifndef PREPASS_HLSL
#define PREPASS_HLSL


struct PrepassVertexVaring
{
    float4 positionOS : POSITION;
    float3 normalOS :  NORMAL;
    float2 uv : TEXCOORD0;
};

struct PrepassFragmentVaring
{
    float4 positionCS : SV_POSITION;
};

PrepassFragmentVaring PrepassVS (PrepassVertexVaring input)
{
    PrepassFragmentVaring output;
    output.positionCS = UnityObjectToClipPos(input.positionOS);
    return output;
}

float4 PrepassFS(PrepassFragmentVaring input) : SV_TARGET
{
    return 1.0;
}




#endif