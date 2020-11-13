namespace UnityEngine.Rendering.Universal.Internal
{
    public class DrawGizmosPass : ScriptableRenderPass
    {
        private UnityEngine.Rendering.GizmoSubset m_gizmoSubset;

        ProfilingSampler m_ProfilingSampler;

        public DrawGizmosPass(RenderPassEvent evt, UnityEngine.Rendering.GizmoSubset gizmoSubset)
        {
            renderPassEvent = evt;
            m_gizmoSubset = gizmoSubset;

            m_ProfilingSampler = new ProfilingSampler($"Draw gizmos ({gizmoSubset.ToString()})");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilingSampler.name);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                if (UnityEditor.Handles.ShouldRenderGizmos())
                    context.DrawGizmos(renderingData.cameraData.camera, m_gizmoSubset);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
#endif
        }
    }
}
