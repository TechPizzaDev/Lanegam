using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Lanegam.Client.Objects;
using Lanegam.Objects;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.Sdl2;

namespace Lanegam.Client
{
    public class Lanegam : Application
    {
        public static RenderDoc? _renderDoc;

        private Sdl2ControllerTracker? _controllerTracker;

        private Scene _scene;
        private SceneContext _sc = new SceneContext();

        private CommandList _frameCommands;

        private ImGuiRenderable _imGuiRenderable;
        private FullScreenQuad _fsq;
        private bool _controllerDebugMenu;

        private readonly string[] _msaaOptions = new string[] { "Off", "2x", "4x", "8x", "16x", "32x" };
        private int _msaaOption = 0;
        private TextureSampleCount? _newSampleCount;

        private ConcurrentQueue<Renderable> _queuedRenderables = new ConcurrentQueue<Renderable>();
        private Dictionary<string, ImageSharpTexture> _textures = new Dictionary<string, ImageSharpTexture>();

        private event Action<int, int> _resizeHandled;
        private bool _windowResized;

        private ParticlePlane particlePlane;
        private SpriteRenderable spriteRenderable;

        public Lanegam() : base(preferredBackend: GraphicsBackend.Direct3D11)
        {
            Sdl2Native.SDL_Init(SDLInitFlags.GameController);
            Sdl2ControllerTracker.CreateDefault(out _controllerTracker);

            GraphicsDevice.SyncToVerticalBlank = true;

            _imGuiRenderable = new ImGuiRenderable(Window.Width, Window.Height);
            _resizeHandled += (w, h) => _imGuiRenderable.WindowResized(w, h);

            _scene = new Scene(GraphicsDevice, Window);
            _scene.Camera.Controller = _controllerTracker;
            _sc.SetCurrentScene(_scene);

            _scene.AddRenderable(_imGuiRenderable);

            _sc.Camera.Position = new Vector3(-6, 24f, -0.43f);
            _sc.Camera.Yaw = MathF.PI * 1.25f;
            _sc.Camera.Pitch = 0;

            ScreenDuplicator duplicator = new ScreenDuplicator();
            _scene.AddRenderable(duplicator);

            _fsq = new FullScreenQuad();
            _scene.AddRenderable(_fsq);

            particlePlane = new ParticlePlane(_scene.Camera);
            //_scene.AddRenderable(particlePlane);

            spriteRenderable = new SpriteRenderable();
            _scene.AddRenderable(spriteRenderable);
            _scene.AddUpdateable(spriteRenderable);

            CreateGraphicsDeviceObjects();

            ImGui.StyleColorsClassic();
        }

        protected override void WindowResized()
        {
            _windowResized = true;
        }

        private void ChangeMsaa(int msaaOption)
        {
            TextureSampleCount sampleCount = (TextureSampleCount)msaaOption;
            _newSampleCount = sampleCount;
        }

        protected override void DisposeGraphicsDeviceObjects()
        {
            GraphicsDevice.WaitForIdle();

            _frameCommands.Dispose();
            StaticResourceCache.DisposeGraphicsDeviceObjects();

            _sc.DisposeGraphicsDeviceObjects();
            _scene.DestroyGraphicsDeviceObjects();

            GraphicsDevice.WaitForIdle();
        }

        protected override void CreateGraphicsDeviceObjects()
        {
            _frameCommands = GraphicsDevice.ResourceFactory.CreateCommandList();
            _frameCommands.Name = "Frame Commands List";

            using CommandList cl = GraphicsDevice.ResourceFactory.CreateCommandList();
            cl.Name = "Recreation Initialization Command List";
            cl.Begin();
            {
                _sc.CreateGraphicsDeviceObjects(GraphicsDevice, cl, _sc);
                _scene.CreateGraphicsDeviceObjects(GraphicsDevice, cl, _sc);
            }
            cl.End();
            GraphicsDevice.SubmitCommands(cl);

            _scene.Camera.UpdateGraphicsBackend(GraphicsDevice, Window);
        }

        public override void Update(in FrameTime time)
        {
            _imGuiRenderable.Update(time);

            _scene.Update(time);

            //particlePlane?.Update(time);

            //_sc.DirectionalLight.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Sin(time.TotalSeconds));

            if (InputTracker.GetKeyDown(Key.F11))
            {
                ToggleFullscreenState();
            }
        }

