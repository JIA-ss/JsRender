using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ShaderID
{
    public static readonly int _CameraColorAttachment = Shader.PropertyToID("_CameraColorAttachment");
    public static readonly int _CameraDepthAttachment = Shader.PropertyToID("_CameraDepthAttachment");
    public static readonly int _DepthTexture = Shader.PropertyToID("_DepthTexture");
    public static readonly int _MainLightColor = Shader.PropertyToID("_MainLightColor");
    public static readonly int _MainLightPosition = Shader.PropertyToID("_MainLightPosition");
    public static readonly int _MainLightIntensity = Shader.PropertyToID("_MainLightIntensity");
}

public static class ShaderKeywordStrings
{
    public static readonly string MainLightShadows = "_MAIN_LIGHT_SHADOWS";
    public static readonly string MainLightShadowCascades = "_MAIN_LIGHT_SHADOWS_CASCADE";
    public static readonly string AdditionalLightsVertex = "_ADDITIONAL_LIGHTS_VERTEX";
    public static readonly string AdditionalLightsPixel = "_ADDITIONAL_LIGHTS";
    public static readonly string AdditionalLightShadows = "_ADDITIONAL_LIGHT_SHADOWS";
    public static readonly string SoftShadows = "_SHADOWS_SOFT";
    public static readonly string MixedLightingSubtractive = "_MIXED_LIGHTING_SUBTRACTIVE";
    public static readonly string LinearToSRGBConversion = "_LINEAR_TO_SRGB_CONVERSION";
}

public static class ShaderPropertyId
{
    public static readonly int scaledScreenParams = Shader.PropertyToID("_ScaledScreenParams");
    public static readonly int worldSpaceCameraPos = Shader.PropertyToID("_WorldSpaceCameraPos");
    public static readonly int screenParams = Shader.PropertyToID("_ScreenParams");
    public static readonly int projectionParams = Shader.PropertyToID("_ProjectionParams");
    public static readonly int zBufferParams = Shader.PropertyToID("_ZBufferParams");
    public static readonly int orthoParams = Shader.PropertyToID("unity_OrthoParams");

    public static readonly int mipmapBias = Shader.PropertyToID("_MipmapBias");

    public static readonly int viewMatrix = Shader.PropertyToID("unity_MatrixV");
    public static readonly int projectionMatrix = Shader.PropertyToID("glstate_matrix_projection");
    public static readonly int viewAndProjectionMatrix = Shader.PropertyToID("unity_MatrixVP");

    public static readonly int inverseViewMatrix = Shader.PropertyToID("unity_MatrixInvV");
    // Undefined:
    // public static readonly int inverseProjectionMatrix = Shader.PropertyToID("unity_MatrixInvP");
    public static readonly int inverseViewAndProjectionMatrix = Shader.PropertyToID("unity_MatrixInvVP");
    public static readonly int prevViewProjMatrix = Shader.PropertyToID("_PrevViewProjMatrix");
    public static readonly int nonJitteredViewProjMatrix = Shader.PropertyToID("_NonJitteredViewProjMatrix");
    public static readonly int currentJitter = Shader.PropertyToID("_CurrentJitter");
    public static readonly int prevJitter = Shader.PropertyToID("_PrevJitter");

    public static readonly int cameraProjectionMatrix = Shader.PropertyToID("unity_CameraProjection");
    public static readonly int inverseCameraProjectionMatrix = Shader.PropertyToID("unity_CameraInvProjection");
    public static readonly int worldToCameraMatrix = Shader.PropertyToID("unity_WorldToCamera");
    public static readonly int cameraToWorldMatrix = Shader.PropertyToID("unity_CameraToWorld");
}