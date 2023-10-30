#ifndef GAUSSIAN_HLSL
#define GAUSSIAN_HLSL

#if GAUSSIANCUSTOM
const static int kTapCount = 7;
const static float kOffsets[] = {
    -6,-2,-1,0,1,2,6
};
const static float kCoeffs[] = {
    0.05,0.1,0.15,0.4,0.15,0.1,0.05
};
#elif GAUSSIAN9x9
        const static int kTapCount = 5;
        const static float kOffsets[] = {
            -3.23076923,
            -1.38461538,
             0.00000000,
             1.38461538,
             3.23076923
        };
        const static float kCoeffs[] = {
             0.07027027,
             0.31621622,
             0.22702703,
             0.31621622,
             0.07027027
        };
#elif GAUSSIAN5x5
    const static int kTapCount = 3;
         const static float kOffsets[] = {
            -1.33333333,
             0.00000000,
             1.33333333
        };
        const static float kCoeffs[] = {
             0.35294118,
             0.29411765,
             0.35294118
        };
#elif GAUSSIAN3x3

const static int kTapCount = 4;
const static float2 kOffsets[] = {
   float2(-0.6, -0.6),
   float2(-0.6,  0.6),
   float2( 0.6,  -0.6),
   float2( 0.6, 0.6)
};
const static float kCoeffs[] = {
     0.25, 0.25, 0.25, 0.25
};

#endif

sampler2D _MainTex;
float4 _TargetBlurRt_TexelSize;
float _BlurRadius;
float4 Gaussian(float2 uv, int2 dir)
{
    float4 blurredColor = 0.0;

    for (int i = 0; i < kTapCount; i++)
    {
        float2 tapUV = uv + kOffsets[i] * dir * _TargetBlurRt_TexelSize.xy;
        float4 color = tex2D(_MainTex, tapUV);
        blurredColor += kCoeffs[i] * color;
    }
    return blurredColor;
}

float4 FragBlurH(float2 uv)
{
    return Gaussian(uv, int2(_BlurRadius, 0));
}

float4 FragBlurV(float2 uv)
{
    return Gaussian(uv, int2(0, _BlurRadius));
}

float4 FragBlurOnePass(float2 uv)
{
    float4 blurredColor = 0.0;

    for (int i = 0; i < kTapCount; i++)
    {
        float2 tapUV = uv + kOffsets[i] * _TargetBlurRt_TexelSize.xy;
        float4 color = tex2D(_MainTex, tapUV);
        blurredColor += kCoeffs[i] * color;
    }

    // return tex2D(_MainTex, uv);
    return blurredColor;
}


#endif