        public override void Draw()
        {
            if (_windowResized)
            {
                _windowResized = false;

                int width = Window.Width;
                int height = Window.Height;
                GraphicsDevice.ResizeMainWindow((uint)width, (uint)height);
                _scene.Camera.WindowResized(width, height);
                _resizeHandled?.Invoke(width, height);

                using CommandList cl = GraphicsDevice.ResourceFactory.CreateCommandList();
                cl.Begin();
                _sc.RecreateWindowSizedResources(GraphicsDevice, cl);
                cl.End();
                GraphicsDevice.SubmitCommands(cl);
            }

            if (_newSampleCount != null)
            {
                _sc.MainSceneSampleCount = _newSampleCount.Value;
                _newSampleCount = null;

                DisposeGraphicsDeviceObjects();
                CreateGraphicsDeviceObjects();
            }

            DrawMainMenu();

            _frameCommands.Begin();
            {
                while (_queuedRenderables.TryDequeue(out Renderable? renderable))
                {
                    _scene.AddRenderable(renderable);
                    renderable.CreateDeviceObjects(GraphicsDevice, _frameCommands, _sc);
                }

                _scene.RenderAllStages(GraphicsDevice, _frameCommands, _sc);
            }
            _frameCommands.End();

            GraphicsDevice.SubmitCommands(_frameCommands);
        }

        private void DrawMainMenu()
        {
            if (ImGui.BeginMainMenuBar())
            {
                DrawSettingsMenu();
                DrawWindowMenu();
                DrawDebugMenu();
                DrawRenderDocMenu();
                DrawControllerDebugMenu();

                ImGui.Separator();

                DrawTickTimes();

                ImGui.EndMainMenuBar();
            }
        }

        private void DrawTickTimes()
        {
            const string popupName = "Tick Time Popup";

            ImGui.SetNextWindowContentSize(new Vector2(180, 0));
            if (ImGui.BeginPopup(popupName))
            {
                ImGui.Columns(2);
                ImGui.Text("Update");
                ImGui.Text("Draw");
                ImGui.Text("Present");

                ImGui.NextColumn();
                ImGui.Text((TimeAverager.AverageUpdateSeconds * 1000).ToString("#00.00 ms"));
                ImGui.Text((TimeAverager.AverageDrawSeconds * 1000).ToString("#00.00 ms"));
                ImGui.Text((TimeAverager.AveragePresentSeconds * 1000).ToString("#00.00 ms"));

                ImGui.EndPopup();
            }

            if (ImGui.Button(TimeAverager.AverageTicksPerSecond.ToString("#00 fps")))
                ImGui.OpenPopup(popupName);
        }

        private void DrawSettingsMenu()
        {
            var gd = GraphicsDevice;

            if (ImGui.BeginMenu("Settings"))
            {
                if (ImGui.BeginMenu("Graphics Backend"))
                {
                    if (ImGui.MenuItem("Vulkan", string.Empty, gd.BackendType == GraphicsBackend.Vulkan, GraphicsDevice.IsBackendSupported(GraphicsBackend.Vulkan)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.Vulkan);
                    }
                    if (ImGui.MenuItem("OpenGL", string.Empty, gd.BackendType == GraphicsBackend.OpenGL, GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGL)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.OpenGL);
                    }
                    if (ImGui.MenuItem("OpenGL ES", string.Empty, gd.BackendType == GraphicsBackend.OpenGLES, GraphicsDevice.IsBackendSupported(GraphicsBackend.OpenGLES)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.OpenGLES);
                    }
                    if (ImGui.MenuItem("Direct3D 11", string.Empty, gd.BackendType == GraphicsBackend.Direct3D11, GraphicsDevice.IsBackendSupported(GraphicsBackend.Direct3D11)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.Direct3D11);
                    }
                    if (ImGui.MenuItem("Metal", string.Empty, gd.BackendType == GraphicsBackend.Metal, GraphicsDevice.IsBackendSupported(GraphicsBackend.Metal)))
                    {
                        ChangeGraphicsBackend(GraphicsBackend.Metal);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("MSAA"))
                {
                    if (ImGui.Combo("MSAA", ref _msaaOption, _msaaOptions, _msaaOptions.Length))
                    {
                        ChangeMsaa(_msaaOption);
                    }

                    ImGui.EndMenu();
                }

                bool tinted = _fsq.UseTintedTexture;
                if (ImGui.MenuItem("Tinted output", string.Empty, tinted, true))
                {
                    _fsq.UseTintedTexture = !tinted;
                }

                ImGui.EndMenu();
            }
        }

