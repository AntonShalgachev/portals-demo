namespace UnityEngine.Rendering.Universal.Internal
{
    public class SetupCameraPropertiesPass : ScriptableRenderPass
    {
        public SetupCameraPropertiesPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            context.SetupCameraProperties(renderingData.cameraData.camera);
        }
    }
}
