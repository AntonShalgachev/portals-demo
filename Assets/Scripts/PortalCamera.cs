using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityPrototype
{
    public class PortalCamera : MonoBehaviour
    {
        [SerializeField] private PortalMaterials m_materials = null;

        private Camera m_camera;
        private Transform m_clippingPlane = null;

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
        }

        public void SyncCameraMatrix(Camera viewCamera)
        {
            if (m_camera == null)
                return;

            m_camera.projectionMatrix = viewCamera.projectionMatrix;
        }

        public void SetClippingPlane(Transform clippingPlane)
        {
            m_clippingPlane = clippingPlane;
        }

        private void OnPreRender()
        {
            foreach (var material in m_materials.materials)
            {
                material.SetInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Equal);
                material.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Back);
                material.SetInt("_StencilRef", 1);

                if (m_clippingPlane != null && m_clippingPlane.gameObject.activeInHierarchy)
                {
                    Plane plane = new Plane(m_clippingPlane.up, m_clippingPlane.position);

                    Vector4 planeRepresentation = new Vector4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                    material.SetVector("_Plane", planeRepresentation);
                    material.SetInt("_PlaneClip", 1);
                }
            }
        }

        private void OnPostRender()
        {
            foreach (var material in m_materials.materials)
            {
                material.SetInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Disabled);
                material.SetInt("_CullMode", (int)UnityEngine.Rendering.CullMode.Off);
                material.SetInt("_PlaneClip", 0);
            }
        }
    }
}
