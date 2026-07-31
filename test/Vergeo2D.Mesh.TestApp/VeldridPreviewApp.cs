using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Vergeo2D.Mesh;
using Vergeo2D.Rendering;

internal sealed class VeldridPreviewApp : IDisposable
{
    private const int MinimumWidth = 1280;
    private const int MinimumHeight = 720;
    private const float PanelWidth = 280f;
    private static readonly Vector2 OverlayCheckboxOrigin = new(18f, 58f);
    private static readonly Vector2 IncludeTransparentCheckboxOrigin = new(18f, 218f);
    private static readonly Vector2 SpacingMinusOrigin = new(18f, 158f);
    private static readonly Vector2 SpacingPlusOrigin = new(110f, 158f);
    private static readonly Vector2 GenerateButtonOrigin = new(18f, 268f);
    private const float CheckboxSize = 18f;
    private static readonly Vector2 StepperButtonSize = new(30f, 24f);
    private static readonly Vector2 GenerateButtonSize = new(150f, 28f);

    private readonly string _imagePath;
    private readonly MeshTestBackend _backend;
    private readonly MeshRenderData2D _renderData = new();
    private readonly MeshRenderData2D _overlayRenderData = new();
    private readonly MeshGridOptions2D _gridOptions = new();
    private readonly RadialDragDeformer2D _dragDeformer = new();

    private Sdl2Window? _window;
    private GraphicsDevice? _graphicsDevice;
    private CommandList? _commandList;
    private MeshPreviewRenderer? _preview;
    private Solid2DRenderer? _solid;
    private UvOverlayRenderer? _uvOverlay;
    private Vector2 _imageSize;
    private Texture2D? _textureInfo;
    private ImageAlphaMask? _alphaMask;
    private Mesh2D? _mesh;
    private Mesh2D? _overlayMesh;
    private Vector2 _dragOriginImage;
    private bool _showUvOverlay;
    private bool _previewTransparent = true;
    private bool _isDragging;

    public VeldridPreviewApp(string imagePath, MeshTestBackend backend)
    {
        _imagePath = imagePath;
        _backend = backend;
    }

    public void Run()
    {
        var windowInfo = new WindowCreateInfo(
            100,
            100,
            MinimumWidth,
            MinimumHeight,
            WindowState.Normal,
            $"Vergeo2D.Mesh Test Render ({MeshBackendSmokeTest.GetBackendLabel(_backend)})");

        var options = new GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true,
            SyncToVerticalBlank = true
        };

        VeldridStartup.CreateWindowAndGraphicsDevice(
            windowInfo,
            options,
            ToGraphicsBackend(_backend),
            out _window,
            out _graphicsDevice);

        Console.WriteLine($"Renderer: {_graphicsDevice.BackendType}");

        _commandList = _graphicsDevice.ResourceFactory.CreateCommandList();
        _solid = new Solid2DRenderer(_graphicsDevice);
        GenerateMesh();

