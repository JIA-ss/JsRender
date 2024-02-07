using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum HdrFormat
{
    R11G11B10,
    R16G16B16A16
}

public enum LightRenderingMode
{
    Disabled = 0,
    PerVertex = 2,
    PerPixel = 1,
}

public enum ShadowCascadesOption
{
    NoCascades,
    TwoCascades,
    FourCascades,
}

public abstract class BaseRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] public List<string> renderPassNames = new List<string>();
    [SerializeField] public int screenHeight = 720;
    [SerializeField] public MSAASamples msaaQuality = MSAASamples.None;
    [SerializeField] public bool supportsHDR = false;
    [SerializeField] public HdrFormat hdrFormat = HdrFormat.R11G11B10;
    [SerializeField] public float renderScale = 1.0f;
    [SerializeField] public float shadowDistance = 50.0f;
    [SerializeField] public bool enablePostProcessing = true;
    [SerializeField] public int maxVisibleAdditionalLight = 256;
    [SerializeField] public int additionalLightsPerObjectLimit = 4;
    [SerializeField] public LightRenderingMode mainLightRenderingMode = LightRenderingMode.PerPixel;
    [SerializeField] public LightRenderingMode additionalLightsRenderingMode = LightRenderingMode.PerPixel;
    [SerializeField] public bool supportsMixedLighting = true;
    [SerializeField] public bool supportsMainLightShadows = true;
    [SerializeField] public ShadowCascadesOption shadowCascadeOption = ShadowCascadesOption.NoCascades;
    [SerializeField] public float cascade2Split = 0.25f;
    [SerializeField] public Vector3 cascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
    [SerializeField] public int mainLightShadowmapResolution = 2048;
    [SerializeField] public int additionalLightsShadowmapResolution = 512;
    [SerializeField] public bool supportsAdditionalLightShadows = false;
    [SerializeField] public bool supportsSoftShadows = true;

    protected List<RenderPassBase> CreateRenderPassesByConfig()
    {
        List<RenderPassBase> renderPasses = new List<RenderPassBase>();
        foreach (string passType in renderPassNames)
        {
            Type type = Type.GetType(passType);
            Debug.Assert(type != null);

            RenderPassBase renderPass = (RenderPassBase)Activator.CreateInstance(type);
            Debug.Assert(renderPass != null);

            renderPasses.Add(renderPass);
        }
        Debug.Assert(renderPasses.Count > 0);
        return renderPasses;
    }
}