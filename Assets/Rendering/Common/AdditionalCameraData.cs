using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using System.ComponentModel;

public enum CameraRenderType
{
    Base,
    Overlay,
}

public enum AntialiasingMode
{
    None,
}

public enum AntialiasingQuality
{
    Low,
    Medium,
    High
}

[RequireComponent(typeof(Camera))]
public class AdditionalCameraData : MonoBehaviour
{
   
    public CameraRenderType renderType
    {
        get => m_CameraType;
        set => m_CameraType = value;
    }
    
    /// <summary>
    /// Returns the camera stack. Only valid for Base cameras.
    /// Overlay cameras have no stack and will return null.
    /// <seealso cref="CameraRenderType"/>.
    /// </summary>
    public List<Camera> cameraStack
    {
        get
        {
            if (renderType != CameraRenderType.Base)
            {
                var camera = gameObject.GetComponent<Camera>();
                Debug.LogWarning(string.Format("{0}: This camera is of {1} type. Only Base cameras can have a camera stack.", camera.name, renderType));
                return null;
            }

            if (renderer.supportedRenderingFeatures.cameraStacking == false)
            {
                var camera = gameObject.GetComponent<Camera>();
                Debug.LogWarning(string.Format("{0}: This camera has a ScriptableRenderer that doesn't support camera stacking. Camera stack is null.", camera.name));
                return null;
            }

            return m_Cameras;
        }
    }
    

    [SerializeField] CameraRenderType m_CameraType = CameraRenderType.Base;
    [SerializeField] List<Camera> m_Cameras = new List<Camera>();
    [SerializeField] LayerMask m_VolumeLayerMask = 1; // "Default"
    [SerializeField] Transform m_VolumeTrigger = null;
    [SerializeField] AntialiasingMode m_Antialiasing = AntialiasingMode.None;
    [SerializeField] AntialiasingQuality m_AntialiasingQuality = AntialiasingQuality.High;
    [SerializeField] bool m_RenderPostProcessing = false;
    [SerializeField] bool m_RenderShadows = true;
    [SerializeField] bool m_ClearDepth = true;

    public ShadowCascadesOption shadowCascadeOption = ShadowCascadesOption.NoCascades;
    public float cascade2Split = 0.25f;
    public Vector3 cascade4Split = new Vector3(0.067f, 0.2f, 0.467f);
    public bool overrideShadowCascade = false;
    public bool overrideShadowResolutionFactor = false;
    public float shadowResolutionFactor = 1.0f;
    public BaseRenderer renderer
    {
        get => BaseRenderPipeline.Renderer;
    }

    public LayerMask volumeLayerMask
    {
        get => m_VolumeLayerMask;
        set => m_VolumeLayerMask = value;
    }
    
    public Transform volumeTrigger
    {
        get => m_VolumeTrigger;
        set => m_VolumeTrigger = value;
    }
    
    public AntialiasingMode antialiasing
    {
        get => m_Antialiasing;
        set => m_Antialiasing = value;
    }
    
    public AntialiasingQuality antialiasingQuality
    {
        get => m_AntialiasingQuality;
        set => m_AntialiasingQuality = value;
    }
    
    public bool renderPostProcessing
    {
        get => m_RenderPostProcessing;
        set => m_RenderPostProcessing = value;
    }
    
    public bool renderShadows
    {
        get => m_RenderShadows;
        set => m_RenderShadows = value;
    }
    
    public bool clearDepth
    {
        get => m_ClearDepth;
    }

    public bool overrideShadowDistance = false;
    public float shadowDistance;

    public void OnDrawGizmos()
    {
        string path = "Packages/com.lilith.render-pipelines.lit/Editor/Gizmos/";
        string gizmoName = "";
        Color tint = Color.white;

        if (m_CameraType == CameraRenderType.Base)
        {
            gizmoName = $"{path}Camera_Base.png";
        }
        else if (m_CameraType == CameraRenderType.Overlay)
        {
            gizmoName = $"{path}Camera_Overlay.png";
        }

#if UNITY_2019_2_OR_NEWER
#if UNITY_EDITOR
        if (Selection.activeObject == gameObject)
        {
            // Get the preferences selection color
            tint = SceneView.selectedOutlineColor;
        }
#endif
        if (!string.IsNullOrEmpty(gizmoName))
        {
            Gizmos.DrawIcon(transform.position, gizmoName, true, tint);
        }
        
        Gizmos.DrawIcon(transform.position, $"{path}Camera_PostProcessing.png", true, tint);
#else
        Gizmos.DrawIcon(transform.position, $"{path}Camera_PostProcessing.png");
        Gizmos.DrawIcon(transform.position, gizmoName);
#endif
    }
}