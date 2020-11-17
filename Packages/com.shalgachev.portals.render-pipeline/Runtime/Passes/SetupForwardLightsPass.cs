namespace UnityEngine.Rendering.Universal.Internal
{
    class SetupForwardLightsPass : ScriptableRenderPass
    {
        private ForwardLights m_forwardLights = null;

        public SetupForwardLightsPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public void Setup(ForwardLights forwardLights)
        {
            m_forwardLights = forwardLights;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            // Reset per-camera shader keywords. They are enabled depending on which render passes are executed.
            cmd.DisableShaderKeyword(ShaderKeywordStrings.MainLightShadows);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.MainLightShadowCascades);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightsVertex);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightsPixel);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.AdditionalLightShadows);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.SoftShadows);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.MixedLightingSubtractive);
            cmd.DisableShaderKeyword(ShaderKeywordStrings.LinearToSRGBConversion);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);

            m_forwardLights.Setup(context, ref renderingData);
        }
    }
}