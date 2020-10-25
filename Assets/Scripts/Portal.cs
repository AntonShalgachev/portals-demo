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
        [SerializeField] private PortalCamera m_portalCamera = null;
        [SerializeField] private Transform m_clippingPlane = null;

        public PortalsController m_controller => GetComponentInParent<PortalsController>(); // inefficient, but I don't care for now
        public Portal m_otherPortal => m_controller.GetOtherPortal(this);

        private void Awake()
        {
            var viewCamera = m_controller.activeCamera;

            m_portalCamera.transform.ResetLocal();
            m_portalCamera.SyncCameraMatrix(viewCamera);

            m_portalCamera.gameObject.name = $"{gameObject.name}Camera";
        }

        private void OnEnable()
        {
            if (m_controller != null)
                m_controller.RegisterPortal(this);

            m_objectDetector.onTransformExit += OnObjectExitedPortalDetector;
        }

        private void OnDisable()
        {
            if (m_controller != null)
                m_controller.UnregisterPortal(this);

            m_objectDetector.onTransformExit -= OnObjectExitedPortalDetector;
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

            TeleportObjects(otherPortal);
        }

        private void LateUpdate()
        {
            var otherPortal = m_otherPortal;
            if (otherPortal == null)
                return;

            var viewCamera = m_controller.activeCamera;
            m_portalCamera.SyncCameraMatrix(viewCamera);
            UpdateCameraTransform(viewCamera, otherPortal);

            m_portalCamera.SetClippingPlane(otherPortal.m_clippingPlane);
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

        private void TeleportObjects(Portal otherPortal)
        {
            foreach (var otherTransform in m_objectDetector.touchingTransforms)
            {
                if (otherTransform == null)
                    continue;

                var teleportableObject = otherTransform.GetComponent<TeleportableObject>();
                if (teleportableObject == null)
                    continue;

                TeleportObject(otherPortal, teleportableObject);
            }
        }

        public void TeleportObject(Portal otherPortal, TeleportableObject teleportableObject)
        {
            var portalMatrix = FromToPortalMatrix(this, otherPortal);
            var portalRotation = FromToPortalRotation(this, otherPortal);

            var objectPosition = teleportableObject.transform.position;
            var objectRotation = teleportableObject.transform.rotation;
            var teleportedPosition = portalMatrix.MultiplyPoint(objectPosition);
            var teleportedRotation = portalRotation * objectRotation;

            var localObjectPosition = transform.InverseTransformPoint(objectPosition);
            if (localObjectPosition.z > 0.0f)
            {
                teleportableObject.TeleportReplica(this, teleportedPosition, teleportedRotation);
            }
            else
            {
                teleportableObject.transform.SetPositionAndRotation(teleportedPosition, teleportedRotation);

                var teleportedEvent = new ObjectTeleportedEvent(portalRotation);
                teleportableObject.OnObjectTeleported(teleportedEvent);
            }
        }

        private void OnObjectExitedPortalDetector(Transform transform)
        {
            var teleportableObject = transform.GetComponent<TeleportableObject>();
            if (teleportableObject == null)
                return;

            teleportableObject.ResetReplica(this);
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
