using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

namespace UnityPrototype
{
    public class PortalsController : MonoBehaviour
    {
        [SerializeField] private Vector2 m_portalSize = Vector2.one;
        [SerializeField] private TeleportableObject[] m_lights = new TeleportableObject[] { };

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

            // Shitty condition
            if (m_portals.Count == 2)
                TeleportLightReplicas();
        }

        public void UnregisterPortal(Portal portal)
        {
            m_portals.Remove(portal);
        }

        private void TeleportLightReplicas()
        {
            Debug.Assert(m_portals.Count == 2);

            // TODO don't use arrays for the portals
            TeleportLightReplicas(m_portals[0], m_portals[1]);
            TeleportLightReplicas(m_portals[1], m_portals[0]);
        }

        public void TeleportLightReplicas(Portal from, Portal to)
        {
            // TODO implement

            // foreach (var light in m_lights)
            //     from.TeleportObject(to, light);
        }
    }
}
