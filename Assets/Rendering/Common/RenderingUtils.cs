using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public class RenderingUtils
{
    public static void SetupGlobalParametersCommand(ScriptableRenderContext context, RenderingData renderingData)
    {
        CommandBuffer cmd = new CommandBuffer() { name = "SetupGlobalParametersCommand" };
        Light mainLight = renderingData.lightData.mainLight;
        Vector3 lightDirection = renderingData.lightData.lightDirection;
        cmd.SetGlobalColor(ShaderID._MainLightColor, mainLight.color);
        cmd.SetGlobalFloat("_MainLightIntensity", mainLight.intensity);
        cmd.SetGlobalVector("_MainLightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));
        context.ExecuteCommandBuffer(cmd);
    }

    public static readonly int _GlobalDepthBufferBits = 24;
    public static RenderTextureDescriptor GetGlobalDepthAttachmentDescriptor(RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        RenderTextureDescriptor desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.Depth, _GlobalDepthBufferBits);
        return desc;
    }
    public static RenderTextureDescriptor GetGlobalColorAttachmentDescriptor(RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        RenderTextureDescriptor desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.ARGBFloat, 0);
        return desc;
    }
}
