#ifndef LIGHTING_HLSL
#define LIGHTING_HLSL


sampler2D _ShadowMap;
float4 _ShadowMap_TexelSize; // x: 1/width, y: 1/height, z: width, w: height
float3 _MainLightDirection;
float3 _MainLightColor;
float _MainLightIntensity;
float4 _MainLightShadowBias; // x: depth bias, y: normal bias
float4x4 _WorldToMainLightShadowmapMatrix;
float4 _CameraNearFar;


#endif