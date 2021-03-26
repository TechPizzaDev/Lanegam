using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.Utilities;

namespace Lanegam.Client
{
    public class Scene
    {
        private static Func<RenderPasses, Func<CullRenderable, bool>> _createFilterFunc = rp => CreateFilter(rp);

        private CommandList _resourceUpdateCL;
        private List<GraphicsResource> _graphicsResources = new();
        private List<Renderable> _freeRenderables = new();
        private List<IUpdateable> _updateables = new();

        private HashSet<Renderable> _allPerFrameRenderablesSet = new();
        private RenderQueue _renderQueue = new();
        private List<CullRenderable> _cullableStage = new();
        private List<Renderable> _renderableStage = new();
        private ConcurrentDictionary<RenderPasses, Func<CullRenderable, bool>> _filters = new();

        private Octree<CullRenderable> _octree = new(new BoundingBox(Vector3.One * -50, Vector3.One * 50), 2);

        public Camera Camera { get; }

        public Scene(GraphicsDevice gd, Sdl2Window window)
        {
            Camera = new Camera(gd, window);
            AddUpdateable(Camera);
        }

        public void AddGraphicsResource(GraphicsResource graphicsResource)
        {
            if (graphicsResource == null)
                throw new ArgumentNullException(nameof(graphicsResource));

            _graphicsResources.Add(graphicsResource);
        }

        public void AddRenderable(Renderable renderable, bool addAsGraphicsResource = true)
        {
            if (renderable == null)
                throw new ArgumentNullException(nameof(renderable));

            if (renderable is CullRenderable cr)
            {
                _octree.AddItem(cr.BoundingBox, cr);
            }
            else
            {
                _freeRenderables.Add(renderable);
            }

            if (addAsGraphicsResource)
                AddGraphicsResource(renderable);
        }

        public void AddUpdateable(IUpdateable updateable)
        {
            if (updateable == null)
                throw new ArgumentNullException(nameof(updateable));

            _updateables.Add(updateable);
        }

        public void Update(in FrameTime time)
        {
            foreach (IUpdateable updateable in _updateables)
            {
                updateable.Update(time);
            }
        }

        public void RenderAllStages(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            RenderQueue renderQueue = _renderQueue;
            List<CullRenderable> cullableStage = _cullableStage;
            List<Renderable> renderableStage = _renderableStage;

            float depthClear = gd.IsDepthRangeZeroToOne ? 0f : 1f;

            cl.PushDebugGroup("Scene");
            cl.SetFramebuffer(sc.MainSceneFramebuffer);
            float fbWidth = sc.MainSceneFramebuffer.Width;
            float fbHeight = sc.MainSceneFramebuffer.Height;
            cl.SetViewport(0, new Viewport(0, 0, fbWidth, fbHeight, 0, 1f));
            cl.ClearColorTarget(0, RgbaFloat.Black);
            cl.ClearDepthStencil(depthClear);
            cl.SetFullScissorRects();
            var cameraFrustum = new BoundingFrustum(Camera.ViewMatrix * Camera.ProjectionMatrix);
            
            Render(gd, cl, sc, RenderPasses.Opaque, cameraFrustum, Camera.Position, renderQueue, cullableStage, renderableStage, null);
            cl.PopDebugGroup();

            cl.PushDebugGroup("AlphaBlend");
            Render(gd, cl, sc, RenderPasses.AlphaBlend, cameraFrustum, Camera.Position, renderQueue, cullableStage, renderableStage, null);
            cl.PopDebugGroup();

            cl.PushDebugGroup("Overlay");
            Render(gd, cl, sc, RenderPasses.Overlay, cameraFrustum, Camera.Position, renderQueue, cullableStage, renderableStage, null);
            cl.PopDebugGroup();

            if (sc.MainSceneColorTexture.SampleCount != TextureSampleCount.Count1)
            {
                cl.ResolveTexture(sc.MainSceneColorTexture, sc.MainSceneResolvedColorTexture);
            }

            cl.PushDebugGroup("Duplicator");
            cl.SetFramebuffer(sc.DuplicatorFramebuffer);
            cl.SetFullViewports();
            Render(gd, cl, sc, RenderPasses.Duplicator, new BoundingFrustum(), Camera.Position, renderQueue, cullableStage, renderableStage, null);
            cl.PopDebugGroup();

            cl.PushDebugGroup("SwapchainOutput");
            cl.SetFramebuffer(gd.SwapchainFramebuffer);
            cl.SetFullViewports();
            Render(gd, cl, sc, RenderPasses.SwapchainOutput, new BoundingFrustum(), Camera.Position, renderQueue, cullableStage, renderableStage, null);
            cl.PopDebugGroup();

            _resourceUpdateCL.Begin();
            {
                foreach (Renderable renderable in _allPerFrameRenderablesSet)
                {
                    renderable.UpdatePerFrameResources(gd, _resourceUpdateCL, sc);
                }
            }
            _resourceUpdateCL.End();
            gd.SubmitCommands(_resourceUpdateCL);
        }

