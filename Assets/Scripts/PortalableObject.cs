using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityPrototype
{
    public class ObjectTeleportedEvent
    {
        public Vector3 originalPosition;
        public Vector3 originalDirection;
        public Vector3 teleportedPosition;
        public Vector3 teleportedDirection;

        public ObjectTeleportedEvent(Vector3 originalPosition, Vector3 originalDirection, Vector3 teleportedPosition, Vector3 teleportedDirection)
        {
            this.originalPosition = originalPosition;
            this.originalDirection = originalDirection;
            this.teleportedPosition = teleportedPosition;
            this.teleportedDirection = teleportedDirection;
        }
    }

    public class PortalableObject : MonoBehaviour
    {
        [SerializeField] private GameObject m_teleportableVisual = null;

        private GameObject m_secondaryVisual = null;

        public event System.Action<ObjectTeleportedEvent> onObjectTeleported;

        private void Awake()
        {
            SpawnSecondaryVisual();
            Debug.Assert(m_secondaryVisual != null);
            ResetSecondaryVisual();
        }

        private void SpawnSecondaryVisual()
        {
            if (m_secondaryVisual != null)
                return;

            if (m_teleportableVisual == gameObject)
            {
                Debug.LogError("Teleportable visual should be a separate object");
                return;
            }

            var targetTransform = m_teleportableVisual.transform;
            m_secondaryVisual = Instantiate(m_teleportableVisual, targetTransform.position, targetTransform.rotation, targetTransform.parent);
        }

        public void TeleportSecondaryVisual(Vector3 position, Vector3 direction)
        {
            m_secondaryVisual.SetActive(true);
            m_secondaryVisual.transform.position = position;
            m_secondaryVisual.transform.LookAt(position + direction);
        }

        public void ResetSecondaryVisual()
        {
            m_secondaryVisual.SetActive(false);
        }

        public void OnObjectTeleported(Vector3 originalPosition, Vector3 originalDirection, Vector3 teleportedPosition, Vector3 teleportedDirection)
        {
            onObjectTeleported?.Invoke(new ObjectTeleportedEvent(originalPosition, originalDirection, teleportedPosition, teleportedDirection));
        }
    }
}
