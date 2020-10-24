using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityPrototype
{
    public class PortalCamera : MonoBehaviour
    {
        private Camera m_camera;
        private Transform m_clippingPlane = null;

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            RenderPipelineManager.endCameraRendering += EndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        }

        public void SyncCameraMatrix(Camera viewCamera)
        {
            if (m_camera == null)
                return;

            m_camera.projectionMatrix = viewCamera.projectionMatrix;
        }

        public void SetClippingPlane(Transform clippingPlane)
        {
            m_clippingPlane = clippingPlane;
        }

        private void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_camera)
                return;

            Shader.SetGlobalInt("_PortalCullMode", (int)UnityEngine.Rendering.CullMode.Back);

            Shader.SetGlobalInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Equal);
            Shader.SetGlobalInt("_StencilRef", 1);

            if (m_clippingPlane != null && m_clippingPlane.gameObject.activeInHierarchy)
            {
                Plane plane = new Plane(m_clippingPlane.up, m_clippingPlane.position);

                Vector4 planeRepresentation = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                Shader.SetGlobalVector("_ClippingPlane", planeRepresentation);
                Shader.SetGlobalInt("_ClippingPlaneEnabled", 1);
            }
        }

        private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_camera)
                return;

            Shader.SetGlobalInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Disabled);

            Shader.SetGlobalInt("_PortalCullMode", (int)UnityEngine.Rendering.CullMode.Off);
            Shader.SetGlobalInt("_ClippingPlaneEnabled", 0);
        }
    }
}
