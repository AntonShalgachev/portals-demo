namespace UnityEngine.Rendering.CustomRenderPipeline
{
    public static class ShaderPassTag
    {
        public static ShaderTagId forwardLit = new ShaderTagId("UniversalForward");
    }

    public static class ShaderBindings
    {
        const string kPerFrameShaderVariablesTag = "SetPerFrameShaderVariables";
        const string kPerCameraShaderVariablesTag = "SetPerCameraShaderVariables";

        // Time constants
        public static int time = Shader.PropertyToID("_Time");
        public static int sinTime = Shader.PropertyToID("_SinTime");
        public static int cosTime = Shader.PropertyToID("_CosTime");
        public static int deltaTime = Shader.PropertyToID("unity_DeltaTime");

        // Ambient and Fog constants
        public static int ambientSky = Shader.PropertyToID("unity_AmbientSky");
        public static int ambientEquator = Shader.PropertyToID("unity_AmbientEquator");
        public static int ambientGround = Shader.PropertyToID("unity_AmbientGround");
        public static int fogColor = Shader.PropertyToID("unity_FogColor");
        public static int fogParams = Shader.PropertyToID("unity_FogParams");

        public static void SetPerFrameShaderVariables(ScriptableRenderContext context)
        {
#if UNITY_EDITOR
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
#else
            float time = Time.time;
#endif
            float deltaTime = Time.deltaTime;
            float smoothDeltaTime = Time.smoothDeltaTime;

            float timeEights = time / 8f;
            float timeFourth = time / 4f;
            float timeHalf = time / 2f;

            // Time values
            Vector4 timeVector = time * new Vector4(1f / 20f, 1f, 2f, 3f);
            Vector4 sinTimeVector = new Vector4(Mathf.Sin(timeEights), Mathf.Sin(timeFourth), Mathf.Sin(timeHalf), Mathf.Sin(time));
            Vector4 cosTimeVector = new Vector4(Mathf.Cos(timeEights), Mathf.Cos(timeFourth), Mathf.Cos(timeHalf), Mathf.Cos(time));
            Vector4 deltaTimeVector = new Vector4(deltaTime, 1f / deltaTime, smoothDeltaTime, 1f / smoothDeltaTime);

            CommandBuffer cmd = CommandBufferPool.Get(kPerFrameShaderVariablesTag);
            cmd.SetGlobalVector(ShaderBindings.time, timeVector);
            cmd.SetGlobalVector(ShaderBindings.sinTime, sinTimeVector);
            cmd.SetGlobalVector(ShaderBindings.cosTime, cosTimeVector);
            cmd.SetGlobalVector(ShaderBindings.deltaTime, deltaTimeVector);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}