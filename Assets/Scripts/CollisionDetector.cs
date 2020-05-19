using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityPrototype
{
    public class CollisionDetector : MonoBehaviour
    {
        [SerializeField] private LayerMask m_targetLayerMask = -1;
        [SerializeField] private UnityEvent m_onCollided = null;

        public bool isTouching => m_touchingColliders.Count > 0;

        private HashSet<Collider> m_touchingColliders = new HashSet<Collider>();
        public IEnumerable<Collider> touchingColliders => m_touchingColliders;

        private HashSet<Transform> m_touchingTransforms = new HashSet<Transform>();
        public IEnumerable<Transform> touchingTransforms => m_touchingTransforms;

        private bool TestLayer(int layer)
        {
            return (m_targetLayerMask.value & (1 << layer)) > 0;
        }

        private void OnCollisionEnter(Collision other)
        {
            ProcessCollsionEnter(other.collider);
        }

        private void OnCollisionExit(Collision other)
        {
            ProcessCollisionExit(other.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            ProcessCollsionEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            ProcessCollisionExit(other);
        }

        private void ProcessCollsionEnter(Collider other)
        {
            if (!TestLayer(other.gameObject.layer))
                return;

            m_touchingColliders.Add(other);
            m_touchingTransforms.Add(GetParentObject(other));
            m_onCollided?.Invoke();
        }

        private void ProcessCollisionExit(Collider other)
        {
            if (!TestLayer(other.gameObject.layer))
                return;

            m_touchingTransforms.Remove(GetParentObject(other));
            m_touchingColliders.Remove(other);
        }

        private Transform GetParentObject(Collider collider)
        {
            var rigidbody = collider.attachedRigidbody;
            if (rigidbody != null)
                return rigidbody.transform;

            return collider.transform;
        }
    }
}
