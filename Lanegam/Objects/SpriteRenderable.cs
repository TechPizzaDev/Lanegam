using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lanegam.Client;
using Veldrid;

namespace Lanegam.Objects
{
    public class SpriteRenderable : Renderable, IUpdateable
    {
        private GeometryBatch<VertexPositionColorTexture> batch;
        private Pipeline batchPipeline;
        private bool needsbuild;

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public SpriteRenderable()
        {
            uint quadCap = 1024 * 16;
            batch = new GeometryBatch<VertexPositionColorTexture>(6 * quadCap, 4 * quadCap);
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            batch.CreateDeviceObjects(gd, cl, sc);
            batchPipeline = SetupBatchPipeline(
                gd,
                gd.ResourceFactory,
                sc.MainSceneFramebuffer.OutputDescription);

            needsbuild = true;
        }

        public override void DestroyDeviceObjects()
        {
            batch.DestroyDeviceObjects();
        }

        public override RenderOrderKey GetRenderOrderKey(Vector3 cameraPosition)
        {
            return new RenderOrderKey();
        }

        public override void Render(GraphicsDevice gd, CommandList cl, SceneContext sc, RenderPasses renderPass)
        {
            cl.SetPipeline(batchPipeline);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);
            batch.Submit(cl);
        }

        private Pipeline SetupBatchPipeline(GraphicsDevice device, ResourceFactory factory, OutputDescription outputs)
        {
            ResourceLayout resourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("SourceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            (Shader vs, Shader fs, SpecializationConstant[] specs) = StaticResourceCache.GetShaders(
                device, device.ResourceFactory, "GeometryBatch", stackalloc[] { new SpecializationConstant(103, false) });

            var raster = RasterizerStateDescription.Default;
            raster.CullMode = FaceCullMode.None;

            GraphicsPipelineDescription pd = new(
                new BlendStateDescription(
                    RgbaFloat.Black,
                    BlendAttachmentDescription.OverrideBlend),
                DepthStencilStateDescription.Disabled,
                raster,
                PrimitiveTopology.TriangleList,
                new ShaderSetDescription(
                    new[]
                    {
                        new VertexLayoutDescription(
                            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                            new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Byte4),
                            new VertexElementDescription("TexCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UShort2))
                    },
                    new[] { vs, fs },
                    specs),
                new ResourceLayout[] { /*resourceLayout*/ },
                outputs);

            return factory.CreateGraphicsPipeline(ref pd);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public unsafe void Update(in FrameTime time)
        {
            //if (!needsbuild)
            //{
            //    return;
            //}
            //needsbuild = false;

            batch.Begin();

            //Span<VertexPositionColorTexture> vertices = stackalloc VertexPositionColorTexture[]
            //{
            //    new VertexPositionColorTexture
            //    {
            //        Position = new Vector3(-0.5f, -0.5f, 0)
            //    },
            //    new VertexPositionColorTexture
            //    {
            //        Position = new Vector3(-0.5f, 0.5f, 0)
            //    },
            //    new VertexPositionColorTexture
            //    {
            //        Position = new Vector3(0.5f, 0.5f, 0)
            //    },
            //    new VertexPositionColorTexture
            //    {
            //        Position = new Vector3(0.5f, -0.5f, 0)
            //    },
            //};
            //
            //for (int i = 0; i < vertices.Length; i++)
            //{
            //    vertices[i].Position *= 0.01f;
            //}

            //for (int j = 0; j < 1024 * 256; j++)
            //{
            //    float t = time.TotalSeconds + j * MathF.PI / 1024;
            //    //float s;
            //    //if(j < 2)
            //    //    s = (MathF.Cos(t) + 1) * 0.02f;
            //    //else
            //    //    s = (MathF.Sin(t) + 1) * 0.02f;
            //
            //    float x = MathF.Cos(t) * 0.01f;
            //    float y = MathF.Sin(t) * 0.01f;
            //
            //    vertices[0].Position.X = -x; // s; // * MathF.Cos(t * 1);
            //    vertices[0].Position.Y = -y; // s; // * MathF.Sin(t * 1);
            //                                 //  
            //    vertices[1].Position.X = -x; // s; // * MathF.Cos(t * MathF.PI * 1.3f);
            //    vertices[1].Position.Y = y; // s; // * MathF.Sin(t * MathF.PI * 1.3f);
            //                                // 
            //    vertices[2].Position.X = x; // s; // * MathF.Cos(t * MathF.PI * 2.6f);
            //    vertices[2].Position.Y = y; // s; // * MathF.Sin(t * MathF.PI * 2.6f);
            //                                //  
            //    vertices[3].Position.X = x; // s; // * MathF.Cos(t * MathF.PI * 3.9f);
            //    vertices[3].Position.Y = -y;// s; // * MathF.Sin(t * MathF.PI * 3.9f);
            //                             
            //    vertices[0].Color = new RgbaByte(255, 0, 0, 255);
            //    vertices[1].Color = new RgbaByte(0, 127, 0, 255);
            //    vertices[2].Color = new RgbaByte(0, 127, 0, 255);
            //    vertices[3].Color = new RgbaByte(0, 0, 255, 255);
            //    //for (int i = 0; i < vertices.Length; i++)
            //    //{
            //    //    vertices[i].Position.Y -= 0.5f;
            //    //}
            //    batch.AppendQuad(vertices[0], vertices[1], vertices[2], vertices[3]);
            //
            //    //vertices[0].Color = new RgbaByte(0, 0, 255, 255);
            //    ////for (int i = 0; i < vertices.Length; i++)
            //    ////{
            //    ////    vertices[i].Position.Y += 1f;
            //    ////}
            //    //batch.AppendQuad(vertices[0], vertices[1], vertices[2], vertices[3]);
            //}

            const int count = 2;
            for (int j = 0; j < 1024 * 256 / count; j++)
            {
                float t = time.TotalSeconds + j * MathF.PI / 1024;

                float x = MathF.Cos(t) * 0.01f;
                float y = MathF.Sin(t) * 0.01f;

                var reserve = batch.ReserveQuadsUnsafe(count);

                for (int i = 0; i < count; i++)
                {
                    VertexPositionColorTexture* ptr = reserve.Vertices + i * 4;

                    ptr[0].Position.X = -x;
                    ptr[0].Position.Y = -y;
                    ptr[0].Position.Z = 0;
                    ptr[0].Color = new RgbaByte(255, 0, 0, 255);

                    ptr[1].Position.X = -x;
                    ptr[1].Position.Y = y;
                    ptr[1].Position.Z = 0;
                    ptr[1].Color = new RgbaByte(0, 127, 0, 255);

                    ptr[2].Position.X = x;
                    ptr[2].Position.Y = y;
                    ptr[2].Position.Z = 0;
                    ptr[2].Color = new RgbaByte(0, 127, 0, 255);

                    ptr[3].Position.X = x;
                    ptr[3].Position.Y = -y;
                    ptr[3].Position.Z = 0;
                    ptr[3].Color = new RgbaByte(0, 0, 255, 255);

                }
            }


            batch.End();
        }

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
        }
    }

    public struct UShort2
    {
        public ushort X;
        public ushort Y;
    }

    public struct VertexPositionColorTexture
    {
        public Vector3 Position;
        public RgbaByte Color;
        public UShort2 Texture;
    }
}
