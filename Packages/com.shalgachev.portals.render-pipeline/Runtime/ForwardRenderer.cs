using System.Collections.Generic;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Default renderer for Universal RP.
    /// This renderer is supported on all Universal RP supported platforms.
    /// It uses a classic forward rendering strategy with per-object light culling.
    /// </summary>
    public sealed class ForwardRenderer : ScriptableRenderer
    {
        const int k_DepthStencilBufferBits = 32;
        const string k_CreateCameraTextures = "Create Camera Texture";

        private class PassContainer
        {
            public SetupForwardLightsPass m_SetupForwardLightsPass;
            public ColorGradingLutPass m_ColorGradingLutPass;
            public DepthOnlyPass m_DepthPrepass;
            public MainLightShadowCasterPass m_MainLightShadowCasterPass;
            public AdditionalLightsShadowCasterPass m_AdditionalLightsShadowCasterPass;
            public ScreenSpaceShadowResolvePass m_ScreenSpaceShadowResolvePass;
            public SetupCameraPropertiesPass m_SetupCameraPropertiesPass;
            public DrawObjectsPass m_RenderOpaqueForwardPass;
            public DrawSkyboxPass m_DrawSkyboxPass;
            public CopyDepthPass m_CopyDepthPass;
            public CopyColorPass m_CopyColorPass;
            public TransparentSettingsPass m_TransparentSettingsPass;
            public DrawObjectsPass m_RenderTransparentForwardPass;
            public InvokeOnRenderObjectCallbackPass m_OnRenderObjectCallbackPass;
            public PostProcessPass m_PostProcessPass;
            public PostProcessPass m_FinalPostProcessPass;
            public FinalBlitPass m_FinalBlitPass;
            public CapturePass m_CapturePass;
            public DrawGizmosPass m_DrawGizmosPreImageEffects;
            public DrawGizmosPass m_DrawGizmosPostImageEffects;
        }

        // PassContainer m_passes = new PassContainer();
        List<List<PassContainer>> m_passContainers = new List<List<PassContainer>>();

#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
        PostProcessPassCompat m_OpaquePostProcessPassCompat;
        PostProcessPassCompat m_PostProcessPassCompat;
#endif

#if UNITY_EDITOR
        SceneViewDepthCopyPass m_SceneViewDepthCopyPass;
#endif

        RenderTargetHandle m_ActiveCameraColorAttachment;
        RenderTargetHandle m_ActiveCameraDepthAttachment;
        RenderTargetHandle m_CameraColorAttachment;
        RenderTargetHandle m_CameraDepthAttachment;
        RenderTargetHandle m_DepthTexture;
        RenderTargetHandle m_OpaqueColor;
        RenderTargetHandle m_AfterPostProcessColor;
        RenderTargetHandle m_ColorGradingLut;

        ForwardLights m_ForwardLights;
        StencilState m_DefaultStencilState;

        Material m_BlitMaterial;
        Material m_CopyDepthMaterial;
        Material m_SamplingMaterial;
        Material m_ScreenspaceShadowsMaterial;

        int m_maxPortalDepth = 0;

        public ForwardRenderer(ForwardRendererData data) : base(data)
        {
            m_maxPortalDepth = data.maxPortalDepth;

            m_BlitMaterial = CoreUtils.CreateEngineMaterial(data.shaders.blitPS);
            m_CopyDepthMaterial = CoreUtils.CreateEngineMaterial(data.shaders.copyDepthPS);
            m_SamplingMaterial = CoreUtils.CreateEngineMaterial(data.shaders.samplingPS);
            m_ScreenspaceShadowsMaterial = CoreUtils.CreateEngineMaterial(data.shaders.screenSpaceShadowPS);

            StencilStateData stencilData = data.defaultStencilState;
            m_DefaultStencilState = StencilState.defaultValue;
            m_DefaultStencilState.enabled = stencilData.overrideStencilState;
            m_DefaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            m_DefaultStencilState.SetPassOperation(stencilData.passOperation);
            m_DefaultStencilState.SetFailOperation(stencilData.failOperation);
            m_DefaultStencilState.SetZFailOperation(stencilData.zFailOperation);

            var maxPortalCameras = 2;

            for (var depth = 0; depth < m_maxPortalDepth; depth++)
            {
                var maxCameras = depth > 0 ? maxPortalCameras : 1;

                var passes = new List<PassContainer>();
                for (var cameraId = 0; cameraId < maxCameras; cameraId++)
                {
                    passes.Add(CreatePassContainer(data));
                }

                m_passContainers.Add(passes);
            }

#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
            m_OpaquePostProcessPassCompat = new PostProcessPassCompat(RenderPassEvent.BeforeRenderingOpaques, true);
            m_PostProcessPassCompat = new PostProcessPassCompat(RenderPassEvent.BeforeRenderingPostProcessing);
#endif

#if UNITY_EDITOR
            m_SceneViewDepthCopyPass = new SceneViewDepthCopyPass(RenderPassEvent.AfterBlitting, m_CopyDepthMaterial);
#endif

            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            m_CameraColorAttachment.Init("_CameraColorTexture");
            m_CameraDepthAttachment.Init("_CameraDepthAttachment");
            m_DepthTexture.Init("_CameraDepthTexture");
            m_OpaqueColor.Init("_CameraOpaqueTexture");
            m_AfterPostProcessColor.Init("_AfterPostProcessTexture");
            m_ColorGradingLut.Init("_InternalGradingLut");
            m_ForwardLights = new ForwardLights();

            supportedRenderingFeatures = new RenderingFeatures()
            {
                cameraStacking = true,
            };
        }

        private PassContainer CreatePassContainer(ForwardRendererData data)
        {
            StencilStateData stencilData = data.defaultStencilState;

            var passContainer = new PassContainer();

            // Note: Since all custom render passes inject first and we have stable sort,
            // we inject the builtin passes in the before events.
            passContainer.m_SetupForwardLightsPass = new SetupForwardLightsPass(RenderPassEvent.BeforeRendering);
            passContainer.m_MainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            passContainer.m_AdditionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            passContainer.m_DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrepasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            passContainer.m_ScreenSpaceShadowResolvePass = new ScreenSpaceShadowResolvePass(RenderPassEvent.BeforeRenderingPrepasses, m_ScreenspaceShadowsMaterial);
            passContainer.m_SetupCameraPropertiesPass = new SetupCameraPropertiesPass(RenderPassEvent.AfterRenderingShadows);
            passContainer.m_ColorGradingLutPass = new ColorGradingLutPass(RenderPassEvent.BeforeRenderingPrepasses, data.postProcessData);
            passContainer.m_RenderOpaqueForwardPass = new DrawObjectsPass("Render Opaques", true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            passContainer.m_CopyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRenderingSkybox, m_CopyDepthMaterial);
            passContainer.m_DrawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
            passContainer.m_CopyColorPass = new CopyColorPass(RenderPassEvent.BeforeRenderingTransparents, m_SamplingMaterial);
            passContainer.m_TransparentSettingsPass = new TransparentSettingsPass(RenderPassEvent.BeforeRenderingTransparents, data.shadowTransparentReceive);
            passContainer.m_RenderTransparentForwardPass = new DrawObjectsPass("Render Transparents", false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            passContainer.m_OnRenderObjectCallbackPass = new InvokeOnRenderObjectCallbackPass(RenderPassEvent.BeforeRenderingPostProcessing);
            passContainer.m_PostProcessPass = new PostProcessPass(RenderPassEvent.BeforeRenderingPostProcessing, data.postProcessData, m_BlitMaterial);
            passContainer.m_FinalPostProcessPass = new PostProcessPass(RenderPassEvent.AfterRendering + 1, data.postProcessData, m_BlitMaterial);
            passContainer.m_CapturePass = new CapturePass(RenderPassEvent.AfterRendering);
            passContainer.m_DrawGizmosPreImageEffects = new DrawGizmosPass(RenderPassEvent.BeforeRenderingPostProcessing, GizmoSubset.PreImageEffects);
            passContainer.m_DrawGizmosPostImageEffects = new DrawGizmosPass(RenderPassEvent.AfterBlitting, GizmoSubset.PostImageEffects);
            passContainer.m_FinalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering + 1, m_BlitMaterial);

            return passContainer;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            // always dispose unmanaged resources

            foreach (var passesList in m_passContainers)
                foreach (var passes in passesList)
                    passes.m_PostProcessPass.Cleanup();

            CoreUtils.Destroy(m_BlitMaterial);
            CoreUtils.Destroy(m_CopyDepthMaterial);
            CoreUtils.Destroy(m_SamplingMaterial);
            CoreUtils.Destroy(m_ScreenspaceShadowsMaterial);
        }

        /// <inheritdoc />
        public override void Setup(ScriptableRenderContext context, ref ExtendedRenderingData extendedRenderingData)
        {
            PassContainer passes = m_passContainers[0][0];

            ref var mainRenderingData = ref extendedRenderingData.mainRenderingData;
            Camera mainCamera = mainRenderingData.cameraData.camera;
            ref CameraData mainCameraData = ref mainRenderingData.cameraData;
            RenderTextureDescriptor cameraTargetDescriptor = mainRenderingData.cameraData.cameraTargetDescriptor;

            // TODO remove
            // ref var camera = ref mainCamera;
            // ref var cameraData = ref mainCameraData;
            // ref var renderingData = ref mainRenderingData;

            // Special path for depth only offscreen cameras. Only write opaques + transparents.
            bool isOffscreenDepthTexture = mainCameraData.targetTexture != null && mainCameraData.targetTexture.format == RenderTextureFormat.Depth;
            if (isOffscreenDepthTexture)
            {
                Debug.LogWarning("Offscreen depth texture isn't supported");

                // ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);

                // for (int i = 0; i < rendererFeatures.Count; ++i)
                // {
                //     if (rendererFeatures[i].isActive)
                //         rendererFeatures[i].AddRenderPasses(this, ref renderingData);
                // }

                // EnqueuePass(passes.m_RenderOpaqueForwardPass);
                // EnqueuePass(passes.m_DrawSkyboxPass);
                // EnqueuePass(passes.m_RenderTransparentForwardPass);
                return;
            }

            // Should apply post-processing after rendering this camera?
            bool applyPostProcessing = mainCameraData.postProcessEnabled;
            // There's at least a camera in the camera stack that applies post-processing
            bool anyPostProcessing = mainRenderingData.postProcessingEnabled;

            var postProcessFeatureSet = UniversalRenderPipeline.asset.postProcessingFeatureSet;

            bool isSceneViewCamera = mainCameraData.isSceneViewCamera;
            bool requiresDepthTexture = mainCameraData.requiresDepthTexture;
            bool isStereoEnabled = mainCameraData.isStereoEnabled;

            // Depth prepass is generated in the following cases:
            // - Scene view camera always requires a depth texture. We do a depth pre-pass to simplify it and it shouldn't matter much for editor.
            // - If game or offscreen camera requires it we check if we can copy the depth from the rendering opaques pass and use that instead.
            bool requiresDepthPrepass = isSceneViewCamera;
            requiresDepthPrepass |= (requiresDepthTexture && !CanCopyDepth(ref mainRenderingData.cameraData));

            // The copying of depth should normally happen after rendering opaques.
            // But if we only require it for post processing or the scene camera then we do it after rendering transparent objects
            passes.m_CopyDepthPass.renderPassEvent = (!requiresDepthTexture && (applyPostProcessing || isSceneViewCamera)) ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.AfterRenderingOpaques;

            // TODO: There's an issue in multiview and depth copy pass. Atm forcing a depth prepass on XR until we have a proper fix.
            if (isStereoEnabled && requiresDepthTexture)
                requiresDepthPrepass = true;

            bool isRunningHololens = false;
#if ENABLE_VR && ENABLE_VR_MODULE
            isRunningHololens = UniversalRenderPipeline.IsRunningHololens(mainCamera);
#endif
            bool createColorTexture = RequiresIntermediateColorTexture(ref mainRenderingData, cameraTargetDescriptor) ||
                (rendererFeatures.Count != 0 && !isRunningHololens);

            // If camera requires depth and there's no depth pre-pass we create a depth texture that can be read later by effect requiring it.
            bool createDepthTexture = mainCameraData.requiresDepthTexture && !requiresDepthPrepass;
            createDepthTexture |= (mainCameraData.renderType == CameraRenderType.Base && !mainRenderingData.resolveFinalTarget);

            // Configure all settings require to start a new camera stack (base camera only)
            if (mainCameraData.renderType == CameraRenderType.Base)
            {
                m_ActiveCameraColorAttachment = (createColorTexture) ? m_CameraColorAttachment : RenderTargetHandle.CameraTarget;
                m_ActiveCameraDepthAttachment = (createDepthTexture) ? m_CameraDepthAttachment : RenderTargetHandle.CameraTarget;

                bool intermediateRenderTexture = createColorTexture || createDepthTexture;

                // Doesn't create texture for Overlay cameras as they are already overlaying on top of created textures.
                bool createTextures = intermediateRenderTexture;
                if (createTextures)
                    CreateCameraRenderTarget(context, ref mainCameraData);

                // if rendering to intermediate render texture we don't have to create msaa backbuffer
                int backbufferMsaaSamples = (intermediateRenderTexture) ? 1 : cameraTargetDescriptor.msaaSamples;

                if (Camera.main == mainCamera && mainCamera.cameraType == CameraType.Game && mainCameraData.targetTexture == null)
                    SetupBackbufferFormat(backbufferMsaaSamples, isStereoEnabled);
            }
            else
            {
                m_ActiveCameraColorAttachment = m_CameraColorAttachment;
                m_ActiveCameraDepthAttachment = m_CameraDepthAttachment;
            }

            ConfigureCameraTarget(m_ActiveCameraColorAttachment.Identifier(), m_ActiveCameraDepthAttachment.Identifier());

            if (rendererFeatures.Count > 0)
                Debug.LogWarning("Renderer Features aren't supported");
            // for (int i = 0; i < rendererFeatures.Count; ++i)
            // {
            //     if (rendererFeatures[i].isActive)
            //         rendererFeatures[i].AddRenderPasses(this, ref renderingData);
            // }

            int count = activeRenderPassQueue.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                if (activeRenderPassQueue[i] == null)
                    activeRenderPassQueue.RemoveAt(i);
            }
            bool hasPassesAfterPostProcessing = activeRenderPassQueue.Find(x => x.renderPassEvent == RenderPassEvent.AfterRendering) != null;

            EnqueueGeometryPasses(ref extendedRenderingData, requiresDepthPrepass, createDepthTexture);

            bool lastCameraInTheStack = mainRenderingData.resolveFinalTarget;
            bool hasCaptureActions = mainRenderingData.cameraData.captureActions != null && lastCameraInTheStack;
            bool applyFinalPostProcessing = anyPostProcessing && lastCameraInTheStack &&
                                     mainRenderingData.cameraData.antialiasing == AntialiasingMode.FastApproximateAntialiasing;

            // When post-processing is enabled we can use the stack to resolve rendering to camera target (screen or RT).
            // However when there are render passes executing after post we avoid resolving to screen so rendering continues (before sRGBConvertion etc)
            bool dontResolvePostProcessingToCameraTarget = hasCaptureActions || hasPassesAfterPostProcessing || applyFinalPostProcessing;

            #region Post-processing v2 support
#if POST_PROCESSING_STACK_2_0_0_OR_NEWER
                        // To keep things clean we'll separate the logic from builtin PP and PPv2 - expect some copy/pasting
                        if (postProcessFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
                        {
                            // if we have additional filters
                            // we need to stay in a RT
                            if (hasPassesAfterPostProcessing)
                            {
                                // perform post with src / dest the same
                                if (applyPostProcessing)
                                {
                                    m_PostProcessPassCompat.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);
                                    EnqueuePass(m_PostProcessPassCompat);
                                }

                                //now blit into the final target
                                if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                                {
                                    if (renderingData.cameraData.captureActions != null)
                                    {
                                        m_CapturePass.Setup(m_ActiveCameraColorAttachment);
                                        EnqueuePass(m_CapturePass);
                                    }

                                    m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
                                    EnqueuePass(m_FinalBlitPass);
                                }
                            }
                            else
                            {
                                if (applyPostProcessing)
                                {
                                    m_PostProcessPassCompat.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, RenderTargetHandle.CameraTarget);
                                    EnqueuePass(m_PostProcessPassCompat);
                                }
                                else if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
                                {
                                    m_FinalBlitPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment);
                                    EnqueuePass(m_FinalBlitPass);
                                }
                            }
                        }
                        else
