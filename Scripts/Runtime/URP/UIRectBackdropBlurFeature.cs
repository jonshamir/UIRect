#if UIRECT_URP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UIRECT_URP_RG
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace UIRect
{
    /// <summary>
    /// URP provider for UIRect backdrop blur. Add this Renderer Feature to your URP Renderer asset.
    /// It captures the camera color before transparents, blurs it, and exposes the global
    /// <c>_UIRectBackdropTex</c> that the <c>UI/UIRectGlass</c> shader samples.
    ///
    /// XR: correct under single-pass instanced / multiview and multi-pass. It drives the blur with Unity's
    /// <see cref="Blitter"/> (which loops the eye slices of the camera texture array) and a URP HLSL shader
    /// (<c>Hidden/UIRect/BackdropBlurURP</c>) that samples via the stereo <c>_X</c> macros. The render
    /// targets are allocated from the camera descriptor, so they become texture arrays automatically in XR.
    ///
    /// Two execution paths, selected automatically by URP:
    /// <list type="bullet">
    /// <item><b>Render Graph</b> (URP 17+ / Unity 6, when <c>UIRECT_URP_RG</c> is defined): <see cref="BackdropBlurPass.RecordRenderGraph"/>.</item>
    /// <item><b>Compatibility mode</b> (older URP, or Unity 6 with Render Graph disabled): the legacy
    /// <c>OnCameraSetup</c>/<c>Execute</c> path.</item>
    /// </list>
    /// Blur amount comes from the shared <see cref="UIRectBlurSettings"/>. Unlike the Built-in provider this
    /// path does not use <c>UIRectBlurCore</c> (that path's <c>cmd.Blit</c> is not stereo-aware).
    /// </summary>
    public class UIRectBackdropBlurFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            public UIRectBlurSettings blur = UIRectBlurSettings.Default;
            public Shader blurShader;
        }

        public Settings settings = new Settings();
        private BackdropBlurPass _pass;
        private bool _registered;

        public override void Create()
        {
            var shader = settings.blurShader != null ? settings.blurShader : Shader.Find(UIRectBlurConstants.ShaderNameURP);
            if (shader == null)
                return;
            var material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new BackdropBlurPass(material, settings) { renderPassEvent = settings.renderPassEvent };
            if (!_registered)
            {
                UIRectBlurCore.RegisterProvider();
                _registered = true;
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
                return;
            if (renderingData.cameraData.cameraType == CameraType.Preview)
                return;
            // Nothing samples _UIRectBackdropTex, so skip the pass entirely. This must happen before
            // ConfigureInput: that call is what forces URP to allocate and resolve an intermediate camera
            // color texture, which on tiler GPUs costs more than the blur itself. With the feature merely
            // present on the renderer and no backdrop in the scene, this makes it free.
            if (!UIRectBlurCore.HasWork)
                return;
            // The camera color target must be read inside the pass, so it is grabbed in Execute / RecordRenderGraph.
            _pass.ConfigureInput(ScriptableRenderPassInput.Color);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            _pass = null;
            if (_registered)
            {
                UIRectBlurCore.UnregisterProvider();
                _registered = false;
            }
        }

        private class BackdropBlurPass : ScriptableRenderPass
        {
            private const string ProfilerTag = "UIRect Backdrop Blur";
            // Stable names for the downsample-chain intermediates (indices 1..MaxDownsample-1).
            private static readonly string[] LevelNames = { null, "_UIRectBackdropDown1", "_UIRectBackdropDown2", "_UIRectBackdropDown3" };

            private readonly Material _material;
            private readonly Settings _settings;

            // Compatibility-path resources (the Render Graph path creates its textures per frame instead).
            private RTHandle _rtA;
            private RTHandle _rtB;
            private readonly RTHandle[] _levels = new RTHandle[UIRectBlurConstants.MaxDownsample];

            public BackdropBlurPass(Material material, Settings settings)
            {
                _material = material;
                _settings = settings;
            }

            private int Downsample => Mathf.Clamp(_settings.blur.downsample,
                UIRectBlurConstants.MinDownsample, UIRectBlurConstants.MaxDownsample);

            // Separable Gaussian ping-pong between dst and scratch via the XR-aware Blitter; result ends in dst.
            private static void BlurPingPong(CommandBuffer cmd, Material material, RTHandle dst, RTHandle scratch,
                int iterations, float radius, int width, int height)
            {
                iterations = Mathf.Clamp(iterations, UIRectBlurConstants.MinIterations, UIRectBlurConstants.MaxIterations);
                for (int i = 0; i < iterations; i++)
                {
                    cmd.SetGlobalVector(UIRectBlurConstants.BlurDirID, new Vector4(radius / width, 0f, 0f, 0f));
                    Blitter.BlitCameraTexture(cmd, dst, scratch, material, 0);
                    cmd.SetGlobalVector(UIRectBlurConstants.BlurDirID, new Vector4(0f, radius / height, 0f, 0f));
                    Blitter.BlitCameraTexture(cmd, scratch, dst, material, 0);
                }
            }

            // --- Compatibility path -------------------------------------------------------------------

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var baseDesc = renderingData.cameraData.cameraTargetDescriptor;
                baseDesc.depthBufferBits = 0;
                baseDesc.msaaSamples = 1;
                // dimension / vrUsage are preserved from the camera target, so these become texture arrays
                // in single-pass instanced XR and Blitter blits every eye slice.
                int ds = Downsample;

                var desc = baseDesc;
                desc.width = Mathf.Max(1, baseDesc.width >> ds);
                desc.height = Mathf.Max(1, baseDesc.height >> ds);
                RenderingUtils.ReAllocateIfNeeded(ref _rtA, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_UIRectBackdropA");
                RenderingUtils.ReAllocateIfNeeded(ref _rtB, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_UIRectBackdropB");

                for (int level = 1; level < UIRectBlurConstants.MaxDownsample; level++)
                {
                    if (level < ds)
                    {
                        var ld = baseDesc;
                        ld.width = Mathf.Max(1, baseDesc.width >> level);
                        ld.height = Mathf.Max(1, baseDesc.height >> level);
                        RenderingUtils.ReAllocateIfNeeded(ref _levels[level], ld, FilterMode.Bilinear, TextureWrapMode.Clamp, name: LevelNames[level]);
                    }
                    else
                    {
                        _levels[level]?.Release();
                        _levels[level] = null;
                    }
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (_material == null || source == null || _rtA == null)
                    return;

                var cmd = CommandBufferPool.Get(ProfilerTag);

                // Downsample chain: source -> _levels[1..ds-1] -> _rtA (each an XR-aware bilinear halving).
                int ds = Downsample;
                RTHandle prev = source;
                for (int level = 1; level < ds; level++)
                {
                    Blitter.BlitCameraTexture(cmd, prev, _levels[level], 0.0f, bilinear: true);
                    prev = _levels[level];
                }
                Blitter.BlitCameraTexture(cmd, prev, _rtA, 0.0f, bilinear: true);

                BlurPingPong(cmd, _material, _rtA, _rtB, _settings.blur.iterations, _settings.blur.blurRadius,
                    _rtA.rt.width, _rtA.rt.height);
                cmd.SetGlobalTexture(UIRectBlurConstants.BackdropTexID, _rtA);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            // --- Render Graph path (URP 17+ / Unity 6) ------------------------------------------------
    #if UIRECT_URP_RG
            private class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle dst;
                public TextureHandle scratch;
                public TextureHandle[] levels;
                public int downsample;
                public int iterations;
                public float radius;
                public int width;
                public int height;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                    return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return; // need an intermediate color texture to sample from

                var baseDesc = cameraData.cameraTargetDescriptor;
                baseDesc.depthBufferBits = 0;
                baseDesc.msaaSamples = 1;
                int ds = Downsample;

                var dstDesc = baseDesc;
                dstDesc.width = Mathf.Max(1, baseDesc.width >> ds);
                dstDesc.height = Mathf.Max(1, baseDesc.height >> ds);

                TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, dstDesc, "_UIRectBackdropA", false, FilterMode.Bilinear, TextureWrapMode.Clamp);
                TextureHandle scratch = UniversalRenderer.CreateRenderGraphTexture(renderGraph, dstDesc, "_UIRectBackdropB", false, FilterMode.Bilinear, TextureWrapMode.Clamp);

                var levels = new TextureHandle[UIRectBlurConstants.MaxDownsample];
                for (int level = 1; level < ds; level++)
                {
                    var ld = baseDesc;
                    ld.width = Mathf.Max(1, baseDesc.width >> level);
                    ld.height = Mathf.Max(1, baseDesc.height >> level);
                    levels[level] = UniversalRenderer.CreateRenderGraphTexture(renderGraph, ld, LevelNames[level], false, FilterMode.Bilinear, TextureWrapMode.Clamp);
                }

                using var builder = renderGraph.AddUnsafePass<PassData>(ProfilerTag, out var passData);
                passData.material = _material;
                passData.source = resourceData.activeColorTexture;
                passData.dst = dst;
                passData.scratch = scratch;
                passData.levels = levels;
                passData.downsample = ds;
                passData.iterations = _settings.blur.iterations;
                passData.radius = _settings.blur.blurRadius;
                passData.width = dstDesc.width;
                passData.height = dstDesc.height;

                builder.UseTexture(resourceData.activeColorTexture);
                builder.UseTexture(dst, AccessFlags.ReadWrite);
                builder.UseTexture(scratch, AccessFlags.ReadWrite);
                for (int level = 1; level < ds; level++)
                    builder.UseTexture(levels[level], AccessFlags.ReadWrite);
                // The only consumer is the UI/UIRectGlass draw, which reads _UIRectBackdropTex as a global
                // and so is invisible to the graph - without this the pass is culled as producing nothing.
                // Enqueueing is already gated on a backdrop existing (see AddRenderPasses), so this does
                // not keep dead work alive.
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                // Publish the blurred result as the global the UI/UIRectGlass shader samples.
                builder.SetGlobalTextureAfterPass(dst, UIRectBlurConstants.BackdropTexID);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext ctx) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);

                    RTHandle prev = (RTHandle)data.source;
                    for (int level = 1; level < data.downsample; level++)
                    {
                        RTHandle lvl = (RTHandle)data.levels[level];
                        Blitter.BlitCameraTexture(cmd, prev, lvl, 0.0f, bilinear: true);
                        prev = lvl;
                    }
                    RTHandle dstH = (RTHandle)data.dst;
                    Blitter.BlitCameraTexture(cmd, prev, dstH, 0.0f, bilinear: true);

                    BlurPingPong(cmd, data.material, dstH, (RTHandle)data.scratch,
                        data.iterations, data.radius, data.width, data.height);
                    // The global is published by builder.SetGlobalTextureAfterPass above.
                });
            }
    #endif

            public void Dispose()
            {
                _rtA?.Release();
                _rtB?.Release();
                _rtA = _rtB = null;
                for (int level = 1; level < UIRectBlurConstants.MaxDownsample; level++)
                {
                    _levels[level]?.Release();
                    _levels[level] = null;
                }
                CoreUtils.Destroy(_material);
            }
        }
    }
}
#endif
