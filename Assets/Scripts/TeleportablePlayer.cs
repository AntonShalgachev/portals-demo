using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    public class TeleportablePlayer : MonoBehaviour
    {
        private FreeFlyCamera m_flyController = null;
        private TeleportableObject m_teleportableObject = null;

        private void Awake()
        {
            m_flyController = GetComponent<FreeFlyCamera>();
            m_teleportableObject = GetComponent<TeleportableObject>();
        }

        private void OnEnable()
        {
            m_teleportableObject.onObjectTeleported += OnObjectTeleported;
        }

        private void OnDisable()
        {
            m_teleportableObject.onObjectTeleported -= OnObjectTeleported;
        }

        private void OnObjectTeleported(ObjectTeleportedEvent e)
        {
            m_flyController.Rotate(e.portalRotation);
        }
    }
}
