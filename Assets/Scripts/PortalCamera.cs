using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityPrototype
{
    public class PortalCamera : MonoBehaviour
    {
        private Camera m_camera;
        private Portal m_portal = null;

        private Transform m_clippingPlane = null;

        public new Camera camera => GetCamera();
        public Portal portal => GetPortal();

        private Camera GetCamera()
        {
            if (m_camera == null)
                m_camera = GetComponent<Camera>();

            return m_camera;
        }

        private Portal GetPortal()
        {
            if (m_portal == null)
                m_portal = GetComponentInParent<Portal>();

            return m_portal;
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

        public void SyncCameraTransform(Camera eyeCamera, int level)
        {
            Portal otherPortal = portal.otherPortal;

            SyncCameraMatrix(eyeCamera);
            UpdateCameraTransform(eyeCamera, portal, otherPortal, level);
            SetClippingPlane(otherPortal.clippingPlane);
        }

        public void ResetCameraTransform()
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void UpdateCameraTransform(Camera viewCamera, Portal thisPortal, Portal otherPortal, int level)
        {
            transform.position = Portal.FromToPortalMatrix(thisPortal, otherPortal).MultiplyPoint(viewCamera.transform.position);
            transform.rotation = Portal.FromToPortalRotation(thisPortal, otherPortal) * viewCamera.transform.rotation;
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

            CommandBuffer cmd = CommandBufferPool.Get("SetShaderVariables");

            cmd.SetGlobalInt("_PortalCullMode", (int)UnityEngine.Rendering.CullMode.Back);

            cmd.SetGlobalInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Equal);
            cmd.SetGlobalInt("_StencilRef", 1);

            if (m_clippingPlane != null && m_clippingPlane.gameObject.activeInHierarchy)
            {
                Plane plane = new Plane(m_clippingPlane.up, m_clippingPlane.position);

                Vector4 planeRepresentation = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                cmd.SetGlobalVector("_ClippingPlane", planeRepresentation);
                cmd.SetGlobalInt("_ClippingPlaneEnabled", 1);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

            context.Submit();
        }

        private void EndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_camera)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("UnsetShaderVariables");

            cmd.SetGlobalInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Disabled);

            cmd.SetGlobalInt("_PortalCullMode", (int)UnityEngine.Rendering.CullMode.Off);
            cmd.SetGlobalInt("_ClippingPlaneEnabled", 0);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

            context.Submit();

            // ResetCameraTransform();
        }
    }
}