#endif
            #endregion
            {

                if (lastCameraInTheStack)
                {
                    // Post-processing will resolve to final target. No need for final blit pass.
                    if (applyPostProcessing)
                    {
                        var destination = dontResolvePostProcessingToCameraTarget ? m_AfterPostProcessColor : RenderTargetHandle.CameraTarget;

                        // if resolving to screen we need to be able to perform sRGBConvertion in post-processing if necessary
                        bool doSRGBConvertion = !(dontResolvePostProcessingToCameraTarget || (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget));
                        passes.m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, destination, m_ActiveCameraDepthAttachment, m_ColorGradingLut, applyFinalPostProcessing, doSRGBConvertion);
                        Debug.Assert(applyPostProcessing || doSRGBConvertion, "This will do unnecessary blit!");
                        EnqueuePass(passes.m_PostProcessPass);
                    }

                    if (mainCameraData.captureActions != null)
                    {
                        passes.m_CapturePass.Setup(m_ActiveCameraColorAttachment);
                        EnqueuePass(passes.m_CapturePass);
                    }

                    // if we applied post-processing for this camera it means current active texture is m_AfterPostProcessColor
                    var sourceForFinalPass = (applyPostProcessing) ? m_AfterPostProcessColor : m_ActiveCameraColorAttachment;

                    // Do FXAA or any other final post-processing effect that might need to run after AA.
                    if (applyFinalPostProcessing)
                    {
                        passes.m_FinalPostProcessPass.SetupFinalPass(sourceForFinalPass);
                        EnqueuePass(passes.m_FinalPostProcessPass);
                    }

                    // if post-processing then we already resolved to camera target while doing post.
                    // Also only do final blit if camera is not rendering to RT.
                    bool cameraTargetResolved =
                        // final PP always blit to camera target
                        applyFinalPostProcessing ||
                        // no final PP but we have PP stack. In that case it blit unless there are render pass after PP
                        (applyPostProcessing && !hasPassesAfterPostProcessing) ||
                        // offscreen camera rendering to a texture, we don't need a blit pass to resolve to screen
                        m_ActiveCameraColorAttachment == RenderTargetHandle.CameraTarget;

                    // We need final blit to resolve to screen
                    if (!cameraTargetResolved)
                    {
                        passes.m_FinalBlitPass.Setup(cameraTargetDescriptor, sourceForFinalPass);
                        EnqueuePass(passes.m_FinalBlitPass);
                    }
                }

                // stay in RT so we resume rendering on stack after post-processing
                else if (applyPostProcessing)
                {
                    passes.m_PostProcessPass.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_AfterPostProcessColor, m_ActiveCameraDepthAttachment, m_ColorGradingLut, false, false);
                    EnqueuePass(passes.m_PostProcessPass);
                }

            }

