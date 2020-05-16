using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    public class PortalsController : MonoBehaviour
    {
        [SerializeField] private Vector2 m_portalSize = Vector2.one;

        private List<Portal> m_portals = new List<Portal>(2);

        public Vector2 portalSize => m_portalSize;

        private Camera m_activeCamera = null;
        public Camera activeCamera
        {
            get
            {
                if (m_activeCamera == null)
                    m_activeCamera = Camera.main;
                return m_activeCamera;
            }
        }

        public Portal GetOtherPortal(Portal portal)
        {
            foreach (var otherPortal in m_portals)
                if (otherPortal != portal)
                    return otherPortal;

            return null;
        }

        public void RegisterPortal(Portal portal)
        {
            m_portals.Add(portal);
        }

        public void DeregisterPortal(Portal portal)
        {
            m_portals.Remove(portal);
        }
    }
}