        while (_window.Exists)
        {
            var snapshot = _window.PumpEvents();
            if (!_window.Exists) break;

            ResizeIfNeeded();
            HandleInput(snapshot);
            DrawFrame();
        }
    }

    private void DrawFrame()
    {
        var graphicsDevice = _graphicsDevice!;
        var commandList = _commandList!;
        var viewport = GetViewport();
        if (viewport.X <= 0 || viewport.Y <= 0) return;

        var layout = GetImageLayout();

        commandList.Begin();
        commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);
        commandList.ClearColorTarget(0, new RgbaFloat(0.08f, 0.09f, 0.1f, 1f));

        var solid = _solid!;
        solid.Begin(commandList, viewport);

        if (_previewTransparent)
            DrawCheckerboard(viewport, layout.Origin, _imageSize * layout.Scale, layout.Scale);

        _preview?.Draw(commandList, viewport, layout.Origin, layout.Scale);
        DrawUi(viewport, layout.Origin, layout.Scale);

        commandList.End();
        graphicsDevice.SubmitCommands(commandList);
        graphicsDevice.SwapBuffers();
    }

    private void HandleInput(InputSnapshot snapshot)
    {
        var mousePosition = snapshot.MousePosition;
        var mousePoint = new Vector2(mousePosition.X, mousePosition.Y);

        foreach (var mouseEvent in snapshot.MouseEvents)
        {
            if (mouseEvent.MouseButton != MouseButton.Left) continue;

            if (mouseEvent.Down)
                OnMouseDown(mousePoint);
            else
                OnMouseUp();
        }

        if (_isDragging)
            OnMouseMove(mousePoint);
    }

    private void OnMouseDown(Vector2 point)
    {
        if (Contains(point, OverlayCheckboxOrigin, new Vector2(CheckboxSize + 130f, CheckboxSize)))
        {
            _showUvOverlay = !_showUvOverlay;
            return;
        }

        if (Contains(point, IncludeTransparentCheckboxOrigin, new Vector2(CheckboxSize + 190f, CheckboxSize)))
        {
            _previewTransparent = !_previewTransparent;
            return;
        }

        if (Contains(point, SpacingMinusOrigin, StepperButtonSize))
        {
            _gridOptions.Spacing = Math.Max(4, _gridOptions.Spacing - 4);
            GenerateMesh();
            return;
        }

        if (Contains(point, SpacingPlusOrigin, StepperButtonSize))
        {
            _gridOptions.Spacing = Math.Min(512, _gridOptions.Spacing + 4);
            GenerateMesh();
            return;
        }

        if (Contains(point, GenerateButtonOrigin, GenerateButtonSize))
        {
            GenerateMesh();
            return;
        }

        BeginImageDrag(point);
    }

    private void OnMouseMove(Vector2 position)
    {
        if (!_isDragging) return;

        var imagePoint = ScreenToImage(position);
        _dragDeformer.SetDrag(_dragOriginImage, imagePoint - _dragOriginImage);
        RefreshDeformedMesh();
    }

    private void OnMouseUp()
    {
        _isDragging = false;
        CommitDragToMesh();
    }

    private void DrawUi(Vector2 viewport, Vector2 imageOrigin, float imageScale)
    {
        var solid = _solid!;
        DrawPanel(solid, viewport.Y);
        DrawCheckbox(solid, OverlayCheckboxOrigin, _showUvOverlay);
        BitmapTextRenderer.Draw(solid, "UV OVERLAY", OverlayCheckboxOrigin + new Vector2(28f, 2f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));

        BitmapTextRenderer.Draw(solid, "GENERATOR", new Vector2(18f, 112f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));
        BitmapTextRenderer.Draw(solid, "SPACING", new Vector2(18f, 136f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));
        DrawButton(solid, SpacingMinusOrigin, StepperButtonSize, "-");
        DrawButton(solid, SpacingPlusOrigin, StepperButtonSize, "+");
        BitmapTextRenderer.Draw(solid, _gridOptions.Spacing.ToString(), new Vector2(58f, 163f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));

        DrawCheckbox(solid, IncludeTransparentCheckboxOrigin, _previewTransparent);
        BitmapTextRenderer.Draw(solid, "PREVIEW TRANSPARENT", IncludeTransparentCheckboxOrigin + new Vector2(28f, 2f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));
        DrawButton(solid, GenerateButtonOrigin, GenerateButtonSize, "GENERATE");

        BitmapTextRenderer.Draw(solid, $"VERTICES: {_overlayRenderData.VertexCount}", new Vector2(18f, 326f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));
        BitmapTextRenderer.Draw(solid, $"FACES: {_overlayRenderData.IndexCount / 3}", new Vector2(18f, 350f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));

        if (!_showUvOverlay) return;

        DrawCheckMark(solid);
        _uvOverlay!.Draw(solid, imageOrigin, imageScale);
    }

    private static void DrawPanel(Solid2DRenderer solid, float viewportHeight)
    {
        solid.DrawRect(Vector2.Zero, new Vector2(PanelWidth, viewportHeight), new Vector4(0.07f, 0.08f, 0.09f, 0.92f));
        BitmapTextRenderer.Draw(solid, "TEST MESH", new Vector2(18f, 20f), 2f, new Vector4(0.72f, 0.78f, 0.84f, 1f));
    }

    private void DrawCheckerboard(Vector2 viewport, Vector2 origin, Vector2 size, float imageScale)
    {
        var solid = _solid!;
        var squareSize = Math.Max(4f, MathF.Round(16f * imageScale));
        var columns = (int)MathF.Ceiling(size.X / squareSize);
        var rows = (int)MathF.Ceiling(size.Y / squareSize);
        var light = new Vector4(0.78f, 0.78f, 0.78f, 1f);
        var dark = new Vector4(0.64f, 0.64f, 0.64f, 1f);

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var rectOrigin = origin + new Vector2(x * squareSize, y * squareSize);
                var rectSize = new Vector2(
                    Math.Min(squareSize, origin.X + size.X - rectOrigin.X),
                    Math.Min(squareSize, origin.Y + size.Y - rectOrigin.Y));
                var color = ((x + y) & 1) == 0 ? light : dark;
                solid.DrawRect(rectOrigin, rectSize, color);
            }
        }
    }

    private static void DrawCheckbox(Solid2DRenderer solid, Vector2 origin, bool isChecked)
    {
        solid.DrawRect(origin, new Vector2(CheckboxSize, CheckboxSize), new Vector4(0.18f, 0.20f, 0.22f, 0.95f));
        solid.DrawLineLoop(RectVertices(origin, new Vector2(CheckboxSize, CheckboxSize)), new Vector4(0.85f, 0.88f, 0.90f, 1f));
        if (isChecked) DrawCheckMark(solid, origin);
    }

    private static void DrawButton(Solid2DRenderer solid, Vector2 origin, Vector2 size, string label)
    {
        solid.DrawRect(origin, size, new Vector4(0.16f, 0.18f, 0.20f, 0.95f));
        solid.DrawLineLoop(RectVertices(origin, size), new Vector4(0.55f, 0.62f, 0.68f, 1f));
        BitmapTextRenderer.Draw(solid, label, origin + new Vector2(8f, 7f), 2f, new Vector4(0.9f, 0.92f, 0.94f, 1f));
    }

    private static void DrawCheckMark(Solid2DRenderer solid)
    {
        DrawCheckMark(solid, OverlayCheckboxOrigin);
    }

    private static void DrawCheckMark(Solid2DRenderer solid, Vector2 origin)
    {
        var x = origin.X;
        var y = origin.Y;
        solid.DrawLines(new[]
        {
            x + 4f, y + 9f,
            x + 8f, y + 13f,
            x + 14f, y + 5f
        }, new Vector4(0.25f, 0.95f, 0.45f, 1f), lineStrip: true);
    }

    private static float[] RectVertices(Vector2 origin, Vector2 size)
    {
        var x = origin.X;
        var y = origin.Y;
        return new[]
        {
            x, y,
            x + size.X, y,
            x + size.X, y + size.Y,
            x, y + size.Y
        };
    }

    private void GenerateMesh()
    {
        _textureInfo = Texture2D.LoadFromFile(_imagePath);
        _alphaMask = ImageAlphaMask.Load(_imagePath);
        _imageSize = new Vector2(_textureInfo.Width, _textureInfo.Height);

        _mesh = MeshGridGenerator2D.GenerateConnectedGrid(_textureInfo, _gridOptions, _alphaMask);
        _overlayMesh = MeshGridGenerator2D.GenerateMaskedContourGrid(_textureInfo, _alphaMask, _gridOptions);
        _dragDeformer.Clear();
        _dragDeformer.Radius = Math.Max(120f, _gridOptions.Spacing * 3f);

        _renderData.Clear();
        MeshRenderExtractor.Extract(_mesh, deformer: null, _renderData);
        _overlayRenderData.Clear();
        MeshRenderExtractor.Extract(_overlayMesh, deformer: null, _overlayRenderData);

        if (_preview is null)
            _preview = new MeshPreviewRenderer(_graphicsDevice!, _imagePath, _renderData);
        else
            _preview.Update(_renderData);

        _uvOverlay = new UvOverlayRenderer(_overlayRenderData);
        Console.WriteLine($"Generated shape mesh: {_overlayRenderData.VertexCount} vertices, {_overlayRenderData.IndexCount / 3} faces, spacing {_gridOptions.Spacing}");
    }

    private void BeginImageDrag(Vector2 screenPoint)
    {
        if (_mesh is null || _overlayMesh is null || _alphaMask is null || screenPoint.X < PanelWidth) return;
        var imagePoint = ScreenToImage(screenPoint);
        if (!IsInsideImage(imagePoint)) return;
        if (!_alphaMask.IsOpaqueAt(imagePoint.X, imagePoint.Y)) return;
        if (_overlayMesh.FindFaceAt(imagePoint) < 0) return;

        _dragOriginImage = imagePoint;
        _dragDeformer.SetDrag(_dragOriginImage, Vector2.Zero);
        _isDragging = true;
        RefreshDeformedMesh();
    }

    private void RefreshDeformedMesh()
    {
        if (_mesh is null || _overlayMesh is null || _preview is null) return;

        _renderData.Clear();
        MeshRenderExtractor.Extract(_mesh, _dragDeformer.HasDrag ? _dragDeformer : null, _renderData);
        _overlayRenderData.Clear();
        MeshRenderExtractor.Extract(_overlayMesh, _dragDeformer.HasDrag ? _dragDeformer : null, _overlayRenderData);
        _preview.Update(_renderData);
        _uvOverlay = new UvOverlayRenderer(_overlayRenderData);
    }

    private void CommitDragToMesh()
    {
        if (_mesh is null || _overlayMesh is null || !_dragDeformer.HasDrag) return;

        _mesh.ApplyDeformer(_dragDeformer);
        _overlayMesh.ApplyDeformer(_dragDeformer);

        _dragDeformer.Clear();
        RefreshDeformedMesh();
    }

    private Vector2 ScreenToImage(Vector2 screenPoint)
    {
        var layout = GetImageLayout();
        return layout.ScreenToContent(screenPoint);
    }

    private bool IsInsideImage(Vector2 imagePoint)
    {
        return GetImageLayout().ContainsContentPoint(imagePoint);
    }

    private MeshViewportLayout2D GetImageLayout()
    {
        var viewport = GetViewport();
        return MeshViewportLayout2D.Fit(_imageSize, viewport);
    }

    private Vector2 GetViewport()
    {
        var framebuffer = _graphicsDevice!.SwapchainFramebuffer;
        return new Vector2(framebuffer.Width, framebuffer.Height);
    }

    private void ResizeIfNeeded()
    {
        var width = Math.Max(1, _window!.Width);
        var height = Math.Max(1, _window.Height);
        var framebuffer = _graphicsDevice!.SwapchainFramebuffer;
        if (framebuffer.Width != width || framebuffer.Height != height)
            _graphicsDevice.ResizeMainWindow((uint)width, (uint)height);
    }

    private static bool Contains(Vector2 point, Vector2 origin, Vector2 size)
    {
        return
            point.X >= origin.X &&
            point.X <= origin.X + size.X &&
            point.Y >= origin.Y &&
            point.Y <= origin.Y + size.Y;
    }

    private static GraphicsBackend ToGraphicsBackend(MeshTestBackend backend)
    {
        return backend switch
        {
            MeshTestBackend.OpenGL => GraphicsBackend.OpenGL,
            MeshTestBackend.Vulkan => GraphicsBackend.Vulkan,
            MeshTestBackend.DirectX => GraphicsBackend.Direct3D11,
            _ => GraphicsBackend.OpenGL
        };
    }

    public void Dispose()
    {
        _graphicsDevice?.WaitForIdle();
        _preview?.Dispose();
        _solid?.Dispose();
        _commandList?.Dispose();
        _graphicsDevice?.Dispose();
    }
}
