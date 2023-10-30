using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.JsRP
{
public struct RenderingData
{
    public CameraData cameraData;
    public LightData lightData;
    public ShadowData shadowData;
    public ResourceData rtList;
}

public struct CameraData
{
    public Camera camera;
    public ScriptableCullingParameters cullingParameters;
    public CullingResults cullingResults;
}

public struct LightData
{
    public int mainLightIndex;
    public float mainLightIntensity;
}

public struct ShadowData
{
    public Matrix4x4 viewMatrix;
    public Matrix4x4 projectionMatrix;
}

public struct ResourceData
{
    public RTInfo[] rts;
}

public struct RTInfo
{
    public RenderTargetIdentifier identifier;
    public RenderTextureDescriptor descriptor;
}


public struct RTHandler
{
    public RTHandler(string name)
    {
        _name = name;
        _nameId = Shader.PropertyToID(name);
        _identifier = new RenderTargetIdentifier(_nameId);
    }
    public string name {get {return _name;}}
    public int id {get {return _nameId;}}
    public RenderTargetIdentifier identifier {get {return _identifier;}}
    int _nameId;
    string _name;
    RenderTargetIdentifier _identifier;
}
}