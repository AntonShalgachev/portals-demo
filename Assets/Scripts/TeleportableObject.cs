using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityPrototype
{
    public class ObjectTeleportedEvent
    {
        public Quaternion portalRotation; // rotation from one portal to the other

        public ObjectTeleportedEvent(Quaternion portalRotation)
        {
            this.portalRotation = portalRotation;
        }
    }

    public class TeleportableObject : MonoBehaviour
    {
        [SerializeField] private GameObject m_teleportableVisual = null;

        private GameObject m_replicasRoot = null;
        private Dictionary<Portal, GameObject> m_replicas = new Dictionary<Portal, GameObject>();

        public event System.Action<ObjectTeleportedEvent> onObjectTeleported;

        private GameObject SpawnReplica()
        {
            if (m_teleportableVisual == gameObject)
            {
                Debug.LogError("Teleportable visual should be a separate object");
                return null;
            }

            if (m_replicasRoot == null)
            {
                m_replicasRoot = new GameObject("Replicas");
                m_replicasRoot.transform.SetParent(transform, false);
            }

            var targetTransform = m_teleportableVisual.transform;
            var replica = Instantiate(m_teleportableVisual, targetTransform.position, targetTransform.rotation, m_replicasRoot.transform);
            replica.name = $"{m_teleportableVisual} (Replica)";
            replica.layer = LayerMask.NameToLayer("Replica"); // temporary

            return replica;
        }

        private GameObject GetReplica(Portal portal)
        {
            if (m_replicas.TryGetValue(portal, out var replica))
                return replica;

            replica = SpawnReplica();
            Debug.Assert(replica != null);
            m_replicas[portal] = replica;
            ResetReplica(portal);

            return replica;
        }

        public void TeleportReplica(Portal portal, Vector3 position, Quaternion rotation)
        {
            var replica = GetReplica(portal);
            replica.SetActive(true);
            replica.transform.position = position;
            replica.transform.rotation = rotation;
        }

        public void ResetReplica(Portal portal)
        {
            GetReplica(portal).SetActive(false);
        }

        public void OnObjectTeleported(ObjectTeleportedEvent teleportedEvent)
        {
            onObjectTeleported?.Invoke(teleportedEvent);
        }
    }
}
