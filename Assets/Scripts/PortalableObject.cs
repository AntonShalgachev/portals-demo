using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    public class PortalableObject : MonoBehaviour
    {
        [SerializeField] private GameObject m_teleportableVisual = null;

        private GameObject m_secondaryVisual = null;

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
    }
}