        private void DrawWindowMenu()
        {
            if (ImGui.BeginMenu("Window"))
            {
                bool isFullscreen = Window.WindowState == WindowState.BorderlessFullScreen;
                if (ImGui.MenuItem("Fullscreen", "F11", isFullscreen, true))
                {
                    ToggleFullscreenState();
                }

                if (ImGui.MenuItem("Always Recreate Sdl2Window", string.Empty, AlwaysRecreateWindow, true))
                {
                    AlwaysRecreateWindow = !AlwaysRecreateWindow;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Causes a new OS window to be created whenever the graphics backend is switched. This is much safer, and is the default.");
                }

                if (ImGui.MenuItem("sRGB Swapchain Format", string.Empty, SrgbSwapchain, true))
                {
                    SrgbSwapchain = !SrgbSwapchain;
                    ChangeGraphicsBackend(GraphicsDevice.BackendType);
                }

                bool vsync = GraphicsDevice.SyncToVerticalBlank;
                if (ImGui.MenuItem("VSync", string.Empty, vsync, true))
                {
                    GraphicsDevice.SyncToVerticalBlank = !GraphicsDevice.SyncToVerticalBlank;
                }

                bool resizable = Window.Resizable;
                if (ImGui.MenuItem("Resizable Window", string.Empty, resizable))
                {
                    Window.Resizable = !Window.Resizable;
                }

                bool bordered = Window.BorderVisible;
                if (ImGui.MenuItem("Visible Window Border", string.Empty, bordered))
                {
                    Window.BorderVisible = !Window.BorderVisible;
                }

                ImGui.EndMenu();
            }
        }

        private void DrawDebugMenu()
        {
            if (ImGui.BeginMenu("Debug"))
            {
                if (ImGui.MenuItem("Refresh Device Objects"))
                {
                    RefreshDeviceObjects(1);
                }

                if (ImGui.MenuItem("Refresh Device Objects (5 times)"))
                {
                    RefreshDeviceObjects(5);
                }

                if (_controllerTracker != null)
                {
                    if (ImGui.MenuItem("Controller State"))
                    {
                        _controllerDebugMenu = true;
                    }
                }
                else
                {
                    if (ImGui.MenuItem("Connect to Controller"))
                    {
                        Sdl2ControllerTracker.CreateDefault(out _controllerTracker);
                        _scene.Camera.Controller = _controllerTracker;
                    }
                }

                ImGui.EndMenu();
            }
        }

        private void DrawRenderDocMenu()
        {
            if (ImGui.BeginMenu("RenderDoc"))
            {
                if (_renderDoc == null)
                {
                    if (ImGui.MenuItem("Load"))
                    {
                        if (RenderDoc.Load(out _renderDoc))
                        {
                            ChangeGraphicsBackend(forceRecreateWindow: true, preferredBackend: GraphicsDevice.BackendType);
                        }
                    }
                }
                else
                {
                    if (ImGui.MenuItem("Trigger Capture"))
                    {
                        _renderDoc.TriggerCapture();
                    }

                    if (ImGui.BeginMenu("Options"))
                    {
                        bool allowVsync = _renderDoc.AllowVSync;
                        if (ImGui.Checkbox("Allow VSync", ref allowVsync))
                        {
                            _renderDoc.AllowVSync = allowVsync;
                        }

                        bool validation = _renderDoc.APIValidation;
                        if (ImGui.Checkbox("API Validation", ref validation))
                        {
                            _renderDoc.APIValidation = validation;
                        }

                        int delayForDebugger = (int)_renderDoc.DelayForDebugger;
                        if (ImGui.InputInt("Debugger Delay", ref delayForDebugger))
                        {
                            delayForDebugger = Math.Clamp(delayForDebugger, 0, int.MaxValue);
                            _renderDoc.DelayForDebugger = (uint)delayForDebugger;
                        }

                        bool verifyBufferAccess = _renderDoc.VerifyBufferAccess;
                        if (ImGui.Checkbox("Verify Buffer Access", ref verifyBufferAccess))
                        {
                            _renderDoc.VerifyBufferAccess = verifyBufferAccess;
                        }

                        bool overlayEnabled = _renderDoc.OverlayEnabled;
                        if (ImGui.Checkbox("Overlay Visible", ref overlayEnabled))
                        {
                            _renderDoc.OverlayEnabled = overlayEnabled;
                        }

                        bool overlayFrameRate = _renderDoc.OverlayFrameRate;
                        if (ImGui.Checkbox("Overlay Frame Rate", ref overlayFrameRate))
                        {
                            _renderDoc.OverlayFrameRate = overlayFrameRate;
                        }

                        bool overlayFrameNumber = _renderDoc.OverlayFrameNumber;
                        if (ImGui.Checkbox("Overlay Frame Number", ref overlayFrameNumber))
                        {
                            _renderDoc.OverlayFrameNumber = overlayFrameNumber;
                        }

                        bool overlayCaptureList = _renderDoc.OverlayCaptureList;
                        if (ImGui.Checkbox("Overlay Capture List", ref overlayCaptureList))
                        {
                            _renderDoc.OverlayCaptureList = overlayCaptureList;
                        }

                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem("Launch Replay UI"))
                    {
                        _renderDoc.LaunchReplayUI();
                    }
                }
                ImGui.EndMenu();
            }
        }

