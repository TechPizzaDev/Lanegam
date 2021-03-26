using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;

namespace Lanegam.Client
{
    public class SceneContext
    {
        public DeviceBuffer CameraInfoBuffer { get; private set; }
        
        // MainSceneView and Duplicator resource sets both use this.
        public ResourceLayout TextureSamplerResourceLayout { get; private set; }

        public Texture MainSceneColorTexture { get; private set; }
        public Texture MainSceneDepthTexture { get; private set; }
        public Framebuffer MainSceneFramebuffer { get; private set; }

        public Texture MainSceneResolvedColorTexture { get; private set; }
        public TextureView MainSceneResolvedColorView { get; private set; }
        public ResourceSet MainSceneViewResourceSet { get; private set; }

        public Texture DuplicatorTarget0 { get; private set; }
        public TextureView DuplicatorTargetView0 { get; private set; }
        public ResourceSet DuplicatorTargetSet0 { get; internal set; }
        public Texture DuplicatorTarget1 { get; private set; }
        public TextureView DuplicatorTargetView1 { get; private set; }
        public ResourceSet DuplicatorTargetSet1 { get; internal set; }
        public Framebuffer DuplicatorFramebuffer { get; private set; }

        public Camera Camera { get; set; }
        public TextureSampleCount MainSceneSampleCount { get; internal set; }

        public virtual void CreateGraphicsDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            ResourceFactory factory = gd.ResourceFactory;

            CameraInfoBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<CameraInfo>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));
            if (Camera != null)
            {
                UpdateCameraBuffers(cl);
            }

            TextureSamplerResourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("SourceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            RecreateWindowSizedResources(gd, cl);
        }

        public virtual void DisposeGraphicsDeviceObjects()
        {
            CameraInfoBuffer.Dispose();
            MainSceneColorTexture.Dispose();
            MainSceneResolvedColorTexture.Dispose();
            MainSceneResolvedColorView.Dispose();
            MainSceneDepthTexture.Dispose();
            MainSceneFramebuffer.Dispose();
            MainSceneViewResourceSet.Dispose();
            DuplicatorTarget0.Dispose();
            DuplicatorTarget1.Dispose();
            DuplicatorTargetView0.Dispose();
            DuplicatorTargetView1.Dispose();
            DuplicatorTargetSet0.Dispose();
            DuplicatorTargetSet1.Dispose();
            DuplicatorFramebuffer.Dispose();
            TextureSamplerResourceLayout.Dispose();
        }

        public void SetCurrentScene(Scene scene)
        {
            Camera = scene.Camera;
        }

        public void UpdateCameraBuffers(CommandList cl)
        {
            cl.UpdateBuffer(CameraInfoBuffer, 0, Camera.GetCameraInfo());
        }

        internal void RecreateWindowSizedResources(GraphicsDevice gd, CommandList cl)
        {
            MainSceneColorTexture?.Dispose();
            MainSceneDepthTexture?.Dispose();
            MainSceneResolvedColorTexture?.Dispose();
            MainSceneResolvedColorView?.Dispose();
            MainSceneViewResourceSet?.Dispose();
            MainSceneFramebuffer?.Dispose();
            DuplicatorTarget0?.Dispose();
            DuplicatorTarget1?.Dispose();
            DuplicatorTargetView0?.Dispose();
            DuplicatorTargetView1?.Dispose();
            DuplicatorTargetSet0?.Dispose();
            DuplicatorTargetSet1?.Dispose();
            DuplicatorFramebuffer?.Dispose();

            ResourceFactory factory = gd.ResourceFactory;

            gd.GetPixelFormatSupport(
                PixelFormat.R16_G16_B16_A16_Float,
                TextureType.Texture2D,
                TextureUsage.RenderTarget,
                out PixelFormatProperties properties);

            TextureSampleCount sampleCount = MainSceneSampleCount;
            while (!properties.IsSampleCountSupported(sampleCount))
            {
                sampleCount -= 1;
            }

            TextureDescription mainColorDesc = TextureDescription.Texture2D(
                gd.SwapchainFramebuffer.Width,
                gd.SwapchainFramebuffer.Height,
                1,
                1,
                PixelFormat.R16_G16_B16_A16_Float,
                TextureUsage.RenderTarget | TextureUsage.Sampled,
                sampleCount);

            MainSceneColorTexture = factory.CreateTexture(ref mainColorDesc);
            if (sampleCount != TextureSampleCount.Count1)
            {
                mainColorDesc.SampleCount = TextureSampleCount.Count1;
                MainSceneResolvedColorTexture = factory.CreateTexture(ref mainColorDesc);
            }
            else
            {
                MainSceneResolvedColorTexture = MainSceneColorTexture;
            }
            MainSceneResolvedColorView = factory.CreateTextureView(MainSceneResolvedColorTexture);
            MainSceneDepthTexture = factory.CreateTexture(TextureDescription.Texture2D(
                gd.SwapchainFramebuffer.Width,
                gd.SwapchainFramebuffer.Height,
                1,
                1,
                PixelFormat.R32_Float,
                TextureUsage.DepthStencil,
                sampleCount));
            MainSceneFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(MainSceneDepthTexture, MainSceneColorTexture));
            MainSceneViewResourceSet = factory.CreateResourceSet(new ResourceSetDescription(TextureSamplerResourceLayout, MainSceneResolvedColorView, gd.PointSampler));

            TextureDescription colorTargetDesc = TextureDescription.Texture2D(
                gd.SwapchainFramebuffer.Width,
                gd.SwapchainFramebuffer.Height,
                1,
                1,
                PixelFormat.R16_G16_B16_A16_Float,
                TextureUsage.RenderTarget | TextureUsage.Sampled);
            DuplicatorTarget0 = factory.CreateTexture(ref colorTargetDesc);
            DuplicatorTargetView0 = factory.CreateTextureView(DuplicatorTarget0);
            DuplicatorTarget1 = factory.CreateTexture(ref colorTargetDesc);
            DuplicatorTargetView1 = factory.CreateTextureView(DuplicatorTarget1);
            DuplicatorTargetSet0 = factory.CreateResourceSet(new ResourceSetDescription(TextureSamplerResourceLayout, DuplicatorTargetView0, gd.PointSampler));
            DuplicatorTargetSet1 = factory.CreateResourceSet(new ResourceSetDescription(TextureSamplerResourceLayout, DuplicatorTargetView1, gd.PointSampler));

            FramebufferDescription fbDesc = new FramebufferDescription(null, DuplicatorTarget0, DuplicatorTarget1);
            DuplicatorFramebuffer = factory.CreateFramebuffer(ref fbDesc);
        }
    }

    public class CascadedShadowMaps
    {
        public Texture NearShadowMap { get; private set; }
        public TextureView NearShadowMapView { get; private set; }
        public Framebuffer NearShadowMapFramebuffer { get; private set; }

        public Texture MidShadowMap { get; private set; }
        public TextureView MidShadowMapView { get; private set; }
        public Framebuffer MidShadowMapFramebuffer { get; private set; }

        public Texture FarShadowMap { get; private set; }
        public TextureView FarShadowMapView { get; private set; }
        public Framebuffer FarShadowMapFramebuffer { get; private set; }

        public void CreateDeviceResources(GraphicsDevice gd)
        {
            var factory = gd.ResourceFactory;
            TextureDescription desc = TextureDescription.Texture2D(2048, 2048, 1, 1, PixelFormat.D32_Float_S8_UInt, TextureUsage.DepthStencil | TextureUsage.Sampled);

            NearShadowMap = factory.CreateTexture(desc);
            NearShadowMap.Name = "Near Shadow Map";
            NearShadowMapView = factory.CreateTextureView(NearShadowMap);
            NearShadowMapFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(
                new FramebufferAttachmentDescription(NearShadowMap, 0), Array.Empty<FramebufferAttachmentDescription>()));

            MidShadowMap = factory.CreateTexture(desc);
            MidShadowMapView = factory.CreateTextureView(new TextureViewDescription(MidShadowMap, 0, 1, 0, 1));
            MidShadowMapFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(
                new FramebufferAttachmentDescription(MidShadowMap, 0), Array.Empty<FramebufferAttachmentDescription>()));

            FarShadowMap = factory.CreateTexture(desc);
            FarShadowMapView = factory.CreateTextureView(new TextureViewDescription(FarShadowMap, 0, 1, 0, 1));
            FarShadowMapFramebuffer = factory.CreateFramebuffer(new FramebufferDescription(
                new FramebufferAttachmentDescription(FarShadowMap, 0), Array.Empty<FramebufferAttachmentDescription>()));
        }

        public void DestroyDeviceObjects()
        {
            NearShadowMap.Dispose();
            NearShadowMapView.Dispose();
            NearShadowMapFramebuffer.Dispose();

            MidShadowMap.Dispose();
            MidShadowMapView.Dispose();
            MidShadowMapFramebuffer.Dispose();

            FarShadowMap.Dispose();
            FarShadowMapView.Dispose();
            FarShadowMapFramebuffer.Dispose();
        }
    }
}
