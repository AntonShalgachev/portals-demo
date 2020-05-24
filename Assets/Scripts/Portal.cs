using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gamelogic.Extensions;

namespace UnityPrototype
{
    public class Portal : MonoBehaviour
    {
        [SerializeField] private Renderer m_viewportRenderer = null;
        [SerializeField] private Renderer m_backViewportRenderer = null;
        [SerializeField] private CollisionDetector m_objectDetector = null;
        [SerializeField] private Camera m_portalCamera = null;
        [SerializeField] private GameObject m_parentWall = null;
        [SerializeField] private float m_teleportationThreshold = 0.0f;
        [SerializeField] private float m_backViewportScale = 100.0f;

        public PortalsController m_controller => GetComponentInParent<PortalsController>(); // inefficient, but I don't care for now
        public Portal m_otherPortal => m_controller.GetOtherPortal(this);

        private RenderTexture m_texture = null;

        private int m_cullingMask = 0;

        private void Awake()
        {
            var viewCamera = m_controller.activeCamera;

            m_texture = CreateRenderTexture(viewCamera);
            m_viewportRenderer.material.mainTexture = m_texture;
            m_backViewportRenderer.material.mainTexture = m_texture;

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

            m_objectDetector.onTransformExit += OnObjectExitedPortalDetector;
        }

        private void OnDisable()
        {
            if (m_controller != null)
                m_controller.DeregisterPortal(this);

            m_objectDetector.onTransformExit -= OnObjectExitedPortalDetector;
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
            if (m_backViewportRenderer != null)
                m_backViewportRenderer.transform.SetScaleXY(m_controller.portalSize.x * m_backViewportScale, m_controller.portalSize.y * m_backViewportScale);
            if (m_objectDetector != null)
                m_objectDetector.transform.SetScaleXY(m_controller.portalSize.x, m_controller.portalSize.y);

            var viewCamera = m_controller.activeCamera;
            SyncCameraMatrix(viewCamera);
            UpdateCameraTransform(viewCamera, otherPortal);

            m_portalCamera.cullingMask = m_cullingMask & ~(1 << m_controller.GetWallLayer(otherPortal));

            TeleportObjects(otherPortal);
        }

        private Quaternion MirrorRotation()
        {
            return Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }

        private Vector3 MirrorPointLocal(Vector3 position)
        {
            var localPosition = transform.InverseTransformPoint(position);
            return MirrorRotation() * localPosition;
        }

        private Vector3 MirrorDirectionLocal(Vector3 direction)
        {
            var localDirection = transform.InverseTransformDirection(direction);
            return MirrorRotation() * localDirection;
        }

        private void UpdateCameraTransform(Camera viewCamera, Portal otherPortal)
        {
            var virtualCameraLocalOffset = MirrorPointLocal(viewCamera.transform.position);
            var virtualCameraLocalDirection = MirrorDirectionLocal(viewCamera.transform.forward);

            var virtualCameraWorldPosition = otherPortal.transform.TransformPoint(virtualCameraLocalOffset);
            var virtualCameraWorldDirection = otherPortal.transform.TransformDirection(virtualCameraLocalDirection);

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

                var portalableObject = otherTransform.GetComponent<PortalableObject>();
                if (portalableObject == null)
                    continue;

                var objectPosition = otherTransform.position;
                var objectDirection = otherTransform.forward;

                var mirroredLocalPosition = MirrorPointLocal(objectPosition);
                var mirroredLocalDirection = MirrorDirectionLocal(objectDirection);

                var teleportedPosition = otherPortal.transform.TransformPoint(mirroredLocalPosition);
                var teleportedDirection = otherPortal.transform.TransformDirection(mirroredLocalDirection);

                if (mirroredLocalPosition.z < m_teleportationThreshold)
                {
                    portalableObject.TeleportSecondaryVisual(teleportedPosition, teleportedDirection);
                }
                else
                {
                    otherTransform.position = teleportedPosition;
                    otherTransform.LookAt(teleportedPosition + teleportedDirection);

                    var teleportedEvent = new ObjectTeleportedEvent(objectPosition, objectDirection, teleportedPosition, teleportedDirection, transform.forward, otherPortal.transform.forward);

                    portalableObject.OnObjectTeleported(teleportedEvent);
                }
            }
        }

        private void OnObjectExitedPortalDetector(Transform transform)
        {
            var portalableObject = transform.GetComponent<PortalableObject>();
            if (portalableObject == null)
                return;

            portalableObject.ResetSecondaryVisual();
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
