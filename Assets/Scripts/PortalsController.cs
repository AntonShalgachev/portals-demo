using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

namespace UnityPrototype
{
    public class PortalsController : MonoBehaviour
    {
        [SerializeField] private Vector2 m_portalSize = Vector2.one;
        [SerializeField, Layer] private int[] m_portalWallLayers = new int[] { };

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

        public int GetWallLayer(Portal portal)
        {
            var index = m_portals.IndexOf(portal);
            Debug.Assert(index >= 0);
            return m_portalWallLayers[index];
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
