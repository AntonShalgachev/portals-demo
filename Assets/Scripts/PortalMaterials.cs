using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    [CreateAssetMenu(fileName = "PortalMaterials", menuName = "Game/PortalMaterials")]
    public class PortalMaterials : ScriptableObject
    {
        [SerializeField] private Material[] m_materials = new Material[] { };

        public IEnumerable<Material> materials => m_materials;
    }
}
