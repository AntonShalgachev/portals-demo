using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gamelogic.Extensions;

namespace UnityPrototype
{
    public class Portal : MonoBehaviour
    {
        [SerializeField] private Renderer m_viewportRenderer = null;
        [SerializeField] private Transform m_debugVisual = null;
        [SerializeField] private Camera m_portalCamera = null;

        public PortalsController m_controller => GetComponentInParent<PortalsController>(); // inefficient, but I don't care for now
        public Portal m_otherPortal => m_controller.GetOtherPortal(this);

        private RenderTexture m_texture = null;

        private void Awake()
        {
            m_texture = CreateRenderTexture(m_controller.activeCamera);
            m_viewportRenderer.material.mainTexture = m_texture;

            m_portalCamera.targetTexture = m_texture;
            m_portalCamera.transform.ResetLocal();
            UpdateCameraMatrix();
        }

        private void OnEnable()
        {
            if (m_controller != null)
                m_controller.RegisterPortal(this);
        }

        private void OnDisable()
        {
            if (m_controller != null)
                m_controller.DeregisterPortal(this);
        }

        private static RenderTexture CreateRenderTexture(Camera cam)
        {
            return new RenderTexture(cam.pixelWidth, cam.pixelHeight, 24, RenderTextureFormat.ARGB32);
        }

        private void Update()
        {
            var otherPortal = m_otherPortal;
            if (otherPortal == null)
                return;

            var viewCamera = m_controller.activeCamera;

            m_debugVisual.SetScaleXY(m_controller.portalSize.x, m_controller.portalSize.y);
            m_viewportRenderer.transform.SetScaleXY(m_controller.portalSize.x, m_controller.portalSize.y);

            UpdateCameraMatrix();

            var virtualCameraLocalOffset = transform.InverseTransformPoint(viewCamera.transform.position);
            var virtualCameraLocalDirection = transform.InverseTransformDirection(viewCamera.transform.forward);

            virtualCameraLocalOffset.Scale(new Vector3(-1.0f, 1.0f, -1.0f)); // position of the virtual camera relative to this portal
            virtualCameraLocalDirection.Scale(new Vector3(-1.0f, 1.0f, -1.0f)); // orientation of the virtual camera relative to this portal

            var virtualCameraWorldPosition = otherPortal.transform.TransformPoint(virtualCameraLocalOffset);
            var virtualCameraWorldDirection = otherPortal.transform.TransformDirection(virtualCameraLocalDirection);

            Debug.DrawLine(otherPortal.transform.position, virtualCameraWorldPosition, Color.blue);
            Debug.DrawLine(virtualCameraWorldPosition, virtualCameraWorldPosition + virtualCameraWorldDirection, Color.magenta);

            m_portalCamera.transform.position = virtualCameraWorldPosition;
            m_portalCamera.transform.LookAt(virtualCameraWorldPosition + virtualCameraWorldDirection);
        }

        private void UpdateCameraMatrix()
        {
            m_portalCamera.projectionMatrix = m_controller.activeCamera.projectionMatrix;
        }

        private void OnDrawGizmos()
        {
            if (m_controller != null)
            {
                Gizmos.color = Color.red;
                var oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                GizmosHelper.DrawWireRectangle(Vector3.zero, m_controller.portalSize);
                Gizmos.matrix = oldMatrix;
            }
        }
    }
}
