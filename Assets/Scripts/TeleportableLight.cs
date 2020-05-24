using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    public class TeleportableLight : MonoBehaviour
    {
        private PortalableObject m_portalableObject = null;

        private void Awake()
        {
            m_portalableObject = GetComponent<PortalableObject>();
        }

        private void Start()
        {
            SetupReplicas();
        }

        public void SetupReplicas()
        {

        }
    }
}
