using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    public class TeleportablePlayer : MonoBehaviour
    {
        private FreeFlyCamera m_flyController = null;
        private PortalableObject m_portalableObject = null;

        private void Awake()
        {
            m_flyController = GetComponent<FreeFlyCamera>();
            m_portalableObject = GetComponent<PortalableObject>();
        }

        private void OnEnable()
        {
            m_portalableObject.onObjectTeleported += OnObjectTeleported;
        }

        private void OnDisable()
        {
            m_portalableObject.onObjectTeleported -= OnObjectTeleported;
        }

        private void OnObjectTeleported(ObjectTeleportedEvent e)
        {
            m_flyController.Rotate(e.portalRotation);
        }
    }
}
