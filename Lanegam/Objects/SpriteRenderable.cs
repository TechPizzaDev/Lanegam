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
        private ImageSharpTexture image0;

        private Pipeline batchPipeline;
        private ResourceSet matrixSet;
        private ResourceSet texSet0;

        private DeviceBuffer matrixBuffer;
        private Texture tex0;
        private Sampler sampler0;

        private bool needsbuild;

        public override RenderPasses RenderPasses => RenderPasses.Opaque;

        public SpriteRenderable()
        {
            uint quadCap = 1024 * 16;
            batch = new GeometryBatch<VertexPositionColorTexture>(6 * quadCap, 4 * quadCap);
            image0 = new ImageSharpTexture("Assets/Textures/DurrrSpaceShip.png", false);
        }

        public override void CreateDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            batch.CreateDeviceObjects(gd, cl, sc);

            tex0 = image0.CreateDeviceTexture(gd, gd.ResourceFactory);

            sampler0 = gd.PointSampler;

            SetupBatchPipeline(
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
            float width = sc.MainSceneFramebuffer.Width;
            float height = sc.MainSceneFramebuffer.Height;

            Matrices matrices;
            matrices.Projection = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, 0, 1);
            matrices.View = Matrix4x4.CreateLookAt(new Vector3(0, 0, 0), new Vector3(0, 0, 0), Vector3.UnitY);
            matrices.World = Matrix4x4.Identity;
            cl.UpdateBuffer(matrixBuffer, 0, ref matrices);

            cl.SetPipeline(batchPipeline);
            cl.SetFramebuffer(sc.MainSceneFramebuffer);
            cl.SetFullViewport(0);
            cl.SetGraphicsResourceSet(0, matrixSet);
            cl.SetGraphicsResourceSet(1, texSet0);
            batch.Submit(cl);
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

            ushort imgW = (ushort)image0.Width;
            ushort imgH = (ushort)image0.Height;
            float tt = time.TotalSeconds;

            const int count = 1;
            for (int yy = 0; yy < 4 / 4; yy++)
            {
                for (int xx = 0; xx < 4 / 4; xx++)
                {
                    //float t = time.TotalSeconds + j * MathF.PI / 1024;

                    //float x = MathF.Cos(t) * 20f + 200;
                    //float y = MathF.Sin(t) * 20f + 200;
                    float x = xx * 4 + 100;
                    float y = yy * 4 + 100;
                    float w = 400;
                    float h = 400;

                    byte r = (byte)((MathF.Cos(tt) + 1) * 127.5f);
                    byte g = (byte)((MathF.Sin(tt) + 1) * 127.5f);

                    var reserve = batch.ReserveQuadsUnsafe(count);

                    for (int i = 0; i < count; i++)
                    {
                        VertexPositionColorTexture* ptr = reserve.Vertices + i * 4;

                        // bottom-right
                        ptr[0].Position.X = x + w;
                        ptr[0].Position.Y = y + h;
                        ptr[0].Position.Z = 0;
                        ptr[0].Color = new RgbaByte(0, g, 0, 255);
                        ptr[0].Texture = new UShort2(imgW, imgH);

                        // bottom-left
                        ptr[1].Position.X = x;
                        ptr[1].Position.Y = y + h;
                        ptr[1].Position.Z = 0;
                        ptr[1].Color = new RgbaByte(0, g, 0, 255);
                        ptr[1].Texture = new UShort2(0, imgH);

                        // top-left
                        ptr[2].Position.X = x;
                        ptr[2].Position.Y = y;
                        ptr[2].Position.Z = 0;
                        ptr[2].Color = new RgbaByte(r, 0, 0, 255);
                        ptr[2].Texture = new UShort2(0, 0);

                        // top-right
                        ptr[3].Position.X = x + w;
                        ptr[3].Position.Y = y;
                        ptr[3].Position.Z = 0;
                        ptr[3].Color = new RgbaByte(0, 0, 255, 255);
                        ptr[3].Texture = new UShort2(imgW, 0);
                    }
                }
            }

            batch.End();
        }

        public struct Matrices
        {
            public Matrix4x4 Projection;
            public Matrix4x4 View;
            public Matrix4x4 World;
        }

        public override void UpdatePerFrameResources(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
        }

        private void SetupBatchPipeline(GraphicsDevice device, ResourceFactory factory, OutputDescription outputs)
        {
            ResourceLayout matrixLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Matrices", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            ResourceLayout texLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("Texture0", ResourceKind.TextureReadOnly, ShaderStages.Vertex | ShaderStages.Fragment),
                new ResourceLayoutElementDescription("Sampler0", ResourceKind.Sampler, ShaderStages.Vertex | ShaderStages.Fragment)));

            (Shader vs, Shader fs, SpecializationConstant[] specs) = StaticResourceCache.GetShaders(
                device, device.ResourceFactory, "GeometryBatch", stackalloc[] { new SpecializationConstant(103, false) });

            var depthStencilState = device.IsDepthRangeZeroToOne
                ? DepthStencilStateDescription.DepthOnlyGreaterEqual
                : DepthStencilStateDescription.DepthOnlyLessEqual;

            var rasterizerState = RasterizerStateDescription.Default;
            
            GraphicsPipelineDescription pd = new(
                new BlendStateDescription(
                    RgbaFloat.Black,
                    BlendAttachmentDescription.OverrideBlend),
                depthStencilState,
                rasterizerState,
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
                new ResourceLayout[] { matrixLayout, texLayout },
                outputs);

            batchPipeline = factory.CreateGraphicsPipeline(ref pd);

            matrixBuffer = factory.CreateBuffer(new BufferDescription((uint)Unsafe.SizeOf<Matrices>(), BufferUsage.UniformBuffer | BufferUsage.Dynamic));

            matrixSet = factory.CreateResourceSet(new ResourceSetDescription(
                matrixLayout, matrixBuffer));

            texSet0 = factory.CreateResourceSet(new ResourceSetDescription(
                texLayout, tex0, sampler0));
        }
    }

    public struct UShort2
    {
        public ushort X;
        public ushort Y;

        public UShort2(ushort x, ushort y)
        {
            X = x;
            Y = y;
        }
    }

    public struct VertexPositionColorTexture
    {
        public Vector3 Position;
        public RgbaByte Color;
        public UShort2 Texture;
    }
}
