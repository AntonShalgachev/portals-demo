using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gamelogic.Extensions;

namespace UnityPrototype
{
    public class Portal : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Renderer[] m_viewports = new Renderer[] { };
        [SerializeField] private Transform m_visual = null;

        [Header("Other")]
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
            foreach (var viewport in m_viewports)
                viewport.material.mainTexture = m_texture;

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

            if (m_visual != null)
                m_visual.transform.SetScaleXY(m_controller.portalSize.x, m_controller.portalSize.y);
            if (m_objectDetector != null)
                m_objectDetector.transform.SetScaleXY(m_controller.portalSize.x, m_controller.portalSize.y);

            var viewCamera = m_controller.activeCamera;
            SyncCameraMatrix(viewCamera);
            UpdateCameraTransform(viewCamera, otherPortal);

            m_portalCamera.cullingMask = m_cullingMask & ~(1 << m_controller.GetWallLayer(otherPortal));

            TeleportObjects(otherPortal);
        }

        private static Quaternion MirrorRotation()
        {
            return Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }

        private static Quaternion FromToPortalRotation(Portal from, Portal to)
        {
            return to.transform.rotation * Quaternion.Inverse(from.transform.rotation) * MirrorRotation();
        }

        private static Matrix4x4 FromToPortalMatrix(Portal from, Portal to)
        {
            return to.transform.localToWorldMatrix * Matrix4x4.Rotate(MirrorRotation()) * from.transform.worldToLocalMatrix;
        }

        private void UpdateCameraTransform(Camera viewCamera, Portal otherPortal)
        {
            m_portalCamera.transform.position = FromToPortalMatrix(this, otherPortal).MultiplyPoint(viewCamera.transform.position);
            m_portalCamera.transform.rotation = FromToPortalRotation(this, otherPortal) * viewCamera.transform.rotation;
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

                var portalMatrix = FromToPortalMatrix(this, otherPortal);
                var portalRotation = FromToPortalRotation(this, otherPortal);

                var objectPosition = otherTransform.position;
                var objectRotation = otherTransform.rotation;
                var teleportedPosition = portalMatrix.MultiplyPoint(objectPosition);
                var teleportedRotation = portalRotation * objectRotation;

                var localObjectPosition = transform.InverseTransformPoint(objectPosition);
                if (localObjectPosition.z > 0.0f)
                {
                    portalableObject.TeleportSecondaryVisual(teleportedPosition, teleportedRotation);
                }
                else
                {
                    otherTransform.SetPositionAndRotation(teleportedPosition, teleportedRotation);

                    var teleportedEvent = new ObjectTeleportedEvent(portalRotation);
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
