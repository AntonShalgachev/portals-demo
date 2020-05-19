using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gamelogic.Extensions;

namespace UnityPrototype
{
    public class Portal : MonoBehaviour
    {
        [SerializeField] private Renderer m_viewportRenderer = null;
        [SerializeField] private CollisionDetector m_objectDetector = null;
        [SerializeField] private Camera m_portalCamera = null;
        [SerializeField] private GameObject m_parentWall = null;

        public PortalsController m_controller => GetComponentInParent<PortalsController>(); // inefficient, but I don't care for now
        public Portal m_otherPortal => m_controller.GetOtherPortal(this);

        private RenderTexture m_texture = null;

        private int m_cullingMask = 0;

        private void Awake()
        {
            var viewCamera = m_controller.activeCamera;

            m_texture = CreateRenderTexture(viewCamera);
            m_viewportRenderer.material.mainTexture = m_texture;

            m_cullingMask = m_portalCamera.cullingMask;

            m_portalCamera.targetTexture = m_texture;
            m_portalCamera.transform.ResetLocal();
            SyncCameraMatrix(viewCamera);
        }

        private void OnEnable()
        {
            if (m_controller != null)
                m_controller.RegisterPortal(this);

            m_parentWall.layer = m_controller.GetWallLayer(this);
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

            if (m_viewportRenderer != null)
                m_viewportRenderer.transform.SetScaleXY(m_controller.portalSize.x, m_controller.portalSize.y);
            if (m_objectDetector != null)
                m_objectDetector.transform.SetScaleXY(m_controller.portalSize.x, m_controller.portalSize.y);

            var viewCamera = m_controller.activeCamera;
            SyncCameraMatrix(viewCamera);
            UpdateCameraTransform(viewCamera, otherPortal);

            m_portalCamera.cullingMask = m_cullingMask & ~(1 << m_controller.GetWallLayer(otherPortal));

            TeleportObjects(otherPortal);
        }

        private void UpdateCameraTransform(Camera viewCamera, Portal otherPortal)
        {
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

        private void SyncCameraMatrix(Camera viewCamera)
        {
            m_portalCamera.projectionMatrix = viewCamera.projectionMatrix;
        }

        private void TeleportObjects(Portal otherPortal)
        {
            foreach (var otherTransform in m_objectDetector.touchingTransforms)
            {
                if (otherTransform == null)
                    continue;

                var localPosition = transform.InverseTransformPoint(otherTransform.position);
                if (localPosition.z >= 0.0f)
                    continue;

                var localDirection = transform.InverseTransformDirection(otherTransform.forward);

                var flipRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
                localPosition = flipRotation * localPosition;
                localDirection = flipRotation * localDirection;

                var teleportedPosition = otherPortal.transform.TransformPoint(localPosition);
                var teleportedDirection = otherPortal.transform.TransformDirection(localDirection);
                otherTransform.position = teleportedPosition;
                otherTransform.LookAt(teleportedPosition + teleportedDirection);
            }
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
