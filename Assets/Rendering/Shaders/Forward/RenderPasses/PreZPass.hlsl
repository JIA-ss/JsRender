#ifndef PRE_Z_PASS_HLSL
#define PRE_Z_PASS_HLSL

#include "UnityCG.cginc"

struct PrepassFragmentVaring
{
    float4 positionCS : SV_POSITION;
};

PrepassFragmentVaring PrepassVertex(appdata_base input)
{
    PrepassFragmentVaring output;
    UNITY_INITIALIZE_OUTPUT(PrepassFragmentVaring, output);
    UNITY_SETUP_INSTANCE_ID(input);
    output.positionCS = UnityObjectToClipPos(input.vertex);
    return output;
}

float4 PrepassFragment(PrepassFragmentVaring input) : SV_TARGET
{
    return float4(0.2,0.3,0.4,1.0);
}

#endif