        public void Render(
            GraphicsDevice gd,
            CommandList rc,
            SceneContext sc,
            RenderPasses pass,
            BoundingFrustum frustum,
            Vector3 viewPosition,
            RenderQueue renderQueue,
            List<CullRenderable> cullRenderableList,
            List<Renderable> renderableList,
            Comparer<RenderItemIndex>? comparer)
        {
            renderQueue.Clear();

            cullRenderableList.Clear();
            CollectVisibleObjects(ref frustum, pass, cullRenderableList);
            renderQueue.AddRange(cullRenderableList, viewPosition);

            renderableList.Clear();
            CollectFreeObjects(pass, renderableList);
            renderQueue.AddRange(renderableList, viewPosition);

            if (comparer == null)
            {
                renderQueue.Sort();
            }
            else
            {
                renderQueue.Sort(comparer);
            }

            foreach (Renderable renderable in renderQueue)
            {
                renderable.Render(gd, rc, sc, pass);
            }

            foreach (CullRenderable thing in cullRenderableList)
                _allPerFrameRenderablesSet.Add(thing);
            foreach (Renderable thing in renderableList)
                _allPerFrameRenderablesSet.Add(thing);
        }

        private void CollectVisibleObjects(
            ref BoundingFrustum frustum,
            RenderPasses renderPass,
            List<CullRenderable> renderables)
        {
            _octree.GetContainedObjects(frustum, renderables, GetFilter(renderPass));
        }

        private void CollectFreeObjects(RenderPasses renderPass, List<Renderable> renderables)
        {
            foreach (Renderable r in _freeRenderables)
            {
                if ((r.RenderPasses & renderPass) != 0)
                {
                    renderables.Add(r);
                }
            }
        }

        private Func<CullRenderable, bool> GetFilter(RenderPasses passes)
        {
            return _filters.GetOrAdd(passes, _createFilterFunc);
        }

        static Func<CullRenderable, bool> CreateFilter(RenderPasses rp)
        {
            // This cannot be inlined into GetFilter -- a Roslyn bug causes copious allocations.
            // https://github.com/dotnet/roslyn/issues/22589
            return cr => (cr.RenderPasses & rp) == rp;
        }

        internal void DestroyGraphicsDeviceObjects()
        {
            foreach (GraphicsResource resource in _graphicsResources)
                resource.DestroyDeviceObjects();

            _resourceUpdateCL.Dispose();
        }

        internal void CreateGraphicsDeviceObjects(GraphicsDevice gd, CommandList cl, SceneContext sc)
        {
            foreach (GraphicsResource resource in _graphicsResources)
                resource.CreateDeviceObjects(gd, cl, sc);

            _resourceUpdateCL = gd.ResourceFactory.CreateCommandList();
            _resourceUpdateCL.Name = "Scene Resource Update Command List";
        }

        private class RenderPassesComparer : IEqualityComparer<RenderPasses>
        {
            public bool Equals(RenderPasses x, RenderPasses y) => x == y;

            public int GetHashCode(RenderPasses obj) => ((byte)obj).GetHashCode();
        }
    }
}