        private void DrawControllerDebugMenu()
        {
            if (_controllerDebugMenu)
            {
                if (ImGui.Begin("Controller State", ref _controllerDebugMenu, ImGuiWindowFlags.NoCollapse))
                {
                    if (_controllerTracker != null)
                    {
                        ImGui.Columns(2);

                        ImGui.Text($"Name: {_controllerTracker.ControllerName}");
                        foreach (SDL_GameControllerAxis axis in (SDL_GameControllerAxis[])Enum.GetValues(typeof(SDL_GameControllerAxis)))
                        {
                            ImGui.Text($"{axis}: {_controllerTracker.GetAxis(axis)}");
                        }

                        ImGui.NextColumn();

                        foreach (SDL_GameControllerButton button in (SDL_GameControllerButton[])Enum.GetValues(typeof(SDL_GameControllerButton)))
                        {
                            ImGui.Text($"{button}: {_controllerTracker.IsPressed(button)}");
                        }
                    }
                    else
                    {
                        ImGui.Text("No controller detected.");
                    }
                }
                ImGui.End();
            }
        }

        private void RefreshDeviceObjects(int numTimes = 1)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < numTimes; i++)
            {
                DisposeGraphicsDeviceObjects();
                CreateGraphicsDeviceObjects();
            }
            sw.Stop();
            Console.WriteLine($"Refreshing resources {numTimes} times took {sw.Elapsed.TotalSeconds} seconds.");
        }

        private static Skybox GetSkybox(string name)
        {
            var skybox = new Skybox((sc) =>
            {
                var front = Image.Load<Rgba32>(AssetHelper.GetPath("Textures", name + "_ft.png"));
                var back = Image.Load<Rgba32>(AssetHelper.GetPath("Textures", name + "_bk.png"));
                var left = Image.Load<Rgba32>(AssetHelper.GetPath("Textures", name + "_lf.png"));
                var right = Image.Load<Rgba32>(AssetHelper.GetPath("Textures", name + "_rt.png"));
                var top = Image.Load<Rgba32>(AssetHelper.GetPath("Textures", name + "_up.png"));
                var bottom = Image.Load<Rgba32>(AssetHelper.GetPath("Textures", name + "_dn.png"));

                var cubemap = new ImageSharpCubemapTexture(right, left, top, bottom, back, front, false);
                return cubemap;
            });

            skybox.TextureLoaded += (sc, cubemap) =>
            {
                foreach (Image<Rgba32>[] imageArray in cubemap.CubemapTextures)
                {
                    foreach (Image<Rgba32> image in imageArray)
                        image.Dispose();
                }
            };

            return skybox;
        }

        // Plz don't call this with the same texturePath and different mipmap values.
        private ImageSharpTexture LoadTexture(string texturePath, bool mipmap)
        {
            lock (_textures)
            {
                if (!_textures.TryGetValue(texturePath, out ImageSharpTexture? tex))
                {
                    tex = new ImageSharpTexture(texturePath, mipmap, true);
                    _textures.Add(texturePath, tex);
                }
                return tex;
            }
        }

        private void AddRenderable(Renderable renderable)
        {
            _queuedRenderables.Enqueue(renderable);
        }

        private void ToggleFullscreenState()
        {
            bool isFullscreen = Window.WindowState == WindowState.BorderlessFullScreen;
            Window.WindowState = isFullscreen ? WindowState.Normal : WindowState.BorderlessFullScreen;
        }
    }
}
