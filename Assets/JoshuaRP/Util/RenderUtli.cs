using UnityEngine;
using System.Collections.Generic;

namespace UnityEngine.Rendering.JsRP
{
public class RenderUtil
{
    static Mesh s_FullscreenMesh = null;
    public static Mesh fullscreenMesh
    {
        get
        {
            if (s_FullscreenMesh != null)
                return s_FullscreenMesh;

            float topV = 1.0f;
            float bottomV = 0.0f;

            s_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
            s_FullscreenMesh.SetVertices(new List<Vector3>
            {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f,  1.0f, 0.0f),
                new Vector3(1.0f, -1.0f, 0.0f),
                new Vector3(1.0f,  1.0f, 0.0f)
            });

            s_FullscreenMesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0.0f, bottomV),
                new Vector2(0.0f, topV),
                new Vector2(1.0f, bottomV),
                new Vector2(1.0f, topV)
            });

            s_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
            s_FullscreenMesh.UploadMeshData(true);
            return s_FullscreenMesh;
        }
    }
}
}