#if UNITY_EDITOR
            if (mainCameraData.isSceneViewCamera)
            {
                // Scene view camera should always resolve target (not stacked)
                Assertions.Assert.IsTrue(lastCameraInTheStack, "Editor camera must resolve target upon finish rendering.");
                m_SceneViewDepthCopyPass.Setup(m_DepthTexture);
                EnqueuePass(m_SceneViewDepthCopyPass);
            }
#endif
        }

        private void EnqueueGeometryPasses(ref ExtendedRenderingData extendedRenderingData, bool requiresDepthPrepass, bool createDepthTexture)
        {
            EnqueueGeometryPassesRecursive(ref extendedRenderingData, requiresDepthPrepass, createDepthTexture, 0);
        }

        private void EnqueueGeometryPassesRecursive(ref ExtendedRenderingData extendedRenderingData, bool requiresDepthPrepass, bool createDepthTexture, int depth)
        {
            var maxDepth = extendedRenderingData.additionalRenderingData.GetLength(0);
            var portalCamerasCount = extendedRenderingData.additionalRenderingData.GetLength(1);

            if (depth > 0 && portalCamerasCount <= 0)
                return;

            if (depth > maxDepth)
                return;

            var previousDepth = m_currentDepth;
            m_currentDepth = depth;

            if (depth == 0)
            {
                EnqueueGeometryPassesRecursive(ref extendedRenderingData, requiresDepthPrepass, createDepthTexture, depth, 0);
            }
            else
            {
                for (var cameraId = 0; cameraId < portalCamerasCount; cameraId++)
                {
                    var previousCameraId = m_currentCameraId;
                    m_currentCameraId = cameraId;

                    EnqueueGeometryPassesRecursive(ref extendedRenderingData, requiresDepthPrepass, createDepthTexture, depth, cameraId);

                    m_currentCameraId = previousCameraId;
                }

            }

            m_currentDepth = previousDepth;
        }

        public void EnqueueGeometryPassesRecursive(ref ExtendedRenderingData extendedRenderingData, bool requiresDepthPrepass, bool createDepthTexture, int depth, int cameraId)
        {
            ref var renderingData = ref (depth <= 0 ? ref extendedRenderingData.mainRenderingData : ref extendedRenderingData.additionalRenderingData[depth - 1, cameraId]);

            ref var cameraData = ref renderingData.cameraData;

            PassContainer passes = m_passContainers[depth][cameraId];
            Camera camera = cameraData.camera;

            passes.m_SetupForwardLightsPass.Setup(m_ForwardLights);
            EnqueuePass(passes.m_SetupForwardLightsPass);

            bool mainLightShadows = passes.m_MainLightShadowCasterPass.Setup(ref renderingData);
            if (mainLightShadows)
                EnqueuePass(passes.m_MainLightShadowCasterPass);

            bool additionalLightShadows = passes.m_AdditionalLightsShadowCasterPass.Setup(ref renderingData);
            if (additionalLightShadows)
                EnqueuePass(passes.m_AdditionalLightsShadowCasterPass);

            EnqueuePass(passes.m_SetupCameraPropertiesPass);

            if (requiresDepthPrepass)
            {
                passes.m_DepthPrepass.Setup(cameraData.cameraTargetDescriptor, m_DepthTexture);
                EnqueuePass(passes.m_DepthPrepass);
            }

            //             // We generate color LUT in the base camera only. This allows us to not break render pass execution for overlay cameras.
            //             bool generateColorGradingLUT = anyPostProcessing && mainCameraData.renderType == CameraRenderType.Base;
            // #if POST_PROCESSING_STACK_2_0_0_OR_NEWER
            //             // PPv2 doesn't need to generate color grading LUT.
            //             if (postProcessFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
            //                 generateColorGradingLUT = false;
            // #endif

            //             if (generateColorGradingLUT)
            //             {
            //                 passes.m_ColorGradingLutPass.Setup(m_ColorGradingLut);
            //                 EnqueuePass(passes.m_ColorGradingLutPass);
            //             }

            EnqueuePass(passes.m_RenderOpaqueForwardPass);

            EnqueueGeometryPassesRecursive(ref extendedRenderingData, requiresDepthPrepass, createDepthTexture, depth + 1);

            EnqueuePass(passes.m_SetupCameraPropertiesPass);

            // #if POST_PROCESSING_STACK_2_0_0_OR_NEWER
            // #pragma warning disable 0618 // Obsolete
            //             bool hasOpaquePostProcessCompat = applyPostProcessing &&
            //                 postProcessFeatureSet == PostProcessingFeatureSet.PostProcessingV2 &&
            //                 renderingData.cameraData.postProcessLayer.HasOpaqueOnlyEffects(RenderingUtils.postProcessRenderContext);

            //             if (hasOpaquePostProcessCompat)
            //             {
            //                 m_OpaquePostProcessPassCompat.Setup(cameraTargetDescriptor, m_ActiveCameraColorAttachment, m_ActiveCameraColorAttachment);
            //                 EnqueuePass(m_OpaquePostProcessPassCompat);
            //             }
            // #pragma warning restore 0618
            // #endif

            bool isBaseCamera = cameraData.renderType == CameraRenderType.Base;
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null && isBaseCamera)
                EnqueuePass(passes.m_DrawSkyboxPass);

            // If a depth texture was created we necessarily need to copy it, otherwise we could have render it to a renderbuffer
            if (!requiresDepthPrepass && cameraData.requiresDepthTexture && createDepthTexture)
            {
                // TODO move pass renderPassEvent initialization here
                passes.m_CopyDepthPass.Setup(m_ActiveCameraDepthAttachment, m_DepthTexture);
                EnqueuePass(passes.m_CopyDepthPass);
            }

            if (cameraData.requiresOpaqueTexture)
            {
                // TODO: Downsampling method should be store in the renderer instead of in the asset.
                // We need to migrate this data to renderer. For now, we query the method in the active asset.
                Downsampling downsamplingMethod = UniversalRenderPipeline.asset.opaqueDownsampling;
                passes.m_CopyColorPass.Setup(m_ActiveCameraColorAttachment.Identifier(), m_OpaqueColor, downsamplingMethod);
                EnqueuePass(passes.m_CopyColorPass);
            }

            bool transparentsNeedSettingsPass = passes.m_TransparentSettingsPass.Setup(ref renderingData);
            if (transparentsNeedSettingsPass)
            {
                EnqueuePass(passes.m_TransparentSettingsPass);
            }

            EnqueuePass(passes.m_RenderTransparentForwardPass);

            EnqueuePass(passes.m_OnRenderObjectCallbackPass);

            EnqueuePass(passes.m_DrawGizmosPreImageEffects);
            EnqueuePass(passes.m_DrawGizmosPostImageEffects);
        }

        /// <inheritdoc />
        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters,
            ref CameraData cameraData)
        {
            // TODO: PerObjectCulling also affect reflection probes. Enabling it for now.
            // if (asset.additionalLightsRenderingMode == LightRenderingMode.Disabled ||
            //     asset.maxAdditionalLightsCount == 0)
            // {
            //     cullingParameters.cullingOptions |= CullingOptions.DisablePerObjectCulling;
            // }

            // We disable shadow casters if both shadow casting modes are turned off
            // or the shadow distance has been turned down to zero
            bool isShadowCastingDisabled = !UniversalRenderPipeline.asset.supportsMainLightShadows && !UniversalRenderPipeline.asset.supportsAdditionalLightShadows;
            bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
            if (isShadowCastingDisabled || isShadowDistanceZero)
            {
                cullingParameters.cullingOptions &= ~CullingOptions.ShadowCasters;
            }

            cullingParameters.shadowDistance = cameraData.maxShadowDistance;
        }

        /// <inheritdoc />
        public override void FinishRendering(CommandBuffer cmd)
        {
            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_ActiveCameraColorAttachment.id);
                m_ActiveCameraColorAttachment = RenderTargetHandle.CameraTarget;
            }

            if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(m_ActiveCameraDepthAttachment.id);
                m_ActiveCameraDepthAttachment = RenderTargetHandle.CameraTarget;
            }
        }

        void CreateCameraRenderTarget(ScriptableRenderContext context, ref CameraData cameraData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_CreateCameraTextures);
            var descriptor = cameraData.cameraTargetDescriptor;
            int msaaSamples = descriptor.msaaSamples;
            if (m_ActiveCameraColorAttachment != RenderTargetHandle.CameraTarget)
            {
                bool useDepthRenderBuffer = m_ActiveCameraDepthAttachment == RenderTargetHandle.CameraTarget;
                var colorDescriptor = descriptor;
                colorDescriptor.depthBufferBits = (useDepthRenderBuffer) ? k_DepthStencilBufferBits : 0;
                cmd.GetTemporaryRT(m_ActiveCameraColorAttachment.id, colorDescriptor, FilterMode.Bilinear);
            }

            if (m_ActiveCameraDepthAttachment != RenderTargetHandle.CameraTarget)
            {
                var depthDescriptor = descriptor;
                depthDescriptor.colorFormat = RenderTextureFormat.Depth;
                depthDescriptor.depthBufferBits = k_DepthStencilBufferBits;
                depthDescriptor.bindMS = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve && (SystemInfo.supportsMultisampledTextures != 0);
                cmd.GetTemporaryRT(m_ActiveCameraDepthAttachment.id, depthDescriptor, FilterMode.Point);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void SetupBackbufferFormat(int msaaSamples, bool stereo)
        {
#if ENABLE_VR && ENABLE_VR_MODULE
            bool msaaSampleCountHasChanged = false;
            int currentQualitySettingsSampleCount = QualitySettings.antiAliasing;
            if (currentQualitySettingsSampleCount != msaaSamples &&
                !(currentQualitySettingsSampleCount == 0 && msaaSamples == 1))
            {
                msaaSampleCountHasChanged = true;
            }

            // There's no exposed API to control how a backbuffer is created with MSAA
            // By settings antiAliasing we match what the amount of samples in camera data with backbuffer
            // We only do this for the main camera and this only takes effect in the beginning of next frame.
            // This settings should not be changed on a frame basis so that's fine.
            QualitySettings.antiAliasing = msaaSamples;

            if (stereo && msaaSampleCountHasChanged)
                XR.XRDevice.UpdateEyeTextureMSAASetting();
#else
            QualitySettings.antiAliasing = msaaSamples;
#endif
        }
        bool RequiresIntermediateColorTexture(ref RenderingData renderingData, RenderTextureDescriptor baseDescriptor)
        {
            // When rendering a camera stack we always create an intermediate render texture to composite camera results.
            // We create it upon rendering the Base camera.
            if (renderingData.cameraData.renderType == CameraRenderType.Base && !renderingData.resolveFinalTarget)
                return true;

            ref CameraData cameraData = ref renderingData.cameraData;
            int msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;
            bool isStereoEnabled = renderingData.cameraData.isStereoEnabled;
            bool isScaledRender = !Mathf.Approximately(cameraData.renderScale, 1.0f) && !cameraData.isStereoEnabled;
            bool isCompatibleBackbufferTextureDimension = baseDescriptor.dimension == TextureDimension.Tex2D;
            bool requiresExplicitMsaaResolve = msaaSamples > 1 && !SystemInfo.supportsMultisampleAutoResolve;
            bool isOffscreenRender = cameraData.targetTexture != null && !cameraData.isSceneViewCamera;
            bool isCapturing = cameraData.captureActions != null;

#if ENABLE_VR && ENABLE_VR_MODULE
            if (isStereoEnabled)
                isCompatibleBackbufferTextureDimension = UnityEngine.XR.XRSettings.deviceEyeTextureDimension == baseDescriptor.dimension;
#endif

            bool requiresBlitForOffscreenCamera = cameraData.postProcessEnabled || cameraData.requiresOpaqueTexture || requiresExplicitMsaaResolve;
            if (isOffscreenRender)
                return requiresBlitForOffscreenCamera;

            return requiresBlitForOffscreenCamera || cameraData.isSceneViewCamera || isScaledRender || cameraData.isHdrEnabled ||
                   !isCompatibleBackbufferTextureDimension || !cameraData.isDefaultViewport || isCapturing ||
                   (Display.main.requiresBlitToBackbuffer && !isStereoEnabled);
        }

        bool CanCopyDepth(ref CameraData cameraData)
        {
            bool msaaEnabledForCamera = cameraData.cameraTargetDescriptor.msaaSamples > 1;
            bool supportsTextureCopy = SystemInfo.copyTextureSupport != CopyTextureSupport.None;
            bool supportsDepthTarget = RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Depth);
            bool supportsDepthCopy = !msaaEnabledForCamera && (supportsDepthTarget || supportsTextureCopy);

            // TODO:  We don't have support to highp Texture2DMS currently and this breaks depth precision.
            // currently disabling it until shader changes kick in.
            //bool msaaDepthResolve = msaaEnabledForCamera && SystemInfo.supportsMultisampledTextures != 0;
            bool msaaDepthResolve = false;
            return supportsDepthCopy || msaaDepthResolve;
        }
    }
}
