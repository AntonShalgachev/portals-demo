using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    public class TeleportableLight : MonoBehaviour
    {
        private TeleportableObject m_teleportableObject = null;

        private void Awake()
        {
            m_teleportableObject = GetComponent<TeleportableObject>();
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
