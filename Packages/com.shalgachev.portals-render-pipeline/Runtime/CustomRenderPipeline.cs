﻿using Unity.Collections;

namespace UnityEngine.Rendering.CustomRenderPipeline
{
    public sealed class CustomRenderPipeline : RenderPipeline
    {
        private Lights m_lights = new Lights();

        public CustomRenderPipeline(CustomRenderPipelineAsset asset)
        {

        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            Camera portalCamera = cameras.Length > 1 ? cameras[1] : null;

            ShaderBindings.SetPerFrameShaderVariables(context);
            foreach (Camera camera in cameras)
            {
                if (camera == portalCamera)
                    continue;

#if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif

                DrawCamera(context, camera, portalCamera);
            }
        }

        CullingResults Cull(ScriptableRenderContext context, Camera camera)
        {
            camera.TryGetCullingParameters(out var cullingParameters);
            return context.Cull(ref cullingParameters);
        }

        void DrawCamera(ScriptableRenderContext context, Camera camera, Camera portalCamera)
        {
            if (camera.clearFlags == CameraClearFlags.Color)
            {
                var cmd = CommandBufferPool.Get("Clear target");
                cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                cmd.ClearRenderTarget(true, true, camera.backgroundColor);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            DrawCameraRecursive(context, camera, portalCamera, 0);

            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
                context.DrawSkybox(camera);

            context.Submit();
        }

        void DrawCameraRecursive(ScriptableRenderContext context, Camera camera, Camera portalCamera, int depth)
        {
            BeginCameraRendering(context, camera);

            CullingResults cullingResults = Cull(context, camera);

            InitializeLightData(ref cullingResults, out LightData lightData);
            m_lights.Setup(context, ref lightData);

            bool enableDynamicBatching = false;
            bool enableInstancing = false;
            PerObjectData perObjectData = PerObjectData.LightData | PerObjectData.LightIndices;

            FilteringSettings opaqueFilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            FilteringSettings transparentFilteringSettings = new FilteringSettings(RenderQueueRange.transparent);

            SortingSettings opaqueSortingSettings = new SortingSettings(camera);
            opaqueSortingSettings.criteria = SortingCriteria.CommonOpaque;

            SortingSettings transparentSortingSettings = new SortingSettings(camera);
            transparentSortingSettings.criteria = SortingCriteria.CommonTransparent;

            // ShaderTagId must match the "LightMode" tag inside the shader pass.
            // If not "LightMode" tag is found the object won't render.
            DrawingSettings opaqueDrawingSettings = new DrawingSettings(ShaderPassTag.forwardLit, opaqueSortingSettings);
            opaqueDrawingSettings.enableDynamicBatching = enableDynamicBatching;
            opaqueDrawingSettings.enableInstancing = enableInstancing;
            opaqueDrawingSettings.perObjectData = perObjectData;

            DrawingSettings transparentDrawingSettings = new DrawingSettings(ShaderPassTag.forwardLit, transparentSortingSettings);
            transparentDrawingSettings.enableDynamicBatching = enableDynamicBatching;
            transparentDrawingSettings.enableInstancing = enableInstancing;
            transparentDrawingSettings.perObjectData = perObjectData;

            ProfilingSampler cameraSampler = new ProfilingSampler(camera.name);
            CommandBuffer cameraCmd = CommandBufferPool.Get(cameraSampler.name);
            using (new ProfilingScope(cameraCmd, cameraSampler))
            {
                context.ExecuteCommandBuffer(cameraCmd);
                cameraCmd.Clear();

                context.SetupCameraProperties(camera);
                DrawRenderersProfiled(context, cullingResults, "Opaque", ref opaqueDrawingSettings, ref opaqueFilteringSettings);
                if (portalCamera != null)
                {
                    // TODO set portal camera position
                    DrawCameraRecursive(context, portalCamera, null, depth + 1);
                    // TODO reset portal camera position (optional)
                }
                context.SetupCameraProperties(camera);
                DrawRenderersProfiled(context, cullingResults, "Transparent", ref transparentDrawingSettings, ref transparentFilteringSettings);
            }
            context.ExecuteCommandBuffer(cameraCmd);
            CommandBufferPool.Release(cameraCmd);

            EndCameraRendering(context, camera);
        }

        static void DrawRenderersProfiled(ScriptableRenderContext context, CullingResults cullingResults, string name, ref DrawingSettings drawingSettings, ref FilteringSettings filteringSettings)
        {
            ProfilingSampler sampler = new ProfilingSampler(name);
            CommandBuffer cmd = CommandBufferPool.Get(sampler.name);

            using (new ProfilingScope(cmd, sampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Main Light is always a directional light
        static int GetMainLightIndex(NativeArray<VisibleLight> visibleLights)
        {
            int totalVisibleLights = visibleLights.Length;

            if (totalVisibleLights == 0)
                return -1;

            Light sunLight = RenderSettings.sun;
            int brightestDirectionalLightIndex = -1;
            float brightestLightIntensity = 0.0f;
            for (int i = 0; i < totalVisibleLights; ++i)
            {
                VisibleLight currVisibleLight = visibleLights[i];
                Light currLight = currVisibleLight.light;

                // Particle system lights have the light property as null. We sort lights so all particles lights
                // come last. Therefore, if first light is particle light then all lights are particle lights.
                // In this case we either have no main light or already found it.
                if (currLight == null)
                    break;

                if (currLight == sunLight)
                    return i;

                // In case no shadow light is present we will return the brightest directional light
                if (currVisibleLight.lightType == LightType.Directional && currLight.intensity > brightestLightIntensity)
                {
                    brightestLightIntensity = currLight.intensity;
                    brightestDirectionalLightIndex = i;
                }
            }

            return brightestDirectionalLightIndex;
        }

        public struct LightData
        {
            public int mainLightIndex;
            public NativeArray<VisibleLight> visibleLights;
        }

        static void InitializeLightData(ref CullingResults cullResults, out LightData lightData)
        {
            var visibleLights = cullResults.visibleLights;

            lightData.mainLightIndex = GetMainLightIndex(visibleLights);
            lightData.visibleLights = visibleLights;
        }
    }

}
